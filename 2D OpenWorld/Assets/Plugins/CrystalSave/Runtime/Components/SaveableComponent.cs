#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
#if CRYSTALSAVE_TIMEMACHINE
using MemoryPack;
using Arawn.CrystalSave.Runtime.TimeMachine;
#endif

namespace Arawn.CrystalSave.Runtime
{
        public enum HomeSceneMode { DesignScene, LastSnapshotScene }

#if CRYSTALSAVE_TIMEMACHINE
        /// <summary>
        /// Wrapper for component data that may include TimeMachine snapshots.
        /// </summary>
        [MemoryPackable]
        public partial class ComponentSaveDataWrapper
        {
                /// <summary>Component-specific serialized data.</summary>
                public byte[] componentData;

                /// <summary>Optional TimeMachine timeline data.</summary>
                public TimelineSaveData timeMachineData;
        }

        /// <summary>
        /// Top-level save data for TimeMachine timeline.
        /// </summary>
        [MemoryPackable]
        public partial class TimelineSaveData
        {
                public List<BranchSaveData> branches = new List<BranchSaveData>();
                public string activeBranchName = "Original";
                public float recordingSpeed = 1f;
                public bool isPlaying = false;
        }

        /// <summary>
        /// Save data for a single timeline branch.
        /// </summary>
        [MemoryPackable]
        public partial class BranchSaveData
        {
                public string branchName;
                public List<SerializableSnapshot> snapshots;
                public string sourceBranchName;
                public float branchPointTime;
        }

        /// <summary>
        /// Serializable version of GameObjectSnapshot.
        /// </summary>
        [MemoryPackable]
        public partial class SerializableSnapshot
        {
                public float timestamp;
                public long utcTimestamp;
                public string uniqueID;
                public string gameObjectName;
                public SaveablePrefabData prefabData;
                public Dictionary<string, byte[]> componentData;
                public bool activeSelf;
                public int frameCount;
                public Dictionary<string, string> metadata;

                /// <summary>
                /// Converts a GameObjectSnapshot to SerializableSnapshot.
                /// </summary>
                public static SerializableSnapshot FromGameObjectSnapshot(GameObjectSnapshot snapshot)
                {
                        if (snapshot == null) return null;

                        return new SerializableSnapshot
                        {
                                timestamp = snapshot.Timestamp,
                                utcTimestamp = snapshot.UtcTimestamp,
                                uniqueID = snapshot.UniqueID,
                                gameObjectName = snapshot.GameObjectName,
                                prefabData = snapshot.PrefabData,
                                componentData = snapshot.ComponentData,
                                activeSelf = snapshot.ActiveSelf,
                                frameCount = snapshot.FrameCount,
                                metadata = snapshot.Metadata
                        };
                }

                /// <summary>
                /// Converts serializable snapshot back to GameObjectSnapshot.
                /// </summary>
                public GameObjectSnapshot ToGameObjectSnapshot()
                {
                        return new GameObjectSnapshot
                        {
                                Timestamp = timestamp,
                                UtcTimestamp = utcTimestamp,
                                UniqueID = uniqueID,
                                GameObjectName = gameObjectName,
                                PrefabData = prefabData,
                                ComponentData = componentData,
                                ActiveSelf = activeSelf,
                                FrameCount = frameCount,
                                Metadata = metadata
                        };
                }
        }
#endif

        [DefaultExecutionOrder(0)]
        //[RequireComponent(typeof(UniqueID))]
        public abstract class SaveableComponent : MonoBehaviour , ISaveable
        {
		// When TRUE the Inspector entry is hidden; set only by RememberComposite.
                [SerializeField, HideInInspector]
                private bool _hiddenByComposite = false;

                private bool _isRegistered = false;

                private SaveablePrefab owningPrefab;
                private bool prefabHandlesSerialization;

                // When false, the component remains registered even if disabled.
                protected virtual bool UnregisterOnDisable => true;

                // Track SaveData/LoadData calls per cycle
                private static readonly Dictionary<string, int> saveCallCounts = new();
                private static readonly Dictionary<string, int> loadCallCounts = new();
                
                // Flag to indicate we're capturing a snapshot (bypasses "Skip Saving When Unchanged")
                [ThreadStatic]
                private static bool _isCapturingSnapshot;

                // Warning-gate caches: track scene-placed SaveableComponent GameObjects so we
                // only warn for runtime-instantiated objects that can become orphaned.
                private static readonly HashSet<int> cachedSceneHandles = new();
                private static readonly HashSet<int> cachedScenePlacedObjectIds = new();
                private static bool sceneWarningCacheInitialized;

                internal static void ResetSaveCallCounts() => saveCallCounts.Clear();
                internal static void ResetLoadCallCounts() => loadCallCounts.Clear();

                internal static IReadOnlyDictionary<string, int> SaveCallCounts => saveCallCounts;
                internal static IReadOnlyDictionary<string, int> LoadCallCounts => loadCallCounts;
                
                /// <summary>
                /// Sets the snapshot capture flag. During snapshot capture, "Skip Saving When Unchanged" 
                /// optimization is bypassed to ensure complete state capture for restoration.
                /// </summary>
                internal static void SetSnapshotCaptureMode(bool isCapturing)
                {
                        _isCapturingSnapshot = isCapturing;
                }
                
                /// <summary>
                /// Returns true if currently capturing a snapshot (for Remember Home Scene functionality).
                /// Protected for use in derived Remember components.
                /// </summary>
                protected bool IsCapturingSnapshot => _isCapturingSnapshot;
                
                /// <summary>
                /// Public static accessor for snapshot capture state.
                /// Used by SaveablePrefab and other components that don't inherit from SaveableComponent.
                /// </summary>
                public static bool IsCapturingSnapshotStatic => _isCapturingSnapshot;

