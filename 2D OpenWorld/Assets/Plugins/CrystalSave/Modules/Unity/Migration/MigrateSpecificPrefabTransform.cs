#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateSpecificPrefabTransform_ToNewVersion", menuName = "Crystal Save/Create Migration Actions/Legacy/Migrate Specific Prefab Transform")]
	public class MigrateSpecificPrefabTransform : MigrationAction
	{
		[Header("Target Identification")]
		[Tooltip("Prefab Asset ID of the SaveablePrefab to migrate.")]
		public string targetPrefabAssetID;

		[Header("New Transform Values")]
		[Tooltip("New position to set.")]
		public Vector3 newPosition = Vector3.zero;

		[Tooltip("New rotation to set (Euler angles).")]
		public Vector3 newEulerRotation = Vector3.zero;

		[Tooltip("New scale to set.")]
		public Vector3 newScale = Vector3.one;

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateSpecificPrefabTransform: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (string.IsNullOrEmpty(targetPrefabAssetID))
			{
				Logger.Log("MigrateSpecificPrefabTransform: targetPrefabAssetID is not set. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (data.Prefabs == null || data.Prefabs.Count == 0)
			{
				Logger.Log("MigrateSpecificPrefabTransform: No SaveablePrefab data available. Migration aborted.", LogLevel.Warning);
				return;
			}

			bool found = false;
			foreach (var prefabData in data.Prefabs)
			{
				if (prefabData != null && prefabData.PrefabID == targetPrefabAssetID)
				{
					prefabData.Position = newPosition;
					prefabData.Rotation = Quaternion.Euler(newEulerRotation);
					prefabData.Scale = newScale;
					Logger.Log($"MigrateSpecificPrefabTransform: Updated transform for PrefabID '{targetPrefabAssetID}'.", LogLevel.Info);
					found = true;
				}
			}

			if (!found)
			{
				Logger.Log($"MigrateSpecificPrefabTransform: No SaveablePrefabData found with PrefabID '{targetPrefabAssetID}'.", LogLevel.Warning);
			}
		}
	}
}
#endif
