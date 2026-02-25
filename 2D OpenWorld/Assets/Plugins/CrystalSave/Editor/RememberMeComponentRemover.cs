#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Editor
{
    public static class RememberMeComponentRemover
    {
        [MenuItem("Tools/Crystal Save/Project/Remove All Crystal Save Components")]
        public static void RemoveAllCrystalSaveComponents()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Confirm Removal",
                "This will remove ALL components from the namespace 'Arawn.CrystalSave' " +
                "from ALL scenes and ALL prefabs in your project.\n\n" +
                "This operation is destructive and cannot be undone after saving. Proceed?",
                "Yes, Remove All",
                "Cancel"
            );

            if (!confirm) return;

            int totalRemoved = 0;
            var crystalSaveTypes = GetAllCrystalSaveComponentTypes();

            // Process all open scenes
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (IsSceneObject(go))
                {
                    totalRemoved += RemoveComponentsFromGameObject(go, crystalSaveTypes);
                }
            }

            // Process all prefabs in the project
            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    int removed = RemoveComponentsFromGameObject(prefab, crystalSaveTypes);
                    if (removed > 0)
                    {
                        EditorUtility.SetDirty(prefab);
                        totalRemoved += removed;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Removal Complete",
                $"Removed {totalRemoved} Crystal Save components from scenes and prefabs.",
                "OK");
        }

        private static List<Type> GetAllCrystalSaveComponentTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .Where(t => typeof(Component).IsAssignableFrom(t)
                            && t.Namespace != null
                            && t.Namespace.StartsWith("Arawn.CrystalSave"))
                .ToList();
        }

        private static int RemoveComponentsFromGameObject(GameObject go, List<Type> types)
        {
            int removed = 0;
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue; // broken/missing script
                Type compType = component.GetType();
                if (types.Contains(compType))
                {
                    Undo.DestroyObjectImmediate(component);
                    removed++;
                }
            }
            return removed;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return !EditorUtility.IsPersistent(go)
                   && (go.hideFlags & HideFlags.NotEditable) == 0
                   && (go.hideFlags & HideFlags.HideAndDontSave) == 0;
        }
    }
}
#endif
#endif