                [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
                private static void ResetWarningCaches()
                {
                        if (sceneWarningCacheInitialized)
                                SceneManager.sceneLoaded -= HandleSceneLoadedForWarningCache;

                        sceneWarningCacheInitialized = false;
                        cachedSceneHandles.Clear();
                        cachedScenePlacedObjectIds.Clear();
                }

                private static void EnsureWarningCacheInitialized()
                {
                        if (sceneWarningCacheInitialized) return;

                        sceneWarningCacheInitialized = true;
                        SceneManager.sceneLoaded += HandleSceneLoadedForWarningCache;

                        for (int i = 0; i < SceneManager.sceneCount; i++)
                                CacheScenePlacedSaveables(SceneManager.GetSceneAt(i));
                }

                private static void HandleSceneLoadedForWarningCache(Scene scene, LoadSceneMode _)
                {
                        CacheScenePlacedSaveables(scene);
                }

                private static void CacheScenePlacedSaveables(Scene scene)
                {
                        if (!scene.IsValid()) return;

                        int handle = scene.handle;
                        if (!cachedSceneHandles.Add(handle))
                                return;

                        var roots = scene.GetRootGameObjects();
                        foreach (var root in roots)
                        {
                                if (root == null) continue;

                                var saveables = root.GetComponentsInChildren<SaveableComponent>(true);
                                foreach (var saveable in saveables)
                                {
                                        if (saveable?.gameObject == null) continue;
                                        cachedScenePlacedObjectIds.Add(saveable.gameObject.GetInstanceID());
                                }
                        }
                }

                private bool IsScenePlacedSaveable()
                {
                        EnsureWarningCacheInitialized();

                        var scene = gameObject.scene;
                        if (!scene.IsValid()) return false;

                        if (!cachedSceneHandles.Contains(scene.handle))
                                CacheScenePlacedSaveables(scene);

                        return cachedScenePlacedObjectIds.Contains(gameObject.GetInstanceID());
                }

                private bool LooksLikeInstantiatedPrefab()
                {
                        if (gameObject.name.EndsWith("(Clone)", StringComparison.Ordinal))
                                return true;

                        var root = transform.root;
                        if (root != null && root.name.EndsWith("(Clone)", StringComparison.Ordinal))
                                return true;

                        return HasDuplicateUniqueIdentifier();
                }

                private bool HasDuplicateUniqueIdentifier()
                {
                        if (string.IsNullOrEmpty(uniqueID))
                                return false;

#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                        var ids = FindObjectsByType<UniqueID>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
                        foreach (var candidate in ids)
                        {
                                if (candidate == null || candidate.gameObject == gameObject)
                                        continue;

                                if (string.Equals(candidate.ID, uniqueID, StringComparison.Ordinal))
                                        return true;
                        }

                        return false;
                }

                private bool IsCoveredBySceneObjectRegistry(SaveManager sm)
                {
                        if (string.IsNullOrEmpty(uniqueID))
                                return false;

                        var registry = AssetProvider.Load<SceneObjectRegistry>("SceneObjectRegistry");
                        if (registry?.Entries == null || registry.Entries.Count == 0)
                                return false;

                        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
                        candidateIds.Add(uniqueID);

                        if (sm != null)
                        {
                                var resolved = sm.ResolveDestroyedIdentifierInfo(uniqueID);
                                if (resolved.Variants != null)
                                {
                                        foreach (var id in resolved.Variants)
                                        {
                                                if (!string.IsNullOrEmpty(id))
                                                        candidateIds.Add(id);
                                        }
                                }
                        }

                        foreach (var id in candidateIds)
                        {
                                if (string.IsNullOrEmpty(id))
                                        continue;

                                foreach (var entry in registry.Entries)
                                {
                                        if (entry == null || string.IsNullOrEmpty(entry.UniqueID) || entry.PrefabAsset == null)
                                                continue;

                                        if (string.Equals(entry.UniqueID, id, StringComparison.Ordinal))
                                                return true;

                                        if (id.StartsWith(entry.UniqueID + "_", StringComparison.Ordinal))
                                                return true;
                                }
                        }

                        return false;
                }

                private void RefreshPrefabManagementFlag()
                {
                        owningPrefab = GetComponentInParent<SaveablePrefab>(true);
                        prefabHandlesSerialization = owningPrefab != null && owningPrefab.TrackComponentBlobs;
                }

                internal bool PrefabHandlesSerialization => prefabHandlesSerialization;

		[SerializeField]
		[HideInInspector]
		protected string uniqueID;

		[SerializeField]
		protected string componentID = System.Guid.NewGuid().ToString();

                [SerializeField]
                [Tooltip("If true, this GameObject is preserved across scene loads (DontDestroyOnLoad)")]
                protected bool keepAcrossScenes = false;
                public bool KeepAcrossScenes
                {
                        get => keepAcrossScenes;
                        set => keepAcrossScenes = value;
                }

                [SerializeField]
                [Tooltip(
                        "When enabled the component's saved state is applied again when its GameObject " +
                        "is restored after being destroyed or deactivated by the save system.")]
                private bool applySavedDataOnRestore = false;

                public bool ApplySavedDataOnRestore
                {
                        get => applySavedDataOnRestore;
                        set => applySavedDataOnRestore = value;
                }

                // Home Scene memory (applies to all SaveableComponents, similar to SaveablePrefab)
                [SerializeField, Tooltip(
                        "Remember runtime state tied to a Home Scene.\n" +
                        "- Enabled: Component snapshots are captured when you leave and re-applied when you return to the Home Scene.\n" +
                        "- Mode: Choose Design Scene to lock to the scene where this object lives in the editor, or Last Snapshot Scene to update after each snapshot.\n" +
                        "- Keep Across Scenes is mutually exclusive.")]
                protected bool rememberHomeScene = false;

                [SerializeField]
                private HomeSceneMode homeSceneMode = HomeSceneMode.DesignScene;

                // Primary single home scene. Kept for backward compatibility with existing data.
                [SerializeField, Tooltip("Primary Home Scene. If empty and Remember Home Scene is enabled, it's auto-filled based on the selected mode.")]
                protected string homeScene = null;

                // Prefab reference used to respawn objects when restoring Last Snapshot Scene
                [SerializeField, Tooltip("Prefab asset ID used to recreate this object if missing when using Last Snapshot Scene mode.")]
                private string homeScenePrefabID = null;

                public string HomeScenePrefabID => homeScenePrefabID;
                public void SetHomeScenePrefabID(string prefabID) => homeScenePrefabID = prefabID;

                // Legacy list of scenes, retained only for migration of existing data.
                [SerializeField, HideInInspector]
                private List<string> homeScenes = new List<string>();

                public bool RememberHomeScene
                {
                        get => rememberHomeScene;
                        set
                        {
                                rememberHomeScene = value;
                                if (rememberHomeScene && homeSceneMode == HomeSceneMode.DesignScene && string.IsNullOrEmpty(homeScene))
                                {
                                        var s = SceneManager.GetActiveScene();
                                        homeScene = s.IsValid() ? s.name : homeScene;
                                }
                        }
                }

                public HomeSceneMode HomeSceneMode
                {
                        get => homeSceneMode;
                        set => homeSceneMode = value;
                }

                // Back-compat: single home scene
                public string HomeScene
                {
                        get { MigrateLegacyHomeSceneData(); return homeScene; }
                        set => homeScene = value;
                }

                public bool MatchesHomeScene(string sceneName)
                {
                        if (string.IsNullOrEmpty(sceneName)) return false;
                        MigrateLegacyHomeSceneData();
                        return !string.IsNullOrEmpty(homeScene) && string.Equals(homeScene, sceneName, StringComparison.Ordinal);
                }

                private void MigrateLegacyHomeSceneData()
                {
                        if (!string.IsNullOrEmpty(homeScene))
                        {
                                // Existing single scene already migrated
                                if (homeSceneMode == default)
                                        homeSceneMode = HomeSceneMode.DesignScene;
                                return;
                        }

                        if (homeScenes != null && homeScenes.Count > 0)
                        {
                                homeScene = homeScenes[0];
                                homeScenes.Clear();
                                homeSceneMode = HomeSceneMode.DesignScene;
                        }
                }

		// Visibility Toggles
		[Tooltip("List of scene names where this GameObject should remain visible.")]
		[SerializeField]
		[HideInInspector]
		public List<string> VisibleInScenes = new List<string>();

                [Header("Visibility Settings")]
                [SerializeField, Tooltip("Behaviour when object is not in one of its VisibleInScenes")]
                private OffScreenDeactivation offScreenMask =
                                         OffScreenDeactivation.Colliders |
                                         OffScreenDeactivation.Renderers |
                                         OffScreenDeactivation.RigidbodyKinematic;

                [Header("Load Scheduling")]
                [SerializeField, Tooltip("Higher priorities (closer to 100) are restored during the initial load batch. Lower values can be deferred for streaming or progressive reveal.")]
                [Range(0, 100)]
                private int loadPriority = 50;

                [SerializeField, Tooltip("When enabled, this component skips the main restoration pass and stays queued until gameplay explicitly processes deferred component data (e.g., when the owning object enters the active area).")]
                private bool deferLowPriorityUntilRequested = false;

#if CRYSTALSAVE_TIMEMACHINE
                [Header("Time Machine Recording")]
                [SerializeField, Tooltip("Enable TimeMachine recording for this component. When multiple SaveableComponents on the same GameObject enable this, they share a single TimeMachineRecorder.")]
                private bool enableTimeMachineRecording = false;

                [SerializeField, Tooltip("Automatically start recording when the component enables.")]
                private bool autoStartRecording = true;

                [SerializeField, Tooltip("Override the global recording settings from SaveSettings. When disabled, uses global defaults.")]
                private bool overrideRecordingSettings = false;

                [SerializeField, Tooltip("Record snapshots at regular intervals instead of every frame.\nOnly applies if Override Recording Settings is enabled.")]
                private bool useInterval = true;

                [SerializeField, Tooltip("Interval between snapshots (seconds). Ignored if useInterval is false.\nOnly applies if Override Recording Settings is enabled.")]
                private float snapshotInterval = 0.1f;

                [SerializeField, Tooltip("Maximum number of snapshots to keep for this GameObject.\nOnly applies if Override Recording Settings is enabled.")]
                private int maxSnapshots = 500;

                [Header("Time Machine Persistence")]
                [SerializeField, Tooltip("Save TimeMachine timeline snapshots to the save file. Requires Enable TimeMachine Recording to be true.")]
                private bool saveTimeMachineSnapshots = false;

                [SerializeField, Tooltip("Override the global Max Save Duration setting from SaveSettings. When disabled, uses SaveSettings.timeMachineMaxSaveDuration.")]
                private bool overrideMaxSaveDuration = false;

                [SerializeField, Tooltip("Maximum duration (seconds) of timeline snapshots to save.\n• Positive value: Save last N seconds\n• 0: Don't save any snapshots\n• -1: Save entire timeline (unlimited)\nOnly applies if Override Max Save Duration is enabled.")]
                private float maxSaveDuration = 30f;

                public bool EnableTimeMachineRecording
                {
                        get => enableTimeMachineRecording;
                        set => enableTimeMachineRecording = value;
                }

                public bool AutoStartRecording
                {
                        get => autoStartRecording;
                        set => autoStartRecording = value;
                }

                public bool OverrideRecordingSettings
                {
                        get => overrideRecordingSettings;
                        set => overrideRecordingSettings = value;
                }

                public bool UseInterval
                {
                        get
                        {
                                if (overrideRecordingSettings)
                                        return useInterval;
                                return SaveManager.Instance != null 
                                        ? SaveManager.Instance.SaveSettings.timeMachineUseInterval 
                                        : true;
                        }
                        set => useInterval = value;
                }

                public float SnapshotInterval
                {
                        get
                        {
                                if (overrideRecordingSettings)
                                        return snapshotInterval;
                                return SaveManager.Instance != null 
                                        ? SaveManager.Instance.SaveSettings.timeMachineSnapshotInterval 
                                        : 0.1f;
                        }
                        set => snapshotInterval = value;
                }

                public int MaxSnapshots
                {
                        get
                        {
                                if (overrideRecordingSettings)
                                        return maxSnapshots;
                                return SaveManager.Instance != null 
                                        ? SaveManager.Instance.SaveSettings.timeMachineMaxSnapshots 
                                        : 500;
                        }
                        set => maxSnapshots = value;
                }

                public bool SaveTimeMachineSnapshots
                {
                        get => saveTimeMachineSnapshots;
                        set
                        {
                                // Only allow setting to true if recording is enabled
                                if (value && !enableTimeMachineRecording)
                                {
                                        Logger.Log($"[{gameObject.name}] Cannot enable Save Time Machine Snapshots when Enable Time Machine Recording is false.", LogCategory.SaveableComponent, LogLevel.Warning);
                                        return;
                                }
                                saveTimeMachineSnapshots = value;
                        }
                }

                public bool OverrideMaxSaveDuration
                {
                        get => overrideMaxSaveDuration;
                        set => overrideMaxSaveDuration = value;
                }

                public float MaxSaveDuration
                {
                        get => maxSaveDuration;
                        set => maxSaveDuration = value;
                }
                
                /// <summary>
                /// Returns true if TimeMachine snapshots should actually be persisted.
                /// Checks: recording enabled, saving enabled, and duration is not 0.
                /// </summary>
                public bool ShouldPersistTimeMachineSnapshots
                {
                        get
                        {
                                if (!enableTimeMachineRecording || !saveTimeMachineSnapshots)
                                        return false;
                                
                                // Get effective duration
                                float duration = overrideMaxSaveDuration 
                                        ? maxSaveDuration 
                                        : (SaveManager.Instance != null ? SaveManager.Instance.SaveSettings.timeMachineMaxSaveDuration : 30f);
                                
                                // Don't persist if duration is 0
                                return duration != 0f;
                        }
                }
#endif

                // backwards-compat wrappers (keep the public API working)
                public bool DisableAllColliders
                {
                        get => offScreenMask.HasFlag(OffScreenDeactivation.Colliders);
                        set => offScreenMask = value
                                ? offScreenMask | OffScreenDeactivation.Colliders
                                : offScreenMask & ~OffScreenDeactivation.Colliders;
                }
		public bool DisableRenderers
		{
			get => offScreenMask.HasFlag(OffScreenDeactivation.Renderers);
			set => offScreenMask = value
				? offScreenMask | OffScreenDeactivation.Renderers
				: offScreenMask & ~OffScreenDeactivation.Renderers;
		}
		public bool DisableCharacterController
		{
			get => offScreenMask.HasFlag(OffScreenDeactivation.CharacterController);
			set => offScreenMask = value
				? offScreenMask | OffScreenDeactivation.CharacterController
				: offScreenMask & ~OffScreenDeactivation.CharacterController;
		}
                public bool SetRigidbodyKinematic
                {
                        get => offScreenMask.HasFlag(OffScreenDeactivation.RigidbodyKinematic);
                        set => offScreenMask = value
                                ? offScreenMask | OffScreenDeactivation.RigidbodyKinematic
                                : offScreenMask & ~OffScreenDeactivation.RigidbodyKinematic;
                }

                public int LoadPriority
                {
                        get => loadPriority;
                        set => loadPriority = Mathf.Clamp(value, 0, 100);
                }

                public bool DeferLowPriorityUntilRequested
                {
                        get => deferLowPriorityUntilRequested;
                        set => deferLowPriorityUntilRequested = value;
                }

                public string GameObjectUniqueID => uniqueID;
                public string ComponentID
                {
                        get => componentID;
                        set => componentID = value;
		}
		public string UniqueIdentifier 
		{
			get
			{
				string gameObjectUniqueID = this.uniqueID ?? gameObject.name;
				string componentID = ComponentID;
				string identifier = $"{gameObjectUniqueID}_{componentID}";
				// Note: Removed frequent logging to improve performance during TimeMachine recording
				// Logger.Log($"[SaveableComponent] UniqueIdentifier for '{gameObject.name}': {identifier}", LogLevel.Info);
				return identifier;
			}
		}

		private SaveDataSerializer serializer = SaveDataSerializer.Instance;
		public SaveDataSerializer Serializer => serializer;

                protected virtual void Awake()
                {
                        MigrateLegacyHomeSceneData();

                        RefreshPrefabManagementFlag();

                        /* 1️⃣  pick up / create GameObject-ID */
                        var prefab = GetComponent<SaveablePrefab>();
                        if (homeSceneMode == HomeSceneMode.LastSnapshotScene &&
                                string.IsNullOrEmpty(homeScenePrefabID) &&
                                prefab != null && !string.IsNullOrEmpty(prefab.PrefabAssetID))
                        {
                                homeScenePrefabID = prefab.PrefabAssetID;
                        }

                        // CRITICAL FIX: If this GameObject is a child (or root) of a SaveablePrefab,
                        // use the parent prefab's UniqueID. Child GameObjects should NOT have their own
                        // UniqueID components - they rely on the parent prefab's ID for identification.
                        // The componentID (GUID) differentiates between different component types.
                        if (owningPrefab != null && !string.IsNullOrEmpty(owningPrefab.UniqueID))
                        {
                                // Use the parent SaveablePrefab's UniqueID directly
                                uniqueID = owningPrefab.UniqueID;
                                Logger.Log($"[SaveableComponent] '{gameObject.name}' is part of SaveablePrefab - Using parent's uniqueID: {uniqueID}", LogCategory.SaveableComponent, LogLevel.Info);
                        }
                        else if (TryGetComponent<UniqueID>(out var uid) && !string.IsNullOrEmpty(uid.ID))
                        {
                                // When a UniqueID component already exists AND we're NOT part of a SaveablePrefab,
                                // its ID is the ground truth. Always mirror it to our internal identifier.
                                uniqueID = uid.ID;
                        }
                        else if (prefab && !string.IsNullOrEmpty(prefab.UniqueID))
                        {
                                uniqueID = prefab.UniqueID;
                        }
                        else
                        {
                                // No UniqueID component present – generate a fresh one
                                uniqueID = Guid.NewGuid().ToString();
                        }

                        // Ensure a UniqueID component exists with the correct value
                        UniqueIDUtility.EnsureComponent(gameObject, uniqueID);

                        /* 2️⃣  ensure componentID exists */
                        if (string.IsNullOrEmpty(componentID))
                        {
                                // Deterministic ID: same component type at the same sibling index
                                // always produces the same componentID, even across multiple
                                // Instantiate() calls from the same prefab asset that ships with
                                // empty componentIDs (e.g., children added by RememberComposite).
                                // This is critical for RememberHomeScene prefabs which are destroyed
                                // and re-instantiated on scene switch: random IDs would cause the
                                // snapshot data (keyed by {goUID}_{componentID}) to become
                                // unreachable after re-instantiation.
                                var sameType = GetComponents(GetType());
                                int idx = System.Array.IndexOf(sameType, this);
                                componentID = $"{GetType().Name}_{idx}";
                        }

                        /* 3️⃣  **reactivate** hidden components when play begins */
                        if (_hiddenByComposite && !enabled && Application.isPlaying)
                                enabled = true;      // required for OnTransformParentChanged to fire :contentReference[oaicite:0]{index=0}

                        /* 4️⃣  optional persistence */
                        PersistentManager.MakePersistent(gameObject, keepAcrossScenes);

                        // 5️⃣  auto-assign home scene if opted-in and mode is DesignScene and not set yet
                        if (rememberHomeScene && homeSceneMode == HomeSceneMode.DesignScene && string.IsNullOrEmpty(homeScene))
                        {
                                var s = SceneManager.GetActiveScene();
                                if (s.IsValid())
                                        homeScene = s.name;
                        }

                        // 6️⃣  Inherit RememberHomeScene from parent SaveablePrefab
                        // When a SaveablePrefab has rememberHomeScene enabled, the editor
                        // grey-outs and locks the individual SaveableComponent's toggle, but
                        // never actually enables it.  The snapshot-restore flow
                        // (TryApplyRememberedSnapshot) is gated on sc.RememberHomeScene, so
                        // without this inheritance the component's cached snapshot is never
                        // re-applied when the prefab respawns in its home scene.
                        if (!rememberHomeScene && prefab != null && prefab.RememberHomeScene)
                        {
                                rememberHomeScene = true;
                                homeScene = prefab.HomeScene;
                        }

                        if (!Application.isPlaying) return;

                        if (!prefabHandlesSerialization)
                                RegisterWithSaveManager();

#if CRYSTALSAVE_TIMEMACHINE
                        // If this component opted into TimeMachine recording, ensure a TimeMachineRecorder exists
                        if (enableTimeMachineRecording)
                        {
                                EnsureTimeMachineRecorder();
                        }
#endif
                }

		protected virtual void OnEnable()
		{
                        RefreshPrefabManagementFlag();

                        /* Bail out when this helper is serialized by a parent SaveablePrefab.
                        * When Track Component Blobs is enabled the prefab owns the data. */
                        if (prefabHandlesSerialization)
                                return;

			/* ───────────────── original scene-object flow ─────────────────── */

			// 1️⃣  Sync with sibling prefab component if we *are* a prefab root
			var prefab = GetComponent<SaveablePrefab>();
			if (prefab && !string.IsNullOrEmpty(prefab.UniqueID) &&
				prefab.UniqueID != uniqueID)
				uniqueID = prefab.UniqueID;

			// 2️⃣  Normal registration for objects that are NOT inside a prefab
			RegisterWithSaveManager();
		}

		/* helper – waits until prefab.IsLoading == false, then registers            */
		private IEnumerator WaitForPrefabAndRegister(SaveablePrefab prefab)
		{
			// safety: bail out if either object is destroyed while we wait
			while (prefab && prefab.IsLoading && this != null)
				yield return null;

			if (this == null || _isRegistered) yield break;      // destroyed or already done

			// prefab may now carry the definitive UniqueID → sync once more
			if (prefab && !string.IsNullOrEmpty(prefab.UniqueID))
				uniqueID = prefab.UniqueID;

                        RefreshPrefabManagementFlag();
                        if (!prefabHandlesSerialization)
                                RegisterWithSaveManager();
		}

                private bool _waitingForInit = false;

                private void WarnIfMissingSaveablePrefab(SaveManager sm)
                {
                        // During loading, objects are actively reconstructed and may be in transient states.
                        if (sm?.StateMachine?.CurrentState == SaveState.Loading)
                                return;

                        // If this object is part of a SaveablePrefab hierarchy, PrefabManager can recreate it.
                        if (GetComponentInParent<SaveablePrefab>(true) != null)
                                return;

                        // Scene Object Registry restores use the "_Restored" suffix and don't require SaveablePrefab.
                        if (gameObject.name.EndsWith("_Restored", StringComparison.Ordinal))
                                return;

                        // Scene-placed objects are expected to exist naturally and are not orphan risk.
                        if (IsScenePlacedSaveable())
                                return;

                        // Restrict this warning to likely prefab-instantiation cases.
                        if (!LooksLikeInstantiatedPrefab())
                                return;

                        // Objects mapped in the Scene Object Registry can be recreated without SaveablePrefab.
                        if (IsCoveredBySceneObjectRegistry(sm))
                                return;

                        string typeName = GetType().Name;
                        Debug.LogWarning(
                                $"[Crystal Save] Runtime-instantiated '{typeName}' on '{gameObject.name}' is registered for saving, " +
                                $"but this GameObject has no SaveablePrefab component. " +
                                $"Data will be serialized, however PrefabManager cannot recreate this object on load — " +
                                $"the saved data will be orphaned. To fix this:\n" +
                                $"  (a) Add a SaveablePrefab component to the source prefab and register it in the PrefabRegistry, OR\n" +
                                $"  (b) Use SaveablePrefabFactory.Instantiate() instead of GameObject.Instantiate() to create this object.\n" +
                                $"  (This warning is suppressed for scene-placed objects and Scene Object Registry mappings.)",
                                gameObject);
                }

                private void RegisterWithSaveManager()
                {
                        RefreshPrefabManagementFlag();
                        
                        if (_isRegistered) return;                          // done already

                        if (prefabHandlesSerialization)
                        {
                                return;
                        }

                        var sm = SaveManager.Instance;
                        if (sm != null && sm.ComponentManager != null)
                        {
                                sm.ComponentManager.RegisterSaveableComponent(this);
                                _isRegistered = true;
                                WarnIfMissingSaveablePrefab(sm);
                        }
                        else if (Application.isPlaying)
                        {
                                if (isActiveAndEnabled)
                                        StartCoroutine(WaitForSaveManagerAndRegister());
                                else if (!_waitingForInit)
                                {
                                        SaveManager.Initialized += OnSaveManagerInitialized;
                                        _waitingForInit = true;
                                }
                        }
                }

                private void OnSaveManagerInitialized(SaveManager manager)
                {
                        SaveManager.Initialized -= OnSaveManagerInitialized;
                        _waitingForInit = false;
                        RegisterWithSaveManager();
                }

		private IEnumerator WaitForSaveManagerAndRegister()
		{
			// wait until SaveManager and its ComponentManager are both alive
			while (this != null &&
			       (SaveManager.Instance == null ||
			        SaveManager.Instance.ComponentManager == null))
			{
				yield return null;                              // one frame
			}

			if (this == null || _isRegistered) yield break;     // destroyed in the meantime

                        RefreshPrefabManagementFlag();
                        if (prefabHandlesSerialization) yield break;

			SaveManager.Instance.ComponentManager.RegisterSaveableComponent(this);
			_isRegistered = true;
                        WarnIfMissingSaveablePrefab(SaveManager.Instance);
		}

                protected virtual void OnDisable()
                {
                        if (!UnregisterOnDisable)
                        {
                                return;
                        }
                        if (_waitingForInit)
                        {
                                SaveManager.Initialized -= OnSaveManagerInitialized;
                                _waitingForInit = false;
                        }
                        if (_isRegistered && SaveManager.Instance != null)
                        {
                                SaveManager.Instance.ComponentManager.UnregisterSaveableComponent(this);
                                _isRegistered = false;
                        }
		}

                protected virtual void OnDestroy()
                {
                        if (_waitingForInit)
                        {
                                SaveManager.Initialized -= OnSaveManagerInitialized;
                                _waitingForInit = false;
                        }
                        
                        // Unregister from ComponentManager to prevent stale references
                        try
                        {
                                ComponentManager.Instance?.UnregisterSaveableComponent(this);
                        }
                        catch (System.Exception)
                        {
                                // Silently ignore - ComponentManager may already be destroyed during app shutdown
                        }
                }

                public virtual byte[] SaveData()
                {
                        lock (saveCallCounts)
                        {
                                string id = UniqueIdentifier;
                                if (!string.IsNullOrEmpty(id))
                                        saveCallCounts[id] = saveCallCounts.TryGetValue(id, out var c) ? c + 1 : 1;
                        }

                        // Serialize component-specific data
                        byte[] componentData = SerializeComponentData();

#if CRYSTALSAVE_TIMEMACHINE
                        // ARCHITECTURAL FIX: Detect calling context to prevent expensive TimeMachine persistence
                        // operations during runtime recording. Only capture TimeMachine data for disk saves.
                        bool isTimeMachineRecordingCall = IsCalledFromTimeMachineRecording();
                        
                        if (!isTimeMachineRecordingCall && ShouldPersistTimeMachineSnapshots)
                        {
                                TimelineSaveData timeMachineData = CaptureTimeMachineData();
                                
                                // Wrap component data with TimeMachine data
                                var wrapper = new ComponentSaveDataWrapper
                                {
                                        componentData = componentData,
                                        timeMachineData = timeMachineData
                                };
                                
                                return MemoryPackSerializer.Serialize(wrapper);
                        }
#endif

                        // Capture and serialize visibility settings
                        PersistentVisibilityController pvc = GetComponent<PersistentVisibilityController>();
                        byte[] visibilityData = pvc != null ? pvc.CaptureAndSerializeSettings() : null;

			return componentData;
		}                public virtual void LoadData(byte[] data)
                {
                        if (this == null)
                        {
                                Logger.Log("LoadData skipped: component was destroyed before restore.", LogCategory.SaveableComponent, LogLevel.Info);
                                return;
                        }

                        string id = UniqueIdentifier;
                        string stackTrace = System.Environment.StackTrace;
                        
                        lock (loadCallCounts)
                        {
                                if (!string.IsNullOrEmpty(id))
                                {
                                        int newCount = loadCallCounts.TryGetValue(id, out var c) ? c + 1 : 1;
                                        loadCallCounts[id] = newCount;
                                }
                        }

                        if (data == null || data.Length == 0)
                        {
                                Logger.Log("LoadData failed: data is null or empty.", LogCategory.SaveableComponent, LogLevel.Warning);
                                return;
                        }

                        try
                        {
#if CRYSTALSAVE_TIMEMACHINE
                                // Try to deserialize as wrapper first (if it has TimeMachine data)
                                byte[] componentData = data;
                                
                                if (ShouldPersistTimeMachineSnapshots)
                                {
                                        try
                                        {
                                                var wrapper = MemoryPackSerializer.Deserialize<ComponentSaveDataWrapper>(data);
                                                if (wrapper != null)
                                                {
                                                        componentData = wrapper.componentData;
                                                        
                                                        // Restore TimeMachine data
                                                        if (wrapper.timeMachineData != null)
                                                        {
                                                                RestoreTimeMachineData(wrapper.timeMachineData);
                                                        }
                                                }
                                        }
                                        catch
                                        {
                                                // If deserialization as wrapper fails, treat as raw component data
                                                componentData = data;
                                        }
                                }
                                
                                // Deserialize component-specific data
                                DeserializeComponentData(componentData);
#else
                                // Deserialize component-specific data
                                DeserializeComponentData(data);
#endif
                        }
                        catch (MissingReferenceException)
                        {
                                Logger.Log("LoadData skipped: component reference became invalid during restore.", LogCategory.SaveableComponent, LogLevel.Warning);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"LoadData encountered an error: {ex.Message}", LogCategory.SaveableComponent, LogLevel.Error);
                        }
		}

