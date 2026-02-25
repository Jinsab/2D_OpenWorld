#if UNITY_EDITOR
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

// Alias Logger to reference the correct namespace
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(TagRegistry))]
	public class TagRegistryEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			// Draw the default inspector UI
			DrawDefaultInspector();

			// Reference to the TagRegistry instance
			TagRegistry registry = (TagRegistry)target;

			GUILayout.Space(10); // Add some spacing

			// Add a button labeled "Auto-Register Tags"
			if (GUILayout.Button("Auto-Register Tags"))
			{
				AutoRegisterTags(registry);
			}
		}

		/// <summary>
		/// Retrieves all Tags defined in the Unity project and assigns them to the TagRegistry.
		/// </summary>
		/// <param name="registry">The TagRegistry instance to populate.</param>
		private void AutoRegisterTags(TagRegistry registry)
		{
			// Retrieve all Tags using InternalEditorUtility
			string[] allTags = UnityEditorInternal.InternalEditorUtility.tags;

			// Clear the existing Tags list to avoid duplicates
			registry.Tags.Clear();

			// Populate the Tags list with all retrieved Tags
			foreach (string tag in allTags)
			{
				registry.Tags.Add(tag);
			}

			// Mark the registry as dirty to ensure changes are saved
			EditorUtility.SetDirty(registry);

			// Save all modified assets to disk
			AssetDatabase.SaveAssets();

			// Log the successful operation
			Logger.Log($"TagRegistryEditor: Successfully registered {allTags.Length} tags.", LogLevel.Info);

			// Provide user feedback in the Editor
			EditorUtility.DisplayDialog("Tag Registry", $"Successfully registered {allTags.Length} tags.", "OK");
		}
	}
}
#endif
