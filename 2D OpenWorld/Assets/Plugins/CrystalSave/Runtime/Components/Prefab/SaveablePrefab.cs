#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[DefaultExecutionOrder(-100)]
	[AddComponentMenu("Crystal Save/Prefabs/Remember Prefab")]
	[Icon("Assets/Plugins/CrystalSave/Editor/Gizmos/SaveablePrefab.png")]
	[DisallowMultipleComponent]
	public partial class SaveablePrefab : MonoBehaviour
	{
       [Tooltip("Cache this SaveablePrefab and its IDs while enabled to avoid repeated GetComponent calls. Caches are cleared on disable or destroy.")]
       [SerializeField] private bool enablePerformanceCaching = true;

	[Tooltip("If ON, SaveManager registers this prefab's subtree in batches to reduce spikes. Note: in rare load scenarios with ordering-sensitive restores (e.g., a child expecting its parent or cross-referenced object to be registered first), the frame-sliced registration can briefly delay lookups until the next frame.")]
	[SerializeField] private bool enableBatchRegistration = false;

       /// <summary>
       /// When <c>true</c>, this prefab stores references and identifiers in static dictionaries
       /// so external systems can perform fast lookups without additional GetComponent calls.
       /// </summary>
       public bool EnablePerformanceCaching => enablePerformanceCaching;
       public bool EnableBatchRegistration => enableBatchRegistration;

       //──────────────────────────────────────────────────────────────
       // Static caches to reduce repeated GetComponent lookups
       //──────────────────────────────────────────────────────────────
       private static readonly Dictionary<int, SaveablePrefab> s_PrefabCache = new();
       private static readonly Dictionary<int, string> s_UniqueIdCache = new();
       private static readonly Dictionary<int, string> s_AssetIdCache = new();

       private void CacheSelfIfEnabled()
       {
               if (!enablePerformanceCaching) return;
               int id = UnityObjectHelper.GetUniqueId(gameObject);
               s_PrefabCache[id] = this;
               if (!string.IsNullOrEmpty(uniqueID)) s_UniqueIdCache[id] = uniqueID;
               if (!string.IsNullOrEmpty(prefabAssetID)) s_AssetIdCache[id] = prefabAssetID;
       }

       private void RemoveFromCaches()
       {
               int id = UnityObjectHelper.GetUniqueId(gameObject);
               s_PrefabCache.Remove(id);
               s_UniqueIdCache.Remove(id);
               s_AssetIdCache.Remove(id);
       }

       /// <summary>
       /// Try to get a cached <see cref="SaveablePrefab"/> for a <see cref="GameObject"/>.
       /// </summary>
       public static bool TryGetCachedSaveablePrefab(GameObject go, out SaveablePrefab comp)
       {
               comp = null;
               if (go == null) return false;
               return s_PrefabCache.TryGetValue(UnityObjectHelper.GetUniqueId(go), out comp) && comp != null;
       }

       /// <summary>
       /// Try to get a cached instance UniqueID for a <see cref="GameObject"/>.
       /// </summary>
       public static bool TryGetCachedUniqueID(GameObject go, out string id)
       {
               id = null;
               if (go == null) return false;
               return s_UniqueIdCache.TryGetValue(UnityObjectHelper.GetUniqueId(go), out id) && !string.IsNullOrEmpty(id);
       }

       /// <summary>
       /// Try to get a cached PrefabAssetID for a <see cref="GameObject"/>.
       /// </summary>
       public static bool TryGetCachedPrefabAssetID(GameObject go, out string id)
       {
               id = null;
               if (go == null) return false;
               return s_AssetIdCache.TryGetValue(UnityObjectHelper.GetUniqueId(go), out id) && !string.IsNullOrEmpty(id);
       }
		HashSet<string> _removedPaths = new HashSet<string>();

                [SerializeField]
                [Tooltip(
                        "Record SkinnedMeshRenderer.sharedMesh and MeshRenderer swaps that happen at *runtime* on " +
                        "this prefab instance. Keep in mind to place the existing and replacing meshes into a Resource folder if you enable this feature!  Leave off to keep save-files smaller.")]
                private bool trackSkinnedMeshOverrides = false;

                [SerializeField]
                [Tooltip(
                        "If ON, capture blendshape weight changes on SkinnedMeshRenderers beneath this prefab " +
                        "(root + children). Leave OFF unless you animate blendshapes at runtime to keep save-files lean.")]
                private bool trackBlendshapeOverrides = false;

		[SerializeField, Tooltip(
		"If ON, capture runtime texture changes on all Renderers beneath this prefab " +
		"(root + children). Useful for character customization, material swaps, etc. Leave OFF to keep save-files lean.")]
		private bool trackTextureOverrides = false;

		[SerializeField, Tooltip(
		"If ON, this prefab records per-slot Material changes on any Renderer " +
		"beneath it during play – useful for palette swaps, damage shaders, etc.")]
		bool trackMaterialOverrides = false;

		[SerializeField, Tooltip(
		"If ON, record components that were ADDed to this prefab instance at runtime " +
		"(handy for visual-scripting or power-ups).")]
		bool trackAddedComponents = false;

		[SerializeField, Tooltip(
		"If ON, snapshot ParticleSystem time / playing-state so effects resume " +
		"exactly where they left off after loading.")]
		bool trackParticleSnapshots = false;

		[SerializeField, Tooltip(
    	"If ON, persist child-object active/tag/layer changes and creation/destruction.")]
                bool trackChildStateOverrides = true;

                [SerializeField, Tooltip(
        "If ON, store and restore local position, rotation and scale for all child objects.")]
                bool trackChildTransformOverrides = false;

                [SerializeField, Tooltip(
        "If ON, store arbitrary data blobs emitted by ISaveable helpers attached " +
        "to this prefab (custom scripts, health, etc.).")]
                bool trackComponentBlobs = false;

                [SerializeField, Tooltip(
        "If ON, persist collider enabled/trigger state and dimensions for all colliders under this prefab.")]
                bool trackColliderSettings = false;

                [Header("Save Optimization")]
                [SerializeField, Tooltip("When enabled, scene-placed prefabs skip emitting save data when their core state matches the initial snapshot.")]
                private bool skipSavingWhenUnchanged = false;

                private PrefabSnapshot? baselineSnapshot;
                private PrefabSnapshot? lastSnapshot;

                [Header("Load Scheduling")]
                [SerializeField, Tooltip("Higher priorities (closer to 100) are restored during the initial load batch. Lower values can be deferred for streaming or progressive reveal.")]
                [Range(0, 100)]
                int loadPriority = 50;

                [SerializeField, Tooltip("When enabled, this prefab skips the main restoration pass and stays queued until gameplay explicitly requests deferred prefabs (e.g., when the player approaches the area).")]
                bool deferLowPriorityUntilRequested = false;

                [Header("Pooling Settings")]
                [SerializeField, Tooltip("If enabled, this prefab will never use pooling even if global pooling is enabled. Always takes precedence over global pooling settings.")]
                private bool disablePooling = false;

                /// <summary>Track changes to SkinnedMeshRenderer mesh swaps.</summary>
                public bool TrackSkinnedMeshOverrides
                {
                        get => trackSkinnedMeshOverrides;
                        set => trackSkinnedMeshOverrides = value;
                }

                /// <summary>Track BlendShape weight changes on SkinnedMeshRenderers.</summary>
                public bool TrackBlendshapeOverrides
                {
                        get => trackBlendshapeOverrides;
                        set => trackBlendshapeOverrides = value;
                }

                /// <summary>Track texture property overrides on materials.</summary>
                public bool TrackTextureOverrides
                {
                        get => trackTextureOverrides;
                        set => trackTextureOverrides = value;
                }

                /// <summary>Track per-slot Material changes on Renderers.</summary>
                public bool TrackMaterialOverrides
                {
                        get => trackMaterialOverrides;
                        set => trackMaterialOverrides = value;
                }

                /// <summary>
                /// Track components that were added to this prefab instance at runtime.
                /// Essential for Scenario 2 (truly procedural GameObjects).
                /// </summary>
                public bool TrackAddedComponents
                {
                        get => trackAddedComponents;
                        set => trackAddedComponents = value;
                }

                /// <summary>Track ParticleSystem time and playing state.</summary>
                public bool TrackParticleSnapshots
                {
                        get => trackParticleSnapshots;
                        set => trackParticleSnapshots = value;
                }

                /// <summary>Track child object active/tag/layer changes and creation/destruction.</summary>
                public bool TrackChildStateOverrides
                {
                        get => trackChildStateOverrides;
                        set => trackChildStateOverrides = value;
                }

                /// <summary>Track local position, rotation, and scale for all child objects.</summary>
                public bool TrackChildTransformOverrides
                {
                        get => trackChildTransformOverrides;
                        set => trackChildTransformOverrides = value;
                }

                /// <summary>
                /// Track arbitrary data blobs emitted by ISaveable helpers attached to this prefab.
                /// Essential for Scenario 2 (truly procedural GameObjects) when custom component data must persist.
                /// </summary>
                public bool TrackComponentBlobs
                {
                        get => trackComponentBlobs;
                        set => trackComponentBlobs = value;
                }

                /// <summary>Track collider enabled/trigger state and dimensions.</summary>
                public bool TrackColliderSettings
                {
                        get => trackColliderSettings;
                        set => trackColliderSettings = value;
                }

                /// <summary>
                /// Priority used to sort prefabs during the initial restoration pass. Values are clamped between 0 and 100.
                /// </summary>
                public int LoadPriority
                {
                        get => loadPriority;
                        set => loadPriority = Mathf.Clamp(value, 0, 100);
                }

                /// <summary>
                /// When <c>true</c>, this prefab is staged for deferred instantiation and must be restored through
                /// <see cref="PrefabManager.ProcessDeferredPrefabs()"/> or <see cref="PrefabManager.ProcessDeferredPrefabsForScene(string)"/>.
                /// </summary>
                public bool DeferLowPriorityUntilRequested
                {
                        get => deferLowPriorityUntilRequested;
                        set => deferLowPriorityUntilRequested = value;
                }

                /// <summary>
                /// When <c>true</c>, this prefab will never use pooling even if global pooling is enabled.
                /// This setting always takes precedence over global pooling settings and overrides.
                /// </summary>
                public bool DisablePooling
                {
                        get => disablePooling;
                        set => disablePooling = value;
                }

		/* Serialized identity */
		[SerializeField, Tooltip("Unique identifier for this prefab instance.")]
		private string uniqueID;

		[SerializeField, Tooltip("Unique identifier of the prefab asset.")]
		private string prefabAssetID;

		/* Scene persistence */
                [SerializeField, Tooltip("If true, this GameObject is preserved across scene loads")]
                private bool keepAcrossScenes;

                public bool KeepAcrossScenes
                {
                        get => keepAcrossScenes;
                        set => keepAcrossScenes = value;
                }

                [SerializeField, Tooltip(
                        "Apply saved component data when this prefab is restored after being destroyed.")]
                private bool applySavedComponentDataOnRespawn = false;

                public bool ApplySavedComponentDataOnRespawn
                {
                        get => applySavedComponentDataOnRespawn;
                        set => applySavedComponentDataOnRespawn = value;
                }

                [SerializeField]
                private bool reuseSceneInstanceOnLoad = false;

#pragma warning disable CS0414 // Field is assigned but its value is never used - used in editor-only code in SaveablePrefab.Identity.cs
                [SerializeField, HideInInspector]
                private bool autoEnforceReuseSceneInstanceOnLoad = true;
#pragma warning restore CS0414

                public bool ReuseSceneInstanceOnLoad
                {
                        get => reuseSceneInstanceOnLoad;
                        set => reuseSceneInstanceOnLoad = value;
                }

                [SerializeField, Tooltip("If true, prefab is restored in its original scene")]
                private bool rememberHomeScene = false;

                public enum HomeSceneMode
                {
                        InstantiationScene,
                        LastSnapshotScene
                }

                [SerializeField, Tooltip("Which scene name to store when remembering the home scene")]
                private HomeSceneMode homeSceneCaptureMode = HomeSceneMode.InstantiationScene;

                [SerializeField, HideInInspector]
                private string homeScene;

                public bool RememberHomeScene
                {
                        get => rememberHomeScene;
                        set => rememberHomeScene = value;
                }

                public HomeSceneMode HomeSceneCaptureMode
                {
                        get => homeSceneCaptureMode;
                        set => homeSceneCaptureMode = value;
                }

                public string HomeScene => homeScene;

                public void SetHomeScene(string sceneName) => homeScene = sceneName;

		/* Visibility / off-screen handling */
		[Header("Visibility Settings")]
		[SerializeField] public List<string> VisibleInScenes = new();

		[SerializeField, Tooltip("Behaviour when prefab is not in its home scene")]
		private OffScreenDeactivation offScreenMask =
			  OffScreenDeactivation.Colliders |
			  OffScreenDeactivation.Renderers |
			  OffScreenDeactivation.RigidbodyKinematic;

		// Back-compat wrappers (were public fields)
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

                /* Save-system integration flags */
                [Tooltip("If unchecked, this prefab will NOT register with the Save System.")]
                public bool RegisterWithSaveSystem = true;

	[SerializeField, Tooltip(
	"If ON and this object is placed in the scene (not instantiated at runtime), " +
	"an Instance Unique ID is generated at runtime when Register With Save System is OFF and the ID is empty. " +
	"Turn OFF to keep the ID empty in that scenario.")]
	private bool autoAssignInstanceIDWhenUnregistered = true;

	public bool AutoAssignInstanceIDWhenUnregistered
	{
		get => autoAssignInstanceIDWhenUnregistered;
		set => autoAssignInstanceIDWhenUnregistered = value;
	}

        [Header("GameObject Properties to Save")]
        [SerializeField]
        private GameObjectPropertySettings propertySettings = new GameObjectPropertySettings
        {
                RememberActive = true,
                RememberName   = true,
                RememberTag    = true,
                RememberLayer  = true,
                // Allow destroyed prefab instances to be restored by default
                RememberDestroyed = true
        };
        public GameObjectPropertySettings PropertySettings => propertySettings;

#if CRYSTALSAVE_TIMEMACHINE
		[Header("Time Machine Recording")]
		[SerializeField, Tooltip("Enable TimeMachine recording for this prefab instance. Automatically adds/configures a TimeMachineRecorder component.")]
		private bool enableTimeMachineRecording = false;

		[SerializeField, Tooltip("Automatically start recording when the prefab enables.")]
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
					Logger.Log($"[{gameObject.name}] Cannot enable Save Time Machine Snapshots when Enable Time Machine Recording is false.", LogCategory.SaveablePrefab, LogLevel.Warning);
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

		/* Runtime bookkeeping (not serialized) */
        private bool isRegisteredWithSaveManager;
        private bool isLoading;
        private GameObjectPropertySettings settings;
        private PersistentVisibilityController visibilityController;

        /* Baseline for name/tag/layer tracking */
        private string initialName;
        private string initialTag;
        private int    initialLayer;
        private bool   initialActiveState;

		/* Runtime-added component support */
		[SerializeField, HideInInspector] private bool isAddedAtRuntime;
		public bool IsAddedAtRuntime => isAddedAtRuntime;

		/* Original prefab asset reference */
		[SerializeField] private GameObject originalPrefabAsset;
		public GameObject OriginalPrefabAsset => originalPrefabAsset;

		/* Public identity accessors */
		public string UniqueID => uniqueID;
                public string PrefabAssetID
                {
                        get => prefabAssetID;
                        set
                        {
                                if (!string.IsNullOrEmpty(prefabAssetID)) return;
                                prefabAssetID = value;
                                CacheSelfIfEnabled();
                        }
                }

                public void NotifyChildDestroyed(string localPath)   // called by DestroyObserver
                {
                        _removedPaths.Add(localPath);
                }

                internal bool TryGetLastSnapshot(out PrefabSnapshot snapshot)
                {
                        if (lastSnapshot.HasValue)
                        {
                                snapshot = lastSnapshot.Value;
                                return true;
                        }

                        snapshot = default;
                        return false;
                }

                internal void RefreshOptimizationBaseline()
                {
                        if (!skipSavingWhenUnchanged || isAddedAtRuntime)
                                return;

                        if (TryCaptureCurrentState(out var current))
                        {
                                baselineSnapshot = current;
                                lastSnapshot     = current;
                        }
                }

                public bool TryCaptureCurrentState(out PrefabSnapshot snapshot)
                {
                        var tr = transform;

                        snapshot = new PrefabSnapshot
                        {
                                WorldPosition       = tr.position,
                                WorldRotation       = tr.rotation,
                                LocalPosition       = tr.localPosition,
                                LocalRotation       = tr.localRotation,
                                LocalScale          = tr.localScale,
                                ParentID            = ResolveParentIdentifier(tr.parent, out bool isSceneObj, out bool hasParent),
                                IsParentSceneObject = isSceneObj,
                                HasParent           = hasParent,
                                ActiveSelf          = gameObject.activeSelf,
                                Name                = gameObject.name,
                                Tag                 = gameObject.tag,
                                Layer               = gameObject.layer
                        };

                        return true;
                }

                private static string ResolveParentIdentifier(Transform parent, out bool isSceneObject, out bool hasParent)
                {
                        isSceneObject = false;
                        hasParent     = parent != null;
                        if (parent == null)
                                return null;

                        if (parent.TryGetComponent(out SaveablePrefab parentSaveable) && parentSaveable != null)
                        {
                                return parentSaveable.UniqueID;
                        }

                        if (parent.TryGetComponent(out SceneObjectID sceneObjectID) && sceneObjectID != null)
                        {
                                isSceneObject = true;
                                return sceneObjectID.UniqueID;
                        }

                        if (parent.TryGetComponent(out UniqueID uid) && uid != null)
                        {
                                isSceneObject = true;
                                return uid.ID;
                        }

                        isSceneObject = false;
                        return null;
                }

                public static bool AreEquivalent(PrefabSnapshot a, PrefabSnapshot b)
                {
                        if (a.HasParent != b.HasParent)
                                return false;

                        if (!string.Equals(a.ParentID, b.ParentID, StringComparison.Ordinal))
                                return false;

                        if (a.IsParentSceneObject != b.IsParentSceneObject)
                                return false;

                        if (a.HasParent)
                        {
                                if (!Approximately(a.LocalPosition, b.LocalPosition))
                                        return false;

                                if (!Approximately(a.LocalRotation, b.LocalRotation))
                                        return false;
                        }
                        else
                        {
                                if (!Approximately(a.WorldPosition, b.WorldPosition))
                                        return false;

                                if (!Approximately(a.WorldRotation, b.WorldRotation))
                                        return false;
                        }

                        if (!Approximately(a.LocalScale, b.LocalScale))
                                return false;

                        if (a.ActiveSelf != b.ActiveSelf)
                                return false;

                        if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal))
                                return false;

                        if (!string.Equals(a.Tag, b.Tag, StringComparison.Ordinal))
                                return false;

                        if (a.Layer != b.Layer)
                                return false;

                        return true;
                }

                private static bool Approximately(Vector3 a, Vector3 b, float tolerance = 0.0001f)
                {
                        return (a - b).sqrMagnitude <= tolerance * tolerance;
                }

                private static bool Approximately(Quaternion a, Quaternion b, float toleranceDegrees = 0.1f)
                {
                        return Quaternion.Angle(a, b) <= toleranceDegrees;
                }

                internal PrefabSnapshot? GetOptimizationBaseline() => baselineSnapshot;
                internal bool HasOptimizationBaseline => baselineSnapshot.HasValue;
                public bool SkipSavingWhenUnchanged => skipSavingWhenUnchanged;

                public struct PrefabSnapshot
                {
                        public Vector3 WorldPosition;
                        public Quaternion WorldRotation;
                        public Vector3 LocalPosition;
                        public Quaternion LocalRotation;
                        public Vector3 LocalScale;
                        public string ParentID;
                        public bool HasParent;
                        public bool IsParentSceneObject;
                        public bool ActiveSelf;
                        public string Name;
                        public string Tag;
                        public int Layer;
                }

		// inside SaveablePrefab
		void AttachDestroyObserversRecursively(Transform root)
		{
			// Safety guard – the prefab may be disabled when Awake() runs
			if (root == null) return;

			foreach (Transform child in root)
			{
				/* 1️⃣  Skip self-contained SaveablePrefabs.
				*     They take care of their own hierarchy tracking.                */
				if (child.GetComponent<SaveablePrefab>() != null)
					continue;

				/* 2️⃣  Build a stable, slash-separated path relative to *this* prefab.
				*     Example:  "Arm/Hand/Index"                                     */
				string path = SaveablePrefabUtil.GetHierarchyPath(child, transform);

				/* 3️⃣  Add (or reuse) a tiny observer component so we learn when the
				*     object disappears at runtime.  Keep it hidden in the Inspector. */
				var obs = child.GetComponent<DestroyObserver>() ??
						child.gameObject.AddComponent<DestroyObserver>();

				obs.hideFlags = HideFlags.HideInInspector;
				obs.Owner     = this;
				obs.LocalPath = path;

				/* 4️⃣  Dive deeper – depth-first so grandchildren are covered too.    */
				AttachDestroyObserversRecursively(child);
			}
		}

                internal static string BuildStableHierarchyKey(SaveablePrefab sp)
                {
                        if (sp == null) return string.Empty;
                        var t = sp.transform;
                        var segments = new System.Collections.Generic.List<string>(8);
                        while (t != null)
                        {
                                segments.Add(t.name);
                                t = t.parent;
                        }
                        segments.Reverse();
                        return string.Join("/", segments);
                }

		/// <summary>
		/// Synchronizes the DisablePooling setting from the PrefabRegistry to this component.
		/// This helps ensure that existing registry settings are reflected on the component.
		/// </summary>
		public void SyncPoolingSettingFromRegistry()
		{
			try
			{
				var registry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
				if (registry != null && !string.IsNullOrEmpty(PrefabAssetID))
				{
				bool registryDisablesPooling = registry.IsPoolingDisabled(PrefabAssetID);
				if (registryDisablesPooling && !disablePooling)
				{
					disablePooling = true;
					Logger.Log($"SaveablePrefab: Synced DisablePooling=true from PrefabRegistry for '{name}'.", LogCategory.SaveablePrefab, LogLevel.Info);
				}
			}
		}
		catch (System.Exception ex)
		{
			Logger.Log($"SaveablePrefab: Failed to sync pooling setting from registry: {ex.Message}", LogCategory.SaveablePrefab, LogLevel.Warning);
		}
	}

