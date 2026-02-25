#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    [DefaultExecutionOrder(-30)]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class PrefabManager : MonoBehaviour 
    {
        #region Fields and Properties

        [SerializeField]
        [Tooltip("Reference to the Prefab Registry asset.")]
        private PrefabRegistry prefabRegistry;

        [SerializeField]
        [Tooltip("List of registered SaveablePrefabs in the scene.")]
        private List<SaveablePrefab> saveablePrefabs = new List<SaveablePrefab>();

        // Dictionary to keep track of instantiated prefabs by their unique IDs.
        private readonly Dictionary<string, GameObject> instantiatedPrefabs = new Dictionary<string, GameObject>();

        private readonly Dictionary<string, List<SaveablePrefabData>> pendingPrefabs = new();
        private readonly Dictionary<string, Queue<SaveablePrefabData>> deferredPrefabs = new();
        private bool deferredPrefabsStagedSinceLastClear = false;
        private const string GlobalDeferredSceneKey = "__GLOBAL__";

        // Per-prefab locks to serialize access when temporarily flagging the asset as loading.
        private static readonly ConcurrentDictionary<GameObject, object> prefabLocks = new();

        private bool UsePrefabPooling =>
                SaveManager.Instance?.SaveSettings?.usePrefabPooling ?? false;

        private int DefaultPoolSize =>
                SaveManager.Instance?.SaveSettings?.defaultPrefabPoolSize ?? 0;

        public List<SaveablePrefab> SaveablePrefabs => saveablePrefabs;

        /// <summary>
        /// Indicates whether all prefabs have been initialized.
        /// </summary>
        public bool AllPrefabsInitialized { get; private set; } = false;

        /// <summary>
        /// Event triggered when all prefabs have been initialized.
        /// </summary>
        public event Action OnAllPrefabsInitialized;

        /// <summary>
        /// Event raised after the high-priority (immediate) prefab batch has fully restored.
        /// </summary>
        public event Action OnImmediatePrefabBatchComplete;

        /// <summary>
        /// Event raised whenever prefabs are queued for deferred instantiation.
        /// </summary>
        public event Action<IReadOnlyList<SaveablePrefabData>> OnDeferredPrefabsQueued;

                private int initializedPrefabs = 0;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            LoadPrefabRegistry();

            if (prefabRegistry == null)
            {
                Logger.Log("PrefabManager: PrefabRegistry is not assigned. Please complete the Settings Wizard via Tools > Crystal Save > Settings Wizard, or import demo settings via Tools > Crystal Save > Settings > Install Demo Settings.", LogCategory.PrefabManager, LogLevel.Error);
                enabled = false;
                return;
            }

            // Ensure prefabs that start inactive are still tracked so they can be
            // saved and restored correctly.
            RegisterExistingInactivePrefabs();

            Logger.Log($"PrefabManager: Initialized instance '{gameObject.name}'.", LogCategory.PrefabManager, LogLevel.Off);
        }
        
        private void OnEnable()
        {
            SaveablePrefab.OnPrefabInstantiated += OnPrefabInitialized;
            SaveablePrefab.OnPrefabInstantiated += RegisterPrefab;
            SaveablePrefab.OnPrefabDestroyed += UnregisterPrefab;
        }

        private void OnDisable()
        {
            SaveablePrefab.OnPrefabInstantiated -= OnPrefabInitialized;
            SaveablePrefab.OnPrefabInstantiated -= RegisterPrefab;
            SaveablePrefab.OnPrefabDestroyed -= UnregisterPrefab;
        }

        private void OnDestroy()
        {
            // Ensure we unsubscribe from static events in case OnDisable wasn't called
            SaveablePrefab.OnPrefabInstantiated -= OnPrefabInitialized;
            SaveablePrefab.OnPrefabInstantiated -= RegisterPrefab;
            SaveablePrefab.OnPrefabDestroyed -= UnregisterPrefab;
        }

        #endregion

        #region Public Accessor Methods

        /// <summary>
        /// Returns the list of SaveablePrefabs.
        /// </summary>
        public List<SaveablePrefab> GetSaveablePrefabs()
        {
            return new List<SaveablePrefab>(saveablePrefabs);
        }

        /// <summary>
        /// Returns the dictionary of instantiated prefabs.
        /// </summary>
        public Dictionary<string, GameObject> GetInstantiatedPrefabs()
        {
            return new Dictionary<string, GameObject>(instantiatedPrefabs);
        }

        /// <summary>
        /// Indicates whether all prefabs have been initialized.
        /// </summary>
        public bool AreAllPrefabsInitialized => AllPrefabsInitialized;

        #endregion

        #region Prefab Registry Management

                private void LoadPrefabRegistry()
                {
                        // Attempt to load PrefabRegistry via AssetProvider.
                        prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");

            if (prefabRegistry == null)
            {
                Logger.Log("PrefabManager: Failed to load PrefabRegistry. Please complete the Settings Wizard via Tools > Crystal Save > Settings Wizard, or import demo settings via Tools > Crystal Save > Settings > Install Demo Settings.", LogCategory.PrefabManager, LogLevel.Error);
            }
            else
            {
                Logger.Log($"PrefabManager: Loaded PrefabRegistry '{prefabRegistry.name}'.", LogCategory.PrefabManager, LogLevel.Off);
            }
        }

        #endregion

        /// <summary>
        /// Registers any <see cref="SaveablePrefab"/> instances that exist in the scene
        /// but start inactive, ensuring they are tracked by the PrefabManager and the
        /// SaveManager even though <c>OnEnable</c> never fires.
        /// </summary>
        private void RegisterExistingInactivePrefabs()
        {
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
            var prefabs = FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
            foreach (var prefab in prefabs)
            {
                if (prefab.gameObject.activeInHierarchy)
                    continue;

                // Ensure inactive prefabs that never ran Awake still get a UniqueID
                prefab.InitializeInstance();

                RegisterPrefab(prefab);
                prefab.RegisterForSaving();
                initializedPrefabs++;
            }

            if (initializedPrefabs >= saveablePrefabs.Count && !AllPrefabsInitialized)
            {
                AllPrefabsInitialized = true;
                OnAllPrefabsInitialized?.Invoke();
            }
        }

        #region Prefab Registration

        private void OnPrefabInitialized(SaveablePrefab prefab)
        {
            initializedPrefabs++;
            Logger.Log($"PrefabManager: Prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}' fully initialized.", LogCategory.PrefabManager, LogLevel.Info);

            if (initializedPrefabs >= saveablePrefabs.Count && !AllPrefabsInitialized)
            {
                AllPrefabsInitialized = true;
                Logger.Log("PrefabManager: All prefabs have been initialized.", LogCategory.PrefabManager, LogLevel.Info);
                OnAllPrefabsInitialized?.Invoke();
            }
        }

                public void RegisterPrefab(SaveablePrefab prefab)
                {
                        if (prefab == null)
                        {
                                Logger.Log("PrefabManager: Attempted to register a null prefab.", LogCategory.PrefabManager, LogLevel.Warning);
                                return;
                        }

                        if (string.IsNullOrEmpty(prefab.UniqueID))
                        {
                                Logger.Log($"PrefabManager: Ignoring prefab '{prefab.gameObject.name}' because it has no UniqueID (likely a TEMP helper clone).", LogCategory.PrefabManager, LogLevel.Off);
                                return;
                        }

                        bool isLoading = SaveManager.Instance != null &&
                                        SaveManager.Instance.StateMachine.CurrentState == SaveState.Loading;
                        // Guard against any stray nulls in the tracking list to avoid NullReferenceExceptions
                        bool alreadyRegistered = saveablePrefabs.Any(p => p != null && p.UniqueID == prefab.UniqueID);

                        if (!alreadyRegistered)
                        {
                                saveablePrefabs.Add(prefab);
                                string msg = isLoading
                                        ? $"PrefabManager: Registered prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}' during loading."
                                        : $"PrefabManager: Registered prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}'.";
                                Logger.Log(msg, LogCategory.PrefabManager, LogLevel.Off);
                        }
                        else
                        {
                                Logger.Log($"PrefabManager: Prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}' is already registered.",
                                           LogCategory.PrefabManager,
                                           isLoading ? LogLevel.Off : LogLevel.Info);
                        }

            if (!string.IsNullOrEmpty(prefab.UniqueID))
            {
                if (instantiatedPrefabs.TryGetValue(prefab.UniqueID, out var existing) && existing != null && existing != prefab.gameObject)
                {
                    Logger.Log($"PrefabManager: UniqueID '{prefab.UniqueID}' was mapped to '{existing.name}'. Reassigning to '{prefab.gameObject.name}'.",
                           LogCategory.PrefabManager,
                           LogLevel.Info);
                }

                instantiatedPrefabs[prefab.UniqueID] = prefab.gameObject;
            }
                }

        public void UnregisterPrefab(SaveablePrefab prefab)
        {
            /* ────────────── basic sanity checks ────────────── */
            if (prefab == null)
            {
                Logger.Log("PrefabManager: Attempted to unregister a null prefab.", LogCategory.PrefabManager, LogLevel.Warning);
                return;
            }

            /* ──────────────────────────────────────────────────
             * Ignore helper/temp prefabs:
             *  • they carry an empty UniqueID
             *  • they were never registered in the first place
             * ────────────────────────────────────────────────── */
            if (string.IsNullOrEmpty(prefab.UniqueID))
            {
                Logger.Log($"PrefabManager: Ignoring helper prefab '{prefab.gameObject.name}' without UniqueID during unregister.",
                           LogCategory.PrefabManager,
                           LogLevel.Off);
                return;
            }

            /* ────────────── normal removal path ────────────── */
            if (saveablePrefabs.Remove(prefab))
            {
                Logger.Log($"PrefabManager: Unregistered prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}'.",
                           LogCategory.PrefabManager,
                           LogLevel.Info);
            }
            else
            {
                // This can legitimately happen during clears when the list has been rebuilt.
                // Keep it quiet to avoid confusing noise in the Console.
                Logger.Log($"PrefabManager: Could not find prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}' to unregister.",
                           LogCategory.PrefabManager,
                           LogLevel.Off);
            }

            if (!string.IsNullOrEmpty(prefab.UniqueID) &&
                instantiatedPrefabs.TryGetValue(prefab.UniqueID, out var tracked) &&
                tracked == prefab.gameObject)
            {
                instantiatedPrefabs.Remove(prefab.UniqueID);
            }
        }

        public void UpdatePrefabUniqueID(SaveablePrefab prefab, string previousUniqueID, string newUniqueID)
        {
            if (prefab == null)
                return;

            if (string.IsNullOrEmpty(newUniqueID))
                return;

            if (!string.IsNullOrEmpty(previousUniqueID) &&
                !string.Equals(previousUniqueID, newUniqueID, StringComparison.Ordinal))
            {
                if (instantiatedPrefabs.TryGetValue(previousUniqueID, out var existing) &&
                    (existing == null || existing == prefab.gameObject))
                {
                    instantiatedPrefabs.Remove(previousUniqueID);
                }
            }

            if (!saveablePrefabs.Contains(prefab))
                saveablePrefabs.Add(prefab);

            if (instantiatedPrefabs.TryGetValue(newUniqueID, out var mapped) && mapped != null && mapped != prefab.gameObject)
            {
                Logger.Log($"PrefabManager: UniqueID '{newUniqueID}' was mapped to '{mapped.name}'. Overriding with '{prefab.gameObject.name}'.",
                           LogCategory.PrefabManager,
                           LogLevel.Info);
            }

            instantiatedPrefabs[newUniqueID] = prefab.gameObject;
        }
        #endregion

        #region Prefab Data Collection

        /// <summary>
        /// Collects data from all registered SaveablePrefabs.
        /// </summary>

                public List<SaveablePrefabData> CollectPrefabData()
                {
                        var prefabDataList = new List<SaveablePrefabData>();
                        int totalPrefabs = saveablePrefabs.Count;
                        Logger.Log($"[PrefabManager] CollectPrefabData: Processing {totalPrefabs} prefabs", LogCategory.PrefabManager, LogLevel.Info);

                        int processedCount = 0;
                        foreach (var prefab in saveablePrefabs)
                        {
                                if (prefab == null)
                                {
                                        processedCount++;
                                        Logger.Log($"[PrefabManager] Skipping null prefab at index {processedCount - 1} (processed {processedCount}/{totalPrefabs})",
                                                LogCategory.PrefabManager,
                                                LogLevel.Warning);
                                        continue;
                                }

                                try
                                {
                                        var data = CreatePrefabData(prefab);
                                        if (data != null)
                                        {
                                                prefabDataList.Add(data);
                                        }
                                }
                                catch (System.Exception ex)
                                {
                                        // Log but don't break the entire save operation
                                        Logger.Log($"[PrefabManager] Failed to collect data for prefab '{prefab.name}': {ex.Message}",
                                                LogCategory.PrefabManager,
                                                LogLevel.Error);
                                        UnityEngine.Debug.LogException(ex);
                                }

                                processedCount++;
                                Logger.Log($"[PrefabManager] Processed {processedCount}/{totalPrefabs} prefabs",
                                        LogCategory.PrefabManager,
                                        LogLevel.Info);
                        }

                        Logger.Log($"[PrefabManager] CollectPrefabData: Collected {prefabDataList.Count} prefab data entries", LogCategory.PrefabManager, LogLevel.Info);

                        foreach (var kvp in pendingPrefabs)
                                prefabDataList.AddRange(kvp.Value);

                        foreach (var kvp in deferredPrefabs)
                                prefabDataList.AddRange(kvp.Value);

                        return prefabDataList;
                }

                public SaveablePrefabData BuildPrefabData(SaveablePrefab prefab)
                {
                        return CreatePrefabData(prefab);
                }

        #region Deferred Prefab Scheduling

        /// <summary>
        /// Splits the provided prefab data into immediate and deferred batches, queues the deferred entries,
        /// and returns the high-priority list sorted by <see cref="SaveablePrefabData.LoadPriority"/>.
        /// </summary>
        public List<SaveablePrefabData> PrepareImmediatePrefabs(List<SaveablePrefabData> prefabDataList)
        {
            PartitionPrefabData(prefabDataList, out var immediate, out var deferred);
            if (deferred.Count > 0)
                QueueDeferredPrefabs(deferred);
            return immediate;
        }

        /// <summary>
        /// Returns <c>true</c> if any deferred prefabs remain queued.
        /// </summary>
        public bool HasDeferredPrefabs => deferredPrefabs.Any(kvp => kvp.Value.Count > 0);

        /// <summary>
        /// Enumerates scene keys that contain deferred prefabs waiting to be processed.
        /// </summary>
        public IEnumerable<string> GetDeferredSceneKeys()
        {
            foreach (var kvp in deferredPrefabs)
            {
                if (kvp.Value.Count == 0)
                    continue;

                yield return kvp.Key == GlobalDeferredSceneKey ? string.Empty : kvp.Key;
            }
        }

        /// <summary>
        /// Returns a snapshot of the deferred prefabs queued for a specific scene without dequeuing them.
        /// </summary>
        public IReadOnlyList<SaveablePrefabData> PeekDeferredPrefabsForScene(string sceneName)
        {
            string key = ResolveDeferredSceneKey(sceneName);
            if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                return Array.Empty<SaveablePrefabData>();

            return queue.ToList().AsReadOnly();
        }

        /// <summary>
        /// Processes all deferred prefabs regardless of scene using the stored queue.
        /// </summary>
        public Coroutine ProcessDeferredPrefabs(List<string> destroyedGameObjectIDs = null)
        {
            var entries = DequeueAllDeferredPrefabs();
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? SaveManager.Instance?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs for a specific scene.
        /// </summary>
        public Coroutine ProcessDeferredPrefabsForScene(string sceneName, List<string> destroyedGameObjectIDs = null)
        {
            var entries = DequeueDeferredPrefabs(sceneName);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? SaveManager.Instance?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs for a specific prefab asset across all scenes.
        /// </summary>
        public Coroutine ProcessDeferredPrefabsForAsset(string prefabAssetID, List<string> destroyedGameObjectIDs = null)
        {
            if (string.IsNullOrEmpty(prefabAssetID))
                return null;

            var entries = ExtractDeferredPrefabsForAsset(prefabAssetID);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? SaveManager.Instance?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs that match a specific instance ID across all scenes.
        /// </summary>
        public Coroutine ProcessDeferredPrefabByUniqueID(string instanceID, List<string> destroyedGameObjectIDs = null)
        {
            if (string.IsNullOrEmpty(instanceID))
                return null;

            var entries = ExtractDeferredPrefabsByInstanceID(instanceID);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? SaveManager.Instance?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs for the supplied instance IDs across all scenes.
        /// </summary>
        /// <param name="instanceIDs">Collection of deferred prefab instance IDs to process.</param>
        /// <param name="destroyedGameObjectIDs">Optional list of destroyed object IDs to pass through to instantiation.</param>
        /// <returns>The coroutine responsible for instantiating the requested prefabs, or <c>null</c> if no entries were queued.</returns>
        public Coroutine ProcessDeferredPrefabsByInstanceIDs(IReadOnlyCollection<string> instanceIDs, List<string> destroyedGameObjectIDs = null)
        {
            if (instanceIDs == null || instanceIDs.Count == 0)
                return null;

            var entries = ExtractDeferredPrefabsByInstanceIDs(instanceIDs);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? SaveManager.Instance?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        private void PartitionPrefabData(IEnumerable<SaveablePrefabData> source, out List<SaveablePrefabData> immediate, out List<SaveablePrefabData> deferred)
        {
            immediate = new List<SaveablePrefabData>();
            deferred = new List<SaveablePrefabData>();

            if (source == null)
                return;

            foreach (var data in source)
            {
                if (data == null)
                    continue;

                if (data.DeferLowPriorityUntilRequested)
                    deferred.Add(data);
                else
                    immediate.Add(data);
            }

            immediate.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));
            deferred.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));
        }

        private void QueueDeferredPrefabs(IEnumerable<SaveablePrefabData> deferredEntries)
        {
            if (deferredEntries == null)
                return;

            var grouped = deferredEntries
                .Where(entry => entry != null)
                .GroupBy(GetDeferredSceneKey);

            List<SaveablePrefabData> snapshot = null;

            foreach (var group in grouped)
            {
                if (!deferredPrefabs.TryGetValue(group.Key, out var queue))
                {
                    queue = new Queue<SaveablePrefabData>();
                    deferredPrefabs[group.Key] = queue;
                }

                foreach (var entry in group.OrderByDescending(e => e.LoadPriority))
                {
                    snapshot ??= new List<SaveablePrefabData>();
                    snapshot.Add(entry);
                    queue.Enqueue(entry);
                }
            }

            if (snapshot != null && snapshot.Count > 0)
                deferredPrefabsStagedSinceLastClear = true;

            if (snapshot != null && snapshot.Count > 0)
                OnDeferredPrefabsQueued?.Invoke(snapshot.AsReadOnly());
        }

        private static string GetDeferredSceneKey(SaveablePrefabData data)
        {
            if (data == null)
                return GlobalDeferredSceneKey;

            return string.IsNullOrEmpty(data.HomeScene) ? GlobalDeferredSceneKey : data.HomeScene;
        }

        private static string ResolveDeferredSceneKey(string sceneName)
        {
            return string.IsNullOrEmpty(sceneName) ? GlobalDeferredSceneKey : sceneName;
        }

        private List<SaveablePrefabData> DequeueDeferredPrefabs(string sceneName)
        {
            string key = ResolveDeferredSceneKey(sceneName);
            if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                return new List<SaveablePrefabData>();

            var list = queue.ToList();
            deferredPrefabs.Remove(key);
            list.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));
            return list;
        }

        private List<SaveablePrefabData> DequeueAllDeferredPrefabs()
        {
            var list = new List<SaveablePrefabData>();

            foreach (var key in deferredPrefabs.Keys.ToList())
            {
                list.AddRange(DequeueDeferredPrefabs(key == GlobalDeferredSceneKey ? string.Empty : key));
            }

            if (list.Count > 0)
                list.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));

            return list;
        }

        private List<SaveablePrefabData> ExtractDeferredPrefabsForAsset(string prefabAssetID)
        {
            var matches = new List<SaveablePrefabData>();

            if (string.IsNullOrEmpty(prefabAssetID))
                return matches;

            foreach (var key in deferredPrefabs.Keys.ToList())
            {
                if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                    continue;

                var remaining = new Queue<SaveablePrefabData>();
                int count = queue.Count;

                for (int i = 0; i < count; i++)
                {
                    var entry = queue.Dequeue();

                    if (entry != null && string.Equals(entry.PrefabID, prefabAssetID, StringComparison.Ordinal))
                        matches.Add(entry);
                    else
                        remaining.Enqueue(entry);
                }

                if (remaining.Count > 0)
                    deferredPrefabs[key] = remaining;
                else
                    deferredPrefabs.Remove(key);
            }

            if (matches.Count > 0)
                matches.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));

            return matches;
        }

        private List<SaveablePrefabData> ExtractDeferredPrefabsByInstanceID(string instanceID)
        {
            if (string.IsNullOrEmpty(instanceID))
                return new List<SaveablePrefabData>();

            return ExtractDeferredPrefabsByInstanceIDs(new[] { instanceID });
        }

        private List<SaveablePrefabData> ExtractDeferredPrefabsByInstanceIDs(IReadOnlyCollection<string> instanceIDs)
        {
            var matches = new List<SaveablePrefabData>();

            if (instanceIDs == null || instanceIDs.Count == 0)
                return matches;

            var lookup = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in instanceIDs)
            {
                if (!string.IsNullOrEmpty(id))
                    lookup.Add(id);
            }

            if (lookup.Count == 0)
                return matches;

            foreach (var key in deferredPrefabs.Keys.ToList())
            {
                if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                    continue;

                var extracted = ExtractMatchingDeferredPrefabs(queue, lookup, out var remaining);
                if (extracted.Count > 0)
                    matches.AddRange(extracted);

                if (remaining.Count > 0)
                    deferredPrefabs[key] = remaining;
                else
                    deferredPrefabs.Remove(key);
            }

            if (matches.Count > 0)
                matches.Sort((a, b) => b.LoadPriority.CompareTo(a.LoadPriority));

            return matches;
        }

        private static List<SaveablePrefabData> ExtractMatchingDeferredPrefabs(Queue<SaveablePrefabData> source, ISet<string> instanceIDLookup, out Queue<SaveablePrefabData> remaining)
        {
            remaining = new Queue<SaveablePrefabData>();
            var matches = new List<SaveablePrefabData>();

            if (source == null || source.Count == 0 || instanceIDLookup == null || instanceIDLookup.Count == 0)
                return matches;

            int count = source.Count;
            for (int i = 0; i < count; i++)
            {
                var entry = source.Dequeue();
                if (entry != null && instanceIDLookup.Contains(entry.InstanceID))
                    matches.Add(entry);
                else
                    remaining.Enqueue(entry);
            }

            return matches;
        }

        #endregion

                private SaveablePrefabData CreatePrefabData(SaveablePrefab prefab)
                {
                        if (prefab == null || !prefab.RegisterWithSaveSystem)
                                return null;

                        var data = prefab.TryBuildSaveData();
                        if (data == null)
                                return null;

                        if (prefab.TryGetLastSnapshot(out var snapshot))
                        {
                                if (data.HasParentOverride || prefab.IsAddedAtRuntime)
                                {
                                        data.ParentID = snapshot.ParentID;
                                        data.IsParentSceneObject = snapshot.IsParentSceneObject;
                                }
                                else if (string.IsNullOrEmpty(data.ParentID))
                                {
                                        data.ParentID = snapshot.ParentID;
                                        data.IsParentSceneObject = snapshot.IsParentSceneObject;
                                }

                                if (data.HasTransformOverride || prefab.IsAddedAtRuntime)
                                {
                                        if (snapshot.HasParent)
                                        {
                                                data.Position = snapshot.LocalPosition;
                                                data.Rotation = snapshot.LocalRotation;
                                        }
                                        else
                                        {
                                                data.Position = snapshot.WorldPosition;
                                                data.Rotation = snapshot.WorldRotation;
                                        }

                                        data.Scale = snapshot.LocalScale;
                                }
                                else if (data.Scale == default)
                                {
                                        data.Scale = snapshot.LocalScale;
                                }

                                // Parent fingerprinting for robust reattachment
                                if (snapshot.HasParent)
                                {
                                    // If parent is a SaveablePrefab, capture its assetID and stable path
                                    var parentSp = prefab.transform.parent ? prefab.transform.parent.GetComponentInParent<SaveablePrefab>() : null;
                                    if (parentSp != null)
                                    {
                                        data.ParentPrefabAssetID = parentSp.PrefabAssetID;
                                        data.ParentStableKey = SaveablePrefab.BuildStableHierarchyKey(parentSp);
                                    }
                                }
            }

            // When remembering Home Scene, always carry the current transform in the save-data
            // so that snapshot-based scene switches restore the latest runtime position/rotation
            // instead of falling back to design-time placement.
            if (prefab.RememberHomeScene && prefab.TryGetLastSnapshot(out var hsSnapshot))
            {
                data.HasTransformOverride = true;
                if (hsSnapshot.HasParent)
                {
                    data.Position = hsSnapshot.LocalPosition;
                    data.Rotation = hsSnapshot.LocalRotation;
                }
                else
                {
                    data.Position = hsSnapshot.WorldPosition;
                    data.Rotation = hsSnapshot.WorldRotation;
                }
                data.Scale = hsSnapshot.LocalScale;

                // Also carry parent fingerprint when remembering home scene
                var parentSp = prefab.transform.parent ? prefab.transform.parent.GetComponentInParent<SaveablePrefab>() : null;
                if (parentSp != null)
                {
                    data.ParentPrefabAssetID = parentSp.PrefabAssetID;
                    data.ParentStableKey = SaveablePrefab.BuildStableHierarchyKey(parentSp);
                }
            }

            if (prefab.RememberHomeScene)
                        {
                                if (prefab.HomeSceneCaptureMode == SaveablePrefab.HomeSceneMode.LastSnapshotScene &&
                                    prefab.gameObject.scene.name != "DontDestroyOnLoad")
                                {
                                        prefab.SetHomeScene(prefab.gameObject.scene.name);
                                }

                                data.HomeScene = prefab.HomeScene;
                
                        }
                        else
                        {
                                data.HomeScene = null;
                        }

                        data.DisablePooling = prefab.DisablePooling;
                        data.LoadPriority = prefab.LoadPriority;
                        data.DeferLowPriorityUntilRequested = prefab.DeferLowPriorityUntilRequested;

                        if (data.HasVisibilityData)
                        {
                                data.VisibilitySettingsData ??= prefab.GetVisibilitySettings();
                                if (data.VisibilitySettingsData == null || data.VisibilitySettingsData.Length == 0)
                                {
                                        data.HasVisibilityData = false;
                                }
                        }

                        if (data.RuntimeModificationData == null)
                        {
                                var mods = prefab.CaptureRuntimeModifications();
                                if (mods != null && mods.Length > 0)
                                        data.RuntimeModificationData = mods;
                        }

                        var rb = prefab.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                                data.HasRigidbody = true;
#if UNITY_6000_0_OR_NEWER
                                data.RigidbodyVelocity        = rb.linearVelocity;
                                data.RigidbodyAngularVelocity = rb.angularVelocity;
                                data.RigidbodyUseGravity      = rb.useGravity;
                                data.RigidbodyIsKinematic     = rb.isKinematic;
                                data.RigidbodyDrag            = rb.linearDamping;
                                data.RigidbodyAngularDrag     = rb.angularDamping;
#else
                                data.RigidbodyVelocity        = rb.velocity;
                                data.RigidbodyAngularVelocity = rb.angularVelocity;
                                data.RigidbodyUseGravity      = rb.useGravity;
                                data.RigidbodyIsKinematic     = rb.isKinematic;
                                data.RigidbodyDrag            = rb.drag;
                                data.RigidbodyAngularDrag     = rb.angularDrag;
#endif
                        }

                        var anim = prefab.GetComponent<Animator>();
                        if (anim != null)
                        {
                                data.HasAnimator = true;
                                AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                                data.AnimatorStateHash     = info.shortNameHash;
                                data.AnimatorNormalizedTime = info.normalizedTime;
                        }

                        if (prefab.TrackColliderSettings)
                                data.Colliders = SaveColliderSettings(prefab);

                        return data;
                }
        #endregion

        #region Prefab Instantiation

        /// <summary>
        /// Instantiates prefabs based on the provided SaveablePrefabData.
        /// </summary>
                public IEnumerator InstantiatePrefabsCoroutine(
                        List<SaveablePrefabData> prefabDataList,
                        List<string> destroyedGameObjectIDs,
                        bool clearExistingPrefabs = true,
                        bool handleDeferral = true
                )
                {
                        if (prefabDataList == null)
                        {
                                if (handleDeferral)
                                        OnImmediatePrefabBatchComplete?.Invoke();
                                yield break;
                        }

                        destroyedGameObjectIDs ??= new List<string>();

                        // De-duplicate incoming entries by InstanceID. It's possible for the
                        // snapshot/populate path to include duplicates (e.g., pending/deferred queues
                        // plus current scene records). Prefer entries that explicitly request reuse of
                        // scene instances, then those with transform overrides, then higher load priority.
                        // This prevents processing the same ID twice and avoids a second pass falling
                        // back to instantiation after we have already claimed a scene instance.
                        try
                        {
                            var byId = new Dictionary<string, SaveablePrefabData>(StringComparer.Ordinal);
                            foreach (var d in prefabDataList)
                            {
                                if (d == null || string.IsNullOrEmpty(d.InstanceID))
                                    continue;

                                if (!byId.TryGetValue(d.InstanceID, out var cur))
                                {
                                    byId[d.InstanceID] = d;
                                    continue;
                                }

                                // Scoring: reuse > transform override > load priority
                                int Score(SaveablePrefabData x) => (x.ReuseSceneInstanceOnLoad ? 1000000 : 0)
                                                                 + (x.HasTransformOverride ? 1000 : 0)
                                                                 + x.LoadPriority;

                                if (Score(d) > Score(cur))
                                    byId[d.InstanceID] = d;
                            }

                            if (byId.Count > 0)
                            {
                                // Preserve original ordering roughly by priority while removing dupes
                                prefabDataList = prefabDataList
                                    .Where(d => d != null && !string.IsNullOrEmpty(d.InstanceID))
                                    .Select(d => byId[d.InstanceID])
                                    .Distinct()
                                    .OrderByDescending(d => d.LoadPriority)
                                    .ToList();
                            }
                        }
                        catch { /* best-effort */ }

                        // Drop any entries that are already mapped to a live instance only for
                        // non-clearing passes. During full scene loads (clearExistingPrefabs=true),
                        // scene-baked prefabs can already exist in instantiatedPrefabs before we
                        // apply saved state; filtering them out here prevents runtime state (such as
                        // activeSelf) from being restored.
                        try
                        {
                            if (!clearExistingPrefabs && prefabDataList.Count > 0 && instantiatedPrefabs.Count > 0)
                            {
                                prefabDataList = prefabDataList
                                    .Where(d => d != null && (!instantiatedPrefabs.TryGetValue(d.InstanceID, out var go) || go == null))
                                    .ToList();
                            }
                        }
                        catch { /* best-effort */ }

                        // Decide which entries participate in mapping/preservation:
                        // - During normal load (handleDeferral=true), we ONLY map immediate entries.
                        // - During deferred processing (handleDeferral=false), we map the provided list
                        //   (but asset-based reuse may be disabled by settings below).
                        List<SaveablePrefabData> mappingList;
                        List<SaveablePrefabData> deferredList;
                        if (handleDeferral)
                        {
                            PartitionPrefabData(prefabDataList, out var immediateOnly, out var deferredOnly);
                            // Maintain priority ordering for mapping
                            mappingList = immediateOnly.OrderByDescending(d => d.LoadPriority).ToList();
                            deferredList = deferredOnly;
                            
                        }
                        else
                        {
                            // In deferred passes we take the provided list as-is
                            mappingList = prefabDataList.Where(d => d != null).OrderByDescending(d => d.LoadPriority).ToList();
                            deferredList = new List<SaveablePrefabData>();
                            
                        }

                        var reuseInstanceIDs = mappingList
                            .Where(data => data != null && data.ReuseSceneInstanceOnLoad)
                            .Select(data => data.InstanceID)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .Distinct(StringComparer.Ordinal)
                            .ToList();

            // 1) Prefer reuse by explicit InstanceID list (from save-data flags)
            if (reuseInstanceIDs.Count > 0)
            {
                var reuseLookup = new HashSet<string>(reuseInstanceIDs, StringComparer.Ordinal);
                foreach (var tracked in saveablePrefabs)
                {
                    if (tracked == null)
                        continue;

                    string trackedId = tracked.UniqueID;
                    if (!string.IsNullOrEmpty(trackedId))
                    {
                        reuseLookup.Remove(trackedId);
                        // Ensure instantiatedPrefabs has a mapping so later phases can find it immediately
                        if (!instantiatedPrefabs.ContainsKey(trackedId))
                            instantiatedPrefabs[trackedId] = tracked.gameObject;
                        
                    }
                }

                if (reuseLookup.Count > 0)
                {
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                    var scenePrefabs = FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
                    foreach (var prefab in scenePrefabs)
                    {
                        if (prefab == null)
                            continue;

                        string uniqueID = prefab.UniqueID;
                        if (string.IsNullOrEmpty(uniqueID) || !reuseLookup.Contains(uniqueID))
                            continue;

                        if (!saveablePrefabs.Contains(prefab))
                            saveablePrefabs.Add(prefab);

                        instantiatedPrefabs[uniqueID] = prefab.gameObject;
                        Logger.Log(
                            $"PrefabManager.InstantiatePrefabsCoroutine: Captured untracked reuse candidate '{prefab.gameObject.name}' with UniqueID '{uniqueID}'.",
                            LogCategory.PrefabManager,
                            LogLevel.Info);

                        reuseLookup.Remove(uniqueID);
                        if (reuseLookup.Count == 0)
                            break;
                    }
                }
            }

            // 2) Asset-based reuse mapping for scene-placed prefabs.
            // This ensures design-time instances without UIDs are claimed and preserved
            // even when the save-data didn't request explicit reuse by InstanceID.
            // Note: During deferred processing we can optionally disable this to avoid
            // reusing scene-baked instances which could lead to under-spawning.
            try
            {
                bool allowAssetReuse = true;
                try
                {
                    // If handleDeferral is false, we are processing deferred entries now.
                    // Respect the SaveSettings toggle to optionally disable asset-based reuse in that case.
                    if (!handleDeferral)
                    {
                        var ss = SaveManager.Instance?.SaveSettings;
                        bool enableForDeferred = ss?.enableAssetBasedReuseDuringDeferred ?? false;
                        allowAssetReuse = enableForDeferred;
                        
                    }
                }
                catch { /* best-effort */ }

                if (!allowAssetReuse)
                {
                    
                    goto SKIP_ASSET_REUSE; // jump to deferral partitioning
                }

#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                var scenePrefabsAll = FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
                var sceneByAsset = scenePrefabsAll
                    .Where(sp => sp != null)
                    .GroupBy(sp => sp.PrefabAssetID)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

                // Group mapping entries by PrefabID (exclude deferred when handleDeferral=true)
                var allGroups = mappingList
                    .Where(d => d != null && !string.IsNullOrEmpty(d.PrefabID))
                    .GroupBy(d => d.PrefabID)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

                foreach (var kvp in allGroups)
                {
                    string prefabAssetId = kvp.Key;
                    var dataList = kvp.Value;

                    

                    if (!sceneByAsset.TryGetValue(prefabAssetId, out var candidates) || candidates == null || candidates.Count == 0)
                        continue; // no scene candidate for this asset

                    // Track which specific scene objects have been claimed for this asset group
                    var claimed = new HashSet<int>();

                    // Resolve candidate for each data entry individually with simple heuristics
                    foreach (var data in dataList)
                    {
                        if (string.IsNullOrEmpty(data.InstanceID))
                            continue; // can't map without a proper instance id

                        // Note: we intentionally do NOT early-return when an existing mapping
                        // is present in instantiatedPrefabs. Instead, we re-evaluate current
                        // scene candidates each load to ensure the mapping stays anchored to
                        // the intended scene object (and to recover from stale mappings).

                        // Local iterator over viable (not yet claimed) candidates
                        // IMPORTANT:
                        // - Only consider scene-placed candidates for asset-based reuse to avoid
                        //   collapsing multiple incoming entries onto a single already-instantiated
                        //   runtime clone during deferred processing.
                        // - However, if a runtime instance already exactly matches the incoming
                        //   InstanceID, allow it (exact UniqueID match) so we can safely reuse it.
                        IEnumerable<SaveablePrefab> Viable()
                            => candidates.Where(c =>
                                c != null &&
                                !claimed.Contains(UnityObjectHelper.GetUniqueId(c)) &&
                                (
                                    // Prefer scene-baked objects
                                    !c.IsAddedAtRuntime
                                    // Or allow an exact-ID match regardless of origin
                                    || string.Equals(c.UniqueID, data.InstanceID, StringComparison.Ordinal)
                                )
                            );
                        var viable = Viable().ToList();
                        if (viable.Count == 0)
                        {
                            
                            continue;
                        }

                        SaveablePrefab chosen = null;
                        string chosenReason = string.Empty;

                        // Emit a candidate list with distances and UID info
                        try
                        {
                            var candInfo = string.Join(", ", viable.Select(c =>
                            {
                                var pos = c.transform.position;
                                float d = Vector3.SqrMagnitude(pos - data.Position);
                                string uid = string.IsNullOrEmpty(c.UniqueID) ? "<empty>" : c.UniqueID;
                                string added = c.IsAddedAtRuntime ? "runtime" : "scene";
                                string reuse = c.ReuseSceneInstanceOnLoad ? "+reuse" : string.Empty;
                                string key = GetStableHierarchyKey(c);
                                int sib = c.transform.GetSiblingIndex();
                                return $"{c.name}(uid={uid}, pos={pos}, d2={(double)d:F4}, sib={sib}, key={key}, {added}{reuse})";
                            }));
                            
                        }
                        catch { /* logging-only */ }

                        // If any candidate already carries this exact InstanceID, prefer it.
                        var exact = viable.FirstOrDefault(c => string.Equals(c.UniqueID, data.InstanceID, StringComparison.Ordinal));
                        if (exact != null)
                        {
                            chosen = exact;
                            chosenReason = "byUniqueID";
                        }
                        else if (viable.Count == 1)
                        {
                            chosen = viable[0];
                            chosenReason = "singleCandidate";
                        }
                        else
                        {
                            // Prefer candidates with empty UniqueID. If ALL viable candidates are empty, use a
                            // deterministic, stable ordering (hierarchy path) to avoid cross-session swaps.
                            // Only fall back to distance when at least one viable has a non-empty UID.
                            var empties = viable.Where(c => string.IsNullOrEmpty(c.UniqueID)).ToList();
                            var nonEmpty = viable.Where(c => !string.IsNullOrEmpty(c.UniqueID)).ToList();

                            if (empties.Count > 0 && nonEmpty.Count == 0)
                            {
                                // Stable order: hierarchy path -> sibling index -> name -> instanceID
                                chosen = empties
                                    .OrderBy(c => GetStableHierarchyKey(c), StringComparer.Ordinal)
                                    .ThenBy(c => c.transform.GetSiblingIndex())
                                    .ThenBy(c => c.name, StringComparer.Ordinal)
                                    .ThenBy(c => UnityObjectHelper.GetUniqueId(c))
                                    .First();
                                chosenReason = empties.Count == 1 ? "emptyUID" : "stableOrder";
                            }
                            else
                            {
                                var pool = (empties.Count > 0) ? empties : viable;
                                // Distance-first for ties, then deterministic tiebreakers
                                chosen = pool
                                    .OrderBy(c => Vector3.SqrMagnitude(c.transform.position - data.Position))
                                    .ThenBy(c => c.transform.GetSiblingIndex())
                                    .ThenBy(c => c.name, StringComparer.Ordinal)
                                    .ThenBy(c => UnityObjectHelper.GetUniqueId(c))
                                    .First();

                                if (empties.Count > 0)
                                    chosenReason = empties.Count == 1 ? "emptyUID" : "emptyUIDNearest";
                                else
                                    chosenReason = "nearestByPosition";
                            }
                        }

                        if (chosen == null)
                        {
                            
                            continue;
                        }

                        // Mark chosen candidate as claimed so we don't reuse the same scene object for multiple entries
                        int chosenId = UnityObjectHelper.GetUniqueId(chosen);
                        claimed.Add(chosenId);
                        candidates.Remove(chosen); // also remove to shrink subsequent searches

                        // Ensure it's tracked by PrefabManager
                        if (!saveablePrefabs.Contains(chosen))
                            saveablePrefabs.Add(chosen);

                        // Assign the saved InstanceID to the scene instance so subsequent lookup finds it
                        if (string.IsNullOrEmpty(chosen.UniqueID) || !string.Equals(chosen.UniqueID, data.InstanceID, StringComparison.Ordinal))
                        {
                            chosen.SetUniqueID(data.InstanceID);
                            string key = GetStableHierarchyKey(chosen);
                            
                        }

                        // Mirror important flags from save-data
                        chosen.SetHomeScene(data.HomeScene);
                        chosen.LoadPriority = data.LoadPriority;
                        chosen.DeferLowPriorityUntilRequested = data.DeferLowPriorityUntilRequested;
                        chosen.DisablePooling = data.DisablePooling;
                        chosen.ReuseSceneInstanceOnLoad = true; // persist behavior for future saves
                        // Ensure the data prefers reuse as well to align with the mapped scene instance
                        data.ReuseSceneInstanceOnLoad = true;

                        // Ensure registration if needed (so future saves/loads see it tracked)
                        if (chosen.RegisterWithSaveSystem)
                        {
                            try { chosen.RegisterForSaving(); }
                            catch { /* ignore */ }
                        }

                        // Seed the lookup so InstantiatePrefabInternal hits the reuse path
                        instantiatedPrefabs[data.InstanceID] = chosen.gameObject;

                        // Also include in the preservation list so ClearSaveablePrefabs keeps it
                        if (!reuseInstanceIDs.Contains(data.InstanceID))
                            reuseInstanceIDs.Add(data.InstanceID);

                        
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }

SKIP_ASSET_REUSE:

            List<SaveablePrefabData> immediatePrefabs;
            if (handleDeferral)
            {
                immediatePrefabs = mappingList;
                if (deferredList.Count > 0)
                    QueueDeferredPrefabs(deferredList);
            }
            else
            {
                immediatePrefabs = mappingList;
            }

            // Ensure the preserve list includes IDs for any incoming entries (in the mapping scope)
            // whose PrefabID exists in the current scene. This lets ClearSaveablePrefabs repopulate
            // scene-placed prefabs with empty UniqueID for the IMMEDIATE batch only.
            try
            {
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                var scenePrefabsForPreserve = FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
                var scenePrefabIds = new HashSet<string>(
                    scenePrefabsForPreserve.Where(sp => sp != null && !string.IsNullOrEmpty(sp.PrefabAssetID))
                                           .Select(sp => sp.PrefabAssetID),
                    StringComparer.Ordinal);

                if (scenePrefabIds.Count > 0)
                {
                    foreach (var entry in mappingList)
                    {
                        if (entry == null) continue;
                        if (string.IsNullOrEmpty(entry.InstanceID) || string.IsNullOrEmpty(entry.PrefabID)) continue;
                        if (!scenePrefabIds.Contains(entry.PrefabID)) continue;
                        if (!reuseInstanceIDs.Contains(entry.InstanceID))
                            reuseInstanceIDs.Add(entry.InstanceID);
                    }
                }
            }
            catch { /* best-effort */ }

            if (clearExistingPrefabs)
            {
                bool preserveDeferredQueue = !handleDeferral && deferredPrefabsStagedSinceLastClear;

                IReadOnlyDictionary<string, string> preserveMap = null;
                if (mappingList.Count > 0)
                {
                    var tmp = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var entry in mappingList)
                    {
                        if (entry == null) continue;
                        if (string.IsNullOrEmpty(entry.InstanceID)) continue;
                        tmp[entry.InstanceID] = entry.PrefabID ?? string.Empty;
                    }

                    if (tmp.Count > 0)
                        preserveMap = tmp;
                }

                ClearSaveablePrefabs(preserveDeferredQueue, reuseInstanceIDs, preserveMap);
            }
            else
            {
                // Do NOT clear the reuse mappings; we rely on them later in this coroutine.
                // Just prune any entries that point to destroyed/null instances.
                try
                {
                    var toRemove = instantiatedPrefabs.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
                    foreach (var k in toRemove)
                        instantiatedPrefabs.Remove(k);
                }
                catch { /* best-effort */ }
            }

            if (immediatePrefabs.Count == 0)
            {
                if (handleDeferral)
                    OnImmediatePrefabBatchComplete?.Invoke();
                yield break;
            }

                        int prefabsProcessed = 0;
            int batchSize = SaveManager.Instance?.SaveSettings?.prefabInstantiationBatchSize ?? 0;
            bool syncTransformsAfterLoad = SaveManager.Instance?.SaveSettings?.syncTransformsAfterPrefabLoad ?? false;
            bool groupByScene = SaveManager.Instance?.SaveSettings?.groupInstantiationByScene ?? false;

            // Track which prefabs were actually instantiated now (vs. reused)
            var justInstantiatedIds = new HashSet<string>(StringComparer.Ordinal);
            var processedInstanceIds = new HashSet<string>(StringComparer.Ordinal);

            // First pass: Instantiate prefabs without finalizing transforms.
            bool TryProcessDeferredComponents(SaveablePrefabData data, GameObject target, string context)
            {
                if (data == null)
                    return false;

                string instanceId = data.InstanceID;
                if (string.IsNullOrEmpty(instanceId))
                {
                    Logger.Log($"{context}: Prefab data is missing an InstanceID; skipping deferred component processing.", LogCategory.PrefabManager, LogLevel.Warning);
                    return false;
                }

                if (target == null)
                {
                    Logger.Log($"{context}: Target instance for '{instanceId}' is not available; deferred components remain queued.", LogCategory.PrefabManager, LogLevel.Info);
                    return false;
                }

                var componentManager = ComponentManager.Instance;
                if (componentManager == null)
                {
                    Logger.Log($"{context}: ComponentManager is not available; deferred components will be retried for '{instanceId}'.", LogCategory.PrefabManager, LogLevel.Info);
                    return false;
                }

                componentManager.ProcessDeferredComponentsForGameObject(instanceId);
                Logger.Log($"{context}: Requested deferred component processing for '{instanceId}'.", LogCategory.PrefabManager, LogLevel.Info);
                return true;
            }

            IEnumerator InstantiatePrefabInternal(SaveablePrefabData prefabData)
            {
                if (prefabData == null)
                {
                    yield break;
                }

                if (prefabData.ReuseSceneInstanceOnLoad)
                {
                    string homeSceneLabel = string.IsNullOrEmpty(prefabData.HomeScene) ? "<null>" : prefabData.HomeScene;
                    string homeSceneState;
                    if (string.IsNullOrEmpty(prefabData.HomeScene))
                    {
                        homeSceneState = "no-home-scene";
                    }
                    else
                    {
                        var sceneLookup = SceneManager.GetSceneByName(prefabData.HomeScene);
                        if (!sceneLookup.IsValid())
                            homeSceneState = "scene-not-found";
                        else
                            homeSceneState = sceneLookup.isLoaded ? "loaded" : "unloaded";
                    }

                    // Begin processing reusable prefab instance
                }

                if (!string.IsNullOrEmpty(prefabData.InstanceID))
                {
                    if (!processedInstanceIds.Add(prefabData.InstanceID))
                    {
                        Logger.Log($"[CrystalSave][Instantiate] Skipping duplicate entry for InstanceID '{prefabData.InstanceID}' (PrefabID '{prefabData.PrefabID ?? "<null>"}')", LogCategory.PrefabManager, LogLevel.Info);
                        yield break;
                    }
                }

                if (destroyedGameObjectIDs.Contains(prefabData.InstanceID))
                {
                    if (SaveManager.Instance != null)
                    {
                        var identifierInfo = SaveManager.Instance.ResolveDestroyedIdentifierInfo(prefabData.InstanceID);
                        string incomingLogId = string.IsNullOrEmpty(identifierInfo.IncomingId)
                            ? "<null or empty>"
                            : identifierInfo.IncomingId;
                        string canonicalLogId = string.IsNullOrEmpty(identifierInfo.CanonicalId)
                            ? "<null or empty>"
                            : identifierInfo.CanonicalId;

                        Logger.Log(
                            $"PrefabManager.InstantiatePrefabsCoroutine: skipping instantiation for incoming '{incomingLogId}' (canonical '{canonicalLogId}') because it remains marked destroyed.",
                            LogCategory.PrefabManager,
                            LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log(
                            $"PrefabManager.InstantiatePrefabsCoroutine: skipping instantiation for prefab ID '{prefabData.InstanceID}' because it remains marked destroyed.",
                            LogCategory.PrefabManager,
                            LogLevel.Info);
                    }

                    yield break;
                }

                GameObject existing = null;
                bool hadTrackedMapping = false;
                bool removedStaleMapping = false;
                if (!string.IsNullOrEmpty(prefabData.InstanceID))
                {
                    if (instantiatedPrefabs.TryGetValue(prefabData.InstanceID, out var tracked) && tracked != null)
                    {
                        existing = tracked;
                        hadTrackedMapping = true;
                    }
                    else if (instantiatedPrefabs.ContainsKey(prefabData.InstanceID))
                    {
                        hadTrackedMapping = true;
                        instantiatedPrefabs.Remove(prefabData.InstanceID);
                        removedStaleMapping = true;
                        Logger.Log($"PrefabManager.InstantiatePrefabInternal: Removed stale mapping for InstanceID '{prefabData.InstanceID}'.", LogCategory.PrefabManager, LogLevel.Info);
                    }
                }

                if (existing == null)
                {
                    existing = SaveManager.Instance?.FindGameObjectByUniqueID(prefabData.InstanceID, SaveManager.IdentifierType.UniqueID);
                    if (existing != null)
                    {
                        Logger.Log($"PrefabManager.InstantiatePrefabInternal: Resolved existing instance for '{prefabData.InstanceID}' via SaveManager lookup.", LogCategory.PrefabManager, LogLevel.Info);
                    }
                }

                if (existing == null && prefabData.ReuseSceneInstanceOnLoad)
                {
                    string sceneState = "<unknown>";
                    if (!string.IsNullOrEmpty(prefabData.HomeScene))
                    {
                        Scene target = SceneManager.GetSceneByName(prefabData.HomeScene);
                        if (target.IsValid())
                            sceneState = target.isLoaded ? $"loaded scene '{target.name}'" : $"unloaded scene '{target.name}'";
                        else
                            sceneState = $"scene '{prefabData.HomeScene}' not found";
                    }

                    string message =
                        $"PrefabManager.InstantiatePrefabsCoroutine: Requested reuse for UniqueID '{prefabData.InstanceID ?? "<null>"}' but no scene instance was found. " +
                        $"Falling back to instantiation (hadTrackedMapping={hadTrackedMapping}, removedStale={removedStaleMapping}, homeSceneState={sceneState}).";
                    Logger.Log(message, LogCategory.PrefabManager, LogLevel.Info);
                }

                if (existing != null)
                {
                    // Treat an existing scene instance as reuse either when the save-data requests it
                    // or when the component on the instance is configured to reuse scene instances.
                    bool reuseRequested = prefabData.ReuseSceneInstanceOnLoad;
                    bool reuseBySceneFlag = false;
                    if (existing.TryGetComponent(out SaveablePrefab existingSp))
                        reuseBySceneFlag = existingSp.ReuseSceneInstanceOnLoad && !existingSp.IsAddedAtRuntime;

                    if (reuseRequested || reuseBySceneFlag)
                    {
                        instantiatedPrefabs[prefabData.InstanceID] = existing;

                        if (existing.TryGetComponent(out SaveablePrefab reusedSaveable))
                        {
                            // Restore tracking flags BEFORE SetUniqueID.
                            // SetUniqueID → SaveableComponent.OverrideGameObjectID → RegisterWithSaveManager
                            // → RefreshPrefabManagementFlag(), which reads TrackComponentBlobs.
                            // If TrackComponentBlobs isn't restored from save data yet, the prefab asset's
                            // default value is used instead, potentially blocking component registration
                            // and causing saved data to go to QUEUING PENDING (never resolved).
                            reusedSaveable.TrackAddedComponents = prefabData.TrackAddedComponents;
                            reusedSaveable.TrackComponentBlobs = prefabData.TrackComponentBlobs;
                            reusedSaveable.TrackMaterialOverrides = prefabData.TrackMaterialOverrides;
                            reusedSaveable.TrackChildStateOverrides = prefabData.TrackChildStateOverrides;
                            reusedSaveable.TrackChildTransformOverrides = prefabData.TrackChildTransformOverrides;
                            reusedSaveable.TrackSkinnedMeshOverrides = prefabData.TrackSkinnedMeshOverrides;
                            reusedSaveable.TrackBlendshapeOverrides = prefabData.TrackBlendshapeOverrides;
                            reusedSaveable.TrackTextureOverrides = prefabData.TrackTextureOverrides;
                            reusedSaveable.TrackParticleSnapshots = prefabData.TrackParticleSnapshots;
                            reusedSaveable.TrackColliderSettings = prefabData.TrackColliderSettings;

                            reusedSaveable.SetUniqueID(prefabData.InstanceID);
                            reusedSaveable.SetHomeScene(prefabData.HomeScene);
                            reusedSaveable.LoadPriority = prefabData.LoadPriority;
                            reusedSaveable.DeferLowPriorityUntilRequested = prefabData.DeferLowPriorityUntilRequested;
                            reusedSaveable.DisablePooling = prefabData.DisablePooling;

                            // Apply runtime modifications first (which may include active state changes)
                            if (prefabData.RuntimeModificationData != null && prefabData.RuntimeModificationData.Length > 0)
                            {
                                reusedSaveable.ApplyRuntimeModifications(prefabData.RuntimeModificationData);
                            }

                            PersistentManager.MakePersistent(existing, reusedSaveable.KeepAcrossScenes);

                            // Restore visibility settings from save data.
                            // PersistentVisibilityController is a plain MonoBehaviour (not a
                            // SaveableComponent), so its data lives exclusively in
                            // SaveablePrefabData.VisibilitySettingsData. Without this call,
                            // the controller falls back to whatever Awake() initialized
                            // (which may be empty defaults), causing the prefab to appear
                            // visible in all scenes instead of only its configured scenes.
                            bool applyVisibility = prefabData.UsesOptimizationFlags ? prefabData.HasVisibilityData : true;
                            if (applyVisibility && prefabData.VisibilitySettingsData != null && prefabData.VisibilitySettingsData.Length > 0)
                            {
                                var pvc = reusedSaveable.GetComponent<PersistentVisibilityController>();
                                pvc?.DeserializeAndStoreSettings(prefabData.VisibilitySettingsData);
                            }
                        }

                        // Apply saved active state AFTER runtime modifications to ensure it takes precedence
                        if (prefabData.ActiveSelfAtSave.HasValue)
                        {
                            Logger.Log($"[CrystalSave][ActiveState] Setting '{existing.name}' active state to {prefabData.ActiveSelfAtSave.Value} (was {existing.activeSelf}) for InstanceID '{prefabData.InstanceID}'", LogCategory.PrefabManager, LogLevel.Info);
                            existing.SetActive(prefabData.ActiveSelfAtSave.Value);
                            Logger.Log($"[CrystalSave][ActiveState] After SetActive: '{existing.name}' active state is now {existing.activeSelf}", LogCategory.PrefabManager, LogLevel.Info);
                            
                            // Also update the GameObjectTracker to prevent conflicts with ApplyGameObjectActiveStates
                            if (SaveManager.Instance?.GameObjectTracker != null)
                            {
                                // Try to get the UniqueID from RememberGameObject component
                                string gameObjectId = null;
                                if (existing.TryGetComponent<RememberGameObject>(out var rememberGO))
                                {
                                    gameObjectId = rememberGO.GameObjectUniqueID;
                                }
                                else if (existing.TryGetComponent<UniqueID>(out var uniqueIdComp))
                                {
                                    gameObjectId = uniqueIdComp.ID;
                                }
                                
                                if (!string.IsNullOrEmpty(gameObjectId))
                                {
                                    Logger.Log($"[CrystalSave][ActiveState] Updating GameObjectTracker for GameObject ID '{gameObjectId}' to active state {prefabData.ActiveSelfAtSave.Value}", LogCategory.PrefabManager, LogLevel.Info);
                                    SaveManager.Instance.GameObjectTracker.UpdateActiveState(gameObjectId, prefabData.ActiveSelfAtSave.Value);
                                }
                            }
                        }

                        TryProcessDeferredComponents(prefabData, existing,
                            "PrefabManager.InstantiatePrefabsCoroutine (reuse)");
                        Logger.Log(
                            $"PrefabManager.InstantiatePrefabsCoroutine: Reused scene instance for UniqueID '{prefabData.InstanceID ?? "<null>"}' " +
                            $"(PrefabID '{prefabData.PrefabID ?? "<null>"}').",
                            LogCategory.PrefabManager,
                            LogLevel.Info);
            yield break;
                    }

                    if (clearExistingPrefabs)
                    {
                        if (existing.TryGetComponent(out SaveablePrefab existSp) && ShouldUsePoolingFor(existSp))
                        {
                            SaveablePrefabPoolCache.TryDespawn(existSp, GetPoolSizeForPrefabID(existSp.PrefabAssetID), true);
                        }
                        else
                        {
                            DestroyHelper.DestroyWithLogging(existing,
                                "PrefabManager.InstantiatePrefabsCoroutine: removing stale instance");
                        }
                        yield return null; // wait one frame for destruction/despawn
                    }
                    else
                    {
                        if (!instantiatedPrefabs.ContainsKey(prefabData.InstanceID))
                            instantiatedPrefabs[prefabData.InstanceID] = existing;
                        TryProcessDeferredComponents(prefabData, existing,
                            "PrefabManager.InstantiatePrefabsCoroutine (reuse)");
                        yield break;
                    }
                }

                if (string.IsNullOrEmpty(prefabData.PrefabID))
                {
                    // Synthesized placeholders (scene objects) do not correspond to prefab assets.
                    yield break;
                }

                GameObject originalPrefab = GetPrefabByID(prefabData.PrefabID);
                if (!originalPrefab)
                {
                    // Skip this entry (prefab asset not found or removed from registry)
                    yield break;
                }

                GameObject instance = null;
                var assetSaveable = originalPrefab.GetComponent<SaveablePrefab>();
                object prefabLock = prefabLocks.GetOrAdd(originalPrefab, _ => new object());
                lock (prefabLock)
                {
                    bool wasLoading = assetSaveable != null && assetSaveable.IsLoading;
                    if (assetSaveable != null)
                        assetSaveable.SetLoading(true);

                    try
                    {
                        if (assetSaveable != null && ShouldUsePoolingFor(prefabData, assetSaveable))
                        {
                            var pooled = SaveablePrefabPoolCache.Get(assetSaveable, GetPoolSizeForPrefab(assetSaveable), true);
                            var spawned = pooled?.Spawn(prefabData.Position, prefabData.Rotation);
                            instance = spawned != null ? spawned.gameObject : null;
                        }
                        else
                        {
                            instance = Instantiate(originalPrefab);
                        }
                    }
                    finally
                    {
                        if (assetSaveable != null)
                            assetSaveable.SetLoading(wasLoading);
                    }
                }

                if (instance == null)
                {
                    yield break;
                }

                // Use saved GameObject name if available, otherwise fall back to prefab name
                instance.name = !string.IsNullOrEmpty(prefabData.GameObjectName) ? prefabData.GameObjectName : originalPrefab.name;

                // Apply saved active state for scene-baked prefabs before any other setup
                if (prefabData.ActiveSelfAtSave.HasValue)
                {
                    instance.SetActive(prefabData.ActiveSelfAtSave.Value);
                }

                var saveable = instance.GetComponent<SaveablePrefab>();
                if (saveable != null)
                {
                    // For procedural objects (TrackAddedComponents = true), do NOT set the original prefab asset.
                    // The blank prefab used for instantiation has no components, so comparing against it
                    // causes MissingComponentException when capturing mesh/material overrides.
                    // Procedural objects save everything as "added at runtime" anyway.
                    if (!prefabData.TrackAddedComponents)
                    {
                        saveable.SetOriginalPrefabAsset(originalPrefab);
                    }

                    // Restore tracking flags BEFORE SetUniqueID.
                    // SetUniqueID → SaveableComponent.OverrideGameObjectID → RegisterWithSaveManager
                    // → RefreshPrefabManagementFlag(), which reads TrackComponentBlobs.
                    // If TrackComponentBlobs isn't restored from save data yet, the prefab asset's
                    // default value is used instead, potentially blocking component registration
                    // and causing saved data to go to QUEUING PENDING (never resolved).
                    saveable.TrackAddedComponents = prefabData.TrackAddedComponents;
                    saveable.TrackComponentBlobs = prefabData.TrackComponentBlobs;
                    saveable.TrackMaterialOverrides = prefabData.TrackMaterialOverrides;
                    saveable.TrackChildStateOverrides = prefabData.TrackChildStateOverrides;
                    saveable.TrackChildTransformOverrides = prefabData.TrackChildTransformOverrides;
                    saveable.TrackSkinnedMeshOverrides = prefabData.TrackSkinnedMeshOverrides;
                    saveable.TrackBlendshapeOverrides = prefabData.TrackBlendshapeOverrides;
                    saveable.TrackTextureOverrides = prefabData.TrackTextureOverrides;
                    saveable.TrackParticleSnapshots = prefabData.TrackParticleSnapshots;
                    saveable.TrackColliderSettings = prefabData.TrackColliderSettings;

                    if (prefabData.TrackAddedComponents)
                    {
                        Logger.Log($"PrefabManager: Restored TrackAddedComponents=true for '{instance.name}'", LogCategory.PrefabManager, LogLevel.Info);
                    }

                    saveable.SetUniqueID(prefabData.InstanceID);
                    saveable.SetHomeScene(prefabData.HomeScene);
                    saveable.LoadPriority = prefabData.LoadPriority;
                    saveable.DeferLowPriorityUntilRequested = prefabData.DeferLowPriorityUntilRequested;
                    
                    // Restore the saved DisablePooling setting to the component
                    saveable.DisablePooling = prefabData.DisablePooling;

                    if (!instantiatedPrefabs.ContainsKey(prefabData.InstanceID))
                        instantiatedPrefabs.Add(prefabData.InstanceID, instance);
                    else
                        instantiatedPrefabs[prefabData.InstanceID] = instance;
                    RegisterPrefab(saveable);
                    PersistentManager.MakePersistent(instance, saveable.KeepAcrossScenes);

                    bool applyVisibility = prefabData.UsesOptimizationFlags ? prefabData.HasVisibilityData : true;

                    if (applyVisibility && prefabData.VisibilitySettingsData != null && prefabData.VisibilitySettingsData.Length > 0)
                    {
                        var pvc = saveable.GetComponent<PersistentVisibilityController>();
                        pvc?.DeserializeAndStoreSettings(prefabData.VisibilitySettingsData);
                    }

                    if (prefabData.RuntimeModificationData != null && prefabData.RuntimeModificationData.Length > 0)
                    {
                        saveable.ApplyRuntimeModifications(prefabData.RuntimeModificationData);
                    }
                }

                // Mark this ID as freshly instantiated in this pass
                if (!string.IsNullOrEmpty(prefabData.InstanceID))
                    justInstantiatedIds.Add(prefabData.InstanceID);

                var rb = instance.GetComponent<Rigidbody>();
                if (prefabData.HasRigidbody && rb != null)
                {
                    if (!rb.isKinematic)
                    {
                        rb.angularVelocity = Vector3.zero;
#if UNITY_6000_0_OR_NEWER
                        rb.linearVelocity = Vector3.zero;
#else
                        rb.velocity = Vector3.zero;
#endif
                    }

                    rb.isKinematic = true;

                    Logger.Log($"Prefab '{instance.name}' forced Kinematic in first pass, will restore later.", LogCategory.PrefabManager, LogLevel.Info);
                }

                Logger.Log($"PrefabManager: Instantiated '{instance.name}' with ID '{prefabData.InstanceID}'.", LogCategory.PrefabManager, LogLevel.Info);

                TryProcessDeferredComponents(prefabData, instance,
                    "PrefabManager.InstantiatePrefabsCoroutine (instantiate)");

                prefabsProcessed++;
                if (batchSize > 0 && prefabsProcessed % batchSize == 0)
                    yield return null;
            }

            if (groupByScene)
            {
                var grouped = immediatePrefabs
                    .Where(pd => pd != null)
                    .GroupBy(pd => string.IsNullOrEmpty(pd.HomeScene) ? string.Empty : pd.HomeScene);

                foreach (var group in grouped)
                {
                    Scene previousActiveScene = SceneManager.GetActiveScene();
                    Scene targetScene = previousActiveScene;
                    string sceneName = group.Key;
                    
                    if (!string.IsNullOrEmpty(sceneName))
                    {
                        // Special handling for DontDestroyOnLoad - this isn't a real scene that can be found by SceneManager
                        if (sceneName == "DontDestroyOnLoad")
                        {
                            // Use the current active scene as target since DontDestroyOnLoad objects are created in the active scene
                            targetScene = previousActiveScene;
                        }
                        else
                        {
                            Scene homeScene = SceneManager.GetSceneByName(sceneName);
                            
                            if (!homeScene.IsValid() || !homeScene.isLoaded)
                            {
                                if (!pendingPrefabs.TryGetValue(sceneName, out var list))
                                {
                                    list = new List<SaveablePrefabData>();
                                    pendingPrefabs[sceneName] = list;
                                }
                                list.AddRange(group);
                                try
                                {
                                    var ids = string.Join(", ", group.Select(d => d?.InstanceID ?? "<null>").Distinct(StringComparer.Ordinal));
                                    string msg = $"PrefabManager.InstantiatePrefabsCoroutine: Deferring {group.Count()} prefab(s) for unloaded scene '{sceneName}'. InstanceIDs: {ids}";
                                    Logger.Log(msg, LogCategory.PrefabManager, LogLevel.Info);
                                }
                                catch { /* logging best-effort */ }
                                continue;
                            }
                            targetScene = homeScene;
                        }
                    }

                    bool changedScene = targetScene != previousActiveScene;
                    if (changedScene)
                        SceneManager.SetActiveScene(targetScene);

                    foreach (var pd in group)
                    {
                        yield return InstantiatePrefabInternal(pd);
                    }

                    if (changedScene)
                        SceneManager.SetActiveScene(previousActiveScene);
                }
            }
            else
            {
                foreach (var prefabData in immediatePrefabs)
                {
                    if (prefabData == null)
                        continue;

                    Scene previousActiveScene = SceneManager.GetActiveScene();
                    Scene targetScene = previousActiveScene;

                    if (!string.IsNullOrEmpty(prefabData.HomeScene))
                    {
                        // Special handling for DontDestroyOnLoad - this isn't a real scene that can be found by SceneManager
                        if (prefabData.HomeScene == "DontDestroyOnLoad")
                        {
                            // Use the current active scene as target since DontDestroyOnLoad objects are created in the active scene
                            targetScene = previousActiveScene;
                        }
                        else
                        {
                            Scene homeScene = SceneManager.GetSceneByName(prefabData.HomeScene);
                            
                            if (!homeScene.IsValid() || !homeScene.isLoaded)
                            {
                                if (!pendingPrefabs.TryGetValue(prefabData.HomeScene, out var list))
                                {
                                    list = new List<SaveablePrefabData>();
                                    pendingPrefabs[prefabData.HomeScene] = list;
                                }
                                list.Add(prefabData);
                                try
                                {
                                    string msg = $"PrefabManager.InstantiatePrefabsCoroutine: Deferring InstanceID '{prefabData.InstanceID ?? "<null>"}' for unloaded scene '{prefabData.HomeScene}'.";
                                    Logger.Log(msg, LogCategory.PrefabManager, LogLevel.Info);
                                }
                                catch { /* logging best-effort */ }
                                continue;
                            }

                            targetScene = homeScene;
                        }
                    }

                    bool changedScene = targetScene != previousActiveScene;
                    if (changedScene)
                        SceneManager.SetActiveScene(targetScene);

                    yield return InstantiatePrefabInternal(prefabData);

                    if (changedScene)
                        SceneManager.SetActiveScene(previousActiveScene);
                }
            }
// ───────────────────────────────────────────────────────────────
            // Second pass: parent, transform, restore Animator & Rigidbody
            // ───────────────────────────────────────────────────────────────
            prefabsProcessed = 0;
            var parentResolver = new ParentResolver(instantiatedPrefabs);

            foreach (var prefabData in immediatePrefabs)
            {
                if (prefabData == null || destroyedGameObjectIDs.Contains(prefabData.InstanceID))
                    continue;

                if (instantiatedPrefabs.TryGetValue(prefabData.InstanceID, out var instance))
                {
                    TryProcessDeferredComponents(prefabData, instance,
                        "PrefabManager.InstantiatePrefabsCoroutine (post-process)");

                    /* ── CharacterController safeguard ───────────────────── */
                    CharacterController cc = instance.GetComponent<CharacterController>();
                    bool ccWasEnabled      = cc && cc.enabled;
                    if (ccWasEnabled) cc.enabled = false;

                    // 1) Parenting & transform – world-vs-local aware
                    bool hasOptimizationFlags = prefabData.UsesOptimizationFlags;
                    bool wasInstantiatedNow = !string.IsNullOrEmpty(prefabData.InstanceID) && justInstantiatedIds.Contains(prefabData.InstanceID);
                    // Always apply parent/transform for newly instantiated instances, regardless of optimization flags
                    bool applyParent = wasInstantiatedNow || (hasOptimizationFlags ? prefabData.HasParentOverride : true);
                    bool applyTransform = wasInstantiatedNow || (hasOptimizationFlags ? prefabData.HasTransformOverride : true);

                    Transform resolvedParent = instance.transform.parent;

                    if (applyParent)
                    {
                        resolvedParent = parentResolver.ResolveParent(prefabData);
                        if (resolvedParent != null)
                        {
                            instance.transform.SetParent(resolvedParent, false);
                        }
                        else
                        {
                            HandleMissingParent(instance);
                            instance.transform.SetParent(null, false);
                        }
                    }

                    if (applyTransform)
                    {
                        bool hasTargetParent = resolvedParent != null;
                        // If a parent has been resolved (either by explicit ParentID or via fingerprint),
                        // and the original save-data indicates the instance HAD a parent (by ParentID or fingerprint fields),
                        // then the stored Position/Rotation are LOCAL and must be applied as such. Otherwise, treat as world.
                        bool savedHadParent =
                            !string.IsNullOrEmpty(prefabData.ParentID) ||
                            !string.IsNullOrEmpty(prefabData.ParentStableKey) ||
                            !string.IsNullOrEmpty(prefabData.ParentPrefabAssetID);
                        bool useLocal = hasTargetParent && savedHadParent;

                        if (useLocal)
                        {
                            instance.transform.localPosition = prefabData.Position;
                            instance.transform.localRotation = prefabData.Rotation;
                            
                        }
                        else
                        {
                            var beforePos = instance.transform.position;
                            var beforeRot = instance.transform.rotation.eulerAngles;
                            instance.transform.position = prefabData.Position;
                            instance.transform.rotation = prefabData.Rotation;
                            var afterPos = instance.transform.position;
                            var afterRot = instance.transform.rotation.eulerAngles;
                            
                        }

                        instance.transform.localScale = prefabData.Scale;
                    }
                    else
                    {
                        
                    }

                    // Re-apply saved active state in case downstream systems toggled it during post-processing
                    if (prefabData.ActiveSelfAtSave.HasValue && instance.activeSelf != prefabData.ActiveSelfAtSave.Value)
                    {
                        Logger.Log($"[CrystalSave][ActiveState-PostProcess] Setting '{instance.name}' active state to {prefabData.ActiveSelfAtSave.Value} (was {instance.activeSelf}) for InstanceID '{prefabData.InstanceID}'", LogCategory.PrefabManager, LogLevel.Info);
                        instance.SetActive(prefabData.ActiveSelfAtSave.Value);
                        
                        // Also update the GameObjectTracker to prevent conflicts with ApplyGameObjectActiveStates
                        if (SaveManager.Instance?.GameObjectTracker != null)
                        {
                            // Try to get the UniqueID from RememberGameObject component
                            string gameObjectId = null;
                            if (instance.TryGetComponent<RememberGameObject>(out var rememberGO))
                            {
                                gameObjectId = rememberGO.GameObjectUniqueID;
                            }
                            else if (instance.TryGetComponent<UniqueID>(out var uniqueIdComp))
                            {
                                gameObjectId = uniqueIdComp.ID;
                            }
                            
                            if (!string.IsNullOrEmpty(gameObjectId))
                            {
                                Logger.Log($"[CrystalSave][ActiveState-PostProcess] Updating GameObjectTracker for GameObject ID '{gameObjectId}' to active state {prefabData.ActiveSelfAtSave.Value}", LogCategory.PrefabManager, LogLevel.Info);
                                SaveManager.Instance.GameObjectTracker.UpdateActiveState(gameObjectId, prefabData.ActiveSelfAtSave.Value);
                            }
                        }
                    }

                    if (ccWasEnabled) cc.enabled = true;

                    // 2) ── Animator snapshot restore ──────────────────
                    if (prefabData.HasAnimator)
                    {
                        var anim = instance.GetComponent<Animator>();
                        if (anim != null && anim.isActiveAndEnabled && anim.gameObject.activeInHierarchy)
                        {
                            anim.Play(prefabData.AnimatorStateHash, 0,
                                      prefabData.AnimatorNormalizedTime);
                            anim.Update(0f); // force evaluation this frame
                            Logger.Log($"PrefabManager: Restored Animator on '{instance.name}' " +
                                       $"state={prefabData.AnimatorStateHash:X}.", LogCategory.PrefabManager, LogLevel.Info);
                        }
                    }

                    // 3) ── AfterRestore callback ─────────────────────
                    var saveable = instance.GetComponent<SaveablePrefab>();
                    if (saveable != null)
                    {
                        SaveablePrefab.RaiseAfterRestore(saveable);
                    }

                    // 4) Rigidbody restore — your existing block
                    if (prefabData.HasRigidbody)
                    {
                        var rb = instance.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            if (!syncTransformsAfterLoad)
                                Physics.SyncTransforms();

                            rb.isKinematic = prefabData.RigidbodyIsKinematic;
                            rb.useGravity  = prefabData.RigidbodyUseGravity;

                        #if UNITY_6000_0_OR_NEWER
                            rb.linearDamping    = prefabData.RigidbodyDrag;
                            rb.angularDamping   = prefabData.RigidbodyAngularDrag;
                        #else
                            rb.drag             = prefabData.RigidbodyDrag;
                            rb.angularDrag      = prefabData.RigidbodyAngularDrag;
                        #endif

                            if (!prefabData.RigidbodyIsKinematic)
                            {
                        #if UNITY_6000_0_OR_NEWER
                                rb.linearVelocity  = prefabData.RigidbodyVelocity;
                        #else
                                rb.velocity        = prefabData.RigidbodyVelocity;
                        #endif
                                rb.angularVelocity = prefabData.RigidbodyAngularVelocity;
                            }

                        #if UNITY_6000_0_OR_NEWER
                            Logger.Log($"PrefabManager: Restored Rigidbody on '{instance.name}' " +
                                    $"isKinematic={rb.isKinematic}, velocity={rb.linearVelocity}.",
                                    LogCategory.PrefabManager,
                                    LogLevel.Info);
                        #else
                            Logger.Log($"PrefabManager: Restored Rigidbody on '{instance.name}' " +
                                    $"isKinematic={rb.isKinematic}, velocity={rb.velocity}.",
                                    LogCategory.PrefabManager,
                                    LogLevel.Info);
                        #endif
                        }
                    }

                    // Colliders
                    if (prefabData.Colliders != null && prefabData.Colliders.Count > 0)
                    {
                        ApplyColliderSettings(instance, prefabData.Colliders);
                    }
                }

                prefabsProcessed++;
                if (batchSize > 0 && prefabsProcessed % batchSize == 0)
                    yield return null;
            }

            if (handleDeferral)
                OnImmediatePrefabBatchComplete?.Invoke();

            if (syncTransformsAfterLoad)
                Physics.SyncTransforms();

            Logger.Log("PrefabManager: Prefab instantiation and setup completed.", LogCategory.PrefabManager, LogLevel.Info);

            // Finalize UniqueIDs for scene-backed prefabs that are intended to be reusable or
            // skipped when unchanged but still lack an ID (common after loading where generation
            // was deferred). This ensures subsequent saves always carry proper InstanceIDs.
            try
            {
                int finalized = 0;
                foreach (var sp in saveablePrefabs)
                {
                    if (sp == null) continue;
                    if (sp.IsAddedAtRuntime) continue;
                    if (!string.IsNullOrEmpty(sp.UniqueID)) continue;
                    bool wantsReuseOrSkip = sp.ReuseSceneInstanceOnLoad || sp.SkipSavingWhenUnchanged;
                    if (!wantsReuseOrSkip) continue;

                    sp.SetUniqueID(Guid.NewGuid().ToString());
                    if (sp.RegisterWithSaveSystem)
                    {
                        try { sp.RegisterForSaving(); } catch { /* ignore */ }
                    }
                    finalized++;
                }
            }
            catch { /* best-effort only */ }

            AllPrefabsInitialized = true;
            OnAllPrefabsInitialized?.Invoke();
            yield break;
        }

    public void ClearSaveablePrefabs(
        bool preserveDeferredQueue = false,
        IReadOnlyCollection<string> instanceIDsToPreserve = null,
        IReadOnlyDictionary<string, string> preserveInstanceIdToPrefabId = null)
                {
            // Minimal tracing (Debug.Log) to avoid console flood via the plugin logger.
            
                        var preserveLookup = instanceIDsToPreserve != null
                                ? new HashSet<string>(instanceIDsToPreserve.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal)
                                : new HashSet<string>(StringComparer.Ordinal);

            var preservePairs = instanceIDsToPreserve != null
                ? instanceIDsToPreserve
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => (id, asset: preserveInstanceIdToPrefabId != null && preserveInstanceIdToPrefabId.TryGetValue(id, out var assetId)
                        ? assetId
                        : string.Empty))
                    .ToList()
                : new List<(string id, string asset)>();

            var claimedPreserveIds = new HashSet<string>(StringComparer.Ordinal);

            string ClaimPreserveIdForPrefab(SaveablePrefab prefab)
            {
                if (prefab != null && preservePairs.Count > 0)
                {
                    string assetId = prefab.PrefabAssetID ?? string.Empty;
                    for (int i = 0; i < preservePairs.Count; i++)
                    {
                        var candidate = preservePairs[i];
                        if (claimedPreserveIds.Contains(candidate.id))
                            continue;

                        if (!string.IsNullOrEmpty(candidate.asset) && string.Equals(candidate.asset, assetId, StringComparison.Ordinal))
                        {
                            claimedPreserveIds.Add(candidate.id);
                            Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Claimed InstanceID '{candidate.id}' for scene prefab '{prefab.name}' (asset '{assetId}')", LogCategory.PrefabManager, LogLevel.Info);
                            return candidate.id;
                        }
                    }
                }

                if (instanceIDsToPreserve != null)
                {
                    foreach (var id in instanceIDsToPreserve)
                    {
                        if (string.IsNullOrEmpty(id))
                            continue;
                        if (claimedPreserveIds.Contains(id))
                            continue;

                        claimedPreserveIds.Add(id);
                        Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Fallback-claimed InstanceID '{id}' for prefab '{prefab?.name ?? "<null>"}'", LogCategory.PrefabManager, LogLevel.Info);
                        return id;
                    }
                }

                return null;
            }

                        var preservedPrefabs = new List<SaveablePrefab>();
                        var preservedInstances = new Dictionary<string, GameObject>(StringComparer.Ordinal);

            // Anchor any existing instanceID->GameObject mappings we already know about
            // so subsequent scans don't reassign the same ID to a different scene object
            // on a second load within play mode.
            if (instantiatedPrefabs.Count > 0)
            {
                foreach (var kv in instantiatedPrefabs)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                    if (!preserveLookup.Contains(kv.Key)) continue; // only anchor what caller asked to preserve
                    if (!preservedInstances.ContainsKey(kv.Key))
                        preservedInstances[kv.Key] = kv.Value;
                }
                foreach (var anchoredId in preservedInstances.Keys)
                    claimedPreserveIds.Add(anchoredId);
            }
                        int destroyedCount = 0;

            // If nothing is currently tracked but we have things to preserve, attempt a targeted
            // scene scan to preserve scene-placed prefabs (ReuseSceneInstanceOnLoad) and any with
            // matching UniqueIDs. This avoids losing preservation when a subsequent load phase
            // invokes a second clear before registration repopulates the list.
            if (saveablePrefabs.Count == 0 && (preserveLookup.Count > 0))
            {
                try
                {
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                    var scenePrefabs = FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
                    foreach (var sp in scenePrefabs)
                    {
                        if (sp == null) continue;
                        string uid = sp.UniqueID;
                        bool sceneReuse = sp.ReuseSceneInstanceOnLoad && !sp.IsAddedAtRuntime;
                        bool matchByList = !string.IsNullOrEmpty(uid) && preserveLookup.Contains(uid);
                        if (!matchByList && !sceneReuse) continue;

                        // If already has an ID and it's in the preserve list, take it and remove from available
                        if (matchByList)
                        {
                            claimedPreserveIds.Add(uid);
                            preservedPrefabs.Add(sp);
                            if (!preservedInstances.ContainsKey(uid))
                                preservedInstances[uid] = sp.gameObject;
                            
                            continue;
                        }

                        // sceneReuse with empty uid → assign one of the remaining IDs if possible
                        if (string.IsNullOrEmpty(uid))
                        {
                            var assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId))
                            {
                                sp.SetUniqueID(assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
                                }
                                claimedPreserveIds.Add(assignId);
                                preservedPrefabs.Add(sp);
                                preservedInstances[assignId] = sp.gameObject;
                                
                                continue;
                            }
                        }

                        // No ID available to preserve this candidate → treat as stray and destroy
                        if (string.IsNullOrEmpty(uid))
                        {
                            if (SaveManager.Instance != null)
                                SaveManager.Instance.SoftUnregisterGameObject(sp.gameObject);
                            if (ShouldUsePoolingFor(sp))
                                SaveablePrefabPoolCache.TryDespawn(sp, GetPoolSizeForPrefab(sp), true);
                            else
                                DestroyHelper.DestroyWithLogging(sp.gameObject, "PrefabManager.ClearSaveablePrefabs(repopulate: stray)");
                            Logger.Log($"[CrystalSave] ClearSaveablePrefabs: (repopulate) destroy stray scene-reuse candidate '{sp.name}' with empty uid (no available preserve ID)", LogCategory.PrefabManager, LogLevel.Info);
                        }
                        else if (!preserveLookup.Contains(uid))
                        {
                            var assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId) && !string.Equals(assignId, uid, StringComparison.Ordinal))
                            {
                                sp.SetUniqueID(assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
                                }
                                claimedPreserveIds.Add(assignId);
                                preservedPrefabs.Add(sp);
                                preservedInstances[assignId] = sp.gameObject;
                                
                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[CrystalSave] ClearSaveablePrefabs: scene-scan preservation fallback failed: {ex.Message}", LogCategory.PrefabManager, LogLevel.Warning);
                }

                // Rebuild tracked and instance maps from preserved without destroying anything else
                saveablePrefabs.Clear();
                saveablePrefabs.AddRange(preservedPrefabs);

                instantiatedPrefabs.Clear();
                foreach (var kv in preservedInstances)
                    instantiatedPrefabs[kv.Key] = kv.Value;

                pendingPrefabs.Clear();
                if (!preserveDeferredQueue)
                    deferredPrefabs.Clear();
                deferredPrefabsStagedSinceLastClear = false;
                initializedPrefabs = preservedPrefabs.Count;
                AllPrefabsInitialized = false;
                
                return;
            }

    foreach (var prefab in saveablePrefabs.ToList())
                        {
                                if (prefab == null)
                                        continue;

                                string uniqueID = prefab.UniqueID;
                                bool shouldPreserve = !string.IsNullOrEmpty(uniqueID) && preserveLookup.Contains(uniqueID);
                                
                                // Also preserve scene prefabs that have ReuseSceneInstanceOnLoad = true
                                // These are design-time placed prefabs that should not be destroyed during clearing
                bool isScenePrefabForReuse = prefab.ReuseSceneInstanceOnLoad && !prefab.IsAddedAtRuntime;
                // Preserve scene-placed prefabs that skip saving when unchanged.
                // Absence of a save entry means "keep default scene object" not "destroy it".
                bool preserveSceneUnchanged = !prefab.IsAddedAtRuntime && prefab.SkipSavingWhenUnchanged;
                                
    bool needsAlignment = (isScenePrefabForReuse || preserveSceneUnchanged) && (!shouldPreserve || string.IsNullOrEmpty(uniqueID));

    if (needsAlignment)
    {
        string claimedId = ClaimPreserveIdForPrefab(prefab);
        if (!string.IsNullOrEmpty(claimedId) && !string.Equals(uniqueID, claimedId, StringComparison.Ordinal))
        {
            Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Assigning preserved ID '{claimedId}' to scene prefab '{prefab.name}' (was '{uniqueID ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
            uniqueID = claimedId;
            prefab.SetUniqueID(claimedId);
            if (prefab.RegisterWithSaveSystem)
            {
                try { prefab.RegisterForSaving(); }
                catch { /* ignore */ }
            }
            shouldPreserve = true;
        }

        if (!string.IsNullOrEmpty(uniqueID))
            claimedPreserveIds.Add(uniqueID);
    }

    if (shouldPreserve || isScenePrefabForReuse || preserveSceneUnchanged)
                                {
                    Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving tracked prefab '{prefab.name}' (ID '{uniqueID ?? "<empty>"}') reason: preserve={shouldPreserve}, reuse={isScenePrefabForReuse}, unchanged={preserveSceneUnchanged}", LogCategory.PrefabManager, LogLevel.Info);
                                        preservedPrefabs.Add(prefab);

                    if (!string.IsNullOrEmpty(uniqueID))
                        claimedPreserveIds.Add(uniqueID);

                    if (!string.IsNullOrEmpty(uniqueID) && !preservedInstances.ContainsKey(uniqueID))
                                        {
                                                GameObject trackedInstance = instantiatedPrefabs.TryGetValue(uniqueID, out var existingInstance) && existingInstance != null
                                                        ? existingInstance
                                                        : prefab.gameObject;
                                                preservedInstances[uniqueID] = trackedInstance;
                                        }

            
                                        continue;
                                }

                                if (SaveManager.Instance != null)
                                        SaveManager.Instance.SoftUnregisterGameObject(prefab.gameObject);

                                if (ShouldUsePoolingFor(prefab))
                                {
                                        SaveablePrefabPoolCache.TryDespawn(prefab, GetPoolSizeForPrefab(prefab), true);
                                }
                                else
                                {
                    Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Destroying tracked prefab '{prefab.name}' (ID '{uniqueID ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
                                        DestroyHelper.DestroyWithLogging(
                                                prefab.gameObject,
                                                "PrefabManager.ClearSaveablePrefabs()"
                                        );
                                }

                                if (!string.IsNullOrEmpty(uniqueID))
                                        instantiatedPrefabs.Remove(uniqueID);

                                destroyedCount++;
                
                        }

            // After processing tracked prefabs, scan the scene for any SaveablePrefabs that are
            // currently NOT tracked. If they are intended for reuse or explicitly preserved,
            // add them to the preserved set; otherwise, proactively remove them to avoid
            // lingering duplicates across subsequent loads.
            try
            {
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                var scenePrefabs = FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
                // IDs already claimed by tracked preserves
                var claimed = new HashSet<string>(preservedInstances.Keys, StringComparer.Ordinal);
                var availableIds = new HashSet<string>(preserveLookup.Where(id => !claimed.Contains(id)), StringComparer.Ordinal);
                foreach (var sp in scenePrefabs)
                {
                    if (sp == null)
                        continue;

                    // Already accounted for
                    if (preservedPrefabs.Contains(sp))
                        continue;

                    bool wasTracked = saveablePrefabs.Contains(sp);
                    if (wasTracked)
                        continue; // handled in the main loop

                    string uid = sp.UniqueID;
                    bool preserveByList = !string.IsNullOrEmpty(uid) && preserveLookup.Contains(uid);
                    bool preserveByReuse = sp.ReuseSceneInstanceOnLoad && !sp.IsAddedAtRuntime;
                    bool preserveByUnchanged = !sp.IsAddedAtRuntime && sp.SkipSavingWhenUnchanged;

                    if (preserveByList)
                    {
                        Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving stray prefab '{sp.name}' by explicit list match (ID '{uid}')", LogCategory.PrefabManager, LogLevel.Info);
                        claimedPreserveIds.Add(uid);
                        preservedPrefabs.Add(sp);
                        if (!string.IsNullOrEmpty(uid) && !preservedInstances.ContainsKey(uid))
                            preservedInstances[uid] = sp.gameObject;
                        availableIds.Remove(uid);
                        
                        continue;
                    }

                    if (preserveByReuse)
                    {
                        string assignId = uid;
                        if (string.IsNullOrEmpty(assignId) || !preserveLookup.Contains(assignId))
                        {
                            assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId) && !string.Equals(assignId, uid, StringComparison.Ordinal))
                            {
                                sp.SetUniqueID(assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
                                }
                                uid = assignId;
                            }
                        }

                        if (!string.IsNullOrEmpty(uid))
                        {
                            claimedPreserveIds.Add(uid);
                            preservedPrefabs.Add(sp);
                            preservedInstances[uid] = sp.gameObject;
                            availableIds.Remove(uid);
                            Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving stray prefab '{sp.name}' via reuse assignment (ID '{uid}')", LogCategory.PrefabManager, LogLevel.Info);
                            
                            continue;
                        }
                    }

                    // Preserve scene-placed prefabs that skip saving when unchanged.
                    // Even with empty uid and no preserve list, they represent default scene state.
                    if (preserveByUnchanged)
                    {
                        if (string.IsNullOrEmpty(uid) || !preserveLookup.Contains(uid))
                        {
                            var assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId))
                            {
                                sp.SetUniqueID(assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
                                }
                                uid = assignId;
                            }
                        }

                        if (!string.IsNullOrEmpty(uid))
                                claimedPreserveIds.Add(uid);
                        preservedPrefabs.Add(sp);
                        if (!string.IsNullOrEmpty(uid) && !preservedInstances.ContainsKey(uid))
                            preservedInstances[uid] = sp.gameObject;
                        Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving stray prefab '{sp.name}' due to SkipSavingWhenUnchanged (ID '{uid ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
                        
                        continue;
                    }

                    // Stray, untracked instance – remove to prevent duplicates later
                    if (SaveManager.Instance != null)
                        SaveManager.Instance.SoftUnregisterGameObject(sp.gameObject);

                    if (ShouldUsePoolingFor(sp))
                    {
                        SaveablePrefabPoolCache.TryDespawn(sp, GetPoolSizeForPrefab(sp), true);
                    }
                    else
                    {
                        Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Destroying stray prefab '{sp.name}' (ID '{uid ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
                        DestroyHelper.DestroyWithLogging(
                            sp.gameObject,
                            "PrefabManager.ClearSaveablePrefabs(scene-scan)"
                        );
                    }

                    if (!string.IsNullOrEmpty(uid))
                        instantiatedPrefabs.Remove(uid);

                    destroyedCount++;
                    
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ClearSaveablePrefabs: scene-scan cleanup failed: {ex.Message}", LogCategory.PrefabManager, LogLevel.Warning);
            }

                        saveablePrefabs.Clear();
                        saveablePrefabs.AddRange(preservedPrefabs);

                        instantiatedPrefabs.Clear();
                        foreach (var kvp in preservedInstances)
                                instantiatedPrefabs[kvp.Key] = kvp.Value;

                        pendingPrefabs.Clear();
                        if (!preserveDeferredQueue)
                                deferredPrefabs.Clear();
                        deferredPrefabsStagedSinceLastClear = false;
                        initializedPrefabs = preservedPrefabs.Count;
                        AllPrefabsInitialized = false;
            
                }

                /// <summary>
                /// Destroys all instantiated <see cref="SaveablePrefab"/>s that
                /// match the provided <paramref name="prefabAssetID"/>.
                /// </summary>
                /// <param name="prefabAssetID">The PrefabAssetID to match.</param>
                public void DestroyPrefabsByAssetID(string prefabAssetID)
                {
                        if (string.IsNullOrEmpty(prefabAssetID))
                        {
                                Logger.Log("PrefabManager: DestroyPrefabsByAssetID called with an empty prefabAssetID.", LogCategory.PrefabManager, LogLevel.Warning);
                                return;
                        }

                        var matches = saveablePrefabs
                                .Where(sp => sp != null && sp.PrefabAssetID == prefabAssetID)
                                .ToList();

                        foreach (var prefab in matches)
                        {
                                if (SaveManager.Instance != null)
                                        SaveManager.Instance.SoftUnregisterGameObject(prefab.gameObject);

                                if (ShouldUsePoolingFor(prefab))
                                {
                                        SaveablePrefabPoolCache.TryDespawn(prefab, GetPoolSizeForPrefab(prefab), true);
                                }
                                else
                                {
                                        DestroyHelper.DestroyWithLogging(
                                                prefab.gameObject,
                                                $"PrefabManager.DestroyPrefabsByAssetID({prefabAssetID})"
                                        );
                                }

                                saveablePrefabs.Remove(prefab);
                                instantiatedPrefabs.Remove(prefab.UniqueID);
                        }

                        if (matches.Count > 0)
                                Logger.Log($"PrefabManager: Destroyed {matches.Count} prefab instance(s) for asset ID '{prefabAssetID}'.", LogCategory.PrefabManager, LogLevel.Info);
                }

                /// <summary>
                /// Destroys all instantiated <see cref="SaveablePrefab"/>s whose
                /// asset IDs match any of the provided <paramref name="prefabAssetIDs"/>.
                /// </summary>
                /// <param name="prefabAssetIDs">List of asset IDs to match.</param>
                public void DestroyPrefabsByAssetID(List<string> prefabAssetIDs)
                {
                        if (prefabAssetIDs == null || prefabAssetIDs.Count == 0)
                        {
                                Logger.Log("PrefabManager: DestroyPrefabsByAssetID called with an empty prefabAssetID list.", LogCategory.PrefabManager, LogLevel.Warning);
                                return;
                        }

                        foreach (var id in prefabAssetIDs)
                        {
                                DestroyPrefabsByAssetID(id);
                        }
                }

        public void ProcessPendingPrefabs(string sceneName, List<string> destroyedIDs)
        {
            if (!pendingPrefabs.TryGetValue(sceneName, out var list) || list.Count == 0)
                return;

            // Check if SceneActivationPipeline hook is set and delays spawning
            if (SaveManager.Instance != null && SaveManager.Instance.SceneActivationPipeline != null)
            {
                bool allowSpawn = SaveManager.Instance.SceneActivationPipeline.Invoke(sceneName);
                if (!allowSpawn)
                {
                    Logger.Log(
                        $"[SCENELOAD] PrefabManager.ProcessPendingPrefabs: SceneActivationPipeline hook returned false for '{sceneName}'. " +
                        "Delaying prefab spawn until hook returns true.",
                        LogCategory.PrefabManager,
                        LogLevel.Info
                    );
                    return; // Don't spawn yet - hook will be checked again on next LateUpdate
                }
            }

            try
            {
                var ids = string.Join(", ", list.Select(d => d?.InstanceID ?? "<null>").Distinct(StringComparer.Ordinal));
                string msg = $"PrefabManager.ProcessPendingPrefabs: Resuming {list.Count} deferred prefab(s) for scene '{sceneName}'. InstanceIDs: {ids}";
                Logger.Log(msg, LogCategory.PrefabManager, LogLevel.Info);
            }
            catch { /* logging best-effort */ }

            pendingPrefabs.Remove(sceneName);
            StartCoroutine(InstantiatePrefabsCoroutine(list, destroyedIDs, clearExistingPrefabs: false));
        }

        private GameObject GetPrefabByID(string prefabID)
        {
            if (string.IsNullOrEmpty(prefabID))
            {
                Logger.Log("PrefabManager: GetPrefabByID called with an empty prefabID.", LogCategory.PrefabManager, LogLevel.Warning);
                return null;
            }
            var entry = prefabRegistry.FindEntryByID(prefabID);
            if (entry != null)
                return entry.prefab;

            Logger.Log($"PrefabManager: Prefab with ID '{prefabID}' not found in PrefabRegistry.", LogCategory.PrefabManager, LogLevel.Warning);
            return null;
        }

        // Returns a deterministic, slash-separated hierarchy key for the given SaveablePrefab
        // relative to its root scene object. This is used to keep mapping of empty-UID scene
        // candidates stable across sessions so two identical prefabs don't swap assignments.
        private static string GetStableHierarchyKey(SaveablePrefab sp)
        {
            if (sp == null) return string.Empty;
            var t = sp.transform;
            // Build path up to the scene root
            var segments = new System.Collections.Generic.List<string>(8);
            while (t != null)
            {
                segments.Add(t.name);
                t = t.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private bool ShouldUsePoolingFor(SaveablePrefab prefab)
        {
                if (prefab == null)
                        return false;

                if (!UsePrefabPooling)
                        return false;

                // Check if pooling is disabled directly on the SaveablePrefab component first
                if (prefab.DisablePooling)
                        return false;

                // If not disabled on component, check the PrefabRegistry setting
                return ShouldUsePoolingFor(prefab.PrefabAssetID);
        }

        /// <summary>
        /// Checks if pooling should be used for a prefab during load operations.
        /// Mirrors SaveablePrefabFactory logic but checks saved data first:
        /// 1. Saved DisablePooling setting (from SaveablePrefabData) - ultimate truth for loaded instances
        /// 2. Current prefab asset DisablePooling setting (from SaveablePrefab component)
        /// 3. PrefabRegistry.IsPoolingDisabled() setting
        /// </summary>
        private bool ShouldUsePoolingFor(SaveablePrefabData prefabData, SaveablePrefab prefabAsset)
        {
                if (prefabData == null || prefabAsset == null)
                {
                        Logger.Log("PrefabManager.ShouldUsePoolingFor: prefabData or prefabAsset is null", LogCategory.PrefabManager, LogLevel.Warning);
                        return false;
                }

                if (!UsePrefabPooling)
                {
                        Logger.Log("PrefabManager.ShouldUsePoolingFor: Global pooling disabled", LogCategory.PrefabManager, LogLevel.Info);
                        return false;
                }

                // 1. Check saved DisablePooling setting first (this is the state when originally instantiated)
                if (prefabData.DisablePooling)
                {
                        Logger.Log($"PrefabManager.ShouldUsePoolingFor: Pooling disabled by saved component data for '{prefabAsset.name}'", LogCategory.PrefabManager, LogLevel.Info);
                        return false;
                }

                // 2. Check current prefab asset DisablePooling setting
                if (prefabAsset.DisablePooling)
                {
                        Logger.Log($"PrefabManager.ShouldUsePoolingFor: Pooling disabled by asset component for '{prefabAsset.name}'", LogCategory.PrefabManager, LogLevel.Info);
                        return false;
                }

                // 3. Check PrefabRegistry setting using the correct asset ID
                if (prefabRegistry != null && prefabRegistry.IsPoolingDisabled(prefabAsset.PrefabAssetID))
                {
                        Logger.Log($"PrefabManager.ShouldUsePoolingFor: Pooling disabled by PrefabRegistry for '{prefabAsset.name}' (ID: {prefabAsset.PrefabAssetID})", LogCategory.PrefabManager, LogLevel.Info);
                        return false;
                }

                Logger.Log($"PrefabManager.ShouldUsePoolingFor: Pooling enabled for '{prefabAsset.name}'", LogCategory.PrefabManager, LogLevel.Info);
                return true;
        }

        private bool ShouldUsePoolingFor(string prefabID)
        {
            if (!UsePrefabPooling)
                return false;

            if (prefabRegistry == null || string.IsNullOrEmpty(prefabID))
                return true;

            return !prefabRegistry.IsPoolingDisabled(prefabID);
        }

        private int GetPoolSizeForPrefab(SaveablePrefab prefab)
        {
            if (prefabRegistry == null || prefab == null)
                return DefaultPoolSize;

            return prefabRegistry.ResolvePoolSize(prefab.PrefabAssetID, DefaultPoolSize);
        }

        private int GetPoolSizeForPrefabID(string prefabID)
        {
            if (prefabRegistry == null || string.IsNullOrEmpty(prefabID))
                return DefaultPoolSize;

            return prefabRegistry.ResolvePoolSize(prefabID, DefaultPoolSize);
        }

        private void HandleMissingParent(GameObject instance)
        {
            Logger.Log($"PrefabManager: No parent specified for prefab '{instance.name}'. Placing at root.", LogCategory.PrefabManager, LogLevel.Info);
        }

        /*──────────────── COLLIDER SNAPSHOTS ───────────────*/
        private static List<ColliderSnapshot> SaveColliderSettings(SaveablePrefab prefab)
        {
            if (prefab == null || !prefab.TrackColliderSettings) return null;

            var list = new List<ColliderSnapshot>();
            foreach (var col in prefab.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (!col) continue;

                var snap = new ColliderSnapshot
                {
                    Path          = GetTransformPath(prefab.transform, col.transform),
                    ColliderType  = col.GetType().Name,
                    Enabled       = col.enabled,
                    IsTrigger     = col.isTrigger
                };

                switch (col)
                {
                    case BoxCollider b:
                        snap.Center = b.center;
                        snap.Size   = b.size;
                        break;
                    case SphereCollider s:
                        snap.Center = s.center;
                        snap.Radius = s.radius;
                        break;
                    case CapsuleCollider c:
                        snap.Center    = c.center;
                        snap.Radius    = c.radius;
                        snap.Height    = c.height;
                        snap.Direction = c.direction;
                        break;
                }

                list.Add(snap);
            }
            return list.Count > 0 ? list : null;
        }

        private static void ApplyColliderSettings(GameObject instance, List<ColliderSnapshot> snaps)
        {
            var prefab = instance != null ? instance.GetComponent<SaveablePrefab>() : null;
            if (prefab == null || !prefab.TrackColliderSettings || snaps == null) return;

            foreach (var snap in snaps)
            {
                var tr = instance.transform.Find(snap.Path);
                if (!tr) continue;

                var col = tr.GetComponent(snap.ColliderType) as Collider;
                if (!col) continue;

                col.enabled = snap.Enabled;
                col.isTrigger = snap.IsTrigger;

                switch (col)
                {
                    case BoxCollider b:
                        b.center = snap.Center;
                        b.size = snap.Size;
                        break;
                    case SphereCollider s:
                        s.center = snap.Center;
                        s.radius = snap.Radius;
                        break;
                    case CapsuleCollider c:
                        c.center = snap.Center;
                        c.radius = snap.Radius;
                        c.height = snap.Height;
                        c.direction = snap.Direction;
                        break;
                }
            }
        }

        private static string GetTransformPath(Transform root, Transform t)
        {
            var stack = new System.Collections.Generic.Stack<string>();
            while (t != root && t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        #endregion
    }
}
#endif
