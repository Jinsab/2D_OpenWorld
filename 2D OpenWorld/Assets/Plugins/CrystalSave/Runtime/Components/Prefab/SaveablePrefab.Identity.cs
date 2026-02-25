#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;
#endif

namespace Arawn.CrystalSave.Runtime
{
        public partial class SaveablePrefab
        {
                
                public bool IsLoading => isLoading;

                // Flag to avoid emitting the instantiation event twice when a prefab
                // starts inactive and later becomes active.
                bool _instantiatedRaised = false;

                // Guard to prevent running initialization more than once when we
                // manually initialize an inactive instance via the factory.
                bool _initialized = false;

               private void Awake()
               {
                       InitializeInstance();
                       CacheSelfIfEnabled();
               }

                /// <summary>
                /// Performs the work previously done in <c>Awake</c> so that inactive
                /// prefabs instantiated at runtime can be initialized manually.
                /// </summary>
                public void InitializeInstance()
                {
                        if (_initialized) return;
                        _initialized = true;

                        // Snapshot initial GameObject properties for runtime tracking
                        initialName        = gameObject.name;
                        initialTag         = gameObject.tag;
                        initialLayer       = gameObject.layer;
                        initialActiveState = gameObject.activeSelf;

                        visibilityController ??= GetComponent<PersistentVisibilityController>();

                        // Runtime safety-net: if the prefab is configured for cross-scene
                        // persistence with off-screen behavior but no PVC component exists
                        // (e.g. the component was never saved onto the prefab asset, or was
                        // stripped), create it dynamically so visibility toggling works.
                        // This MUST run before the rememberHomeScene block below which may
                        // clear offScreenMask, so PVC captures the original flag values.
                        if (visibilityController == null
                            && (keepAcrossScenes || rememberHomeScene)
                            && offScreenMask != OffScreenDeactivation.None
                            && VisibleInScenes != null
                            && VisibleInScenes.Count > 0)
                        {
                                visibilityController = gameObject.AddComponent<PersistentVisibilityController>();
                        }

                        EnsureUniqueID();             // generate ONLY when needed
                        EnsureUniqueIDComponent();    // keep the UniqueID component in-sync
                        HandleDontDestroy();
                        DetectRuntimeInstantiation();

                        if (skipSavingWhenUnchanged && !isAddedAtRuntime)
                        {
                                if (TryCaptureCurrentState(out var snapshot))
                                {
                                        baselineSnapshot = snapshot;
                                        lastSnapshot     = snapshot;
                                }
                        }

                        // If this is a scene-placed instance (not added at runtime), the
                        // user opted out of save-system registration, and the Instance
                        // Unique ID is still empty (e.g., during a Loading state where
                        // EnsureUniqueID() intentionally skipped generation), then assign
                        // a runtime ID so other systems relying on a stable identifier can
                        // function. This runs in both Editor Play Mode and Player builds.
                                                bool shouldAssignInstanceId = autoAssignInstanceIDWhenUnregistered &&
                                                        !RegisterWithSaveSystem &&
                                                        !isAddedAtRuntime &&
                                                        string.IsNullOrEmpty(uniqueID);

                        #if UNITY_EDITOR
                                                if (shouldAssignInstanceId && ShouldSkipEditorInstanceIDGeneration())
                                                {
                                                        shouldAssignInstanceId = false;
                                                }
                        #endif

                                                if (shouldAssignInstanceId)
                                                {
                                                        uniqueID = Guid.NewGuid().ToString();

                                // Keep UniqueID component mirrored when present (we do not
                                // auto-create it during play).
                                var uid = GetComponent<UniqueID>();
                                if (uid != null) uid.ID = uniqueID;
                        }

                        // For pooled prefabs with RememberHomeScene, don't overwrite HomeScene 
                        // if it's already been set to a real scene name and we're now in DontDestroyOnLoad
                        if (rememberHomeScene && 
                            !string.IsNullOrEmpty(homeScene) && 
                            homeScene != "DontDestroyOnLoad" &&
                            gameObject.scene.name == "DontDestroyOnLoad")
                        {
                            // Keep the existing HomeScene instead of overwriting with DontDestroyOnLoad
                        }
                        else
                        {
                            homeScene = gameObject.scene.name;
                        }

                        if (rememberHomeScene)
                        {
                                keepAcrossScenes = false;
                                offScreenMask   = OffScreenDeactivation.None;
                                if (propertySettings != null)
                                        propertySettings.RememberDestroyed = false;
                        }

                        if (trackChildStateOverrides)
                                AttachDestroyObserversRecursively(transform);

                        if (!RegisterWithSaveSystem) return;

                        var remember = GetComponent<RememberGameObject>();
                        settings = remember ? remember.PropertySettings : propertySettings;
                        settings ??= new GameObjectPropertySettings { RememberActive = true };

                        /* During a load we defer registration until the real UniqueID has
                         * been written by PrefabManager → SetUniqueID() will take care of it */
                        var state = SaveManager.Instance?.StateMachine?.CurrentState;
                        if (!string.IsNullOrEmpty(uniqueID) && state != SaveState.Loading)
                                RegisterForSaving();

                        // If the prefab is instantiated while inactive, OnEnable will not
                        // execute and the PrefabManager would miss the registration. Raise
                        // the instantiation event here so disabled prefabs are handled
                        // immediately.
                        if (!gameObject.activeInHierarchy && !string.IsNullOrEmpty(uniqueID))
                        {
                                OnPrefabInstantiated?.Invoke(this);
                                _instantiatedRaised = true;
                        }
                }

