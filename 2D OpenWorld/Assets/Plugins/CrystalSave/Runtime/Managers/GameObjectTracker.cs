#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Tracks GameObjects for the SaveManager and handles active-state watching.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public class GameObjectTracker : MonoBehaviour
    {
        
        readonly Dictionary<string, TrackedGameObject> trackedGameObjects = new();
        readonly object trackedLock = new();

        readonly HashSet<string> destroyedIDs = new();
        readonly Dictionary<string, bool> activeStates = new();
        readonly object activeLock = new();

        internal Dictionary<string, TrackedGameObject> TrackedObjects => trackedGameObjects;
        internal object TrackedLock => trackedLock;
        internal HashSet<string> DestroyedIDs => destroyedIDs;
        internal Dictionary<string, bool> ActiveStates => activeStates;
        internal object ActiveStatesLock => activeLock;

        [SerializeField] int  activeStateEnforceDelayFrames = 1;
        [SerializeField] bool enforceActiveState = false;
        [SerializeField] float activeStateWatchDuration = 0f;
        Coroutine watchRoutine;

        internal int ActiveStateEnforceDelayFrames => activeStateEnforceDelayFrames;
        internal bool EnforceActiveState => enforceActiveState;
        internal float ActiveStateWatchDuration => activeStateWatchDuration;

        SaveManager saveManager;
        PrefabManager prefabManager;
        ComponentManager componentManager;

        internal SaveManager SaveManager => saveManager;

        internal void Initialize(SaveManager mgr, PrefabManager prefabMgr, ComponentManager compMgr)
        {
            saveManager = mgr;
            prefabManager = prefabMgr;
            componentManager = compMgr;
        }

        #region Accessors
        public bool IsTracked(string id)
        {
            lock (trackedLock) return trackedGameObjects.ContainsKey(id);
        }

        public bool IsGameObjectDestroyed(string id) => destroyedIDs.Contains(id);

        public Dictionary<string, TrackedGameObject> GetTrackedGameObjects()
        {
            lock (trackedLock) return new Dictionary<string, TrackedGameObject>(trackedGameObjects);
        }

        public List<string> GetDestroyedGameObjectIDs() => destroyedIDs.ToList();

        #endregion

        string GetUniqueID(GameObject obj)
        {
            // Fast path: use caches populated by RememberGameObject
            if (RememberGameObject.TryGetCachedUniqueIdentifier(obj, out var cachedUid) && !string.IsNullOrEmpty(cachedUid))
                return cachedUid;

            if (RememberGameObject.TryGetCachedRemember(obj, out var cachedRemember) && cachedRemember != null)
            {
                // If GameObjectUniqueID is ready, UniqueIdentifier is fully formed
                if (!string.IsNullOrEmpty(cachedRemember.GameObjectUniqueID))
                    return cachedRemember.UniqueIdentifier;

                // Fallback: compose from UniqueID component + Remember componentID
                if (obj.TryGetComponent<UniqueID>(out var uidComp) && !string.IsNullOrEmpty(uidComp.ID))
                    return $"{uidComp.ID}_{cachedRemember.ComponentID}";
            }

            // Prefer UniqueID before falling back to SaveablePrefab or SceneObjectID
            if (obj.TryGetComponent<UniqueID>(out var fallbackUid) && !string.IsNullOrEmpty(fallbackUid.ID))
                return fallbackUid.ID;

            // Slow path: SaveablePrefab identifiers and caches
            string saveablePrefabId = null;
            if (SaveablePrefab.TryGetCachedUniqueID(obj, out var cachedSpId) && !string.IsNullOrEmpty(cachedSpId))
            {
                saveablePrefabId = cachedSpId;
            }
            else if (SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedSp) && cachedSp != null && !string.IsNullOrEmpty(cachedSp.UniqueID))
            {
                saveablePrefabId = cachedSp.UniqueID;
            }
            else if (obj.TryGetComponent<SaveablePrefab>(out var sp) && !string.IsNullOrEmpty(sp.UniqueID))
            {
                saveablePrefabId = sp.UniqueID;
            }

            if (!string.IsNullOrEmpty(saveablePrefabId))
            {
                if (obj.TryGetComponent<SceneObjectID>(out var sceneId) && !string.IsNullOrEmpty(sceneId.UniqueID))
                {
                    // Prefer SaveablePrefab ID over SceneObjectID; no debug log.
                }

                return saveablePrefabId;
            }

            if (obj.TryGetComponent<SceneObjectID>(out var fallbackSceneId) && !string.IsNullOrEmpty(fallbackSceneId.UniqueID))
                return fallbackSceneId.UniqueID;

            return null;
        }

        public void RegisterGameObject(GameObject obj, GameObjectPropertySettings settings)
        {
            if (obj == null || settings == null) return;
            string id = GetUniqueID(obj);
            if (string.IsNullOrEmpty(id)) return;

            lock (trackedLock)
            {
                trackedGameObjects[id] = new TrackedGameObject(obj, settings);
            }

            // Always cache the object regardless of its active state to prevent first-load issues
            // This is especially important for objects that are inactive at design time
            saveManager?.CacheGameObject(id, obj);

            if (settings.RememberActive)
            {
                lock (activeLock) 
                {
                    // Only set the initial active state if not already tracked
                    // This prevents overwriting the correct active state during re-registration
                    if (!activeStates.ContainsKey(id))
                    {
                        activeStates[id] = obj.activeSelf;
                    }
                    else
                    {
                        // Preserve existing value when the object re-registers
                    }
                }
            }

            var proxy = obj.GetComponent<TrackedGameObjectProxy>();
            if (proxy == null) proxy = obj.AddComponent<TrackedGameObjectProxy>();
            proxy.Initialize(this, id);
        }

        public void UnregisterGameObject(GameObject obj)
        {
            if (obj == null) return;
            string id = GetUniqueID(obj);
            if (string.IsNullOrEmpty(id)) return;

            lock (trackedLock) trackedGameObjects.Remove(id);
            lock (activeLock) activeStates.Remove(id);

            saveManager?.UncacheGameObject(id);

            var proxy = obj.GetComponent<TrackedGameObjectProxy>();
            // Only remove the proxy component; do NOT destroy the entire GameObject.
            if (proxy != null) Destroy(proxy);
        }

        public void SoftUnregisterGameObject(GameObject obj)
        {
            if (!obj) return;
            string id = GetUniqueID(obj);
            if (string.IsNullOrEmpty(id)) return;

            lock (trackedLock) trackedGameObjects.Remove(id);
            lock (activeLock) activeStates.Remove(id);

            saveManager?.UncacheGameObject(id);

            var proxy = obj.GetComponent<TrackedGameObjectProxy>();
            if (proxy != null) Destroy(proxy);

            var sp = SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedSp) ? cachedSp : obj.GetComponent<SaveablePrefab>();
            if (sp != null)
            {
                sp.ClearRegisteredFlag();
                if (prefabManager != null && prefabManager.SaveablePrefabs.Contains(sp))
                    prefabManager.UnregisterPrefab(sp);
                // Only reset for pooling (which clears UniqueID) if this was a runtime-added instance.
                // Scene-backed prefabs should retain their UniqueID to remain anchorable across loads.
                if (sp.IsAddedAtRuntime)
                    sp.ResetForPooling();
            }
        }

        public void RegisterDestroyedGameObject(string id)
        {
            if (string.IsNullOrEmpty(id) || destroyedIDs.Contains(id)) return;

            if (saveManager.CurrentSaveData == null)
                saveManager.CurrentSaveData = new SaveData();

            if (trackedGameObjects.TryGetValue(id, out var tgo) && tgo?.GameObject != null)
            {
                var obj = tgo.GameObject;
                UnityEngine.Debug.Log($"[GameObjectTracker.RegisterDestroyedGameObject] Marking '{obj.name}' (ID: {id}) as destroyed in scene '{obj.scene.name}'.", obj);
                var data = componentManager.CollectComponentDataForObject(obj);
                saveManager.CurrentSaveData.DestroyedObjectData[id] = data;
            }
            else
            {
                UnityEngine.Debug.Log($"[GameObjectTracker.RegisterDestroyedGameObject] Marking ID '{id}' as destroyed (object not currently tracked).");
            }

            destroyedIDs.Add(id);
            lock (trackedLock) trackedGameObjects.Remove(id);
            lock (activeLock) activeStates.Remove(id);
            saveManager?.UncacheGameObject(id);
        }

        internal void RemoveDestroyedID(string id) => destroyedIDs.Remove(id);
        internal void RemoveDestroyedWhere(System.Predicate<string> match) => destroyedIDs.RemoveWhere(match);

        public void CaptureDestroyedDataIfPossible(string id)
        {
            if (saveManager.CurrentSaveData == null)
                saveManager.CurrentSaveData = new SaveData();

            if (trackedGameObjects.TryGetValue(id, out var tgo) && tgo?.GameObject != null)
            {
                var obj = tgo.GameObject;
                var data = componentManager.CollectComponentDataForObject(obj);
                saveManager.CurrentSaveData.DestroyedObjectData[id] = data;
                
                var sp = SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedSp) ? cachedSp : obj.GetComponent<SaveablePrefab>();
                if (sp != null && prefabManager != null)
                {
                    // Standard path: build full prefab data from SaveablePrefab
                    var pd = prefabManager.BuildPrefabData(sp);
                    if (pd != null)
                    {
                        saveManager.CurrentSaveData.Prefabs.RemoveAll(p => p.InstanceID == id);
                        saveManager.CurrentSaveData.Prefabs.Add(pd);
                        
                    }
                }
                else
                {
                    // New: Scene object without SaveablePrefab – persist its last known transform
                    // so RestoreDestroyedGameObject can place the restored prefab at the correct spot.
                    var tr = obj.transform;
                    string parentId = null;
                    bool isParentSceneObject = false;
                    if (tr != null && tr.parent != null)
                    {
                        var parentGO = tr.parent.gameObject;
                        parentId = GetUniqueID(parentGO);
                        // Consider it a scene object parent if it has a SceneObjectID
                        isParentSceneObject = parentGO.GetComponent<SceneObjectID>() != null;
                    }

                    var synthesized = new SaveablePrefabData(
                        instanceID: id,
                        prefabID: string.Empty, // unknown here, mapping is handled via SceneObjectRegistry during restore
                        gameObjectName: obj.name,
                        position: tr != null ? tr.position : Vector3.zero,
                        rotation: tr != null ? tr.rotation : Quaternion.identity,
                        scale: tr != null ? tr.localScale : Vector3.one,
                        parentID: parentId,
                        isParentSceneObject: isParentSceneObject,
                        visibilitySettingsData: null,
                        homeScene: null,
                        disablePooling: false
                    );
                    synthesized.HasTransformOverride = true;
                    synthesized.HasParentOverride = !string.IsNullOrEmpty(parentId);

                    saveManager.CurrentSaveData.Prefabs.RemoveAll(p => p.InstanceID == id);
                    saveManager.CurrentSaveData.Prefabs.Add(synthesized);
                    
                }
            }
        }

        public bool TryGetTrackedGameObject(string id, out TrackedGameObject tracked)
        {
            lock (trackedLock) return trackedGameObjects.TryGetValue(id, out tracked);
        }

        public void UpdateActiveState(string id, bool isActive)
        {
            lock (trackedLock)
            {
                if (trackedGameObjects.TryGetValue(id, out var tgo) && tgo.Settings.RememberActive)
                {
                    bool isSceneTransition = saveManager != null && saveManager.IsInSceneTransition;

                    lock (activeLock)
                    {
                        if (isSceneTransition && !isActive && activeStates.TryGetValue(id, out var previousValue) && previousValue)
                        {
                            // Ignore temporary deactivations triggered by scene unload while we're in a managed transition.
                            return;
                        }

                        activeStates[id] = isActive;
                    }
                }
            }
        }

        public List<GameObjectState> CollectActiveStates()
        {
            var list = new List<GameObjectState>();
            var updatesToCache = new System.Collections.Generic.List<(string id, bool state)>();
            
            lock (activeLock)
            {
                foreach (var kvp in activeStates)
                {
                    string uniqueId = kvp.Key;
                    bool isActive = kvp.Value;

                    // Skip emitting active-state data for reusable SaveablePrefabs. Their
                    // persisted prefab snapshot already records the correct state and the
                    // prefab pipeline should remain authoritative to avoid reactivation when
                    // scenes are reloaded.
                    GameObject candidate = saveManager != null
                        ? saveManager.FindGameObjectByUniqueID(uniqueId, SaveManager.IdentifierType.UniqueID)
                        : null;

                    if (candidate != null && candidate.TryGetComponent<SaveablePrefab>(out var sp))
                    {
                        if (sp.ReuseSceneInstanceOnLoad && !sp.IsAddedAtRuntime)
                        {
                            Logger.Log($"[CrystalSave][GameObjectTracker] CollectActiveStates: skipping reusable SaveablePrefab '{candidate.name}' (UniqueID '{uniqueId}')", LogCategory.GameObjectTracker, LogLevel.Info);
                            continue;
                        }
                    }

                    // Fix for first-save issue: Query the actual GameObject's current state instead of
                    // relying solely on the cached value. This ensures we capture runtime state changes
                    // even if the proxy's OnEnable/OnDisable didn't fire or the cache is stale.
                    if (candidate != null)
                    {
                        bool actualState = candidate.activeSelf;
                        // Store updates to apply after enumeration to avoid concurrent modification
                        if (actualState != isActive)
                        {
                            updatesToCache.Add((uniqueId, actualState));
                        }
                        isActive = actualState;
                    }

                    list.Add(new GameObjectState { UniqueID = uniqueId, IsActive = isActive });
                }
                
                // Apply cache updates after enumeration is complete
                foreach (var update in updatesToCache)
                {
                    activeStates[update.id] = update.state;
                }
            }
            return list;
        }

        private bool ShouldSkipActiveStateForRememberHome(GameObject go)
        {
            if (go == null) return false;
            if (go.TryGetComponent<RememberGameObject>(out var rememberGO) &&
                rememberGO != null &&
                rememberGO.RememberHomeScene)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid() && rememberGO.MatchesHomeScene(scene.name))
                {
                    return true;
                }
            }
            return false;
        }

        public void ApplyGameObjectActiveStates(List<GameObjectState> states)
        {
            if (states == null) return;
            int batchSize = saveManager.SaveSettings?.activeStateApplyBatchSize ?? 0;
            if (batchSize <= 0)
            {
                foreach (var state in states)
                {
                    if (!state.IsActive.HasValue) continue;
                    if (!IsTracked(state.UniqueID)) continue;

                    var go = saveManager.FindGameObjectByUniqueID(state.UniqueID, SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;

                    // Check if this GameObject has a SaveablePrefab that reuses scene instances
                    // If so, the prefab system should take precedence over GameObject active states
                    bool skipForReusablePrefab = false;
                    if (go.TryGetComponent<SaveablePrefab>(out var saveablePrefab))
                    {
                        if (saveablePrefab.ReuseSceneInstanceOnLoad && !saveablePrefab.IsAddedAtRuntime)
                        {
                            Logger.Log($"[CrystalSave][GameObjectTracker] Skipping active state application for reusable SaveablePrefab '{go.name}' (UniqueID '{state.UniqueID}') - prefab system takes precedence", LogCategory.GameObjectTracker, LogLevel.Info);
                            skipForReusablePrefab = true;
                        }
                    }
                    else
                    {
                        // No SaveablePrefab component found, continue with normal processing
                    }

                    if (skipForReusablePrefab)
                    {
                        // Still update our internal tracking but don't change the actual GameObject
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }

                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        // RememberHomeScene snapshot is authoritative in its home scene.
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }

                    RememberGameObject rememberComponent = null;
                    go.TryGetComponent<RememberGameObject>(out rememberComponent);

                    try
                    {
                        // Set flag to prevent OnDestroy from registering destroyed GameObjects during state application
                        if (rememberComponent != null)
                        {
                            rememberComponent.SetApplyingActiveState(true);
                        }
                        
                        try
                        {
                            go.SetActive(state.IsActive.Value);
                            lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                        }
                        finally
                        {
                            // Clear flag after state application (even if SetActive throws)
                            if (rememberComponent != null)
                            {
                                rememberComponent.SetApplyingActiveState(false);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.Log($"GameObjectTracker: Error applying active state for '{state.UniqueID}': {ex.Message}", LogCategory.GameObjectTracker, LogLevel.Error);
                    }
                }
                StartCoroutine(EnforceActiveStatesCoroutine(states, batchSize));
            }
            else
            {
                StartCoroutine(ApplyActiveStatesCoroutine(states, batchSize));
            }
        }

        IEnumerator ApplyActiveStatesCoroutine(List<GameObjectState> states, int batchSize)
        {
            if (states == null) yield break;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!state.IsActive.HasValue) continue;
                if (!IsTracked(state.UniqueID)) continue;

                var go = saveManager.FindGameObjectByUniqueID(state.UniqueID, SaveManager.IdentifierType.UniqueID);
                if (go == null) continue;

                // Check if this GameObject has a SaveablePrefab that reuses scene instances
                // If so, the prefab system should take precedence over GameObject active states
                bool skipForReusablePrefab = false;
                if (go.TryGetComponent<SaveablePrefab>(out var saveablePrefab))
                {
                    if (saveablePrefab.ReuseSceneInstanceOnLoad && !saveablePrefab.IsAddedAtRuntime)
                    {
                        skipForReusablePrefab = true;
                    }
                }

                if (skipForReusablePrefab)
                {
                    // Still update our internal tracking but don't change the actual GameObject
                    lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                    continue;
                }

                if (ShouldSkipActiveStateForRememberHome(go))
                {
                    // RememberHomeScene snapshot is authoritative in its home scene.
                    lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                    continue;
                }

                RememberGameObject rememberComponent = null;
                go.TryGetComponent<RememberGameObject>(out rememberComponent);

                try
                {
                    // Set flag to prevent OnDestroy from registering destroyed GameObjects during state application
                    if (rememberComponent != null)
                    {
                        rememberComponent.SetApplyingActiveState(true);
                    }
                    
                    try
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                    finally
                    {
                        // Clear flag after state application (even if SetActive throws)
                        if (rememberComponent != null)
                        {
                            rememberComponent.SetApplyingActiveState(false);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Log($"GameObjectTracker: Error applying active state for '{state.UniqueID}': {ex.Message}", LogCategory.GameObjectTracker, LogLevel.Error);
                }

                if ((i + 1) % batchSize == 0)
                    yield return null;
            }
            StartCoroutine(EnforceActiveStatesCoroutine(states, batchSize));
        }

        IEnumerator EnforceActiveStatesCoroutine(List<GameObjectState> states, int batchSize)
        {
            if (states == null) yield break;
            for (int i = 0; i < activeStateEnforceDelayFrames; i++)
                yield return null;

            if (batchSize <= 0)
            {
                foreach (var state in states)
                {
                    if (!state.IsActive.HasValue) continue;
                    var go = saveManager.FindGameObjectByUniqueID(state.UniqueID, SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;
                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }
                    if (go.activeSelf != state.IsActive.Value)
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                }
            }
            else
            {
                for (int i = 0; i < states.Count; i++)
                {
                    var state = states[i];
                    if (!state.IsActive.HasValue) continue;
                    var go = saveManager.FindGameObjectByUniqueID(state.UniqueID, SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;
                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }
                    if (go.activeSelf != state.IsActive.Value)
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                    if ((i + 1) % batchSize == 0)
                        yield return null;
                }
            }
        }

        IEnumerator WatchActiveStatesCoroutine(List<GameObjectState> states)
        {
            if (states == null) yield break;
            float elapsed = 0f;
            while (elapsed < activeStateWatchDuration)
            {
                foreach (var state in states)
                {
                    if (!state.IsActive.HasValue) continue;
                    var go = saveManager.FindGameObjectByUniqueID(state.UniqueID, SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;
                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }
                    if (go.activeSelf != state.IsActive.Value)
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            watchRoutine = null;
        }

        public void StartActiveStateWatch(List<GameObjectState> states)
        {
            if (activeStateWatchDuration <= 0f) return;
            StopActiveStateWatch();
            watchRoutine = StartCoroutine(WatchActiveStatesCoroutine(states));
        }

        public void StopActiveStateWatch()
        {
            if (watchRoutine != null)
            {
                StopCoroutine(watchRoutine);
                watchRoutine = null;
            }
        }
    }
}
#endif