		protected abstract byte[] SerializeComponentData();
		protected abstract void DeserializeComponentData(byte[] data);

#if CRYSTALSAVE_TIMEMACHINE
                /// <summary>
                /// Captures TimeMachine timeline data for this GameObject.
                /// </summary>
                private TimelineSaveData CaptureTimeMachineData()
                {
                        var timeMachine = GameObjectTimeMachine.Instance;
                        if (timeMachine == null || gameObject == null)
                                return null;

                        // Get effective duration (from override or global settings)
                        float duration = overrideMaxSaveDuration ? maxSaveDuration : SaveManager.Instance.SaveSettings.timeMachineMaxSaveDuration;
                        
                        // Check if we should save snapshots
                        if (duration == 0f)
                                return null; // Don't save any snapshots

                        var saveData = new TimelineSaveData
                        {
                                activeBranchName = timeMachine.GetActiveBranchName(),
                                recordingSpeed = timeMachine.GetPlaybackSpeed(),
                                isPlaying = timeMachine.IsPlaying
                        };

                        // Get all branch names
                        var branchNames = timeMachine.GetAllBranchNames();

                        foreach (var branchName in branchNames)
                        {
                                // Get timeline for this GameObject on this branch
                                var timeline = timeMachine.GetTimelineForObject(gameObject, branchName);
                                if (timeline == null || timeline.Count == 0)
                                        continue;

                                // Filter snapshots by duration
                                var filteredSnapshots = FilterSnapshotsByDuration(timeline, duration);
                                
                                // Convert to serializable snapshots
                                var serializableSnapshots = filteredSnapshots
                                        .Select(SerializableSnapshot.FromGameObjectSnapshot)
                                        .Where(s => s != null)
                                        .ToList();

                                if (serializableSnapshots.Count == 0)
                                        continue;

                                // Get branch metadata
                                var (sourceBranch, branchPointTime) = timeMachine.GetBranchMetadata(branchName);

                                var branchData = new BranchSaveData
                                {
                                        branchName = branchName,
                                        snapshots = serializableSnapshots,
                                        sourceBranchName = sourceBranch,
                                        branchPointTime = branchPointTime
                                };

                                saveData.branches.Add(branchData);
                        }

                        return saveData.branches.Count > 0 ? saveData : null;
                }

