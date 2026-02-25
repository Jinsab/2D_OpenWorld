#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(SaveManager))]
	public class SaveManagerEditor : UnityEditor.Editor
	{
		SerializedProperty keepAcrossScenesProperty;

		void OnEnable()
		{
			if (target == null) return;
			keepAcrossScenesProperty = serializedObject.FindProperty("keepAcrossScenes");
		}

		public override void OnInspectorGUI()
		{
			if (serializedObject == null) return;

			serializedObject.Update();

			// Draw everything except m_Script (and the obsolete keepAcrossScenes)
			DrawPropertiesExcluding(serializedObject, "m_Script", "keepAcrossScenes");

			var sm = (SaveManager)target;

			/* ---- Keep-Across-Scenes block only if the field still exists ---- */
			if (keepAcrossScenesProperty != null)
			{
				bool isRoot = sm.transform.root == sm.transform;

				// Auto-clear when not root
				if (!isRoot && keepAcrossScenesProperty.boolValue)
				{
					keepAcrossScenesProperty.boolValue = false;
					serializedObject.ApplyModifiedProperties();
				}

				EditorGUI.BeginDisabledGroup(!isRoot);
				EditorGUILayout.PropertyField(
					keepAcrossScenesProperty,
					new GUIContent("Keep Across Scenes",
						"If true, this GameObject is preserved across scene loads (DontDestroyOnLoad). " +
						"Only has an effect when this is a root object.")
				);
				EditorGUI.EndDisabledGroup();
			}
                        else
                        {
                                EditorGUILayout.HelpBox(
                                        "This version of SaveManager is always kept across scenes. " +
                                        "The old 'Keep Across Scenes' toggle has been removed.",
                                        MessageType.Info);
                        }

                        serializedObject.ApplyModifiedProperties();

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Lookup Cache", EditorStyles.boldLabel);
                        bool cacheEnabled = EditorGUILayout.Toggle("Enabled", sm.LookupCacheEnabled);
                        if (cacheEnabled != sm.LookupCacheEnabled)
                        {
                                sm.SetLookupCacheEnabled(cacheEnabled);
                        }
                        if (GUILayout.Button("Clear Lookup Cache"))
                        {
                                sm.ClearLookupCache();
                        }
                }
        }
}

#endif
#endif