               private void OnEnable()
               {
                       CacheSelfIfEnabled();
                       // in normal game-play OnEnable _might_ be the first moment where
                       // uniqueID exists (e.g. when prefab was added at runtime)
                       if (RegisterWithSaveSystem &&
                               !isRegisteredWithSaveManager &&
                               !string.IsNullOrEmpty(uniqueID))
                       {
                               RegisterForSaving();
                       }
                       if (!string.IsNullOrEmpty(uniqueID) && !_instantiatedRaised)
                       {
                               OnPrefabInstantiated?.Invoke(this);
                               _instantiatedRaised = true;
                       }

#if CRYSTALSAVE_TIMEMACHINE
                       // If this prefab opted into TimeMachine recording, ensure a TimeMachineRecorder exists
                       if (Application.isPlaying && enableTimeMachineRecording)
                       {
                               EnsureTimeMachineRecorder();
                       }
#endif
               }

                private void OnValidate()
                {
                #if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                                                bool markDirty = false;
                                                bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);

                                var hasComposite = GetComponent<RememberComposite>() != null;
                                var uid = GetComponent<UniqueID>();

                                if (hasComposite && uid != null)
                                {
                                        var go = gameObject; // capture for closure
                                        EditorApplication.delayCall += () =>
                                        {
                                                if (go != null && uid != null)
                                                {
                                                        Undo.DestroyObjectImmediate(uid);
                                                        Logger.Log($"[Crystal Save] Deferred removal of UniqueID from '{go.name}' due to SaveablePrefab + RememberComposite combo.");
                                                }
                                        };
                                }
                                else if (uid != null)
                                {
                                        uid.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
                                }

                                // Only assign Instance UniqueID if:
                                // 1. Not a prefab asset (isPrefabAsset == false)
                                // 2. Not in prefab editing mode (IsInPrefabMode() == false)
                                // 3. GameObject is placed in a valid scene (scene.IsValid() == true)
                                //
                                // This ensures only DESIGN-TIME PLACED instances get deterministic IDs.
                                // Runtime-spawned prefabs will get their IDs generated at runtime in Awake().
                                //
                                // Valid scenarios for design-time ID assignment:
                                // - Prefabs dropped into scene hierarchy during edit mode
                                // - Disabled prefabs placed in scene (enabled at runtime)
                                // - Nested prefabs in scene (children of scene GameObjects)
                                bool isPlacedInScene = gameObject.scene.IsValid();
                                bool inPrefabMode = IsInPrefabMode();
                                bool isSceneInstance = isPlacedInScene && !isPrefabAsset && !inPrefabMode;

                                if (isSceneInstance && string.IsNullOrEmpty(uniqueID))
                                {
                                        uniqueID = Guid.NewGuid().ToString();
                                        markDirty = true;
                                }
                                else if (isSceneInstance && !string.IsNullOrEmpty(uniqueID))
                                {
                                        // Check for duplicate UniqueIDs and clear them if found
                                        if (ValidateAndClearDuplicateUniqueID())
                                        {
                                                markDirty = true;
                                        }
                                }

                                // Only sync UniqueID component for scene-placed instances
                                if (isSceneInstance)
                                {
                                        EnsureUniqueIDComponent();
                                }

