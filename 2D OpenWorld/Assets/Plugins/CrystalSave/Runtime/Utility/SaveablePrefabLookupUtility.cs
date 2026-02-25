#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public static class SaveablePrefabLookupUtility
    {
        private static GameObject EnsureTracked(SaveManager saveManager, GameObject candidate, string context)
        {
            if (candidate == null)
            {
                return null;
            }

            if (saveManager != null && saveManager.IsGameObjectTracked(candidate))
            {
                return candidate;
            }

            if (Debug.isDebugBuild)
            {
                Logger.Log(
                    $"SaveablePrefabLookupUtility: Skipping {context} because it is not tracked.",
                    LogLevel.Info
                );
            }

            return null;
        }

        public static GameObject FindTrackedPrefabInstanceByAssetID(string prefabAssetID)
        {
            if (string.IsNullOrEmpty(prefabAssetID))
            {
                return null;
            }

            SaveManager saveManager = SaveManager.Instance;

            if (saveManager != null)
            {
                string currentUniqueID = saveManager.GetCurrentUniqueIDFromPrefabAssetID(prefabAssetID);
                if (!string.IsNullOrEmpty(currentUniqueID))
                {
                    GameObject trackedInstance = saveManager.FindGameObjectByUniqueID(
                        currentUniqueID,
                        SaveManager.IdentifierType.UniqueID
                    );

                    GameObject ensured = EnsureTracked(
                        saveManager,
                        trackedInstance,
                        $"candidate with UniqueID '{currentUniqueID}'"
                    );

                    if (ensured != null)
                    {
                        return ensured;
                    }
                }

                GameObject result = saveManager.FindGameObjectByUniqueID(
                    prefabAssetID,
                    SaveManager.IdentifierType.PrefabAssetID
                );

                GameObject trackedPrefab = EnsureTracked(
                    saveManager,
                    result,
                    $"candidate with PrefabAssetID '{prefabAssetID}'"
                );

                if (trackedPrefab != null)
                {
                    return trackedPrefab;
                }

                Dictionary<string, TrackedGameObject> trackedObjects = saveManager.GetTrackedGameObjects();
                foreach (TrackedGameObject tracked in trackedObjects.Values)
                {
                    GameObject trackedObject = tracked?.GameObject;
                    if (trackedObject == null)
                    {
                        continue;
                    }

                    SaveablePrefab prefab = SaveablePrefab.TryGetCachedSaveablePrefab(trackedObject, out var cachedPrefab)
                        ? cachedPrefab
                        : trackedObject.GetComponent<SaveablePrefab>();

                    if (prefab != null && prefab.PrefabAssetID == prefabAssetID)
                    {
                        return trackedObject;
                    }
                }
            }

#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
            SaveablePrefab[] prefabs = UnityEngine.Object
                .FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618

            foreach (SaveablePrefab prefab in prefabs)
            {
                if (prefab != null && prefab.PrefabAssetID == prefabAssetID)
                {
                    return prefab.gameObject;
                }
            }

            return null;
        }

        public static GameObject FindChildByName(GameObject root, string childName, bool matchRoot = false)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform rootTransform = root.transform;

            if (matchRoot && string.Equals(rootTransform.name, childName, StringComparison.Ordinal))
            {
                return root;
            }

            var stack = new Stack<Transform>();
            for (int i = 0; i < rootTransform.childCount; ++i)
            {
                stack.Push(rootTransform.GetChild(i));
            }

            while (stack.Count > 0)
            {
                Transform current = stack.Pop();
                if (string.Equals(current.name, childName, StringComparison.Ordinal))
                {
                    return current.gameObject;
                }

                for (int i = 0; i < current.childCount; ++i)
                {
                    stack.Push(current.GetChild(i));
                }
            }

            return null;
        }

        public static GameObject FindChildFromAssetID(string prefabAssetID, string childName, bool matchRoot = false)
        {
            GameObject root = FindTrackedPrefabInstanceByAssetID(prefabAssetID);
            if (root == null)
            {
                return null;
            }

            return FindChildByName(root, childName, matchRoot);
        }
    }
}
#endif
