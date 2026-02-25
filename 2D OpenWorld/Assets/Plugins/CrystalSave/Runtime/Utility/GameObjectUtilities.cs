#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public static class GameObjectUtilities
	{
		public static string GetUniqueID(GameObject go)
		{
			if (go == null)
			{
				Logger.Log("GetUniqueID: Provided GameObject is null.", LogLevel.Info);
				return null;
			}

			// 1. Check for UniqueID component
			UniqueID uidComponent = go.GetComponent<UniqueID>();
			if (uidComponent != null && !string.IsNullOrEmpty(uidComponent.ID))
			{
				return uidComponent.ID;
			}

			// 2. Check for SceneObjectID component
			SceneObjectID sceneObjectID = go.GetComponent<SceneObjectID>();
			if (sceneObjectID != null && !string.IsNullOrEmpty(sceneObjectID.UniqueID))
			{
				return sceneObjectID.UniqueID;
			}

			// 3. Check for SaveablePrefab component
			SaveablePrefab saveablePrefab = go.GetComponent<SaveablePrefab>();
			if (saveablePrefab != null && !string.IsNullOrEmpty(saveablePrefab.UniqueID))
			{
				return saveablePrefab.UniqueID;
			}

			// 4. Fallback: Use GameObject's name as uniqueID
			string uniqueID = go.name;
			if (!string.IsNullOrEmpty(uniqueID))
			{
				Logger.Log($"GetUniqueID: Using GameObject name '{uniqueID}' as uniqueID.");
				return uniqueID;
			}

			// 5. Unable to determine uniqueID
			Logger.Log($"GetUniqueID: Unable to determine uniqueID for GameObject '{go.name}'.", LogLevel.Warning);
			return null;
		}

		/*
		public static string GetResourcePath(Object obj)
		{
			if (obj == null)
			{
				Logger.Log("GetResourcePath: Provided object is null.", LogLevel.Info);
				return null;
			}

#if UNITY_EDITOR
			// Check if the object resides in the Resources folder
			string path = UnityEditor.AssetDatabase.GetAssetPath(obj);
			if (!string.IsNullOrEmpty(path) && path.Contains("Resources"))
			{
				int startIndex = path.IndexOf("Resources/") + 10;
				int endIndex = path.LastIndexOf('.');
				string resourcePath = path.Substring(startIndex, endIndex - startIndex);

				Logger.Log($"GetResourcePath: Found path '{resourcePath}' for object '{obj.name}'.", LogLevel.Info);
				return resourcePath;
			}

			Logger.Log($"GetResourcePath: Object '{obj.name}' is not in the Resources folder.", LogLevel.Warning);
			return null;
#endif
		}
		*/
	}

}
#endif