                /// <summary>
                /// Filters snapshots to keep only those within the specified duration window.
                /// </summary>
                private List<GameObjectSnapshot> FilterSnapshotsByDuration(List<GameObjectSnapshot> timeline, float duration)
                {
                        if (timeline == null || timeline.Count == 0)
                                return new List<GameObjectSnapshot>();

                        // If duration is -1, keep entire timeline (unlimited)
                        if (duration < 0f)
                                return new List<GameObjectSnapshot>(timeline);

                        // Find the latest timestamp
                        float latestTime = timeline.Max(s => s.Timestamp);
                        float cutoffTime = latestTime - duration;

                        // Filter snapshots within duration window
                        return timeline.Where(s => s.Timestamp >= cutoffTime).ToList();
                }

                /// <summary>
                /// Restores TimeMachine timeline data for this GameObject.
                /// </summary>
                private void RestoreTimeMachineData(TimelineSaveData saveData)
                {
                        if (saveData == null || gameObject == null)
                                return;

                        var timeMachine = GameObjectTimeMachine.Instance;
                        if (timeMachine == null)
                                return;

                        // Clear existing timelines for this GameObject
                        timeMachine.ClearTimelinesForObject(gameObject);

                        // Restore each branch
                        foreach (var branchData in saveData.branches)
                        {
                                if (branchData.snapshots == null || branchData.snapshots.Count == 0)
                                        continue;

                                // Convert serializable snapshots back to GameObjectSnapshots
                                foreach (var serializableSnapshot in branchData.snapshots)
                                {
                                        var snapshot = serializableSnapshot.ToGameObjectSnapshot();
                                        timeMachine.InjectSnapshot(gameObject, snapshot, branchData.branchName);
                                }

                                // Restore branch metadata
                                if (!string.IsNullOrEmpty(branchData.sourceBranchName))
                                {
                                        timeMachine.SetBranchMetadata(
                                                branchData.branchName,
                                                branchData.sourceBranchName,
                                                branchData.branchPointTime
                                        );
                                }
                        }

                        // Restore playback state
                        if (saveData.isPlaying)
                        {
                                timeMachine.SetPlaybackSpeed(saveData.recordingSpeed);
                                // Note: Don't auto-start playback, let the user control that
                        }
                }

