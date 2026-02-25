#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Runtime
{
    public partial class SaveManager
    {
                #region Save and Load API

                // Capture the UI element that triggered the save so we can
                // disable it while the operation is running and prevent
                // repeated clicks.
                private static Selectable GetInvokingSelectable()
                {
                        var go = EventSystem.current?.currentSelectedGameObject;
                        return go != null ? go.GetComponent<Selectable>() : null;
                }

		// SaveManager.cs  –  add inside the public class, e.g. near the bottom.

		/// <summary>
		/// Legacy: always fire the event.
		/// </summary>
		public void RestoreSingleGameObject(GameObject target, SaveData data = null)
			=> RestoreSingleGameObject(target, data, suppressEvent: false);

		/// <summary>
		/// Restores only the specified GameObject from the current (or supplied) SaveData.
		/// Nothing else in the scene is touched.
		/// </summary>
		public void RestoreSingleGameObject(GameObject target, SaveData data = null, bool suppressEvent = false)
		{
			if (target == null) { Logger.Log("RestoreSingleGameObject: target is null.", LogLevel.Warning); return; }

			data ??= CurrentSaveData;
			if (data == null) { Logger.Log("RestoreSingleGameObject: no SaveData loaded.", LogLevel.Warning); return; }

                        string goUID = GetUniqueID(target);
                        if (string.IsNullOrEmpty(goUID))
                        {
                                Logger.Log($"RestoreSingleGameObject: '{target.name}' has no UniqueID.", LogLevel.Warning);
                                return;
                        }

                        string baseID = GetGameObjectBaseID(target) ?? goUID;

			// 1) Active-state
			var goState = data.GameObjectStates.FirstOrDefault(s => s.UniqueID == goUID);
			if (goState != null && goState.IsActive.HasValue)
				target.SetActive(goState.IsActive.Value);

			// 2) Transform / Rigidbody / Animator
			var sp = target.GetComponent<SaveablePrefab>();
			if (sp != null)
			{
				var spData = data.Prefabs.FirstOrDefault(p => p.InstanceID == goUID);
				if (spData == null && !string.IsNullOrEmpty(sp.PrefabAssetID))
					spData = data.Prefabs.FirstOrDefault(p => p.PrefabID == sp.PrefabAssetID);
				if (spData != null)
				{
					target.transform.position = spData.Position;
					target.transform.rotation = spData.Rotation;
					target.transform.localScale = spData.Scale;

					// Restore tracking flags BEFORE applying runtime modifications (1.6.0)
					// This is critical for procedural/runtime-modified prefabs
					sp.TrackAddedComponents = spData.TrackAddedComponents;
					sp.TrackComponentBlobs = spData.TrackComponentBlobs;
					sp.TrackMaterialOverrides = spData.TrackMaterialOverrides;
					sp.TrackChildStateOverrides = spData.TrackChildStateOverrides;
					sp.TrackChildTransformOverrides = spData.TrackChildTransformOverrides;
					sp.TrackSkinnedMeshOverrides = spData.TrackSkinnedMeshOverrides;
					sp.TrackBlendshapeOverrides = spData.TrackBlendshapeOverrides;
					sp.TrackTextureOverrides = spData.TrackTextureOverrides;
					sp.TrackParticleSnapshots = spData.TrackParticleSnapshots;
					sp.TrackColliderSettings = spData.TrackColliderSettings;

					// Apply any stored runtime modifications such as
					// name, tag or layer changes.
					if (spData.RuntimeModificationData != null &&
						spData.RuntimeModificationData.Length > 0)
					{
						sp.ApplyRuntimeModifications(spData.RuntimeModificationData);
					}

					var rb = target.GetComponent<Rigidbody>();
					if (spData.HasRigidbody && rb != null)
					{
						rb.isKinematic = spData.RigidbodyIsKinematic;
						rb.useGravity = spData.RigidbodyUseGravity;
#if UNITY_6000_0_OR_NEWER
						rb.linearDamping = spData.RigidbodyDrag;
						rb.angularDamping = spData.RigidbodyAngularDrag;
						rb.linearVelocity = spData.RigidbodyVelocity;
#else
						rb.drag            = spData.RigidbodyDrag;
						rb.angularDrag     = spData.RigidbodyAngularDrag;
						rb.velocity        = spData.RigidbodyVelocity;
#endif
						rb.angularVelocity = spData.RigidbodyAngularVelocity;
					}

					var anim = target.GetComponent<Animator>();
					if (spData.HasAnimator && anim != null)
					{
						anim.Play(spData.AnimatorStateHash, 0, spData.AnimatorNormalizedTime);
						anim.Update(0f);
					}
				}
			}

			// 3) Component data
                        var subset = data.ComponentsData
                                                        .Where(kvp => kvp.Key.StartsWith(baseID + "_"))
                                                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                        // When restoring a single object multiple times within the same
                        // play session we still want each component to deserialize again
                        // (e.g. the TargetedRestore demo repeatedly restoring the cubes).
                        //
                        // Without forcing the reload the ComponentManager remembers that
                        // these components have already deserialized once and skips
                        // subsequent LoadData calls, so physics-driven changes such as a
                        // cube falling due to gravity are never reverted on the second
                        // restore. Allow duplicate loads so targeted restores always
                        // reapply the serialized state.
                        ComponentManager.ApplyComponentDataToObject(target, subset, forceDuplicateLoads: true);

			Logger.Log($"RestoreSingleGameObject: restored '{target.name}' ({goUID}) from save.", target, LogCategory.SaveManager, LogLevel.Info);

			if (!suppressEvent)
				OnSingleGameObjectRestored?.Invoke(target);
		}

		/// <summary>
		/// Tries up to <paramref name="maxRetries"/> times to LoadSaveDataForSlotAsync(slotNumber),
		/// waiting <paramref name="retryDelayMs"/>ms between attempts. If successful, applies the restore.
		/// </summary>
		public async Task<bool> RestoreSingleGameObjectWithRetryAsync(
			GameObject target,
			int        slotNumber,
			int        maxRetries   = 3,
			int        retryDelayMs = 500
		)
		{
			if (target == null)
			{
				Logger.Log("RestoreSingleGameObjectWithRetryAsync: target is null.", LogLevel.Warning);
				return false;
			}

			for (int attempt = 1; attempt <= maxRetries; attempt++)
			{
				SaveData data = null;
				try
				{
					data = await LoadSaveDataForSlotAsync(slotNumber);
				}
				catch (Exception ex)
				{
					Logger.Log($"Attempt {attempt}: error loading slot {slotNumber}: {ex.Message}", LogLevel.Warning);
				}

				if (data != null)
				{
					RestoreSingleGameObject(target, data);
					Logger.Log($"RestoreSingleGameObject: succeeded on attempt {attempt}.", LogLevel.Info);
					return true;
				}

				Logger.Log(
					$"RestoreSingleGameObject: attempt {attempt} failed (no data). " +
					$"Retrying in {retryDelayMs}ms…",
					LogLevel.Warning
				);
				await Task.Delay(retryDelayMs);
			}

			Logger.Log(
				$"RestoreSingleGameObject: all {maxRetries} attempts failed for slot {slotNumber}.",
				LogLevel.Error
			);
			return false;
		}

		/// <summary>
		/// Uses the retrying API under the hood so you get retry & error logging.
		/// </summary>
                public async void RestoreSingleGameObject(GameObject target, int slotNumber)
                {
                        bool ok = await RestoreSingleGameObjectWithRetryAsync(target, slotNumber);
                        if (!ok)
                                Logger.Log(
                                        $"RestoreSingleGameObject: ultimately failed to restore '{target?.name}' from slot {slotNumber}.",
                                        LogLevel.Error
                                );
                }

                /// <summary>
                /// Attempts to restore <paramref name="target"/> from <see cref="CurrentSaveData"/>,
                /// retrying while data is unavailable or missing required entries.
                /// </summary>
                public async Task<bool> RestoreSingleGameObjectFromCurrentDataAsync(
                        GameObject target,
                        int maxRetries = 3,
                        int retryDelayMs = 1000
                )
                {
                        if (target == null)
                        {
                                Logger.Log("RestoreSingleGameObjectFromCurrentDataAsync: target is null.", LogLevel.Warning);
                                return false;
                        }

                        string goUID = GetUniqueID(target);
                        if (string.IsNullOrEmpty(goUID))
                        {
                                Logger.Log($"RestoreSingleGameObjectFromCurrentDataAsync: '{target.name}' has no UniqueID.", LogLevel.Warning);
                                return false;
                        }

                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                                var data = CurrentSaveData;
                                if (data != null)
                                {
                                        string baseID = GetGameObjectBaseID(target) ?? goUID;
                                        bool hasState = data.GameObjectStates.Any(s => s.UniqueID == goUID);
                                        bool hasPrefab = target.GetComponent<SaveablePrefab>() is SaveablePrefab sp &&
                                                (
                                                        data.Prefabs.Any(p => p.InstanceID == goUID) ||
                                                        (!string.IsNullOrEmpty(sp.PrefabAssetID) && data.Prefabs.Any(p => p.PrefabID == sp.PrefabAssetID))
                                                );
                                        bool hasComponents = data.ComponentsData.Keys.Any(k => k.StartsWith(baseID + "_"));

                                        if (hasState || hasPrefab || hasComponents)
                                        {
                                                RestoreSingleGameObject(target, data);
                                                Logger.Log($"RestoreSingleGameObjectFromCurrentData: succeeded on attempt {attempt}.", target, LogCategory.SaveManager, LogLevel.Info);
                                                return true;
                                        }
                                }

                                Logger.Log(
                                        $"RestoreSingleGameObjectFromCurrentData: attempt {attempt} found no data. Retrying in {retryDelayMs}ms…",
                                        LogLevel.Warning
                                );
                                await Task.Delay(retryDelayMs);
                        }

                        Logger.Log(
                                $"RestoreSingleGameObjectFromCurrentData: all {maxRetries} attempts failed for '{target.name}' ({goUID}).",
                                LogLevel.Error
                        );
                        return false;
                }

                /// <summary>
                /// Fire-and-forget wrapper for <see cref="RestoreSingleGameObjectFromCurrentDataAsync"/>.
                /// Logs an error if the restore ultimately fails.
                /// </summary>
                public async void RestoreSingleGameObjectFromCurrentData(GameObject target)
                {
                        bool ok = await RestoreSingleGameObjectFromCurrentDataAsync(target);
                        if (!ok)
                                Logger.Log(
                                        $"RestoreSingleGameObjectFromCurrentData: failed to restore '{target?.name}'.",
                                        LogLevel.Error
                                );
                }

                /// <summary>
                /// Attempts to restore a GameObject or SaveablePrefab identified by
                /// <paramref name="uniqueID"/> (which may be either a prefab instance ID
                /// or a PrefabAssetID) from <see cref="CurrentSaveData"/>, retrying while
                /// data is unavailable or missing required entries.
                /// </summary>
                /// <param name="uniqueID">UniqueID or PrefabAssetID to restore.</param>
                /// <param name="maxRetries">Maximum number of retries while data is unavailable.</param>
                /// <param name="retryDelayMs">Delay in milliseconds between retries.</param>
                public async Task<bool> RestoreSingleGameObjectFromCurrentDataAsync(
                        string uniqueID,
                        int maxRetries = 3,
                        int retryDelayMs = 1000
                )
                {
                        if (string.IsNullOrEmpty(uniqueID))
                        {
                                Logger.Log("RestoreSingleGameObjectFromCurrentDataAsync: uniqueID is null or empty.", LogLevel.Warning);
                                return false;
                        }

                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                                var data = CurrentSaveData;
                                if (data != null)
                                {
                                        var instanceID = GetCurrentUniqueIDFromPrefabAssetID(uniqueID) ?? uniqueID;

                                        bool hasState = data.GameObjectStates.Any(s => s.UniqueID == instanceID);
                                        bool hasPrefab = data.Prefabs.Any(p => p.InstanceID == instanceID || p.PrefabID == instanceID);
                                        bool hasComponents = data.ComponentsData.Keys.Any(k => k.StartsWith(instanceID + "_"));

                                        if (hasState || hasPrefab || hasComponents)
                                        {
                                                RestoreSingleGameObject(uniqueID, data);
                                                Logger.Log($"RestoreSingleGameObjectFromCurrentData: succeeded on attempt {attempt}.", LogCategory.SaveManager, LogLevel.Info);
                                                return true;
                                        }
                                }

                                Logger.Log(
                                        $"RestoreSingleGameObjectFromCurrentData: attempt {attempt} found no data for '{uniqueID}'. Retrying in {retryDelayMs}ms…",
                                        LogCategory.SaveManager,
                                        LogLevel.Warning
                                );
                                await Task.Delay(retryDelayMs);
                        }

                        Logger.Log(
                                $"RestoreSingleGameObjectFromCurrentData: all {maxRetries} attempts failed for '{uniqueID}'.",
                                LogLevel.Error
                        );
                        return false;
                }

                /// <summary>
                /// Fire-and-forget wrapper for <see cref="RestoreSingleGameObjectFromCurrentDataAsync(string,int,int)"/>.
                /// Logs an error if the restore ultimately fails.
                /// </summary>
                public async void RestoreSingleGameObjectFromCurrentData(string uniqueID)
                {
                        bool ok = await RestoreSingleGameObjectFromCurrentDataAsync(uniqueID);
                        if (!ok)
                                Logger.Log(
                                        $"RestoreSingleGameObjectFromCurrentData: failed to restore '{uniqueID}'.",
                                        LogLevel.Error
                                );
                }

                /// <summary>
                /// Returns the current UniqueID of an instantiated <see cref="SaveablePrefab"/>
                /// that matches the provided <paramref name="prefabAssetID"/>. If no instance
                /// exists, <c>null</c> is returned.
                /// </summary>
                /// <param name="prefabAssetID">The PrefabAssetID to search for.</param>
                public string GetCurrentUniqueIDFromPrefabAssetID(string prefabAssetID)
                {
                        if (string.IsNullOrEmpty(prefabAssetID))
                        {
                                Logger.Log("GetCurrentUniqueIDFromPrefabAssetID: prefabAssetID is null or empty.", LogLevel.Warning);
                                return null;
                        }

                        var prefab = prefabManager?
                                .GetSaveablePrefabs()
                                .FirstOrDefault(p => p.PrefabAssetID == prefabAssetID);

                        if (prefab != null)
                                return prefab.UniqueID;

#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                        var fallback = UnityEngine.Object
                                .FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .FirstOrDefault(p => p != null && p.PrefabAssetID == prefabAssetID);
#pragma warning restore CS0618

                        if (fallback != null)
                                return fallback.UniqueID;

                        Logger.Log($"GetCurrentUniqueIDFromPrefabAssetID: no instance found for asset ID '{prefabAssetID}'.", LogLevel.Info);
                        return null;
                }

                /// <summary>
                /// Returns the PrefabAssetID associated with a live <see cref="SaveablePrefab"/>
                /// instance that has the provided <paramref name="uniqueID"/>. If no instance
                /// is found, <c>null</c> is returned.
                /// </summary>
                /// <param name="uniqueID">The SaveablePrefab instance UniqueID to search for.</param>
                public string GetPrefabAssetIDFromCurrentUniqueID(string uniqueID)
                {
                        if (string.IsNullOrEmpty(uniqueID))
                        {
                                Logger.Log("GetPrefabAssetIDFromCurrentUniqueID: uniqueID is null or empty.", LogLevel.Warning);
                                return null;
                        }

                        var prefab = prefabManager?
                                .GetSaveablePrefabs()
                                .FirstOrDefault(p => p != null && p.UniqueID == uniqueID && !string.IsNullOrEmpty(p.PrefabAssetID));

                        if (prefab != null)
                                return prefab.PrefabAssetID;

                        GameObject candidate = FindGameObjectByUniqueID(uniqueID, IdentifierType.UniqueID);
                        if (candidate != null)
                        {
                                var cached = SaveablePrefab.TryGetCachedSaveablePrefab(candidate, out var cachedPrefab)
                                        ? cachedPrefab
                                        : candidate.GetComponent<SaveablePrefab>();

                                if (cached != null && !string.IsNullOrEmpty(cached.PrefabAssetID))
                                        return cached.PrefabAssetID;
                        }

#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                        var fallback = UnityEngine.Object
                                .FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .FirstOrDefault(p => p != null && p.UniqueID == uniqueID && !string.IsNullOrEmpty(p.PrefabAssetID));
#pragma warning restore CS0618

                        if (fallback != null)
                                return fallback.PrefabAssetID;

                        Logger.Log($"GetPrefabAssetIDFromCurrentUniqueID: no prefab asset found for instance ID '{uniqueID}'.", LogLevel.Info);
                        return null;
                }

                /// <summary>
                /// Attempts to restore a destroyed GameObject from <see cref="CurrentSaveData"/>,
                /// retrying while data is unavailable or missing required entries.
                /// </summary>
                public async Task<bool> RestoreDestroyedGameObjectFromCurrentDataAsync(
                        string uniqueID,
                        int maxRetries = 3,
                        int retryDelayMs = 1000
                )
                {
                        if (string.IsNullOrEmpty(uniqueID))
                        {
                                Logger.Log("RestoreDestroyedGameObjectFromCurrentDataAsync: uniqueID is null or empty.", LogLevel.Warning);
                                return false;
                        }

                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                                var data = CurrentSaveData;
                                if (data != null)
                                {
                                        bool hasData =
                                                data.DestroyedObjectData.ContainsKey(uniqueID) ||
                                                gameObjectTracker.GetDestroyedGameObjectIDs().Any(id => id == uniqueID || id.StartsWith(uniqueID + "_"));

                                        if (hasData)
                                        {
                                                RestoreDestroyedGameObject(uniqueID, data);
                                                Logger.Log($"RestoreDestroyedGameObjectFromCurrentData: succeeded on attempt {attempt}.", LogLevel.Info);
                                                return true;
                                        }
                                }

                                Logger.Log(
                                        $"RestoreDestroyedGameObjectFromCurrentData: attempt {attempt} found no data for '{uniqueID}'. Retrying in {retryDelayMs}ms…",
                                        LogLevel.Warning
                                );
                                await Task.Delay(retryDelayMs);
                        }

                        Logger.Log(
                                $"RestoreDestroyedGameObjectFromCurrentData: all {maxRetries} attempts failed for '{uniqueID}'.",
                                LogLevel.Error
                        );
                        return false;
                }

                /// <summary>
                /// Fire-and-forget wrapper for <see cref="RestoreDestroyedGameObjectFromCurrentDataAsync"/>.
                /// Logs an error if the restore ultimately fails.
                /// </summary>
                public async void RestoreDestroyedGameObjectFromCurrentData(string uniqueID)
                {
                        bool ok = await RestoreDestroyedGameObjectFromCurrentDataAsync(uniqueID);
                        if (!ok)
                                Logger.Log(
                                        $"RestoreDestroyedGameObjectFromCurrentData: failed to restore '{uniqueID}'.",
                                        LogLevel.Error
                                );
                }

		/// <summary>
	/// Tries to restore a single prefab by its saved PrefabID (asset ID),
	/// instantiating it and then applying its saved data.
	/// </summary>
	private IEnumerator RestoreSinglePrefabCoroutine(SaveablePrefabData prefabData, SaveData data)
	{
		// Remove any lingering instance so PersistentManager doesn't nuke
		// our freshly restored prefab because of a duplicate UniqueID.
            var existing = FindGameObjectByUniqueID(prefabData.InstanceID, IdentifierType.UniqueID);
		if (existing != null)
		{
		DestroyHelper.DestroyWithLogging(
			existing,
			"RestoreSinglePrefabCoroutine: removing stale instance"
		);
		// Unity destroys objects at frame end – wait one frame so the
		// old reference vanishes before instantiating the replacement.
		yield return null;
		}

		// Instantiate just this one prefab (skipping any destroyed-IDs that match)
		yield return prefabManager.InstantiatePrefabsCoroutine(
		new List<SaveablePrefabData> { prefabData },
		data.DestroyedGameObjects,
		clearExistingPrefabs: false
		);

		// Now find the instance by its newly-assigned UniqueID (InstanceID)
            var go = FindGameObjectByUniqueID(prefabData.InstanceID, IdentifierType.UniqueID);
		if (go != null)
		{
		RestoreSingleGameObject(go, data);
		}
		else
		{
		Logger.Log(
			$"RestoreSinglePrefab: after instantiation, GameObject '{prefabData.InstanceID}' not found.",
			LogLevel.Warning
		);
		}
	}

		/// <summary>
	/// Finds the live GameObject by UniqueID then applies the supplied data.
	/// Falls back to instantiating & restoring a single prefab if needed.
	/// The <paramref name="uniqueID"/> may be either the instance UniqueID
	/// or the prefab's asset ID.
	/// </summary>
	public void RestoreSingleGameObject(string uniqueID, SaveData data = null)
	{
		data ??= CurrentSaveData;

		// 1️⃣ Try UniqueID lookup (includes SceneObjectID & existing SaveablePrefab instances)
            var go = FindGameObjectByUniqueID(uniqueID, IdentifierType.UniqueID);

		// 2️⃣ Fallback: any already-instantiated prefab with matching PrefabAssetID
		if (go == null && prefabManager != null)
		{
		var sp = prefabManager
			.GetSaveablePrefabs()
			.FirstOrDefault(x => x.PrefabAssetID == uniqueID);
		if (sp != null)
			go = sp.gameObject;
		}

		// 3️⃣ Fallback: if no instance in scene, look in the saved data for a prefab record
		if (go == null && data != null)
		{
		var prefabData = data.Prefabs
			.FirstOrDefault(p => p.PrefabID == uniqueID || p.InstanceID == uniqueID);
		if (prefabData != null)
		{
			// asynchronously instantiate + restore
			StartCoroutine(RestoreSinglePrefabCoroutine(prefabData, data));
			return;
		}
		}

		// 4️⃣ Finally: restore if we found something, otherwise warn
		if (go != null)
		{
		RestoreSingleGameObject(go, data);
		}
		else
		{
		Logger.Log(
			$"RestoreSingleGameObject: no GameObject, prefab instance, or saved prefab data for ID '{uniqueID}'.",
			LogLevel.Warning
		);
		}
	}

	/// <summary>
	/// Restores by UniqueID from the given slot (with retry),
	/// falling back to instantiating a prefab if needed. The
	/// <paramref name="uniqueID"/> parameter can be either the
	/// prefab instance ID or the prefab asset ID.
	/// </summary>
        public async void RestoreSingleGameObject(string uniqueID, int slotNumber)
        {
                await RestoreSingleGameObjectAsync(uniqueID, slotNumber);
        }


        /// <summary>
        /// Async counterpart to <see cref="RestoreSingleGameObject(string,int)"/>,
        /// allowing callers to <c>await</c> the restore operation.
        /// </summary>
        /// <param name="uniqueID">UniqueID or prefab asset ID.</param>
        /// <param name="slotNumber">Save slot to load from.</param>
        public async Task RestoreSingleGameObjectAsync(string uniqueID, int slotNumber)
        {
                // 1️⃣ Try UniqueID lookup
                var go = FindGameObjectByUniqueID(uniqueID, IdentifierType.UniqueID);

                // 2️⃣ Fallback to any existing prefab instance
                if (go == null && prefabManager != null)
                {
                var sp = prefabManager
                        .GetSaveablePrefabs()
                        .FirstOrDefault(x => x.PrefabAssetID == uniqueID);
                if (sp != null)
                        go = sp.gameObject;
                }

                // 3️⃣ If we have a live GameObject, run your retry logic
                if (go != null)
                {
                bool ok = await RestoreSingleGameObjectWithRetryAsync(go, slotNumber);
                if (!ok)
                        Logger.Log(
                        $"RestoreSingleGameObjectAsync: ultimately failed to restore '{uniqueID}' from slot {slotNumber}.",
                        LogLevel.Error
                        );
                return;
                }

                // 4️⃣ No live instance → load the save data
                var data = await LoadSaveDataForSlotAsync(slotNumber);
                if (data == null)
                {
                Logger.Log(
                        $"RestoreSingleGameObjectAsync: failed to load save data for slot {slotNumber}.",
                        LogLevel.Warning
                );
                return;
                }

                // 5️⃣ Look for a prefab record in that data
                var prefabData = data.Prefabs.FirstOrDefault(p => p.PrefabID == uniqueID || p.InstanceID == uniqueID);
                if (prefabData != null)
                {
                // instantiate + restore via coroutine
                StartCoroutine(RestoreSinglePrefabCoroutine(prefabData, data));
                }
                else
                {
                Logger.Log(
                        $"RestoreSingleGameObjectAsync: no GameObject or saved prefab data for ID '{uniqueID}' in slot {slotNumber}.",
                        LogLevel.Warning
                );
                }
        }


        /// <summary>
        /// The general save method for most cases. Initiates a save operation for the specified slot.
                /// </summary>
                /// <param name="slotNumber">The save slot number to save to.</param>
                /// <param name="lastActiveScene">Optional: The name of the last active scene.</param>
                public void Save(int slotNumber = 1, string lastActiveScene = null)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("Save: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => Save(slotNumber, lastActiveScene));
                                return;
                        }

                    // Automatically rename slots to reflect the current scene name.
                    // Previously, only empty slots (named "Slot {n}") were renamed,
                    // which meant subsequent saves in a different scene kept the
                    // old name.  We now update the slot name on every save so the
                    // SaveSlotManagerWindow always shows the latest scene.
                    SaveSlot slot = SlotManager?.GetByNumber(slotNumber);
                    if (slot != null)
                    {
                            string sceneName = string.IsNullOrEmpty(lastActiveScene)
                                    ? SceneManager.GetActiveScene().name
                                    : lastActiveScene;
                            if (!string.IsNullOrEmpty(sceneName))
                                    RenameSlot(slotNumber, sceneName);
                    }

                        SaveOperations.Save(slotNumber, lastActiveScene);
                }

                /// <summary>
                /// Overload allowing the slot name to be set explicitly.
                /// </summary>
                /// <param name="slotNumber">The save slot number to save to.</param>
                /// <param name="lastActiveScene">Optional: The name of the last active scene.</param>
                /// <param name="slotName">Custom slot name. If null or empty the current scene name is used.</param>
                public void Save(int slotNumber, string lastActiveScene, string slotName)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("Save: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => Save(slotNumber, lastActiveScene, slotName));
                                return;
        }


                    SaveSlot slot = SlotManager?.GetByNumber(slotNumber);
                    if (slot != null)
                    {
                            string nameToUse = !string.IsNullOrEmpty(slotName)
                                    ? slotName
                                    : (string.IsNullOrEmpty(lastActiveScene)
                                            ? SceneManager.GetActiveScene().name
                                            : lastActiveScene);
                            if (!string.IsNullOrEmpty(nameToUse))
                                    RenameSlot(slotNumber, nameToUse);
                    }

                        SaveOperations.Save(slotNumber, lastActiveScene);
                }

		/// <summary>
		/// Saves immediately on the calling thread – no coroutines, no yields.
		/// Intended for OnApplicationQuit / crash handlers.
		/// Screenshots are optional (default: off because they need a frame).
		/// </summary>
                public void SaveSync(int slotNumber = 1,
                                                        string lastActiveScene = null,
                                                        bool captureScreenshot = false)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "SaveSync is blocked: Cloud Save is enabled. Falling back to SaveAsync to prevent freezing.",
                                        LogLevel.Warning);
                                _ = SaveAsync(slotNumber, lastActiveScene);
                                return;
                        }

                        var invoker = GetInvokingSelectable();
                        if (invoker != null) invoker.interactable = false;
                        try
                        {
                                if (!IsInitialized || SaveOperations == null)
                                {
                                        Logger.Log("SaveSync: Waiting for SaveManager initialisation...", LogLevel.Info);
                                        QueueOperation(() => SaveSync(slotNumber, lastActiveScene, captureScreenshot));
                                        return;
                                }
                                SaveSlot slot = GetSaveSlot(slotNumber);
                                if (slot == null) {
                                        Logger.Log($"SaveSync: slot {slotNumber} does not exist.", LogLevel.Error);
                                        return;
                                }
                                if (!string.IsNullOrEmpty(lastActiveScene) && !IsSceneInBuild(lastActiveScene)) {
                                        Logger.Log($"SaveSync: scene '{lastActiveScene}' not in build.", LogLevel.Error);
                                        return;
                                }

                                if (saveSettings.enableCloudSave &&
                                    saveSettings.cloudCryptoMode == CloudCryptoMode.ServerSide)
                                {
                                        Logger.Log("SaveSync is not supported with server-side cloud crypto. Use SaveAsync/SaveCoroutine instead.",
                                                   LogLevel.Error);
                                        return;
                                }

                                SaveOperations.ResolveSlotDataSync(slot);

                                /* 1 ─ Screenshot (optional) */
                                string shot = null;
                                if (captureScreenshot && saveSettings.enableScreenshots) {
                                        if (!string.IsNullOrEmpty(slot.ScreenshotFileName))
                                                SaveOperations.DeleteExistingScreenshot(slot);
                                        shot = SaveOperations.CaptureScreenshotSync(slotNumber);
                                }

                                /* 2 ─ Gather data */
                                SaveableComponent.ResetSaveCallCounts();
                                SaveData data = CollectSaveData(lastActiveScene);
                                lock (TrackedGameObjectsLock)
                                        data.TrackedUniqueIDs = TrackedGameObjects.Keys.ToList();

                                var dupSaves = SaveableComponent.SaveCallCounts.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
                                if (dupSaves.Count > 0)
                                        Debug.LogWarning($"SaveSync: Duplicate SaveData calls for {string.Join(", ", dupSaves)}");
                                else
                                        Debug.Log($"SaveSync: Serialized {SaveableComponent.SaveCallCounts.Count} components.");

                                slot.LastSaved         = DateTime.Now;
                                slot.ScreenshotFileName = shot;     // may be null
                                slot.LastActiveScene    = data.LastActiveScene;

                                /* 3 ─ Serialize + write */
								byte[] plain = serializer.Serialize(data);
								// Standardize order with async/coroutine saves: compress then encrypt
								byte[] compressed = SaveOperations.MaybeCompress(plain);
								byte[] blob  = SaveOperations.MaybeEncrypt(compressed);

				if (saveSettings.enableCloudSave)
				{
					// Offload cloud save to a background task for backends that provide async
					// implementations to avoid blocking the main thread with GetAwaiter().GetResult().
					if (saveSettings.backend == SaveBackend.Supabase
						|| saveSystem is MySqlSaveSystem)
					{
						Task.Run(() => saveSystem.SaveAsync(blob, slot));
					}
					else
					{
						// For other async backends, run the async call on a threadpool thread
						// to avoid deadlock if the implementation schedules continuations
						// back to the Unity main thread.
						Task.Run(() => saveSystem.SaveAsync(blob, slot)).GetAwaiter().GetResult();
					}
				}
				else
				{
					saveSystem.Save(blob, slot);
				}

                                /* 4 ─ Metadata mirror (block until done) */
                                if (UseLocalMirror)
                                {
					if (saveSystem is SaveSystem concrete)
					{
						concrete.SaveSlotMetadata(slot);
					}
					else
					{
						if (saveSettings.backend == SaveBackend.Supabase
						    || saveSystem is MySqlSaveSystem)
						{
							Task.Run(() => saveSystem.SaveSlotMetadataAsync(slot));
						}
						else
						{
							// Run on background thread to avoid deadlock if metadata async
							// code requires main-thread continuations.
							Task.Run(() => saveSystem.SaveSlotMetadataAsync(slot)).GetAwaiter().GetResult();
						}
					}
                                }

                                HandleSaveCompletion(slot, true);
                        }
                        finally
                        {
                                if (invoker != null) invoker.interactable = true;
                        }
                }

		/// <summary>
		/// Starts the usual coroutine-based <see cref="Save"/> and returns a
		/// <see cref="Task"/> that completes when the save finishes (or fails).
		///
		/// No blocking, no <c>ConfigureAwait</c> needed – you stay on the main
		/// thread while Unity does its <c>yield return</c> dance.
		/// </summary>
		/// <remarks>
		/// Ideal for autosave loops:
		/// <code>
		///     async void Start () {
		///         while (true) {
		///             await SaveManager.Instance.SaveAsync(AUTO_SLOT);
		///             await Task.Delay(TimeSpan.FromMinutes(3));
		///         }
		///     }
		/// </code>
		/// </remarks>
                public Task SaveAsync(int slotNumber = 1,
                                                        string lastActiveScene = null,
                                                        CancellationToken ct = default)
                {
                        return SaveAsync(slotNumber, lastActiveScene, null, ct);
                }

                /// <summary>
                /// Saves the current state to the specified slot with a custom slot name.
                /// </summary>
                /// <param name="slotNumber">The slot number to save to</param>
                /// <param name="lastActiveScene">The last active scene name</param>
                /// <param name="customSlotName">Custom name for the slot (if null, scene name will be used)</param>
                /// <param name="ct">Cancellation token</param>
                /// <returns>A task representing the save operation</returns>
                public Task SaveAsync(int slotNumber,
                                                        string lastActiveScene,
                                                        string customSlotName,
                                                        CancellationToken ct = default)
                {
                        var invoker = GetInvokingSelectable();
                        if (invoker != null) invoker.interactable = false;

                        Task result;
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("SaveAsync: Waiting for SaveManager initialisation...", LogLevel.Info);
                                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                QueueOperation(async () =>
                                {
                                        try { await SaveOperations.SaveAsync(slotNumber, lastActiveScene, customSlotName, ct); tcs.SetResult(true); }
                                        catch (Exception ex) { tcs.SetException(ex); }
                                });
                                result = tcs.Task;
                        }
                        else
                        {
                                result = SaveOperations.SaveAsync(slotNumber, lastActiveScene, customSlotName, ct);
                        }

                        if (invoker != null)
                                result.ContinueWith(_ => UnityMainThreadDispatcher.Instance()
                                        .Enqueue(() => { if (invoker != null) invoker.interactable = true; }));

                        return result;
                }

                /// <summary>
                /// Debug helper that wraps <see cref="SaveAsync(int,string,string,System.Threading.CancellationToken)"/> in a
                /// try/catch block to surface exceptions that might otherwise freeze the editor
                /// when using certain backends (e.g. Supabase).
                /// </summary>
                /// <remarks>
                /// Intended for diagnostics only.
                /// </remarks>
                public async Task SaveAsyncDebug(int slotNumber = 1,
                                                 string lastActiveScene = null,
                                                 string customSlotName = null,
                                                 CancellationToken ct = default)
                {
                        try
                        {
                                await SaveAsync(slotNumber, lastActiveScene, customSlotName, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SaveAsyncDebug caught exception: {ex}", LogLevel.Error);
                        }
                }

		/// <summary>
		/// Alternative to SaveSync that saves immediately on the calling thread.
		/// Immediately saves the current game state on the caller’s thread.
		/// <para>
		/// *No coroutines, no <c>yield</c>. The core save-file is flushed before the
		/// method returns; the returned <see cref="Task"/> completes when slot
		/// metadata has also been written (if <see cref="UseLocalMirror"/> is <c>true</c>).
		/// </para>
		/// </summary>
		/// <param name="slotNumber">1-based index of the save-slot.</param>
		/// <param name="lastActiveScene">
		/// Scene name to store in the slot; <c>null</c> ➜ use the current scene.
		/// </param>
		/// <remarks>
		/// Ideal for <c>OnApplicationQuit</c> / <c>OnApplicationPause(true)</c>.
		/// Use <c>await SaveImmediateAsync()</c> or
		/// <c>SaveImmediateAsync().GetAwaiter().GetResult()</c> in synchronous code.
		/// </remarks>
                public Task SaveImmediateAsync(int slotNumber = 1,
                                                                               string lastActiveScene = null)
                {
                        var invoker = GetInvokingSelectable();
                        if (invoker != null) invoker.interactable = false;

                        Task result;
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("SaveImmediateAsync: Waiting for SaveManager initialisation...", LogLevel.Info);
                                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                QueueOperation(async () =>
                                {
                                        try { await SaveOperations.SaveImmediateAsync(slotNumber, lastActiveScene); tcs.SetResult(true); }
                                        catch (Exception ex) { tcs.SetException(ex); }
                                });
                                result = tcs.Task;
                        }
                        else
                        {
                                result = SaveOperations.SaveImmediateAsync(slotNumber, lastActiveScene);
                        }

                        if (invoker != null)
                                result.ContinueWith(_ => UnityMainThreadDispatcher.Instance()
                                        .Enqueue(() => invoker.interactable = true));

                        return result;
                }

		/// <summary>
		/// Reads the chosen save slot **without** instantiating anything in the scene
		/// and returns the deserialised <see cref="SaveData"/> blob.
		/// </summary>
		/// <remarks>
		/// • Uses the synchronous path when Cloud-Save is OFF (fast).
		/// • Uses the async API when Cloud-Save is ON; you can `await` it from a coroutine.
		/// </remarks>
		public async Task<SaveData> LoadSaveDataForSlotAsync(int slotNumber)
		{
			SaveSlot slot = GetSaveSlot(slotNumber);
			if (slot == null) { Logger.Log($"Slot {slotNumber} does not exist.", LogLevel.Warning); return null; }

                       return await SaveOperations.ResolveSlotDataAsync(slot);
               }

		/// <summary>
		/// Initiates a load operation with cancellation support.
		/// </summary>
		/// <param name="slotNumber">The save slot number to load.</param>
		/// <param name="restoreLastActiveScene">Whether to restore the last active scene.</param>
		/// <param name="loadAsync">Whether to load the scene asynchronously.</param>
		/// <param name="allowSceneActivation">Whether to allow scene activation when loaded asynchronously.</param>
		/// <param name="cancellationToken">CancellationToken to observe.</param>
                public void Load(int slotNumber = 1, bool restoreLastActiveScene = false, bool loadAsync = false, bool allowSceneActivation = true, CancellationToken cancellationToken = default)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("Load: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => Load(slotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation, cancellationToken));
                                return;
                        }

                        SaveSlot slot = GetSaveSlot(slotNumber);
			if (slot == null)
			{
				Logger.Log($"Load failed: Save slot {slotNumber} does not exist.", LogLevel.Error);
				HandleLoadCompletion(slotNumber, false, $"Save slot {slotNumber} does not exist.");
				return;
			}

			// ─── Guard: Supabase Custom + not logged in + no local mirror ───
                        if (saveSettings.enableCloudSave
                                && saveSettings.backend == SaveBackend.Supabase
                                && saveSettings.userFolderStrategy == UserFolderStrategy.Custom
                                && !IsSupabaseCustomLoggedIn
                                && !UseLocalMirror)
                        {
				string msg = "Not logged in and no local mirror save available.";
				Logger.Log($"Load failed for slot {slotNumber}: {msg}", LogLevel.Warning);
				// fire the failure event so UI can react
				OnLoadFailed?.Invoke(
					this,
					new OperationFailedEventArgs(slot, "Load", msg));
				HandleLoadCompletion(slotNumber, false, msg);
				return;
			}

			if (isLoading)
			{
				Logger.Log($"Load: A load operation is already in progress. Ignoring request for slot {slotNumber}.", LogLevel.Warning);
				return;
			}

			ClearLoadTracking(slotNumber);

			lock (loadCompletionLock)
			{
				loadCompletionSources[slotNumber] = new TaskCompletionSource<LoadResult>();
			}

			if (!TryAcquireLoadLock(slotNumber))
			{
				Logger.Log($"Load operation already in progress for slot {slotNumber}. Ignoring additional load request.", LogLevel.Warning);
				return;
			}

			StopActiveStateWatch();
			isLoading = true;
			StartCoroutine(LoadCoroutine(slot, restoreLastActiveScene, loadAsync, allowSceneActivation, cancellationToken));
		}

		/// <summary>
		/// Clears any previous completion / failure trackers for this slot so
		/// the next load request starts with a clean slate.
		/// </summary>
		private void ClearLoadTracking(int slotNumber)
		{
			lock (loadCompletionLock)
			{
				loadCompletionSources.Remove(slotNumber);   // TaskCompletionSource dict
			}
			// If you keep separate flags/dictionaries for success or failure,
			// clear them here as well, e.g.:
			// loadCompletion.TryRemove(slotNumber, out _);
			// loadFailure   .TryRemove(slotNumber, out _);
		}

		/// <summary>
		/// Attempts to acquire a load lock for the specified slot.
		/// </summary>
		/// <param name="slotNumber">The save slot number.</param>
		/// <returns>True if the lock was acquired; otherwise, false.</returns>
                internal bool TryAcquireLoadLock(int slotNumber)
		{
			lock (loadLocksLock)
			{
				if (loadLocks.TryGetValue(slotNumber, out bool isSlotLoading) && isSlotLoading)
				{
					return false; // Lock already held
				}
				else
				{
					loadLocks[slotNumber] = true; // Acquire lock
					return true;
				}
			}
		}

		/// <summary>
		/// Releases the load lock for the specified slot.
		/// </summary>
		/// <param name="slotNumber">The save slot number.</param>
                internal void ReleaseLoadLock(int slotNumber)
		{
			lock (loadLocksLock)
			{
				if (loadLocks.ContainsKey(slotNumber))
				{
					loadLocks[slotNumber] = false;
				}
				else
				{
					Logger.Log($"ReleaseLoadLock: Attempted to release load lock for non-existent slot {slotNumber}.", LogLevel.Warning);
				}
			}
		}

		/// <summary>
		/// Asynchronously loads a save slot and awaits its completion.
		/// Supports cancellation and timeout.
		/// </summary>
		/// <param name="slotNumber">The save slot number to load.</param>
		/// <param name="restoreLastActiveScene">Whether to restore the last active scene.</param>
		/// <param name="loadAsync">Whether to load the scene asynchronously.</param>
		/// <param name="allowSceneActivation">Whether to allow scene activation when loaded asynchronously.</param>
		/// <param name="timeout">Optional: Maximum duration to wait for the load operation.</param>
		/// <param name="cancellationToken">Optional: Token to observe while waiting for the load operation to complete.</param>
		/// <returns>A Task that resolves to a LoadResult indicating success or failure with an error message.</returns>
                public async Task<LoadResult> LoadSaveSlotAsync(
                        int slotNumber = 1,
                        bool restoreLastActiveScene = false,
                        bool loadAsync = false,
                        bool allowSceneActivation = true,
                        TimeSpan? timeout = null,
                        CancellationToken cancellationToken = default)
                {
                        return await sceneLoadManager.LoadSaveSlotAsync(slotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation, timeout, cancellationToken);
                }

		/// <summary>
		/// Deletes the specified save slot.
		/// </summary>
		/// <param name="slotNumber">The save slot number to delete.</param>
                public async void Delete(int slotNumber)
                {
                        SaveSlot slot = SlotManager.GetByNumber(slotNumber);
			if (slot == null)
			{
				Logger.Log($"Delete failed: Save slot {slotNumber} does not exist.", LogLevel.Error);
				InvokeDeleteFailed(null, "Delete", $"Save slot {slotNumber} does not exist.");
				return;
			}

			try
			{
                                await SlotManager.DeleteAsync(slotNumber);
				// ───────────────────────────────────────────────────────────────────

				Logger.Log($"Deleted save slot {slotNumber}.", LogCategory.SaveSlotManager, LogLevel.Info);
				OnDeleteCompleted?.Invoke(this, new SaveManagerEventArgs(slot));
				OnSaveSlotsUpdated?.Invoke();
			}
			catch (Exception ex)
			{
				Logger.Log($"Failed to delete save slot {slotNumber}: {ex.Message}", LogCategory.SaveSlotManager, LogLevel.Error);
				InvokeDeleteFailed(slot, "Delete", ex.Message);
			}
		}

		/// <summary>
		/// Registers a TaskCompletionSource for a specific save slot.
		/// Prevents multiple concurrent load operations for the same slot.
		/// </summary>
		public bool RegisterLoadCompletion(int slotNumber, TaskCompletionSource<LoadResult> tcs)
		{
			lock (loadCompletionLock)
			{
				if (loadCompletionSources.ContainsKey(slotNumber))
				{
					Logger.Log($"LoadSaveSlotAsync: A load operation is already in progress for slot {slotNumber}.", LogLevel.Warning);
					return false;
				}
				loadCompletionSources.Add(slotNumber, tcs);
				return true;
			}
		}

		/// <summary>
		/// Unregisters a TaskCompletionSource for a specific save slot.
		/// </summary>
		public void UnregisterLoadCompletion(int slotNumber)
		{
			lock (loadCompletionLock)
			{
				loadCompletionSources.Remove(slotNumber);
			}
		}

		/// <summary>
		/// Attempts to set the load completion TaskCompletionSource.
		/// </summary>
		public bool TrySetLoadCompletion(int slotNumber, bool success, string errorMessage = null)
		{
			TaskCompletionSource<LoadResult> tcs = null;
			lock (loadCompletionLock)
			{
				if (loadCompletionSources.TryGetValue(slotNumber, out tcs))
				{
					loadCompletionSources.Remove(slotNumber);
				}
			}

			if (tcs != null)
			{
				tcs.SetResult(new LoadResult(success, errorMessage));
				return true;
			}
			return false;
		}

		/// <summary>
		/// Attempts to set the load failure TaskCompletionSource.
		/// </summary>
		public bool TrySetLoadFailure(int slotNumber, string errorMessage)
		{
			TaskCompletionSource<LoadResult> tcs = null;
			lock (loadCompletionLock)
			{
				if (loadCompletionSources.TryGetValue(slotNumber, out tcs))
				{
					loadCompletionSources.Remove(slotNumber);
				}
			}

			if (tcs != null)
			{
				tcs.SetResult(new LoadResult(false, errorMessage));
				return true;
			}
			return false;
		}

		/// <summary>
		/// Handles the completion of a load operation, signaling success or failure.
		/// </summary>
		/// <param name="slotNumber">The save slot number associated with the load operation.</param>
		/// <param name="success">Indicates whether the load was successful.</param>
		/// <param name="errorMessage">Optional error message in case of failure.</param>
                internal void HandleLoadCompletion(int slotNumber, bool success, string errorMessage = null)
		{
			UnityMainThreadDispatcher.Instance().Enqueue(() =>
			{
				if (success)
				{
					Logger.Log($"Post-load destroyedGameObjectIDs: {string.Join(", ", destroyedGameObjectIDs)}", LogCategory.SaveManager, LogLevel.Info);

					/* ─── only fire events if this slot hasn’t been completed before ─── */
					if (TrySetLoadCompletion(slotNumber, true, null))
					{
						currentSaveSlot = GetSaveSlot(slotNumber);
						OnLoadCompleted?.Invoke(
							this,
							new SaveLoadEventArgs(currentSaveSlot, true,
												$"Load completed for slot {slotNumber}."));
					}

					Logger.Log($"HandleLoadCompletion: Load operation for slot {slotNumber} completed successfully.", LogCategory.SaveManager, LogLevel.Info);
				}
				else
				{
					if (TrySetLoadFailure(slotNumber, errorMessage))
					{
						OnLoadFailed?.Invoke(
							this,
							new OperationFailedEventArgs(GetSaveSlot(slotNumber), "Load", errorMessage));
					}

					Logger.Log($"HandleLoadCompletion: Load operation for slot {slotNumber} failed with error: {errorMessage}", LogLevel.Error);
				}
			});
		}

		/// <summary>
		/// Handles the completion of a save operation, signaling success or failure.
		/// Ensures that event invocations occur on the main thread.
		/// </summary>
		/// <param name="slot">The save slot associated with the save operation.</param>
		/// <param name="success">Indicates whether the save was successful.</param>
		/// <param name="errorMessage">Optional error message in case of failure.</param>
		public void HandleSaveCompletion(SaveSlot slot, bool success, string errorMessage = null)
		{
			UnityMainThreadDispatcher.Instance().Enqueue(() =>
			{
				Logger.Log("Dispatching save completion event to the main thread.", LogCategory.SaveManager);
				if (success)
				{
					// Clear cloud existence cache so next HasSave check will re-probe
					SlotManager?.InvalidateCloudCache(slot?.SlotNumber ?? 0);

					// Sync the updated slot to the internal list to keep it in sync
					SyncSlotToInternalList(slot);

					Logger.Log($"[SaveManager] HandleSaveCompletion SUCCESS for slot {slot.SlotNumber}: Name='{slot.SlotName}', LastSaved={slot.LastSaved}, Screenshot='{slot.ScreenshotFileName ?? "null"}'", LogCategory.SaveManager);
					
					// Check if the SlotManager has the updated slot
					var managerSlot = SlotManager?.GetByNumber(slot.SlotNumber);
					if (managerSlot != null)
					{
						Logger.Log($"[SaveManager] SlotManager slot {slot.SlotNumber}: Name='{managerSlot.SlotName}', LastSaved={managerSlot.LastSaved}, Screenshot='{managerSlot.ScreenshotFileName ?? "null"}'", LogCategory.SaveManager);
					}
					else
					{
						Logger.Log($"[SaveManager] SlotManager slot {slot.SlotNumber} is NULL!", LogCategory.SaveManager);
					}
					
					currentSaveSlot = slot;
					var saveCompletedEvent = OnSaveCompleted;
					saveCompletedEvent?.Invoke(this, new SaveLoadEventArgs(slot, true, $"Save completed for slot {slot.SlotNumber}."));
					// Notify listeners that slot metadata may have changed (e.g., SlotName)
					OnSaveSlotsUpdated?.Invoke();
					Logger.Log($"HandleSaveCompletion: Save operation for slot {slot.SlotNumber} completed successfully.", LogCategory.SaveManager, LogLevel.Info);

					// Ensure the slots list is fully refreshed from local and remote sources
					// so editor/runtime UIs observe the new state immediately.
					// Run on the Unity main thread to avoid off-thread API calls.
					UnityMainThreadDispatcher.Instance().Enqueue(() => { _ = ForceRefreshSlotsAsync(); });

										// Capture a fresh snapshot of the active scene so RememberHomeScene
										// components can be restored when returning without requiring a scene switch.
										try { componentManager?.SnapshotCurrentSceneAll(); }
										catch (Exception ex) { Logger.Log($"Snapshot after save failed: {ex.Message}", LogLevel.Warning); }
				}
				else
				{
					var saveFailedEvent = OnSaveFailed;
					saveFailedEvent?.Invoke(this, new OperationFailedEventArgs(slot, "Save", errorMessage));
					Logger.Log($"HandleSaveCompletion: Save operation for slot {slot.SlotNumber} failed with error: {errorMessage}", LogLevel.Error);
				}
			});
		}

		/// <summary>
		/// Asynchronously renames a save slot.
		/// Ensures that Unity API calls occur on the main thread.
		/// </summary>
		/// <param name="slotNumber">The save slot number to rename.</param>
		/// <param name="newName">The new name for the save slot.</param>
		/// <returns>A Task that resolves to true if the renaming was successful; otherwise, false.</returns>
		public async Task<bool> RenameSaveSlotAsync(int slotNumber, string newName)
		{
			if (string.IsNullOrEmpty(newName))
			{
				Logger.Log($"RenameSaveSlotAsync: New name cannot be null or empty.", LogLevel.Error);
				return false;
			}

			try
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log($"[SaveManager] WebGL: Starting async rename for slot {slotNumber} to '{newName}'");
#endif
                        await WaitForRefreshCompletionAsync();

                                SaveSlot slot = SlotManager.GetByNumber(slotNumber) ?? EnsurePrimarySlotExists(slotNumber);
                                if (slot == null)
                                {
                                        Logger.Log($"RenameSaveSlotAsync: Rename failed: Save slot {slotNumber} does not exist.", LogLevel.Error);
					
					// Use main thread dispatcher for UI events
					UnityMainThreadDispatcher.Instance().Enqueue(() =>
					{
						InvokeRenameSlotFailed(slot, "RenameSaveSlotAsync", $"Save slot {slotNumber} does not exist.");
					});
					return false;
				}

                                string oldName = slot.SlotName;

                                // Preserve metadata and screenshot before renaming in case the save system
                                // loses these fields (for example when using cloud-only storage).
                                string originalScreenshot = slot.ScreenshotFileName;
                                Dictionary<string, string> originalMeta = null;
                                if (slot.CustomMetadata != null && slot.CustomMetadata.Count > 0)
                                        originalMeta = new Dictionary<string, string>(slot.CustomMetadata);

                                // Rename the slot in memory
                                bool renameSuccess = SlotManager.Rename(slotNumber, newName);
                                if (!renameSuccess)
                                {
                                        Logger.Log($"RenameSaveSlotAsync: Failed to rename slot {slotNumber} in memory.", LogLevel.Error);
                                        return false;
                                }

                                // Reapply preserved metadata if the rename operation cleared it
                                if (!string.IsNullOrEmpty(originalScreenshot) && string.IsNullOrEmpty(slot.ScreenshotFileName))
                                        slot.ScreenshotFileName = originalScreenshot;
                                if (originalMeta != null && (slot.CustomMetadata == null || slot.CustomMetadata.Count == 0))
                                        slot.CustomMetadata = originalMeta;

                                // Persist the updated slot metadata asynchronously
                                await saveSystem.SaveSlotMetadataAsync(slot);

#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log($"[SaveManager] WebGL: Slot metadata saved successfully for slot {slotNumber}");
#endif
				Logger.Log($"RenameSaveSlotAsync: Successfully renamed slot {slotNumber} to '{newName}'.", LogCategory.SaveSlotManager, LogLevel.Info);
				
				// Use main thread dispatcher for UI events
				UnityMainThreadDispatcher.Instance().Enqueue(() =>
				{
					OnSaveSlotsUpdated?.Invoke();
					
					// Invoke OnRenameSlotCompleted event
					var successArgs = new RenameSlotEventArgs(slot, oldName, newName);
					OnRenameSlotCompleted?.Invoke(this, successArgs);
				});
				
				return true;
			}
			catch (Exception ex)
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log($"[SaveManager] WebGL: Exception during rename slot {slotNumber}: {ex.Message}", LogLevel.Error);
#endif
				Logger.Log($"RenameSaveSlotAsync: Failed to rename slot {slotNumber}: {ex.Message}", LogLevel.Error);
				
				// Use main thread dispatcher for UI events
				SaveSlot slot = SlotManager.GetByNumber(slotNumber);
				UnityMainThreadDispatcher.Instance().Enqueue(() =>
				{
					InvokeRenameSlotFailed(slot, "RenameSaveSlotAsync", ex.Message);
				});
				
				return false;
			}
		}

                private async Task WaitForRefreshCompletionAsync(TimeSpan? timeout = null)
                {
                        if (!_refreshInProgress)
                                return;

                        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(2);
                        float start = Time.realtimeSinceStartup;
                        while (_refreshInProgress && Time.realtimeSinceStartup - start < (float)limit.TotalSeconds)
                        {
                                await Task.Yield();
                        }

                        if (_refreshInProgress)
                        {
                                Logger.Log("WaitForRefreshCompletionAsync: Timed out while waiting for slot refresh to finish.", LogLevel.Warning);
                        }
                }

		/// <summary>
		/// Renames the specified save slot.
		/// Should be called only from the main thread.
		/// </summary>
		/// <param name="slotNumber">The save slot number to rename.</param>
		/// <param name="newName">The new name for the save slot.</param>
                public void RenameSlot(int slotNumber, string newName)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "RenameSlot is blocked: Cloud Save is enabled. Using RenameSaveSlotAsync instead to prevent freezing.",
                                        LogLevel.Warning);
                                _ = RenameSaveSlotAsync(slotNumber, newName);
                                return;
                        }

                        SaveSlot slot = SlotManager.GetByNumber(slotNumber);
                        if (slot == null)
                        {
                                Logger.Log($"Rename failed: Save slot {slotNumber} does not exist.", LogLevel.Error);
                                InvokeRenameSlotFailed(slot, "RenameSlot", $"Save slot {slotNumber} does not exist.");
                                return;
                        }

                        string oldName = slot.SlotName;
                        try
                        {
                                // Preserve the current screenshot file name and metadata before renaming.
                                string originalScreenshot = slot.ScreenshotFileName;
                                Dictionary<string, string> originalMeta = null;
                                if (slot.CustomMetadata != null && slot.CustomMetadata.Count > 0)
                                        originalMeta = new Dictionary<string, string>(slot.CustomMetadata);

                                SlotManager.Rename(slotNumber, newName);

                                // Restore preserved values if they were lost during the rename operation.
                                if (!string.IsNullOrEmpty(originalScreenshot) && string.IsNullOrEmpty(slot.ScreenshotFileName))
                                        slot.ScreenshotFileName = originalScreenshot;
                                if (originalMeta != null && (slot.CustomMetadata == null || slot.CustomMetadata.Count == 0))
                                        slot.CustomMetadata = originalMeta;

                                // Persist the updated slot name so it survives
                                // application restarts.
				if (saveSettings.backend == SaveBackend.Supabase)
					Task.Run(() => saveSystem.SaveSlotMetadataAsync(slot));
				else
					Task.Run(() => saveSystem.SaveSlotMetadataAsync(slot)).GetAwaiter().GetResult();

                                Logger.Log($"Renamed save slot {slotNumber} to '{newName}'.", LogLevel.Info);
                                OnSaveSlotsUpdated?.Invoke();

                                // Invoke OnRenameSlotCompleted event
                                var successArgs = new RenameSlotEventArgs(slot, oldName, newName);
                                OnRenameSlotCompleted?.Invoke(this, successArgs);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Failed to rename save slot {slotNumber}: {ex.Message}", LogLevel.Error);
                                InvokeRenameSlotFailed(slot, "RenameSlot", ex.Message);
                        }
                }

		/// <summary>
		/// Retrieves a screenshot associated with the specified save slot.
		/// </summary>
		/// <param name="slot">The save slot to retrieve the screenshot from.</param>
		/// <returns>The screenshot as a Texture2D, or null if not found.</returns>
                public Texture2D GetScreenshot(SaveSlot slot)
                {
                        return SaveOperations?.GetScreenshot(slot);
                }

		/// <summary>
		/// Captures a screenshot synchronously for the given slot number and
		/// returns the generated file name.  Uses the same logic as the
		/// internal save routine.
		/// </summary>
                public string CaptureScreenshotSync(int slotNumber)
                {
                        return SaveOperations != null ?
                                SaveOperations.CaptureScreenshotSync(slotNumber) :
                                null;
                }

		/// <summary>
		/// Cleans up unused screenshots based on the current save slots.
		/// </summary>
                public void CleanupScreenshots()
                {
                        SaveOperations?.CleanupScreenshots();
                }

                public async Task QuickSaveAsync()
                {
                        float startTime = Time.realtimeSinceStartup;

                        await WaitForQuickSlotsAsync();

                        var settings = SaveSettings;
                        if (settings == null)
                        {
                                Logger.Log("[QuickSave] Aborting: SaveSettings is null.", LogLevel.Warning);
                                return;
                        }

                        int limit = settings.numberOfQuickSaveSlots;
                        int offset = settings.quickSaveSlotOffset;
                        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                        Logger.Log($"[QuickSave] Starting QuickSaveAsync (limit={limit}, offset={offset}, scene='{sceneName}', time={DateTime.UtcNow:O}).", LogLevel.Info);

                        if (limit <= 0)
                        {
                                Logger.Log($"[QuickSave] Aborting: numberOfQuickSaveSlots={limit} (must be > 0).", LogLevel.Warning);
                                return;
                        }

                        if (SlotManager == null)
                        {
                                Logger.Log("[QuickSave] Aborting: SlotManager is null.", LogLevel.Error);
                                return;
                        }

                        if (QuickSlotManager == null)
                        {
                                Logger.Log("[QuickSave] QuickSlotManager is null after WaitForQuickSlotsAsync; reinitializing quick slots.", LogLevel.Warning);
                                await InitializeQuickSaveSlotsAsync(limit);
                                if (QuickSlotManager == null)
                                {
                                        Logger.Log("[QuickSave] Aborting: QuickSlotManager is still null after reinitialization.", LogLevel.Error);
                                        return;
                                }
                        }

                        try
                        {
                                for (int i = limit; i > 1; i--)
                                {
                                        int source = offset + i - 1;
                                        int destination = offset + i;
                                        Logger.Log($"[QuickSave] Shifting data {source} -> {destination}.", LogLevel.Info);
                                        await CopySlotDataAsync(source, destination);
                                }

                                int slotNumber = offset + 1;
                                var primarySlot = EnsurePrimarySlotExists(slotNumber);
                                Logger.Log($"[QuickSave] Target primary slot {slotNumber} ready? {primarySlot != null}.", LogLevel.Info);
                                Logger.Log($"[QuickSave] Saving slot {slotNumber}.", LogLevel.Info);
                                await SaveAsync(slotNumber, sceneName);

                                Logger.Log($"[QuickSave] Renaming slot {slotNumber} to 'Quick Save'.", LogLevel.Info);
                                await RenameSaveSlotAsync(slotNumber, "Quick Save");

                                var savedSlot = SlotManager?.GetByNumber(slotNumber) ?? EnsurePrimarySlotExists(slotNumber);
                                var quickSlot = QuickSlotManager?.GetByNumber(slotNumber);
                                Logger.Log($"[QuickSave] Post-save lookup: savedSlot null? {savedSlot == null}, quickSlot null? {quickSlot == null}.", LogLevel.Info);

                                if (savedSlot != null)
                                {
                                        Logger.Log($"[QuickSave] Saved slot {savedSlot.SlotNumber}: LastSaved={savedSlot.LastSaved:O}, SlotName='{savedSlot.SlotName}', Screenshot='{savedSlot.ScreenshotFileName ?? "null"}'.", LogLevel.Info);
                                }

                                if (savedSlot != null && quickSlot != null)
                                {
                                        quickSlot.LastSaved = savedSlot.LastSaved;
                                        quickSlot.LastActiveScene = savedSlot.LastActiveScene;
                                        quickSlot.SlotName = savedSlot.SlotName;
                                        quickSlot.ScreenshotFileName = savedSlot.ScreenshotFileName;
                                        quickSlot.CustomMetadata = savedSlot.CustomMetadata != null
                                                ? new Dictionary<string, string>(savedSlot.CustomMetadata)
                                                : null;

                                        Logger.Log($"[QuickSave] Quick slot {quickSlot.SlotNumber} metadata synchronized.", LogLevel.Info);
                                }
                                else if (quickSlot == null)
                                {
                                        Logger.Log("[QuickSave] QuickSlotManager returned null for target slot; metadata sync skipped.", LogLevel.Warning);
                                }

                                OnQuickSlotsUpdated?.Invoke();
                                Logger.Log($"[QuickSave] Completed in {(Time.realtimeSinceStartup - startTime) * 1000f:F1} ms.", LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"[QuickSave] Exception encountered: {ex}", LogLevel.Error);
                                throw;
                        }
                }

                public void QuickSave()
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("QuickSave: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => QuickSave());
                                return;
                        }

                        int slot = SaveSettings.quickSaveSlotOffset + 1;
                        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                        Save(slot, sceneName, $"Quick Save {slot - SaveSettings.quickSaveSlotOffset}");
                }

                private SaveSlot EnsurePrimarySlotExists(int slotNumber)
                {
                        if (SlotManager == null)
                                return null;

                        var slot = SlotManager.GetByNumber(slotNumber);
                        if (slot != null)
                                return slot;

                        slot = new SaveSlot(slotNumber, $"Slot {slotNumber}", DateTime.MinValue, string.Empty, string.Empty);
                        SlotManager.Slots.Add(slot);
                        SlotManager.Slots.Sort((a, b) => a.SlotNumber.CompareTo(b.SlotNumber));

                        if (!saveSlots.Any(s => s.SlotNumber == slotNumber))
                        {
                                saveSlots.Add(slot);
                                saveSlots.Sort((a, b) => a.SlotNumber.CompareTo(b.SlotNumber));
                        }

                        Logger.Log($"[QuickSave] Created placeholder slot {slotNumber} in primary SlotManager.", LogLevel.Info);
                        return slot;
                }

                public void QuickLoad()
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("QuickLoad: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => QuickLoad());
                                return;
                        }

                        int slot = SaveSettings.quickSaveSlotOffset + 1;
                        Load(slot, true);
                }

                public async Task QuickLoadAsync(
                        bool loadAsync = false,
                        bool allowSceneActivation = true,
                        TimeSpan? timeout = null,
                        CancellationToken cancellationToken = default)
                {
                        int slot = SaveSettings.quickSaveSlotOffset + 1;
                        await LoadSaveSlotAsync(
                                slot,
                                true,
                                loadAsync,
                                allowSceneActivation,
                                timeout,
                                cancellationToken);
                }

                public async Task AutoSaveAsync()
                {
                        var settings = SaveSettings;
                        int limit = settings.numberOfAutoSaveSlots;

                        // Multi-slot FIFO auto save (mirrors QuickSaveAsync)
                        if (limit > 0)
                        {
                                await WaitForAutoSlotsAsync();

                                if (AutoSlotManager == null)
                                {
                                        Logger.Log("[AutoSave] Aborting: AutoSlotManager is null. Ensure InitializeAutoSaveSlotsAsync was called before using multi-slot auto saves.", LogLevel.Warning);
                                        return;
                                }

                                int offset = settings.autoSaveSlotOffset;
                                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                                Logger.Log($"[AutoSave] Starting multi-slot AutoSaveAsync (limit={limit}, offset={offset}).", LogLevel.Info);

                                if (SlotManager == null)
                                {
                                        Logger.Log("[AutoSave] Aborting: SlotManager is null.", LogLevel.Error);
                                        return;
                                }

                                for (int i = limit; i > 1; i--)
                                {
                                        int source = offset + i - 1;
                                        int destination = offset + i;
                                        await CopyAutoSlotDataAsync(source, destination);
                                }

                                int slotNumber = offset + 1;
                                EnsurePrimarySlotExists(slotNumber);
                                await SaveAsync(slotNumber, sceneName);
                                await RenameSaveSlotAsync(slotNumber, "Auto Save");

                                var savedSlot = SlotManager?.GetByNumber(slotNumber) ?? EnsurePrimarySlotExists(slotNumber);
                                var autoSlot = AutoSlotManager?.GetByNumber(slotNumber);
                                if (savedSlot != null && autoSlot != null)
                                {
                                        autoSlot.LastSaved = savedSlot.LastSaved;
                                        autoSlot.LastActiveScene = savedSlot.LastActiveScene;
                                        autoSlot.SlotName = savedSlot.SlotName;
                                        autoSlot.ScreenshotFileName = savedSlot.ScreenshotFileName;
                                        autoSlot.CustomMetadata = savedSlot.CustomMetadata != null
                                                ? new Dictionary<string, string>(savedSlot.CustomMetadata)
                                                : null;
                                }

                                OnAutoSlotsUpdated?.Invoke();
                                return;
                        }

                        // Legacy single-slot auto save
                        int slot = settings.autoSaveSlotNumber;
                        if (slot <= 0) return;

                        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                        await SaveAsync(slot, scene, "Auto Save");
                }

                public void AutoSave()
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("AutoSave: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => AutoSave());
                                return;
                        }

                        var settings = SaveSettings;

                        // Multi-slot FIFO auto save: delegate to async version
                        if (settings.numberOfAutoSaveSlots > 0)
                        {
                                _ = AutoSaveAsync();
                                return;
                        }

                        int slot = settings.autoSaveSlotNumber;
                        if (slot <= 0)
                        {
                                Logger.Log("AutoSave: Auto save slot number is not configured (must be > 0).", LogLevel.Warning);
                                return;
                        }

                        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                        Save(slot, sceneName, "Auto Save");
                }

                /// <summary>
                /// Loads the autosave asynchronously with full control over scene loading.
                /// </summary>
                public async Task LoadAutoSaveAsync(
                        bool restoreScene = true,
                        bool loadAsync = false,
                        bool allowSceneActivation = true,
                        TimeSpan? timeout = null,
                        CancellationToken cancellationToken = default)
                {
                        int slot = SaveSettings.numberOfAutoSaveSlots > 0
                                ? SaveSettings.autoSaveSlotOffset + 1
                                : SaveSettings.autoSaveSlotNumber;
                        if (slot <= 0)
                        {
                                Logger.Log("LoadAutoSaveAsync: Auto save is not configured (legacy Auto Save Slot Number <= 0 and Number Of Auto Save Slots <= 0).", LogLevel.Warning);
                                return;
                        }
                        
                        await LoadSaveSlotAsync(
                                slot,
                                restoreScene,
                                loadAsync,
                                allowSceneActivation,
                                timeout,
                                cancellationToken);
                }

                /// <summary>
                /// Loads the autosave synchronously. Alias for LoadAutoSave().
                /// </summary>
                public void AutoLoad(bool restoreScene = true)
                {
                        LoadAutoSave(restoreScene);
                }

                /// <summary>
                /// Loads the autosave synchronously.
                /// </summary>
                public void LoadAutoSave(bool restoreScene = true)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("LoadAutoSave: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => LoadAutoSave(restoreScene));
                                return;
                        }

                        int slot = SaveSettings.numberOfAutoSaveSlots > 0
                                ? SaveSettings.autoSaveSlotOffset + 1
                                : SaveSettings.autoSaveSlotNumber;
                        if (slot <= 0)
                        {
                                Logger.Log("LoadAutoSave: Auto save is not configured (legacy Auto Save Slot Number <= 0 and Number Of Auto Save Slots <= 0).", LogLevel.Warning);
                                return;
                        }
                        
                        Load(slot, restoreScene);
                }

		/// <summary>
		/// Fetches metadata for the given slot, falling back to remote backends if no local mirror exists.
		/// </summary>
		public async Task<SaveSlot> GetSlotMetadataAsync(int slotNumber)
		{
			// 1 Unity Cloud Save fallback 
#if REMEMBERME_CLOUDSAVE_PRESENT
			if (SaveSettings.backend == SaveBackend.UnityCloudSave
			    && SaveSystem is SaveSystem unityCloudSave)
			{
				var remote = await unityCloudSave.LoadSlotMetadataAsync(slotNumber);
				if (remote != null) return remote;
			}
#endif
			
			// 2 Supabase fallback
			if (SaveSettings.backend == SaveBackend.Supabase
                                && SaveSystem is SupabaseSaveSystem supabase)
                        {
                                var remote = await supabase.LoadSlotMetadataAsync(slotNumber);
                                if (remote != null) return remote;
                        }
                        else if (SaveSettings.backend == SaveBackend.Firebase
                                && SaveSystem is FirebaseSaveSystem firebase)
                        {
                                var remote = await firebase.LoadSlotMetadataAsync(slotNumber);
                                if (remote != null) return remote;
                        }

			// 3 Local in-memory load from startup (fallback)
			return GetSaveSlotByNumber(slotNumber);
		}

		/// <summary>
		/// Synchronous version (blocks until remote fetch completes).
		/// </summary>
		public SaveSlot GetSlotMetadata(int slotNumber)
		{
			if (SaveSettings.backend == SaveBackend.Supabase
				&& SaveSystem is SupabaseSaveSystem supabase)
			{
				try
				{
					var remote = supabase
						.LoadSlotMetadataAsync(slotNumber)
						.GetAwaiter()
						.GetResult();
					if (remote != null) return remote;
				}
				catch { /* swallow, fallback below */ }
			}
                        else if (SaveSettings.backend == SaveBackend.Firebase &&
                                SaveSystem is FirebaseSaveSystem firebase)
                        {
                                try
                                {
                                        var remote = firebase
                                                .LoadSlotMetadataAsync(slotNumber)
                                                .GetAwaiter()
                                                .GetResult();
                                        if (remote != null) return remote;
                                }
                                catch { /* swallow, fallback below */ }
                        }
			return GetSaveSlotByNumber(slotNumber);
		}

		/// <summary>
		/// Retrieves a SaveSlot by its slot number.
		/// </summary>
		/// <param name="slotNumber">The slot number to retrieve.</param>
		/// <returns>The corresponding SaveSlot, or null if not found.</returns>
                private SaveSlot GetSaveSlot(int slotNumber)
                {
                        return SlotManager.GetByNumber(slotNumber);
                }

		/// <summary>
		/// Retrieves a SaveSlot by its slot name.
		/// </summary>
		/// <param name="slotName">The slot name to retrieve.</param>
		/// <returns>The corresponding SaveSlot, or null if not found.</returns>
		public SaveSlot GetSaveSlotByName(string slotName)
		{
                        return SlotManager.GetByName(slotName);
		}

		/// <summary>
		/// Retrieves a SaveSlot by its SlotNumber.
		/// </summary>
		public SaveSlot GetSaveSlotByNumber(int slotNumber)
		{
                        return SlotManager.GetByNumber(slotNumber);
		}

	/// <summary>
	/// Retrieves all save slots.
	/// </summary>
	/// <returns>A list of SaveSlot objects.</returns>
	public List<SaveSlot> GetSaveSlots()
	{
                        // Use the internal saveSlots list which is synchronized with SlotManager
                        // This ensures we return the correct runtime slot count even after SetSaveSlotCountAsync
                        var slots = saveSlots != null && saveSlots.Count > 0 
                            ? new List<SaveSlot>(saveSlots) 
                            : (SlotManager?.GetAll() ?? new List<SaveSlot>());
                        
						Logger.Log($"[SaveManager] GetSaveSlots returning {slots.Count} slots", LogCategory.SaveManager);
                        foreach (var slot in slots)
                        {
                            if (slot.LastSaved > DateTime.MinValue)
                            {
								Logger.Log($"[SaveManager]   Slot {slot.SlotNumber}: Name='{slot.SlotName}', LastSaved={slot.LastSaved}, Screenshot='{slot.ScreenshotFileName ?? "null"}'", LogCategory.SaveManager);
                            }
                        }
                        return slots;
	}		public SaveSlot GetLatestSaveSlot()
		{
                        var slots = SlotManager.Slots;
                        if (slots == null || slots.Count == 0)
                                return null;
                        SaveSlot latest = null;
                        foreach (var slot in slots)
			{
				if (latest == null || slot.LastSaved > latest.LastSaved)
				{
					latest = slot;
				}
			}
			return latest;
		}

		public SaveSettings GetSaveSettings()
		{
			return saveSettings;
		}

		/// <summary>
                /// Checks if there is at least one saved game.
                /// </summary>
                /// <returns>True if any save slot has been saved; otherwise, false.</returns>
                public async Task<bool> HasSaveAsync(bool hasMetaData = true)
                {
                        return await RunOnMainThreadAsync(async () =>
                        {
                                await EnsureCloudFreshnessAsync().ConfigureAwait(false);
                                return await SlotManager.HasSaveAsync(hasMetaData).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                }

                public bool HasSave(bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSave is blocked: Cloud Save is enabled. Use HasSaveAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        return HasSaveAsync(hasMetaData).GetAwaiter().GetResult();
                }

                /// <summary>
                /// Checks if a specific save slot contains saved metadata and
                /// that the corresponding data is accessible (local or cloud).
                /// </summary>
                /// <param name="slotNumber">The save slot number to check.</param>
                /// <returns>True if the specified save slot has been saved; otherwise, false.</returns>
                public async Task<bool> HasSaveAtAsync(int slotNumber, bool hasMetaData = true)
                {
                        return await RunOnMainThreadAsync(async () =>
                        {
                                await EnsureCloudFreshnessAsync().ConfigureAwait(false);
                                SaveSlot slot = SlotManager.GetByNumber(slotNumber);
                                if (slot == null)
                                {
                                        Logger.Log($"HasSaveAt: Save slot {slotNumber} does not exist.", LogLevel.Warning);
                                        return false;
                                }

                                return await SlotManager.HasSaveAtAsync(slotNumber, hasMetaData).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                }

                public bool HasSaveAt(int slotNumber, bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSaveAt is blocked: Cloud Save is enabled. Use HasSaveAtAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        return HasSaveAtAsync(slotNumber, hasMetaData).GetAwaiter().GetResult();
                }

		/// <summary>
                /// Checks if there is at least one saved game after the specified date.
                /// </summary>
                /// <param name="date">The date to compare against.</param>
                /// <returns>True if any save slot has LastSaved > date; otherwise, false.</returns>
                public async Task<bool> HasSaveAfterDateAsync(DateTime date, bool hasMetaData = true)
                {
                        return await RunOnMainThreadAsync(async () =>
                        {
                                await EnsureCloudFreshnessAsync().ConfigureAwait(false);
                                return await SlotManager.HasSaveAfterAsync(date, hasMetaData).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                }

		/// <summary>
		/// Checks if any save slot has data by examining the LastSaved timestamp.
		/// This is a more reliable alternative to HasSave() when cloud save is enabled.
		/// </summary>
		/// <returns>True if any save slot has a LastSaved timestamp greater than DateTime.MinValue; otherwise, false.</returns>
		public bool HasAnySaveInSlots()
		{
			var latestSlot = GetLatestSaveSlot();
			return latestSlot != null && latestSlot.LastSaved > DateTime.MinValue;
		}

		/// <summary>
		/// Asynchronously checks if any save slot has data by examining the LastSaved timestamp.
		/// This is a more reliable alternative to HasSaveAsync() when cloud save is enabled.
		/// </summary>
		/// <returns>True if any save slot has a LastSaved timestamp greater than DateTime.MinValue; otherwise, false.</returns>
		public Task<bool> HasAnySaveInSlotsAsync()
		{
			return RunOnMainThreadAsync(() =>
			{
				var latestSlot = GetLatestSaveSlot();
				bool result = latestSlot != null && latestSlot.LastSaved > DateTime.MinValue;
				return Task.FromResult(result);
			});
		}

		// Ensure we have the most recent cloud state before evaluating presence checks
                private async Task EnsureCloudFreshnessAsync()
                {
                        try
                        {
                                if (saveSettings != null &&
                                    saveSettings.enableCloudSave &&
                                    !saveSettings.keepLocalMirror &&
                                    CloudSaveService != null)
                                {
                                        // Full force-refresh: reinitialize local slots and then merge remote
                                        await ForceRefreshSlotsAsync();
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"EnsureCloudFreshnessAsync: refresh failed: {ex.Message}", LogLevel.Warning);
                        }
                }

                private static Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> func)
                {
                        if (UnityMainThreadDispatcher.IsMainThread)
                                return func();

                        return UnityMainThreadDispatcher.Instance().EnqueueAsync(func);
                }

		/// <summary>
		/// Reinitializes slot metadata from disk and then fetches and merges the latest
		/// remote state from the configured cloud backend. Notifies listeners afterwards.
		/// Always executed on the Unity main thread to safely interact with Unity APIs.
		/// </summary>
		private volatile bool _refreshInProgress;
                public async Task ForceRefreshSlotsAsync()
                {
                        if (UnityMainThreadDispatcher.IsMainThread)
                        {
                                await ForceRefreshSlotsOnMainThreadAsync().ConfigureAwait(false);
                                return;
                        }

                        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        UnityMainThreadDispatcher.Instance().Enqueue(async () =>
                        {
                                try
                                {
                                        await ForceRefreshSlotsOnMainThreadAsync().ConfigureAwait(false);
                                        tcs.TrySetResult(true);
                                }
                                catch (Exception ex)
                                {
                                        tcs.TrySetException(ex);
                                }
                        });
                        await tcs.Task.ConfigureAwait(false);
                }

                private async Task ForceRefreshSlotsOnMainThreadAsync()
                {
                        if (_refreshInProgress)
                                return;

                        _refreshInProgress = true;
                        try
                        {
                                // Use current runtime slot count instead of settings to preserve dynamic slot changes
                                int slotCount = CurrentSaveSlotCount > 0 
                                    ? CurrentSaveSlotCount 
                                    : saveSettings.numberOfSaveSlots;
                                await InitializeSaveSlotsAsync(slotCount);
                                await RefreshRemoteSlotsAsync();
                                NotifySaveSlotsUpdated();
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ForceRefreshSlotsAsync failed: {ex.Message}", LogLevel.Warning);
                                throw;
                        }
                        finally
                        {
                                _refreshInProgress = false;
                        }
                }

                public bool HasSaveAfterDate(DateTime date, bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSaveAfterDate is blocked: Cloud Save is enabled. Use HasSaveAfterDateAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        return HasSaveAfterDateAsync(date, hasMetaData).GetAwaiter().GetResult();
                }

		/// <summary>
		/// Checks if there is at least one saved game in the specified scene.
		/// </summary>
                /// <param name="sceneName">The name of the scene to check.</param>
                /// <returns>True if any save slot has LastActiveScene equal to sceneName; otherwise, false.</returns>
                public async Task<bool> HasSaveInSceneAsync(string sceneName, bool hasMetaData = true)
                {
                        if (string.IsNullOrEmpty(sceneName))
                        {
                                Logger.Log("HasSaveInScene: Provided sceneName is null or empty.", LogLevel.Warning);
                                return false;
                        }

                        return await RunOnMainThreadAsync(() =>
                                SlotManager.HasSaveInSceneAsync(sceneName, hasMetaData)
                        ).ConfigureAwait(false);
                }

                public bool HasSaveInScene(string sceneName, bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSaveInScene is blocked: Cloud Save is enabled. Use HasSaveInSceneAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        return HasSaveInSceneAsync(sceneName, hasMetaData).GetAwaiter().GetResult();
                }

                /// <summary>
                /// Captures the current game state into <see cref="CurrentSaveData"/> without writing to disk.
                /// Useful before calling restore helpers that rely on in-memory data.
                /// </summary>
                /// <param name="lastActiveScene">Optional name of the active scene to store in the snapshot. If null or empty, the current scene is used.</param>
                /// <returns>True if the snapshot was captured successfully; otherwise, false.</returns>
                public bool SnapshotCurrentData(string lastActiveScene = null)
                {
                        try
                        {
                                var data = CollectSaveData(lastActiveScene);
                                if (data == null)
                                {
                                        Logger.Log("SnapshotCurrentData: CollectSaveData returned null.", LogLevel.Warning);
                                        return false;
                                }

                                CurrentSaveData = data;

                                // Refresh destroyed-object tracker to match the new snapshot
                                GameObjectTracker.DestroyedIDs.Clear();
                                if (data.DestroyedGameObjects != null)
                                {
                                        foreach (var id in data.DestroyedGameObjects)
                                                GameObjectTracker.DestroyedIDs.Add(id);
                                }

                                Logger.Log("SnapshotCurrentData: captured current state in memory.", LogCategory.SaveManager, LogLevel.Info);
                                return true;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SnapshotCurrentData: failed to collect save data: {ex.Message}", LogCategory.SaveManager, LogLevel.Error);
                                return false;
                        }
                }

                /// <summary>
                /// Captures the current game state and updates prefab/component managers so pending
                /// prefabs will spawn when their home scenes load.
                /// </summary>
                /// <param name="lastActiveScene">Optional name of the active scene to record in the snapshot.
                /// If null or empty, the current scene is used.</param>
                /// <returns>True if the snapshot was captured successfully; otherwise, false.</returns>
                public async Task<bool> SnapshotAndPopulateAsync(string lastActiveScene = null)
                {
                        var ok = SnapshotCurrentData(lastActiveScene);
                        if (ok)
                                await this.PopulatePendingPrefabsFromSnapshotAsync();
                        return ok;
                }
                /// <summary>
                /// Initiates the load process by creating and running the LoadStateMachine.
                /// </summary>
                public IEnumerator StartLoadProcess(SaveSlot slot, bool restoreScene, bool asyncLoad, bool allowActivation, CancellationToken token)
                {
                        yield return sceneLoadManager.StartLoadProcess(slot, restoreScene, asyncLoad, allowActivation, token);
                }

                /// <summary>
                /// Retrieves the base UniqueID of a GameObject without any component suffix.
                /// </summary>
                private string GetGameObjectBaseID(GameObject obj)
                {
                        if (obj == null) return null;

                        var rememberGO = obj.GetComponent<RememberGameObject>();
                        if (rememberGO != null && !string.IsNullOrEmpty(rememberGO.GameObjectUniqueID))
                                return rememberGO.GameObjectUniqueID;

                        // Prefer the dedicated UniqueID component. If absent, fall back to a
                        // SceneObjectID and finally to SaveablePrefab identifiers.
                        var uid = obj.GetComponent<UniqueID>()?.ID;
                        if (!string.IsNullOrEmpty(uid)) return uid;

                        var sceneId = obj.GetComponent<SceneObjectID>()?.UniqueID;
                        if (!string.IsNullOrEmpty(sceneId)) return sceneId;

                        var sp = obj.GetComponent<SaveablePrefab>();
                        if (sp != null)
                        {
                                if (!string.IsNullOrEmpty(sp.PrefabAssetID)) return sp.PrefabAssetID;
                                if (!string.IsNullOrEmpty(sp.UniqueID)) return sp.UniqueID;
                        }

                        return null;
                }

                #region Utility Methods for Scene Loading and Prefab Queue Inspection

                /// <summary>
                /// Gets the total number of prefabs currently queued for spawning across all scenes.
                /// Useful for debugging scene loading issues and verifying that prefabs are queued correctly.
                /// </summary>
                /// <returns>Total count of queued prefabs</returns>
                public int GetPendingPrefabCount()
                {
                        if (prefabManager == null)
                                return 0;

                        int totalCount = 0;
                        var deferredScenes = prefabManager.GetDeferredSceneKeys();
                        foreach (var sceneKey in deferredScenes)
                        {
                                // Count would need to be exposed from PrefabManager
                                // For now, return a general indicator
                                totalCount++;
                        }
                        return totalCount;
                }

                /// <summary>
                /// Gets the names of scenes that have prefabs queued for spawning.
                /// Useful for debugging to see which scenes have pending prefabs waiting to be instantiated.
                /// </summary>
                /// <returns>List of scene names with queued prefabs</returns>
                public List<string> GetScenesWithPendingPrefabs()
                {
                        if (prefabManager == null)
                                return new List<string>();

                        return new List<string>(prefabManager.GetDeferredSceneKeys());
                }

                /// <summary>
                /// Validates that the current scene loading workflow follows best practices.
                /// Logs warnings if potential timing issues are detected.
                /// </summary>
                /// <param name="operationName">Name of the operation being validated (for logging)</param>
                /// <param name="targetSceneName">Name of the target scene being loaded</param>
                public void ValidateSceneLoadingTiming(string operationName, string targetSceneName)
                {
                        if (string.IsNullOrEmpty(targetSceneName))
                        {
                                Logger.Log($"[{operationName}] Target scene name is null or empty.", LogLevel.Warning);
                                return;
                        }

                        // Check if target scene is already loaded
                        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
                        bool sceneIsLoaded = targetScene.isLoaded;
                        bool sceneIsActive = SceneManager.GetActiveScene().name == targetSceneName;

                        if (sceneIsActive && !sceneIsLoaded)
                        {
                                Logger.Log(
                                        $"[{operationName}] WARNING: Scene '{targetSceneName}' is set as active but not fully loaded. " +
                                        "This may cause prefabs to spawn in the wrong scene. " +
                                        "Ensure you wait for SceneManager.LoadSceneAsync().isDone before setting the active scene.",
                                        LogLevel.Warning
                                );
                        }

                        // Check if there are pending prefabs for a scene that's about to become active
                        var pendingScenes = GetScenesWithPendingPrefabs();
                        if (pendingScenes.Contains(targetSceneName) && sceneIsActive)
                        {
                                Logger.Log(
                                        $"[{operationName}] INFO: Scene '{targetSceneName}' is active and has {pendingScenes.Count} queued prefabs. " +
                                        "Prefabs will spawn in the next frame.",
                                        LogLevel.Info
                                );
                        }
                }

                /// <summary>
                /// Clears all queued prefabs without spawning them.
                /// WARNING: Use with extreme caution! This will permanently discard queued prefabs.
                /// Only use this when you're certain you want to abandon the queued state.
                /// </summary>
                public void ClearPendingPrefabs()
                {
                        Logger.Log(
                                "ClearPendingPrefabs: Clearing all queued prefabs. This operation cannot be undone.",
                                LogLevel.Warning
                        );

                        // Clear would need to be implemented in PrefabManager
                        // For now, just log the intent
                        if (prefabManager != null)
                        {
                                Logger.Log("ClearPendingPrefabs: Operation not yet implemented in PrefabManager.", LogLevel.Error);
                        }
                }

                /// <summary>
                /// Gets diagnostic information about the current save/load state.
                /// Useful for debugging scene loading and prefab spawning issues.
                /// </summary>
                /// <returns>Formatted diagnostic string</returns>
                public string GetLoadStateDiagnostics()
                {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("=== Crystal Save Load State Diagnostics ===");
                        sb.AppendLine($"Current Save Data Loaded: {(CurrentSaveData != null ? "Yes" : "No")}");
                        
                        if (CurrentSaveData != null)
                        {
                                sb.AppendLine($"  - Saved Prefabs: {CurrentSaveData.Prefabs?.Count ?? 0}");
                                sb.AppendLine($"  - Last Active Scene: {CurrentSaveData.LastActiveScene ?? "None"}");
                        }

                        sb.AppendLine($"Active Scene: {SceneManager.GetActiveScene().name}");
                        sb.AppendLine($"Loaded Scenes: {SceneManager.sceneCount}");
                        
                        for (int i = 0; i < SceneManager.sceneCount; i++)
                        {
                                Scene scene = SceneManager.GetSceneAt(i);
                                sb.AppendLine($"  [{i}] {scene.name} (Active: {scene == SceneManager.GetActiveScene()})");
                        }

                        var pendingScenes = GetScenesWithPendingPrefabs();
                        sb.AppendLine($"Scenes with Pending Prefabs: {pendingScenes.Count}");
                        foreach (var sceneName in pendingScenes)
                        {
                                sb.AppendLine($"  - {sceneName}");
                        }

                        return sb.ToString();
                }

                #endregion

                #region Scene Load Orchestrator Registration

                /// <summary>
                /// Registers a custom scene load orchestrator to receive callbacks during the scene loading lifecycle.
                /// Use this to integrate custom loaders (Addressables, asset bundles, etc.) with Crystal Save.
                /// </summary>
                /// <param name="orchestrator">The orchestrator implementation to register</param>
                /// <remarks>
                /// Orchestrators receive callbacks at critical points:
                /// - OnScenePreLoad: Before scene load starts (prime prefab data here)
                /// - OnSceneWillActivate: After load completes, before activation
                /// - OnSceneActivated: After scene becomes active
                /// - OnSceneUnloaded: After scene unloads
                /// 
                /// Example:
                /// <code>
                /// public class MyLoader : SceneLoadOrchestratorBase
                /// {
                ///     public override async Task OnScenePreLoad(string sceneName)
                ///     {
                ///         await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(0);
                ///     }
                /// }
                /// 
                /// // Register
                /// SaveManager.Instance.RegisterSceneLoadOrchestrator(new MyLoader());
                /// </code>
                /// </remarks>
                public void RegisterSceneLoadOrchestrator(ISceneLoadOrchestrator orchestrator)
                {
                        if (orchestrator == null)
                        {
                                Logger.Log("[SCENELOAD] RegisterSceneLoadOrchestrator: orchestrator is null.", LogLevel.Warning);
                                return;
                        }

                        if (!sceneLoadOrchestrators.Contains(orchestrator))
                        {
                                sceneLoadOrchestrators.Add(orchestrator);
                                Logger.Log($"[SCENELOAD] Registered scene load orchestrator: {orchestrator.GetType().Name}", LogLevel.Info);
                        }
                        else
                        {
                                Logger.Log($"[SCENELOAD] Scene load orchestrator already registered: {orchestrator.GetType().Name}", LogLevel.Warning);
                        }
                }

                /// <summary>
                /// Unregisters a previously registered scene load orchestrator.
                /// </summary>
                /// <param name="orchestrator">The orchestrator to unregister</param>
                public void UnregisterSceneLoadOrchestrator(ISceneLoadOrchestrator orchestrator)
                {
                        if (orchestrator == null)
                        {
                                Logger.Log("[SCENELOAD] UnregisterSceneLoadOrchestrator: orchestrator is null.", LogLevel.Warning);
                                return;
                        }

                        if (sceneLoadOrchestrators.Remove(orchestrator))
                        {
                                Logger.Log($"[SCENELOAD] Unregistered scene load orchestrator: {orchestrator.GetType().Name}", LogLevel.Info);
                        }
                }

                /// <summary>
                /// Internal method to invoke OnScenePreLoad on all registered orchestrators.
                /// </summary>
                internal async Task InvokeOrchestratorPreLoad(string sceneName)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        await orchestrator.OnScenePreLoad(sceneName);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnScenePreLoad failed: {ex.Message}", LogLevel.Error);
                                }
                        }
                }

                /// <summary>
                /// Internal method to invoke OnSceneWillActivate on all registered orchestrators.
                /// </summary>
                internal async Task InvokeOrchestratorWillActivate(Scene scene)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        await orchestrator.OnSceneWillActivate(scene);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnSceneWillActivate failed: {ex.Message}", LogLevel.Error);
                                }
                        }
                }

                /// <summary>
                /// Internal method to invoke OnSceneActivated on all registered orchestrators.
                /// </summary>
                internal void InvokeOrchestratorActivated(Scene scene)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        orchestrator.OnSceneActivated(scene);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnSceneActivated failed: {ex.Message}", LogLevel.Error);
                                }
                        }
                }

                /// <summary>
                /// Internal method to invoke OnSceneUnloaded on all registered orchestrators.
                /// </summary>
                internal void InvokeOrchestratorUnloaded(Scene scene)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        orchestrator.OnSceneUnloaded(scene);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnSceneUnloaded failed: {ex.Message}", LogLevel.Error);
                                }
                        }
                }

                #endregion

                #region Save Sharing (Export / Import)

                /* ============================================================ *
                 *  Low-level helpers (byte[] in / byte[] out)                    *
                 * ============================================================ */

                /// <summary>
                /// Exports the save data for a given slot as portable bytes that can be
                /// shared with another device or user. The receiving side calls
                /// <see cref="ImportSaveAsync"/> to install the data into a local slot.
                /// </summary>
                /// <remarks>
                /// <para>
                /// Works regardless of whether encryption or compression is enabled.
                /// When encryption is ON the data is decrypted with the local key so
                /// the output is always plain MemoryPack-serialized <see cref="SaveData"/>.
                /// When encryption is OFF the on-disk bytes are already plain, so the
                /// method simply strips compression (if any) and returns the raw payload.
                /// </para>
                /// <para>
                /// For a one-click solution that bundles save data <b>and</b> metadata
                /// <b>and</b> screenshots into a single shareable file, use
                /// <see cref="ExportSaveBundleAsync(int)"/> instead.
                /// </para>
                /// </remarks>
                /// <param name="slotNumber">Slot number to export.</param>
                /// <returns>
                /// A <c>byte[]</c> containing the portable save payload, or <c>null</c>
                /// if the slot does not exist or contains no data.
                /// </returns>
                public async Task<byte[]> ExportSaveAsync(int slotNumber)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("ExportSaveAsync: SaveManager is not initialised yet.", LogLevel.Warning);
                                return null;
                        }

                        SaveSlot slot = GetSaveSlot(slotNumber);
                        if (slot == null)
                        {
                                Logger.Log($"ExportSaveAsync: Slot {slotNumber} does not exist.", LogLevel.Warning);
                                return null;
                        }

                        // Read the raw on-disk (or cloud) bytes
                        byte[] raw;
                        if (saveSettings.enableCloudSave)
                                raw = await SaveSystem.LoadAsync(slot).ConfigureAwait(false);
                        else
                                raw = SaveSystem.Load(slot);

                        if (raw == null || raw.Length == 0)
                        {
                                Logger.Log($"ExportSaveAsync: Slot {slotNumber} is empty.", LogLevel.Warning);
                                return null;
                        }

                        // Strip device-specific encryption
                        byte[] decrypted = SaveOperations.MaybeDecrypt(raw);
                        if (decrypted == null || decrypted.Length == 0)
                        {
                                Logger.Log("ExportSaveAsync: Decryption failed — the save may have been " +
                                           "encrypted with a different key.", LogLevel.Error);
                                return null;
                        }

                        // Decompress so the output is fully portable
                        byte[] plain = SaveOperations.MaybeDecompress(decrypted);

                        // Validate that the bytes are actually a valid SaveData
                        try
                        {
                                var probe = Serializer.Deserialize<SaveData>(plain);
                                if (probe == null)
                                {
                                        Logger.Log("ExportSaveAsync: Deserialization sanity-check failed.", LogLevel.Error);
                                        return null;
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ExportSaveAsync: Deserialization sanity-check threw: {ex.Message}", LogLevel.Error);
                                return null;
                        }

                        Logger.Log($"ExportSaveAsync: Exported {plain.Length} bytes from slot {slotNumber}.",
                                   LogCategory.SaveManager, LogLevel.Info);
                        return plain;
                }

                /// <summary>
                /// Imports portable save bytes (produced by <see cref="ExportSaveAsync"/>
                /// on any device) into a local save slot. The data is compressed and
                /// encrypted with the <b>local</b> device's key (when those features are
                /// enabled) before being written, so subsequent <c>Load</c> calls work
                /// transparently. Slot metadata is also written so the slot appears
                /// correctly in any UI (name, timestamp, last scene, etc.).
                /// </summary>
                /// <remarks>
                /// Works with any combination of encryption/compression settings.
                /// The imported data will be stored using whatever settings the local
                /// install is configured with.
                /// </remarks>
                /// <param name="portableBytes">
                /// The unencrypted MemoryPack payload returned by
                /// <see cref="ExportSaveAsync"/>.
                /// </param>
                /// <param name="slotNumber">Target slot number.</param>
                /// <returns><c>true</c> if the import succeeded.</returns>
                public async Task<bool> ImportSaveAsync(byte[] portableBytes, int slotNumber)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("ImportSaveAsync: SaveManager is not initialised yet.", LogLevel.Warning);
                                return false;
                        }

                        if (portableBytes == null || portableBytes.Length == 0)
                        {
                                Logger.Log("ImportSaveAsync: portableBytes is null or empty.", LogLevel.Warning);
                                return false;
                        }

                        SaveSlot slot = GetSaveSlot(slotNumber);
                        if (slot == null)
                        {
                                Logger.Log($"ImportSaveAsync: Slot {slotNumber} does not exist.", LogLevel.Warning);
                                return false;
                        }

                        // Validate that the incoming bytes are a valid SaveData
                        SaveData importedData;
                        try
                        {
                                importedData = Serializer.Deserialize<SaveData>(portableBytes);
                                if (importedData == null)
                                {
                                        Logger.Log("ImportSaveAsync: Failed to deserialize the portable save data.", LogLevel.Error);
                                        return false;
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ImportSaveAsync: Deserialization failed: {ex.Message}", LogLevel.Error);
                                return false;
                        }

                        // Re-apply local compression + encryption so the save
                        // integrates seamlessly with the local install.
                        byte[] compressed = SaveOperations.MaybeCompress(portableBytes);
                        byte[] blob       = SaveOperations.MaybeEncrypt(compressed);

                        // Write to local storage
                        try
                        {
                                if (saveSettings.enableCloudSave)
                                        await SaveSystem.SaveAsync(blob, slot).ConfigureAwait(false);
                                else
                                        SaveSystem.Save(blob, slot);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ImportSaveAsync: Write failed: {ex.Message}", LogLevel.Error);
                                return false;
                        }

                        // Update slot metadata so the UI reflects the imported save
                        slot.LastSaved = importedData.LastSaved;
                        slot.LastActiveScene = importedData.LastActiveScene;

                        try
                        {
                                await SaveSystem.SaveSlotMetadataAsync(slot).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ImportSaveAsync: Metadata write warning: {ex.Message}", LogLevel.Warning);
                                // Non-fatal — the save data itself was written successfully.
                        }

                        Logger.Log($"ImportSaveAsync: Imported {portableBytes.Length} bytes into slot {slotNumber}.",
                                   LogCategory.SaveManager, LogLevel.Info);
                        return true;
                }

                /* ============================================================ *
                 *  One-click bundle API (.crystalsave)                          *
                 *                                                              *
                 *  Bundles save data + slot metadata + screenshot into a        *
                 *  single GZip-compressed file that any other install of the    *
                 *  same game can import with ImportSaveBundleAsync.             *
                 *                                                              *
                 *  File layout (GZip-compressed):                              *
                 *   [6 B] magic   "CSAVBX"                                     *
                 *   [1 B] version  0x01                                        *
                 *   [2 B] entry count (little-endian ushort)                   *
                 *   per entry:                                                 *
                 *     [2 B] name length (LE ushort)                            *
                 *     [n B] UTF-8 name                                         *
                 *     [4 B] data length (LE int)                               *
                 *     [m B] data                                               *
                 *                                                              *
                 *  Entries:                                                     *
                 *    "manifest.json"  – JSON with slot metadata                *
                 *    "savedata.bin"   – portable MemoryPack bytes              *
                 *    "screenshot.png" – screenshot bytes (optional)            *
                 * ============================================================ */

                // Bundle magic bytes
                private static readonly byte[] BundleMagic = { (byte)'C', (byte)'S', (byte)'A', (byte)'V', (byte)'B', (byte)'X' };
                private const byte BundleVersion = 0x01;

                /// <summary>
                /// Exports a save slot as a single <c>.crystalsave</c> bundle file
                /// containing save data, slot metadata and screenshot. The file is
                /// written to a <c>CrystalSave Exports</c> folder inside the game's
                /// persistent data path and the full path is returned so you can
                /// display it to the player or open it with a share dialog.
                /// </summary>
                /// <remarks>
                /// <para>
                /// This is the recommended method for implementing a "Share Save"
                /// button. It handles encryption, compression, metadata, and
                /// screenshots automatically — just wire it to a UI button:
                /// </para>
                /// <code>
                /// async void OnExportClicked()
                /// {
                ///     string path = await SaveManager.Instance.ExportSaveBundleAsync(slotNumber: 1);
                ///     if (path != null)
                ///         Debug.Log($"Save exported to: {path}");
                /// }
                /// </code>
                /// <para>
                /// On the receiving device, call
                /// <see cref="ImportSaveBundleAsync(string, int)"/> with the path to
                /// the <c>.crystalsave</c> file.
                /// </para>
                /// </remarks>
                /// <param name="slotNumber">Slot to export.</param>
                /// <returns>
                /// The absolute path to the written <c>.crystalsave</c> file, or
                /// <c>null</c> if the export failed.
                /// </returns>
                public async Task<string> ExportSaveBundleAsync(int slotNumber)
                {
                        string folder = Path.Combine(Application.persistentDataPath, "CrystalSave Exports");
                        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string fileName = $"Slot_{slotNumber}_{ts}.crystalsave";
                        string destPath = Path.Combine(folder, fileName);

                        bool ok = await ExportSaveBundleAsync(slotNumber, destPath);
                        return ok ? destPath : null;
                }

                /// <summary>
                /// Exports a save slot as a single <c>.crystalsave</c> bundle file to
                /// a caller-specified path. Identical to
                /// <see cref="ExportSaveBundleAsync(int)"/> but lets you choose where
                /// the file is written.
                /// </summary>
                /// <param name="slotNumber">Slot to export.</param>
                /// <param name="destinationPath">Absolute file path to write to.</param>
                /// <returns><c>true</c> if the export succeeded.</returns>
                public async Task<bool> ExportSaveBundleAsync(int slotNumber, string destinationPath)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("ExportSaveBundleAsync: SaveManager is not initialised yet.", LogLevel.Warning);
                                return false;
                        }

                        // 1) Get portable save bytes
                        byte[] saveBytes = await ExportSaveAsync(slotNumber);
                        if (saveBytes == null) return false;

                        SaveSlot slot = GetSaveSlot(slotNumber);

                        // 2) Collect screenshot bytes (if any)
                        byte[] screenshotBytes = null;
                        string screenshotExt = null;
                        if (saveSettings.enableScreenshots &&
                            slot != null &&
                            !string.IsNullOrEmpty(slot.ScreenshotFileName))
                        {
                                screenshotExt = Path.GetExtension(slot.ScreenshotFileName);
                                string screenshotFolder = Path.Combine(_rootPath, saveSettings.screenshotFolderName);
                                string screenshotPath = Path.Combine(screenshotFolder, slot.ScreenshotFileName);
                                if (File.Exists(screenshotPath))
                                {
                                        try
                                        {
                                                screenshotBytes = File.ReadAllBytes(screenshotPath);
                                                // Decrypt screenshot if it was encrypted on disk
                                                if (screenshotBytes != null &&
                                                    saveSettings.enableEncryption &&
                                                    saveSettings.encryptScreenshots)
                                                {
                                                        screenshotBytes = SaveOperations.MaybeDecrypt(screenshotBytes);
                                                }
                                        }
                                        catch (Exception ex)
                                        {
                                                Logger.Log($"ExportSaveBundleAsync: Could not read screenshot: {ex.Message}",
                                                           LogLevel.Warning);
                                        }
                                }
                        }

                        // 3) Build manifest JSON (simple, no external dependencies)
                        string manifest = BuildBundleManifest(slot, screenshotExt);

                        // 4) Pack entries into the bundle binary format and
                        //    GZip-compress the whole thing
                        byte[] bundle = PackBundle(manifest, saveBytes, screenshotBytes, screenshotExt);

                        // 5) Write to disk
                        try
                        {
                                string dir = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(dir))
                                        Directory.CreateDirectory(dir);

                                await Task.Run(() => File.WriteAllBytes(destinationPath, bundle));

                                Logger.Log($"ExportSaveBundleAsync: Bundle written ({bundle.Length} bytes) → {destinationPath}",
                                           LogCategory.SaveManager, LogLevel.Info);
                                return true;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ExportSaveBundleAsync: File write failed: {ex.Message}", LogLevel.Error);
                                return false;
                        }
                }

                /// <summary>
                /// Imports a <c>.crystalsave</c> bundle file (created by
                /// <see cref="ExportSaveBundleAsync(int)"/>) into a local save slot.
                /// Restores save data, slot metadata (name, timestamp, scene) and the
                /// screenshot — everything needed for a seamless experience on the
                /// receiving device.
                /// </summary>
                /// <remarks>
                /// <para>
                /// This is the counterpart to <see cref="ExportSaveBundleAsync(int)"/>.
                /// Wire it to an "Import Save" button:
                /// </para>
                /// <code>
                /// async void OnImportClicked(string filePath)
                /// {
                ///     bool ok = await SaveManager.Instance.ImportSaveBundleAsync(filePath, slotNumber: 1);
                ///     if (ok)
                ///         Debug.Log("Save imported successfully!");
                /// }
                /// </code>
                /// </remarks>
                /// <param name="bundlePath">
                /// Absolute path to the <c>.crystalsave</c> file.
                /// </param>
                /// <param name="slotNumber">Target slot to import into.</param>
                /// <returns><c>true</c> if the import succeeded.</returns>
                public async Task<bool> ImportSaveBundleAsync(string bundlePath, int slotNumber)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("ImportSaveBundleAsync: SaveManager is not initialised yet.", LogLevel.Warning);
                                return false;
                        }

                        if (!File.Exists(bundlePath))
                        {
                                Logger.Log($"ImportSaveBundleAsync: File not found: {bundlePath}", LogLevel.Error);
                                return false;
                        }

                        SaveSlot slot = GetSaveSlot(slotNumber);
                        if (slot == null)
                        {
                                Logger.Log($"ImportSaveBundleAsync: Slot {slotNumber} does not exist.", LogLevel.Warning);
                                return false;
                        }

                        // 1) Read and unpack the bundle
                        byte[] bundleBytes;
                        try
                        {
                                bundleBytes = await Task.Run(() => File.ReadAllBytes(bundlePath));
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ImportSaveBundleAsync: File read failed: {ex.Message}", LogLevel.Error);
                                return false;
                        }

                        Dictionary<string, byte[]> entries;
                        try
                        {
                                entries = UnpackBundle(bundleBytes);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ImportSaveBundleAsync: Bundle is invalid: {ex.Message}", LogLevel.Error);
                                return false;
                        }

                        // 2) Extract save data (required)
                        if (!entries.TryGetValue("savedata.bin", out byte[] saveBytes) ||
                            saveBytes == null || saveBytes.Length == 0)
                        {
                                Logger.Log("ImportSaveBundleAsync: Bundle does not contain save data.", LogLevel.Error);
                                return false;
                        }

                        // 3) Import the save data (handles compression, encryption,
                        //    writing, and metadata persistence)
                        bool imported = await ImportSaveAsync(saveBytes, slotNumber);
                        if (!imported) return false;

                        // 4) Parse manifest and restore slot metadata
                        if (entries.TryGetValue("manifest.json", out byte[] manifestBytes) &&
                            manifestBytes != null && manifestBytes.Length > 0)
                        {
                                try
                                {
                                        string json = System.Text.Encoding.UTF8.GetString(manifestBytes);
                                        ApplyBundleManifest(slot, json);
                                        await SaveSystem.SaveSlotMetadataAsync(slot).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"ImportSaveBundleAsync: Manifest restore warning: {ex.Message}",
                                                   LogLevel.Warning);
                                }
                        }

                        // 5) Restore screenshot
                        string screenshotEntry = FindScreenshotEntry(entries);
                        if (screenshotEntry != null && entries.TryGetValue(screenshotEntry, out byte[] shotBytes) &&
                            shotBytes != null && shotBytes.Length > 0)
                        {
                                try
                                {
                                        string ext = Path.GetExtension(screenshotEntry); // e.g. ".png"
                                        string screenshotFolder = Path.Combine(_rootPath, saveSettings.screenshotFolderName);
                                        Directory.CreateDirectory(screenshotFolder);

                                        string newName = $"Slot_{slotNumber}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}";
                                        string dstPath = Path.Combine(screenshotFolder, newName);

                                        byte[] toWrite = shotBytes;
                                        // Re-encrypt screenshot if local settings require it
                                        if (saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                                                toWrite = SaveOperations.MaybeEncrypt(shotBytes);

                                        File.WriteAllBytes(dstPath, toWrite);

                                        slot.ScreenshotFileName = newName;
                                        await SaveSystem.SaveSlotMetadataAsync(slot).ConfigureAwait(false);

                                        Logger.Log($"ImportSaveBundleAsync: Screenshot restored → {newName}",
                                                   LogCategory.SaveManager, LogLevel.Info);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"ImportSaveBundleAsync: Screenshot restore warning: {ex.Message}",
                                                   LogLevel.Warning);
                                }
                        }

                        Logger.Log($"ImportSaveBundleAsync: Bundle imported into slot {slotNumber}.",
                                   LogCategory.SaveManager, LogLevel.Info);
                        return true;
                }

                /* ──────────────────────────────────────────────────────────── *
                 *  Bundle helpers (private)                                    *
                 * ──────────────────────────────────────────────────────────── */

                /// <summary>
                /// Builds a minimal JSON manifest string from slot metadata.
                /// Hand-written to avoid any JSON library dependency.
                /// </summary>
                private static string BuildBundleManifest(SaveSlot slot, string screenshotExt)
                {
                        string slotName = EscapeJson(slot?.SlotName ?? "");
                        string lastScene = EscapeJson(slot?.LastActiveScene ?? "");
                        string lastSaved = slot?.LastSaved.ToString("o") ?? DateTime.MinValue.ToString("o");
                        int slotNumber = slot?.SlotNumber ?? 0;
                        string shotExt = EscapeJson(screenshotExt ?? "");

                        // Build custom metadata entries
                        string metaJson = "{}";
                        if (slot?.CustomMetadata != null && slot.CustomMetadata.Count > 0)
                        {
                                var sb = new System.Text.StringBuilder();
                                sb.Append('{');
                                bool first = true;
                                foreach (var kvp in slot.CustomMetadata)
                                {
                                        if (!first) sb.Append(',');
                                        sb.Append('"').Append(EscapeJson(kvp.Key)).Append("\":\"")
                                          .Append(EscapeJson(kvp.Value)).Append('"');
                                        first = false;
                                }
                                sb.Append('}');
                                metaJson = sb.ToString();
                        }

                        return "{"
                             + $"\"version\":1,"
                             + $"\"slotNumber\":{slotNumber},"
                             + $"\"slotName\":\"{slotName}\","
                             + $"\"lastSaved\":\"{lastSaved}\","
                             + $"\"lastActiveScene\":\"{lastScene}\","
                             + $"\"screenshotExt\":\"{shotExt}\","
                             + $"\"customMetadata\":{metaJson}"
                             + "}";
                }

                /// <summary>
                /// Applies parsed manifest JSON back to a <see cref="SaveSlot"/>.
                /// Uses simple string parsing to stay dependency-free.
                /// </summary>
                private static void ApplyBundleManifest(SaveSlot slot, string json)
                {
                        slot.SlotName = ReadJsonString(json, "slotName") ?? slot.SlotName;
                        slot.LastActiveScene = ReadJsonString(json, "lastActiveScene") ?? slot.LastActiveScene;

                        string savedStr = ReadJsonString(json, "lastSaved");
                        if (!string.IsNullOrEmpty(savedStr) &&
                            DateTime.TryParse(savedStr, System.Globalization.CultureInfo.InvariantCulture,
                                              System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                        {
                                slot.LastSaved = dt;
                        }

                        // Restore custom metadata
                        string metaBlock = ReadJsonObject(json, "customMetadata");
                        if (!string.IsNullOrEmpty(metaBlock) && metaBlock != "{}")
                        {
                                slot.CustomMetadata ??= new Dictionary<string, string>();
                                ParseFlatJsonObject(metaBlock, slot.CustomMetadata);
                        }
                }

                /// <summary>
                /// Packs entries into the CSAVBX binary format and GZip-compresses the
                /// result. No external dependencies required.
                /// </summary>
                private static byte[] PackBundle(string manifest, byte[] saveBytes,
                                                 byte[] screenshotBytes, string screenshotExt)
                {
                        using var raw = new MemoryStream();
                        using var writer = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true);

                        // Header
                        writer.Write(BundleMagic);
                        writer.Write(BundleVersion);

                        // Count entries: manifest + savedata + optional screenshot
                        ushort count = (ushort)(screenshotBytes != null && screenshotBytes.Length > 0 ? 3 : 2);
                        writer.Write(count);

                        // Entry 1: manifest.json
                        WriteEntry(writer, "manifest.json", System.Text.Encoding.UTF8.GetBytes(manifest));

                        // Entry 2: savedata.bin
                        WriteEntry(writer, "savedata.bin", saveBytes);

                        // Entry 3: screenshot (optional)
                        if (screenshotBytes != null && screenshotBytes.Length > 0)
                        {
                                string ext = string.IsNullOrEmpty(screenshotExt) ? ".png" : screenshotExt;
                                WriteEntry(writer, $"screenshot{ext}", screenshotBytes);
                        }

                        writer.Flush();
                        raw.Position = 0;

                        // GZip compress the whole bundle
                        using var compressed = new MemoryStream();
                        using (var gz = new System.IO.Compression.GZipStream(
                                   compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                        {
                                raw.CopyTo(gz);
                        }

                        return compressed.ToArray();
                }

                /// <summary>
                /// GZip-decompresses and unpacks a CSAVBX bundle into named entries.
                /// </summary>
                private static Dictionary<string, byte[]> UnpackBundle(byte[] bundle)
                {
                        // GZip decompress
                        byte[] raw;
                        using (var input = new MemoryStream(bundle))
                        using (var gz = new System.IO.Compression.GZipStream(
                                   input, System.IO.Compression.CompressionMode.Decompress))
                        using (var output = new MemoryStream())
                        {
                                gz.CopyTo(output);
                                raw = output.ToArray();
                        }

                        using var ms = new MemoryStream(raw);
                        using var reader = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true);

                        // Validate magic
                        byte[] magic = reader.ReadBytes(6);
                        for (int i = 0; i < BundleMagic.Length; i++)
                        {
                                if (magic[i] != BundleMagic[i])
                                        throw new InvalidDataException("Not a valid CrystalSave bundle file.");
                        }

                        byte version = reader.ReadByte();
                        if (version > BundleVersion)
                                throw new InvalidDataException($"Unsupported bundle version: {version}");

                        ushort count = reader.ReadUInt16();
                        var entries = new Dictionary<string, byte[]>(count, StringComparer.OrdinalIgnoreCase);

                        for (int i = 0; i < count; i++)
                        {
                                ushort nameLen = reader.ReadUInt16();
                                string name = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(nameLen));
                                int dataLen = reader.ReadInt32();
                                byte[] data = reader.ReadBytes(dataLen);
                                entries[name] = data;
                        }

                        return entries;
                }

                private static void WriteEntry(BinaryWriter writer, string name, byte[] data)
                {
                        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
                        writer.Write((ushort)nameBytes.Length);
                        writer.Write(nameBytes);
                        writer.Write(data.Length);
                        writer.Write(data);
                }

                /// <summary>
                /// Finds the screenshot entry name in the unpacked bundle (if any).
                /// </summary>
                private static string FindScreenshotEntry(Dictionary<string, byte[]> entries)
                {
                        foreach (var key in entries.Keys)
                        {
                                if (key.StartsWith("screenshot", StringComparison.OrdinalIgnoreCase))
                                        return key;
                        }
                        return null;
                }

                /* ── Minimal JSON helpers (no external dependencies) ───────── */

                private static string EscapeJson(string s)
                {
                        if (string.IsNullOrEmpty(s)) return s;
                        return s.Replace("\\", "\\\\")
                                .Replace("\"", "\\\"")
                                .Replace("\n", "\\n")
                                .Replace("\r", "\\r")
                                .Replace("\t", "\\t");
                }

                private static string ReadJsonString(string json, string key)
                {
                        string needle = $"\"{key}\":\"";
                        int start = json.IndexOf(needle, StringComparison.Ordinal);
                        if (start < 0) return null;
                        start += needle.Length;
                        var sb = new System.Text.StringBuilder();
                        for (int i = start; i < json.Length; i++)
                        {
                                char c = json[i];
                                if (c == '\\' && i + 1 < json.Length)
                                {
                                        sb.Append(json[i + 1]);
                                        i++;
                                        continue;
                                }
                                if (c == '"') break;
                                sb.Append(c);
                        }
                        return sb.ToString();
                }

                private static string ReadJsonObject(string json, string key)
                {
                        string needle = $"\"{key}\":";
                        int start = json.IndexOf(needle, StringComparison.Ordinal);
                        if (start < 0) return null;
                        start += needle.Length;
                        // Find the opening brace
                        while (start < json.Length && json[start] != '{') start++;
                        if (start >= json.Length) return null;
                        int depth = 0;
                        int end = start;
                        for (int i = start; i < json.Length; i++)
                        {
                                if (json[i] == '{') depth++;
                                else if (json[i] == '}') depth--;
                                if (depth == 0) { end = i; break; }
                        }
                        return json.Substring(start, end - start + 1);
                }

                private static void ParseFlatJsonObject(string json, Dictionary<string, string> target)
                {
                        // Very simple parser for {"key":"value","key2":"value2"}
                        int i = 0;
                        while (i < json.Length)
                        {
                                int keyStart = json.IndexOf('"', i);
                                if (keyStart < 0) break;
                                int keyEnd = json.IndexOf('"', keyStart + 1);
                                if (keyEnd < 0) break;
                                string k = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                                int valStart = json.IndexOf('"', keyEnd + 1);
                                if (valStart < 0) break;
                                int valEnd = valStart + 1;
                                while (valEnd < json.Length)
                                {
                                        if (json[valEnd] == '\\') { valEnd += 2; continue; }
                                        if (json[valEnd] == '"') break;
                                        valEnd++;
                                }
                                string v = json.Substring(valStart + 1, valEnd - valStart - 1)
                                               .Replace("\\\"", "\"").Replace("\\\\", "\\");
                                target[k] = v;
                                i = valEnd + 1;
                        }
                }

                /* ============================================================ *
                 *  Legacy file-based helpers (kept for backward compatibility)  *
                 * ============================================================ */

                /// <summary>
                /// Exports a save slot to a file on disk. The file contains
                /// portable, unencrypted data that can be transferred to
                /// another device and imported with
                /// <see cref="ImportSaveFromFileAsync"/>.
                /// </summary>
                /// <param name="slotNumber">Slot to export.</param>
                /// <param name="destinationPath">
                /// Absolute file path to write to (e.g.
                /// <c>Path.Combine(Application.persistentDataPath, "shared_save.crystalsave")</c>).
                /// </param>
                /// <returns><c>true</c> if the file was written successfully.</returns>
                public async Task<bool> ExportSaveToFileAsync(int slotNumber, string destinationPath)
                {
                        byte[] portable = await ExportSaveAsync(slotNumber);
                        if (portable == null) return false;

                        try
                        {
                                string dir = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(dir))
                                        Directory.CreateDirectory(dir);

                                await Task.Run(() => File.WriteAllBytes(destinationPath, portable));

                                Logger.Log($"ExportSaveToFileAsync: Written {portable.Length} bytes to {destinationPath}.",
                                           LogCategory.SaveManager, LogLevel.Info);
                                return true;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ExportSaveToFileAsync: File write failed: {ex.Message}", LogLevel.Error);
                                return false;
                        }
                }

                /// <summary>
                /// Imports a save from a file previously created by
                /// <see cref="ExportSaveToFileAsync"/> (or any source that
                /// provides the same unencrypted MemoryPack format).
                /// </summary>
                /// <param name="sourcePath">Path to the portable save file.</param>
                /// <param name="slotNumber">Target slot.</param>
                /// <returns><c>true</c> if the import succeeded.</returns>
                public async Task<bool> ImportSaveFromFileAsync(string sourcePath, int slotNumber)
                {
                        if (!File.Exists(sourcePath))
                        {
                                Logger.Log($"ImportSaveFromFileAsync: File not found: {sourcePath}", LogLevel.Error);
                                return false;
                        }

                        byte[] portable;
                        try
                        {
                                portable = await Task.Run(() => File.ReadAllBytes(sourcePath));
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ImportSaveFromFileAsync: File read failed: {ex.Message}", LogLevel.Error);
                                return false;
                        }

                        return await ImportSaveAsync(portable, slotNumber);
                }

                #endregion

                #endregion
    }
}
#endif