                                if (!isPrefabAsset && !isAddedAtRuntime)
                                {
                                        if (autoEnforceReuseSceneInstanceOnLoad)
                                        {
                                                if (!reuseSceneInstanceOnLoad)
                                                {
                                                        reuseSceneInstanceOnLoad = true;
                                                        markDirty = true;
                                                }

                                                autoEnforceReuseSceneInstanceOnLoad = false;
                                                markDirty = true;
                                        }
                                }

                                GetComponent<RememberComposite>()?.ScheduleRefresh();

                                // ── Auto-assign PrefabAssetID when this becomes a prefab asset ──
                                if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                                {
                                        if (string.IsNullOrEmpty(prefabAssetID))
                                        {
                                                prefabAssetID = Guid.NewGuid().ToString();
                                                EditorUtility.SetDirty(this);
                                        }

                                        // Resolve the actual prefab asset - handles Prefab Mode staging objects
                                        GameObject prefabAsset = GetPrefabAssetFromInstance(gameObject);
                                        if (prefabAsset == null) return;

                                        const string path = "Assets/Plugins/CrystalSave/Resources/PrefabRegistry.asset";
                                        string capturedId = prefabAssetID; // capture ID for delayed usage
                                        EditorApplication.delayCall += () =>
                                        {
                                                if (prefabAsset == null) return;

                                                PrefabRegistry registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(path);
                                                if (registry != null)
                                                {
                                                        bool exists = registry.prefabEntries.Any(e => e.prefab == prefabAsset);
                                                        if (!exists)
                                                        {
                                                                registry.TryAddPrefab(capturedId, prefabAsset, out _);
                                                                EditorUtility.SetDirty(registry);
                                                        }
                                                }
                                        };
                                }

                                if (rememberHomeScene)
                                {
                                        keepAcrossScenes = false;
                                        offScreenMask   = OffScreenDeactivation.None;
                                        if (propertySettings != null)
                                                propertySettings.RememberDestroyed = false;
                                }
                                else
                                {
                                        if (keepAcrossScenes)
                                                rememberHomeScene = false;
                                }

                                if (markDirty)
                                        EditorUtility.SetDirty(this);
                        }
                #endif
                }

#if UNITY_EDITOR
                /// <summary>
                /// Resolves the actual prefab asset from an instance. Handles Prefab Mode staging objects,
                /// scene instances, and direct asset references.
                /// </summary>
                /// <param name="instance">The GameObject instance (may be a Prefab Mode staging object)</param>
                /// <returns>The persistent prefab asset, or null if not found</returns>
                private static GameObject GetPrefabAssetFromInstance(GameObject instance)
                {
                        if (instance == null) return null;

                        // 1) Already a persistent asset
                        if (UnityEditor.EditorUtility.IsPersistent(instance))
                                return instance;

                        // 2) Get the source asset via PrefabUtility
                        var asset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(instance);
                        if (asset != null && UnityEditor.EditorUtility.IsPersistent(asset))
                                return asset;

                        // 3) We're likely in Prefab Mode - load via asset path
                        string path = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
                        if (!string.IsNullOrEmpty(path))
                                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

                        // 4) Scene instance or unknown - return null
                        return null;
                }
