#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	public class MigrationWindow : EditorWindow
	{
		private MigrationManager migrationManager;
		private SerializedObject serializedMigrationManager;
		private SerializedProperty migrationStepsProperty;

		private string newDescription = "";
		private List<MigrationAction> newMigrationActions = new List<MigrationAction>();
		private MigrationAction selectedMigrationAction;

		private Texture2D logoSprite;

		[MenuItem("Tools/Crystal Save/Migration Window")]
		public static void ShowWindow()
		{
			GetWindow<MigrationWindow>("Migration Manager");
		}

		private void OnEnable()
		{
			// Define separate paths for MigrationManager.asset and the logo sprite
			string migrationManagerPath = "Assets/Plugins/CrystalSave/Resources/MigrationManager.asset";
			string logoPath = "Assets/Plugins/CrystalSave/Editor/Logo/DataMigrationLogo.PNG";

			// Load or create the MigrationManager asset
			migrationManager = AssetDatabase.LoadAssetAtPath<MigrationManager>(migrationManagerPath);
			if (migrationManager == null)
			{
				Debug.LogWarning($"MigrationManager asset not found at path: {migrationManagerPath}. Creating a new one.");
				migrationManager = CreateInstance<MigrationManager>();
				EnsureDirectoryExists(System.IO.Path.GetDirectoryName(migrationManagerPath));
				AssetDatabase.CreateAsset(migrationManager, migrationManagerPath);
				AssetDatabase.SaveAssets();
				Debug.Log("MigrationManager asset created.");
			}
			else
			{
				Debug.Log($"MigrationManager asset loaded from path: {migrationManagerPath}.");
			}

			// Load the logo sprite
			logoSprite = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);

			if (logoSprite == null)
			{
				Debug.LogWarning($"MigrationWindow: Logo sprite not found at path: {logoPath}. Ensure the file exists and the path is correct.");
			}
			else
			{
				Debug.Log("MigrationWindow: Logo sprite loaded successfully.");
			}

			serializedMigrationManager = new SerializedObject(migrationManager);
			migrationStepsProperty = serializedMigrationManager.FindProperty("migrationSteps");

			if (migrationStepsProperty == null)
			{
				Debug.LogError("MigrationSteps property not found in MigrationManager. Ensure it is properly serialized.");
			}
			else
			{
				Debug.Log($"MigrationSteps list found with {migrationStepsProperty.arraySize} steps.");
			}
		}

		private void OnGUI()
		{
			// Display the logo at the top center
			if (logoSprite != null)
			{
				// Calculate dynamic logo size based on window width
				float windowWidth = position.width;
				float logoWidth = windowWidth * 0.5f; // 50% of window width
				float aspectRatio = (float)logoSprite.height / logoSprite.width;
				float logoHeight = logoWidth * aspectRatio;

				// Begin horizontal layout to center the logo
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label(logoSprite, GUILayout.Width(logoWidth), GUILayout.Height(logoHeight));
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				EditorGUILayout.Space(); // Add some space below the logo
			}
			else
			{
				EditorGUILayout.Space(); // Ensure there's space even if the logo is missing
			}

			if (migrationStepsProperty == null)
			{
				EditorGUILayout.HelpBox("MigrationSteps property is missing. Please ensure MigrationManager.asset is correctly set up.", MessageType.Error);
				return;
			}

			serializedMigrationManager.Update();

			GUILayout.Label("Migration Steps", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			// Iterate through the migration steps
			for (int i = 0; i < migrationStepsProperty.arraySize; i++)
			{
				SerializedProperty step = migrationStepsProperty.GetArrayElementAtIndex(i);
				SerializedProperty targetVersionProp = step.FindPropertyRelative("targetVersion");
				SerializedProperty descriptionProp = step.FindPropertyRelative("description");
				SerializedProperty migrationActionsProp = step.FindPropertyRelative("migrationActions");

				bool isValid = true;
				string errorMessage = "";

				// Validate SerializedProperties
				if (targetVersionProp == null)
				{
					isValid = false;
					errorMessage += "- Missing 'targetVersion' property.\n";
				}
				if (descriptionProp == null)
				{
					isValid = false;
					errorMessage += "- Missing 'description' property.\n";
				}
				if (migrationActionsProp == null)
				{
					isValid = false;
					errorMessage += "- Missing 'migrationActions' property.\n";
				}

				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.BeginHorizontal();

				// Target Version Display
				EditorGUILayout.BeginVertical();
				EditorGUILayout.LabelField("Target Version:", EditorStyles.boldLabel);
				if (isValid && targetVersionProp != null)
				{
					int major = targetVersionProp.FindPropertyRelative("Major").intValue;
					int minor = targetVersionProp.FindPropertyRelative("Minor").intValue;
					int patch = targetVersionProp.FindPropertyRelative("Patch").intValue;
					EditorGUILayout.LabelField("Major", major.ToString());
					EditorGUILayout.LabelField("Minor", minor.ToString());
					EditorGUILayout.LabelField("Patch", patch.ToString());
				}
				else
				{
					EditorGUILayout.LabelField("Major", "N/A");
					EditorGUILayout.LabelField("Minor", "N/A");
					EditorGUILayout.LabelField("Patch", "N/A");
				}
				EditorGUILayout.EndVertical();

				// Description and Migration Actions
				EditorGUILayout.BeginVertical();
				if (isValid && descriptionProp != null && migrationActionsProp != null)
				{
					EditorGUILayout.PropertyField(descriptionProp, new GUIContent("Description", "A brief description of what this migration step does."));

					// Handle the list of MigrationActions
					EditorGUILayout.LabelField("Migration Actions:", EditorStyles.boldLabel);
					EditorGUI.indentLevel++;

					for (int j = 0; j < migrationActionsProp.arraySize; j++)
					{
						SerializedProperty actionProp = migrationActionsProp.GetArrayElementAtIndex(j);
						EditorGUILayout.PropertyField(actionProp, new GUIContent($"Action {j + 1}"));
					}

					// Button to add a new MigrationAction
					if (GUILayout.Button("Add Migration Action"))
					{
						migrationActionsProp.arraySize++;
						SerializedProperty newActionProp = migrationActionsProp.GetArrayElementAtIndex(migrationActionsProp.arraySize - 1);
						newActionProp.objectReferenceValue = null; // Initialize with null; user can assign later
					}

					// Button to remove the last MigrationAction
					if (migrationActionsProp.arraySize > 0 && GUILayout.Button("Remove Last Migration Action"))
					{
						migrationActionsProp.arraySize--;
					}

					EditorGUI.indentLevel--;
				}
				else
				{
					EditorGUILayout.HelpBox("One or more properties are missing for this migration step.", MessageType.Warning);
				}
				EditorGUILayout.EndVertical();

				// Remove Button
				EditorGUILayout.BeginVertical();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Remove", GUILayout.Width(60)))
				{
					if (EditorUtility.DisplayDialog("Confirm Removal", $"Are you sure you want to remove migration step at index {i}?", "Yes", "No"))
					{
						migrationStepsProperty.DeleteArrayElementAtIndex(i);
						Debug.Log($"Migration step at index {i} removed.");
						break; // Exit the loop to avoid indexing issues after deletion
					}
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				EditorGUILayout.Space();
			}

			// Add "Clean Up" Button
			EditorGUILayout.Space();
			if (GUILayout.Button("Clean Up Invalid Migration Steps"))
			{
				if (EditorUtility.DisplayDialog("Confirm Clean Up", "Are you sure you want to remove all invalid migration steps?", "Yes", "No"))
				{
					int removedCount = 0;
					// Iterate in reverse to avoid indexing issues when removing elements
					for (int i = migrationStepsProperty.arraySize - 1; i >= 0; i--)
					{
						SerializedProperty step = migrationStepsProperty.GetArrayElementAtIndex(i);
						SerializedProperty targetVersionProp = step.FindPropertyRelative("targetVersion");
						SerializedProperty descriptionProp = step.FindPropertyRelative("description");
						SerializedProperty migrationActionsProp = step.FindPropertyRelative("migrationActions");

						bool isInvalid = false;

						if (targetVersionProp == null || descriptionProp == null || migrationActionsProp == null)
						{
							isInvalid = true;
						}

						if (isInvalid)
						{
							migrationStepsProperty.DeleteArrayElementAtIndex(i);
							removedCount++;
							Debug.Log($"Migration step at index {i} removed during clean up.");
						}
					}

					if (removedCount > 0)
					{
						Debug.Log($"Clean Up: Removed {removedCount} invalid migration step(s).");
						EditorUtility.DisplayDialog("Clean Up Completed", $"Removed {removedCount} invalid migration step(s).", "OK");
					}
					else
					{
						EditorUtility.DisplayDialog("Clean Up Completed", "No invalid migration steps found.", "OK");
					}
				}
			}

			GUILayout.Label("Add New Migration Step", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			// Display the current game version from SaveSettings as a HelpBox
			DisplayCurrentGameVersionHelpBox();

			EditorGUILayout.Space();

			// Input fields for new migration step
			newDescription = EditorGUILayout.TextField("Description", newDescription);

			GUILayout.BeginHorizontal();
			selectedMigrationAction = (MigrationAction)EditorGUILayout.ObjectField("Migration Action", selectedMigrationAction, typeof(MigrationAction), false);
			if (GUILayout.Button("Add to List"))
			{
				if (selectedMigrationAction != null && !newMigrationActions.Contains(selectedMigrationAction))
				{
					newMigrationActions.Add(selectedMigrationAction);
					Debug.Log($"Migration Action '{selectedMigrationAction.name}' added to the new migration step.");
				}
				else if (newMigrationActions.Contains(selectedMigrationAction))
				{
					EditorUtility.DisplayDialog("Duplicate Action", "This Migration Action is already in the list.", "OK");
				}
				else
				{
					EditorUtility.DisplayDialog("No Action Selected", "Please select a Migration Action to add.", "OK");
				}
			}
			GUILayout.EndHorizontal();

			// Display the list of selected MigrationActions
			if (newMigrationActions.Count > 0)
			{
				EditorGUILayout.LabelField("Selected Migration Actions:", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				for (int i = 0; i < newMigrationActions.Count; i++)
				{
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.ObjectField($"Action {i + 1}", newMigrationActions[i], typeof(MigrationAction), false);
					if (GUILayout.Button("Remove", GUILayout.Width(60)))
					{
						newMigrationActions.RemoveAt(i);
						Debug.Log($"Migration Action at index {i} removed from the new migration step.");
						break;
					}
					EditorGUILayout.EndHorizontal();
				}
				EditorGUI.indentLevel--;
			}

			// Button to clear all selected MigrationActions
			if (newMigrationActions.Count > 0 && GUILayout.Button("Clear All Selected Actions"))
			{
				newMigrationActions.Clear();
				Debug.Log("All selected Migration Actions have been cleared.");
			}

			// Button to add the new Migration Step
			if (GUILayout.Button("Add Migration Step"))
			{
				if (string.IsNullOrWhiteSpace(newDescription))
				{
					EditorUtility.DisplayDialog("Missing Description", "Please enter a description for the migration step.", "OK");
				}
				else if (newMigrationActions.Count == 0)
				{
					EditorUtility.DisplayDialog("No Migration Actions", "Please add at least one Migration Action for this step.", "OK");
				}
				else
				{
					// Retrieve the current game version from SaveSettings
					SaveSettings saveSettings = LoadSaveSettings();
					if (saveSettings == null)
					{
						EditorUtility.DisplayDialog("Missing SaveSettings", "SaveSettings asset not found in Resources. Please create one via the Crystal Save Save Settings menu.", "OK");
						return;
					}

					VersionData targetVersion = saveSettings.version.Clone() as VersionData;

					// Check if a migration step for the current version already exists
					bool exists = false;
					for (int i = 0; i < migrationStepsProperty.arraySize; i++)
					{
						SerializedProperty step = migrationStepsProperty.GetArrayElementAtIndex(i);
						SerializedProperty existingVersionProp = step.FindPropertyRelative("targetVersion");
						if (existingVersionProp == null)
						{
							Debug.LogError($"Migration step at index {i} is missing 'targetVersion' property.");
							continue;
						}

						int major = existingVersionProp.FindPropertyRelative("Major").intValue;
						int minor = existingVersionProp.FindPropertyRelative("Minor").intValue;
						int patch = existingVersionProp.FindPropertyRelative("Patch").intValue;

						if (major == targetVersion.Major && minor == targetVersion.Minor && patch == targetVersion.Patch)
						{
							exists = true;
							break;
						}
					}

					if (exists)
					{
						EditorUtility.DisplayDialog("Migration Step Exists", $"A migration step targeting version {targetVersion} already exists.", "OK");
					}
					else
					{
						AddMigrationStep(targetVersion, newDescription, newMigrationActions);
						// Reset input fields after adding
						newDescription = "";
						newMigrationActions.Clear();
						selectedMigrationAction = null;
					}
				}
			}

			serializedMigrationManager.ApplyModifiedProperties();
		}

		/// <summary>
		/// Displays the current game version in a styled box with bold text.
		/// </summary>
		private void DisplayCurrentGameVersionHelpBox()
		{
			SaveSettings saveSettings = LoadSaveSettings();
			if (saveSettings != null)
			{
				string versionString = $"{saveSettings.version.Major}.{saveSettings.version.Minor}.{saveSettings.version.Patch}";
				string message = "This is the target version for all migration steps.";

				// Begin a vertical group with a help box background
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);

				// Display the "Current Game Version:" label in bold
				EditorGUILayout.LabelField("Current Game Version:", EditorStyles.boldLabel);

				// Display the version number in bold
				EditorGUILayout.LabelField(versionString, EditorStyles.boldLabel);

				// Display the informational message in regular style
				EditorGUILayout.LabelField(message, EditorStyles.label);

				EditorGUILayout.EndVertical();
			}
			else
			{
				// Display an error help box if SaveSettings is missing
				EditorGUILayout.HelpBox("SaveSettings asset not found. Please create one via the Crystal Save Save Settings menu.", MessageType.Error);
			}
		}

		/// <summary>
		/// Ensures that the directory exists; if not, creates it.
		/// </summary>
		/// <param name="path">The directory path.</param>
		private void EnsureDirectoryExists(string path)
		{
			if (!AssetDatabase.IsValidFolder(path))
			{
				string[] folders = path.Split('/');
				string currentPath = "";
				foreach (string folder in folders)
				{
					if (string.IsNullOrEmpty(currentPath))
						currentPath = folder;
					else
						currentPath = $"{currentPath}/{folder}";

					if (!AssetDatabase.IsValidFolder(currentPath))
					{
						AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(currentPath), folder);
						Debug.Log($"Folder created at: {currentPath}");
					}
				}
			}
		}

		/// <summary>
		/// Loads SaveSettings from the Resources folder.
		/// </summary>
		/// <returns>The loaded SaveSettings instance.</returns>
		private SaveSettings LoadSaveSettings()
		{
			SaveSettings saveSettings = AssetDatabase.LoadAssetAtPath<SaveSettings>("Assets/Plugins/CrystalSave/Resources/SaveSettings.asset");
			if (saveSettings == null)
			{
				Debug.LogError("MigrationWindow: SaveSettings asset not found in Resources. Please create one via the Crystal Save Save Settings menu.");
			}
			return saveSettings;
		}

		/// <summary>
		/// Adds a new migration step to the MigrationManager with multiple MigrationActions.
		/// </summary>
		/// <param name="version">The target VersionInfo.</param>
		/// <param name="description">Description of the migration.</param>
		/// <param name="actions">The list of MigrationActions to apply.</param>
		private void AddMigrationStep(VersionData version, string description, List<MigrationAction> actions)
		{
			if (migrationStepsProperty == null)
			{
				Debug.LogError("AddMigrationStep: migrationStepsProperty is null. Cannot add migration step.");
				return;
			}

			migrationStepsProperty.arraySize++;
			SerializedProperty newStep = migrationStepsProperty.GetArrayElementAtIndex(migrationStepsProperty.arraySize - 1);

			SerializedProperty targetVersionProp = newStep.FindPropertyRelative("targetVersion");
			SerializedProperty descriptionProp = newStep.FindPropertyRelative("description");
			SerializedProperty migrationActionsProp = newStep.FindPropertyRelative("migrationActions");

			if (targetVersionProp == null || descriptionProp == null || migrationActionsProp == null)
			{
				Debug.LogError("AddMigrationStep: One or more SerializedProperties are null. Ensure MigrationStep is properly defined.");
				return;
			}

			// Assign the cloned VersionInfo fields individually
			targetVersionProp.FindPropertyRelative("Major").intValue = version.Major;
			targetVersionProp.FindPropertyRelative("Minor").intValue = version.Minor;
			targetVersionProp.FindPropertyRelative("Patch").intValue = version.Patch;

			descriptionProp.stringValue = description;

			// Assign each MigrationAction to the migrationActions list
			foreach (var action in actions)
			{
				if (action != null)
				{
					migrationActionsProp.arraySize++;
					SerializedProperty newActionProp = migrationActionsProp.GetArrayElementAtIndex(migrationActionsProp.arraySize - 1);
					newActionProp.objectReferenceValue = action;
				}
				else
				{
					Debug.LogWarning("AddMigrationStep: Encountered a null MigrationAction. Skipping assignment.");
				}
			}

			Debug.Log($"Migration Step Added: Version {version} - {description} with {actions.Count} Action(s).");

			// Refresh the serialized object
			serializedMigrationManager.ApplyModifiedPropertiesWithoutUndo();
			serializedMigrationManager.Update();
		}
	}
}
#endif
#endif