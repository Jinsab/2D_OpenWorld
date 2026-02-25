#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;
using MemoryPack;
using UnityEngine.Rendering;

#if REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

#if REMEMBERME_URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace Arawn.CrystalSave.Runtime
{
	public enum MigrationLightType
	{
		Directional,
		Point,
		Spot,
		Area
	}

	[CreateAssetMenu(fileName = "MigrateMultipleLights", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple Lights")]
	public class MigrateMultipleLights : MigrationAction
	{
		[System.Serializable]
		public class LightMigrationEntry
		{
			public string targetName;

			[Header("Target Identification")]
			[Tooltip("Unique Identifier of the Light component to migrate.")]
			public string targetUniqueID;

			[Header("Light Type")]
			[Tooltip("The type of Light this entry applies to.")]
			public MigrationLightType lightType;

			[Header("Common Light Properties")]
			[Tooltip("New enabled state.")]
			public bool newEnabled = true;

			[Tooltip("New light color.")]
			public Color newColor = Color.white;

			[Tooltip("New light intensity.")]
			public float newIntensity = 1f;

			[Tooltip("New shadow enabled state.")]
			public bool newShadowsEnabled = true;

			[Header("Additional Properties (if applicable)")]
			[Tooltip("New range for Point, Spot and Area lights.")]
			public float newRange = 10f;

			[Tooltip("New spot angle for Spot lights.")]
			public float newSpotAngle = 30f;

			[Tooltip("New size for Area lights (HDRP only).")]
			public Vector3 newSize = Vector3.one;
		}

		[Tooltip("List of light migration entries for all Lights that need updating.")]
		public List<LightMigrationEntry> migrationEntries = new List<LightMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleLights: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleLights: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleLights: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleLights: targetUniqueID is not set for one of the entries. Skipping entry.", LogLevel.Warning);
					continue;
				}

				if (!data.ComponentsData.ContainsKey(entry.targetUniqueID))
				{
					Logger.Log($"MigrateMultipleLights: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				byte[] componentData = data.ComponentsData[entry.targetUniqueID];
				if (componentData == null || componentData.Length == 0)
				{
					Logger.Log($"MigrateMultipleLights: Component data is empty for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				bool dataChanged = false;
				object updatedLightData = null;

				switch (entry.lightType)
				{
					case MigrationLightType.Directional:
						{
							var lightData = SaveDataSerializer.Instance.Deserialize<DirectionalLightData>(componentData);
							if (lightData != null)
							{
								lightData.Enabled = entry.newEnabled;
								lightData.Color = entry.newColor;
								lightData.Intensity = entry.newIntensity;
								lightData.ShadowsEnabled = entry.newShadowsEnabled;
								lightData.Range = entry.newRange;
								lightData.Size = entry.newSize;
								updatedLightData = lightData;
								dataChanged = true;
								Logger.Log($"MigrateMultipleLights: Updated Directional Light '{entry.targetUniqueID}'.", LogLevel.Info);
							}
							else
							{
								Logger.Log($"MigrateMultipleLights: Failed to deserialize DirectionalLightData for '{entry.targetUniqueID}'. Skipping.", LogLevel.Warning);
							}
						}
						break;

					case MigrationLightType.Point:
						{
							var lightData = SaveDataSerializer.Instance.Deserialize<PointLightData>(componentData);
							if (lightData != null)
							{
								lightData.Enabled = entry.newEnabled;
								lightData.Color = entry.newColor;
								lightData.Intensity = entry.newIntensity;
								lightData.ShadowsEnabled = entry.newShadowsEnabled;
								lightData.Range = entry.newRange;
								updatedLightData = lightData;
								dataChanged = true;
								Logger.Log($"MigrateMultipleLights: Updated Point Light '{entry.targetUniqueID}'.", LogLevel.Info);
							}
							else
							{
								Logger.Log($"MigrateMultipleLights: Failed to deserialize PointLightData for '{entry.targetUniqueID}'. Skipping.", LogLevel.Warning);
							}
						}
						break;

					case MigrationLightType.Spot:
						{
							var lightData = SaveDataSerializer.Instance.Deserialize<SpotLightData>(componentData);
							if (lightData != null)
							{
								lightData.Enabled = entry.newEnabled;
								lightData.Color = entry.newColor;
								lightData.Intensity = entry.newIntensity;
								lightData.ShadowsEnabled = entry.newShadowsEnabled;
								lightData.Range = entry.newRange;
								lightData.SpotAngle = entry.newSpotAngle;
								updatedLightData = lightData;
								dataChanged = true;
								Logger.Log($"MigrateMultipleLights: Updated Spot Light '{entry.targetUniqueID}'.", LogLevel.Info);
							}
							else
							{
								Logger.Log($"MigrateMultipleLights: Failed to deserialize SpotLightData for '{entry.targetUniqueID}'. Skipping.", LogLevel.Warning);
							}
						}
						break;

					case MigrationLightType.Area:
						{
							var lightData = SaveDataSerializer.Instance.Deserialize<AreaLightData>(componentData);
							if (lightData != null)
							{
								lightData.Enabled = entry.newEnabled;
								lightData.Color = entry.newColor;
								lightData.Intensity = entry.newIntensity;
								lightData.ShadowsEnabled = entry.newShadowsEnabled;
								lightData.Range = entry.newRange;
#if REMEMBERME_HDRP_PRESENT
                                lightData.Size = entry.newSize;
                                Logger.Log($"MigrateMultipleLights: Updated Area Light '{entry.targetUniqueID}' (HDRP).", LogLevel.Info);
#elif REMEMBERME_STANDARD_PRESENT
                                Logger.Log($"MigrateMultipleLights: Size update is not applicable for Area Lights in the Standard Render Pipeline for '{entry.targetUniqueID}'.", LogLevel.Info);
#elif REMEMBERME_URP_PRESENT
								Logger.Log($"MigrateMultipleLights: Area Lights are not fully supported in URP; size update skipped for '{entry.targetUniqueID}'.", LogLevel.Info);
#else
                                lightData.Size = entry.newSize;
                                Logger.Log($"MigrateMultipleLights: Updated Area Light '{entry.targetUniqueID}' (fallback).", LogLevel.Info);
#endif
								updatedLightData = lightData;
								dataChanged = true;
							}
							else
							{
								Logger.Log($"MigrateMultipleLights: Failed to deserialize AreaLightData for '{entry.targetUniqueID}'. Skipping.", LogLevel.Warning);
							}
						}
						break;

					default:
						Logger.Log($"MigrateMultipleLights: Unknown light type for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
						break;
				}

				if (dataChanged && updatedLightData != null)
				{
					byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(updatedLightData);
					if (updatedComponentData != null)
					{
						data.ComponentsData[entry.targetUniqueID] = updatedComponentData;
						Logger.Log($"MigrateMultipleLights: Successfully updated LightData for '{entry.targetUniqueID}'.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"MigrateMultipleLights: Failed to serialize updated LightData for '{entry.targetUniqueID}'.", LogLevel.Error);
					}
				}
				else
				{
					Logger.Log($"MigrateMultipleLights: No changes applied for '{entry.targetUniqueID}'.", LogLevel.Info);
				}
			}
		}
	}
}
#endif