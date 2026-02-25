#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(RememberCustomComponents))]
    [CanEditMultipleObjects]
    public class RememberCustomComponentsEditor : SaveableComponentEditor
    {
        private SerializedProperty modeProperty;
        private SerializedProperty componentsProperty;
    private SerializedProperty restoreDelayProperty;
    private SerializedProperty includePublicPropertiesProperty;

        private MonoBehaviour[] availableComponents;
        private string[] componentNames;

        protected override void OnEnable()
        {
            base.OnEnable();
            modeProperty = serializedObject.FindProperty("mode");
            componentsProperty = serializedObject.FindProperty("components");
            restoreDelayProperty = serializedObject.FindProperty("restoreDelaySeconds");
            includePublicPropertiesProperty = serializedObject.FindProperty("includePublicProperties");
            RefreshComponentList();
        }

        private void RefreshComponentList()
        {
            var comp = (RememberCustomComponents)target;
            availableComponents = comp.GetComponents<MonoBehaviour>()
                .Where(RememberCustomComponents.IsCustomComponent)
                .ToArray();
            componentNames = availableComponents
                .Select(c => c.GetType().Name)
                .ToArray();
        }

        protected override void DrawDerivedProperties()
        {
            EditorGUILayout.PropertyField(modeProperty);
            EditorGUILayout.PropertyField(restoreDelayProperty);
            EditorGUILayout.PropertyField(includePublicPropertiesProperty);

            if ((RememberCustomComponents.SerializationMode)modeProperty.enumValueIndex ==
                RememberCustomComponents.SerializationMode.SelectedComponents)
            {
                RefreshComponentList();
                int mask = 0;
                for (int i = 0; i < availableComponents.Length; i++)
                {
                    if (IsSelected(availableComponents[i]))
                        mask |= 1 << i;
                }

                EditorGUI.BeginChangeCheck();
                mask = EditorGUILayout.MaskField("Components", mask, componentNames);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyMask(mask);
                }
            }
        }

        private bool IsSelected(MonoBehaviour comp)
        {
            for (int i = 0; i < componentsProperty.arraySize; i++)
            {
                if (componentsProperty.GetArrayElementAtIndex(i).objectReferenceValue == comp)
                    return true;
            }
            return false;
        }

        private void ApplyMask(int mask)
        {
            componentsProperty.ClearArray();
            for (int i = 0; i < availableComponents.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    int idx = componentsProperty.arraySize;
                    componentsProperty.InsertArrayElementAtIndex(idx);
                    componentsProperty.GetArrayElementAtIndex(idx).objectReferenceValue = availableComponents[i];
                }
            }
        }

        protected override string[] AdditionalExclusions()
        {
            // Prevent the base editor from auto-drawing fields we render manually here
            return new[] { "mode", "components", "restoreDelaySeconds", "includePublicProperties" };
        }
    }
}
#endif
#endif