                /// <summary>
                /// Detects if SaveData() is being called from TimeMachine recording operations
                /// by analyzing the call stack. This prevents expensive persistence operations
                /// during runtime recording.
                /// </summary>
                private bool IsCalledFromTimeMachineRecording()
                {
                        try
                        {
                                var stackTrace = new System.Diagnostics.StackTrace();
                                for (int i = 0; i < stackTrace.FrameCount; i++)
                                {
                                        var method = stackTrace.GetFrame(i)?.GetMethod();
                                        if (method?.DeclaringType?.Name == "ComponentManager" && 
                                            method.Name == "CollectComponentDataForTimeMachine")
                                        {
                                                return true;
                                        }
                                }
                        }
                        catch
                        {
                                // If stack trace analysis fails, err on the side of caution
                                // and assume it's a persistence call to maintain data integrity
                        }
                        
                        return false;
                }
#endif

		/*────────────────────────  API for RememberComposite  ────────────────────────*/
		/// <summary>
		/// Called by <see cref="RememberComposite"/> right after the component is
		/// auto-added.  Hides the entry in the Inspector, but leaves the script active
		/// so callbacks (e.g. OnTransformParentChanged) work instantly in both
		/// Edit-mode and Play-mode.
		/// </summary>
		internal void MarkHiddenByComposite()
		{
			_hiddenByComposite = true;

			// Keep it out of the regular Inspector **but leave it editable**
			hideFlags |= HideFlags.HideInInspector;
			hideFlags &= ~HideFlags.NotEditable;      // guarantee writable

			enabled = true;          // remain active in Edit-mode, Play-mode, and builds
		}


#if UNITY_EDITOR
		private void OnValidate()
		{
			if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

			// Don't add components during OnValidate. Only synchronize if one already exists.
			var uid = GetComponent<UniqueID>();
			if (uid != null)
			{
				if (!string.IsNullOrEmpty(uniqueID) && uid.ID != uniqueID)
				{
					uid.ID = uniqueID;
					UnityEditor.EditorUtility.SetDirty(uid);
				}
				else if (string.IsNullOrEmpty(uniqueID) && !string.IsNullOrEmpty(uid.ID))
				{
					uniqueID = uid.ID;
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
			else
			{
				// Ensure we at least have a stable serialized ID; the UniqueID component will
				// be created and synchronized at runtime in Awake.
				if (string.IsNullOrEmpty(uniqueID))
				{
					uniqueID = Guid.NewGuid().ToString("N");
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}

			/* 1️⃣  componentID uniqueness */
			if (string.IsNullOrEmpty(componentID))
				componentID = Guid.NewGuid().ToString("N");

			foreach (var sib in GetComponents<SaveableComponent>())
				if (sib != this && sib.componentID == componentID)
					componentID = Guid.NewGuid().ToString("N");

			/* 2️⃣  Inspector visibility honouring the composite flag */
			if (_hiddenByComposite)
			{
				hideFlags |= HideFlags.HideInInspector;
				hideFlags &= ~HideFlags.NotEditable;   // make sure it stays editable
			}
			else hideFlags &= ~HideFlags.HideInInspector;
		}
                private void OnTransformParentChanged()
                {
                        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

                        var uid = UniqueIDUtility.EnsureComponent(gameObject);
                        if (uid != null && uid.ID != uniqueID)
                        {
                                OverrideGameObjectID(uid.ID);
                                UnityEditor.EditorUtility.SetDirty(this);
                                UnityEditor.EditorUtility.SetDirty(uid);
                        }
                }

#endif

		#region internal helpers
		/// <summary>
		/// Called by <see cref="SaveablePrefab"/> after it has determined the
		/// final UniqueID.  Updates our composite identifier and refreshes the
		/// registration inside <see cref="ComponentManager"/>.
		/// </summary>
		internal void OverrideGameObjectID(string newGOid)
		{
			/* ── 0) sanity ───────────────────────────────────────────── */
			if (string.IsNullOrEmpty(newGOid)) return;

			/* ── 1) rebuild the composite key ──────────────────────────
			 * uniqueID format is  <GameObjectGUID>_<ComponentPart>
			 * We keep the right-hand part and replace only the GO-part. */
			string componentPart = null;
			if (!string.IsNullOrEmpty(uniqueID))
			{
				var parts = uniqueID.Split('_');
				if (parts.Length == 2)
					componentPart = parts[1];
			}

			string newCompositeID = componentPart == null
				? newGOid                       // fallback – shouldn’t happen
				: $"{newGOid}_{componentPart}";

			if (newCompositeID == uniqueID) return;   // nothing to do

			/* 2️⃣ unregister old key if we were registered */
			if (_isRegistered && SaveManager.Instance != null)
			{
				SaveManager.Instance.ComponentManager.UnregisterSaveableComponent(this);
				_isRegistered = false;
			}

			/* 3️⃣ adopt new ID */
			uniqueID = newCompositeID;

			if (TryGetComponent(out UniqueID uidComp) && GetComponent<SaveablePrefab>() != null)
				uidComp.ID = newGOid;

			/* 4️⃣ register once under the new key */
			if (enabled)
			{
				RegisterWithSaveManager();               // ← does the single registration
			}
			else
			{
				// Disabled components never enter OnEnable, so register them here
				if (SaveManager.Instance != null)
				{
					SaveManager.Instance.ComponentManager.RegisterSaveableComponent(this);
					_isRegistered = true;
				}
			}
		}

#if CRYSTALSAVE_TIMEMACHINE
		/// <summary>
		/// Ensures a single TimeMachineRecorder exists on the GameObject.
		/// When multiple SaveableComponents enable TimeMachine recording,
		/// they share settings from the first enabled component.
		/// </summary>
	private void EnsureTimeMachineRecorder()
	{
		// Special case: ParticleSystems also need ParticleRewinder for playback control
		if (this is Arawn.CrystalSave.Runtime.RememberParticleSystem)
		{
			EnsureParticleRewinder();
			// Continue to add TimeMachineRecorder below (needed for branching timelines)
		}

		// Check if a TimeMachineRecorder already exists
		var recorder = GetComponent<Arawn.CrystalSave.Runtime.TimeMachine.TimeMachineRecorder>();
		
		if (recorder == null)
		{
			// Add the recorder component
			recorder = gameObject.AddComponent<Arawn.CrystalSave.Runtime.TimeMachine.TimeMachineRecorder>();
			
			// Configure with this component's settings (first component wins)
			ConfigureTimeMachineRecorder(recorder);
			
			Logger.Log($"[SaveableComponent] Auto-added TimeMachineRecorder to '{gameObject.name}' from component '{GetType().Name}'", LogCategory.SaveableComponent, LogLevel.Info);
		}
		else
		{
			// Recorder already exists (added by another SaveableComponent)
			// We could optionally validate settings match, but for now just log
			Logger.Log($"[SaveableComponent] TimeMachineRecorder already exists on '{gameObject.name}', component '{GetType().Name}' will share it", LogCategory.SaveableComponent, LogLevel.Info);
		}
	}		/// <summary>
		/// Ensures ParticleRewinder component exists (for RememberParticleSystem).
		/// ParticleSystems need simulation-based playback, not snapshot interpolation.
		/// </summary>
		private void EnsureParticleRewinder()
		{
			var rewinder = GetComponent<Arawn.CrystalSave.Runtime.TimeMachine.ParticleRewinder>();
			
			if (rewinder == null)
			{
				rewinder = gameObject.AddComponent<Arawn.CrystalSave.Runtime.TimeMachine.ParticleRewinder>();
				Logger.Log($"[SaveableComponent] Auto-added ParticleRewinder to '{gameObject.name}' for RememberParticleSystem", LogCategory.SaveableComponent, LogLevel.Info);
			}
			else
			{
				Logger.Log($"[SaveableComponent] ParticleRewinder already exists on '{gameObject.name}'", LogCategory.SaveableComponent, LogLevel.Info);
			}
		}

		/// <summary>
		/// Configures a TimeMachineRecorder with settings from this SaveableComponent.
		/// Uses reflection to set the private fields.
		/// </summary>
		private void ConfigureTimeMachineRecorder(Arawn.CrystalSave.Runtime.TimeMachine.TimeMachineRecorder recorder)
		{
			if (recorder == null) return;

			var recorderType = recorder.GetType();

			// Set autoStartRecording
			var autoStartField = recorderType.GetField("autoStartRecording", 
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (autoStartField != null)
				autoStartField.SetValue(recorder, autoStartRecording);

			// Set useInterval
			var useIntervalField = recorderType.GetField("useInterval",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (useIntervalField != null)
				useIntervalField.SetValue(recorder, useInterval);

			// Set snapshotInterval
			var intervalField = recorderType.GetField("snapshotInterval",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (intervalField != null)
				intervalField.SetValue(recorder, snapshotInterval);

			// Set maxSnapshots
			var maxSnapshotsField = recorderType.GetField("maxSnapshots",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (maxSnapshotsField != null)
				maxSnapshotsField.SetValue(recorder, maxSnapshots);

			// Set isRecording to true
			recorder.isRecording = true;

			Logger.Log($"[SaveableComponent] Configured TimeMachineRecorder on '{gameObject.name}' with settings: autoStart={autoStartRecording}, interval={useInterval}, snapshotInterval={snapshotInterval}s, maxSnapshots={maxSnapshots}", LogCategory.SaveableComponent, LogLevel.Info);
		}
#endif

		#endregion
	}
}
#endif