#endif
		
               private void OnDisable()
               {
#if UNITY_EDITOR
                       if (!Application.isPlaying)
                       {
                               // If SaveablePrefab is being removed but RememberComposite remains
                               var composite = GetComponent<RememberComposite>();
                               var uid = GetComponent<UniqueID>();

                               // Note: UniqueID components are no longer automatically created
                               // Users must manually add UniqueID components if needed for RememberComposite
                       }
#endif
                       RemoveFromCaches();
               }

               private void OnDestroy()
               {
#if UNITY_EDITOR
                       if (!Application.isPlaying)
                       {
                                // If this SaveablePrefab is being removed but RememberComposite remains
                                var composite = GetComponent<RememberComposite>();
                                var uniqueID = GetComponent<UniqueID>();

                                // Note: UniqueID components are no longer automatically created
                                // Users must manually add UniqueID components if needed for RememberComposite
                       }
#endif

                                               if (Application.isPlaying &&
                                                       SaveManager.Instance != null &&
                                                       !SaveManager.Instance.IsLoading &&
                                                       propertySettings != null &&
                                                       propertySettings.RememberDestroyed)
                                               {
                                                       var manager = SaveManager.Instance;
                                                       if (manager.IsInSceneTransition)
                                                       {
                                                               Logger.Log(
                                                                       $"[SaveablePrefab.OnDestroy] Skip destroyed registration for '{name}' (ID: {uniqueID ?? "<null>"}) during scene transition.",
                                                                       gameObject,
                                                                       LogCategory.SaveablePrefab,
                                                                       LogLevel.Info);
                                                       }
                                                       else
                                                       {
                                                               var tracker = manager.GameObjectTracker;
                                                               bool alreadyDestroyed = tracker != null && tracker.IsGameObjectDestroyed(uniqueID);
                                                               if (!alreadyDestroyed)
                                                               {
                                                                       Logger.Log(
                                                                               $"[SaveablePrefab.OnDestroy] Registering '{name}' (ID: {uniqueID ?? "<null>"}) as destroyed.",
                                                                               gameObject,
                                                                               LogCategory.SaveablePrefab,
                                                                               LogLevel.Info);
                                                                       manager.RegisterDestroyedGameObject(uniqueID);
                                                               }
                                                               else
                                                               {
                                                                       Logger.Log(
                                                                               $"[SaveablePrefab.OnDestroy] '{name}' (ID: {uniqueID ?? "<null>"}) already marked destroyed; skipping duplicate registration.",
                                                                               gameObject,
                                                                               LogCategory.SaveablePrefab,
                                                                               LogLevel.Info);
                                                               }
                                                       }
                                               }

                       /* 1 ─ Cleanly detach from the save-system without spamming PrefabManager */
                       if (isRegisteredWithSaveManager && SaveManager.Instance != null)
                       {
                                SaveManager.Instance.SoftUnregisterGameObject(gameObject);
                                isRegisteredWithSaveManager = false;    // mark clean
                       }

                       /* 2 ─ Notify listeners (unchanged) */
                       OnPrefabDestroyed?.Invoke(this);

                       // Ensure caches are cleaned up
                       RemoveFromCaches();
               }

		/* ─────────────────────────────────────────────────────────────── */

		private void EnsureUniqueID()
		{
			if (!string.IsNullOrEmpty(uniqueID)) return;

#if UNITY_EDITOR
                        if (ShouldSkipEditorInstanceIDGeneration())
                        {
                                return;
                        }
#endif

			var state = SaveManager.Instance?.StateMachine?.CurrentState;
			if (state == SaveState.Loading || isLoading) return;   // ID will be injected later

			if (TryGetComponent(out UniqueID idComp) &&
				!string.IsNullOrEmpty(idComp.ID))
			{
				uniqueID = idComp.ID;
			}
			else
			{
				uniqueID = Guid.NewGuid().ToString();
			}
		}

                /// <summary>Ensures that any existing <see cref="UniqueID"/> component 
		/// mirrors <c>uniqueID</c>. Does not auto-create UniqueID components.</summary>
                private void EnsureUniqueIDComponent()
                {
                        var uid = GetComponent<UniqueID>();

                        // Only sync existing UniqueID components, don't auto-create new ones
                        if (uid == null)
                                return;

                        if (!string.IsNullOrEmpty(uniqueID) && uid.ID != uniqueID)
                                uid.ID = uniqueID;

#if UNITY_EDITOR
                        if (!Application.isPlaying)
                                uid.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
#endif
                }

#if UNITY_EDITOR
                private bool ShouldSkipEditorInstanceIDGeneration()
                {
                        if (Application.isPlaying)
                                return false;

                        if (!gameObject.scene.IsValid())
                                return true;

                        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                                return true;

                        if (IsInPrefabMode())
                                return true;

                        return false;
                }

                /// <summary>
                /// Check if the current GameObject is being edited in prefab mode.
                /// </summary>
                private bool IsInPrefabMode()
                {
                        try
                        {
                                var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
                                if (prefabStage != null)
                                {
                                        return prefabStage.IsPartOfPrefabContents(gameObject);
                                }

                                prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                                if (prefabStage != null)
                                {
                                        // Check if this GameObject is part of the prefab being edited
                                        // Note: Unity doesn't allow accessing prefabContentsRoot during Awake/OnEnable
                                        // so we wrap this in try-catch to handle InvalidOperationException gracefully
                                        return prefabStage.IsPartOfPrefabContents(gameObject);
                                }
                        }
                        catch (System.InvalidOperationException)
                        {
                                // Unity throws this when accessing prefabContentsRoot from Awake/OnEnable/early OnValidate
                                // If detection fails, err on the side of assuming prefab mode to avoid writing IDs to assets.
                                return true;
                        }
                        // If the object has no valid scene (e.g., prefab asset selected in Project), treat it as prefab context.
                        if (!gameObject.scene.IsValid())
                                return true;

                        return false;
                }
