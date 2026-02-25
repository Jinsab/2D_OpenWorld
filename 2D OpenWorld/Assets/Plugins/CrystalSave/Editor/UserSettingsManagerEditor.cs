#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(UserSettingsManager))]
	public class UserSettingsManagerEditor : UnityEditor.Editor
	{
		// Cached serialized property references
		private SerializedProperty mainCameraProp;
		private SerializedProperty autoReferenceMainCameraProp;

#if UNITY_POST_PROCESSING_STACK_V2
        private SerializedProperty builtinPostProcessVolumeProp;
        private SerializedProperty autoReferencePostProcessVolumeProp;
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
		private SerializedProperty globalVolumeProp;
		private SerializedProperty autoReferenceGlobalVolumeProp;
#endif

		private SerializedProperty audioMixerProp;
		private SerializedProperty masterVolumeParamProp;
		private SerializedProperty musicVolumeParamProp;
		private SerializedProperty sfxVolumeParamProp;
		private SerializedProperty voiceVolumeParamProp;

		private void OnEnable()
		{
			// Cache camera properties
			mainCameraProp = serializedObject.FindProperty("mainCamera");
			autoReferenceMainCameraProp = serializedObject.FindProperty("autoReferenceMainCamera");

#if UNITY_POST_PROCESSING_STACK_V2
            // Cache Post-Processing properties
            builtinPostProcessVolumeProp = serializedObject.FindProperty("builtinPostProcessVolume");
            autoReferencePostProcessVolumeProp = serializedObject.FindProperty("autoReferencePostProcessVolume");
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
			// Cache global volume properties
			globalVolumeProp = serializedObject.FindProperty("globalVolume");
			autoReferenceGlobalVolumeProp = serializedObject.FindProperty("autoReferenceGlobalVolume");
#endif

			// Cache audio properties
			audioMixerProp = serializedObject.FindProperty("audioMixer");
			masterVolumeParamProp = serializedObject.FindProperty("masterVolumeParam");
			musicVolumeParamProp = serializedObject.FindProperty("musicVolumeParam");
			sfxVolumeParamProp = serializedObject.FindProperty("sfxVolumeParam");
			voiceVolumeParamProp = serializedObject.FindProperty("voiceVolumeParam");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawScriptField();
			EditorGUILayout.Space();
			DrawCameraSettings();

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
			EditorGUILayout.Space();
			DrawGlobalVolume();
#endif

#if UNITY_POST_PROCESSING_STACK_V2
            EditorGUILayout.Space();
            DrawPostProcessingSettings();
#endif

			EditorGUILayout.Space();
			DrawAudioSettings();

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawScriptField()
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
			EditorGUI.EndDisabledGroup();
		}

		private void DrawCameraSettings()
		{
			EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(autoReferenceMainCameraProp, new GUIContent("Auto Reference Main Camera"));
			if (!autoReferenceMainCameraProp.boolValue)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(mainCameraProp, new GUIContent("Main Camera"));
				EditorGUI.indentLevel--;
        }
}

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
		private void DrawGlobalVolume()
		{
			EditorGUILayout.LabelField("Global Volume (URP/HDRP)", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(autoReferenceGlobalVolumeProp, new GUIContent("Auto Reference Global Volume"));
			if (!autoReferenceGlobalVolumeProp.boolValue)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(globalVolumeProp, new GUIContent("Global Volume"));
				EditorGUI.indentLevel--;
			}
		}
#endif

#if UNITY_POST_PROCESSING_STACK_V2
        private void DrawPostProcessingSettings()
        {
            EditorGUILayout.LabelField("Built-in Post Processing (v2)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoReferencePostProcessVolumeProp, new GUIContent("Auto Reference Post Process Volume"));
            if (!autoReferencePostProcessVolumeProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(builtinPostProcessVolumeProp, new GUIContent("Post Process Volume"));
                EditorGUI.indentLevel--;
            }
        }
#endif

		private void DrawAudioSettings()
		{
			EditorGUILayout.LabelField("Audio Mixer Settings", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(audioMixerProp);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(masterVolumeParamProp, new GUIContent("Master Volume Param"));
			EditorGUILayout.PropertyField(musicVolumeParamProp, new GUIContent("Music Volume Param"));
			EditorGUILayout.PropertyField(sfxVolumeParamProp, new GUIContent("SFX Volume Param"));
			EditorGUILayout.PropertyField(voiceVolumeParamProp, new GUIContent("Voice Volume Param"));
			EditorGUI.indentLevel--;
		}
	}
}
#endif
