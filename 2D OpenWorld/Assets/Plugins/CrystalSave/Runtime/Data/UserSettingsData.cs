#if REMEMBERME_NVIDIA_DLSS_PRESENT
using UnityEngine.NVIDIA;
#endif
#if REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Arawn.CrystalSave.Runtime
{
	[System.Serializable]
	public class UserSettingsData
	{
		// ------ Graphics / Video Settings ------
		public int ResolutionWidth = 1920;
		public int ResolutionHeight = 1080;
		public bool FullScreen = true;
		public int FullScreenModeValue = 1; // 0=ExclusiveFullScreen, 1=FullScreenWindow (Borderless), 2=MaximizedWindow, 3=Windowed
		public int QualityLevel = 2;
		public int AntiAliasing = 0;      // 0=Off, 2=2x, 4=4x, 8=8x
		public int QualityAntiAliasing = 0;
		public float QualityShadowDistance = 150f;
		public int QualityVSyncCount = 1;
		public int ShadowQuality = 2;     // 0=Low, 1=Med, 2=High, 3=Ultra
		public int TextureQuality = 0;    // 0=FullRes, 1=Half, etc.
		public int TargetFPS = -1;

		// ------ Locale Settings ------
		public string LocaleCode = "en";  // Default locale is English

		// ------ Volume Overrides (URP) ------
		public bool URP_AmbientOcclusion = true;
		public bool URP_MotionBlur = true;
		public bool URP_Bloom = true;
		public bool URP_FilmGrain = true;
		public bool URP_ChannelMixer = true;
		public bool URP_ChromaticAberration = true;
		public bool URP_ColorAdjustments = true;
		public bool URP_ColorCurves = true;
		public bool URP_ColorLookup = true;
		public bool URP_DepthOfField = true;
		public bool URP_LensDistortion = true;
		public bool URP_LiftGammaGain = true;
		public bool URP_PaniniProjection = true;
		public bool URP_ScreenSpaceLensFlare = true;
		public bool URP_ShadowsMidtonesHighlights = true;
		public bool URP_SplitToning = true;
		public bool URP_Tonemapping = true;
		public bool URP_Vignette = true;
		public bool URP_WhiteBalance = true;

		// ------ Volume Overrides (HDRP) ------
		public bool HDRP_AmbientOcclusion = true;
		public bool HDRP_MotionBlur = true;
		public bool HDRP_Bloom = true;
		public bool HDRP_FilmGrain = true;
		public bool HDRP_ChannelMixer = true;
		public bool HDRP_ChromaticAberration = true;
		public bool HDRP_ColorCurves = true;
		public bool HDRP_ColorAdjustments = true;
		public bool HDRP_DepthOfField = true;
		public bool HDRP_LensDistortion = true;
		public bool HDRP_LiftGammaGain = true;
		public bool HDRP_PaniniProjection = true;
		public bool HDRP_ScreenSpaceLensFlare = true;
		public bool HDRP_ShadowsMidtonesHighlights = true;
		public bool HDRP_SplitToning = true;
		public bool HDRP_Tonemapping = true;
		public bool HDRP_Vignette = true;
		public bool HDRP_WhiteBalance = true;

		// NEW: Add HDRP ScreenSpaceAmbientOcclusion field
		public bool HDRP_ScreenSpaceAmbientOcclusion = true;

		// ------ Audio Settings ------
		public float MasterVolume = 1.0f;
		public float MusicVolume = 0.8f;
		public float SfxVolume = 0.8f;
		public float VoiceVolume = 0.8f;

		// ------ Camera FOV ------
		public float CameraFOV = 60f;

		// ------ Main Camera HDRP Settings ------
		// These fields store the state of the Main Camera's HDAdditionalCameraData:
		public bool MainCamera_DynamicResolutionEnabled = false;
		public bool MainCamera_AllowDeepLearningSuperSampling = true;
		public bool MainCamera_DeepLearningSuperSamplingUseCustomQualitySettings = false;
#if REMEMBERME_NVIDIA_DLSS_PRESENT && REMEMBERME_HDRP_PRESENT
		public uint MainCamera_DeepLearningSuperSamplingQuality = (uint)DLSSQuality.Balanced;
#endif
		public bool MainCamera_AllowFidelityFX2SuperResolution = true;
#if REMEMBERME_HDRP_PRESENT
		public HDAdditionalCameraData.AntialiasingMode MainCamera_Antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
#endif
		// ------ Main Camera URP Settings ------
#if REMEMBERME_URP_PRESENT
        public UnityEngine.Rendering.Universal.AntialiasingMode MainCamera_UrpAntialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
#endif
		// ------ Post-Processing Stack v2 Overrides ------
		public bool MotionBlurOverrideEnabled = true;
		public bool DepthOfFieldOverrideEnabled = true;
		public bool BloomOverrideEnabled = true;
		public bool LensDistortionOverrideEnabled = true;
		public bool ChromaticAberrationOverrideEnabled = true;
		public bool AutoExposureOverrideEnabled = true;
		public bool ColorGradingOverrideEnabled = true;
		public bool VignetteOverrideEnabled = true;
		public bool GrainOverrideEnabled = true;
		public bool ScreenSpaceReflectionsOverrideEnabled = true;
		public bool AmbientOcclusionOverrideEnabled = true;
	}
}
