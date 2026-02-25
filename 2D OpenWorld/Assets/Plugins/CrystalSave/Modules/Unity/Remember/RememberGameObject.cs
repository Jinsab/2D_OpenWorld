#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember GameObject")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(0)]
	[RememberTarget(typeof(GameObject))]
	public class RememberGameObject : SaveableComponent
	{
#if UNITY_EDITOR
		private static bool s_PlaymodeWarningShown = false;
		private string _cachedName;
#endif

		//──────────────────────────────────────────────────────────────
		// Performance toggles (Inspector)
		//──────────────────────────────────────────────────────────────
		[Header("Performance")]
		[Tooltip("Enable lightweight caches so external systems can avoid repeated GetComponent calls.")]
		[SerializeField]
		private bool enablePerformanceCaching = true;

		[Tooltip("Batch registration: when enabled on any RememberGameObject in a subtree, SaveManager uses a single GetComponentsInChildren sweep and a coroutine to register all, reducing recursion and per-node GetComponent calls. Use with care if you depend on strict parent-before-child registration order.")]
		[SerializeField]
		private bool enableBatchRegistration = false;

		/// <summary>
		/// Whether this object opts into batched registration for its subtree.
		/// </summary>
		public bool EnableBatchRegistration => enableBatchRegistration;

                [Header("GameObject Properties to Save")]
                [SerializeField]
                private GameObjectPropertySettings propertySettings = new GameObjectPropertySettings
                {
                        RememberActive = true
                };
                public GameObjectPropertySettings PropertySettings => propertySettings;

                [Header("Save Optimization")]
                [Tooltip("Skip serialization when no tracked GameObject properties changed during play mode.")]
                [SerializeField]
                private bool skipSavingWhenUnchanged = false;

		// Static variable to cache the TagRegistry reference
		private static TagRegistry cachedTagRegistry = null;

		// Instance variable to hold the TagRegistry reference
		private TagRegistry tagRegistry;

                private GameObjectData initialState;
                private bool hasInitialState;
                private byte[] cachedSerializedData;
                private bool isApplyingActiveState = false;
                private bool? lastKnownActiveState = null;

                internal bool IsApplyingActiveState => isApplyingActiveState;

                protected override void Awake()
                {
                        base.Awake();

                        // Auto-reference the TagRegistry
                        tagRegistry = GetTagRegistry();

                        if (tagRegistry == null)
                        {
                                Logger.Log($"RememberGameObject: TagRegistry is not assigned on '{gameObject.name}'. Disabling component.", LogCategory.RememberGameObject, LogLevel.Error);
                                enabled = false;
                                return;
                        }
                        Logger.Log($"[RememberGameObject] Awake called on '{gameObject.name}' with UniqueID: {UniqueIdentifier}", LogCategory.RememberGameObject, LogLevel.Off);

                        if (skipSavingWhenUnchanged)
                        {
                                hasInitialState = TryCaptureCurrentState(out initialState, logSerialization: false);
                        }
                        else
                        {
                                hasInitialState = false;
                                initialState = null;
                        }

                        // Editor-only safety check: skip entirely in player builds to avoid hierarchy scans
#if UNITY_EDITOR
			WarnIfInsideSaveablePrefab();
#endif

#if UNITY_EDITOR
			_cachedName = gameObject.name;
#endif

                        // Populate caches early so external systems can query without extra GetComponent calls
                        CacheSelfIfEnabled();
                        
                        // Initialize the last known active state
                        lastKnownActiveState = gameObject.activeSelf;

                        if (SaveManager.Instance != null)
                        {
                                var settings = SaveManager.Instance.SaveSettings;
                                if (settings != null && !settings.scanForExistingGameObjects)
                                {
                                        if (SaveManager.IsInitialized)
                                        {
                                                SaveManager.Instance.RegisterGameObject(gameObject, propertySettings);
                                                // Ensure object is cached immediately, especially critical for inactive objects
                                                // when enableLookupCache is true to prevent first-load issues
                                                if (settings.enableLookupCache && !string.IsNullOrEmpty(UniqueIdentifier))
                                                {
                                                        SaveManager.Instance.CacheGameObject(UniqueIdentifier, gameObject);
                                                }
                                        }
                                        else
                                        {
                                                SaveManager.Instance.QueueOperation(() => {
                                                        SaveManager.Instance.RegisterGameObject(gameObject, propertySettings);
                                                        // Ensure object is cached immediately after registration
                                                        if (settings.enableLookupCache && !string.IsNullOrEmpty(UniqueIdentifier))
                                                        {
                                                                SaveManager.Instance.CacheGameObject(UniqueIdentifier, gameObject);
                                                        }
                                                });
                                        }
                                }
                        }
                }

		//──────────────────────────────────────────────────────────────
		// Static caches to reduce repeated GetComponent lookups
		//──────────────────────────────────────────────────────────────
		private static readonly System.Collections.Generic.Dictionary<int, RememberGameObject> s_RememberCache = new System.Collections.Generic.Dictionary<int, RememberGameObject>();
		private static readonly System.Collections.Generic.Dictionary<int, string> s_UniqueIdCache = new System.Collections.Generic.Dictionary<int, string>();
		private static readonly System.Collections.Generic.Dictionary<int, string> s_GameObjectIdCache = new System.Collections.Generic.Dictionary<int, string>();

	private void CacheSelfIfEnabled()
	{
		if (!enablePerformanceCaching) return;
		int id = UnityObjectHelper.GetUniqueId(gameObject);
		s_RememberCache[id] = this;
		// UniqueIdentifier and GameObjectUniqueID are provided by SaveableComponent
		if (!string.IsNullOrEmpty(UniqueIdentifier))
			s_UniqueIdCache[id] = UniqueIdentifier;
		if (!string.IsNullOrEmpty(GameObjectUniqueID))
			s_GameObjectIdCache[id] = GameObjectUniqueID;
	}

	private void RemoveFromCaches()
	{
		int id = UnityObjectHelper.GetUniqueId(gameObject);
		s_RememberCache.Remove(id);
		s_UniqueIdCache.Remove(id);
		s_GameObjectIdCache.Remove(id);
	}

	/// <summary>
	/// Try to get a cached RememberGameObject for a GameObject.
	/// </summary>
	public static bool TryGetCachedRemember(GameObject go, out RememberGameObject comp)
	{
		comp = null;
		if (go == null) return false;
		return s_RememberCache.TryGetValue(UnityObjectHelper.GetUniqueId(go), out comp) && comp != null;
	}

	/// <summary>
	/// Try to get a cached UniqueIdentifier for a GameObject.
	/// </summary>
	public static bool TryGetCachedUniqueIdentifier(GameObject go, out string uniqueId)
	{
		uniqueId = null;
		if (go == null) return false;
		return s_UniqueIdCache.TryGetValue(UnityObjectHelper.GetUniqueId(go), out uniqueId) && !string.IsNullOrEmpty(uniqueId);
	}

	/// <summary>
	/// Try to get a cached GameObjectUniqueID (base ID) for a GameObject.
	/// </summary>
	public static bool TryGetCachedGameObjectUniqueID(GameObject go, out string goId)
	{
		goId = null;
		if (go == null) return false;
		return s_GameObjectIdCache.TryGetValue(UnityObjectHelper.GetUniqueId(go), out goId) && !string.IsNullOrEmpty(goId);
	}		/// <summary>
		/// Retrieves the TagRegistry from the Resources folder, caching it for future use.
		/// </summary>
		/// <returns>The TagRegistry instance if found; otherwise, null.</returns>
		private TagRegistry GetTagRegistry()
		{
			if (cachedTagRegistry != null)
			{
				return cachedTagRegistry;
			}

                        // Load the TagRegistry via AssetProvider using the configured key
                        string configuredKey = SaveManager.GetTagRegistryAssetKey();
                        cachedTagRegistry = AssetProvider.Load<TagRegistry>(configuredKey);

                        if (cachedTagRegistry == null &&
                                !string.Equals(configuredKey, SaveManager.DefaultTagRegistryAssetKey, StringComparison.Ordinal))
                        {
                                // Fallback to legacy key to support projects that haven't updated their configuration yet
                                cachedTagRegistry = AssetProvider.Load<TagRegistry>(SaveManager.DefaultTagRegistryAssetKey);
                        }

                        if (cachedTagRegistry == null)
                        {
                                Logger.Log(
                                        $"RememberGameObject: Failed to load TagRegistry using key '{configuredKey}'.",
                                        LogCategory.RememberGameObject,
                                        LogLevel.Error);
                        }

                        return cachedTagRegistry;
		}

		/// <summary>
		/// Serializes the selected GameObject properties based on settings.
		/// </summary>
		/// <returns>Serialized byte array of GameObjectData.</returns>
		protected override byte[] SerializeComponentData()
		{
                        if (!TryCaptureCurrentState(out var gameObjectData, logSerialization: true))
                                return null;

                        // During snapshot capture, we still compare against cached state to avoid
                        // unnecessary serialization, but we DON'T return null for unchanged data.
                        // Instead, we serialize the cached state to ensure snapshot completeness.
                        bool isSnapshot = IsCapturingSnapshot;
                        
                        if (skipSavingWhenUnchanged && hasInitialState && AreEquivalent(gameObjectData, initialState))
                        {
                                if (isSnapshot)
                                {
                                        // Data unchanged: for snapshot, serialize the existing cached state
                                        Logger.Log($"RememberGameObject: Snapshot capture for '{gameObject.name}' using cached state (unchanged).", LogCategory.RememberGameObject, LogLevel.Off);
                                        return Serializer.Serialize<GameObjectData>(initialState);
                                }
                                else
                                {
                                        // Data unchanged: return cached serialized data instead of null to preserve save integrity
                                        if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                        {
                                                Logger.Log($"RememberGameObject: Returning cached serialized data for '{gameObject.name}' (unchanged).", LogCategory.RememberGameObject, LogLevel.Off);
                                                return cachedSerializedData;
                                        }
                                        
                                        Logger.Log($"RememberGameObject: Data unchanged but no cached serialized data for '{gameObject.name}' - will serialize fresh.", LogCategory.RememberGameObject, LogLevel.Off);
                                }
                        }

                        try
                        {
                                byte[] serializedData = Serializer.Serialize<GameObjectData>(gameObjectData);
                                Logger.Log($"RememberGameObject: Successfully serialized data for '{gameObject.name}'.", LogCategory.RememberGameObject, LogLevel.Info);
                                if (skipSavingWhenUnchanged)
                                {
                                        initialState = gameObjectData;
                                        hasInitialState = true;
                                        cachedSerializedData = serializedData;
                                }
                                return serializedData;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberGameObject: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberGameObject, LogLevel.Error);
                                return null;
                        }
                }

		/// <summary>
		/// Deserializes and applies the saved GameObject properties based on settings.
		/// </summary>
		/// <param name="data">Serialized byte array of GameObjectData.</param>
                protected override void DeserializeComponentData(byte[] data)
                {
                        if (data == null || data.Length == 0)
                        {
                                Logger.Log("RememberGameObject: DeserializeComponentData failed - data is null or empty.", LogCategory.RememberGameObject, LogLevel.Warning);
                                return;
                        }

                        try
                        {
                                GameObjectData deserializedData = Serializer.Deserialize<GameObjectData>(data);

                                if (deserializedData == null)
                                {
                                        Logger.Log("RememberGameObject: DeserializeComponentData failed - deserialized data is null.", LogCategory.RememberGameObject, LogLevel.Warning);
                                        return;
                                }

                                // Restore Name
                                if (propertySettings.RememberName && !string.IsNullOrEmpty(deserializedData.Name))
                                {
                                        gameObject.name = deserializedData.Name;
                                        Logger.Log($"RememberGameObject: Restored name to '{gameObject.name}'.", LogCategory.RememberGameObject, LogLevel.Info);
                                }

                                // Restore Layer
                                if (propertySettings.RememberLayer && deserializedData.Layer.HasValue)
                                {
                                        if (IsValidLayer(deserializedData.Layer.Value))
                                        {
                                                gameObject.layer = deserializedData.Layer.Value;
                                                Logger.Log($"RememberGameObject: Restored layer to '{LayerMask.LayerToName(gameObject.layer)}' for '{gameObject.name}'.", LogCategory.RememberGameObject, LogLevel.Info);
                                        }
                                        else
                                        {
                                                Logger.Log($"RememberGameObject: Invalid layer '{deserializedData.Layer.Value}' for '{gameObject.name}'. Layer not changed.", LogCategory.RememberGameObject, LogLevel.Warning);
                                        }
                                }

                                // Restore Tag (lazy: ensure TagRegistry available first)
                                if (propertySettings.RememberTag && !string.IsNullOrEmpty(deserializedData.Tag))
                                {
                                        if (tagRegistry == null) tagRegistry = GetTagRegistry();
                                        if (tagRegistry != null)
                                        {
                                                if (IsValidTag(deserializedData.Tag))
                                                {
                                                        gameObject.tag = deserializedData.Tag;
                                                        Logger.Log($"RememberGameObject: Restored tag to '{gameObject.tag}' for '{gameObject.name}'.", LogCategory.RememberGameObject, LogLevel.Info);
                                                }
                                                else
                                                {
                                                        Logger.Log($"RememberGameObject: Invalid tag '{deserializedData.Tag}' for '{gameObject.name}'. Tag not changed.", LogCategory.RememberGameObject, LogLevel.Warning);
                                                }
                                        }
                                        else
                                        {
                                                // Defer: registry not yet available (object was disabled at design time). We'll rely on Awake later.
                                                Logger.Log("RememberGameObject: TagRegistry not yet available during Deserialize; deferring tag restore.", LogCategory.RememberGameObject, LogLevel.Off);
                                        }
                                }

                                // Restore Active State
                                if (propertySettings.RememberActive && deserializedData.IsActive.HasValue)
                                {
                                        gameObject.SetActive(deserializedData.IsActive.Value);
                                        Logger.Log($"RememberGameObject: Restored active state to '{deserializedData.IsActive.Value}' for '{gameObject.name}'.", LogCategory.RememberGameObject, LogLevel.Info);
                                }

                                // Note: Destruction is handled by SaveManager's DestroyedGameObjects list
                                // Therefore, no need to handle IsDestroyed here

                                Logger.Log($"RememberGameObject: Successfully loaded data for '{gameObject.name}'.", LogCategory.RememberGameObject, LogLevel.Info);

                                if (skipSavingWhenUnchanged)
                                {
                                        initialState = deserializedData;
                                        hasInitialState = true;
                                        // Clear cached serialized data after load to force fresh serialization on next save
                                        // This ensures the comparison happens correctly and data is properly saved
                                        cachedSerializedData = null;
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberGameObject: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberGameObject, LogLevel.Error);
                        }
                }

                private bool TryCaptureCurrentState(out GameObjectData data, bool logSerialization)
                {
                        bool hasAnyData = false;
                        data = new GameObjectData();

                        if (propertySettings.RememberName)
                        {
                                data.Name = gameObject.name;
                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialize: '{gameObject.name}' Name = {gameObject.name}", LogCategory.RememberGameObject, LogLevel.Info);
                        }

                        if (propertySettings.RememberLayer)
                        {
                                data.Layer = gameObject.layer;
                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialize: '{gameObject.name}' Layer = {gameObject.layer}", LogCategory.RememberGameObject, LogLevel.Info);
                        }

                        if (propertySettings.RememberTag)
                        {
                                data.Tag = gameObject.tag;
                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialize: '{gameObject.name}' Tag = {gameObject.tag}", LogCategory.RememberGameObject, LogLevel.Info);
                        }

                        if (propertySettings.RememberActive)
                        {
                                bool currentActiveState = gameObject.activeSelf;
                                bool useCurrentState = true;

                                // Check if we're in a scene transition
                                bool inSceneTransition = SaveManager.Instance != null && SaveManager.Instance.IsInSceneTransition;

                                // Detect scene-bound SaveablePrefabs that reuse their scene instance or remember their home scene.
                                SaveablePrefab owningPrefab = null;
                                if (!SaveablePrefab.TryGetCachedSaveablePrefab(gameObject, out owningPrefab))
                                {
                                        gameObject.TryGetComponent(out owningPrefab);
                                }

                                bool isSceneBoundPrefab = owningPrefab != null &&
                                        !owningPrefab.IsAddedAtRuntime &&
                                        (owningPrefab.ReuseSceneInstanceOnLoad || owningPrefab.RememberHomeScene);

                                // When capturing RememberHome snapshots, prefer the tracker state (if available)
                                // to avoid transition-time inconsistencies, then fall back to the current state.
                                if (IsCapturingSnapshot)
                                {
                                        bool snapshotState = currentActiveState;
                                        var tracker = SaveManager.Instance?.GameObjectTracker;
                                        if (tracker != null && tracker.ActiveStates.TryGetValue(UniqueIdentifier, out var trackedState))
                                        {
                                                snapshotState = trackedState;
                                        }

                                        data.IsActive = snapshotState;
                                        lastKnownActiveState = snapshotState;
                                        useCurrentState = false;
                                }
                                else if (inSceneTransition && lastKnownActiveState.HasValue)
                                {
                                        // During scene transitions, if the object was previously active but is now inactive,
                                        // preserve the previous active state to avoid saving incorrect inactive states
                                        if (lastKnownActiveState.Value && !currentActiveState && !isSceneBoundPrefab)
                                        {
                                                data.IsActive = lastKnownActiveState.Value;
                                                useCurrentState = false;
                                        }
                                }

                                if (useCurrentState)
                                {
                                        data.IsActive = currentActiveState;
                                        // Update the last known state when not in a problematic transition
                                        if (!inSceneTransition || isSceneBoundPrefab)
                                        {
                                                lastKnownActiveState = currentActiveState;
                                        }
                                }

                                hasAnyData = true;

                                if (logSerialization)
                                        Logger.Log($"Serialize: '{gameObject.name}' IsActive = {data.IsActive}", LogCategory.RememberGameObject, LogLevel.Info);
                        }

                        if (!hasAnyData)
                                data = null;

                        return hasAnyData;
                }

                private static bool AreEquivalent(GameObjectData a, GameObjectData b)
                {
                        return StringEquals(a?.Name, b?.Name) &&
                               NullableIntEquals(a?.Layer, b?.Layer) &&
                               StringEquals(a?.Tag, b?.Tag) &&
                               NullableBoolEquals(a?.IsActive, b?.IsActive);
                }

                private static bool StringEquals(string a, string b)
                {
                        if (a == null && b == null)
                                return true;

                        if (a == null || b == null)
                                return false;

                        return string.Equals(a, b, StringComparison.Ordinal);
                }

                private static bool NullableIntEquals(int? a, int? b)
                {
                        if (!a.HasValue && !b.HasValue)
                                return true;

                        if (a.HasValue != b.HasValue)
                                return false;

                        return a.Value == b.Value;
                }

                private static bool NullableBoolEquals(bool? a, bool? b)
                {
                        if (!a.HasValue && !b.HasValue)
                                return true;

                        if (a.HasValue != b.HasValue)
                                return false;

                        return a.Value == b.Value;
                }

                private bool IsValidLayer(int layer)
                {
                        // Unity supports layers 0-31
                        if (layer < 0 || layer >= 32)
			{
				return false;
			}

			return true;
		}

		private bool IsValidTag(string tag)
		{
			// Lazy-resolve TagRegistry to handle cases where Deserialize runs
			// before this component's Awake (e.g., objects enabled at runtime)
			if (tagRegistry == null)
			{
				tagRegistry = GetTagRegistry();
			}

			if (tagRegistry == null)
			{
				Logger.Log("RememberGameObject: TagRegistry is not assigned.", LogLevel.Error);
				return false;
			}

			return tagRegistry.Tags.Contains(tag);
		}

	protected override void OnEnable()
	{
		base.OnEnable();
		// Refresh caches when enabled
		CacheSelfIfEnabled();
		// Track explicit enablement outside of scene transitions so snapshots capture true state
		if (ShouldUpdateLastKnownActiveState())
		{
			lastKnownActiveState = true;
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		// Remove from caches to avoid stale references
		RemoveFromCaches();
		// Track explicit disablement outside of scene transitions so snapshots capture true state
		if (ShouldUpdateLastKnownActiveState())
		{
			lastKnownActiveState = false;
		}
	}

	private bool ShouldUpdateLastKnownActiveState()
	{
		if (!Application.isPlaying) return false;
		if (isApplyingActiveState) return false;
		var sm = SaveManager.Instance;
		if (sm != null && (sm.IsLoading || sm.IsInSceneTransition)) return false;
		return true;
	}

		/// <summary>
		/// Sets the flag indicating whether active state is being applied during load.
		/// This prevents OnDestroy from incorrectly registering GameObjects as destroyed
		/// when they're just being deactivated during state restoration.
		/// </summary>
		public void SetApplyingActiveState(bool applying)
		{
			isApplyingActiveState = applying;
		}

		protected override void OnDestroy()
		{
			// 1) If we are not playing, don't register
			if (!Application.isPlaying) return;

			// 2) If the SaveManager is loading or if we are in some 'exiting play mode' state, skip
                        if (SaveManager.Instance == null) return;

                        var manager = SaveManager.Instance;
                        bool isLoadingFlag = manager.IsLoading;
                        bool inSceneTransition = manager.IsInSceneTransition;

                        if (isLoadingFlag || inSceneTransition)
                        {
                                RemoveFromCaches();
                                return;
                        }

			// 3) If we're currently applying active state during load, don't register as destroyed
			if (isApplyingActiveState)
			{
				RemoveFromCaches();
				return;
			}

			// Use GameObjectUniqueID (base ID) instead of UniqueIdentifier
                        if (propertySettings != null && propertySettings.RememberDestroyed)
                        {
                                manager.RegisterDestroyedGameObject(GameObjectUniqueID);
                        }

			// Ensure caches are cleaned up
			RemoveFromCaches();
		}

		/*──────────────────────────────────────────────────────────────*/
        /* PLAY-MODE WARNING                                            */
        /*──────────────────────────────────────────────────────────────*/
		#if UNITY_EDITOR
		private void WarnIfInsideSaveablePrefab()
		{
			// Already warned this play-session?
			if (s_PlaymodeWarningShown) return;
			if (!Application.isPlaying)  return;

			// Are we a descendant of a SaveablePrefab?
			if (GetComponentInParent<SaveablePrefab>(includeInactive: true) is SaveablePrefab sp &&
				sp.gameObject != gameObject)                       // exclude prefab root itself
			{
				Debug.LogError(
					$"[Crystal Save] ‘{name}’ has a RememberGameObject component " +
					$"but lives **inside** the instantiated Remember Prefab '{sp.name}'.\n" +
					$"• Remember Prefab already records Tag, Layer, Disabled/Destroyed of its children automatically.\n" +
					$"• Each child’s *name* becomes its Unique Identifier, so renaming " +
					$"them or adding RememberGameObject is no longer supported.",
					this
				);
				s_PlaymodeWarningShown = true;                      // prevent spam
			}
		}
		#endif
		/*──────────────────────────────────────────────────────────────*/
        /* EDITOR-TIME RENAME WARNING                                   */
        /*──────────────────────────────────────────────────────────────*/
#if UNITY_EDITOR
        private void Update()
        {
            // Run only in the Editor, never in builds
            if (!Application.isPlaying &&
                _cachedName != gameObject.name &&                   // name changed?
                GetComponentInParent<SaveablePrefab>(true) != null) // under prefab?
            {
                Debug.LogError(
                    $"[Crystal Save] Renaming child '{_cachedName}' → '{gameObject.name}' " +
                    $"inside a Remember Prefab breaks the save-system. Undo or keep the " +
                    $"original name.",
                    this
                );
                _cachedName = gameObject.name;                      // update cache
            }
        }
#endif
	}

	[MemoryPackable]
	public partial class GameObjectData
	{
		public string Name { get; set; }
		public int? Layer { get; set; }
		public string Tag { get; set; }
		public bool? IsActive { get; set; }
		public bool? IsDestroyed { get; set; }

		public string ParentID { get; set; }
		public bool IsParentSceneObject { get; set; }

		public GameObjectData() { }
	}


	[System.Serializable]
	public class GameObjectPropertySettings
	{
		[Tooltip("Enable saving the active state of the GameObject.")]
		public bool RememberActive = false;

		[Tooltip("Enable saving the name of the GameObject.")]
		public bool RememberName = false;

		[Tooltip("Enable saving the layer of the GameObject.")]
		public bool RememberLayer = false;

		[Tooltip("Enable saving the tag of the GameObject.")]
		public bool RememberTag = false;

		[Tooltip("Enable tracking and saving the destruction of the GameObject.")]
		public bool RememberDestroyed = false;
	}

	[MemoryPackable]
	public partial class LegacyGameObjectData : ILegacyConvertible<GameObjectData>
	{
		public string Name { get; set; }
		public int? Layer { get; set; }
		public string Tag { get; set; }
		public bool? IsActive { get; set; }

		public LegacyGameObjectData() { }

		[MemoryPackConstructor]
		public LegacyGameObjectData(string name, int? layer, string tag, bool? isActive)
		{
			Name = name;
			Layer = layer;
			Tag = tag;
			IsActive = isActive;
		}

		public GameObjectData ConvertToCurrent()
		{
			return new GameObjectData
			{
				Name = this.Name,
				Layer = this.Layer,
				Tag = this.Tag,
				IsActive = this.IsActive,
				IsDestroyed = false, // Default or handle as needed
				ParentID = null, // Default since legacy data doesn't have this
				IsParentSceneObject = false // Default value
			};
		}
	}
}
#endif
