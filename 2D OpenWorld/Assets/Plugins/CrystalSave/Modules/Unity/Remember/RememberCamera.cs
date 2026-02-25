#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using UnityEngine;

#if REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Camera")]
	[DisallowMultipleComponent]
	[RememberIcon("Camera Icon")]
	public class RememberCamera : SaveableComponent
	{
                [Header("Camera Properties to Save")]
                [SerializeField]
                private CameraPropertySettings cameraSettings = new CameraPropertySettings();

                [Header("Save Optimization")]
                [SerializeField]
                private bool skipSavingWhenUnchanged;

                private const float VectorComparisonThreshold = 1e-6f;

                private Camera cameraComponent;
                private CameraData cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

#if REMEMBERME_HDRP_PRESENT
		private HDAdditionalCameraData hdCameraData;
#endif

		protected override void Awake()
		{
                        base.Awake();
                        cachedSnapshot = null;
                        hasCachedSnapshot = false;

                        cameraComponent = GetComponent<Camera>();
                        if (cameraComponent == null)
                        {
                                Logger.Log($"RememberCamera: No Camera component found on '{gameObject.name}'. Disabling component.", LogCategory.RememberCamera, LogLevel.Warning);
                                enabled = false;
                                return;
			}

#if REMEMBERME_HDRP_PRESENT
			hdCameraData = GetComponent<HDAdditionalCameraData>();
			if (cameraSettings.SaveHDRPDynamicResolution && hdCameraData == null)
			{
				Logger.Log($"RememberCamera: HDRP Dynamic Resolution is enabled to be saved, but 'HDAdditionalCameraData' component is missing on '{gameObject.name}'. Disabling RememberCamera component.", LogCategory.RememberCamera, LogLevel.Warning);
				enabled = false;
				return;
			}

                        if (cameraSettings.SaveExposureTarget && hdCameraData == null)
                        {
                                Logger.Log($"RememberCamera: Exposure Target saving is enabled, but 'HDAdditionalCameraData' component is missing on '{gameObject.name}'. Disabling RememberCamera component.", LogCategory.RememberCamera, LogLevel.Warning);
                                enabled = false;
                                return;
                        }
#endif

                        if (skipSavingWhenUnchanged)
                        {
                                if (TryCaptureCurrentState(out CameraData snapshot, false))
                                {
                                        cachedSnapshot = snapshot;
                                        hasCachedSnapshot = true;
                                }
                                else
                                {
                                        cachedSnapshot = null;
                                        hasCachedSnapshot = false;
                                }
                        }
                        else
                        {
                                cachedSnapshot = null;
                                hasCachedSnapshot = false;
                        }
                }

		/// <summary>
		/// Serializes the selected Camera properties based on settings.
		/// </summary>
		/// <returns>Serialized byte array of CameraData.</returns>
		protected override byte[] SerializeComponentData()
		{
			if (cameraComponent == null)
			{
				Logger.Log($"SerializeComponentData: No Camera on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberCamera, LogLevel.Warning);
				return null;
			}

                        if (!TryCaptureCurrentState(out CameraData snapshot, true))
                        {
                                return null;
                        }

                        if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(snapshot, cachedSnapshot))
                        {
                                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                {
                                        return cachedSerializedData;
                                }
                        }

                        try
                        {
                                byte[] serializedData = Serializer.Serialize<CameraData>(snapshot);
                                Logger.Log($"RememberCamera: Successfully serialized Camera data for '{gameObject.name}'.", LogCategory.RememberCamera, LogLevel.Info);
                                if (skipSavingWhenUnchanged)
                                {
                                        cachedSnapshot = snapshot;
                                        hasCachedSnapshot = true;
                                        cachedSerializedData = serializedData;
                                }
                                return serializedData;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberCamera: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberCamera, LogLevel.Error);
				return null;
			}
		}

		/// <summary>
		/// Deserializes and applies the saved Camera properties based on settings.
		/// </summary>
		/// <param name="data">Serialized byte array of CameraData.</param>
		protected override void DeserializeComponentData(byte[] data)
		{
			if (cameraComponent == null)
			{
				Logger.Log($"DeserializeComponentData: No Camera on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberCamera, LogLevel.Warning);
				return;
			}

			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberCamera, LogLevel.Warning);
				return;
			}

			try
			{
				CameraData deserializedData = Serializer.Deserialize<CameraData>(data);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberCamera, LogLevel.Warning);
					return;
				}

				// Restore Field of View
				if (cameraSettings.SaveFieldOfView && deserializedData.FieldOfView.HasValue)
				{
					cameraComponent.fieldOfView = deserializedData.FieldOfView.Value;
				}

				// Restore Clipping Planes
				if (cameraSettings.SaveClippingPlanes && deserializedData.ClippingPlanes.HasValue)
				{
					cameraComponent.nearClipPlane = deserializedData.ClippingPlanes.Value.x;
					cameraComponent.farClipPlane = deserializedData.ClippingPlanes.Value.y;
				}

				// Restore Projection Type
				if (cameraSettings.SaveProjection && deserializedData.Projection.HasValue)
				{
					cameraComponent.orthographic = deserializedData.Projection.Value == CameraProjection.Orthographic;
				}

				// Restore Orthographic Size
				if (cameraSettings.SaveOrthographicSize && deserializedData.OrthographicSize.HasValue && cameraComponent.orthographic)
				{
					cameraComponent.orthographicSize = deserializedData.OrthographicSize.Value;
				}

				// Restore Clear Flags
				if (cameraSettings.SaveClearFlags && deserializedData.ClearFlags.HasValue)
				{
					cameraComponent.clearFlags = deserializedData.ClearFlags.Value;
				}

				// Restore Background Color
				if (cameraSettings.SaveBackgroundColor && deserializedData.BackgroundColor.HasValue)
				{
					cameraComponent.backgroundColor = deserializedData.BackgroundColor.Value;
				}

				// Restore Culling Mask
				if (cameraSettings.SaveCullingMask && deserializedData.CullingMask.HasValue)
				{
					cameraComponent.cullingMask = deserializedData.CullingMask.Value;
				}

				// Restore Depth
				if (cameraSettings.SaveDepth && deserializedData.Depth.HasValue)
				{
					cameraComponent.depth = deserializedData.Depth.Value;
				}

				// Restore Aspect Ratio
				if (cameraSettings.SaveAspect && deserializedData.Aspect.HasValue)
				{
					cameraComponent.aspect = deserializedData.Aspect.Value;
				}

				// HDRP-Specific Deserialization
