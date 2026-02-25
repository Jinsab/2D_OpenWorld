#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR && TMP_PRESENT
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.UI;

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(SaveSlotEntryUI))]
    public class SaveSlotEntryUIEditor : UnityEditor.Editor
    {
        SerializedProperty textModeProp;
        SerializedProperty screenshotProp;
        SerializedProperty backgroundProp;
        SerializedProperty themeProp;
        SerializedProperty slotNameTextProp;
        SerializedProperty slotNameTMPProp;
        SerializedProperty metadataTextProp;
        SerializedProperty metadataTMPProp;
        SerializedProperty customMetadataContainerProp;
        SerializedProperty customMetadataLinePrefabProp;
        SerializedProperty customMetadataLineTMPPrefabProp;
        SerializedProperty deleteConfirmationWindowProp;
        SerializedProperty confirmDeleteButtonProp;
        SerializedProperty cancelDeleteButtonProp;
        SerializedProperty renameInputProp;
        SerializedProperty renameInputTMPProp;
        SerializedProperty hideRenameInputOnStartProp;
        SerializedProperty renameButtonProp;
        SerializedProperty loadButtonProp;
        SerializedProperty saveButtonProp;
        SerializedProperty deleteButtonProp;
        SerializedProperty syncOverlayProp;

        void OnEnable()
        {
            textModeProp       = serializedObject.FindProperty("textMode");
            screenshotProp     = serializedObject.FindProperty("screenshotImage");
            backgroundProp     = serializedObject.FindProperty("background");
            themeProp          = serializedObject.FindProperty("theme");
            slotNameTextProp   = serializedObject.FindProperty("slotNameText");
            slotNameTMPProp    = serializedObject.FindProperty("slotNameTMP");
            metadataTextProp   = serializedObject.FindProperty("metadataText");
            metadataTMPProp    = serializedObject.FindProperty("metadataTMP");
            customMetadataContainerProp = serializedObject.FindProperty("customMetadataContainer");
            customMetadataLinePrefabProp = serializedObject.FindProperty("customMetadataLinePrefab");
            customMetadataLineTMPPrefabProp = serializedObject.FindProperty("customMetadataLineTMPPrefab");
            deleteConfirmationWindowProp = serializedObject.FindProperty("deleteConfirmationWindow");
            confirmDeleteButtonProp = serializedObject.FindProperty("confirmDeleteButton");
            cancelDeleteButtonProp  = serializedObject.FindProperty("cancelDeleteButton");
            renameInputProp    = serializedObject.FindProperty("renameInput");
            renameInputTMPProp = serializedObject.FindProperty("renameInputTMP");
            hideRenameInputOnStartProp = serializedObject.FindProperty("hideRenameInputOnStart");
            renameButtonProp   = serializedObject.FindProperty("renameButton");
            loadButtonProp     = serializedObject.FindProperty("loadButton");
            saveButtonProp     = serializedObject.FindProperty("saveButton");
            deleteButtonProp   = serializedObject.FindProperty("deleteButton");
            syncOverlayProp    = serializedObject.FindProperty("syncOverlay");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(textModeProp);
            EditorGUILayout.PropertyField(screenshotProp);
            EditorGUILayout.PropertyField(backgroundProp);
            EditorGUILayout.PropertyField(syncOverlayProp);
            EditorGUILayout.PropertyField(themeProp);
            EditorGUILayout.PropertyField(customMetadataContainerProp);

            if ((SaveSlotEntryUI.TextMode)textModeProp.enumValueIndex == SaveSlotEntryUI.TextMode.Legacy)
            {
                EditorGUILayout.PropertyField(slotNameTextProp);
                EditorGUILayout.PropertyField(metadataTextProp);
                EditorGUILayout.PropertyField(customMetadataLinePrefabProp);
                EditorGUILayout.PropertyField(renameInputProp);
            }
            else
            {
                EditorGUILayout.PropertyField(slotNameTMPProp);
                EditorGUILayout.PropertyField(metadataTMPProp);
                EditorGUILayout.PropertyField(customMetadataLineTMPPrefabProp);
                EditorGUILayout.PropertyField(renameInputTMPProp);
            }

            EditorGUILayout.PropertyField(hideRenameInputOnStartProp);

            EditorGUILayout.PropertyField(renameButtonProp);
            EditorGUILayout.PropertyField(loadButtonProp);
            EditorGUILayout.PropertyField(saveButtonProp);
            EditorGUILayout.PropertyField(deleteButtonProp);

            EditorGUILayout.PropertyField(deleteConfirmationWindowProp);
            EditorGUILayout.PropertyField(confirmDeleteButtonProp);
            EditorGUILayout.PropertyField(cancelDeleteButtonProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
#endif
