#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using Logger = Arawn.CrystalSave.Runtime.Logger;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(UniqueID))]
	public class UniqueIDEditor : UnityEditor.Editor
	{
	private SerializedProperty idProperty;
	private bool isEditing = false;
	private string newID = "";

	private static readonly Dictionary<string, DateTime> lastLoggedWarnings = new();
	private const int WarningIntervalSeconds = 10;
	
	private static SaveSettings cachedSettings;
	private static bool settingsCacheInitialized = false;

	// Cached duplicate-check data – refreshed on hierarchy change, not every repaint
	private static UniqueID[] cachedAllUniqueIDs;
	private static bool uniqueIDCacheDirty = true;		private void OnEnable()
		{
			idProperty = serializedObject.FindProperty("id");
			uniqueIDCacheDirty = true; // rescan when a different UniqueID is selected
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			/*─────────────────────────  READ-ONLY DISPLAY  ─────────────────────*/
			DrawPropertiesExcluding(serializedObject, "m_Script", "id");
			EditorGUILayout.Space();

			GUIStyle bold = new(EditorStyles.label) { fontStyle = FontStyle.Bold };
			EditorGUILayout.LabelField("Unique ID", idProperty.stringValue, bold);
			EditorGUILayout.Space();

			/*─────────────────────────  BUTTONS  ───────────────────────────────*/
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Edit", GUILayout.Width(100)))
			{
				newID = idProperty.stringValue;
				isEditing = true;
			}

			if (GUILayout.Button("Generate New ID", GUILayout.Width(150)))
			{
				if (EditorUtility.DisplayDialog("Generate New Unique ID",
					"Generate a new Unique ID?  This will break existing save-game references.",
					"Yes", "No"))
				{
					GenerateNewID();
				}
			}

			if (GUILayout.Button("Copy ID", GUILayout.Width(100)))
			{
				EditorGUIUtility.systemCopyBuffer = idProperty.stringValue;
				EditorUtility.DisplayDialog("Copied", "Unique ID copied to clipboard.", "OK");
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			/*─────────────────────────  EDIT PANEL  ────────────────────────────*/
			if (isEditing)
			{
				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.LabelField("Edit Unique ID", EditorStyles.boldLabel);

				newID = EditorGUILayout.TextField("New ID", newID);

				if (!IsValidID(newID))
					EditorGUILayout.HelpBox("Unique ID cannot be empty or whitespace.", MessageType.Error);

				if (GUILayout.Button("Auto-Generate ID", GUILayout.Width(150)))
					newID = Guid.NewGuid().ToString("N");                // ← FORMAT FIX

				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("OK"))
				{
					if (IsValidID(newID))
					{
						Undo.RecordObject(target, "Edit Unique ID");
						idProperty.stringValue = newID;
						serializedObject.ApplyModifiedProperties();
						isEditing = false;
						EditorUtility.SetDirty(target);

						Logger.Log($"UniqueIDEditor: Edited ID to '{newID}' for '{((UniqueID)target).gameObject.name}'.");
						EditorUtility.DisplayDialog("Unique ID Edited", $"Unique ID updated to:\n{newID}", "OK");
					}
					else
					{
						EditorUtility.DisplayDialog("Invalid ID", "ID cannot be empty or whitespace.", "OK");
					}
				}

				if (GUILayout.Button("Cancel")) isEditing = false;
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
			}
			else
			{
				// Friendly reminder when not editing
				EditorGUILayout.HelpBox(
					"Changing a Unique ID will orphan any existing save-game references. " +
					"Only do this on brand-new objects.", MessageType.Info);
			}

			/*─────────────────────────  DUPLICATE CHECK  ───────────────────────*/
			CheckForDuplicateIDs();
			serializedObject.ApplyModifiedProperties();
		}

		/*─────────────────────────  HELPERS  ──────────────────────────────────*/
		private bool IsValidID(string id) => !string.IsNullOrWhiteSpace(id);

		private void GenerateNewID()
		{
			string generatedID = Guid.NewGuid().ToString("N");           // ← FORMAT FIX

			Undo.RecordObject(target, "Generate New Unique ID");
			idProperty.stringValue = generatedID;
			serializedObject.ApplyModifiedProperties();
			EditorUtility.SetDirty(target);

			Logger.Log($"UniqueIDEditor: Generated new ID '{generatedID}' for '{((UniqueID)target).gameObject.name}'.");
			EditorUtility.DisplayDialog("New Unique ID Generated", $"A new Unique ID has been generated:\n{generatedID}", "OK");
		}

	private void CheckForDuplicateIDs()
	{
		// Check if duplicate ID validation is enabled in settings
		var settings = GetCachedSettings();
		bool shouldCheckDuplicates = (settings == null || !settings.skipDuplicateIDCheck);			if (!shouldCheckDuplicates) return;

			// Use cached array instead of scanning every repaint
			UniqueID[] all = GetCachedUniqueIDs();

			string currentID = idProperty.stringValue;
			if (string.IsNullOrEmpty(currentID)) return;

			int duplicates = 0;
			foreach (var uid in all) if (uid.ID == currentID) duplicates++;

			if (duplicates > 1)
			{
				EditorGUILayout.HelpBox(
					$"Warning: the Unique ID '{currentID}' is duplicated in the scene. " +
					"All Unique IDs must be distinct to avoid conflicts.",
					MessageType.Warning);

				string key = $"UniqueID-{currentID}";
				if (ShouldLogWarning(key))
				{
					Debug.LogWarning($"Duplicate Unique ID detected: '{currentID}' on multiple objects.");
					UpdateLastLoggedTime(key);
				}
			}
		}

		private static SaveSettings GetCachedSettings()
		{
			if (!settingsCacheInitialized)
			{
				settingsCacheInitialized = true;
				try
				{
					string[] guids = AssetDatabase.FindAssets("t:SaveSettings");
					foreach (string guid in guids)
					{
						string path = AssetDatabase.GUIDToAssetPath(guid);
						var asset = AssetDatabase.LoadAssetAtPath<SaveSettings>(path);
						if (asset != null)
						{
							cachedSettings = asset;
							break;
						}
					}
				}
				catch {}
			}
			return cachedSettings;
		}

		private bool ShouldLogWarning(string key) =>
			!lastLoggedWarnings.TryGetValue(key, out var last) ||
			(DateTime.Now - last).TotalSeconds >= WarningIntervalSeconds;

		private void UpdateLastLoggedTime(string key) =>
			lastLoggedWarnings[key] = DateTime.Now;

		private static UniqueID[] GetCachedUniqueIDs()
		{
			if (uniqueIDCacheDirty || cachedAllUniqueIDs == null)
			{
				uniqueIDCacheDirty = false;
#pragma warning disable CS0618
				cachedAllUniqueIDs = UnityEngine.Object.FindObjectsByType<UniqueID>(
					FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
			}
			return cachedAllUniqueIDs;
		}

		[InitializeOnLoadMethod]
		private static void InitializeOnLoad() =>
			EditorApplication.hierarchyChanged += () =>
			{
				lastLoggedWarnings.Clear();
				uniqueIDCacheDirty = true;
			};
	}
}
#endif
#endif