#if REMEMBERME_HDRP_PRESENT
				if (cameraSettings.SaveHDRPDynamicResolution && deserializedData.HDRPDynamicResolutionEnabled.HasValue && hdCameraData != null)
				{
					hdCameraData.allowDynamicResolution = deserializedData.HDRPDynamicResolutionEnabled.Value;
				}

				if (cameraSettings.SaveExposureTarget && !string.IsNullOrEmpty(deserializedData.ExposureTargetUniqueID) && hdCameraData != null)
				{
					GameObject exposureTarget = SaveManager.Instance != null
                                            ? SaveManager.Instance.FindGameObjectByUniqueID(deserializedData.ExposureTargetUniqueID, SaveManager.IdentifierType.UniqueID)
						: null;

					if (exposureTarget != null)
					{
						hdCameraData.exposureTarget = exposureTarget;
						Logger.Log($"RememberCamera: Successfully re-assigned Exposure Target '{exposureTarget.name}' to Camera '{gameObject.name}'.", LogCategory.RememberCamera, LogLevel.Info);
					}
					else
					{
						Logger.Log($"RememberCamera: Exposure Target with UniqueID '{deserializedData.ExposureTargetUniqueID}' not found. Exposure Target will not be restored.", LogCategory.RememberCamera, LogLevel.Warning);
					}
				}
