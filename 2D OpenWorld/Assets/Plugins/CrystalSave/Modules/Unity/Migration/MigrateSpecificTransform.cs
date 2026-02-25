#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateSpecificTransform_ToNewVersion", menuName = "Crystal Save/Create Migration Actions/Legacy/Migrate Specific Transform")]
	public class MigrateSpecificTransform : MigrationAction
	{
		[Header("Target Identification")]
		[Tooltip("Unique Identifier of the GameObject to migrate.")]
		public string targetUniqueID;

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
				Logger.Log("MigrateSpecificTransform: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (string.IsNullOrEmpty(targetUniqueID))
			{
				Logger.Log("MigrateSpecificTransform: targetUniqueID is not set. Migration aborted.", LogLevel.Warning);
				return;
			}

			// Check if ComponentsData contains the targetUniqueID
			if (data.ComponentsData.ContainsKey(targetUniqueID))
			{
				byte[] componentData = data.ComponentsData[targetUniqueID];
				if (componentData == null || componentData.Length == 0)
				{
					Logger.Log($"MigrateSpecificTransform: No data found for UniqueIdentifier '{targetUniqueID}'. Migration aborted.", LogLevel.Warning);
					return;
				}

				// Deserialize using the existing Deserialize<T> method
				TransformData transformData = SaveDataSerializer.Instance.Deserialize<TransformData>(componentData);
				if (transformData != null)
				{
					// Update Position
					if (transformData.Position.HasValue)
					{
						transformData.Position = newPosition;
						Logger.Log($"MigrationAction: Updated Position for UniqueIdentifier '{targetUniqueID}' to {newPosition}.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"MigrationAction: Position for UniqueIdentifier '{targetUniqueID}' was not set. Skipping position update.", LogLevel.Info);
					}

					// Update Rotation
					if (transformData.Rotation.HasValue)
					{
						transformData.Rotation = Quaternion.Euler(newEulerRotation);
						Logger.Log($"MigrationAction: Updated Rotation for UniqueIdentifier '{targetUniqueID}' to {Quaternion.Euler(newEulerRotation)}.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"MigrationAction: Rotation for UniqueIdentifier '{targetUniqueID}' was not set. Skipping rotation update.", LogLevel.Info);
					}

					// Update Scale
					if (transformData.Scale.HasValue)
					{
						transformData.Scale = newScale;
						Logger.Log($"MigrationAction: Updated Scale for UniqueIdentifier '{targetUniqueID}' to {newScale}.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"MigrationAction: Scale for UniqueIdentifier '{targetUniqueID}' was not set. Skipping scale update.", LogLevel.Info);
					}

					// Serialize and save back
					byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(transformData);
					if (updatedComponentData != null)
					{
						data.ComponentsData[targetUniqueID] = updatedComponentData;
						Logger.Log($"MigrationAction: Successfully updated TransformData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"MigrationAction: Failed to serialize updated TransformData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Error);
					}
				}
				else
				{
					Logger.Log($"MigrateSpecificTransform: Failed to deserialize TransformData for UniqueIdentifier '{targetUniqueID}'. Migration aborted.", LogLevel.Warning);
					// Optionally, assign default TransformData or handle accordingly
					TransformData defaultTransformData = new TransformData
					{
						Position = newPosition,
						Rotation = Quaternion.Euler(newEulerRotation),
						Scale = newScale
					};

					byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(defaultTransformData);
					if (updatedComponentData != null)
					{
						data.ComponentsData[targetUniqueID] = updatedComponentData;
						Logger.Log($"MigrateSpecificTransform: Assigned default TransformData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"MigrateSpecificTransform: Failed to serialize default TransformData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Error);
					}
				}
			}
			else
			{
				Logger.Log($"MigrateSpecificTransform: No data found for UniqueIdentifier '{targetUniqueID}'. Migration aborted.", LogLevel.Warning);
			}
		}
	}
}
#endif