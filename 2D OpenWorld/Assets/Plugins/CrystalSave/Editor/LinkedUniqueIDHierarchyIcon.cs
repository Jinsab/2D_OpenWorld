#if UNITY_EDITOR && ARAWN_REMEMBERME && MEMORYPACK
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// Displays a small icon in the hierarchy for any GameObject whose
    /// UniqueID (from its UniqueID component) appears in the SceneObjectRegistry.
    /// This optimized version caches the registry and its set of linked IDs,
    /// avoiding per-frame disk loads and linear searches.
    /// </summary>
    [InitializeOnLoad]
    static class LinkedRememberComponentHierarchyIcon
    {
        private const string k_IconPath = "Assets/Plugins/CrystalSave/Editor/Gizmos/LinkedUniqueID.png";
        private static readonly Texture2D s_Icon;

        // Avoid overlap with SaveablePrefab icon (Slot 1). Use Slot 2:
        private const int k_SlotIndex = 2;

        // Cached reference to the SceneObjectRegistry asset path
        private const string k_SceneObjectRegistryPath = "Assets/Plugins/CrystalSave/Resources/SceneObjectRegistry.asset";

        // Cached registry and its set of UniqueIDs
        private static SceneObjectRegistry s_Registry;
        private static HashSet<string> s_LinkedIDs = new HashSet<string>();

        static LinkedRememberComponentHierarchyIcon()
        {
            s_Icon = AssetDatabase.LoadAssetAtPath<Texture2D>(k_IconPath);
            LoadRegistryAndCacheIDs();
            // Re-cache whenever the project changes (e.g., registry asset edited)
            EditorApplication.projectChanged += LoadRegistryAndCacheIDs;
            #if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUIByEntityId;
            #else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            #endif
        }

        #if UNITY_6000_4_OR_NEWER
        private static void OnHierarchyGUIByEntityId(UnityEngine.EntityId entityId, Rect selectionRect)
        {
            if (s_Icon == null) return;

            var go = EditorUtility.EntityIdToObject(entityId) as GameObject;
            if (go == null) return;

            // Does it have a RememberGameObject? If not, skip
            var rememberComp = go.GetComponent<RememberGameObject>();
            if (rememberComp == null) return;

            // Does it have a UniqueID? If not, skip
            var uidComp = go.GetComponent<UniqueID>();
            if (uidComp == null || string.IsNullOrEmpty(uidComp.ID)) return;
            string uniqueID = uidComp.ID;

            // Check cache of linked IDs
            if (s_LinkedIDs == null || !s_LinkedIDs.Contains(uniqueID)) return;

            // Draw the icon in the chosen slot
            float iconSize = 16f;
            float rightMargin = 0f;
            float slotOffset = k_SlotIndex * iconSize;

            Rect iconRect = new Rect(
                selectionRect.xMax - iconSize - rightMargin - slotOffset,
                selectionRect.y,
                iconSize,
                iconSize
            );

            GUI.DrawTexture(iconRect, s_Icon);
        }
        #endif

        /// <summary>
        /// Loads the SceneObjectRegistry asset once and builds a HashSet of all UniqueIDs,
        /// so OnHierarchyGUI can perform an O(1) lookup instead of reloading and iterating every frame.
        /// </summary>
        private static void LoadRegistryAndCacheIDs()
        {
            var registry = AssetDatabase.LoadAssetAtPath<SceneObjectRegistry>(k_SceneObjectRegistryPath);
            if (registry == null)
            {
                s_Registry = null;
                s_LinkedIDs.Clear();
                return;
            }

            if (registry == s_Registry)
            {
                // No change in registry reference, but its contents might still have changed.
                // Best to rebuild from scratch to catch edits.
            }
            s_Registry = registry;
            var newSet = new HashSet<string>();
            foreach (var entry in s_Registry.Entries)
            {
                if (!string.IsNullOrEmpty(entry.UniqueID))
                    newSet.Add(entry.UniqueID);
            }
            s_LinkedIDs = newSet;
        }

        #if !UNITY_6000_4_OR_NEWER
        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (s_Icon == null) return;

            // 1) Get the GameObject for this hierarchy line
            #if UNITY_6000_3_OR_NEWER
            var go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            #else
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            #endif
            if (go == null) return;

            // 2) Does it have a RememberGameObject? If not, skip
            var rememberComp = go.GetComponent<RememberGameObject>();
            if (rememberComp == null) return;

            // 3) Does it have a UniqueID? If not, skip
            var uidComp = go.GetComponent<UniqueID>();
            if (uidComp == null || string.IsNullOrEmpty(uidComp.ID)) return;
            string uniqueID = uidComp.ID;

            // 4) Check cache of linked IDs
            if (s_LinkedIDs == null || !s_LinkedIDs.Contains(uniqueID)) return;

            // 5) Draw the icon in Slot 2 on the right side of the hierarchy row
            const float iconSize = 16f;
            const float padding = 2f;
            float xPos = selectionRect.xMax
                       - (iconSize + padding) * (k_SlotIndex + 1)
                       + padding;

            var iconRect = new Rect(
                xPos,
                selectionRect.yMin + (selectionRect.height - iconSize) * 0.5f,
                iconSize,
                iconSize
            );

            GUI.DrawTexture(iconRect, s_Icon);
        }
        #endif
    }
}
#endif
