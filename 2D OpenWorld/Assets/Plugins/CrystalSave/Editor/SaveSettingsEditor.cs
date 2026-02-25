#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.Runtime;
#if CRYSTALSAVE_TIMEMACHINE
using Arawn.CrystalSave.Runtime.TimeMachine;
#endif

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(SaveSettings))]
    public class SaveSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty persistentPathProp;
        SerializedProperty modeProp;
        SerializedProperty folderProp;
        SerializedProperty rootProp;
        SerializedProperty runMigrationProp;
        SerializedProperty autoSaveMigratedDataProp;
        SerializedProperty useAddressablesProp;
        SerializedProperty enableAssetReuseDuringDeferredProp;
        bool showPersistent = true;
        
#if CRYSTALSAVE_TIMEMACHINE
        SerializedProperty timeMachinePresetProp;
        SerializedProperty useContinuousTimeProp;
        SerializedProperty defaultResumeModeProp;
        SerializedProperty defaultAutoBranchBehaviorProp;
        SerializedProperty defaultBranchCopyModeProp;
#endif

        void OnEnable()
        {
            persistentPathProp = serializedObject.FindProperty("persistentPath");
            modeProp = persistentPathProp.FindPropertyRelative("mode");
            folderProp = persistentPathProp.FindPropertyRelative("customFolderName");
            rootProp = persistentPathProp.FindPropertyRelative("nonWebGLOutputRoot");
            runMigrationProp = serializedObject.FindProperty("runPersistentPathMigrationOnStartup");
            autoSaveMigratedDataProp = serializedObject.FindProperty("autoSaveMigratedData");
            useAddressablesProp = serializedObject.FindProperty("useAddressables");
            enableAssetReuseDuringDeferredProp = serializedObject.FindProperty("enableAssetBasedReuseDuringDeferred");
            
#if CRYSTALSAVE_TIMEMACHINE
            timeMachinePresetProp = serializedObject.FindProperty("timeMachinePreset");
            useContinuousTimeProp = serializedObject.FindProperty("useContinuousTimeByDefault");
            defaultResumeModeProp = serializedObject.FindProperty("defaultResumeMode");
            defaultAutoBranchBehaviorProp = serializedObject.FindProperty("defaultAutoBranchBehavior");
            defaultBranchCopyModeProp = serializedObject.FindProperty("defaultBranchCopyMode");
#endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
#if CRYSTALSAVE_TIMEMACHINE
            // Store preset value before changes
            var previousPreset = timeMachinePresetProp != null ? 
                (TimeMachinePresetType)timeMachinePresetProp.enumValueIndex : 
                TimeMachinePresetType.Custom;
#endif
            
            DrawPersistentPath();
            DrawAddressablesToggle();
            DrawDeferredReuseToggle();
            DrawPropertiesExcluding(serializedObject, "m_Script", "persistentPath", "runPersistentPathMigrationOnStartup", "autoSaveMigratedData", "useAddressables");
            
#if CRYSTALSAVE_TIMEMACHINE
            // Check if preset changed and auto-apply
            if (timeMachinePresetProp != null)
            {
                var newPreset = (TimeMachinePresetType)timeMachinePresetProp.enumValueIndex;
                if (newPreset != previousPreset)
                {
                    var settings = (SaveSettings)target;
                    settings.ApplyPresetToSettings();
                    EditorUtility.SetDirty(settings);
                    
                    if (newPreset != TimeMachinePresetType.Custom)
                    {
                        Debug.Log($"[SaveSettings] ✅ Auto-applied preset: {newPreset}");
                    }
                }
                
                // Add preset helper UI after drawing all properties
                DrawTimeMachinePresetHelper();
            }
#endif
            
            serializedObject.ApplyModifiedProperties();
        }

        void DrawPersistentPath()
        {
            showPersistent = EditorGUILayout.Foldout(showPersistent, "Persistent Path", true);
            if (!showPersistent) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(modeProp, new GUIContent("Mode"));
            if ((PersistentPathMode)modeProp.enumValueIndex == PersistentPathMode.Custom)
            {
                folderProp.stringValue = Regex.Replace(folderProp.stringValue.Trim(), "[\\\\/:*?\"<>|]", "");
                if (string.IsNullOrWhiteSpace(folderProp.stringValue))
                    folderProp.stringValue = "CrystalSave";
                EditorGUILayout.PropertyField(folderProp, new GUIContent("Custom Folder Name"));
                EditorGUILayout.PropertyField(rootProp, new GUIContent("Non WebGL Output Root"));
                if (!string.IsNullOrEmpty(rootProp.stringValue) && !Path.IsPathRooted(rootProp.stringValue))
                    EditorGUILayout.HelpBox("Non WebGL Output Root must be an absolute path.", MessageType.Warning);
                string webGLPath = $"/idbfs/{folderProp.stringValue}";
                string nonWeb = Path.Combine(string.IsNullOrEmpty(rootProp.stringValue) ? Application.persistentDataPath : rootProp.stringValue, folderProp.stringValue);
                EditorGUILayout.LabelField("Preview WebGL Path", webGLPath);
                EditorGUILayout.LabelField("Preview Non WebGL Path", nonWeb);
                if (GUILayout.Button("Test Resolve Path"))
                {
                    var provider = ((SaveSettings)target).CreatePathProvider();
                    Debug.Log($"Crystal Save path: {provider.GetRootPath()}");
                }
                if (GUILayout.Button("Move data from old path to new"))
                {
                    var provider = ((SaveSettings)target).CreatePathProvider();
                    PersistentPathMigration.TryMigrate(Application.persistentDataPath, provider.GetRootPath());
                }
                EditorGUILayout.PropertyField(runMigrationProp, new GUIContent("Run Migration On Startup"));
                EditorGUILayout.PropertyField(autoSaveMigratedDataProp);
            }
            EditorGUI.indentLevel--;
        }

        void DrawAddressablesToggle()
        {
            if (useAddressablesProp == null)
                return;

            bool addressablesPresent = false;
#if REMEMBERME_ADDRESSABLES_PRESENT
            addressablesPresent = true;
#endif

            using (new EditorGUI.DisabledScope(!addressablesPresent))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(useAddressablesProp);
                if (useAddressablesProp.boolValue)
                {
                    if (GUILayout.Button(AddressablesHelpWindow.HelpButtonContent, EditorStyles.miniButton, GUILayout.Width(22f)))
                    {
                        AddressablesHelpWindow.ShowWindow();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (!addressablesPresent)
            {
                if (useAddressablesProp.boolValue)
                {
                    useAddressablesProp.boolValue = false;
                }
                EditorGUILayout.HelpBox("Unity Addressables package is not installed. Install it to enable Addressables integration for Crystal Save.", MessageType.Info);
            }
        }

        void DrawDeferredReuseToggle()
        {
            if (enableAssetReuseDuringDeferredProp == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Deferred Processing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                enableAssetReuseDuringDeferredProp,
                new GUIContent(
                    "Enable Asset-based Reuse (Deferred)",
                    "Allow PrefabManager to claim existing scene instances by PrefabAssetID during deferred processing.\n" +
                    "When off, deferred entries will always instantiate new instances unless an exact UniqueID match is found.")
            );
        }
        
#if CRYSTALSAVE_TIMEMACHINE
        void DrawTimeMachinePresetHelper()
        {
            var presetType = (TimeMachinePresetType)timeMachinePresetProp.enumValueIndex;
            
            // Info box with preset description
            if (presetType != TimeMachinePresetType.Custom)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    var config = TimeMachinePresets.GetPresetConfig(presetType);
                    if (config != null)
                    {
                        EditorGUILayout.LabelField("📋 Preset Configuration", EditorStyles.boldLabel);
                        EditorGUILayout.Space(3);
                        
                        EditorGUILayout.LabelField(config.GetDescription(), EditorStyles.wordWrappedLabel);
                        EditorGUILayout.Space(5);
                        
                        EditorGUILayout.LabelField("Settings:", EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField($"• Resume Mode: {config.resumeMode}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Branch Behavior: {config.autoBranchBehavior}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Branch Copy: {config.branchCopyMode}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Ghost Mode: {(config.allowRecordingDuringPlayback ? "✅ Enabled" : "❌ Disabled")}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Continuous Time: {(config.useContinuousTime ? "✅ Enabled" : "❌ Disabled")}", EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "✅ This preset configuration is automatically applied. Advanced settings below will be overridden by this preset. " +
                    "Select 'Custom' preset to manually control all settings.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "⚙️ Custom preset selected - you have full manual control over all advanced settings below.",
                    MessageType.Info);
            }
        }
#endif
    }
}
#endif
