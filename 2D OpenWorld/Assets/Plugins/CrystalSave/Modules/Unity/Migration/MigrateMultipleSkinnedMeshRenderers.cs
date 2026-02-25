#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleSkinnedMeshRenderers", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple SkinnedMeshRenderers")]
	public class MigrateMultipleSkinnedMeshRenderers : MigrationAction
	{
		[System.Serializable]
		public class SkinnedMeshRendererMigrationEntry
		{
			[Header("Target Identification")]
			[Tooltip("Unique Identifier of the SkinnedMeshRenderer to migrate.")]
			public string targetUniqueID;

			public string targetName;

			[Header("Shared Materials Update")]
			[Tooltip("If true, update the shared material resource paths.")]
			public bool updateSharedMaterials = false;
			[Tooltip("New shared material resource paths (relative to Resources).")]
			public string[] newSharedMaterialNames = new string[0];

			[Header("Unique Material Properties Update")]
			[Tooltip("If true, update the unique material properties.")]
			public bool updateUniqueMaterialProperties = false;
			[Tooltip("New unique material data.")]
			public UniqueMaterialData[] newUniqueMaterialProperties = new UniqueMaterialData[0];

			[Header("Blend Shape Weights Update")]
			[Tooltip("If true, update the blend shape weights.")]
			public bool updateBlendShapeWeights = false;
			[Tooltip("New blend shape weights.")]
			public List<float> newBlendShapeWeights = new List<float>();

			[Header("Other SkinnedMeshRenderer Properties Update")]
			[Tooltip("If true, update the enabled state.")]
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

			[Tooltip("If true, update the root bone reference.")]
			public bool updateRootBone = false;
			[Tooltip("Name of the new root bone (must exist in the scene).")]
			public string newRootBoneName;

			[Tooltip("If true, update the skin quality setting.")]
			public bool updateQuality = false;
			public SkinQuality newQuality;

			[Tooltip("If true, update whether the renderer updates when offscreen.")]
			public bool updateUpdateWhenOffscreen = false;
			public bool newUpdateWhenOffscreen;
		}

		[Tooltip("List of SkinnedMeshRenderer migration entries.")]
		public List<SkinnedMeshRendererMigrationEntry> migrationEntries = new List<SkinnedMeshRendererMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleSkinnedMeshRenderers: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}
			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleSkinnedMeshRenderers: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleSkinnedMeshRenderers: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleSkinnedMeshRenderers: targetUniqueID is not set. Skipping entry.", LogLevel.Warning);
					continue;
				}
				if (!data.ComponentsData.ContainsKey(entry.targetUniqueID))
				{
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				byte[] compData = data.ComponentsData[entry.targetUniqueID];
				if (compData == null || compData.Length == 0)
				{
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Component data is empty for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				// Deserialize the stored SkinnedMeshRendererData.
				SkinnedMeshRendererData meshData = SaveDataSerializer.Instance.Deserialize<SkinnedMeshRendererData>(compData);
				if (meshData == null)
				{
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Failed to deserialize SkinnedMeshRendererData for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				bool dataChanged = false;

				if (entry.updateSharedMaterials)
				{
					meshData.SharedMaterialNames = entry.newSharedMaterialNames;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated shared material names for '{entry.targetUniqueID}'.", LogLevel.Info);
				}

				if (entry.updateUniqueMaterialProperties)
				{
					meshData.UniqueMaterialProperties = entry.newUniqueMaterialProperties;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated unique material properties for '{entry.targetUniqueID}'.", LogLevel.Info);
				}

				if (entry.updateBlendShapeWeights)
				{
					meshData.BlendShapeWeights = new List<float>(entry.newBlendShapeWeights);
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated blend shape weights for '{entry.targetUniqueID}'.", LogLevel.Info);
				}

				if (entry.updateRendererEnabled)
				{
					meshData.RendererEnabled = entry.newRendererEnabled;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated RendererEnabled for '{entry.targetUniqueID}' to {entry.newRendererEnabled}.", LogLevel.Info);
				}
				if (entry.updateShadowCastingMode)
				{
					meshData.ShadowCasting = entry.newShadowCastingMode;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated ShadowCastingMode for '{entry.targetUniqueID}' to {entry.newShadowCastingMode}.", LogLevel.Info);
				}
				if (entry.updateReceiveShadows)
				{
					meshData.ReceiveShadows = entry.newReceiveShadows;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated ReceiveShadows for '{entry.targetUniqueID}' to {entry.newReceiveShadows}.", LogLevel.Info);
				}
				if (entry.updateLightProbeUsage)
				{
					meshData.LightProbeUsage = entry.newLightProbeUsage;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated LightProbeUsage for '{entry.targetUniqueID}' to {entry.newLightProbeUsage}.", LogLevel.Info);
				}
				if (entry.updateReflectionProbeUsage)
				{
					meshData.ReflectionProbeUsage = entry.newReflectionProbeUsage;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated ReflectionProbeUsage for '{entry.targetUniqueID}' to {entry.newReflectionProbeUsage}.", LogLevel.Info);
				}
				if (entry.updateProbeAnchor)
				{
					meshData.ProbeAnchorName = entry.newProbeAnchorName;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated ProbeAnchorName for '{entry.targetUniqueID}' to '{entry.newProbeAnchorName}'.", LogLevel.Info);
				}
				if (entry.updateMotionVectorGenerationMode)
				{
					meshData.MotionVectorMode = entry.newMotionVectorGenerationMode;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated MotionVectorGenerationMode for '{entry.targetUniqueID}' to {entry.newMotionVectorGenerationMode}.", LogLevel.Info);
				}
				if (entry.updateSortingLayerID)
				{
					meshData.SortingLayerID = entry.newSortingLayerID;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated SortingLayerID for '{entry.targetUniqueID}' to {entry.newSortingLayerID}.", LogLevel.Info);
				}
				if (entry.updateSortingOrder)
				{
					meshData.SortingOrder = entry.newSortingOrder;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated SortingOrder for '{entry.targetUniqueID}' to {entry.newSortingOrder}.", LogLevel.Info);
				}
				if (entry.updateRootBone)
				{
					meshData.RootBoneName = entry.newRootBoneName;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated RootBoneName for '{entry.targetUniqueID}' to '{entry.newRootBoneName}'.", LogLevel.Info);
				}
				if (entry.updateQuality)
				{
					meshData.Quality = entry.newQuality;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated Quality for '{entry.targetUniqueID}' to {entry.newQuality}.", LogLevel.Info);
				}
				if (entry.updateUpdateWhenOffscreen)
				{
					meshData.UpdateWhenOffscreen = entry.newUpdateWhenOffscreen;
					dataChanged = true;
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Updated UpdateWhenOffscreen for '{entry.targetUniqueID}' to {entry.newUpdateWhenOffscreen}.", LogLevel.Info);
				}

				if (dataChanged)
				{
					try
					{
						byte[] updatedData = SaveDataSerializer.Instance.Serialize(meshData);
						if (updatedData != null)
						{
							data.ComponentsData[entry.targetUniqueID] = updatedData;
							Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Successfully updated SkinnedMeshRendererData for '{entry.targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Serialization returned null for '{entry.targetUniqueID}'.", LogLevel.Error);
						}
					}
					catch (Exception ex)
					{
						Logger.Log($"MigrateMultipleSkinnedMeshRenderers: Exception during serialization for '{entry.targetUniqueID}': {ex.Message}", LogLevel.Error);
					}
				}
				else
				{
					Logger.Log($"MigrateMultipleSkinnedMeshRenderers: No changes applied for '{entry.targetUniqueID}'.", LogLevel.Info);
				}
			}
		}
	}
}
#endif