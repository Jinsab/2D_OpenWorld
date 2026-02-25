#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Demo
{
        /// <summary>
        /// Demo component that spawns a handful of SaveablePrefabs inside a rectangular
        /// region only when explicitly requested. Spawned instances are marked to defer
        /// their restoration until requested.
        /// </summary>
        [AddComponentMenu("Crystal Save/Demos/Deferred Prefab Area Spawner")]
        public sealed class DeferredPrefabAreaSpawnerDemo : MonoBehaviour
        {
                [Header("Saveable Prefabs")]
                [Tooltip("Three SaveablePrefab assets that will be spawned inside the defined area.")]
                [SerializeField] private GameObject[] prefabAssets = new GameObject[3];

                [Header("Spawn Area")]
                [Tooltip("World-space start of the rectangle's main axis.")]
                [SerializeField] private Transform startPoint;

                [Tooltip("World-space end of the rectangle's main axis.")]
                [SerializeField] private Transform endPoint;

                [Tooltip("Width of the rectangle measured perpendicular to the start → end axis.")]
                [Min(0f)]
                [SerializeField] private float width = 10f;

                [Tooltip("Minimum spacing maintained between spawned prefabs.")]
                [Min(0f)]
                [SerializeField] private float distance = 3f;

                [Header("Optional UI")]
                [Tooltip("If assigned, clicking this button spawns the prefabs.")]
                [SerializeField] private Button spawnButton;

                private readonly List<SaveablePrefab> spawnedInstances = new();

                private void Awake()
                {
                        if (spawnButton != null)
                                spawnButton.onClick.AddListener(SpawnPrefabs);
                }

                private void OnDestroy()
                {
                        if (spawnButton != null)
                                spawnButton.onClick.RemoveListener(SpawnPrefabs);
                }

                /// <summary>
                /// Public entry-point used by UI buttons or other scripts to trigger spawning.
                /// </summary>
                [ContextMenu("Spawn Prefabs")]
                public void SpawnPrefabs()
                {
                        if (!ValidateInputs()) return;

                        PruneDestroyedInstances();

                        Vector3 origin = startPoint.position;
                        Vector3 target = endPoint.position;
                        Vector3 axis = target - origin;
                        float length = axis.magnitude;

                        if (length < 0.01f)
                        {
                                Debug.LogWarning($"{nameof(DeferredPrefabAreaSpawnerDemo)}: Start and End points are too close together.");
                                return;
                        }

                        Vector3 axisDirection = axis / length;
                        Vector3 sideDirection = Vector3.Cross(Vector3.up, axisDirection);
                        if (sideDirection.sqrMagnitude < 0.0001f)
                        {
                                sideDirection = Vector3.Cross(axisDirection, Vector3.right);
                                if (sideDirection.sqrMagnitude < 0.0001f)
                                {
                                        sideDirection = Vector3.up; // final fallback
                                }
                        }
                        sideDirection.Normalize();

                        List<GameObject> availablePrefabs = new(prefabAssets.Length);
                        foreach (GameObject prefab in prefabAssets)
                        {
                                if (prefab != null)
                                        availablePrefabs.Add(prefab);
                        }

                        if (availablePrefabs.Count == 0)
                        {
                                Debug.LogWarning($"{nameof(DeferredPrefabAreaSpawnerDemo)}: No valid SaveablePrefabs assigned.");
                                return;
                        }

                        int axisSlots = distance > 0f ? Mathf.Max(1, Mathf.FloorToInt(length / distance) + 1) : 1;
                        int widthSlots = distance > 0f ? Mathf.Max(1, Mathf.FloorToInt(width / distance) + 1) : 1;
                        int totalSlots = axisSlots * widthSlots;
                        float halfWidth = width * 0.5f;

                        List<Vector3> usedPositions = new(totalSlots);
                        List<Vector3> spawnPositions = new(totalSlots);

                        for (int alongIndex = 0; alongIndex < axisSlots; ++alongIndex)
                        {
                                float alongAxis = axisSlots == 1 ? 0f : Mathf.Min(alongIndex * distance, length);

                                for (int widthIndex = 0; widthIndex < widthSlots; ++widthIndex)
                                {
                                        float lateral = widthSlots == 1 ? 0f : Mathf.Min(-halfWidth + widthIndex * distance, halfWidth);
                                        Vector3 candidate = origin + axisDirection * alongAxis + sideDirection * lateral;

                                        if (!IsFarEnough(candidate, usedPositions))
                                                continue;

                                        usedPositions.Add(candidate);
                                        spawnPositions.Add(candidate);
                                }
                        }

                        if (spawnPositions.Count == 0)
                                return;

                        int prefabIndex = 0;
                        foreach (Vector3 spawnPos in spawnPositions)
                        {
                                GameObject prefabAsset = availablePrefabs[prefabIndex % availablePrefabs.Count];
                                ++prefabIndex;

                                SaveablePrefab instance = SaveablePrefabFactory.Instantiate(
                                        prefabAsset,
                                        spawnPos,
                                        Quaternion.identity,
                                        parent: null,
                                        registerWithSaveSystem: true);

                                if (instance == null)
                                        continue;

                                instance.DeferLowPriorityUntilRequested = true;

                                spawnedInstances.Add(instance);
                        }
                }

                private bool IsFarEnough(Vector3 candidate, List<Vector3> usedPositions)
                {
                        if (distance <= 0f) return true;

                        float minDistanceSqr = distance * distance;
                        foreach (Vector3 used in usedPositions)
                        {
                                if ((candidate - used).sqrMagnitude < minDistanceSqr)
                                        return false;
                        }

                        foreach (SaveablePrefab instance in spawnedInstances)
                        {
                                if (!instance) continue;
                                if ((candidate - instance.transform.position).sqrMagnitude < minDistanceSqr)
                                        return false;
                        }

                        return true;
                }

                private void PruneDestroyedInstances()
                {
                        spawnedInstances.RemoveAll(instance => instance == null);
                }

                private bool ValidateInputs()
                {
                        if (prefabAssets == null || prefabAssets.Length == 0)
                        {
                                Debug.LogWarning($"{nameof(DeferredPrefabAreaSpawnerDemo)}: Assign three SaveablePrefabs in the inspector.");
                                return false;
                        }

                        if (!startPoint || !endPoint)
                        {
                                Debug.LogWarning($"{nameof(DeferredPrefabAreaSpawnerDemo)}: Assign both a Start Point and an End Point transform.");
                                return false;
                        }

                        return true;
                }
        }
}
#endif
