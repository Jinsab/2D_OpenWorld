#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles the scene loading process including the LoadStateMachine.
    /// </summary>
    public class SceneLoadManager
    {
        readonly SaveManager manager;

        public SceneLoadManager(SaveManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// Initiates the load process by creating and running the LoadStateMachine.
        /// </summary>
        public IEnumerator StartLoadProcess(SaveSlot slot, bool restoreScene, bool asyncLoad, bool allowActivation, CancellationToken token)
        {
            manager.StateMachine.TransitionTo(SaveState.Loading);
            LoadStateMachine loadStateMachine = new LoadStateMachine(this, manager, slot, restoreScene, asyncLoad, allowActivation, token);
            yield return manager.StartCoroutine(loadStateMachine.Run());
            if (manager.StateMachine.CurrentState != SaveState.Error)
                manager.StateMachine.TransitionTo(SaveState.Idle);
        }

        /// <summary>
        /// Asynchronously loads a save slot and awaits its completion.
        /// </summary>
        public async Task<LoadResult> LoadSaveSlotAsync(int slotNumber = 1,
            bool restoreLastActiveScene = false,
            bool loadAsync = false,
            bool allowSceneActivation = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (manager.IsLoading)
            {
                Logger.Log($"LoadSaveSlotAsync: Cannot initiate load for slot {slotNumber} because another load is already running.", LogCategory.SceneManagement, LogLevel.Warning);
                return new LoadResult(false, "Another load operation is already in progress.");
            }

            var tcs = new TaskCompletionSource<LoadResult>();
            if (!manager.RegisterLoadCompletion(slotNumber, tcs))
            {
                Logger.Log($"LoadSaveSlotAsync: Cannot initiate load for slot {slotNumber} as another load is in progress for this slot.", LogCategory.SceneManagement, LogLevel.Warning);
                return new LoadResult(false, "Another load operation is already in progress for this slot.");
            }

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (timeout.HasValue)
                    linkedCts.CancelAfter(timeout.Value);

                manager.Load(slotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation, linkedCts.Token);

                try
                {
                    LoadResult result = await tcs.Task.ConfigureAwait(false);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    Logger.Log($"LoadSaveSlotAsync: Load operation for slot {slotNumber} was canceled.", LogCategory.SceneManagement, LogLevel.Warning);
                    return new LoadResult(false, "Load operation was canceled.");
                }
                catch (Exception ex)
                {
                    Logger.Log($"LoadSaveSlotAsync: Exception during load operation for slot {slotNumber}: {ex.Message}", LogCategory.SceneManagement, LogLevel.Error);
                    return new LoadResult(false, $"Exception during load: {ex.Message}");
                }
                finally
                {
                    manager.UnregisterLoadCompletion(slotNumber);
                    linkedCts.Dispose();
                }
            }
        }

        IEnumerator WaitForLoadTask(Task<byte[]> loadTask, Action<byte[]> onSuccess, Action<Exception> onFailure)
        {
            while (!loadTask.IsCompleted)
                yield return null;
            if (loadTask.IsFaulted)
                onFailure?.Invoke(loadTask.Exception?.InnerException ?? loadTask.Exception);
            else
                onSuccess?.Invoke(loadTask.Result);
        }

        void DestroyAllKASPrefabs()
        {
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
            SaveablePrefab[] allSaveables = GameObject.FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
            foreach (var sp in allSaveables)
            {
                if (sp.KeepAcrossScenes)
                {
                    Logger.Log($"DestroyAllKASPrefabs: Destroying leftover KAS prefab '{sp.name}' (ID: {sp.UniqueID}).");
                    DestroyHelper.DestroyWithLogging(sp.gameObject, "DestroyAllKASPrefabs");
                }
            }
        }

        class LoadStateMachine
        {
            readonly SceneLoadManager owner;
            enum LoadState
            {
                Start,
                LoadScene,
                DeserializeData,
                VersionCheck,
                InstantiatePrefabs,
                ApplyComponents,
                ApplyGameObjectStates,
                HandleDestroyedObjects,
                Finalize,
                Completed,
                Failed
            }

            readonly SaveManager saveManager;
            readonly SaveSlot slot;
            readonly bool restoreLastActiveScene;
            readonly bool loadAsync;
            readonly bool allowSceneActivationFlag;
            readonly CancellationToken cancellationToken;

            LoadState currentState = LoadState.Start;
            SaveData loadedData;
            bool migrationPerformed = false;

            public LoadStateMachine(SceneLoadManager owner, SaveManager manager, SaveSlot saveSlot, bool restoreScene, bool asyncLoad, bool allowActivation, CancellationToken token)
            {
                this.owner = owner;
                saveManager = manager;
                slot = saveSlot;
                restoreLastActiveScene = restoreScene;
                loadAsync = asyncLoad;
                allowSceneActivationFlag = allowActivation;
                cancellationToken = token;
            }

            IEnumerator ExecuteStep(LoadState state, LoadState nextState, Func<IEnumerator> action)
            {
                Logger.Log($"LoadStateMachine: Entering {state} state.", LogCategory.SceneManagement);
                yield return action();
                if (currentState == state)
                {
                    Logger.Log($"LoadStateMachine: Exiting {state} state.", LogCategory.SceneManagement);
                    currentState = nextState;
                }
            }

            public IEnumerator Run()
            {
                Logger.Log($"LoadStateMachine: Starting load process for slot {slot.SlotNumber}.", LogCategory.SceneManagement, LogLevel.Info);
                while (currentState != LoadState.Completed && currentState != LoadState.Failed)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger.Log($"LoadStateMachine: Load operation for slot {slot.SlotNumber} was canceled.", LogCategory.SceneManagement, LogLevel.Warning);
                        saveManager.HandleLoadCompletion(slot.SlotNumber, false, "Load operation was canceled.");
                        saveManager.StateMachine.TransitionTo(SaveState.Error);
                        currentState = LoadState.Failed;
                        yield break;
                    }

                    switch (currentState)
                    {
                        case LoadState.Start:
                            yield return ExecuteStep(LoadState.Start, LoadState.LoadScene, StartLoadProcess);
                            break;
                        case LoadState.LoadScene:
                            yield return ExecuteStep(LoadState.LoadScene, LoadState.DeserializeData, LoadSceneCoroutine);
                            break;
                        case LoadState.DeserializeData:
                            yield return ExecuteStep(LoadState.DeserializeData, LoadState.VersionCheck, DeserializeDataCoroutine);
                            break;
                        case LoadState.VersionCheck:
                            yield return ExecuteStep(LoadState.VersionCheck, LoadState.InstantiatePrefabs, VersionCheckCoroutine);
                            break;
                        case LoadState.InstantiatePrefabs:
                            yield return ExecuteStep(LoadState.InstantiatePrefabs, LoadState.ApplyComponents, InstantiatePrefabsCoroutine);
                            break;
                        case LoadState.ApplyComponents:
                            yield return ExecuteStep(LoadState.ApplyComponents, LoadState.ApplyGameObjectStates, ApplyComponentsCoroutine);
                            break;
                        case LoadState.ApplyGameObjectStates:
                            yield return ExecuteStep(LoadState.ApplyGameObjectStates, LoadState.HandleDestroyedObjects, ApplyGameObjectStatesCoroutine);
                            break;
                        case LoadState.HandleDestroyedObjects:
                            yield return ExecuteStep(LoadState.HandleDestroyedObjects, LoadState.Finalize, HandleDestroyedObjectsCoroutine);
                            break;
                        case LoadState.Finalize:
                            Logger.Log("LoadStateMachine: Entering Finalize state.", LogCategory.SceneManagement);
                            FinalizeLoad();
                            Logger.Log("LoadStateMachine: Exiting Finalize state.", LogCategory.SceneManagement);
                            if (saveManager.SaveSettings.autoSaveMigratedData && migrationPerformed)
                                yield return saveManager.StartCoroutine(saveManager.SaveOperations.SaveCoroutine(slot, loadedData.LastActiveScene));
                            currentState = LoadState.Completed;
                            break;
                    }
                    yield return null;
                }

                if (currentState == LoadState.Completed)
                {
                    saveManager.HandleLoadCompletion(slot.SlotNumber, true);
                    saveManager.StateMachine.TransitionTo(SaveState.Idle);
                    Logger.Log($"LoadStateMachine: Load process for slot {slot.SlotNumber} completed successfully.", LogCategory.SceneManagement, LogLevel.Info);
                }
                else if (currentState == LoadState.Failed)
                {
                    Logger.Log($"LoadStateMachine: Load process for slot {slot.SlotNumber} failed.", LogCategory.SceneManagement, LogLevel.Error);
                }
            }

            IEnumerator StartLoadProcess()
            {
                yield return null;
            }

            IEnumerator LoadSceneCoroutine()
            {
                if (restoreLastActiveScene && !string.IsNullOrEmpty(slot.LastActiveScene))
                {
                    string sceneName = slot.LastActiveScene;
                    // If the scene isn't in the Build Settings or an AssetBundle, skip loading gracefully
                    if (!Application.CanStreamedLevelBeLoaded(sceneName))
                    {
                        Logger.Log($"LoadStateMachine: Scene '{sceneName}' is not available (not in Build Settings or bundle not loaded). Skipping scene restore and continuing load.", LogCategory.SceneManagement, LogLevel.Warning);
                        yield break; // proceed to next state (DeserializeData)
                    }

                    saveManager.RaiseSceneLoadStarted(sceneName);
                    Logger.Log($"LoadStateMachine: Initiating loading of scene '{sceneName}'.", LogCategory.SceneManagement, LogLevel.Info);

                    if (loadAsync)
                    {
                        Logger.Log($"LoadStateMachine: Loading scene '{sceneName}' asynchronously.", LogCategory.SceneManagement, LogLevel.Info);
                        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
                        if (asyncLoad == null)
                        {
                            Logger.Log($"LoadStateMachine: Scene '{sceneName}' returned null AsyncOperation; treating as unavailable and skipping scene restore.", LogCategory.SceneManagement, LogLevel.Warning);
                            yield break; // proceed to next state
                        }

                        asyncLoad.allowSceneActivation = allowSceneActivationFlag;

                        while (!asyncLoad.isDone)
                        {
                            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                            saveManager.RaiseSceneLoadProgress(progress);
                            yield return null;
                            if (cancellationToken.IsCancellationRequested)
                            {
                                Logger.Log($"LoadStateMachine: Load operation for slot {slot.SlotNumber} was canceled during scene loading.", LogCategory.SceneManagement, LogLevel.Warning);
                                saveManager.HandleLoadCompletion(slot.SlotNumber, false, "Load operation was canceled during scene loading.");
                                saveManager.StateMachine.TransitionTo(SaveState.Error);
                                currentState = LoadState.Failed;
                                yield break;
                            }
                            if (!allowSceneActivationFlag && asyncLoad.progress >= 0.9f)
                            {
                                saveManager.RaiseSceneActivationRequested(sceneName);
                                Logger.Log($"LoadStateMachine: Scene '{sceneName}' loaded to 90%. Awaiting activation.", LogCategory.SceneManagement, LogLevel.Info);
                                break;
                            }
                        }

                        if (allowSceneActivationFlag)
                        {
                            saveManager.RaiseSceneLoadCompleted(sceneName);
                            Logger.Log($"LoadStateMachine: Successfully loaded scene '{sceneName}' asynchronously.", LogCategory.SceneManagement, LogLevel.Info);
                        }
                    }
                    else
                    {
                        try
                        {
                            Logger.Log($"LoadStateMachine: Loading scene '{sceneName}' synchronously.", LogCategory.SceneManagement, LogLevel.Info);
                            SceneManager.LoadScene(sceneName);
                            saveManager.RaiseSceneLoadCompleted(sceneName);
                            Logger.Log($"LoadStateMachine: Successfully loaded scene '{sceneName}' synchronously.", LogCategory.SceneManagement, LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                            // Gracefully skip scene restore and continue
                            Logger.Log($"LoadStateMachine: Scene '{sceneName}' failed to load synchronously ({ex.Message}). Skipping scene restore and continuing load.", LogCategory.SceneManagement, LogLevel.Warning);
                            yield break;
                        }
                    }
                }
                yield break;
            }

            IEnumerator DeserializeDataCoroutine()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, "Load operation was canceled before deserialization.");
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }

                byte[] serializedData = null;
                Exception loadException = null;
                bool loadCompleted = false;

                if (saveManager.SaveSettings.enableCloudSave)
                {
                    Task<byte[]> loadTask = saveManager.SaveSystem.LoadAsync(slot);
                    yield return owner.WaitForLoadTask(loadTask, r => { serializedData = r; loadCompleted = true; }, ex => { loadException = ex; loadCompleted = true; });
                }
                else
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        try
                        {
                            serializedData = saveManager.SaveSystem.Load(slot);
                            Logger.Log($"Deserialization for save slot {slot.SlotNumber} started.", LogCategory.SceneManagement, LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                            loadException = ex;
                        }
                        finally
                        {
                            loadCompleted = true;
                        }
                    });
                }

                while (!loadCompleted)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        saveManager.HandleLoadCompletion(slot.SlotNumber, false, "Load operation was canceled during deserialization.");
                        saveManager.StateMachine.TransitionTo(SaveState.Error);
                        currentState = LoadState.Failed;
                        yield break;
                    }
                    yield return null;
                }

                if (serializedData == null || serializedData.Length == 0)
                {
                    string msg = "No data returned for the requested slot.";
                    Logger.Log($"Load failed for slot {slot.SlotNumber}: {msg}", LogCategory.SceneManagement, LogLevel.Error);
                    var failureArgs = new OperationFailedEventArgs(slot, "Load", msg);
                    saveManager.RaiseLoadFailed(failureArgs);
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, msg);
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }

                if (loadException != null)
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        Logger.Log($"Load failed during deserialization for slot {slot.SlotNumber}: {loadException.Message}", LogCategory.SceneManagement, LogLevel.Error);
                        var failureArgs = new OperationFailedEventArgs(slot, "Load", loadException.Message);
                        saveManager.RaiseLoadFailed(failureArgs);
                        saveManager.TrySetLoadCompletion(slot.SlotNumber, false, loadException.Message);
                    });
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, "Load operation was canceled after deserialization.");
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }

                bool enc = serializedData.Length > 33 &&
                    serializedData[0] == (byte)'C' &&
                    serializedData[1] == (byte)'S' &&
                    serializedData[2] == (byte)'A' &&
                    serializedData[3] == (byte)'V' &&
                    serializedData[4] == 1;

                byte[] decryptedData;
                if (enc)
                {
                    decryptedData = saveManager.EncryptionService?.MaybeDecrypt(serializedData);
                    if (decryptedData == null)
                    {
                        string msg = "Save file was encrypted with a different master secret.";
                        Logger.Log($"Load failed during deserialization for slot {slot.SlotNumber}: {msg}", LogCategory.SceneManagement, LogLevel.Error);
                        var args = new OperationFailedEventArgs(slot, "Load", msg);
                        saveManager.RaiseLoadFailed(args);
                        saveManager.HandleLoadCompletion(slot.SlotNumber, false, msg);
                        saveManager.StateMachine.TransitionTo(SaveState.Error);
                        currentState = LoadState.Failed;
                        yield break;
                    }
                    serializedData = decryptedData;
                }

                serializedData = saveManager.SaveOperations.MaybeDecompress(serializedData);

                try
                {
                    loadedData = saveManager.Serializer.Deserialize<SaveData>(serializedData);
                    Logger.Log($"Deserialization for save slot {slot.SlotNumber} completed successfully.", LogCategory.SceneManagement, LogLevel.Info);
                    if (loadedData.TrackedUniqueIDs == null)
                        loadedData.TrackedUniqueIDs = new List<string>();
                    if (loadedData.DestroyedGameObjects == null)
                        loadedData.DestroyedGameObjects = new List<string>();

                    saveManager.CurrentSaveData = loadedData;
                    // Import persisted RememberHome snapshots into the ComponentManager
                    try
                    {
                        saveManager.ComponentManager?.ImportHomeSceneSnapshots(loadedData.HomeSceneComponentSnapshots, loadedData.HomeScenePrefabAssetIDs);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[RememberHome] Import during load failed: {ex.Message}", LogCategory.SceneManagement, LogLevel.Warning);
                    }
                    saveManager.GameObjectTracker.DestroyedIDs.Clear();
                    foreach (var id in loadedData.DestroyedGameObjects)
                        saveManager.GameObjectTracker.DestroyedIDs.Add(id);

                    Logger.Log($"DeserializeDataCoroutine: {loadedData.DestroyedGameObjects.Count} destroyed GameObjects loaded.", LogCategory.SceneManagement, LogLevel.Info);
                    foreach (var destroyedID in loadedData.DestroyedGameObjects)
                        Logger.Log($"DeserializeDataCoroutine: DestroyedID '{destroyedID}' loaded.", LogCategory.SceneManagement, LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Load failed during deserialization for slot {slot.SlotNumber}: {ex.Message}", LogCategory.SceneManagement, LogLevel.Error);
                    var failureArgs = new OperationFailedEventArgs(slot, "Load", ex.Message);
                    saveManager.RaiseLoadFailed(failureArgs);
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, ex.Message);
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }

                if (loadedData.Version != null && saveManager.VersionManager != null && saveManager.VersioningService != null)
                {
                    if (loadedData.Version.CompareTo(saveManager.VersionManager.CurrentVersion) < 0)
                    {
                        Logger.Log($"LoadStateMachine: SaveData version {loadedData.Version} is older than current version {saveManager.VersionManager.CurrentVersion}. Initiating migration.", LogCategory.SceneManagement, LogLevel.Info);
                        try
                        {
                            saveManager.VersioningService.Migrate(loadedData);
                            Logger.Log($"LoadStateMachine: Migration completed. Updated SaveData version to {loadedData.Version}.", LogCategory.SceneManagement, LogLevel.Info);
                            if (saveManager.SaveSettings.autoSaveMigratedData)
                                migrationPerformed = true;
                        }
                        catch (Exception migEx)
                        {
                            Logger.Log($"LoadStateMachine: Migration failed for slot {slot.SlotNumber}: {migEx.Message}", LogCategory.SceneManagement, LogLevel.Error);
                            var failureArgs = new OperationFailedEventArgs(slot, "Load", $"Migration failed: {migEx.Message}");
                            saveManager.RaiseLoadFailed(failureArgs);
                            saveManager.HandleLoadCompletion(slot.SlotNumber, false, $"Migration failed: {migEx.Message}");
                            saveManager.StateMachine.TransitionTo(SaveState.Error);
                            currentState = LoadState.Failed;
                            yield break;
                        }
                    }
                    else
                    {
                        Logger.Log($"LoadStateMachine: SaveData version {loadedData.Version} is up-to-date.", LogCategory.SceneManagement, LogLevel.Info);
                    }
                }
                else
                {
                    Logger.Log("LoadStateMachine: VersionInfo or MigrationManager is null. Cannot perform migration.", LogCategory.SceneManagement, LogLevel.Error);
                }

                yield break;
            }

            IEnumerator VersionCheckCoroutine()
            {
                try
                {
                    if (loadedData == null)
                    {
                        Logger.Log($"VersionCheck failed: no save data for slot {slot.SlotNumber}.", LogCategory.SceneManagement, LogLevel.Error);
                        saveManager.HandleLoadCompletion(slot.SlotNumber, false, "No save data found.");
                        saveManager.StateMachine.TransitionTo(SaveState.Error);
                        yield break;
                    }

                    if (loadedData.Version == null)
                    {
                        Logger.Log("Load failed: Save data version is null.", LogCategory.SceneManagement, LogLevel.Error);
                        var failureArgs = new OperationFailedEventArgs(slot, "Load", "Save data version is null.");
                        saveManager.RaiseLoadFailed(failureArgs);
                        saveManager.HandleLoadCompletion(slot.SlotNumber, false, "Incompatible save data version.");
                        saveManager.StateMachine.TransitionTo(SaveState.Error);
                        saveManager.TrySetLoadCompletion(slot.SlotNumber, false, "Incompatible save data version.");
                        currentState = LoadState.Failed;
                        yield break;
                    }

                    VersionComparisonResult result = saveManager.VersionManager.CompareVersion(loadedData.Version);
                    saveManager.HandleVersionResult(result, loadedData);
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Log($"Load aborted due to version incompatibility: {ex.Message}", LogCategory.SceneManagement, LogLevel.Error);
                    var failureArgs = new OperationFailedEventArgs(slot, "Load", ex.Message);
                    saveManager.RaiseLoadFailed(failureArgs);
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, ex.Message);
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }
                catch (Exception ex)
                {
                    Logger.Log($"VersionCheckCoroutine error: {ex.Message}", LogCategory.SceneManagement, LogLevel.Error);
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, ex.Message);
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    yield break;
                }
                yield break;
            }

            IEnumerator InstantiatePrefabsCoroutine()
            {
                SaveManager.Instance.StateMachine.TransitionTo(SaveState.Loading);
                IEnumerator instantiationCoroutine = null;
                try
                {
                    var prefabManager = saveManager.GetPrefabManager;
                    var destroyedIds = saveManager.GameObjectTracker.GetDestroyedGameObjectIDs();
                    var immediatePrefabs = prefabManager.PrepareImmediatePrefabs(loadedData.Prefabs);

                    instantiationCoroutine = prefabManager.InstantiatePrefabsCoroutine(
                        immediatePrefabs,
                        destroyedIds,
                        clearExistingPrefabs: true,
                        handleDeferral: false);
                }
                catch (Exception ex)
                {
                    Logger.Log($"LoadStateMachine: Failed to start prefab instantiation for slot {slot.SlotNumber}: {ex.Message}", LogCategory.SceneManagement, LogLevel.Error);
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, $"Starting prefab instantiation failed: {ex.Message}");
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }

                if (instantiationCoroutine != null)
                {
                    while (instantiationCoroutine.MoveNext())
                        yield return instantiationCoroutine.Current;
                }
                else
                {
                    yield break;
                }
            }

            IEnumerator ApplyComponentsCoroutine()
            {
                SaveableComponent.ResetLoadCallCounts();
                saveManager.ComponentManager.ResetDeserializedComponents();
                saveManager.ComponentManager.InstantiateMissingHomeSceneObjectsForLoadedScenes();
                int batchSize = saveManager.SaveSettings?.componentApplyBatchSize ?? 0;
                if (batchSize > 0)
                {
                    var compCoroutine = saveManager.ComponentManager.ApplyComponentDataCoroutine(loadedData, batchSize);
                    while (compCoroutine.MoveNext())
                        yield return compCoroutine.Current;
                }
                else
                {
                    saveManager.ComponentManager.ApplyComponentData(loadedData);
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    saveManager.HandleLoadCompletion(slot.SlotNumber, false, "Load operation was canceled after applying components.");
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }
                var dupLoads = SaveableComponent.LoadCallCounts.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
                if (dupLoads.Count > 0)
                    Debug.LogWarning($"ApplyComponentsCoroutine: Duplicate LoadData calls for {string.Join(", ", dupLoads)}");
                else
                yield return null;
            }

            IEnumerator ApplyGameObjectStatesCoroutine()
            {
                try
                {
                    if (loadedData == null || loadedData.GameObjectStates == null)
                    {
                        Logger.Log("LoadStateMachine: No GameObjectStates to apply (loadedData or GameObjectStates is null).", LogCategory.SceneManagement, LogLevel.Warning);
                    }
                    else
                    {
                        Logger.Log($"LoadStateMachine: Applying active states for {loadedData.GameObjectStates.Count} GameObjects.", LogCategory.SceneManagement, LogLevel.Info);
                        saveManager.ApplyGameObjectActiveStates(loadedData.GameObjectStates);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"ApplyGameObjectStatesCoroutine error: {ex.Message}", LogCategory.SceneManagement, LogLevel.Error);
                }
                yield return null;
            }

            IEnumerator HandleDestroyedObjectsCoroutine()
            {
                try
                {
                    if (loadedData == null || loadedData.DestroyedGameObjects == null)
                    {
                        Logger.Log("HandleDestroyedObjectsCoroutine: No destroyed-object list to process.", LogCategory.SceneManagement, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log($"HandleDestroyedObjectsCoroutine: Processing {loadedData.DestroyedGameObjects.Count} destroyed GameObjects.", LogCategory.SceneManagement, LogLevel.Info);
                        foreach (string destroyedID in loadedData.DestroyedGameObjects)
                        {
                            if (string.IsNullOrEmpty(destroyedID))
                            {
                                Logger.Log("HandleDestroyedObjectsCoroutine: Encountered null or empty destroyedID; skipping.", LogCategory.SceneManagement, LogLevel.Info);
                                continue;
                            }

                            // Debug logging for Quad objects
                            if (destroyedID.Contains("Quad"))
                            {
                                UnityEngine.Debug.Log($"[SceneLoadManager.HandleDestroyedObjects] Processing destroyed ID '{destroyedID}' - looking for GameObject to destroy");
                            }

                            GameObject go = saveManager.FindGameObjectByUniqueID(destroyedID, SaveManager.IdentifierType.UniqueID);
                            if (go != null)
                            {
                                if (go.name.Contains("Quad"))
                                {
                                    UnityEngine.Debug.Log($"[SceneLoadManager.HandleDestroyedObjects] DESTROYING GameObject '{go.name}' with ID '{destroyedID}' in scene '{go.scene.name}' - was saved as destroyed");
                                }
                                DestroyHelper.DestroyWithLogging(go, "LoadStateMachine.HandleDestroyedObjects: Removing destroyed GameObject");
                                Logger.Log($"LoadStateMachine: Destroyed GameObject with ID '{destroyedID}' according to saved data.", LogCategory.SceneManagement, LogLevel.Info);
                            }
                            else
                            {
                                if (destroyedID.Contains("Quad"))
                                {
                                    UnityEngine.Debug.Log($"[SceneLoadManager.HandleDestroyedObjects] GameObject with ID '{destroyedID}' not found for destruction - already destroyed or missing");
                                }
                                Logger.Log($"HandleDestroyedObjectsCoroutine: GameObject with ID '{destroyedID}' not found.", LogCategory.SceneManagement, LogLevel.Info);
                            }
                        }
                    }

                    string currentSceneName = SceneManager.GetActiveScene().name;

                    foreach (var component in saveManager.SaveableComponents)
                    {
                        if (component == null)
                            continue;

                        if (component is PersistentVisibilityController pvc)
                            pvc.ApplyVisibilityBasedOnScene(currentSceneName);
                    }

                    var prefabManager = saveManager.GetPrefabManager;
                    if (prefabManager != null)
                    {
                        foreach (var prefab in prefabManager.SaveablePrefabs.ToArray())
                        {
                            if (prefab == null)
                                continue;

                            if (prefab.TryGetComponent(out PersistentVisibilityController prefabPvc))
                                prefabPvc.ApplyVisibilityBasedOnScene(currentSceneName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"HandleDestroyedObjectsCoroutine error: {ex.Message}", LogCategory.SceneManagement, LogLevel.Error);
                }
                yield return null;
            }

            void FinalizeLoad()
            {
                Logger.Log("LoadStateMachine: Load successful.", LogCategory.SceneManagement, LogLevel.Info);
                if (saveManager.EnforceActiveState && loadedData != null && loadedData.GameObjectStates != null)
                    saveManager.ApplyGameObjectActiveStates(loadedData.GameObjectStates);

                if (loadedData != null && loadedData.GameObjectStates != null)
                    saveManager.StartActiveStateWatch(loadedData.GameObjectStates);

                Physics.Simulate(Time.fixedDeltaTime);

                // After physics settle, ensure visibility is correct for the active scene once more
                try
                {
                    string sceneName = SceneManager.GetActiveScene().name;
                    foreach (var component in saveManager.SaveableComponents)
                    {
                        if (component == null)
                            continue;

                        if (component is PersistentVisibilityController pvc)
                            pvc.ApplyVisibilityBasedOnScene(sceneName);
                    }

                    var prefabManager = saveManager.GetPrefabManager;
                    if (prefabManager != null)
                    {
                        foreach (var prefab in prefabManager.SaveablePrefabs.ToArray())
                        {
                            if (prefab == null)
                                continue;

                            if (prefab.TryGetComponent(out PersistentVisibilityController prefabPvc))
                                prefabPvc.ApplyVisibilityBasedOnScene(sceneName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"FinalizeLoad visibility pass error: {ex.Message}", LogCategory.SceneManagement, LogLevel.Warning);
                }
            }
        }

        internal IEnumerator RunLoadCoroutine(SaveSlot slot, bool restoreLastActiveScene, bool loadAsync, bool allowSceneActivation, CancellationToken cancellationToken)
        {
            if (manager.SaveSettings.enableCloudSave)
            {
                var waitTask = manager.WaitForCloudConnectionAsync();
                yield return manager.AwaitTask(waitTask);
                if (!waitTask.Result)
                {
                    manager.HandleLoadCompletion(slot.SlotNumber, false, "Unable to load: connection to cloud provider not established.");
                    manager.ReleaseLoadLock(slot.SlotNumber);
                    manager.IsLoadingInternal = false;
                    yield break;
                }
            }

            #if UNITY_2022_1_OR_NEWER
            var prev3DMode = Physics.simulationMode;
            var prev2DMode = Physics2D.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            Physics2D.simulationMode = SimulationMode2D.Script;
            #else
            #pragma warning disable CS0618
            bool prev3D = Physics.autoSimulation;
            bool prev2D = Physics2D.autoSimulation;
            Physics.autoSimulation = false;
            Physics2D.autoSimulation = false;
            #pragma warning restore CS0618
            #endif
            try
            {
                DestroyAllKASPrefabs();
                yield return manager.StartCoroutine(StartLoadProcess(slot, restoreLastActiveScene, loadAsync, allowSceneActivation, cancellationToken));
                manager.ReMapTrackedUniqueIDs(slot);
            }
            finally
            {
            #if UNITY_2022_1_OR_NEWER
                Physics.simulationMode = prev3DMode;
                Physics2D.simulationMode = prev2DMode;
            #else
            #pragma warning disable CS0618
                Physics.autoSimulation = prev3D;
                Physics2D.autoSimulation = prev2D;
            #pragma warning restore CS0618
            #endif
                manager.PrefabRestoreService?.ProcessPendingRestores();
                manager.ReleaseLoadLock(slot.SlotNumber);
                manager.IsLoadingInternal = false;
            }
        }
    }
}
#endif
