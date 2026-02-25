#if ARAWN_REMEMBERME && MEMORYPACK
using UnityEngine;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demos
{
    /// <summary>
    /// Spawns a prefab at start, wires in-scene UI Buttons for gravity and restore.
    /// </summary>
    public class PrefabSpawnerWithUI : MonoBehaviour
    {
        [Header("Prefab & Spawn Point")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private Transform spawnPoint;

        [Header("Enable Gravity Button")]
        [Tooltip("Drag in your in-scene Button that turns on gravity for the spawned prefab")]
        [SerializeField] private Button enableGravityButton;

        [Header("Restore Button")]
        [Tooltip("Drag in your in-scene Button that calls RestoreSingleGameObject(...)")]
        [SerializeField] private Button restoreButton;

        [Header("SaveManager Unique ID")]
        [Tooltip("Passed to SaveManager.RestoreSingleGameObject(...)")]
        [SerializeField] private string uniqueID;

        private Rigidbody spawnedRigidbody;

        private void Start()
        {
            // 1) Check if the save system already restored an instance of this prefab
            GameObject spawned = null;
            var saveablePrefab = prefab != null ? prefab.GetComponent<SaveablePrefab>() : null;
            if (saveablePrefab != null && SaveManager.IsInitialized)
            {
                string prefabAssetID = saveablePrefab.PrefabAssetID;
                if (!string.IsNullOrEmpty(prefabAssetID))
                {
                    string existingUID = SaveManager.Instance.GetCurrentUniqueIDFromPrefabAssetID(prefabAssetID);
                    if (!string.IsNullOrEmpty(existingUID))
                    {
                        spawned = SaveManager.Instance.FindGameObjectByUniqueID(existingUID);
                    }
                }
            }

            // 2) Only instantiate if no restored instance was found
            if (spawned == null)
                spawned = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            spawnedRigidbody = spawned.GetComponent<Rigidbody>() 
                             ?? spawned.AddComponent<Rigidbody>();
            spawnedRigidbody.useGravity = false;

            // 2) Hook up the gravity button
            if (enableGravityButton != null)
            {
                enableGravityButton.onClick.AddListener(() =>
                {
                    spawnedRigidbody.useGravity = true;
                });
            }
            else
            {
                Debug.LogWarning($"{nameof(PrefabSpawnerWithUI)}: enableGravityButton not assigned.");
            }

            // 3) Hook up the restore button
            if (restoreButton != null)
            {
                restoreButton.onClick.AddListener(async () =>
                {
                    // This will call:
                    //   RestoreSingleGameObject(uniqueID, null)
                    // which internally does:
                    //   data ??= CurrentSaveData;
                    //   1️⃣ Find by UniqueID
                    //   2️⃣ Fallback to PrefabAssetID
                    //   3️⃣ Fallback to saved PrefabData → coroutine
                    //   4️⃣ Warn if nothing found
                    //SaveManager.Instance.RestoreSingleGameObjectsFromMostRecentSlotAsync(uniqueID);
                    _ = await SaveManager.Instance.RestoreSingleGameObjectFromCurrentDataAsync(uniqueID);
                });
            }
            else
            {
                Debug.LogWarning($"{nameof(PrefabSpawnerWithUI)}: restoreButton not assigned.");
            }
        }
    }
}
#endif
