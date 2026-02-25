#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(RememberMaterial))]
	[CanEditMultipleObjects]
	public class RememberMaterialEditor : UnityEditor.Editor
	{
		private SerializedProperty trackAllMaterialsProperty;
		private SerializedProperty materialIndicesProperty;
		private SerializedProperty rememberColorProperty;
		private SerializedProperty rememberMainTextureProperty;
		private SerializedProperty rememberAdditionalTexturesProperty;
		private SerializedProperty rememberFloatPropertiesProperty;
		private SerializedProperty rememberVectorPropertiesProperty;
		private SerializedProperty rememberShaderProperty;
		private SerializedProperty rememberRenderQueueProperty;
		private SerializedProperty rememberShaderKeywordsProperty;
		private SerializedProperty skipSavingWhenUnchangedProperty;
		private SerializedProperty enablePerformanceCachingProperty;

		private bool showPropertyToggles = true;

		private void OnEnable()
		{
			// Early exit if serializedObject is invalid
			if (serializedObject == null || target == null)
				return;

			trackAllMaterialsProperty = serializedObject.FindProperty("trackAllMaterials");
			materialIndicesProperty = serializedObject.FindProperty("materialIndices");
			rememberColorProperty = serializedObject.FindProperty("RememberColor");
			rememberMainTextureProperty = serializedObject.FindProperty("RememberMainTexture");
			rememberAdditionalTexturesProperty = serializedObject.FindProperty("RememberAdditionalTextures");
			rememberFloatPropertiesProperty = serializedObject.FindProperty("RememberFloatProperties");
			rememberVectorPropertiesProperty = serializedObject.FindProperty("RememberVectorProperties");
			rememberShaderProperty = serializedObject.FindProperty("RememberShader");
			rememberRenderQueueProperty = serializedObject.FindProperty("RememberRenderQueue");
			rememberShaderKeywordsProperty = serializedObject.FindProperty("RememberShaderKeywords");
			skipSavingWhenUnchangedProperty = serializedObject.FindProperty("skipSavingWhenUnchanged");
			enablePerformanceCachingProperty = serializedObject.FindProperty("enablePerformanceCaching");
		}

		public override void OnInspectorGUI()
		{
			// Validate serializedObject
			if (serializedObject == null || target == null)
				return;

			serializedObject.Update();

			RememberMaterial rememberMaterial = (RememberMaterial)target;
			if (rememberMaterial == null)
				return;

			Renderer renderer = rememberMaterial.GetComponent<Renderer>();

			if (renderer == null)
			{
				EditorGUILayout.HelpBox("No Renderer component found on this GameObject. RememberMaterial requires a Renderer.", MessageType.Warning);
				serializedObject.ApplyModifiedProperties();
				return;
			}

			Material[] materials = renderer.sharedMaterials;
			if (materials == null || materials.Length == 0)
			{
				EditorGUILayout.HelpBox("Renderer has no materials assigned.", MessageType.Info);
				serializedObject.ApplyModifiedProperties();
				return;
			}

			DrawInfoHeader();
			EditorGUILayout.Space(5);

			DrawMaterialSelection(materials);
			EditorGUILayout.Space(10);

			DrawPropertyToggles();
			EditorGUILayout.Space(10);

			DrawOptimizationSettings();

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawInfoHeader()
		{
			EditorGUILayout.LabelField("Material Tracking", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"This component saves and restores material properties. " +
				"Choose to track all materials or select specific material indices.",
				MessageType.Info
			);
		}

		private void DrawMaterialSelection(Material[] materials)
		{
			if (trackAllMaterialsProperty == null || materialIndicesProperty == null)
			{
				EditorGUILayout.HelpBox("Could not find material tracking properties. Please reload the editor.", MessageType.Error);
				return;
			}

			EditorGUILayout.LabelField("Material Selection", EditorStyles.boldLabel);

			EditorGUILayout.PropertyField(trackAllMaterialsProperty, new GUIContent("Track All Materials"));

			if (!trackAllMaterialsProperty.boolValue)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.LabelField("Material Indices to Track:", EditorStyles.miniBoldLabel);

				// Display current indices with material previews
				for (int i = 0; i < materialIndicesProperty.arraySize; i++)
				{
					EditorGUILayout.BeginHorizontal();

					SerializedProperty indexProp = materialIndicesProperty.GetArrayElementAtIndex(i);
					int matIndex = indexProp.intValue;

					// Material preview and name
					if (matIndex >= 0 && matIndex < materials.Length && materials[matIndex] != null)
					{
						Material mat = materials[matIndex];
						
						// Material preview texture
						if (mat != null)
						{
							Texture preview = AssetPreview.GetAssetPreview(mat);
							if (preview != null)
							{
								GUILayout.Label(preview, GUILayout.Width(32), GUILayout.Height(32));
							}
							else
							{
								GUILayout.Box("", GUILayout.Width(32), GUILayout.Height(32));
							}
						}

						EditorGUILayout.LabelField($"Index {matIndex}: {mat.name}", GUILayout.MinWidth(150));
					}
					else
					{
						EditorGUILayout.LabelField($"Index {matIndex} (Invalid)", GUILayout.MinWidth(150));
					}

					// Index field
					EditorGUI.BeginChangeCheck();
					int newIndex = EditorGUILayout.IntField(matIndex, GUILayout.Width(50));
					if (EditorGUI.EndChangeCheck())
					{
						indexProp.intValue = Mathf.Clamp(newIndex, 0, materials.Length - 1);
					}

					// Remove button
					if (GUILayout.Button("-", GUILayout.Width(30)))
					{
						materialIndicesProperty.DeleteArrayElementAtIndex(i);
					}

					EditorGUILayout.EndHorizontal();
				}

				// Add button
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(EditorGUI.indentLevel * 15);
				if (GUILayout.Button("Add Material Index", GUILayout.Width(150)))
				{
					materialIndicesProperty.arraySize++;
					SerializedProperty newIndexProp = materialIndicesProperty.GetArrayElementAtIndex(materialIndicesProperty.arraySize - 1);
					newIndexProp.intValue = 0;
				}
				EditorGUILayout.EndHorizontal();

				EditorGUI.indentLevel--;
			}
			else
			{
				// Show all materials being tracked
				EditorGUI.indentLevel++;
				EditorGUILayout.LabelField($"Tracking {materials.Length} material(s):", EditorStyles.miniBoldLabel);
				
				for (int i = 0; i < materials.Length; i++)
				{
					Material mat = materials[i];
					EditorGUILayout.BeginHorizontal();

					if (mat != null)
					{
						Texture preview = AssetPreview.GetAssetPreview(mat);
						if (preview != null)
						{
							GUILayout.Label(preview, GUILayout.Width(32), GUILayout.Height(32));
						}
						else
						{
							GUILayout.Box("", GUILayout.Width(32), GUILayout.Height(32));
						}
						
						EditorGUILayout.LabelField($"Index {i}: {mat.name}");
					}
					else
					{
						EditorGUILayout.LabelField($"Index {i}: (null)");
					}

					EditorGUILayout.EndHorizontal();
				}
				EditorGUI.indentLevel--;
			}
		}

		private void DrawPropertyToggles()
		{
			showPropertyToggles = EditorGUILayout.Foldout(showPropertyToggles, "Material Properties to Save", true, EditorStyles.foldoutHeader);

			if (showPropertyToggles)
			{
				EditorGUI.indentLevel++;

				if (rememberColorProperty != null)
					EditorGUILayout.PropertyField(rememberColorProperty, new GUIContent("Color", "Save and restore material color (_Color property)"));
				
				if (rememberMainTextureProperty != null)
					EditorGUILayout.PropertyField(rememberMainTextureProperty, new GUIContent("Main Texture", "Save and restore main texture (_MainTex property)"));
				
				if (rememberAdditionalTexturesProperty != null)
					EditorGUILayout.PropertyField(rememberAdditionalTexturesProperty, new GUIContent("Additional Textures", "Save and restore normal maps, metallic maps, etc."));
				
				if (rememberFloatPropertiesProperty != null)
					EditorGUILayout.PropertyField(rememberFloatPropertiesProperty, new GUIContent("Float Properties", "Save and restore float properties like metallic, glossiness, etc."));
				
				if (rememberVectorPropertiesProperty != null)
					EditorGUILayout.PropertyField(rememberVectorPropertiesProperty, new GUIContent("Vector Properties", "Save and restore texture tiling and offset"));
				
				if (rememberShaderProperty != null)
					EditorGUILayout.PropertyField(rememberShaderProperty, new GUIContent("Shader", "Save and restore shader reference"));
				
				if (rememberRenderQueueProperty != null)
					EditorGUILayout.PropertyField(rememberRenderQueueProperty, new GUIContent("Render Queue", "Save and restore render queue"));
				
				if (rememberShaderKeywordsProperty != null)
					EditorGUILayout.PropertyField(rememberShaderKeywordsProperty, new GUIContent("Shader Keywords", "Save and restore shader keywords"));

				EditorGUI.indentLevel--;
			}
		}

		private void DrawOptimizationSettings()
		{
			EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);

			EditorGUI.indentLevel++;

			if (skipSavingWhenUnchangedProperty != null)
			{
				EditorGUILayout.PropertyField(skipSavingWhenUnchangedProperty, new GUIContent(
					"Skip Saving When Unchanged",
					"Only save data if material properties have changed from their initial state. Reduces save file size."
				));
			}

			if (enablePerformanceCachingProperty != null)
			{
				EditorGUILayout.PropertyField(enablePerformanceCachingProperty, new GUIContent(
					"Enable Performance Caching",
					"Cache Renderer reference for faster serialization. Recommended for frequently saved objects."
				));
			}

			EditorGUI.indentLevel--;
		}
	}
}
#endif
#endif
