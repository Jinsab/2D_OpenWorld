#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleTransforms", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple Transforms")]
	public class MigrateMultipleTransforms : MigrationAction
	{
		// Serializable class representing each migration entry.
		[System.Serializable]
		public class TransformMigrationEntry
		{
			[Header("Target Identification")]
			[Tooltip("Unique Identifier of the GameObject to migrate.")]
			public string targetUniqueID;

			[Tooltip("Human-friendly name (auto-populated).")]
			public string targetName;

			[Header("New Transform Values")]
			[Tooltip("New position to set.")]
			public Vector3 newPosition = Vector3.zero;

			[Tooltip("New rotation to set (Euler angles).")]
			public Vector3 newEulerRotation = Vector3.zero;

			[Tooltip("New scale to set.")]
			public Vector3 newScale = Vector3.one;
		}

		[Tooltip("List of transform migration entries for all GameObjects that need updating.")]
		public List<TransformMigrationEntry> migrationEntries = new List<TransformMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleTransforms: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleTransforms: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			// Iterate over each entry in the list and apply migration.
			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleTransforms: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}

				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleTransforms: targetUniqueID is not set for one of the entries. Skipping entry.", LogLevel.Warning);
					continue;
				}

				// Check if the SaveData contains data for the specified unique ID.
				if (data.ComponentsData.ContainsKey(entry.targetUniqueID))
				{
					byte[] componentData = data.ComponentsData[entry.targetUniqueID];
					if (componentData == null || componentData.Length == 0)
					{
						Logger.Log($"MigrateMultipleTransforms: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
						continue;
					}

					// Deserialize the existing TransformData.
					TransformData transformData = SaveDataSerializer.Instance.Deserialize<TransformData>(componentData);
					if (transformData != null)
					{
						// Update the transform values if they are set.
						if (transformData.Position.HasValue)
						{
							transformData.Position = entry.newPosition;
							Logger.Log($"MigrateMultipleTransforms: Updated Position for UniqueIdentifier '{entry.targetUniqueID}' to {entry.newPosition}.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleTransforms: Position for UniqueIdentifier '{entry.targetUniqueID}' was not set. Skipping position update.", LogLevel.Info);
						}

						if (transformData.Rotation.HasValue)
						{
							transformData.Rotation = Quaternion.Euler(entry.newEulerRotation);
							Logger.Log($"MigrateMultipleTransforms: Updated Rotation for UniqueIdentifier '{entry.targetUniqueID}' to {Quaternion.Euler(entry.newEulerRotation)}.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleTransforms: Rotation for UniqueIdentifier '{entry.targetUniqueID}' was not set. Skipping rotation update.", LogLevel.Info);
						}

						if (transformData.Scale.HasValue)
						{
							transformData.Scale = entry.newScale;
							Logger.Log($"MigrateMultipleTransforms: Updated Scale for UniqueIdentifier '{entry.targetUniqueID}' to {entry.newScale}.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleTransforms: Scale for UniqueIdentifier '{entry.targetUniqueID}' was not set. Skipping scale update.", LogLevel.Info);
						}

						// Serialize and save the updated TransformData.
						byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(transformData);
						if (updatedComponentData != null)
						{
							data.ComponentsData[entry.targetUniqueID] = updatedComponentData;
							Logger.Log($"MigrateMultipleTransforms: Successfully updated TransformData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleTransforms: Failed to serialize updated TransformData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Error);
						}
					}
					else
					{
						Logger.Log($"MigrateMultipleTransforms: Failed to deserialize TransformData for UniqueIdentifier '{entry.targetUniqueID}'. Attempting default assignment.", LogLevel.Warning);
						// Create default TransformData if deserialization fails.
						TransformData defaultTransformData = new TransformData
						{
							Position = entry.newPosition,
							Rotation = Quaternion.Euler(entry.newEulerRotation),
							Scale = entry.newScale
						};

						byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(defaultTransformData);
						if (updatedComponentData != null)
						{
							data.ComponentsData[entry.targetUniqueID] = updatedComponentData;
							Logger.Log($"MigrateMultipleTransforms: Assigned default TransformData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleTransforms: Failed to serialize default TransformData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Error);
						}
					}
				}
				else
				{
					Logger.Log($"MigrateMultipleTransforms: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
				}
			}
		}
	}
}
#endif