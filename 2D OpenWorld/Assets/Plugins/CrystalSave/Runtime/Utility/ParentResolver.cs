#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Resolves the stored ParentID of a SaveablePrefab to the live Transform in the scene.
    /// Now caches *all* UniqueIDs (roots + children) from every instantiated prefab, so nested
    /// relationships are handled in a single pass.
    /// </summary>
    public class ParentResolver
    {
        // cache built once in the ctor ───────────────────────────────────────
        private readonly Dictionary<string, Transform> idToTransform = new();

        // still needed for the legacy fallback path
        private readonly Dictionary<string, GameObject> instantiatedPrefabs;

        public ParentResolver(Dictionary<string, GameObject> instantiatedPrefabs)
        {
            this.instantiatedPrefabs = instantiatedPrefabs;

            // Collect every UniqueID found in each prefab (including inactive children)
            foreach (var prefabGO in instantiatedPrefabs.Values)
            {
                if (prefabGO == null)
                    continue;
                foreach (var uid in prefabGO.GetComponentsInChildren<UniqueID>(true))
                {
                    if (!string.IsNullOrEmpty(uid.ID) && !idToTransform.ContainsKey(uid.ID))
                    {
                        idToTransform.Add(uid.ID, uid.transform);
                    }
                }
            }
        }

        public Transform ResolveParent(SaveablePrefabData prefabData)
        {
            // If there is no explicit parentID, or IDs are unreliable across sessions for scene-baked prefabs,
            // first try fingerprint-based resolution.
            if (string.IsNullOrEmpty(prefabData.ParentID))
            {
                var byFp = ResolveByFingerprint(prefabData);
                if (byFp != null) return byFp;
                return null;
            }

            // 0️⃣ Fast path: already cached?
            if (idToTransform.TryGetValue(prefabData.ParentID, out var parentTransform))
                return parentTransform;

            // 1️⃣ Scene object parent?
            if (prefabData.IsParentSceneObject)
            {
                GameObject parentGO = SaveManager.Instance.FindGameObjectByUniqueID(prefabData.ParentID, SaveManager.IdentifierType.UniqueID);
                parentTransform = parentGO ? parentGO.transform : null;

                if (parentTransform == null)
                {
                    Logger.Log($"Scene parent with ID '{prefabData.ParentID}' not found.", LogCategory.PrefabManager, LogLevel.Warning);
                }

                return parentTransform;
            }

            // 2️⃣ Another SaveablePrefab root (fallback to original logic)
            if (!instantiatedPrefabs.TryGetValue(prefabData.ParentID, out GameObject parentInstance))
            {
                // Last-ditch search: maybe the parent existed already (e.g., from pooling/KAS)
                parentInstance = SaveManager.Instance.FindGameObjectByUniqueID(prefabData.ParentID, SaveManager.IdentifierType.UniqueID);
                if (parentInstance != null)
                {
                    Logger.Log(
                        $"Parent with ID '{prefabData.ParentID}' found via fallback. " +
                        "Not in 'instantiatedPrefabs' dictionary, but still in scene."
                    );
                }
            }

            if (parentInstance != null)
                return parentInstance.transform;

            // As a final attempt, try fingerprint-based resolution
            var fp = ResolveByFingerprint(prefabData);
            if (fp != null)
                return fp;

            Logger.Log($"Parent SaveablePrefab with ID '{prefabData.ParentID}' not found.", LogLevel.Warning);
            return null;
        }

        private Transform ResolveByFingerprint(SaveablePrefabData prefabData)
        {
            // Use ParentPrefabAssetID and ParentStableKey to find a scene parent.
            if (prefabData == null) return null;
            if (string.IsNullOrEmpty(prefabData.ParentPrefabAssetID) && string.IsNullOrEmpty(prefabData.ParentStableKey))
                return null;

            try
            {
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
                var candidates = Object.FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
                List<SaveablePrefab> matches = new();
                foreach (var sp in candidates)
                {
                    if (sp == null) continue;
                    if (!string.IsNullOrEmpty(prefabData.ParentPrefabAssetID) && sp.PrefabAssetID != prefabData.ParentPrefabAssetID)
                        continue;
                    matches.Add(sp);
                }

                if (matches.Count == 0) return null;

                // If stable key is present, select exact match by stable hierarchy path
                if (!string.IsNullOrEmpty(prefabData.ParentStableKey))
                {
                    foreach (var sp in matches)
                    {
                        string key = SaveablePrefab.BuildStableHierarchyKey(sp);
                        if (string.Equals(key, prefabData.ParentStableKey, System.StringComparison.Ordinal))
                        {
                            return sp.transform;
                        }
                    }
                }

                // If only one assetID match remains, use it
                if (matches.Count == 1)
                    return matches[0].transform;
            }
            catch { /* best-effort only */ }

            return null;
        }
    }

}
#endif