#if CRYSTALSAVE_TIMEMACHINE
		/// <summary>
		/// Ensures a single TimeMachineRecorder exists on the GameObject.
		/// Configures it with this SaveablePrefab's settings.
		/// </summary>
		private void EnsureTimeMachineRecorder()
		{
		// Check if GameObjectTimeMachine exists in the scene
		if (!Arawn.CrystalSave.Runtime.TimeMachine.GameObjectTimeMachine.IsInitialized)
		{
			Logger.Log($"[SaveablePrefab] Cannot add TimeMachineRecorder to '{gameObject.name}' because GameObjectTimeMachine is not in the scene. " +
				"Please add a GameObjectTimeMachine component to your scene to use TimeMachine recording features.", LogCategory.SaveablePrefab, LogLevel.Warning);
			return;
		}			// Check if a TimeMachineRecorder already exists
			var recorder = GetComponent<Arawn.CrystalSave.Runtime.TimeMachine.TimeMachineRecorder>();
			
			if (recorder == null)
			{
			// Add the recorder component
			recorder = gameObject.AddComponent<Arawn.CrystalSave.Runtime.TimeMachine.TimeMachineRecorder>();
			
			// Configure with this prefab's settings
			ConfigureTimeMachineRecorder(recorder);
			
			Logger.Log($"[SaveablePrefab] Auto-added TimeMachineRecorder to prefab '{gameObject.name}'", LogCategory.SaveablePrefab, LogLevel.Info);
		}
		else
		{
			// Recorder already exists (maybe added manually or by a SaveableComponent)
			Logger.Log($"[SaveablePrefab] TimeMachineRecorder already exists on '{gameObject.name}', will use existing instance", LogCategory.SaveablePrefab, LogLevel.Info);
		}
	}		/// <summary>
		/// Configures a TimeMachineRecorder with settings from this SaveablePrefab.
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
			var snapshotIntervalField = recorderType.GetField("snapshotInterval",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (snapshotIntervalField != null)
				snapshotIntervalField.SetValue(recorder, snapshotInterval);

			// Set maxSnapshots
			var maxSnapshotsField = recorderType.GetField("maxSnapshots",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (maxSnapshotsField != null)
				maxSnapshotsField.SetValue(recorder, maxSnapshots);

			// Set isRecording (public field)
			var isRecordingField = recorderType.GetField("isRecording",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			if (isRecordingField != null)
				isRecordingField.SetValue(recorder, autoStartRecording);

			Logger.Log($"[SaveablePrefab] Configured TimeMachineRecorder for '{gameObject.name}' " +
				$"(autoStart={autoStartRecording}, interval={snapshotInterval}s, maxSnapshots={maxSnapshots})", 
				LogLevel.Info);
		}
#endif
		
		/* Lifecycle hooks are in partials */
	}
}
#endif