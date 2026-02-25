#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using CSLogger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles restoration of destroyed prefabs including retry logic.
    /// </summary>
    public class PrefabRestoreService
    {
        readonly SaveManager      manager;
        readonly PrefabManager    prefabManager;
        readonly GameObjectTracker tracker;

        readonly List<string> pendingRestores = new();

        public PrefabRestoreService(SaveManager manager,
                                    PrefabManager prefabManager,
                                    GameObjectTracker tracker)
        {
            this.manager       = manager;
            this.prefabManager = prefabManager;
            this.tracker       = tracker;
        }

        IEnumerator RestoreSinglePrefabCoroutine(SaveablePrefabData prefabData, SaveData data)
        {
            var existing = manager.FindGameObjectByUniqueID(prefabData.InstanceID, SaveManager.IdentifierType.UniqueID);
            if (existing != null)
            {
                DestroyHelper.DestroyWithLogging(existing,
                    "RestoreSinglePrefabCoroutine: removing stale instance");
                yield return null; // wait one frame for destruction
            }

            yield return prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { prefabData },
                data.DestroyedGameObjects,
                clearExistingPrefabs: false);

            var go = manager.FindGameObjectByUniqueID(prefabData.InstanceID, SaveManager.IdentifierType.UniqueID);
            if (go != null)
                manager.RestoreSingleGameObject(go, data);
            else
                CSLogger.Log($"RestoreSinglePrefab: after instantiation, GameObject '{prefabData.InstanceID}' not found.", LogLevel.Warning);
        }

        public void RestoreDestroyedPrefab(string uniqueID, SaveData data = null)
        {
            if (manager.IsLoading)
            {
                if (!pendingRestores.Contains(uniqueID))
                    pendingRestores.Add(uniqueID);

                CSLogger.Log($"RestoreDestroyedPrefab: queued '{uniqueID}' until loading completes.");
                return;
            }

            data ??= manager.CurrentSaveData;
            if (data == null)
            {
                CSLogger.Log($"RestoreDestroyedPrefab: no SaveData available for '{uniqueID}'.", LogCategory.PrefabManager, LogLevel.Error);
                return;
            }

            bool trackerMarked = tracker.IsGameObjectDestroyed(uniqueID);
            bool hasSnapshot = data.DestroyedObjectData != null && data.DestroyedObjectData.ContainsKey(uniqueID);
            var  sceneObj    = manager.FindGameObjectByUniqueID(uniqueID, SaveManager.IdentifierType.UniqueID);
            bool missingInScene = sceneObj == null;

            if (!trackerMarked && !hasSnapshot && !missingInScene)
            {
                CSLogger.Log($"RestoreDestroyedPrefab: ID '{uniqueID}' is not marked destroyed and exists in scene; aborting.");
                return;
            }
            if (!trackerMarked && (hasSnapshot || missingInScene))
            {
                CSLogger.Log($"RestoreDestroyedPrefab: proceeding for '{uniqueID}' based on {(hasSnapshot ? "saved destroyed snapshot" : "absence in scene")} despite tracker not being populated.");
            }

            var prefabData = data.Prefabs.FirstOrDefault(p => p.InstanceID == uniqueID);
            if (prefabData == null)
            {
                CSLogger.Log($"RestoreDestroyedPrefab: no prefab data for '{uniqueID}'.", LogCategory.PrefabManager, LogLevel.Warning);
                return;
            }

            // Safe to call even if it wasn't present
            tracker.RemoveDestroyedID(uniqueID);

            data.DestroyedGameObjects?.Remove(uniqueID);
            if (manager.CurrentSaveData != null && manager.CurrentSaveData != data)
                manager.CurrentSaveData.DestroyedGameObjects?.Remove(uniqueID);

            if (data.DestroyedObjectData != null)
                data.DestroyedObjectData.Remove(uniqueID);
            if (manager.CurrentSaveData != null && manager.CurrentSaveData != data &&
                manager.CurrentSaveData.DestroyedObjectData != null)
                manager.CurrentSaveData.DestroyedObjectData.Remove(uniqueID);

            manager.StartCoroutine(RestoreSinglePrefabCoroutine(prefabData, data));
        }

        /// <summary>
        /// Restores a destroyed prefab by its PrefabAssetID. Looks up the
        /// corresponding destroyed instance and forwards to
        /// <see cref="RestoreDestroyedPrefab(string, SaveData)"/>.
        /// </summary>
        public void RestoreDestroyedPrefabByAssetID(string prefabAssetID, SaveData data = null)
        {
            data ??= manager.CurrentSaveData;
            if (data == null)
            {
                CSLogger.Log($"RestoreDestroyedPrefabByAssetID: no SaveData available for '{prefabAssetID}'.", LogCategory.PrefabManager, LogLevel.Error);
                return;
            }

            var prefabData = data.Prefabs
                .FirstOrDefault(p =>
                    p.PrefabID == prefabAssetID &&
                    (
                        tracker.IsGameObjectDestroyed(p.InstanceID)
                        || (data.DestroyedObjectData != null && data.DestroyedObjectData.ContainsKey(p.InstanceID))
                        || manager.FindGameObjectByUniqueID(p.InstanceID, SaveManager.IdentifierType.UniqueID) == null
                    ));

            if (prefabData == null)
            {
                CSLogger.Log($"RestoreDestroyedPrefabByAssetID: no destroyed prefab data for asset ID '{prefabAssetID}'.", LogCategory.PrefabManager, LogLevel.Warning);
                return;
            }

            // If we matched via snapshot/absence, and tracker isn't populated, log for traceability
            if (!tracker.IsGameObjectDestroyed(prefabData.InstanceID))
            {
                bool hasSnapshot = data.DestroyedObjectData != null && data.DestroyedObjectData.ContainsKey(prefabData.InstanceID);
                var  obj        = manager.FindGameObjectByUniqueID(prefabData.InstanceID, SaveManager.IdentifierType.UniqueID);
                CSLogger.Log($"RestoreDestroyedPrefabByAssetID: selected '{prefabData.InstanceID}' for asset '{prefabAssetID}' based on {(hasSnapshot ? "snapshot" : (obj == null ? "absence in scene" : "unknown"))}.");
            }

            RestoreDestroyedPrefab(prefabData.InstanceID, data);
        }

        public void RestoreAllDestroyedPrefabs(SaveData data = null)
        {
            data ??= manager.CurrentSaveData;
            if (data == null)
            {
                CSLogger.Log("RestoreAllDestroyedPrefabs: no SaveData loaded.", LogCategory.PrefabManager, LogLevel.Error);
                return;
            }

            var destroyedCopy = new List<string>(tracker.GetDestroyedGameObjectIDs());
            foreach (var id in destroyedCopy)
                RestoreDestroyedPrefab(id, data);

            if (tracker.GetDestroyedGameObjectIDs().Count == 0)
                CSLogger.Log("All destroyed prefabs restored.");
            else
                CSLogger.Log($"Some destroyed prefabs remain: {tracker.GetDestroyedGameObjectIDs().Count}", LogCategory.PrefabManager, LogLevel.Warning);
        }

        /// <summary>
        /// Restores all destroyed prefabs from <see cref="SaveManager.CurrentSaveData"/>.
        /// </summary>
        /// <returns>True if data was available and restoration was triggered; false otherwise.</returns>
        public bool RestoreAllDestroyedPrefabsFromCurrentData()
        {
            var data = manager.CurrentSaveData;
            if (data == null)
            {
                CSLogger.Log("RestoreAllDestroyedPrefabsFromCurrentData: no current SaveData available.", LogCategory.PrefabManager, LogLevel.Error);
                return false;
            }

            RestoreAllDestroyedPrefabs(data);
            return true;
        }

        /// <summary>
        /// Attempts to restore all destroyed prefabs from <see cref="SaveManager.CurrentSaveData"/> with retry logic.
        /// </summary>
        /// <param name="maxRetries">Maximum number of attempts to find current data.</param>
        /// <param name="retryDelayMs">Delay in milliseconds between attempts.</param>
        /// <returns>True if restoration succeeded; false if no data was available.</returns>
        public async Task<bool> RestoreAllDestroyedPrefabsFromCurrentDataWithRetryAsync(
            int maxRetries = 3,
            int retryDelayMs = 500)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var data = manager.CurrentSaveData;
                if (data != null)
                {
                    RestoreAllDestroyedPrefabs(data);
                    return true;
                }

                CSLogger.Log($"RestoreAllDestroyedPrefabsFromCurrentData: attempt {attempt} found no data. Retrying in {retryDelayMs}ms…", LogCategory.PrefabManager, LogLevel.Warning);
                await Task.Delay(retryDelayMs);
            }

            CSLogger.Log("RestoreAllDestroyedPrefabsFromCurrentData: no SaveData available after retries.", LogCategory.PrefabManager, LogLevel.Error);
            return false;
        }

        public async Task<bool> RestoreDestroyedPrefabWithRetryAsync(
            string uniqueID,
            int    slotNumber,
            int    maxRetries   = 3,
            int    retryDelayMs = 500)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                SaveData data = null;
                try { data = await manager.LoadSaveDataForSlotAsync(slotNumber); }
                catch (System.Exception ex)
                {
                    CSLogger.Log($"Attempt {attempt}: error loading slot {slotNumber}: {ex.Message}", LogCategory.PrefabManager, LogLevel.Warning);
                }

                if (data != null)
                {
                    RestoreDestroyedPrefab(uniqueID, data);
                    return true;
                }

                CSLogger.Log($"RestoreDestroyedPrefab: attempt {attempt} found no data. Retrying in {retryDelayMs}ms…", LogCategory.PrefabManager, LogLevel.Warning);
                await Task.Delay(retryDelayMs);
            }

            CSLogger.Log($"RestoreDestroyedPrefab: all {maxRetries} attempts failed for '{uniqueID}' in slot {slotNumber}.", LogCategory.PrefabManager, LogLevel.Error);
            return false;
        }

        public async void RestoreDestroyedPrefab(string uniqueID, int slotNumber)
        {
            await RestoreDestroyedPrefabWithRetryAsync(uniqueID, slotNumber);
        }

        public async Task<bool> RestoreDestroyedPrefabByAssetIDWithRetryAsync(
            string prefabAssetID,
            int    slotNumber,
            int    maxRetries   = 3,
            int    retryDelayMs = 500)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                SaveData data = null;
                try { data = await manager.LoadSaveDataForSlotAsync(slotNumber); }
                catch (System.Exception ex)
                {
                    CSLogger.Log($"Attempt {attempt}: error loading slot {slotNumber}: {ex.Message}", LogCategory.PrefabManager, LogLevel.Warning);
                }

                if (data != null)
                {
                    RestoreDestroyedPrefabByAssetID(prefabAssetID, data);
                    return true;
                }

                await Task.Delay(retryDelayMs);
            }

            CSLogger.Log($"RestoreDestroyedPrefabByAssetID: all {maxRetries} attempts failed for '{prefabAssetID}' in slot {slotNumber}.", LogCategory.PrefabManager, LogLevel.Error);
            return false;
        }

        public async void RestoreDestroyedPrefabByAssetID(string prefabAssetID, int slotNumber)
        {
            await RestoreDestroyedPrefabByAssetIDWithRetryAsync(prefabAssetID, slotNumber);
        }

        public async Task<bool> RestoreAllDestroyedPrefabsWithRetryAsync(
            int slotNumber,
            int maxRetries = 3,
            int retryDelayMs = 500)
        {
            SaveData data = null;
            for (int i = 1; i <= maxRetries; i++)
            {
                try { data = await manager.LoadSaveDataForSlotAsync(slotNumber); }
                catch (System.Exception ex)
                {
                    CSLogger.Log($"Attempt {i} load failed: {ex.Message}", LogCategory.PrefabManager, LogLevel.Warning);
                }

                if (data != null)
                {
                    RestoreAllDestroyedPrefabs(data);
                    return true;
                }
                await Task.Delay(retryDelayMs);
            }

            CSLogger.Log($"RestoreAllDestroyedPrefabs: all {maxRetries} attempts failed for slot {slotNumber}.", LogCategory.PrefabManager, LogLevel.Error);
            return false;
        }

        public async void RestoreAllDestroyedPrefabs(int slotNumber)
        {
            await RestoreAllDestroyedPrefabsWithRetryAsync(slotNumber);
        }

        public void ProcessPendingRestores()
        {
            if (pendingRestores.Count == 0)
                return;

            var ids = pendingRestores.ToList();
            pendingRestores.Clear();

            foreach (var id in ids)
                RestoreDestroyedPrefab(id);
        }
    }
}
#endif
