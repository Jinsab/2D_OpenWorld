#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        internal static class UniqueIDUtility
        {
                public static UniqueID EnsureComponent(GameObject go, string guid = null)
                {
                        var uid = go.GetComponent<UniqueID>();

                        // Runtime spawned SaveablePrefabs manage their own identity and do not
                        // require a UniqueID component.
                        if (uid == null && Application.isPlaying && go.GetComponent<SaveablePrefab>() != null)
                                return null;

                        // CRITICAL FIX: Children of SaveablePrefabs should NOT get UniqueID components.
                        // They use their parent SaveablePrefab's UniqueID for identification.
                        if (uid == null && Application.isPlaying && IsChildOfSaveablePrefab(go))
                                return null;

                        if (uid == null)
                        {
                                uid = go.AddComponent<UniqueID>();
                                uid.hideFlags |= HideFlags.HideInInspector;
                        }
                        else
                        {
                                uid.hideFlags |= HideFlags.HideInInspector;
                        }

                        if (!string.IsNullOrEmpty(guid) && uid.ID != guid)
                                uid.ID = guid;

                        return uid;
                }

                /// <summary>
                /// Checks if the GameObject is a child (not root) of a SaveablePrefab.
                /// </summary>
                private static bool IsChildOfSaveablePrefab(GameObject go)
                {
                        if (go == null) return false;
                        
                        // If this GameObject itself has a SaveablePrefab, it's the root, not a child
                        if (go.GetComponent<SaveablePrefab>() != null) return false;
                        
                        // Check if any parent has a SaveablePrefab component
                        Transform parent = go.transform.parent;
                        while (parent != null)
                        {
                                if (parent.GetComponent<SaveablePrefab>() != null)
                                        return true;
                                parent = parent.parent;
                        }
                        
                        return false;
                }
        }
}
#endif