#endif

		private void HandleDontDestroy()
		{
			if (keepAcrossScenes)
				PersistentManager.MakePersistent(gameObject, true);
		}

		private void DetectRuntimeInstantiation()
		{
			if (string.IsNullOrEmpty(prefabAssetID))
				isAddedAtRuntime = true;
		}

		public void MarkAsAddedAtRuntime() => isAddedAtRuntime = true;
		public void SetOriginalPrefabAsset(GameObject asset) => originalPrefabAsset = asset;

		/* =======================================================================
		 * Called by PrefabManager _after_ instantiation when the real GUID is
		 * known (coming from the save file).  Also triggers registration if we
		 * skipped it in Awake().
		 * =====================================================================*/
		public void SetUniqueID(string newUniqueID)
		{
                       if (string.IsNullOrEmpty(newUniqueID)) return;


                       // Store old ID before changing it
                       string oldUniqueID = uniqueID;
                       uniqueID = newUniqueID;
                       CacheSelfIfEnabled();

                        // keep any existing UniqueID component in sync (don't auto-create)
                        var uid = GetComponent<UniqueID>();
                        if (uid != null)
                                uid.ID = newUniqueID;

			//  ────────────────────────────────────────────────────────────

		// ──────────────────────────────────────────────────────────────
		// Update PrefabManager's instantiatedPrefabs dictionary when the ID changes
		// This ensures duplicated prefabs with new IDs can be properly restored
		// ──────────────────────────────────────────────────────────────
		var prefabManager = SaveManager.Instance?.GetPrefabManager;
		if (prefabManager != null && !string.IsNullOrEmpty(oldUniqueID) && oldUniqueID != newUniqueID)
		{
			prefabManager.UpdatePrefabUniqueID(this, oldUniqueID, newUniqueID);
		}
			// Tell every SaveableComponent (on this prefab & its children) that
			// the GameObject-part of the composite key has changed, so they can
			// re-register in ComponentManager with the correct identifier.
			foreach (var comp in GetComponentsInChildren<SaveableComponent>(true))
			{
				comp.OverrideGameObjectID(newUniqueID);
				// make absolutely sure the component is registered under the new key
				//if (!comp.enabled)                       // alt. use a public getter on _isRegistered
					//ComponentManager.Instance?.RegisterSaveableComponent(comp);
			}
			// ───────────────────────────────────────────────────────────────────

			// late registration – only if we aren’t already registered
			if (RegisterWithSaveSystem && !isRegisteredWithSaveManager)
			{
                                if (settings == null)
                                {
                                        var remember = GetComponent<RememberGameObject>();
                                        settings = remember ? remember.PropertySettings : propertySettings;
                                        settings ??= new GameObjectPropertySettings { RememberActive = true };
                                }

				RegisterForSaving();
			}
		}

		/// <summary>
		/// Validates that this SaveablePrefab's UniqueID is unique in the scene.
		/// If duplicates are found, clears the UniqueID of this instance.
		/// Returns true if the UniqueID was cleared (requiring the object to be marked dirty).
		/// </summary>
		private bool ValidateAndClearDuplicateUniqueID()
		{
			if (string.IsNullOrEmpty(uniqueID)) return false;

			// Find all SaveablePrefabs in the scene with the same UniqueID
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
#if UNITY_2023_1_OR_NEWER
			SaveablePrefab[] allSaveablePrefabs = UnityEngine.Object.FindObjectsByType<SaveablePrefab>(
				FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
			SaveablePrefab[] allSaveablePrefabs = Resources.FindObjectsOfTypeAll<SaveablePrefab>();
#endif
#pragma warning restore CS0618

			// Count duplicates (excluding self)
			int duplicateCount = 0;
			for (int i = 0; i < allSaveablePrefabs.Length; i++)
			{
				if (allSaveablePrefabs[i] != this && allSaveablePrefabs[i].UniqueID == uniqueID)
				{
					duplicateCount++;
				}
			}
			
			if (duplicateCount > 0)
			{
				// Clear this instance's UniqueID since it's likely a duplicate from object duplication
				string oldID = uniqueID;
				uniqueID = "";
				Logger.Log($"SaveablePrefab: Cleared duplicate UniqueID '{oldID}' from '{name}' (likely from object duplication).", LogCategory.SaveablePrefab, LogLevel.Info);
				
				return true; // Indicate that the object needs to be marked dirty
			}

			return false;
		}
	}
}
#endif
