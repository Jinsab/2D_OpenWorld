#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.VisualScripting.Editor
{
    [CustomEditor(typeof(CrystalSaveVisualActionHubButtonBinder))]
    [CanEditMultipleObjects]
    public class CrystalSaveVisualActionHubButtonBinderEditor : UnityEditor.Editor
    {
        SerializedProperty hubProp;
        SerializedProperty buttonProp;
        SerializedProperty autoRegisterProp;
        SerializedProperty triggerModeProp;
        SerializedProperty actionIndexProp;

        void OnEnable()
        {
            hubProp = serializedObject.FindProperty("hub");
            buttonProp = serializedObject.FindProperty("button");
            autoRegisterProp = serializedObject.FindProperty("autoRegisterOnEnable");
            triggerModeProp = serializedObject.FindProperty("trigger");
            actionIndexProp = serializedObject.FindProperty("actionIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(hubProp);
            EditorGUILayout.PropertyField(buttonProp);
            EditorGUILayout.PropertyField(autoRegisterProp);
            EditorGUILayout.PropertyField(triggerModeProp);

            var mode = (CrystalSaveVisualActionHubButtonBinder.TriggerMode)triggerModeProp.enumValueIndex;
            if (mode == CrystalSaveVisualActionHubButtonBinder.TriggerMode.ExecuteAction)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawActionSelector();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawActionSelector()
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.PropertyField(actionIndexProp);
                return;
            }

            var binder = target as CrystalSaveVisualActionHubButtonBinder;
            if (binder == null)
            {
                EditorGUILayout.PropertyField(actionIndexProp);
                return;
            }

            var hub = binder.Hub;
            if (hub == null)
            {
                EditorGUILayout.PropertyField(actionIndexProp, new GUIContent("Action Index"));
                EditorGUILayout.HelpBox("Assign a Visual Action Hub to pick one of its actions.", MessageType.Info);
                return;
            }

            var actions = hub.Actions;
            if (actions == null || actions.Count == 0)
            {
                EditorGUILayout.PropertyField(actionIndexProp, new GUIContent("Action Index"));
                EditorGUILayout.HelpBox("The referenced hub does not define any actions.", MessageType.Warning);
                return;
            }

            var options = actions.Select((action, index) =>
            {
                string displayName = action != null && !string.IsNullOrEmpty(action.Name)
                    ? action.Name
                    : "(Unnamed Action)";
                return $"{index}: {displayName}";
            }).ToArray();

            int currentIndex = Mathf.Clamp(actionIndexProp.intValue, 0, options.Length - 1);
            int selected = EditorGUILayout.Popup(new GUIContent("Action"), currentIndex, options);
            if (selected != currentIndex)
            {
                actionIndexProp.intValue = selected;
            }
        }
    }
}
#endif
