#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.Rendering;

#if REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

#if REMEMBERME_URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Light")]
	[DisallowMultipleComponent]
	[RememberIcon("Light Icon")]
	public class RememberLight : SaveableComponent
	{
                private RenderPipelineAsset activeRenderPipeline;
                private Light targetLight;

                [Header("Save Optimization")]
                [Tooltip("Skip serialization when the captured state did not change since the last save.")]
                [SerializeField]
                private bool skipSavingWhenUnchanged;

                private LightSnapshot cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

                private const float FloatTolerance = 0.0001f;
                private const float ColorTolerance = 0.001f;

                [Header("Common Light Toggles")]
                [Tooltip("Serialize whether the Light component is enabled.")]
                [SerializeField]
                private bool rememberEnabled = true;

		[Tooltip("Serialize the Light's color.")]
		[SerializeField]
		private bool rememberColor = true;

		[Tooltip("Serialize the Light's intensity.")]
		[SerializeField]
		private bool rememberIntensity = true;

		[Tooltip("Serialize whether the Light casts shadows.")]
		[SerializeField]
		private bool rememberShadowsEnabled = true;

		[Header("Point Light Toggles")]
		[Tooltip("Serialize the Point Light's range.")]
		[SerializeField]
		private bool rememberPointLightRange = true;

		[Header("Spot Light Toggles")]
		[Tooltip("Serialize the Spot Light's range.")]
		[SerializeField]
		private bool rememberSpotLightRange = true;

		[Tooltip("Serialize the Spot Light's spot angle.")]
		[SerializeField]
		private bool rememberSpotLightAngle = true;

		[Header("Area Light Toggles")]
		[Tooltip("Serialize the Area Light's range.")]
		[SerializeField]
		private bool rememberAreaLightRange = true;

                [Tooltip("Serialize the Area Light's size.")]
                [SerializeField]
                private bool rememberAreaLightSize = true;

                private struct LightSnapshot
                {
                        public LightType Type;
                        public bool UsesHdrpPath;

                        public bool HasEnabled;
                        public bool Enabled;

                        public bool HasColor;
                        public Color Color;

                        public bool HasIntensity;
                        public float Intensity;

                        public bool HasShadowsEnabled;
                        public bool ShadowsEnabled;

                        public bool HasRange;
                        public float Range;

                        public bool HasSpotAngle;
                        public float SpotAngle;

                        public bool HasSize;
                        public Vector3 Size;
                }

                protected override void Awake()
                {
                        base.Awake();
                        targetLight = GetComponent<Light>();

			if (targetLight == null)
			{
				Logger.Log($"{nameof(RememberLight)} requires a Light component on the same GameObject. None was found on '{gameObject.name}'.", LogCategory.RememberLight, LogLevel.Error);
				return;
			}

                        activeRenderPipeline = GraphicsSettings.currentRenderPipeline;

#if REMEMBERME_HDRP_PRESENT
                        if (activeRenderPipeline is HDRenderPipelineAsset)
			{
				Logger.Log($"RememberLight: HDRP detected for '{gameObject.name}'.", LogCategory.RememberLight, LogLevel.Info);
			}
			else
#endif
#if REMEMBERME_URP_PRESENT
            if (activeRenderPipeline is UniversalRenderPipelineAsset)
            {
                Logger.Log($"RememberLight: URP detected for '{gameObject.name}'.", LogCategory.RememberLight, LogLevel.Info);
            }
            else
#endif
                        {
                                Logger.Log($"RememberLight: Standard Render Pipeline detected for '{gameObject.name}'. Component will serialize only standard light properties.", LogCategory.RememberLight, LogLevel.Info);
                        }

                        if (!skipSavingWhenUnchanged)
                        {
                                hasCachedSnapshot = false;
                        }
                        else if (TryCaptureCurrentState(out LightSnapshot snapshot, false))
                        {
                                cachedSnapshot = snapshot;
                                hasCachedSnapshot = true;
                        }
                        else
                        {
                                hasCachedSnapshot = false;
                        }
                }

                protected override byte[] SerializeComponentData()
                {
                        try
                        {
                                if (!TryCaptureCurrentState(out LightSnapshot snapshot, true))
                                {
                                        return null;
                                }

                                if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
                                {
                                        if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                        {
                                                Logger.Log($"RememberLight: Returning cached serialized data for '{gameObject.name}' (unchanged).", LogCategory.RememberLight, LogLevel.Off);
                                                return cachedSerializedData;
                                        }
                                        
                                        Logger.Log($"RememberLight: Data unchanged but no cached serialized data for '{gameObject.name}' - will serialize fresh.", LogCategory.RememberLight, LogLevel.Off);
                                }

                                byte[] serializedData = SerializeSnapshot(snapshot);
                                if (serializedData != null)
                                {
                                        Logger.Log($"RememberLight: Successfully serialized {snapshot.Type} Light data for '{gameObject.name}'.", LogCategory.RememberLight, LogLevel.Info);

                                        if (skipSavingWhenUnchanged)
                                        {
                                                cachedSnapshot = snapshot;
                                                hasCachedSnapshot = true;
                                                cachedSerializedData = serializedData;
                                        }
                                }

                                return serializedData;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberLight: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberLight, LogLevel.Error);
                                return null;
                        }
                }

                private bool TryCaptureCurrentState(out LightSnapshot snapshot, bool logWarnings)
                {
                        snapshot = default;

                        if (targetLight == null)
                        {
                                if (logWarnings)
                                {
                                        Logger.Log("SerializeComponentData failed: Light component not found.", LogCategory.RememberLight, LogLevel.Warning);
                                }

                                return false;
                        }

                        snapshot.Type = targetLight.type;

                        bool hasAnyData = false;
                        bool useHdrpPath = false;

#if REMEMBERME_HDRP_PRESENT
                        if (activeRenderPipeline is HDRenderPipelineAsset)
                        {
                                useHdrpPath = true;
                                HDAdditionalLightData hdLight = GetComponent<HDAdditionalLightData>();
                                if (hdLight == null)
                                {
                                        if (logWarnings)
                                        {
                                                Logger.Log($"{nameof(RememberLight)}: HDAdditionalLightData component not found on '{gameObject.name}'. Skipping HDRP-specific serialization.", LogCategory.RememberLight, LogLevel.Warning);
                                        }

                                        return false;
                                }
                        }
#endif

                        switch (targetLight.type)
                        {
                                case LightType.Directional:
                                {
                                        if (rememberEnabled)
                                        {
                                                snapshot.HasEnabled = true;
                                                snapshot.Enabled = targetLight.enabled;
                                                hasAnyData = true;
                                        }

                                        if (rememberColor)
                                        {
                                                snapshot.HasColor = true;
                                                snapshot.Color = targetLight.color;
                                                hasAnyData = true;
                                        }

                                        if (rememberIntensity)
                                        {
                                                snapshot.HasIntensity = true;
                                                snapshot.Intensity = targetLight.intensity;
                                                hasAnyData = true;
                                        }

                                        if (rememberShadowsEnabled)
                                        {
                                                snapshot.HasShadowsEnabled = true;
                                                snapshot.ShadowsEnabled = targetLight.shadows != LightShadows.None;
                                                hasAnyData = true;
                                        }

                                        break;
                                }
                                case LightType.Point:
                                {
                                        if (rememberEnabled)
                                        {
                                                snapshot.HasEnabled = true;
                                                snapshot.Enabled = targetLight.enabled;
                                                hasAnyData = true;
                                        }

                                        if (rememberColor)
                                        {
                                                snapshot.HasColor = true;
                                                snapshot.Color = targetLight.color;
                                                hasAnyData = true;
                                        }

                                        if (rememberIntensity)
                                        {
                                                snapshot.HasIntensity = true;
                                                snapshot.Intensity = targetLight.intensity;
                                                hasAnyData = true;
                                        }

                                        if (rememberShadowsEnabled)
                                        {
                                                snapshot.HasShadowsEnabled = true;
                                                snapshot.ShadowsEnabled = targetLight.shadows != LightShadows.None;
                                                hasAnyData = true;
                                        }

                                        if (rememberPointLightRange)
                                        {
                                                snapshot.HasRange = true;
                                                snapshot.Range = targetLight.range;
                                                hasAnyData = true;
                                        }

                                        break;
                                }
                                case LightType.Spot:
                                {
                                        if (rememberEnabled)
                                        {
                                                snapshot.HasEnabled = true;
                                                snapshot.Enabled = targetLight.enabled;
                                                hasAnyData = true;
                                        }

                                        if (rememberColor)
                                        {
                                                snapshot.HasColor = true;
                                                snapshot.Color = targetLight.color;
                                                hasAnyData = true;
                                        }

                                        if (rememberIntensity)
                                        {
                                                snapshot.HasIntensity = true;
                                                snapshot.Intensity = targetLight.intensity;
                                                hasAnyData = true;
                                        }

                                        if (rememberShadowsEnabled)
                                        {
                                                snapshot.HasShadowsEnabled = true;
                                                snapshot.ShadowsEnabled = targetLight.shadows != LightShadows.None;
                                                hasAnyData = true;
                                        }

                                        if (rememberSpotLightRange)
                                        {
                                                snapshot.HasRange = true;
                                                snapshot.Range = targetLight.range;
                                                hasAnyData = true;
                                        }

                                        if (rememberSpotLightAngle)
                                        {
                                                snapshot.HasSpotAngle = true;
                                                snapshot.SpotAngle = targetLight.spotAngle;
                                                hasAnyData = true;
                                        }

                                        break;
                                }
                                case LightType.Rectangle:
                                {
                                        if (rememberEnabled)
                                        {
                                                snapshot.HasEnabled = true;
                                                snapshot.Enabled = targetLight.enabled;
                                                hasAnyData = true;
                                        }

                                        if (rememberColor)
                                        {
                                                snapshot.HasColor = true;
                                                snapshot.Color = targetLight.color;
                                                hasAnyData = true;
                                        }

                                        if (rememberIntensity)
                                        {
                                                snapshot.HasIntensity = true;
                                                snapshot.Intensity = targetLight.intensity;
                                                hasAnyData = true;
                                        }

                                        if (rememberShadowsEnabled)
                                        {
                                                snapshot.HasShadowsEnabled = true;
                                                snapshot.ShadowsEnabled = targetLight.shadows != LightShadows.None;
                                                hasAnyData = true;
                                        }

                                        if (rememberAreaLightRange)
                                        {
                                                snapshot.HasRange = true;
                                                snapshot.Range = targetLight.range;
                                                hasAnyData = true;
                                        }

                                        if (rememberAreaLightSize)
                                        {
                                                snapshot.HasSize = true;
                                                snapshot.Size = targetLight.transform.localScale;
                                                hasAnyData = true;
                                        }

                                        break;
                                }
                                default:
                                {
                                        if (logWarnings)
                                        {
                                                Logger.Log($"RememberLight: Unsupported LightType '{targetLight.type}' on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberLight, LogLevel.Warning);
                                        }

                                        return false;
                                }
                        }

                        if (!hasAnyData)
                        {
                                if (logWarnings)
                                {
                                        Logger.Log($"RememberLight: No light properties selected for serialization on '{gameObject.name}'.", LogCategory.RememberLight, LogLevel.Info);
                                }

                                return false;
                        }

                        snapshot.UsesHdrpPath = useHdrpPath;
                        return true;
                }

                private byte[] SerializeSnapshot(in LightSnapshot snapshot)
                {
                        switch (snapshot.Type)
                        {
                                case LightType.Directional:
                                {
                                        DirectionalLightData data = new DirectionalLightData
                                        {
                                                Enabled = snapshot.HasEnabled ? snapshot.Enabled : default,
                                                Color = snapshot.HasColor ? snapshot.Color : default,
                                                Intensity = snapshot.HasIntensity ? snapshot.Intensity : default,
                                                ShadowsEnabled = snapshot.HasShadowsEnabled && snapshot.ShadowsEnabled,
                                                Range = default,
                                                Size = default
                                        };

                                        return SaveDataSerializer.Instance.Serialize(data);
                                }
                                case LightType.Point:
                                {
                                        PointLightData data = new PointLightData
                                        {
                                                Enabled = snapshot.HasEnabled ? snapshot.Enabled : default,
                                                Color = snapshot.HasColor ? snapshot.Color : default,
                                                Intensity = snapshot.HasIntensity ? snapshot.Intensity : default,
                                                ShadowsEnabled = snapshot.HasShadowsEnabled && snapshot.ShadowsEnabled,
                                                Range = snapshot.HasRange ? snapshot.Range : default
                                        };

                                        return SaveDataSerializer.Instance.Serialize(data);
                                }
                                case LightType.Spot:
                                {
                                        SpotLightData data = new SpotLightData
                                        {
                                                Enabled = snapshot.HasEnabled ? snapshot.Enabled : default,
                                                Color = snapshot.HasColor ? snapshot.Color : default,
                                                Intensity = snapshot.HasIntensity ? snapshot.Intensity : default,
                                                ShadowsEnabled = snapshot.HasShadowsEnabled && snapshot.ShadowsEnabled,
                                                Range = snapshot.HasRange ? snapshot.Range : default,
                                                SpotAngle = snapshot.HasSpotAngle ? snapshot.SpotAngle : default
                                        };

                                        return SaveDataSerializer.Instance.Serialize(data);
                                }
                                case LightType.Rectangle:
                                {
                                        AreaLightData data = new AreaLightData
                                        {
                                                Enabled = snapshot.HasEnabled ? snapshot.Enabled : default,
                                                Color = snapshot.HasColor ? snapshot.Color : default,
                                                Intensity = snapshot.HasIntensity ? snapshot.Intensity : default,
                                                ShadowsEnabled = snapshot.HasShadowsEnabled && snapshot.ShadowsEnabled,
                                                Range = snapshot.HasRange ? snapshot.Range : default,
                                                Size = snapshot.HasSize ? snapshot.Size : default
                                        };

                                        return SaveDataSerializer.Instance.Serialize(data);
                                }
                                default:
                                {
                                        Logger.Log($"RememberLight: Unsupported LightType '{snapshot.Type}' on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberLight, LogLevel.Warning);
                                        return null;
                                }
                        }
                }

                private static bool AreEquivalent(in LightSnapshot a, in LightSnapshot b)
                {
                        if (a.Type != b.Type)
                        {
                                return false;
                        }

                        if (a.UsesHdrpPath != b.UsesHdrpPath)
                        {
                                return false;
                        }

                        if (!CompareBoolField(a.HasEnabled, a.Enabled, b.HasEnabled, b.Enabled)) return false;
                        if (!CompareColorField(a.HasColor, a.Color, b.HasColor, b.Color)) return false;
                        if (!CompareFloatField(a.HasIntensity, a.Intensity, b.HasIntensity, b.Intensity)) return false;
                        if (!CompareBoolField(a.HasShadowsEnabled, a.ShadowsEnabled, b.HasShadowsEnabled, b.ShadowsEnabled)) return false;
                        if (!CompareFloatField(a.HasRange, a.Range, b.HasRange, b.Range)) return false;
                        if (!CompareFloatField(a.HasSpotAngle, a.SpotAngle, b.HasSpotAngle, b.SpotAngle)) return false;
                        if (!CompareVectorField(a.HasSize, a.Size, b.HasSize, b.Size)) return false;

                        return true;
                }

                private static bool CompareBoolField(bool aHas, bool aValue, bool bHas, bool bValue)
                {
                        if (aHas != bHas)
                        {
                                return false;
                        }

                        return !aHas || aValue == bValue;
                }

                private static bool CompareFloatField(bool aHas, float aValue, bool bHas, float bValue)
                {
                        if (aHas != bHas)
                        {
                                return false;
                        }

                        return !aHas || Mathf.Approximately(aValue, bValue);
                }

                private static bool CompareColorField(bool aHas, Color aValue, bool bHas, Color bValue)
                {
                        if (aHas != bHas)
                        {
                                return false;
                        }

                        if (!aHas)
                        {
                                return true;
                        }

                        return Mathf.Abs(aValue.r - bValue.r) <= ColorTolerance &&
                               Mathf.Abs(aValue.g - bValue.g) <= ColorTolerance &&
                               Mathf.Abs(aValue.b - bValue.b) <= ColorTolerance &&
                               Mathf.Abs(aValue.a - bValue.a) <= ColorTolerance;
                }

                private static bool CompareVectorField(bool aHas, Vector3 aValue, bool bHas, Vector3 bValue)
                {
                        if (aHas != bHas)
                        {
                                return false;
                        }

                        if (!aHas)
                        {
                                return true;
                        }

                        return (aValue - bValue).sqrMagnitude <= FloatTolerance;
                }

                protected override void DeserializeComponentData(byte[] data)
                {
                        if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberLight, LogLevel.Warning);
				return;
			}

			if (targetLight == null)
			{
				Logger.Log("DeserializeComponentData failed: Light component not found.", LogCategory.RememberLight, LogLevel.Warning);
				return;
			}

                        try
                        {
                                bool deserialized = false;
                                switch (targetLight.type)
                                {
                                        case LightType.Directional:
                                                deserialized = DeserializeDirectionalLight(data);
                                                break;
                                        case LightType.Point:
                                                deserialized = DeserializePointLight(data);
                                                break;
                                        case LightType.Spot:
                                                deserialized = DeserializeSpotLight(data);
                                                break;
                                        case LightType.Rectangle:
                                                deserialized = DeserializeAreaLight(data);
                                                break;
                                        default:
                                                Logger.Log($"RememberLight: Unsupported LightType '{targetLight.type}' on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberLight, LogLevel.Warning);
                                                break;
                                }

                                if (deserialized)
                                {
                                        Logger.Log($"RememberLight: Successfully deserialized {targetLight.type} Light data for '{gameObject.name}'.", LogCategory.RememberLight, LogLevel.Info);

                                        if (skipSavingWhenUnchanged)
                                        {
                                                if (TryCaptureCurrentState(out LightSnapshot snapshot, false))
                                                {
                                                        cachedSnapshot = snapshot;
                                                        hasCachedSnapshot = true;
                                                }
                                                else
                                                {
                                                        hasCachedSnapshot = false;
                                                }
                                        }
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberLight: Deserialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberLight, LogLevel.Error);
                        }
                }

                private bool DeserializeDirectionalLight(byte[] data)
                {
#if REMEMBERME_HDRP_PRESENT
                        if (activeRenderPipeline is HDRenderPipelineAsset)
                        {
                                HDAdditionalLightData hdLight = GetComponent<HDAdditionalLightData>();
                                if (hdLight == null)
                                {
                                        Logger.Log($"DeserializeDirectionalLight: HDAdditionalLightData component not found on '{gameObject.name}'. Skipping HDRP-specific deserialization.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                DirectionalLightData deserializedData = SaveDataSerializer.Instance.Deserialize<DirectionalLightData>(data);
                                if (deserializedData == null)
                                {
                                        Logger.Log("DeserializeDirectionalLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                if (rememberEnabled) targetLight.enabled = deserializedData.Enabled;
                                if (rememberColor) targetLight.color = deserializedData.Color;
                                if (rememberIntensity) targetLight.intensity = deserializedData.Intensity;
                                if (rememberShadowsEnabled) targetLight.shadows = deserializedData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                                return true;
                        }
#endif

                        DirectionalLightData standardData = SaveDataSerializer.Instance.Deserialize<DirectionalLightData>(data);
                        if (standardData == null)
                        {
                                Logger.Log("DeserializeDirectionalLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                return false;
                        }

                        if (rememberEnabled) targetLight.enabled = standardData.Enabled;
                        if (rememberColor) targetLight.color = standardData.Color;
                        if (rememberIntensity) targetLight.intensity = standardData.Intensity;
                        if (rememberShadowsEnabled) targetLight.shadows = standardData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                        return true;
                }

                private bool DeserializePointLight(byte[] data)
                {
#if REMEMBERME_HDRP_PRESENT
                        if (activeRenderPipeline is HDRenderPipelineAsset)
                        {
                                HDAdditionalLightData hdLight = GetComponent<HDAdditionalLightData>();
                                if (hdLight == null)
                                {
                                        Logger.Log($"DeserializePointLight: HDAdditionalLightData component not found on '{gameObject.name}'. Skipping HDRP-specific deserialization.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                PointLightData deserializedData = SaveDataSerializer.Instance.Deserialize<PointLightData>(data);
                                if (deserializedData == null)
                                {
                                        Logger.Log("DeserializePointLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                if (rememberEnabled) targetLight.enabled = deserializedData.Enabled;
                                if (rememberColor) targetLight.color = deserializedData.Color;
                                if (rememberIntensity) targetLight.intensity = deserializedData.Intensity;
                                if (rememberShadowsEnabled) targetLight.shadows = deserializedData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                                if (rememberPointLightRange) targetLight.range = deserializedData.Range;
                                return true;
                        }
#endif

                        PointLightData standardData = SaveDataSerializer.Instance.Deserialize<PointLightData>(data);
                        if (standardData == null)
                        {
                                Logger.Log("DeserializePointLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                return false;
                        }

                        if (rememberEnabled) targetLight.enabled = standardData.Enabled;
                        if (rememberColor) targetLight.color = standardData.Color;
                        if (rememberIntensity) targetLight.intensity = standardData.Intensity;
                        if (rememberShadowsEnabled) targetLight.shadows = standardData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                        if (rememberPointLightRange) targetLight.range = standardData.Range;
                        return true;
                }

                private bool DeserializeSpotLight(byte[] data)
                {
#if REMEMBERME_HDRP_PRESENT
                        if (activeRenderPipeline is HDRenderPipelineAsset)
                        {
                                HDAdditionalLightData hdLight = GetComponent<HDAdditionalLightData>();
                                if (hdLight == null)
                                {
                                        Logger.Log($"DeserializeSpotLight: HDAdditionalLightData component not found on '{gameObject.name}'. Skipping HDRP-specific deserialization.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                SpotLightData deserializedData = SaveDataSerializer.Instance.Deserialize<SpotLightData>(data);
                                if (deserializedData == null)
                                {
                                        Logger.Log("DeserializeSpotLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                if (rememberEnabled) targetLight.enabled = deserializedData.Enabled;
                                if (rememberColor) targetLight.color = deserializedData.Color;
                                if (rememberIntensity) targetLight.intensity = deserializedData.Intensity;
                                if (rememberShadowsEnabled) targetLight.shadows = deserializedData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                                if (rememberSpotLightRange) targetLight.range = deserializedData.Range;
                                if (rememberSpotLightAngle) targetLight.spotAngle = deserializedData.SpotAngle;
                                return true;
                        }
#endif

                        SpotLightData standardData = SaveDataSerializer.Instance.Deserialize<SpotLightData>(data);
                        if (standardData == null)
                        {
                                Logger.Log("DeserializeSpotLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                return false;
                        }

                        if (rememberEnabled) targetLight.enabled = standardData.Enabled;
                        if (rememberColor) targetLight.color = standardData.Color;
                        if (rememberIntensity) targetLight.intensity = standardData.Intensity;
                        if (rememberShadowsEnabled) targetLight.shadows = standardData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                        if (rememberSpotLightRange) targetLight.range = standardData.Range;
                        if (rememberSpotLightAngle) targetLight.spotAngle = standardData.SpotAngle;
                        return true;
                }

                private bool DeserializeAreaLight(byte[] data)
                {
#if REMEMBERME_HDRP_PRESENT
                        if (activeRenderPipeline is HDRenderPipelineAsset)
                        {
                                HDAdditionalLightData hdLight = GetComponent<HDAdditionalLightData>();
                                if (hdLight == null)
                                {
                                        Logger.Log($"DeserializeAreaLight: HDAdditionalLightData component not found on '{gameObject.name}'. Skipping HDRP-specific deserialization.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                AreaLightData deserializedData = SaveDataSerializer.Instance.Deserialize<AreaLightData>(data);
                                if (deserializedData == null)
                                {
                                        Logger.Log("DeserializeAreaLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                        return false;
                                }

                                if (rememberEnabled) targetLight.enabled = deserializedData.Enabled;
                                if (rememberColor) targetLight.color = deserializedData.Color;
                                if (rememberIntensity) targetLight.intensity = deserializedData.Intensity;
                                if (rememberShadowsEnabled) targetLight.shadows = deserializedData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                                if (rememberAreaLightRange) targetLight.range = deserializedData.Range;
                                if (rememberAreaLightSize) targetLight.transform.localScale = deserializedData.Size;
                                return true;
                        }
#endif

                        AreaLightData standardData = SaveDataSerializer.Instance.Deserialize<AreaLightData>(data);
                        if (standardData == null)
                        {
                                Logger.Log("DeserializeAreaLight failed: deserialized data is null.", LogCategory.RememberLight, LogLevel.Warning);
                                return false;
                        }

                        if (rememberEnabled) targetLight.enabled = standardData.Enabled;
                        if (rememberColor) targetLight.color = standardData.Color;
                        if (rememberIntensity) targetLight.intensity = standardData.Intensity;
                        if (rememberShadowsEnabled) targetLight.shadows = standardData.ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
                        if (rememberAreaLightRange) targetLight.range = standardData.Range;
                        if (rememberAreaLightSize)
                        {
                                Logger.Log($"RememberLight: Size is not applicable in Standard Render Pipeline for '{gameObject.name}'.", LogCategory.RememberLight, LogLevel.Info);
                        }
                        return true;
                }
        }

	[MemoryPackable]
	public partial class AreaLightData : IMemoryPackable<AreaLightData>
	{
		public bool Enabled { get; set; }
		public Color Color { get; set; }
		public float Intensity { get; set; }
		public bool ShadowsEnabled { get; set; }
		public float Range { get; set; }
		public Vector3 Size { get; set; }

		public AreaLightData() { }
	}

	[MemoryPackable]
	public partial class SpotLightData : IMemoryPackable<SpotLightData>
	{
		public bool Enabled { get; set; }
		public Color Color { get; set; }
		public float Intensity { get; set; }
		public bool ShadowsEnabled { get; set; }
		public float Range { get; set; }
		public float SpotAngle { get; set; }

		public SpotLightData() { }
	}

	[MemoryPackable]
	public partial class PointLightData : IMemoryPackable<PointLightData>
	{
		public bool Enabled { get; set; }
		public Color Color { get; set; }
		public float Intensity { get; set; }
		public bool ShadowsEnabled { get; set; }
		public float Range { get; set; }

		public PointLightData() { }
	}

	[MemoryPackable]
	public partial class DirectionalLightData : IMemoryPackable<DirectionalLightData>
	{
		public bool Enabled { get; set; }
		public Color Color { get; set; }
		public float Intensity { get; set; }
		public bool ShadowsEnabled { get; set; }
		public float Range { get; set; }
		public Vector3 Size { get; set; }

		public DirectionalLightData() { }
	}
}
#endif
