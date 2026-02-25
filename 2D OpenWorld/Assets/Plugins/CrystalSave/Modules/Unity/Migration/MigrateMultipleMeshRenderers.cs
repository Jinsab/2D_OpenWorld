#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleMeshRenderers", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple MeshRenderers")]
	public class MigrateMultipleMeshRenderers : MigrationAction
	{
		[System.Serializable]
		public class MeshRendererMigrationEntry
		{
			public string targetName;

			[Header("Target Identification")]
			[Tooltip("Unique Identifier of the MeshRenderer to migrate.")]
			public string targetUniqueID;

			[Header("Shared Materials Update")]
			[Tooltip("If true, update the shared materials.")]
			public bool updateSharedMaterials = false;
			[Tooltip("New shared material resource paths (relative to Resources).")]
			public string[] newSharedMaterialNames = new string[0];

			[Header("Unique Material Properties Update")]
			[Tooltip("If true, update the unique material properties.")]
			public bool updateUniqueMaterialProperties = false;
			[Tooltip("New unique material data.")]
			public UniqueMaterialData[] newUniqueMaterialProperties = new UniqueMaterialData[0];

			[Header("Other MeshRenderer Properties Update")]
			[Tooltip("If true, update the renderer's enabled state.")]
			public bool updateRendererEnabled = false;
			public bool newRendererEnabled;

			[Tooltip("If true, update the shadow casting mode.")]
			public bool updateShadowCastingMode = false;
			public ShadowCastingMode newShadowCastingMode;

			[Tooltip("If true, update the receive shadows setting.")]
			public bool updateReceiveShadows = false;
			public bool newReceiveShadows;

			[Tooltip("If true, update the light probe usage.")]
			public bool updateLightProbeUsage = false;
			public LightProbeUsage newLightProbeUsage;

			[Tooltip("If true, update the reflection probe usage.")]
			public bool updateReflectionProbeUsage = false;
			public ReflectionProbeUsage newReflectionProbeUsage;

			[Tooltip("If true, update the probe anchor.")]
			public bool updateProbeAnchor = false;
			[Tooltip("Name of the new probe anchor (must exist in the scene).")]
			public string newProbeAnchorName;

			[Tooltip("If true, update the motion vector generation mode.")]
			public bool updateMotionVectorGenerationMode = false;
			public MotionVectorGenerationMode newMotionVectorGenerationMode;

			[Tooltip("If true, update the sorting layer ID.")]
			public bool updateSortingLayerID = false;
			public int newSortingLayerID;

			[Tooltip("If true, update the sorting order.")]
			public bool updateSortingOrder = false;
			public int newSortingOrder;
		}

		[Tooltip("List of MeshRenderer migration entries.")]
		public List<MeshRendererMigrationEntry> migrationEntries = new List<MeshRendererMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleMeshRenderers: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}
			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleMeshRenderers: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleMeshRenderers: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleMeshRenderers: targetUniqueID is not set. Skipping entry.", LogLevel.Warning);
					continue;
				}

				if (!data.ComponentsData.ContainsKey(entry.targetUniqueID))
				{
					Logger.Log($"MigrateMultipleMeshRenderers: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				byte[] compData = data.ComponentsData[entry.targetUniqueID];
				if (compData == null || compData.Length == 0)
				{
					Logger.Log($"MigrateMultipleMeshRenderers: Component data is empty for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				// Deserialize the stored MeshRendererData using SaveDataSerializer.Instance
				MeshRendererData meshData = SaveDataSerializer.Instance.Deserialize<MeshRendererData>(compData);
				if (meshData == null)
				{
					Logger.Log($"MigrateMultipleMeshRenderers: Failed to deserialize MeshRendererData for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				bool dataChanged = false;

				// Update shared material names if flagged.
				if (entry.updateSharedMaterials)
				{
					meshData.SharedMaterialNames = entry.newSharedMaterialNames;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated shared material names for '{entry.targetUniqueID}'.", LogLevel.Info);
				}

				// Update unique material properties if flagged.
				if (entry.updateUniqueMaterialProperties)
				{
					meshData.UniqueMaterialProperties = entry.newUniqueMaterialProperties;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated unique material properties for '{entry.targetUniqueID}'.", LogLevel.Info);
				}

				// Update other MeshRenderer properties.
				if (entry.updateRendererEnabled)
				{
					meshData.RendererEnabled = entry.newRendererEnabled;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated RendererEnabled for '{entry.targetUniqueID}' to {entry.newRendererEnabled}.", LogLevel.Info);
				}
				if (entry.updateShadowCastingMode)
				{
					meshData.ShadowCasting = entry.newShadowCastingMode;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated ShadowCastingMode for '{entry.targetUniqueID}' to {entry.newShadowCastingMode}.", LogLevel.Info);
				}
				if (entry.updateReceiveShadows)
				{
					meshData.ReceiveShadows = entry.newReceiveShadows;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated ReceiveShadows for '{entry.targetUniqueID}' to {entry.newReceiveShadows}.", LogLevel.Info);
				}
				if (entry.updateLightProbeUsage)
				{
					meshData.LightProbeUsage = entry.newLightProbeUsage;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated LightProbeUsage for '{entry.targetUniqueID}' to {entry.newLightProbeUsage}.", LogLevel.Info);
				}
				if (entry.updateReflectionProbeUsage)
				{
					meshData.ReflectionProbeUsage = entry.newReflectionProbeUsage;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated ReflectionProbeUsage for '{entry.targetUniqueID}' to {entry.newReflectionProbeUsage}.", LogLevel.Info);
				}
				if (entry.updateProbeAnchor)
				{
					meshData.ProbeAnchorName = entry.newProbeAnchorName;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated ProbeAnchorName for '{entry.targetUniqueID}' to '{entry.newProbeAnchorName}'.", LogLevel.Info);
				}
				if (entry.updateMotionVectorGenerationMode)
				{
					meshData.MotionVectorMode = entry.newMotionVectorGenerationMode;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated MotionVectorGenerationMode for '{entry.targetUniqueID}' to {entry.newMotionVectorGenerationMode}.", LogLevel.Info);
				}
				if (entry.updateSortingLayerID)
				{
					meshData.SortingLayerID = entry.newSortingLayerID;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated SortingLayerID for '{entry.targetUniqueID}' to {entry.newSortingLayerID}.", LogLevel.Info);
				}
				if (entry.updateSortingOrder)
				{
					meshData.SortingOrder = entry.newSortingOrder;
					dataChanged = true;
					Logger.Log($"MigrateMultipleMeshRenderers: Updated SortingOrder for '{entry.targetUniqueID}' to {entry.newSortingOrder}.", LogLevel.Info);
				}

				if (dataChanged)
				{
					try
					{
						byte[] updatedData = SaveDataSerializer.Instance.Serialize(meshData);
						if (updatedData != null)
						{
							data.ComponentsData[entry.targetUniqueID] = updatedData;
							Logger.Log($"MigrateMultipleMeshRenderers: Successfully updated MeshRendererData for '{entry.targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleMeshRenderers: Serialization returned null for '{entry.targetUniqueID}'.", LogLevel.Error);
						}
					}
					catch (Exception ex)
					{
						Logger.Log($"MigrateMultipleMeshRenderers: Exception during serialization for '{entry.targetUniqueID}': {ex.Message}", LogLevel.Error);
					}
				}
				else
				{
					Logger.Log($"MigrateMultipleMeshRenderers: No changes applied for '{entry.targetUniqueID}'.", LogLevel.Info);
				}
			}
		}
	}
}
#endif