#endif

                                // Add deserialization for more properties as needed

                                if (skipSavingWhenUnchanged)
                                {
                                        if (TryCaptureCurrentState(out CameraData snapshot, false))
                                        {
                                                cachedSnapshot = snapshot;
                                                hasCachedSnapshot = true;
                                        }
                                        else
                                        {
                                                cachedSnapshot = null;
                                                hasCachedSnapshot = false;
                                        }
                                }
                                else
                                {
                                        cachedSnapshot = null;
                                        hasCachedSnapshot = false;
                                }

                                Logger.Log($"RememberCamera: Successfully loaded Camera data for '{gameObject.name}'.", LogCategory.RememberCamera, LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberCamera: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberCamera, LogLevel.Error);
                        }
                }

                protected override void OnEnable()
                {
			base.OnEnable();
                }

                private bool TryCaptureCurrentState(out CameraData snapshot, bool log)
                {
                        snapshot = null;

                        if (cameraComponent == null)
                        {
                                if (log)
                                {
                                        Logger.Log($"SerializeComponentData: No Camera on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberCamera, LogLevel.Warning);
                                }

                                return false;
                        }

                        CameraData cameraData = new CameraData();
                        bool hasData = false;

                        if (cameraSettings.SaveFieldOfView)
                        {
                                cameraData.FieldOfView = cameraComponent.fieldOfView;
                                hasData = true;
                        }

                        if (cameraSettings.SaveClippingPlanes)
                        {
                                cameraData.ClippingPlanes = new Vector2(cameraComponent.nearClipPlane, cameraComponent.farClipPlane);
                                hasData = true;
                        }

                        if (cameraSettings.SaveProjection)
                        {
                                cameraData.Projection = cameraComponent.orthographic ? CameraProjection.Orthographic : CameraProjection.Perspective;
                                hasData = true;
                        }

                        if (cameraSettings.SaveOrthographicSize && cameraComponent.orthographic)
                        {
                                cameraData.OrthographicSize = cameraComponent.orthographicSize;
                                hasData = true;
                        }

                        if (cameraSettings.SaveClearFlags)
                        {
                                cameraData.ClearFlags = cameraComponent.clearFlags;
                                hasData = true;
                        }

                        if (cameraSettings.SaveBackgroundColor)
                        {
                                cameraData.BackgroundColor = cameraComponent.backgroundColor;
                                hasData = true;
                        }

                        if (cameraSettings.SaveCullingMask)
                        {
                                cameraData.CullingMask = cameraComponent.cullingMask;
                                hasData = true;
                        }

                        if (cameraSettings.SaveDepth)
                        {
                                cameraData.Depth = cameraComponent.depth;
                                hasData = true;
                        }

                        if (cameraSettings.SaveAspect)
                        {
                                cameraData.Aspect = cameraComponent.aspect;
                                hasData = true;
                        }

#if REMEMBERME_HDRP_PRESENT
                        if (cameraSettings.SaveHDRPDynamicResolution && hdCameraData != null)
                        {
                                cameraData.HDRPDynamicResolutionEnabled = hdCameraData.allowDynamicResolution;
                                hasData = true;
                        }

                        if (cameraSettings.SaveExposureTarget && hdCameraData != null)
                        {
                                GameObject exposureTarget = hdCameraData.exposureTarget;
                                if (exposureTarget != null)
                                {
                                        UniqueID exposureUniqueID = exposureTarget.GetComponent<UniqueID>();
                                        if (exposureUniqueID != null)
                                        {
                                                cameraData.ExposureTargetUniqueID = exposureUniqueID.ID;
                                                hasData = true;
                                        }
                                        else if (log)
                                        {
                                                Logger.Log($"RememberCamera: Exposure Target '{exposureTarget.name}' does not have a UniqueID component. Exposure Target will not be saved.", LogCategory.RememberCamera, LogLevel.Warning);
                                        }
                                }
                        }
#endif

                        if (!hasData)
                        {
                                snapshot = null;
                                if (log)
                                {
                                        Logger.Log($"RememberCamera: No Camera properties were configured to be saved on '{gameObject.name}'.", LogCategory.RememberCamera, LogLevel.Info);
                                }

                                return false;
                        }

                        snapshot = cameraData;
                        return true;
                }

                private bool AreEquivalent(CameraData current, CameraData cached)
                {
                        if (current == null || cached == null)
                        {
                                return false;
                        }

                        if (!Approximately(current.FieldOfView, cached.FieldOfView)) return false;
                        if (!Approximately(current.ClippingPlanes, cached.ClippingPlanes)) return false;
                        if (!EqualsNullable(current.Projection, cached.Projection)) return false;
                        if (!Approximately(current.OrthographicSize, cached.OrthographicSize)) return false;
                        if (!EqualsNullable(current.ClearFlags, cached.ClearFlags)) return false;
                        if (!Approximately(current.BackgroundColor, cached.BackgroundColor)) return false;
                        if (!EqualsNullable(current.CullingMask, cached.CullingMask)) return false;
                        if (!Approximately(current.Depth, cached.Depth)) return false;
                        if (!Approximately(current.Aspect, cached.Aspect)) return false;

#if REMEMBERME_HDRP_PRESENT
                        if (!EqualsNullable(current.HDRPDynamicResolutionEnabled, cached.HDRPDynamicResolutionEnabled)) return false;
                        if (!string.Equals(current.ExposureTargetUniqueID, cached.ExposureTargetUniqueID, StringComparison.Ordinal)) return false;
#endif

                        return true;
                }

                private static bool Approximately(float? left, float? right)
                {
                        if (!left.HasValue && !right.HasValue) return true;
                        if (!left.HasValue || !right.HasValue) return false;
                        return Mathf.Approximately(left.Value, right.Value);
                }

                private static bool Approximately(Vector2? left, Vector2? right)
                {
                        if (!left.HasValue && !right.HasValue) return true;
                        if (!left.HasValue || !right.HasValue) return false;
                        return (left.Value - right.Value).sqrMagnitude <= VectorComparisonThreshold;
                }

                private static bool Approximately(Color? left, Color? right)
                {
                        if (!left.HasValue && !right.HasValue) return true;
                        if (!left.HasValue || !right.HasValue) return false;

                        Color l = left.Value;
                        Color r = right.Value;

                        return Mathf.Approximately(l.r, r.r)
                                && Mathf.Approximately(l.g, r.g)
                                && Mathf.Approximately(l.b, r.b)
                                && Mathf.Approximately(l.a, r.a);
                }

                private static bool EqualsNullable<T>(T? left, T? right) where T : struct
                {
                        if (!left.HasValue && !right.HasValue) return true;
                        if (!left.HasValue || !right.HasValue) return false;
                        return EqualityComparer<T>.Default.Equals(left.Value, right.Value);
                }

		protected override void OnDisable()
		{
			base.OnDisable();
		}
	}

	[Serializable]
	public class CameraPropertySettings
	{
		[Tooltip("Enable saving the Camera's Field of View (FOV).")]
		public bool SaveFieldOfView = false;

		[Tooltip("Enable saving the Camera's Clipping Planes.")]
		public bool SaveClippingPlanes = false;

		[Tooltip("Enable saving the Camera's Projection Type.")]
		public bool SaveProjection = false;

		[Tooltip("Enable saving the Camera's Orthographic Size.")]
		public bool SaveOrthographicSize = false;

		[Tooltip("Enable saving the Camera's Clear Flags.")]
		public bool SaveClearFlags = false;

		[Tooltip("Enable saving the Camera's Background Color.")]
		public bool SaveBackgroundColor = false;

		[Tooltip("Enable saving the Camera's Culling Mask.")]
		public bool SaveCullingMask = false;

		[Tooltip("Enable saving the Camera's Depth.")]
		public bool SaveDepth = false;

		[Tooltip("Enable saving the Camera's Aspect Ratio.")]
		public bool SaveAspect = false;

		// HDRP-Specific Settings
#if REMEMBERME_HDRP_PRESENT
		[Header("HDRP Settings")]
		[Tooltip("Enable saving HDRP Dynamic Resolution settings.")]
		public bool SaveHDRPDynamicResolution = false;

		[Tooltip("Enable saving HDRP Exposure Target reference.")]
		public bool SaveExposureTarget = false;
#endif
	}
	[MemoryPackable]
	public partial class CameraData
	{
		public float? FieldOfView { get; set; }
		public Vector2? ClippingPlanes { get; set; } // x = Near, y = Far
		public CameraProjection? Projection { get; set; }
		public float? OrthographicSize { get; set; }
		public CameraClearFlags? ClearFlags { get; set; }
		public Color? BackgroundColor { get; set; }
		public int? CullingMask { get; set; }
		public float? Depth { get; set; }
		public float? Aspect { get; set; }

		// HDRP-Specific Data
#if REMEMBERME_HDRP_PRESENT

		[MemoryPackOrder(9)]
		public bool? HDRPDynamicResolutionEnabled { get; set; }

		[MemoryPackOrder(10)]
		public string ExposureTargetUniqueID { get; set; }
#endif

		public CameraData() { }
	}

	public enum CameraProjection
	{
		Perspective,
		Orthographic
	}
}
#endif
