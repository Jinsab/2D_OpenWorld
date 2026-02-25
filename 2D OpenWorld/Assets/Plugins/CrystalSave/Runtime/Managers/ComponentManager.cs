#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    public class ComponentManager
    {
#if UNITY_EDITOR
        private const string DebugLogTag = "[CrystalSaveDebug]";
#endif
        /*─────────────────────────────── CONFIG ───────────────────────────────*/
        /// <summary>The maximum number of full passes over <see cref="parentingQueue"/> we will
        /// perform while trying to resolve missing parents.  In practice 2–3 is enough because
        /// each successful parenting shrinks the queue for the next pass.</summary>
       private const int MaxParentingResolveIterations = 3;
               private readonly List<ISaveable> saveableComponents = new();
               private readonly Dictionary<string, ISaveable> saveableLookup = new();
               private readonly Dictionary<string, byte[]> pendingComponentData = new();
               private readonly HashSet<string> deserializedComponents = new();
               private readonly Dictionary<string, Queue<ComponentDataEnvelope>> deferredComponentData = new();
               public event Action<IReadOnlyCollection<string>> OnDeferredComponentsQueued;
               private const string GlobalDeferredSceneKey = "__GLOBAL__";

              private sealed class ComponentDataEnvelope
              {
                      public string UniqueIdentifier { get; }
                      public byte[] Data { get; }
                      public int LoadPriority { get; }
                      public string HomeScene { get; }
                      public string GameObjectUniqueID { get; }
                      public string ComponentID { get; }

                      public ComponentDataEnvelope(string uniqueIdentifier, byte[] data, ComponentDataMetadata metadata)
                      {
                              UniqueIdentifier = uniqueIdentifier;
                              Data = data;
                              LoadPriority = Mathf.Clamp(metadata?.LoadPriority ?? 50, 0, 100);
                              HomeScene = metadata?.HomeScene;
                              GameObjectUniqueID = ExtractGameObjectID(uniqueIdentifier);
                              ComponentID = ExtractComponentID(uniqueIdentifier);
                      }
              }

               // Scene-local runtime snapshot cache
               // Key: sceneName
               //   -> Key: GameObject UniqueID
               //        -> Key: SaveableComponent.UniqueIdentifier -> byte[] data
               private readonly Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>> sceneSnapshots
                       = new();

               // Prefab references for objects using Last Snapshot Scene
               // Key: sceneName -> GameObject UniqueID -> Prefab Asset ID
               private readonly Dictionary<string, Dictionary<string, string>> scenePrefabRefs
                       = new();

        #region Singleton Implementation

                /// <summary>
                /// Provides access to the active <see cref="ComponentManager"/> via the <see cref="SaveManager"/>.
                /// </summary>
                public static ComponentManager Instance => SaveManager.Instance?.ComponentManager;

               #endregion

               #region Validation Helpers

               private static bool IsValidSaveable(ISaveable component, string context)
               {
                       if (component == null)
                       {
                               Logger.Log($"{context}: ISaveable is null.", LogCategory.ComponentManager, LogLevel.Warning);
                               return false;
                       }

                       if (string.IsNullOrEmpty(component.UniqueIdentifier))
                       {
                               Logger.Log($"{context}: ISaveable has empty UniqueIdentifier.", LogCategory.ComponentManager, LogLevel.Error);
                               return false;
                       }

                       return true;
               }

               #endregion

        #region Public Methods

               public void ResetDeserializedComponents()
               {
                       deserializedComponents.Clear();
               }

               public void MarkComponentDeserialized(string id)
               {
                       if (!string.IsNullOrEmpty(id))
                       {
                               bool wasAlreadyMarked = deserializedComponents.Contains(id);
                               deserializedComponents.Add(id);
                               Logger.Log($"ComponentManager: MarkComponentDeserialized '{id}' (wasAlreadyMarked: {wasAlreadyMarked})", LogCategory.ComponentManager, LogLevel.Info);
                       }
               }

               public bool HasComponentDeserialized(string id)
               {
                       bool result = !string.IsNullOrEmpty(id) && deserializedComponents.Contains(id);
                       Logger.Log($"ComponentManager: HasComponentDeserialized '{id}' = {result}", LogCategory.ComponentManager, LogLevel.Info);
                       return result;
               }

               public void RemovePendingComponentData(string uniqueIdentifier)
               {
                       if (string.IsNullOrEmpty(uniqueIdentifier))
                               return;

                       pendingComponentData.Remove(uniqueIdentifier);
               }

               public bool HasDeferredComponents => deferredComponentData.Any(kvp => kvp.Value.Count > 0);

               public IEnumerable<string> GetDeferredSceneKeys()
               {
                       foreach (var kvp in deferredComponentData)
                       {
                               if (kvp.Value.Count == 0)
                                       continue;

                               yield return kvp.Key == GlobalDeferredSceneKey ? string.Empty : kvp.Key;
                       }
               }

               public IReadOnlyList<string> PeekDeferredComponentsForScene(string sceneName)
               {
                       string key = ResolveDeferredSceneKey(sceneName);
                       if (!deferredComponentData.TryGetValue(key, out var queue) || queue.Count == 0)
                               return Array.Empty<string>();

                       return queue
                               .Where(entry => entry != null)
                               .Select(entry => entry.UniqueIdentifier)
                               .ToList()
                               .AsReadOnly();
               }

               public void ProcessDeferredComponents()
               {
                       var entries = DequeueAllDeferredComponents();
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public IEnumerator ProcessDeferredComponentsCoroutine(int batchSize)
               {
                       var entries = DequeueAllDeferredComponents();
                       if (entries.Count == 0)
                               yield break;

                       yield return ApplyComponentEntriesCoroutine(entries, batchSize);
               }

               public void ProcessDeferredComponentsForScene(string sceneName)
               {
                       var entries = DequeueDeferredComponents(sceneName);
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public IEnumerator ProcessDeferredComponentsForSceneCoroutine(string sceneName, int batchSize)
               {
                       var entries = DequeueDeferredComponents(sceneName);
                       if (entries.Count == 0)
                               yield break;

                       yield return ApplyComponentEntriesCoroutine(entries, batchSize);
               }

               public void ProcessDeferredComponentsForGameObject(string gameObjectUniqueID)
               {
                       if (string.IsNullOrEmpty(gameObjectUniqueID))
                               return;

                       var entries = ExtractDeferredComponents(entry => string.Equals(entry.GameObjectUniqueID, gameObjectUniqueID, StringComparison.Ordinal));
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

              /// <summary>
              /// Processes deferred entries that belong to the provided <paramref name="componentID"/>.
              /// </summary>
              /// <param name="componentID">The <see cref="SaveableComponent.ComponentID"/> to process.</param>
              public void ProcessDeferredComponentsForComponentID(string componentID)
              {
                      if (string.IsNullOrEmpty(componentID))
                              return;

                      var entries = ExtractDeferredComponents(entry => string.Equals(entry.ComponentID, componentID, StringComparison.Ordinal));
                      if (entries.Count == 0)
                              return;

                      ApplyComponentEntries(entries);
              }

              /// <summary>
              /// Processes deferred entries that belong to any of the provided component IDs.
              /// </summary>
              /// <param name="componentIDs">Collection of <see cref="SaveableComponent.ComponentID"/> values to process.</param>
              public void ProcessDeferredComponentsForComponentIDs(IReadOnlyCollection<string> componentIDs)
              {
                      if (componentIDs == null || componentIDs.Count == 0)
                              return;

                      var set = new HashSet<string>(componentIDs.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
                      if (set.Count == 0)
                              return;

                      var entries = ExtractDeferredComponents(entry => set.Contains(entry.ComponentID));
                      if (entries.Count == 0)
                              return;

                      ApplyComponentEntries(entries);
              }

               public void ProcessDeferredComponent(string uniqueIdentifier)
               {
                       if (string.IsNullOrEmpty(uniqueIdentifier))
                               return;

                       var entries = ExtractDeferredComponents(entry => string.Equals(entry.UniqueIdentifier, uniqueIdentifier, StringComparison.Ordinal));
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public void ProcessDeferredComponentsByUniqueIDs(IReadOnlyCollection<string> uniqueIdentifiers)
               {
                       if (uniqueIdentifiers == null || uniqueIdentifiers.Count == 0)
                               return;

                       var set = new HashSet<string>(uniqueIdentifiers.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
                       if (set.Count == 0)
                               return;

                       var entries = ExtractDeferredComponents(entry => set.Contains(entry.UniqueIdentifier));
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public void ClearDeferredComponentData()
               {
                       deferredComponentData.Clear();
               }

        /// <summary>
               /// Registers a SaveableComponent.
               /// </summary>
               public void RegisterSaveableComponent(ISaveable component)
               {
                       if (!IsValidSaveable(component, nameof(RegisterSaveableComponent)))
                               return;

                       if (saveableComponents.Contains(component))
                       {
                               Logger.Log($"ComponentManager: ISaveable '{component.UniqueIdentifier}' is already registered.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }

                       saveableComponents.Add(component);
                       saveableLookup[component.UniqueIdentifier] = component;
                       Logger.Log($"ComponentManager: Registered ISaveable '{component.UniqueIdentifier}' on GameObject '{(component as MonoBehaviour)?.gameObject.name ?? "Unknown"}'.", LogCategory.ComponentManager, LogLevel.Info);

                       // Runtime safety: components that start disabled skip Awake, so their HomeScene
                       // might be empty even when RememberHomeScene is enabled. Auto-fill for DesignScene
                       // so RememberHomeScene can apply snapshots correctly after registration.
                       if (Application.isPlaying &&
                           component is SaveableComponent sc &&
                           sc.RememberHomeScene &&
                           sc.HomeSceneMode == HomeSceneMode.DesignScene &&
                           string.IsNullOrEmpty(sc.HomeScene))
                       {
                               var activeScene = SceneManager.GetActiveScene();
                               if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.name))
                               {
                                       sc.HomeScene = activeScene.name;
                                       Logger.Log(
                                               $"[RememberHome] Auto-filled HomeScene='{activeScene.name}' for '{sc.UniqueIdentifier}' during registration (component may have started disabled).",
                                               LogCategory.ComponentManager,
                                               LogLevel.Info);
                               }
                       }

                       if (pendingComponentData.TryGetValue(component.UniqueIdentifier, out var pending))
                       {
                               //Debug.Log($"[ComponentManager] FOUND PENDING DATA for '{component.UniqueIdentifier}' on '{(component as MonoBehaviour)?.gameObject.name ?? "Unknown"}', dataLen={pending?.Length ?? 0}");
                               Logger.Log($"ComponentManager: Found pending data for '{component.UniqueIdentifier}' during registration on GameObject '{(component as MonoBehaviour)?.gameObject.name ?? "Unknown"}'.", LogCategory.ComponentManager, LogLevel.Info);
                               
                               // Check if this component should be handled by prefab blob system instead
                               if (component is SaveableComponent saveableComp && saveableComp.PrefabHandlesSerialization)
                               {
                                       Logger.Log($"ComponentManager: Skipping pending LoadData for '{component.UniqueIdentifier}' - component is managed by prefab blob system.", LogCategory.ComponentManager, LogLevel.Info);
                                       MarkComponentDeserialized(component.UniqueIdentifier);
                               }
                               else if (HasComponentDeserialized(component.UniqueIdentifier))
                               {
                                       Logger.Log($"ComponentManager: Skipping duplicate pending LoadData for '{component.UniqueIdentifier}' - already deserialized.", LogCategory.ComponentManager, LogLevel.Warning);
                               }
                               else
                               {
                                       Logger.Log($"ComponentManager: Calling LoadData for '{component.UniqueIdentifier}' from pending data during registration.", LogCategory.ComponentManager, LogLevel.Info);
                                       
                                       try
                                       {
                                               component.LoadData(pending);
                                               MarkComponentDeserialized(component.UniqueIdentifier);
                                               Logger.Log($"ComponentManager: Successfully applied pending data to '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                                       }
                                       catch (Exception ex)
                                       {
                                               Logger.Log($"ComponentManager: Error applying pending data to '{component.UniqueIdentifier}': {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                                       }
                               }

                               pendingComponentData.Remove(component.UniqueIdentifier);
                       }

                       // If the component is configured to remember its home scene and we have a snapshot
                       // for this GameObject in the current scene, apply it now.
                       TryApplyRememberedSnapshot(component);

                       ProcessQueuedParenting();
               }

               /// <summary>
               /// Reindexes internal lookup keys when a component's <see cref="ISaveable.UniqueIdentifier"/>
               /// changes at runtime (for example after deserializing a runtime-added component).
               /// </summary>
               public void ReindexComponentIdentifier(ISaveable component, string previousIdentifier)
               {
                       if (component == null)
                               return;

                       string currentIdentifier = null;
                       try
                       {
                               currentIdentifier = component.UniqueIdentifier;
                       }
                       catch
                       {
                               // If the component cannot report an identifier (e.g. destroyed), nothing to reindex.
                               return;
                       }

                       if (string.IsNullOrEmpty(currentIdentifier))
                               return;

                       // Ensure the current key always resolves to this live component.
                       saveableLookup[currentIdentifier] = component;

                       if (string.IsNullOrEmpty(previousIdentifier) ||
                           string.Equals(previousIdentifier, currentIdentifier, StringComparison.Ordinal))
                       {
                               return;
                       }

                       // Remove stale key(s) that still point to this component.
                       if (saveableLookup.TryGetValue(previousIdentifier, out var mapped) &&
                           ReferenceEquals(mapped, component))
                       {
                               saveableLookup.Remove(previousIdentifier);
                       }
                       else
                       {
                               foreach (var staleKey in saveableLookup
                                        .Where(kvp => !string.Equals(kvp.Key, currentIdentifier, StringComparison.Ordinal)
                                                && ReferenceEquals(kvp.Value, component))
                                        .Select(kvp => kvp.Key)
                                        .ToList())
                               {
                                       saveableLookup.Remove(staleKey);
                               }
                       }

                       // Preserve pending data that may have been queued under the old key.
                       if (pendingComponentData.TryGetValue(previousIdentifier, out var pendingBytes))
                       {
                               if (!pendingComponentData.ContainsKey(currentIdentifier))
                                       pendingComponentData[currentIdentifier] = pendingBytes;
                               pendingComponentData.Remove(previousIdentifier);
                       }

                       // Preserve deserialized marker if it was recorded under the old key.
                       if (deserializedComponents.Remove(previousIdentifier))
                               deserializedComponents.Add(currentIdentifier);

                       Logger.Log(
                               $"ComponentManager: Reindexed component identifier '{previousIdentifier}' -> '{currentIdentifier}'.",
                               LogCategory.ComponentManager,
                               LogLevel.Info);
               }

               public IEnumerator ApplyComponentDataCoroutine(SaveData saveData, int batchSize)
               {
                       if (batchSize <= 0)
                       {
                               ApplyComponentData(saveData);
                               yield break;
                       }

                       if (saveData?.ComponentsData == null)
                       {
                               Logger.Log(
                                       "ComponentManager: No component data to apply (saveData or ComponentsData is null).",
                                       LogCategory.ComponentManager,
                                       LogLevel.Warning
                               );
                               yield break;
                       }

                       ClearDeferredComponentData();

                       PartitionComponentData(saveData, out var immediateEntries, out var deferredEntries);
                       if (deferredEntries.Count > 0)
                               QueueDeferredComponentData(deferredEntries);

                       if (immediateEntries.Count == 0)
                       {
                               ProcessQueuedParenting();
                               yield break;
                       }

                       yield return ApplyComponentEntriesCoroutine(immediateEntries, batchSize);
               }

        /// <summary>
        /// Unregisters a SaveableComponent.
        /// </summary>
               public void UnregisterSaveableComponent(ISaveable component)
               {
                       if (component == null)
                       {
                               Logger.Log("ComponentManager: Attempted to unregister a null ISaveable.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }

                       if (saveableComponents.Remove(component))
                       {
                               if (!string.IsNullOrEmpty(component.UniqueIdentifier))
                                       saveableLookup.Remove(component.UniqueIdentifier);
                               Logger.Log($"ComponentManager: Unregistered ISaveable '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                       }
                       else
                       {
                               Logger.Log($"ComponentManager: Attempted to unregister ISaveable '{component.UniqueIdentifier}', but it was not found.", LogCategory.ComponentManager, LogLevel.Warning);
                       }
               }

                /// <summary>
                /// Destroys the <see cref="GameObject"/> whose
                /// <see cref="UniqueID"/> or <see cref="SaveableComponent.GameObjectUniqueID"/>
                /// matches <paramref name="uniqueID"/>.
                /// </summary>
                /// <param name="uniqueID">Unique ID from a <see cref="UniqueID"/> component.</param>
               public void DestroyGameObjectByUniqueID(string uniqueID)
               {
                       if (string.IsNullOrEmpty(uniqueID))
                       {
                               Logger.Log("ComponentManager: DestroyGameObjectByUniqueID called with an empty ID.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }

                       GameObject obj = SaveManager.Instance?.FindGameObjectByUniqueID(uniqueID, SaveManager.IdentifierType.UniqueID);
                       if (obj == null)
                       {
                               Logger.Log($"ComponentManager: No GameObject found with UniqueID '{uniqueID}'.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }

                       SaveManager.Instance?.SoftUnregisterGameObject(obj);
                       DestroyHelper.DestroyWithLogging(obj, $"ComponentManager.DestroyGameObjectByUniqueID({uniqueID})");
               }

                /// <summary>
                /// Destroys each <see cref="GameObject"/> whose
                /// <see cref="UniqueID"/> or <see cref="SaveableComponent.GameObjectUniqueID"/>
                /// matches any of the provided <paramref name="uniqueIDs"/>.
                /// </summary>
                /// <param name="uniqueIDs">List of Unique IDs from <see cref="UniqueID"/> components.</param>
                public void DestroyGameObjectByUniqueID(List<string> uniqueIDs)
                {
                        if (uniqueIDs == null || uniqueIDs.Count == 0)
                        {
                                Logger.Log("ComponentManager: DestroyGameObjectByUniqueID called with an empty ID list.", LogCategory.ComponentManager, LogLevel.Warning);
                                return;
                        }

                        foreach (var id in uniqueIDs)
                        {
                                DestroyGameObjectByUniqueID(id);
                        }
                }

        /// <summary>
        /// Collects serialized data from all registered ISaveable components.
        /// </summary>
               public void CollectComponentData(SaveData saveData)
               {
                       if (saveData == null)
                               return;

                       if (saveData.ComponentMetadata == null)
                               saveData.ComponentMetadata = new Dictionary<string, ComponentDataMetadata>();
                       else
                               saveData.ComponentMetadata.Clear();

                       // Build a fast lookup of any deferred component entries we still have queued,
                       // so we can persist their last-known bytes instead of overwriting them with
                       // the current (design-time) runtime state when saving mid-stream.
                       var deferredLookup = new Dictionary<string, ComponentDataEnvelope>(StringComparer.Ordinal);
                       foreach (var kv in deferredComponentData)
                       {
                               var queue = kv.Value;
                               if (queue == null || queue.Count == 0) continue;
                               foreach (var entry in queue)
                               {
                                       if (entry == null || string.IsNullOrEmpty(entry.UniqueIdentifier)) continue;
                                       // Last write wins if duplicates exist (shouldn't in practice)
                                       deferredLookup[entry.UniqueIdentifier] = entry;
                               }
                       }

                       // Track which component IDs we've written into saveData to avoid duplicates
                       var written = new HashSet<string>(StringComparer.Ordinal);

                       foreach (var component in saveableComponents)
                       {
                               // Skip entries whose underlying Unity object has been destroyed
                               if (component is MonoBehaviour mb && mb == null)
                                       continue;
                               // Also skip if the owning GameObject is marked destroyed in tracker
                               try
                               {
                                       var goUid = ExtractGameObjectID(component.UniqueIdentifier);
                                       if (!string.IsNullOrEmpty(goUid) && SaveManager.Instance != null && SaveManager.Instance.IsGameObjectDestroyed(goUid))
                                               continue;
                               }
                               catch { /* best-effort guard */ }
                               if (!IsValidSaveable(component, nameof(CollectComponentData)))
                                       continue;

                               try
                               {
                                       var id = component.UniqueIdentifier;

                                       // If this component's data is still deferred from a previous load,
                                       // prefer the deferred bytes instead of live-serializing its current
                                       // design-time state. This preserves correctness across save/load cycles
                                       // when using 'Defer Until Requested'.
                                       if (deferredLookup.TryGetValue(id, out var deferredEntry) && deferredEntry.Data != null && deferredEntry.Data.Length > 0)
                                       {
                                               saveData.ComponentsData[id] = deferredEntry.Data;
                                               if (component is SaveableComponent sc)
                                               {
                                                       string homeScene = sc.RememberHomeScene ? sc.HomeScene : deferredEntry.HomeScene;
                                                       // Force Defer flag to true to keep the entry deferred on next load
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(
                                                               sc.LoadPriority,
                                                               true,
                                                               homeScene);
                                               }
                                               else
                                               {
                                                       // Non SaveableComponent implementers: persist metadata if it existed; otherwise omit.
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(50, true, null);
                                               }
#if UNITY_EDITOR
                                               Logger.Log($"[CrystalSaveDebug][CompSave] Using deferred bytes for '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                                               written.Add(id);
                                               continue;
                                       }

                                       // Also prefer any pending data that was queued because the component
                                       // hadn't registered yet during load (rare for scene components).
                                       if (pendingComponentData.TryGetValue(id, out var pendingBytes) && pendingBytes != null && pendingBytes.Length > 0)
                                       {
                                               saveData.ComponentsData[id] = pendingBytes;
                                               if (component is SaveableComponent sc)
                                               {
                                                       string homeScene = sc.RememberHomeScene ? sc.HomeScene : null;
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(sc.LoadPriority, sc.DeferLowPriorityUntilRequested, homeScene);
                                               }
                                               else
                                               {
                                                       saveData.ComponentMetadata.Remove(id);
                                               }
#if UNITY_EDITOR
                                               Logger.Log($"[CrystalSaveDebug][CompSave] Using pending bytes for '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                                               written.Add(id);
                                               continue;
                                       }

                                       // Fall back to live serialization of the component
                                       byte[] serializedData = component.SaveData();
                                       if (serializedData != null && serializedData.Length > 0)
                                       {
                                               saveData.ComponentsData[id] = serializedData;
                                               if (component is SaveableComponent sc)
                                               {
                                                       string homeScene = sc.RememberHomeScene ? sc.HomeScene : null;
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(sc.LoadPriority, sc.DeferLowPriorityUntilRequested, homeScene);
                                               }
                                               else
                                               {
                                                       saveData.ComponentMetadata.Remove(id);
                                               }
                                               Logger.Log($"ComponentManager: Serialized data for '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
                                               written.Add(id);
                                       }
                                       else
                                       {
                                               saveData.ComponentMetadata.Remove(id);
                                       }
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log($"ComponentManager: Error serializing '{component.UniqueIdentifier}': {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       // Also persist any deferred entries for components that are not currently registered
                       // so they are not lost on save. This preserves "defer across saves" semantics.
                       foreach (var kv in deferredLookup)
                       {
                               var id = kv.Key;
                               var entry = kv.Value;
                               if (written.Contains(id)) continue;
                               if (entry?.Data == null || entry.Data.Length == 0) continue;

                               saveData.ComponentsData[id] = entry.Data;
                               // Use the preserved LoadPriority and force Defer = true
                               saveData.ComponentMetadata[id] = new ComponentDataMetadata(entry.LoadPriority, true, entry.HomeScene);
#if UNITY_EDITOR
                               Logger.Log($"[CrystalSaveDebug][CompSave] Added unregistered deferred entry '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                               written.Add(id);
                       }

                       // Finally, persist any pending component data for unregistered components
                       // that wasn't covered by deferredLookup, to avoid losing their state.
                       //
                       // IMPORTANT: Only persist pending entries whose owning prefab instance is
                       // still tracked by PrefabManager. Entries for prefab instances that were never
                       // re-instantiated during loading are orphans — they would accumulate in the
                       // save file indefinitely, growing by ~60-80 entries per save/load cycle.
                       // We log how many entries we skip so the diagnostic trail is visible.
                       int pendingPersisted = 0;
                       int pendingDropped = 0;
                       foreach (var kv in pendingComponentData)
                       {
                               var id = kv.Key;
                               var bytes = kv.Value;
                               if (written.Contains(id)) continue;
                               if (bytes == null || bytes.Length == 0) continue;

                               // Check if the owning prefab instance still exists in the scene.
                               // The composite key format is "{instanceGUID}_{componentID}".
                               // If PrefabManager doesn't have a live mapping for the instanceGUID,
                               // and SaveManager can't find the GameObject, this entry is orphaned
                               // — UNLESS it belongs to a prefab whose home scene is not loaded.
                               bool hasLiveInstance = false;
                               try
                               {
                                       string instanceGUID = ExtractGameObjectID(id);
                                       if (!string.IsNullOrEmpty(instanceGUID))
                                       {
                                               // Check if a live component registered with this key
                                               if (saveableLookup.ContainsKey(id))
                                               {
                                                       hasLiveInstance = true;
                                               }
                                               else
                                               {
                                                       // Check if the owning GameObject still exists
                                                       var go = SaveManager.Instance?.FindGameObjectByUniqueID(instanceGUID, SaveManager.IdentifierType.UniqueID);
                                                       if (go != null)
                                                       {
                                                               hasLiveInstance = true;
                                                       }
                                                       else
                                                       {
                                                               // Check if this entry belongs to a prefab in an
                                                               // unloaded scene (Remember Home Scene). Look up
                                                               // the prefab entry in the current save data to
                                                               // find its HomeScene.
                                                               var currentSave = SaveManager.Instance?.CurrentSaveData;
                                                               if (currentSave?.Prefabs != null)
                                                               {
                                                                       foreach (var pd in currentSave.Prefabs)
                                                                       {
                                                                               if (pd != null && pd.InstanceID == instanceGUID
                                                                                       && !string.IsNullOrEmpty(pd.HomeScene))
                                                                               {
                                                                                       var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(pd.HomeScene);
                                                                                       if (!scene.IsValid() || !scene.isLoaded)
                                                                                       {
                                                                                               hasLiveInstance = true; // belongs to unloaded scene — preserve
                                                                                       }
                                                                                       break;
                                                                               }
                                                                       }
                                                               }
                                                       }
                                               }
                                       }
                               }
                               catch { /* best-effort */ }

                               if (!hasLiveInstance)
                               {
                                       pendingDropped++;
                                       continue; // Don't persist orphaned pending data
                               }

                               saveData.ComponentsData[id] = bytes;
                               // No strong signal to defer; keep metadata minimal here
                               if (!saveData.ComponentMetadata.ContainsKey(id))
                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(50, false, null);
#if UNITY_EDITOR
                               Logger.Log($"[CrystalSaveDebug][CompSave] Added unregistered pending entry '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                               pendingPersisted++;
                       }
                       if (pendingDropped > 0 || pendingPersisted > 0)
                       {
                               UnityEngine.Debug.Log($"[ComponentManager] CollectComponentData: Pending entries — persisted={pendingPersisted}, dropped(orphaned)={pendingDropped}");
                       }

                       // Also persist the Remember Home per-scene snapshot cache so it survives across sessions
                       try
                       {
                               if (saveData.HomeSceneComponentSnapshots == null)
                                       saveData.HomeSceneComponentSnapshots = new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>();

                               // Create a shallow copy to avoid external mutation of our internal dictionary
                               saveData.HomeSceneComponentSnapshots.Clear();
                               foreach (var sceneEntry in sceneSnapshots)
                               {
                                       // Copy per-object maps
                                       var perObjectCopy = new Dictionary<string, Dictionary<string, byte[]>>();
                                       foreach (var objEntry in sceneEntry.Value)
                                       {
                                               var perComponentCopy = new Dictionary<string, byte[]>();
                                               foreach (var compEntry in objEntry.Value)
                                               {
                                                       // Copy the byte[] reference; SaveData will be serialized immediately after
                                                       perComponentCopy[compEntry.Key] = compEntry.Value;
                                               }
                                               perObjectCopy[objEntry.Key] = perComponentCopy;
                                       }
                                       saveData.HomeSceneComponentSnapshots[sceneEntry.Key] = perObjectCopy;
                               }
                               Logger.Log($"[RememberHome] Persisted scene snapshot cache: Scenes={saveData.HomeSceneComponentSnapshots.Count}", LogCategory.ComponentManager, LogLevel.Info);
                       }
                       catch (Exception ex)
                       {
                               Logger.Log($"[RememberHome] Persist cache failed: {ex.Message}", LogCategory.ComponentManager, LogLevel.Warning);
                       }

                       // Persist prefab asset references for Last Snapshot Scene
                       try
                       {
                               if (saveData.HomeScenePrefabAssetIDs == null)
                                       saveData.HomeScenePrefabAssetIDs = new Dictionary<string, Dictionary<string, string>>();

                               saveData.HomeScenePrefabAssetIDs.Clear();
                               foreach (var sceneEntry in scenePrefabRefs)
                               {
                                       saveData.HomeScenePrefabAssetIDs[sceneEntry.Key] = new Dictionary<string, string>(sceneEntry.Value);
                               }
                               Logger.Log($"[RememberHome] Persisted prefab refs: Scenes={saveData.HomeScenePrefabAssetIDs.Count}", LogCategory.ComponentManager, LogLevel.Info);
                       }
                       catch (Exception ex)
                       {
                               Logger.Log($"[RememberHome] Persist prefab refs failed: {ex.Message}", LogCategory.ComponentManager, LogLevel.Warning);
                       }
               }

        /// <summary>
        /// Applies serialized data to the corresponding ISaveable components.
        /// </summary>
               public void ApplyComponentData(SaveData saveData)
               {
                       Logger.Log(
                               $"[ComponentManager] ApplyComponentData called, saveData null: {saveData == null}, ComponentsData null: {saveData?.ComponentsData == null}, ComponentsData count: {saveData?.ComponentsData?.Count ?? 0}",
                               LogCategory.ComponentManager,
                               LogLevel.Info);
                       
                       if (saveData?.ComponentsData == null)
                       {
                               Logger.Log(
                                       "ComponentManager: No component data to apply (saveData or ComponentsData is null).",
                                       LogLevel.Warning
                               );
                               return;
                       }

                       ClearDeferredComponentData();

                       PartitionComponentData(saveData, out var immediateEntries, out var deferredEntries);
                       Logger.Log(
                               $"[ComponentManager] ApplyComponentData: immediateEntries={immediateEntries.Count}, deferredEntries={deferredEntries.Count}",
                               LogCategory.ComponentManager,
                               LogLevel.Info);
                       if (deferredEntries.Count > 0)
                               QueueDeferredComponentData(deferredEntries);

                       ApplyComponentEntries(immediateEntries);
               }

               private void PartitionComponentData(SaveData saveData,
                                                   out List<ComponentDataEnvelope> immediate,
                                                   out List<ComponentDataEnvelope> deferred)
               {
                       immediate = new List<ComponentDataEnvelope>();
                       deferred = new List<ComponentDataEnvelope>();

                       if (saveData?.ComponentsData == null)
                               return;

                       foreach (var kvp in saveData.ComponentsData)
                       {
                               string uniqueID = kvp.Key;
                               byte[] data = kvp.Value;
                               if (string.IsNullOrEmpty(uniqueID) || data == null)
                                       continue;

                               ComponentDataMetadata metadata = null;
                               if (saveData.ComponentMetadata != null)
                                       saveData.ComponentMetadata.TryGetValue(uniqueID, out metadata);

                               var envelope = new ComponentDataEnvelope(uniqueID, data, metadata);
                               bool defer = metadata != null && metadata.DeferLowPriorityUntilRequested;
                               if (defer)
                                       deferred.Add(envelope);
                               else
                                       immediate.Add(envelope);
                       }

                       immediate.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));
                       deferred.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));
               }

               private void QueueDeferredComponentData(IEnumerable<ComponentDataEnvelope> deferredEntries)
               {
                       if (deferredEntries == null)
                               return;

                       HashSet<string> stagedSceneKeys = null;

                       foreach (var group in deferredEntries
                               .Where(entry => entry != null)
                               .GroupBy(GetDeferredSceneKey))
                       {
                               if (!deferredComponentData.TryGetValue(group.Key, out var queue))
                               {
                                       queue = new Queue<ComponentDataEnvelope>();
                                       deferredComponentData[group.Key] = queue;
                               }

                               bool anyQueued = false;

                               foreach (var entry in group.OrderByDescending(e => e.LoadPriority))
                               {
                                       queue.Enqueue(entry);
                                       anyQueued = true;
                               }

                               if (anyQueued)
                               {
                                       stagedSceneKeys ??= new HashSet<string>(StringComparer.Ordinal);
                                       string sceneKey = group.Key == GlobalDeferredSceneKey
                                               ? string.Empty
                                               : group.Key;
                                       stagedSceneKeys.Add(sceneKey);
                               }
                       }

                       if (stagedSceneKeys != null && stagedSceneKeys.Count > 0)
                       {
                               var scenes = stagedSceneKeys.ToList();
                               OnDeferredComponentsQueued?.Invoke(scenes);
                       }
               }

               /// <summary>
               /// Removes stale ISaveable references whose backing Unity object has already
               /// been destroyed (deferred destroy window).
               /// </summary>
               private void PurgeDestroyedComponents()
               {
                       for (int i = saveableComponents.Count - 1; i >= 0; i--)
                       {
                               var component = saveableComponents[i];
                               if (!(component is MonoBehaviour mb) || mb != null)
                                       continue;

                               string uid = null;
                               try
                               {
                                       uid = component.UniqueIdentifier;
                               }
                               catch
                               {
                                       // Best-effort: some implementations may throw when destroyed.
                               }

                               if (!string.IsNullOrEmpty(uid))
                               {
                                       // Remove only if this key still points to the same stale instance.
                                       if (saveableLookup.TryGetValue(uid, out var mapped) && ReferenceEquals(mapped, component))
                                               saveableLookup.Remove(uid);
                               }
                               else
                               {
                                       // Fallback: remove any lookup entries that still reference this stale object.
                                       foreach (var key in saveableLookup
                                                .Where(kvp => ReferenceEquals(kvp.Value, component))
                                                .Select(kvp => kvp.Key)
                                                .ToList())
                                       {
                                               saveableLookup.Remove(key);
                                       }
                               }

                               saveableComponents.RemoveAt(i);
                       }
               }

               private void ApplyComponentEntries(List<ComponentDataEnvelope> entries)
               {
                       if (entries == null || entries.Count == 0)
                       {
                               ProcessQueuedParenting();
                               return;
                       }

                       PurgeDestroyedComponents();

                       foreach (var entry in entries)
                               ApplyComponentEntry(entry);

                       ProcessQueuedParenting();
               }

               private IEnumerator ApplyComponentEntriesCoroutine(List<ComponentDataEnvelope> entries, int batchSize)
               {
                       if (entries == null || entries.Count == 0)
                       {
                               ProcessQueuedParenting();
                               yield break;
                       }

                       PurgeDestroyedComponents();

                       int processed = 0;
                       foreach (var entry in entries)
                       {
                               ApplyComponentEntry(entry);
                               processed++;
                               if (batchSize > 0 && processed >= batchSize)
                               {
                                       processed = 0;
                                       yield return null;
                               }
                       }

                       ProcessQueuedParenting();
               }

               private void ApplyComponentEntry(ComponentDataEnvelope entry)
               {
                       if (entry == null || string.IsNullOrEmpty(entry.UniqueIdentifier) || entry.Data == null || entry.Data.Length == 0)
                               return;

                       string uniqueID = entry.UniqueIdentifier;
                       //Debug.Log($"[ComponentManager] ApplyComponentEntry: Processing uniqueID='{uniqueID}', dataLen={entry.Data.Length}");
                       var comp = FindComponentByUniqueID(uniqueID);
                       if (comp is MonoBehaviour foundMb && foundMb == null)
                               comp = null;

                       if (comp == null)
                       {
                               // Fallback: try matching by componentID alone, but ONLY for scene-baked
                               // objects (not SaveablePrefab instances). For prefabs, each instance has
                               // a unique instanceGUID prefix so the full key must match. Matching by
                               // componentID alone would silently route ALL instances' data to the
                               // first-registered component, causing data loss.
                               string compId = ExtractComponentID(uniqueID);
                               if (!string.IsNullOrEmpty(compId))
                               {
                                       comp = saveableComponents
                                               .OfType<SaveableComponent>()
                                               .Where(c => c != null) // Unity null check for destroyed components
                                               .FirstOrDefault(c => c.ComponentID == compId
                                                       && c.GetComponentInParent<SaveablePrefab>(true) == null);
                               }

                               if (comp == null)
                               {
                                       //Debug.Log($"[ComponentManager] QUEUING PENDING DATA for '{uniqueID}', dataLen={entry.Data?.Length ?? 0} (component not found)");
                                       Logger.Log($"ComponentManager: Queuing data for '{uniqueID}' until component registers.", LogCategory.ComponentManager, LogLevel.Info);
                                       pendingComponentData[uniqueID] = entry.Data;
                                       return;
                               }
                       }

                       if (comp is MonoBehaviour resolvedMb && resolvedMb == null)
                       {
                               Logger.Log($"ComponentManager: Queuing data for '{uniqueID}' until component registers.", LogCategory.ComponentManager, LogLevel.Info);
                               pendingComponentData[uniqueID] = entry.Data;
                               return;
                       }

                       // Check if this component is managed by a prefab with TrackComponentBlobs enabled
                       if (comp is SaveableComponent saveableComp && saveableComp.PrefabHandlesSerialization)
                       {
                               Logger.Log($"ComponentManager: Skipping LoadData for '{uniqueID}' - component is managed by prefab blob system on GameObject '{saveableComp.gameObject.name}'.", LogCategory.ComponentManager, LogLevel.Info);
                               MarkComponentDeserialized(uniqueID); // Mark as processed to prevent duplicate warnings
                               return;
                       }

                       if (HasComponentDeserialized(uniqueID))
                       {
                               Logger.Log($"ComponentManager: Skipping duplicate LoadData for '{uniqueID}' - already deserialized.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }


                       
                       try
                       {
                               comp.LoadData(entry.Data);
                               MarkComponentDeserialized(uniqueID);
                               Logger.Log($"ComponentManager: Successfully applied data to '{uniqueID}'.", LogCategory.ComponentManager, LogLevel.Info);
                       }
                       catch (Exception ex)
                       {
                               Logger.Log($"ComponentManager: Error applying data to '{uniqueID}': {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                       }
               }

               private List<ComponentDataEnvelope> DequeueDeferredComponents(string sceneName)
               {
                       string key = ResolveDeferredSceneKey(sceneName);
                       if (!deferredComponentData.TryGetValue(key, out var queue) || queue.Count == 0)
                               return new List<ComponentDataEnvelope>();

                       var list = queue
                               .Where(entry => entry != null)
                               .ToList();

                       deferredComponentData.Remove(key);

                       if (list.Count > 0)
                               list.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));

                       return list;
               }

               private List<ComponentDataEnvelope> DequeueAllDeferredComponents()
               {
                       var results = new List<ComponentDataEnvelope>();

                       foreach (var key in deferredComponentData.Keys.ToList())
                       {
                               var sceneKey = key == GlobalDeferredSceneKey ? string.Empty : key;
                               var entries = DequeueDeferredComponents(sceneKey);
                               if (entries.Count > 0)
                                       results.AddRange(entries);
                       }

                       if (results.Count > 0)
                               results.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));

                       return results;
               }

               private List<ComponentDataEnvelope> ExtractDeferredComponents(Predicate<ComponentDataEnvelope> match)
               {
                       var matches = new List<ComponentDataEnvelope>();
                       if (match == null || deferredComponentData.Count == 0)
                               return matches;

                       foreach (var key in deferredComponentData.Keys.ToList())
                       {
                               if (!deferredComponentData.TryGetValue(key, out var queue) || queue.Count == 0)
                               {
                                       deferredComponentData.Remove(key);
                                       continue;
                               }

                               var remaining = new Queue<ComponentDataEnvelope>();
                               while (queue.Count > 0)
                               {
                                       var entry = queue.Dequeue();
                                       if (entry == null)
                                               continue;

                                       if (match(entry))
                                               matches.Add(entry);
                                       else
                                               remaining.Enqueue(entry);
                               }

                               if (remaining.Count > 0)
                                       deferredComponentData[key] = remaining;
                               else
                                       deferredComponentData.Remove(key);
                       }

                       if (matches.Count > 0)
                               matches.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));

                       return matches;
               }

               private static string GetDeferredSceneKey(ComponentDataEnvelope entry)
               {
                       if (entry == null || string.IsNullOrEmpty(entry.HomeScene))
                               return GlobalDeferredSceneKey;
                       return entry.HomeScene;
               }

               private static string ResolveDeferredSceneKey(string sceneName)
               {
                       return string.IsNullOrEmpty(sceneName) ? GlobalDeferredSceneKey : sceneName;
               }

              private static string ExtractGameObjectID(string uniqueIdentifier)
              {
                      if (string.IsNullOrEmpty(uniqueIdentifier))
                              return string.Empty;

                      int underscore = uniqueIdentifier.IndexOf('_');
                      return underscore < 0 ? uniqueIdentifier : uniqueIdentifier.Substring(0, underscore);
              }

              private static string ExtractComponentID(string uniqueIdentifier)
              {
                      if (string.IsNullOrEmpty(uniqueIdentifier))
                              return string.Empty;

                      int underscore = uniqueIdentifier.IndexOf('_');
                      return underscore < 0 || underscore + 1 >= uniqueIdentifier.Length
                              ? string.Empty
                              : uniqueIdentifier.Substring(underscore + 1);
              }

               /// <summary>
               /// Captures the runtime state of all ISaveable components on the provided GameObject
               /// and stores it in the scene snapshot cache under the current active scene.
               /// </summary>
               public void SnapshotObjectToSceneCache(GameObject obj)
               {
                       if (obj == null) return;

                       var active = SceneManager.GetActiveScene();
                       if (!active.IsValid()) return;

                       SnapshotObjectToSceneCache(obj, active.name);
               }

               /// <summary>
               /// Internal helper to snapshot a single object into a specific scene cache bucket.
               /// </summary>
               private void SnapshotObjectToSceneCache(GameObject obj, string sceneName)
               {
                       if (obj == null || string.IsNullOrEmpty(sceneName)) return;

                       // Determine this object's identity — prefer UniqueID component,
                       // fall back to SaveablePrefab.UniqueID for prefabs that manage
                       // their own identity (e.g. SaveablePrefab + RememberComposite
                       // which intentionally removes the UniqueID component).
                       string objectID = null;
                       var uid = obj.GetComponent<UniqueID>();
                       if (uid != null && !string.IsNullOrEmpty(uid.ID))
                       {
                               objectID = uid.ID;
                       }
                       else
                       {
                               var sp = obj.GetComponent<SaveablePrefab>();
                               if (sp != null && !string.IsNullOrEmpty(sp.UniqueID))
                                       objectID = sp.UniqueID;
                       }
                       if (string.IsNullOrEmpty(objectID)) return;

                       var sceneDict = GetOrCreate(sceneName);
                       var perObject = new Dictionary<string, byte[]>();

                       // Track prefab reference for Last Snapshot Scene restoration
                       string prefabRef = null;
                       var saveablePrefab = obj.GetComponent<SaveablePrefab>();
                       if (saveablePrefab != null && !string.IsNullOrEmpty(saveablePrefab.PrefabAssetID))
                               prefabRef = saveablePrefab.PrefabAssetID;
                       else if (obj.TryGetComponent<SaveableComponent>(out var scParent) && !string.IsNullOrEmpty(scParent.HomeScenePrefabID))
                               prefabRef = scParent.HomeScenePrefabID;
                       if (!string.IsNullOrEmpty(prefabRef))
                               GetOrCreatePrefabMap(sceneName)[objectID] = prefabRef;

                       int compCount = 0;
                       foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
                       {
                               // Skip entries whose underlying Unity object has been destroyed
                               if (comp is MonoBehaviour mb && mb == null)
                                       continue;
                               if (!IsValidSaveable(comp, nameof(SnapshotObjectToSceneCache))) continue;
                               // Skip if this component belongs to a GameObject that is marked destroyed
                               try
                               {
                                       var goUid = ExtractGameObjectID(comp.UniqueIdentifier);
                                       if (!string.IsNullOrEmpty(goUid) && SaveManager.Instance != null && SaveManager.Instance.IsGameObjectDestroyed(goUid))
                                               continue;
                               }
                               catch { /* best-effort guard */ }

                               if (comp is SaveableComponent sc && sc.RememberHomeScene && sc.HomeSceneMode == HomeSceneMode.LastSnapshotScene)
                               {
                                       sc.HomeScene = sceneName;
                               }

                               try
                               {
                                       // Enable snapshot capture mode to bypass "Skip Saving When Unchanged" optimization
                                       // This ensures we always capture the current state for proper restoration
                                       SaveableComponent.SetSnapshotCaptureMode(true);
                                       try
                                       {
                                               var bytes = comp.SaveData();
                                               if (bytes != null && bytes.Length > 0)
                                               {
                                                       perObject[comp.UniqueIdentifier] = bytes;
                                                       compCount++;
                                               }
                                       }
                                       finally
                                       {
                                               SaveableComponent.SetSnapshotCaptureMode(false);
                                       }
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log($"SnapshotObjectToSceneCache('{sceneName}'): Error serializing '{comp.UniqueIdentifier}': {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       sceneDict[objectID] = perObject;
                       Logger.Log($"[RememberHome] SnapshotObject: '{obj.name}' ({objectID}) -> scene '{sceneName}', Components={compCount}", LogCategory.ComponentManager, LogLevel.Info);
               }

               /// <summary>
               /// Public helper to snapshot the entire scene's registered objects.
               /// </summary>
               public void SnapshotCurrentSceneAll()
               {
                       var scene = SceneManager.GetActiveScene();
                       if (!scene.IsValid()) return;

                       SnapshotSceneAll(scene);
               }

               /// <summary>
               /// Snapshots all <see cref="ISaveable"/> components in the provided scene into the per-scene cache.
               /// </summary>
               public void SnapshotSceneAll(Scene scene)
               {
                       if (!scene.IsValid()) return;

                       // Start from a clean slate for this scene
                       ClearSceneSnapshot(scene.name);

                       // Iterate root objects to avoid crossing into other scenes
                       var roots = scene.GetRootGameObjects();
                       string sceneName = scene.name;
                       int uidCount = 0;
                       int totalComponents = 0;
                       Logger.Log($"[RememberHome] SnapshotSceneAll: Begin capture for scene '{sceneName}'. Root count={roots?.Length ?? 0}", LogCategory.ComponentManager, LogLevel.Info);
                       
                       // Track which GameObjects have already been snapshotted to avoid
                       // double-processing when an object has both UniqueID and SaveablePrefab
                       var snapshotted = new HashSet<GameObject>();
                       
                       foreach (var root in roots)
                       {
                               if (root == null) continue;

                               // Pass 1: Snapshot objects with a UniqueID component
                               var uniques = root.GetComponentsInChildren<UniqueID>(includeInactive: true);
                               foreach (var uid in uniques)
                               {
                                       if (uid == null || string.IsNullOrEmpty(uid.ID)) continue;
                                       
                                       SnapshotObjectToSceneCache(uid.gameObject, sceneName);
                                       snapshotted.Add(uid.gameObject);
                                       uidCount++;
                               }

                               // Pass 2: Snapshot SaveablePrefab objects that have NO
                               // UniqueID component (e.g. SaveablePrefab + RememberComposite
                               // where UniqueID is intentionally removed). These prefabs
                               // manage their own identity via SaveablePrefab.UniqueID.
                               var prefabs = root.GetComponentsInChildren<SaveablePrefab>(includeInactive: true);
                               foreach (var sp in prefabs)
                               {
                                       if (sp == null || string.IsNullOrEmpty(sp.UniqueID)) continue;
                                       if (snapshotted.Contains(sp.gameObject)) continue; // already handled
                                       
                                       SnapshotObjectToSceneCache(sp.gameObject, sceneName);
                                       snapshotted.Add(sp.gameObject);
                                       uidCount++;
                               }
                       }
                       
                       if (sceneSnapshots.TryGetValue(sceneName, out var perScene))
                       {
                               foreach (var kv in perScene)
                                       totalComponents += kv.Value?.Count ?? 0;
                       }
                       Logger.Log($"[RememberHome] SnapshotSceneAll: Done scene '{sceneName}'. Objects={uidCount}, Components={totalComponents}", LogCategory.ComponentManager, LogLevel.Info);
               }

               /// <summary>
               /// Clears cached snapshots for a specific scene name.
               /// </summary>
               public void ClearSceneSnapshot(string sceneName)
               {
                       if (string.IsNullOrEmpty(sceneName)) return;
                       if (sceneSnapshots.Remove(sceneName))
                               Logger.Log($"ComponentManager: Cleared snapshot cache for scene '{sceneName}'.", LogCategory.ComponentManager, LogLevel.Info);
               }

               /// <summary>
               /// Attempts to apply a previously cached snapshot to a component when it registers
               /// and the current scene matches its home scene preference.
               /// </summary>
               private void TryApplyRememberedSnapshot(ISaveable component)
               {
                       if (component is not SaveableComponent sc) return;
                       if (!sc.RememberHomeScene)
                       {
                               Logger.Log($"[RememberHome] ApplySkip: '{component.UniqueIdentifier}' not marked RememberHomeScene.", LogCategory.ComponentManager, LogLevel.Info);
                               return;
                       }

                       var scene = SceneManager.GetActiveScene();
                       if (!scene.IsValid()) { Logger.Log($"[RememberHome] ApplySkip: current scene invalid.", LogCategory.ComponentManager, LogLevel.Info); return; }
                       if (string.IsNullOrEmpty(sc.HomeScene)) { Logger.Log($"[RememberHome] ApplySkip: HomeScene empty for '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info); return; }

                       // Only apply in its designated home scene
                       if (!sc.MatchesHomeScene(scene.name))
                       {
                               var primary = sc.HomeScene;
                               Logger.Log($"[RememberHome] ApplySkip: Current='{scene.name}' not HomeScene (primary='{primary}') for '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                               return;
                       }

                       // Find per-scene cache
                       if (!sceneSnapshots.TryGetValue(scene.name, out var sceneDict))
                       {
                               Logger.Log($"[RememberHome] ApplyMiss: No scene cache for '{scene.name}'.", LogCategory.ComponentManager, LogLevel.Info);
                               return;
                       }

                       // Find per-object cache by this GameObject UniqueID
                       var goUID = sc.GameObjectUniqueID;
                       if (string.IsNullOrEmpty(goUID)) { Logger.Log($"[RememberHome] ApplySkip: GameObjectUniqueID empty for '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info); return; }
                       if (!sceneDict.TryGetValue(goUID, out var perObject))
                       {
                               Logger.Log($"[RememberHome] ApplyMiss: No object entry for GOUID='{goUID}' in scene '{scene.name}'.", LogCategory.ComponentManager, LogLevel.Info);
                               return;
                       }

                       // If the component has cached data, apply it now
                       if (perObject.TryGetValue(sc.UniqueIdentifier, out var data) && data != null && data.Length > 0)
                       {
                               if (HasComponentDeserialized(sc.UniqueIdentifier))
                               {
                                       Logger.Log($"[RememberHome] ApplySkip: Duplicate LoadData for '{sc.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                               }
                               else
                               {
                                       try
                                       {
                                               sc.LoadData(data);
                                               MarkComponentDeserialized(sc.UniqueIdentifier);
                                               Logger.Log($"[RememberHome] ApplyOK: Applied snapshot to '{sc.UniqueIdentifier}' in scene '{scene.name}'. Bytes={data.Length}", LogCategory.ComponentManager, LogLevel.Info);
                                       }
                                       catch (Exception ex)
                                       {
                                               Logger.Log($"[RememberHome] ApplyERR: '{sc.UniqueIdentifier}' failed: {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                                       }
                               }
                       }
                       else
                       {
                               Logger.Log($"[RememberHome] ApplyMiss: No component entry for '{sc.UniqueIdentifier}' under GOUID='{goUID}' in scene '{scene.name}'.", LogCategory.ComponentManager, LogLevel.Info);
                       }
               }

               private Dictionary<string, Dictionary<string, byte[]>> GetOrCreate(string sceneName)
               {
                       if (!sceneSnapshots.TryGetValue(sceneName, out var d))
                       {
                               d = new Dictionary<string, Dictionary<string, byte[]>>();
                               sceneSnapshots[sceneName] = d;
                       }
                       return d;
               }

               private Dictionary<string, string> GetOrCreatePrefabMap(string sceneName)
               {
                       if (!scenePrefabRefs.TryGetValue(sceneName, out var d))
                       {
                               d = new Dictionary<string, string>();
                               scenePrefabRefs[sceneName] = d;
                       }
                       return d;
               }


        /// <summary>
        /// Retrieves all registered SaveableComponents.
        /// </summary>
        public IReadOnlyList<ISaveable> GetSaveableComponents()
        {
            return saveableComponents.AsReadOnly();
        }

                /// <summary>
                /// Replaces the internal sceneSnapshots cache with data provided from loaded SaveData.
                /// Call this early during load before components register so TryApplyRememberedSnapshot can succeed.
                /// </summary>
                public void ImportHomeSceneSnapshots(
                                Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>> snapshots,
                                Dictionary<string, Dictionary<string, string>> prefabRefs = null)
                {
                        sceneSnapshots.Clear();
                        scenePrefabRefs.Clear();
                        if (snapshots != null && snapshots.Count > 0)
                        {
                                foreach (var sceneKvp in snapshots)
                                {
                                        var perObject = new Dictionary<string, Dictionary<string, byte[]>>();
                                        foreach (var objKvp in sceneKvp.Value)
                                        {
                                                var perComp = new Dictionary<string, byte[]>();
                                                foreach (var compKvp in objKvp.Value)
                                                        perComp[compKvp.Key] = compKvp.Value;
                                                perObject[objKvp.Key] = perComp;
                                        }
                                        sceneSnapshots[sceneKvp.Key] = perObject;
                                }
                        }

                        if (prefabRefs != null && prefabRefs.Count > 0)
                        {
                                foreach (var sceneKvp in prefabRefs)
                                {
                                        scenePrefabRefs[sceneKvp.Key] = new Dictionary<string, string>(sceneKvp.Value);
                                }
                        }

                       Logger.Log($"[RememberHome] Import: Restored snapshot cache. Scenes={sceneSnapshots.Count}", LogCategory.ComponentManager, LogLevel.Info);
               }

               public void InstantiateMissingHomeSceneObjectsForLoadedScenes()
               {
                       for (int i = 0; i < SceneManager.sceneCount; i++)
                       {
                               var scene = SceneManager.GetSceneAt(i);
                               if (scene.IsValid() && scene.isLoaded)
                                       InstantiateMissingHomeSceneObjects(scene);
                       }
               }

               private void InstantiateMissingHomeSceneObjects(Scene scene)
               {
                       if (!scenePrefabRefs.TryGetValue(scene.name, out var prefabMap)) return;
                       foreach (var kvp in prefabMap)
                       {
                               string goUID = kvp.Key;
                               string prefabID = kvp.Value;
                               if (SaveManager.Instance?.FindGameObjectByUniqueID(goUID, SaveManager.IdentifierType.UniqueID) != null)
                                       continue;

                               PrefabRegistry registry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
                               var prefabAsset = registry != null ? registry.FindPrefabByID(prefabID) : null;
                               if (prefabAsset == null) continue;

                               var sp = SaveablePrefabFactory.Instantiate(prefabAsset, Vector3.zero, Quaternion.identity, scene.name);
                               if (sp != null)
                                       sp.SetUniqueID(goUID);
                       }
               }

        #endregion

        #region Parenting Management

        private readonly List<ParentingRequest> parentingQueue = new List<ParentingRequest>();

        /// <summary>
        /// Queues a parenting request to be processed after all components are loaded.
        /// </summary>
        /// <param name="childTransform">The Transform of the child GameObject.</param>
        /// <param name="parentUniqueID">The UniqueID of the parent GameObject.</param>
        public void QueueParenting(Transform child, string parentUniqueID)
        {
            if (child == null || string.IsNullOrEmpty(parentUniqueID))
            {
                Logger.Log("ComponentManager: Invalid parenting request. ChildTransform or ParentUniqueID is null.", LogCategory.ComponentManager, LogLevel.Warning);
                return;
            }

            parentingQueue.Add(new ParentingRequest { Child = child, ParentUniqueID = parentUniqueID });
            //Debug.Log($"[ComponentManager] QueueParenting - Queued parenting for '{child.gameObject.name}' (Active: {child.gameObject.activeSelf}) to parent ID '{parentUniqueID}'.");
        }

        /// <summary>
        /// Processes all queued parenting requests.
        /// </summary>
        private void ProcessQueuedParenting()
        {
            if (parentingQueue.Count == 0) return;

            //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Processing {parentingQueue.Count} parenting requests.");
            var manager = SaveManager.Instance;
            for (int iteration = 0; iteration < MaxParentingResolveIterations && parentingQueue.Count > 0; iteration++)
            {
                int unresolvedAtStart = parentingQueue.Count;

                for (int i = parentingQueue.Count - 1; i >= 0; i--)
                {
                    var req = parentingQueue[i];

                    // Skip and remove if the child was destroyed in the meantime
                    if (req.Child == null)
                    {
                        parentingQueue.RemoveAt(i);
                        Logger.Log($"ComponentManager: Skipped parenting request for destroyed child (parent ID '{req.ParentUniqueID}').", LogCategory.ComponentManager, LogLevel.Warning);
                        continue;
                    }

                    GameObject parent = manager?.FindGameObjectByUniqueID(req.ParentUniqueID, SaveManager.IdentifierType.UniqueID);
                    if (parent == null)
                    {
                        //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Iteration {iteration}: Parent with ID '{req.ParentUniqueID}' not found for child '{req.Child.gameObject.name}' (Active: {req.Child.gameObject.activeSelf}).");
                        continue;
                    }

                    //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Parenting '{req.Child.gameObject.name}' (ActiveInHierarchy: {req.Child.gameObject.activeInHierarchy}) to '{parent.name}' (ActiveInHierarchy: {parent.gameObject.activeInHierarchy}).");
                    req.Child.SetParent(parent.transform);
                    parentingQueue.RemoveAt(i);
                    //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Successfully parented '{req.Child.gameObject.name}' to '{parent.name}'. Current parent: '{req.Child.parent?.name ?? "null"}'.");
                }

                if (parentingQueue.Count == unresolvedAtStart) break;
            }

            if (parentingQueue.Count > 0)
                Logger.Log($"[ComponentManager] ProcessQueuedParenting - {parentingQueue.Count} parenting requests unresolved after retries.", LogCategory.ComponentManager, LogLevel.Warning);
        }

        #endregion

        #region Helper Methods

        private ISaveable FindComponentByUniqueID(string uniqueID)
        {
            if (SaveManager.Instance?.SaveSettings?.enableComponentLookupCache == true)
            {
                if (saveableLookup.TryGetValue(uniqueID, out var comp))
                {
                    // Safety check: ensure the component hasn't been destroyed (Unity null check)
                    if (comp is MonoBehaviour mb && mb == null)
                    {
                        // Stale reference - remove it from lookup and return null
                        saveableLookup.Remove(uniqueID);
                        saveableComponents.Remove(comp);
                        return null;
                    }
                    return comp;
                }

                // Cache miss recovery: scan the live list once and repopulate lookup entries.
                // This heals key drift when a component registered before its identifier changed.
                for (int i = saveableComponents.Count - 1; i >= 0; i--)
                {
                        var candidate = saveableComponents[i];
                        if (candidate == null)
                        {
                                saveableComponents.RemoveAt(i);
                                continue;
                        }

                        if (candidate is MonoBehaviour candidateMb && candidateMb == null)
                        {
                                saveableComponents.RemoveAt(i);
                                continue;
                        }

                        string candidateId = null;
                        try
                        {
                                candidateId = candidate.UniqueIdentifier;
                        }
                        catch
                        {
                                continue;
                        }

                        if (string.IsNullOrEmpty(candidateId))
                                continue;

                        saveableLookup[candidateId] = candidate;

                        if (string.Equals(candidateId, uniqueID, StringComparison.Ordinal))
                                return candidate;
                }

                return null;
            }

            return saveableComponents.FirstOrDefault(c =>
            {
                if (c is MonoBehaviour mb && mb == null)
                    return false;
                return c.UniqueIdentifier == uniqueID;
            });
        }

        public bool Contains(ISaveable comp) => saveableComponents.Contains(comp);

        #endregion

        #region Single-Object Methods

        /// <summary>
        /// Gathers serialized data for every ISaveable component on a single GameObject (including children).
        /// Returns a Dictionary keyed by the component's UniqueIdentifier, with each value being serialized bytes.
        /// </summary>
               public Dictionary<string, byte[]> CollectComponentDataForObject(GameObject obj)
               {
                       var result = new Dictionary<string, byte[]>();
                       if (obj == null)
                       {
                               Logger.Log("CollectComponentDataForObject: Received null GameObject.", LogCategory.ComponentManager, LogLevel.Info);
                               return result;
                       }
                       foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
                       {
                               // Skip entries whose underlying Unity object has been destroyed
                               if (comp is MonoBehaviour mb && mb == null)
                                       continue;
                               if (!IsValidSaveable(comp, nameof(CollectComponentDataForObject)))
                                       continue;

                               try
                               {
                                       byte[] serializedData = comp.SaveData();
                                       if (serializedData != null && serializedData.Length > 0)
                                               result[comp.UniqueIdentifier] = serializedData;
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log($"CollectComponentDataForObject: Error serializing '{comp.UniqueIdentifier}': {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       return result;
               }

        /// <summary>
#if CRYSTALSAVE_TIMEMACHINE
        /// <summary>
        /// Optimized collection method for TimeMachine recording that only captures specific component types.
        /// This prevents performance issues by avoiding expensive components like ParticleSystem, Rigidbody, etc.
        /// when we only need to record Transform data for timeline playback.
        /// </summary>
        /// <param name="obj">GameObject to collect data from</param>
        /// <param name="allowedTypes">Set of component types to record. If null, records all ISaveable components (legacy behavior)</param>
        /// <returns>Dictionary of component data keyed by UniqueIdentifier</returns>
               public Dictionary<string, byte[]> CollectComponentDataForTimeMachine(GameObject obj, HashSet<System.Type> allowedTypes = null)
               {
                       var result = new Dictionary<string, byte[]>();
                       if (obj == null)
                       {
                               //Logger.Log("[TIMEMACHINE] CollectComponentDataForTimeMachine: Received null GameObject.", LogLevel.Info);
                               return result;
                       }

                       // If no filtering specified, fall back to collecting all components
                       if (allowedTypes == null)
                       {
                               return CollectComponentDataForObject(obj);
                       }

                       foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
                       {
                               // Skip entries whose underlying Unity object has been destroyed
                               if (comp is MonoBehaviour mb && mb == null)
                                       continue;
                               if (!IsValidSaveable(comp, nameof(CollectComponentDataForTimeMachine)))
                                       continue;

                               // Filter by component type - this is the key performance optimization
                               System.Type componentType = comp.GetType();
                               if (!allowedTypes.Contains(componentType))
                               {
                                       //Logger.Log($"[TIMEMACHINE] Skipping component type {componentType.Name} - not in allowed types.", LogLevel.Debug);
                                       continue;
                               }

                               try
                               {
                                       byte[] serializedData = comp.SaveData();
                                       if (serializedData != null && serializedData.Length > 0)
                                       {
                                               result[comp.UniqueIdentifier] = serializedData;
                                               //Logger.Log($"[TIMEMACHINE] Recorded {componentType.Name} data ({serializedData.Length} bytes)", LogLevel.Debug);
                                       }
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log($"[TIMEMACHINE] Error serializing '{comp.UniqueIdentifier}': {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       return result;
               }
#endif

        /// <summary>
        /// Applies data for every ISaveable component on a single GameObject (including children),
        /// matching by UniqueIdentifier. If a matching component is found, LoadData() is invoked.
        /// </summary>
               public void ApplyComponentDataToObject(
                       GameObject obj,
                       Dictionary<string, byte[]> componentData,
                       bool forceDuplicateLoads = false)
               {
                       if (obj == null || componentData == null || componentData.Count == 0) return;

                       bool forceAll = forceDuplicateLoads;
                       if (!forceAll && obj != null)
                       {
                               var prefabContext = obj.transform.GetComponentInParent<SaveablePrefab>(true);
                               if (prefabContext != null && prefabContext.ApplySavedComponentDataOnRespawn)
                               {
                                       forceAll = true;
                               }
                       }

                       foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
                       {
                               if (!IsValidSaveable(comp, nameof(ApplyComponentDataToObject))) continue;
                               if (!componentData.TryGetValue(comp.UniqueIdentifier, out var data)) continue;

                               bool allowMultipleLoads = forceAll;

                               if (!allowMultipleLoads && comp is SaveableComponent saveableComponent && saveableComponent.ApplySavedDataOnRestore)
                               {
                                       allowMultipleLoads = true;
                               }

                               if (!allowMultipleLoads && HasComponentDeserialized(comp.UniqueIdentifier))
                               {
                                       Logger.Log($"ApplyComponentDataToObject: Skipping duplicate LoadData for '{comp.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Warning);
                                       continue;
                               }

                               try
                               {
                                       comp.LoadData(data);
                                       MarkComponentDeserialized(comp.UniqueIdentifier);
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log($"ApplyComponentDataToObject: Error applying data to '{comp.UniqueIdentifier}': {ex.Message}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       ProcessQueuedParenting();
               }

        #endregion

        #region Nested Classes

        private class ParentingRequest
        {
            public Transform Child { get; set; }
            public string ParentUniqueID { get; set; }
        }

        #endregion
    }
}
#endif
