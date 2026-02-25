#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// Instantiates a SaveablePrefab via SaveablePrefabFactory when the assigned
    /// UI Button is clicked.
    /// </summary>
    public sealed class InstantiateSaveableOnClick : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Button that triggers the instantiation")]
        [SerializeField] private Button spawnButton;

        [Header("Prefab")]
        [Tooltip("Prefab asset that already (or will automatically) carry a SaveablePrefab component")]
        [SerializeField] private GameObject prefabAsset;

        [SerializeField] private string SceneName = "";

        private void Awake()
        {
            if (spawnButton == null || prefabAsset == null)
            {
                //Debug.LogWarning($"{nameof(InstantiateSaveableOnClick)}: missing references");
                enabled = false;
                return;
            }

            // Hook up the click-event once
            spawnButton.onClick.AddListener(SpawnAtOrigin);
        }

        /// <summary>Spawns the prefab at world-space origin, no parent.</summary>
        private void SpawnAtOrigin()
        {
            // Quaternion.identity → no rotation, Vector3.zero → (0,0,0)
            SaveablePrefabFactory.Instantiate(
                prefabAsset,
                Vector3.zero,
                Quaternion.identity,
                SceneName,
                parent: null,
                registerWithSaveSystem: true);
        }
    }
}
#endif
