using UnityEditor;
using UnityEngine;
using System.IO;

namespace Arawn.CrystalSave.Editor
{
	public static class RememberMeInstallMenu
	{
		[MenuItem("Tools/Crystal Save/Settings/Install Demo Settings")]
		public static void InstallDefaultSettings()
		{
			// 1. Check if any of the specified ScriptableObjects exist in a Resources folder
			bool migrationManagerExists = Resources.Load("MigrationManager") != null;
			bool prefabRegistryExists = Resources.Load("PrefabRegistry") != null;
			bool saveSettingsExists = Resources.Load("SaveSettings") != null;
			bool tagRegistryExists = Resources.Load("TagRegistry") != null;

			bool anyExist = migrationManagerExists || prefabRegistryExists
							|| saveSettingsExists || tagRegistryExists;

			// 2. If any exist, show a popup warning that this will override settings
			if (anyExist)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"Override Existing Settings?",
					"Warning: Importing demo settings will overwrite your existing settings. " +
					"Do you want to continue?",
					"Yes",
					"No"
				);

				// If user clicks "No", stop here
				if (!proceed)
					return;
			}

			// 3. If user confirms (or no existing SOs were found), import the package
			string packagePath = "Assets/Plugins/CrystalSave/Packages/CrystalSaveSettings.unitypackage";
			if (File.Exists(packagePath))
			{
				AssetDatabase.ImportPackage(packagePath, /* interactive: */ true);
			}
			else
			{
				Debug.LogError("CrystalSaveSettings.unitypackage not found at path: " + packagePath);
			}
		}
	}

}
