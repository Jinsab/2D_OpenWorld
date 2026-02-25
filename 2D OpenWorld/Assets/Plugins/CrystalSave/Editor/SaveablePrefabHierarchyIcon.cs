#if UNITY_EDITOR && ARAWN_REMEMBERME && MEMORYPACK
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// Adds a custom icon to the Hierarchy window for GameObjects with a SaveablePrefab component.
    /// </summary>
    [InitializeOnLoad]
    static class SaveablePrefabHierarchyIcon
    {
        // Path must be exactly the same as you used in [Icon(...)] on SaveablePrefab
        private const string k_IconPath = "Assets/Plugins/CrystalSave/Editor/Gizmos/SaveablePrefab.png";
        private static readonly Texture2D s_Icon;

        // Choose which “slot” from the right you want to occupy.
        // Slot 0 is the absolute right-most 16×16. Slot 1 is 16px to its left, slot 2 is 32px left, etc.
        private const int k_SlotIndex = 1;

        static SaveablePrefabHierarchyIcon()
        {
            s_Icon = AssetDatabase.LoadAssetAtPath<Texture2D>(k_IconPath);
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

            var saveablePrefab = go.GetComponent<SaveablePrefab>();
            if (saveablePrefab == null) return;

            // Calculate the icon position in the right-side "slots"
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

        #if !UNITY_6000_4_OR_NEWER
        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (s_Icon == null) return;

            #if UNITY_6000_3_OR_NEWER
            var go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            #else
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            #endif
            if (go == null) return;
            if (go.GetComponent<SaveablePrefab>() == null) return;

            const float iconSize = 16f;
            const float padding = 2f; // gap between icon slots

            // Compute the X position using our “slot index”:
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
