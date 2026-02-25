#if ARAWN_REMEMBERME && MEMORYPACK
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using CSLogger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// Spawns a prefab that already carries <see cref="SaveablePrefab"/>.
    /// Allows destroying it directly or by <c>PrefabAssetID</c>.
    /// (Respawn / restore logic removed.)
    /// </summary>
    public sealed class PrefabSpawnerController : MonoBehaviour
    {
        // ───────────────────────────────────────── Inspector ─────────────────────────────────────────
        [Header("Prefab Settings")]
        [Tooltip("Prefab must carry a SaveablePrefab component.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Where to spawn the prefab. Optional - if not set, uses manual position.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Manual spawn position. Used when spawnPoint is not assigned.")]
        [SerializeField] private Vector3 manualSpawnPosition = Vector3.zero;

        [Tooltip("SaveablePrefab.PrefabAssetID used for destroy‑by‑ID.")]
        [SerializeField] private string prefabAssetID;

        [Header("UI Buttons")]
        [SerializeField] private Button spawnButton;
        [SerializeField] private Button destroyButton;
        [SerializeField] private Button destroyByIdButton;

        // ───────────────────────────────────────── Runtime ───────────────────────────────────────────
        private GameObject instance;

        private void Awake()
        {
            spawnButton?.onClick.AddListener(OnSpawnClicked);
            destroyButton?.onClick.AddListener(OnDestroyClicked);
            destroyByIdButton?.onClick.AddListener(OnDestroyByIdClicked);

            // Validation warnings for missing components
            //if (!prefab)            Debug.LogWarning("Prefab reference not assigned. Spawning will fail.", this);
            if (!spawnButton)       CSLogger.Log("[PrefabSpawnerController] Spawn Button not assigned.", LogLevel.Warning);
            if (!destroyButton)     CSLogger.Log("[PrefabSpawnerController] Destroy Button not assigned.", LogLevel.Warning);
            if (!destroyByIdButton) CSLogger.Log("[PrefabSpawnerController] Destroy-by-ID Button not assigned.", LogLevel.Warning);
        }

        // ─────────────────────────────── Button callbacks ────────────────────────────────
        private void OnSpawnClicked()
        {
            if (!prefab)
            {
                //Debug.LogError("Cannot spawn: Prefab reference is null. Please assign a prefab in the Inspector.", this);
                return;
            }

            if (instance)
            {
                CSLogger.Log("[PrefabSpawnerController] Prefab already instantiated.");
                return;
            }

            InstantiatePrefab();
        }

        private void OnDestroyClicked()
        {
            if (!instance)
            {
                CSLogger.Log("[PrefabSpawnerController] Nothing to destroy. No instance is currently spawned.", LogCategory.Other, LogLevel.Info);
                return;
            }

            try
            {
                // Prefer Crystal Save's snapshot-aware destruction so the state is captured
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.DestroyWithSnapshot(instance);
                }
                else
                {
                    CSLogger.Log("[PrefabSpawnerController] SaveManager instance is not available. Falling back to Destroy.", LogLevel.Warning);
                    if (instance != null) // Additional null check before destroying
                    {
                        Destroy(instance);
                    }
                }
            }
            catch (System.Exception ex)
            {
                CSLogger.Log($"[PrefabSpawnerController] Error occurred while destroying instance: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                instance = null; // Always clear the reference
            }
        }

        private void OnDestroyByIdClicked()
        {
            if (string.IsNullOrEmpty(prefabAssetID))
            {
                CSLogger.Log("[PrefabSpawnerController] Cannot destroy by ID: PrefabAssetID is empty. Set it in the Inspector.", LogLevel.Error);
                return;
            }

            if (SaveManager.Instance == null)
            {
                CSLogger.Log("[PrefabSpawnerController] Cannot destroy by ID: SaveManager instance is not available.", LogLevel.Error);
                return;
            }

            var pm = SaveManager.Instance.GetPrefabManager;
            if (pm == null)
            {
                CSLogger.Log("[PrefabSpawnerController] Cannot destroy by ID: PrefabManager not available from SaveManager.", LogLevel.Error);
                return;
            }

            try
            {
                var saveablePrefabs = pm.GetSaveablePrefabs();
                if (saveablePrefabs == null)
                {
                    CSLogger.Log("[PrefabSpawnerController] No saveable prefabs collection found.", LogLevel.Warning);
                    return;
                }

                var sp = saveablePrefabs.FirstOrDefault(x => x && x.PrefabAssetID == prefabAssetID);

                if (!sp)
                {
                    CSLogger.Log($"[PrefabSpawnerController] No prefab instance found with asset ID '{prefabAssetID}'.");
                    return;
                }

                if (sp.gameObject == null)
                {
                    CSLogger.Log($"[PrefabSpawnerController] Found SaveablePrefab with ID '{prefabAssetID}' but its GameObject is null.", LogLevel.Warning);
                    return;
                }

                // Prefer Crystal Save's destruction helper for proper snapshot + pooling handling
                SaveManager.Instance.DestroyWithSnapshot(sp.gameObject);

                // Clear our instance reference if it was the destroyed object
                if (sp.gameObject == instance)
                    instance = null;
            }
            catch (System.Exception ex)
            {
                CSLogger.Log($"[PrefabSpawnerController] Error occurred while destroying prefab by ID '{prefabAssetID}': {ex.Message}", LogLevel.Error);
            }
        }

        // ─────────────────────────────── Helper methods ────────────────────────────────
        private void InstantiatePrefab()
        {
            if (!prefab)
            {
                CSLogger.Log("[PrefabSpawnerController] Cannot instantiate: Prefab reference is null. Please assign a prefab in the Inspector.", LogLevel.Error);
                return;
            }

            try
            {
                Vector3 spawnPosition;
                Quaternion spawnRotation;
                Transform parentTransform;

                if (spawnPoint != null)
                {
                    // Use spawn point if assigned
                    spawnPosition = spawnPoint.position;
                    spawnRotation = spawnPoint.rotation;
                    parentTransform = spawnPoint;
                }
                else
                {
                    // Use manual position if no spawn point is assigned
                    spawnPosition = manualSpawnPosition;
                    spawnRotation = Quaternion.identity;
                    parentTransform = transform; // Use this GameObject as parent
                    
                    // Additional safety check for this transform
                    if (parentTransform == null)
                    {
                        CSLogger.Log("[PrefabSpawnerController] Parent transform is null, instantiating without parent.", LogLevel.Warning);
                        parentTransform = null;
                    }
                }

                instance = Instantiate(
                    prefab,
                    spawnPosition,
                    spawnRotation,
                    parentTransform); // parent for tidy hierarchy

                if (instance == null)
                {
                    CSLogger.Log("[PrefabSpawnerController] Failed to instantiate prefab. Instantiate returned null.", LogLevel.Error);
                    return;
                }

                // Move the instantiated prefab up by 1 unit on the Y axis
                if (instance.transform != null)
                {
                    instance.transform.position += Vector3.up;
                }
                else
                {
                    CSLogger.Log("[PrefabSpawnerController] Instantiated prefab has no transform component.", LogLevel.Warning);
                }

                // Auto‑fill asset ID if user forgot
                if (string.IsNullOrEmpty(prefabAssetID))
                {
                    var sp = instance.GetComponent<SaveablePrefab>();
                    if (sp != null)
                    {
                        prefabAssetID = sp.PrefabAssetID;
                        CSLogger.Log($"[PrefabSpawnerController] Auto-filled PrefabAssetID: '{prefabAssetID}'");
                    }
                    else
                    {
                        CSLogger.Log("[PrefabSpawnerController] Instantiated prefab does not have a SaveablePrefab component.", LogLevel.Warning);
                    }
                }
            }
            catch (System.Exception ex)
            {
                CSLogger.Log($"[PrefabSpawnerController] Error occurred while instantiating prefab: {ex.Message}", LogLevel.Error);
                instance = null; // Ensure instance is null if instantiation failed
            }
        }

        private void OnDestroy()
        {
            try
            {
                // Safely remove listeners even if buttons are null
                spawnButton?.onClick.RemoveListener(OnSpawnClicked);
                destroyButton?.onClick.RemoveListener(OnDestroyClicked);
                destroyByIdButton?.onClick.RemoveListener(OnDestroyByIdClicked);
            }
            catch (System.Exception ex)
            {
                CSLogger.Log($"[PrefabSpawnerController] Error occurred while cleaning up button listeners: {ex.Message}", LogLevel.Warning);
            }
        }
    }
}
#endif
