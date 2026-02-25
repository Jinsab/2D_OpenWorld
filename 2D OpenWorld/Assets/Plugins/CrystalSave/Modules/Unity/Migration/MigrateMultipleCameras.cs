#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
using MemoryPack;

#if REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleCameras", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple Cameras")]
	public class MigrateMultipleCameras : MigrationAction
	{
		[System.Serializable]
		public class CameraMigrationEntry
		{
			public string targetName;

			[Tooltip("Unique Identifier of the Camera to migrate.")]
			public string targetUniqueID;

			[Tooltip("If true, update the Camera's Field of View.")]
			public bool updateFieldOfView = false;
			public float newFieldOfView;

			[Tooltip("If true, update the Camera's Clipping Planes (x = near, y = far).")]
			public bool updateClippingPlanes = false;
			public Vector2 newClippingPlanes;

			[Tooltip("If true, update the Camera's Projection type.")]
			public bool updateProjection = false;
			public CameraProjection newProjection;

			[Tooltip("If true, update the Camera's Orthographic Size (if applicable).")]
			public bool updateOrthographicSize = false;
			public float newOrthographicSize;

			[Tooltip("If true, update the Camera's Clear Flags.")]
			public bool updateClearFlags = false;
			public CameraClearFlags newClearFlags;

			[Tooltip("If true, update the Camera's Background Color.")]
			public bool updateBackgroundColor = false;
			public Color newBackgroundColor;

			[Tooltip("If true, update the Camera's Culling Mask.")]
			public bool updateCullingMask = false;
			public int newCullingMask;

			[Tooltip("If true, update the Camera's Depth.")]
			public bool updateDepth = false;
			public float newDepth;

			[Tooltip("If true, update the Camera's Aspect Ratio.")]
			public bool updateAspect = false;
			public float newAspect;

#if REMEMBERME_HDRP_PRESENT
            [Header("HDRP-Specific Updates")]
            [Tooltip("If true, update the HDRP Dynamic Resolution setting.")]
            public bool updateHDRPDynamicResolutionEnabled = false;
            public bool newHDRPDynamicResolutionEnabled;

            [Tooltip("If true, update the Exposure Target's Unique ID.")]
            public bool updateExposureTargetUniqueID = false;
            public string newExposureTargetUniqueID;
#endif
		}

		[Tooltip("List of Camera migration entries.")]
		public List<CameraMigrationEntry> migrationEntries = new List<CameraMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleCameras: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}
			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleCameras: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleCameras: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleCameras: targetUniqueID is not set. Skipping entry.", LogLevel.Warning);
					continue;
				}
				if (!data.ComponentsData.ContainsKey(entry.targetUniqueID))
				{
					Logger.Log($"MigrateMultipleCameras: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				byte[] compData = data.ComponentsData[entry.targetUniqueID];
				if (compData == null || compData.Length == 0)
				{
					Logger.Log($"MigrateMultipleCameras: Component data is empty for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				// Deserialize the stored CameraData.
				CameraData cameraData = SaveDataSerializer.Instance.Deserialize<CameraData>(compData);
				if (cameraData == null)
				{
					Logger.Log($"MigrateMultipleCameras: Failed to deserialize CameraData for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				bool dataChanged = false;

				if (entry.updateFieldOfView)
				{
					cameraData.FieldOfView = entry.newFieldOfView;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated FieldOfView for '{entry.targetUniqueID}' to {entry.newFieldOfView}.", LogLevel.Info);
				}
				if (entry.updateClippingPlanes)
				{
					cameraData.ClippingPlanes = entry.newClippingPlanes;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated ClippingPlanes for '{entry.targetUniqueID}' to {entry.newClippingPlanes}.", LogLevel.Info);
				}
				if (entry.updateProjection)
				{
					cameraData.Projection = entry.newProjection;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated Projection for '{entry.targetUniqueID}' to {entry.newProjection}.", LogLevel.Info);
				}
				if (entry.updateOrthographicSize)
				{
					cameraData.OrthographicSize = entry.newOrthographicSize;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated OrthographicSize for '{entry.targetUniqueID}' to {entry.newOrthographicSize}.", LogLevel.Info);
				}
				if (entry.updateClearFlags)
				{
					cameraData.ClearFlags = entry.newClearFlags;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated ClearFlags for '{entry.targetUniqueID}' to {entry.newClearFlags}.", LogLevel.Info);
				}
				if (entry.updateBackgroundColor)
				{
					cameraData.BackgroundColor = entry.newBackgroundColor;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated BackgroundColor for '{entry.targetUniqueID}' to {entry.newBackgroundColor}.", LogLevel.Info);
				}
				if (entry.updateCullingMask)
				{
					cameraData.CullingMask = entry.newCullingMask;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated CullingMask for '{entry.targetUniqueID}' to {entry.newCullingMask}.", LogLevel.Info);
				}
				if (entry.updateDepth)
				{
					cameraData.Depth = entry.newDepth;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated Depth for '{entry.targetUniqueID}' to {entry.newDepth}.", LogLevel.Info);
				}
				if (entry.updateAspect)
				{
					cameraData.Aspect = entry.newAspect;
					dataChanged = true;
					Logger.Log($"MigrateMultipleCameras: Updated Aspect for '{entry.targetUniqueID}' to {entry.newAspect}.", LogLevel.Info);
				}
#if REMEMBERME_HDRP_PRESENT
                if (entry.updateHDRPDynamicResolutionEnabled)
                {
                    cameraData.HDRPDynamicResolutionEnabled = entry.newHDRPDynamicResolutionEnabled;
                    dataChanged = true;
                    Logger.Log($"MigrateMultipleCameras: Updated HDRPDynamicResolutionEnabled for '{entry.targetUniqueID}' to {entry.newHDRPDynamicResolutionEnabled}.", LogLevel.Info);
                }
                if (entry.updateExposureTargetUniqueID)
                {
                    cameraData.ExposureTargetUniqueID = entry.newExposureTargetUniqueID;
                    dataChanged = true;
                    Logger.Log($"MigrateMultipleCameras: Updated ExposureTargetUniqueID for '{entry.targetUniqueID}' to '{entry.newExposureTargetUniqueID}'.", LogLevel.Info);
                }
#endif

				if (dataChanged)
				{
					try
					{
						byte[] updatedData = SaveDataSerializer.Instance.Serialize(cameraData);
						if (updatedData != null)
						{
							data.ComponentsData[entry.targetUniqueID] = updatedData;
							Logger.Log($"MigrateMultipleCameras: Successfully updated CameraData for '{entry.targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleCameras: Serialization returned null for '{entry.targetUniqueID}'.", LogLevel.Error);
						}
					}
					catch (Exception ex)
					{
						Logger.Log($"MigrateMultipleCameras: Exception during serialization for '{entry.targetUniqueID}': {ex.Message}", LogLevel.Error);
					}
				}
				else
				{
					Logger.Log($"MigrateMultipleCameras: No changes applied for '{entry.targetUniqueID}'.", LogLevel.Info);
				}
			}
		}
	}
}
#endif