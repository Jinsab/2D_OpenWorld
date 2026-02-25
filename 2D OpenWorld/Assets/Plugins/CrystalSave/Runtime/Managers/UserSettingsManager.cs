#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using System.IO;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

#if REMEMBERME_LOCALIZATION_PRESENT
using UnityEngine.Localization.Settings;
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering;  // For Volume, VolumeProfile
#endif
#if REMEMBERME_HDRP_PRESENT && REMEMBERME_NVIDIA_DLSS_PRESENT
using UnityEngine.NVIDIA;
#endif
#if REMEMBERME_URP_PRESENT
using UnityEngine.Rendering.Universal;  // For URP-specific components
#endif
#if REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition; // For HDRP-specific components
#endif
#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;  // For PostProcessVolume (PPv2)
#endif

namespace Arawn.CrystalSave.Runtime
{
	public class UserSettingsManager : MonoBehaviour 
	{
		// --------------------------------------------------------
		// Singleton
		// --------------------------------------------------------
		public static UserSettingsManager Instance { get; private set; }

		// --------------------------------------------------------
		// Fields & Serialized Fields
		// --------------------------------------------------------
		[Header("Camera Settings")]
		[Tooltip("Reference to the main camera for syncing FOV. If not assigned, Camera.main is used.")]
		[SerializeField] private Camera mainCamera;
		[Tooltip("If true, automatically references the main camera of the current scene when mainCamera is null.")]
		[SerializeField] private bool autoReferenceMainCamera = true;

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
		[Header("Global Volume (for Volume Overrides)")]
		[Tooltip("Assign your global Volume here if using URP/HDRP with Volume-based overrides.")]
		[SerializeField] private Volume globalVolume;
		[Tooltip("If true, automatically references the Global Volume in the current scene when globalVolume is null.")]
		[SerializeField] private bool autoReferenceGlobalVolume = true;
#endif

#if UNITY_POST_PROCESSING_STACK_V2
        [Header("Built-in Post Processing (v2)")]
        [Tooltip("Assign your PostProcessVolume here if you want to toggle PPv2 overrides.")]
        [SerializeField] private PostProcessVolume builtinPostProcessVolume;
        [Tooltip("If true, automatically references the Post-Process Volume in the current scene when null.")]
        [SerializeField] private bool autoReferencePostProcessVolume = true;
#endif

		[Header("Audio Mixer")]
		[SerializeField] private AudioMixer audioMixer;
		[SerializeField] private string masterVolumeParam = "Master";
		[SerializeField] private string musicVolumeParam = "Music";
		[SerializeField] private string sfxVolumeParam = "SFX";
		[SerializeField] private string voiceVolumeParam = "Voice";

		[Header("Dynamic Resolution Mode")]
		[Tooltip("Choose whether to use the HDRP Asset or the Main Camera's setting for dynamic resolution state.")]

		// Settings data container (serialized as JSON)
		private UserSettingsData currentSettings;
		private const string UserSettingsFileName = "UserSettings.json";

		// --------------------------------------------------------
		// Properties
		// --------------------------------------------------------
                private string UserSettingsPath => Path.Combine(SaveManager.Instance?.RootPath ?? Application.persistentDataPath, UserSettingsFileName);

		// --------------------------------------------------------
		// Unity Lifecycle Methods
		// --------------------------------------------------------
		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);
			SceneManager.sceneLoaded += OnSceneLoaded;
			LoadSettings();
		}

		private void Start()
		{
			ApplyAudioMixerSettings();
			ApplyHDRPDynamicResolutionSettings();
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			UpdateMainCameraReference();
#if UNITY_POST_PROCESSING_STACK_V2
            FindPostProcessVolume();
#endif
#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
			FindGlobalVolume();
#endif
			ApplySettings();
		}

		// --------------------------------------------------------
		// Public API
		// --------------------------------------------------------
		public void LoadSettings()
		{
			if (File.Exists(UserSettingsPath))
			{
				string json = File.ReadAllText(UserSettingsPath);
				currentSettings = JsonUtility.FromJson<UserSettingsData>(json);
				Logger.Log($"UserSettingsManager: Loaded settings from '{UserSettingsPath}'", LogCategory.UserSettingsManager, LogLevel.Info);
			}
			else
			{
				Logger.Log("UserSettingsManager: No settings file found. Using default settings.", LogCategory.UserSettingsManager, LogLevel.Info);
				currentSettings = new UserSettingsData();
			}
			ApplySettings();
			ApplyAudioMixerSettings();
			ApplyHDRPDynamicResolutionSettings();
		}

		public void SaveSettings()
		{
			if (currentSettings == null)
			{
				Logger.Log("UserSettingsManager: currentSettings was null in SaveSettings(). Creating a new instance.", LogCategory.UserSettingsManager, LogLevel.Warning);
				currentSettings = new UserSettingsData();
			}
			SyncFromRuntime();
			string json = JsonUtility.ToJson(currentSettings, true);
			File.WriteAllText(UserSettingsPath, json);
			Logger.Log($"UserSettingsManager: Saved user settings to '{UserSettingsPath}'", LogCategory.UserSettingsManager, LogLevel.Info);
		}

public void ApplySettings()
{
    if (currentSettings == null)
    {
        Logger.Log("UserSettingsManager: currentSettings is null. Cannot apply settings.", LogCategory.UserSettingsManager, LogLevel.Warning);
        return;
    }

    UpdateMainCameraReference();
#if UNITY_POST_PROCESSING_STACK_V2
    FindPostProcessVolume();
#endif
#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
    FindGlobalVolume();
#endif

    QualitySettings.SetQualityLevel(currentSettings.QualityLevel, true);
    Logger.Log($"Applied Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}", LogCategory.UserSettingsManager, LogLevel.Info);

    // Apply resolution with proper fullscreen mode
    FullScreenMode mode = (FullScreenMode)currentSettings.FullScreenModeValue;
    Screen.SetResolution(
        currentSettings.ResolutionWidth,
        currentSettings.ResolutionHeight,
        mode
    );
    Logger.Log($"Applied Resolution: {currentSettings.ResolutionWidth}x{currentSettings.ResolutionHeight}, Mode={mode}", LogCategory.UserSettingsManager, LogLevel.Info);
    
    QualitySettings.globalTextureMipmapLimit = currentSettings.TextureQuality;

    // Apply built-in QualitySettings for anti aliasing, shadow distance, and vSync count:
    QualitySettings.antiAliasing   = currentSettings.QualityAntiAliasing;
    QualitySettings.shadowDistance = currentSettings.QualityShadowDistance;
    QualitySettings.vSyncCount     = currentSettings.QualityVSyncCount;
    Application.targetFrameRate    = currentSettings.TargetFPS;
    Logger.Log(
        $"Applied QualitySettings: AntiAliasing = {QualitySettings.antiAliasing}, " +
        $"ShadowDistance = {QualitySettings.shadowDistance}, " +
        $"VSyncCount = {QualitySettings.vSyncCount}, " +
        $"TargetFPS = {Application.targetFrameRate}",
        LogLevel.Info
    );

    ApplyShadowQuality(currentSettings.ShadowQuality);

#if REMEMBERME_HDRP_PRESENT
    if (globalVolume != null && globalVolume.profile != null)
    {
        // Notice: Each generic <T> is now fully qualified to HDRP's namespaces,
        // so there is no conflict with PPv2 or URP.

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.MotionBlur>(
            globalVolume.profile,
            currentSettings.HDRP_MotionBlur,
            "Motion Blur (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.Bloom>(
            globalVolume.profile,
            currentSettings.HDRP_Bloom,
            "Bloom (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.ChannelMixer>(
            globalVolume.profile,
            currentSettings.HDRP_ChannelMixer,
            "Channel Mixer (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.ChromaticAberration>(
            globalVolume.profile,
            currentSettings.HDRP_ChromaticAberration,
            "Chromatic Aberration (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.FilmGrain>(
            globalVolume.profile,
            currentSettings.HDRP_FilmGrain,
            "Film Grain (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.ColorCurves>(
            globalVolume.profile,
            currentSettings.HDRP_ColorCurves,
            "Color Curves (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.ColorAdjustments>(
            globalVolume.profile,
            currentSettings.HDRP_ColorAdjustments,
            "Color Lookup (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.DepthOfField>(
            globalVolume.profile,
            currentSettings.HDRP_DepthOfField,
            "Depth Of Field (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.LensDistortion>(
            globalVolume.profile,
            currentSettings.HDRP_LensDistortion,
            "Lens Distortion (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.LiftGammaGain>(
            globalVolume.profile,
            currentSettings.HDRP_LiftGammaGain,
            "Lift Gamma Gain (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.PaniniProjection>(
            globalVolume.profile,
            currentSettings.HDRP_PaniniProjection,
            "Panini Projection (HDRP)"
        );

#if UNITY_6000_0_OR_NEWER
        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.ScreenSpaceLensFlare>(
            globalVolume.profile,
            currentSettings.HDRP_ScreenSpaceLensFlare,
            "Screen Space Lens Flare (HDRP)"
        );
#endif

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.ShadowsMidtonesHighlights>(
            globalVolume.profile,
            currentSettings.HDRP_ShadowsMidtonesHighlights,
            "Shadows Midtones Highlights (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.SplitToning>(
            globalVolume.profile,
            currentSettings.HDRP_SplitToning,
            "Split Toning (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.Tonemapping>(
            globalVolume.profile,
            currentSettings.HDRP_Tonemapping,
            "Tone Mapping (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.Vignette>(
            globalVolume.profile,
            currentSettings.HDRP_Vignette,
            "Vignette (HDRP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.HighDefinition.WhiteBalance>(
            globalVolume.profile,
            currentSettings.HDRP_WhiteBalance,
            "White Balance (HDRP)"
        );

        ApplyVolumeOverride<ScreenSpaceAmbientOcclusion>(
            globalVolume.profile,
            currentSettings.HDRP_ScreenSpaceAmbientOcclusion,
            "Ambient Occlusion (HDRP)"
        );
    }
#elif REMEMBERME_URP_PRESENT
    var urpData = mainCamera?.GetComponent<
                      UnityEngine.Rendering.Universal.UniversalAdditionalCameraData
                  >();
    if (urpData != null)
    {
        urpData.antialiasing = currentSettings.MainCamera_UrpAntialiasing;
        Logger.Log(
            $"Applied Main Camera URP settings: Antialiasing = {currentSettings.MainCamera_UrpAntialiasing}",
            LogLevel.Info
        );
    }

    if (globalVolume != null && globalVolume.profile != null)
    {
        // Again, fully qualify every generic argument to avoid PPv2/URP ambiguity:

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.MotionBlur>(
            globalVolume.profile,
            currentSettings.URP_MotionBlur,
            "Motion Blur (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.Bloom>(
            globalVolume.profile,
            currentSettings.URP_Bloom,
            "Bloom (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.FilmGrain>(
            globalVolume.profile,
            currentSettings.URP_FilmGrain,
            "Film Grain (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.ChannelMixer>(
            globalVolume.profile,
            currentSettings.URP_ChannelMixer,
            "Channel Mixer (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.ChromaticAberration>(
            globalVolume.profile,
            currentSettings.URP_ChromaticAberration,
            "Chromatic Aberration (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.ColorAdjustments>(
            globalVolume.profile,
            currentSettings.URP_ColorAdjustments,
            "Color Adjustments (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.ColorCurves>(
            globalVolume.profile,
            currentSettings.URP_ColorCurves,
            "Color Curves (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.ColorLookup>(
            globalVolume.profile,
            currentSettings.URP_ColorLookup,
            "Color Lookup (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.DepthOfField>(
            globalVolume.profile,
            currentSettings.URP_DepthOfField,
            "Depth Of Field (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.LensDistortion>(
            globalVolume.profile,
            currentSettings.URP_LensDistortion,
            "Lens Distortion (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.LiftGammaGain>(
            globalVolume.profile,
            currentSettings.URP_LiftGammaGain,
            "Lift Gamma Gain (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.PaniniProjection>(
            globalVolume.profile,
            currentSettings.URP_PaniniProjection,
            "Panini Projection (URP)"
        );

#if UNITY_6000_0_OR_NEWER
        ApplyVolumeOverride<UnityEngine.Rendering.Universal.ScreenSpaceLensFlare>(
            globalVolume.profile,
            currentSettings.URP_ScreenSpaceLensFlare,
            "Screen Space Lens Flare (URP)"
        );
#endif

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.ShadowsMidtonesHighlights>(
            globalVolume.profile,
            currentSettings.URP_ShadowsMidtonesHighlights,
            "Shadows Midtones Highlights (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.SplitToning>(
            globalVolume.profile,
            currentSettings.URP_SplitToning,
            "Split Toning (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.Tonemapping>(
            globalVolume.profile,
            currentSettings.URP_Tonemapping,
            "Tone Mapping (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.Vignette>(
            globalVolume.profile,
            currentSettings.URP_Vignette,
            "Vignette (URP)"
        );

        ApplyVolumeOverride<UnityEngine.Rendering.Universal.WhiteBalance>(
            globalVolume.profile,
            currentSettings.URP_WhiteBalance,
            "White Balance (URP)"
        );
    }
#else
    // If neither URP nor HDRP, fall back to the built‐in PPv2 block (if enabled)
    QualitySettings.antiAliasing = currentSettings.AntiAliasing;
#if UNITY_POST_PROCESSING_STACK_V2
    ApplyPostProcessingStackV2Overrides(currentSettings.MotionBlurOverrideEnabled);
#endif
#endif

    // 6) Camera FOV
    if (mainCamera != null && !mainCamera.orthographic)
    {
        mainCamera.fieldOfView = currentSettings.CameraFOV;
        Logger.Log(
            $"UserSettingsManager: Set mainCamera FOV to {currentSettings.CameraFOV}",
            LogLevel.Info
        );
    }
    else
    {
        Logger.Log(
            "UserSettingsManager: No valid main camera found (or orthographic). Cannot set FOV.",
            LogLevel.Warning
        );
    }

    Logger.Log("UserSettingsManager: Applied user settings.", LogCategory.UserSettingsManager, LogLevel.Info);
}


		public void ApplyAudioMixerSettings()
		{
			if (audioMixer == null)
			{
				Logger.Log("UserSettingsManager: No Audio Mixer assigned. Cannot apply audio settings.", LogCategory.UserSettingsManager, LogLevel.Warning);
				return;
			}
			audioMixer.SetFloat(masterVolumeParam, LinearToDecibels(currentSettings.MasterVolume));
			audioMixer.SetFloat(musicVolumeParam, LinearToDecibels(currentSettings.MusicVolume));
			audioMixer.SetFloat(sfxVolumeParam, LinearToDecibels(currentSettings.SfxVolume));
			audioMixer.SetFloat(voiceVolumeParam, LinearToDecibels(currentSettings.VoiceVolume));
			Logger.Log("UserSettingsManager: Applied Audio Mixer volumes.", LogCategory.UserSettingsManager, LogLevel.Info);
		}

		public void ApplyHDRPDynamicResolutionSettings()
		{
#if REMEMBERME_HDRP_PRESENT
			HDAdditionalCameraData hdCameraData = mainCamera.GetComponent<HDAdditionalCameraData>();
			if (hdCameraData != null)
			{
				hdCameraData.allowDynamicResolution = currentSettings.MainCamera_DynamicResolutionEnabled;
				hdCameraData.allowDeepLearningSuperSampling = currentSettings.MainCamera_AllowDeepLearningSuperSampling;
    #if UNITY_6000_0_OR_NEWER
				hdCameraData.allowFidelityFX2SuperResolution = currentSettings.MainCamera_AllowFidelityFX2SuperResolution;
    #endif
				hdCameraData.deepLearningSuperSamplingUseCustomQualitySettings = currentSettings.MainCamera_DeepLearningSuperSamplingUseCustomQualitySettings;
#if REMEMBERME_NVIDIA_DLSS_PRESENT && REMEMBERME_HDRP_PRESENT
				hdCameraData.deepLearningSuperSamplingQuality = currentSettings.MainCamera_DeepLearningSuperSamplingQuality;
#endif
				hdCameraData.antialiasing = currentSettings.MainCamera_Antialiasing;
				Logger.Log($"Applied Main Camera HDRP settings: DynamicResolution = {currentSettings.MainCamera_DynamicResolutionEnabled}, " +
						   $"DLSS = {currentSettings.MainCamera_AllowDeepLearningSuperSampling}, FSR2 = {currentSettings.MainCamera_AllowFidelityFX2SuperResolution}, " +
						   $"Custom DLSS = {currentSettings.MainCamera_DeepLearningSuperSamplingUseCustomQualitySettings}, " +
#if REMEMBERME_NVIDIA_DLSS_PRESENT && REMEMBERME_HDRP_PRESENT
						   $"Quality = {currentSettings.MainCamera_DeepLearningSuperSamplingQuality}, " +
#endif
						   $"Antialiasing = {currentSettings.MainCamera_Antialiasing}",
						   LogLevel.Info);
			}
#endif
			}

public void SyncFromRuntime()
{
    if (currentSettings == null)
    {
        Logger.Log(
            "UserSettingsManager: currentSettings is null. Cannot sync from runtime.",
            LogLevel.Warning
        );
        return;
    }

    UpdateMainCameraReference();

    // Basic resolution/quality fields:
    currentSettings.ResolutionWidth  = Screen.width;
    currentSettings.ResolutionHeight = Screen.height;
    currentSettings.FullScreen       = Screen.fullScreen;
    currentSettings.FullScreenModeValue = (int)Screen.fullScreenMode;
    currentSettings.QualityLevel     = QualitySettings.GetQualityLevel();
    Logger.Log(
        $"Synced Resolution: {currentSettings.ResolutionWidth}x{currentSettings.ResolutionHeight}, Mode={Screen.fullScreenMode}",
        LogLevel.Info
    );
    Logger.Log(
        $"Synced Quality Level: {QualitySettings.names[currentSettings.QualityLevel]}",
        LogLevel.Info
    );

    currentSettings.QualityAntiAliasing  = QualitySettings.antiAliasing;
    currentSettings.QualityShadowDistance = QualitySettings.shadowDistance;
    currentSettings.QualityVSyncCount     = QualitySettings.vSyncCount;
    currentSettings.TargetFPS             = Application.targetFrameRate;
    Logger.Log(
        $"Synced QualitySettings: " +
        $"AntiAliasing = {currentSettings.QualityAntiAliasing}, " +
        $"ShadowDistance = {currentSettings.QualityShadowDistance}, " +
        $"VSyncCount = {currentSettings.QualityVSyncCount}, " +
        $"TargetFPS = {currentSettings.TargetFPS}",
        LogLevel.Info
    );

    // Camera FOV (if non-orthographic)
    if (mainCamera != null && !mainCamera.orthographic)
    {
        currentSettings.CameraFOV = mainCamera.fieldOfView;
    }

    // HDRP-specific camera data:
#if REMEMBERME_HDRP_PRESENT
    if (mainCamera != null)
    {
        HDAdditionalCameraData hdCameraData =
            mainCamera.GetComponent<HDAdditionalCameraData>();

        if (hdCameraData != null)
        {
            currentSettings.MainCamera_DynamicResolutionEnabled =
                hdCameraData.allowDynamicResolution;

            currentSettings.MainCamera_AllowDeepLearningSuperSampling =
                hdCameraData.allowDeepLearningSuperSampling;
                
    #if UNITY_6000_0_OR_NEWER
            currentSettings.MainCamera_AllowFidelityFX2SuperResolution =
                hdCameraData.allowFidelityFX2SuperResolution;
    #endif

                    currentSettings.MainCamera_DeepLearningSuperSamplingUseCustomQualitySettings =
                hdCameraData.deepLearningSuperSamplingUseCustomQualitySettings;

    #if REMEMBERME_NVIDIA_DLSS_PRESENT && REMEMBERME_HDRP_PRESENT
            currentSettings.MainCamera_DeepLearningSuperSamplingQuality =
                hdCameraData.deepLearningSuperSamplingQuality;
    #endif

            currentSettings.MainCamera_Antialiasing = hdCameraData.antialiasing;

            Logger.Log(
                "UserSettingsManager: Synced Main Camera HDRP settings.",
                LogLevel.Info
            );
        }
    }
#endif

    // URP-specific camera data:
#if REMEMBERME_URP_PRESENT
    if (mainCamera != null)
    {
        var urpData =
            mainCamera.GetComponent<
                UnityEngine.Rendering.Universal.UniversalAdditionalCameraData
            >();

        if (urpData != null)
        {
            currentSettings.MainCamera_UrpAntialiasing = urpData.antialiasing;
            Logger.Log(
                "UserSettingsManager: Synced Main Camera URP anti aliasing settings.",
                LogLevel.Info
            );
        }
    }
#endif

    // AudioMixer volumes:
    if (audioMixer != null)
    {
        audioMixer.GetFloat(masterVolumeParam, out float masterDb);
        currentSettings.MasterVolume = DecibelsToLinear(masterDb);

        audioMixer.GetFloat(musicVolumeParam, out float musicDb);
        currentSettings.MusicVolume = DecibelsToLinear(musicDb);

        audioMixer.GetFloat(sfxVolumeParam, out float sfxDb);
        currentSettings.SfxVolume = DecibelsToLinear(sfxDb);

        audioMixer.GetFloat(voiceVolumeParam, out float voiceDb);
        currentSettings.VoiceVolume = DecibelsToLinear(voiceDb);

        Logger.Log(
            "UserSettingsManager: Synced Audio Mixer volumes.",
            LogLevel.Info
        );
    }

#if REMEMBERME_LOCALIZATION_PRESENT
    // Localization:
    var currentLocale = LocalizationSettings.SelectedLocale;
    if (currentLocale != null)
    {
        currentSettings.LocaleCode = currentLocale.Identifier.Code;
        Logger.Log(
            $"UserSettingsManager: Synced locale '{currentSettings.LocaleCode}'.",
            LogLevel.Info
        );
    }
#endif

#if UNITY_POST_PROCESSING_STACK_V2
    // Built-in PPv2 overrides:
    if (builtinPostProcessVolume != null && builtinPostProcessVolume.profile != null)
    {
        var profile = builtinPostProcessVolume.profile;

        // Fully‐qualified PPv2 types to avoid ambiguity with URP/HDRP:
        var motionBlur =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.MotionBlur
            >();
        if (motionBlur != null)
            currentSettings.MotionBlurOverrideEnabled = motionBlur.enabled.value;

        var dof =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.DepthOfField
            >();
        if (dof != null)
            currentSettings.DepthOfFieldOverrideEnabled = dof.enabled.value;

        var bloom =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.Bloom
            >();
        if (bloom != null)
            currentSettings.BloomOverrideEnabled = bloom.enabled.value;

        var lens =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.LensDistortion
            >();
        if (lens != null)
            currentSettings.LensDistortionOverrideEnabled = lens.enabled.value;

        var chromatic =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.ChromaticAberration
            >();
        if (chromatic != null)
            currentSettings.ChromaticAberrationOverrideEnabled =
                chromatic.enabled.value;

        var autoExposure =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.AutoExposure
            >();
        if (autoExposure != null)
            currentSettings.AutoExposureOverrideEnabled =
                autoExposure.enabled.value;

        var colorGrading =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.ColorGrading
            >();
        if (colorGrading != null)
            currentSettings.ColorGradingOverrideEnabled =
                colorGrading.enabled.value;

        var vignette =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.Vignette
            >();
        if (vignette != null)
            currentSettings.VignetteOverrideEnabled = vignette.enabled.value;

        var grain =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.Grain
            >();
        if (grain != null)
            currentSettings.GrainOverrideEnabled = grain.enabled.value;

        var ssr =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.ScreenSpaceReflections
            >();
        if (ssr != null)
            currentSettings.ScreenSpaceReflectionsOverrideEnabled =
                ssr.enabled.value;

        var ao =
            profile.GetSetting<
                UnityEngine.Rendering.PostProcessing.AmbientOcclusion
            >();
        if (ao != null)
            currentSettings.AmbientOcclusionOverrideEnabled = ao.enabled.value;

        Logger.Log(
            "UserSettingsManager: Synced PPv2 override states.",
            LogLevel.Info
        );
    }
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
    // URP/HDRP Volume overrides:
    if (globalVolume != null && globalVolume.profile != null)
    {
#if REMEMBERME_URP_PRESENT
        // Each generic <T> fully qualified to URP’s namespace:
        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.MotionBlur
        >(globalVolume.profile, (val) => currentSettings.URP_MotionBlur = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.Bloom
        >(globalVolume.profile, (val) => currentSettings.URP_Bloom = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.FilmGrain
        >(globalVolume.profile, (val) => currentSettings.URP_FilmGrain = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.ChannelMixer
        >(globalVolume.profile, (val) => currentSettings.URP_ChannelMixer = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.ChromaticAberration
        >(globalVolume.profile, (val) => currentSettings.URP_ChromaticAberration = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.ColorAdjustments
        >(globalVolume.profile, (val) => currentSettings.URP_ColorAdjustments = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.ColorCurves
        >(globalVolume.profile, (val) => currentSettings.URP_ColorCurves = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.ColorLookup
        >(globalVolume.profile, (val) => currentSettings.URP_ColorLookup = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.DepthOfField
        >(globalVolume.profile, (val) => currentSettings.URP_DepthOfField = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.LensDistortion
        >(globalVolume.profile, (val) => currentSettings.URP_LensDistortion = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.LiftGammaGain
        >(globalVolume.profile, (val) => currentSettings.URP_LiftGammaGain = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.PaniniProjection
        >(globalVolume.profile, (val) => currentSettings.URP_PaniniProjection = val);

#if UNITY_6000_0_OR_NEWER
        SyncVolumeOverride<
                    UnityEngine.Rendering.Universal.ScreenSpaceLensFlare
                >(globalVolume.profile, (val) => currentSettings.URP_ScreenSpaceLensFlare = val);
#endif

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.ShadowsMidtonesHighlights
        >(globalVolume.profile, (val) => currentSettings.URP_ShadowsMidtonesHighlights = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.SplitToning
        >(globalVolume.profile, (val) => currentSettings.URP_SplitToning = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.Tonemapping
        >(globalVolume.profile, (val) => currentSettings.URP_Tonemapping = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.Vignette
        >(globalVolume.profile, (val) => currentSettings.URP_Vignette = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.Universal.WhiteBalance
        >(globalVolume.profile, (val) => currentSettings.URP_WhiteBalance = val);

#elif REMEMBERME_HDRP_PRESENT
        // Each generic <T> fully qualified to HDRP’s namespace:
        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.MotionBlur
        >(globalVolume.profile, (val) => currentSettings.HDRP_MotionBlur = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.Bloom
        >(globalVolume.profile, (val) => currentSettings.HDRP_Bloom = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ChannelMixer
        >(globalVolume.profile, (val) => currentSettings.HDRP_ChannelMixer = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ChromaticAberration
        >(globalVolume.profile, (val) => currentSettings.HDRP_ChromaticAberration = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.FilmGrain
        >(globalVolume.profile, (val) => currentSettings.HDRP_FilmGrain = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ColorCurves
        >(globalVolume.profile, (val) => currentSettings.HDRP_ColorCurves = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ColorAdjustments
        >(globalVolume.profile, (val) => currentSettings.HDRP_ColorAdjustments = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.DepthOfField
        >(globalVolume.profile, (val) => currentSettings.HDRP_DepthOfField = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.LensDistortion
        >(globalVolume.profile, (val) => currentSettings.HDRP_LensDistortion = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.LiftGammaGain
        >(globalVolume.profile, (val) => currentSettings.HDRP_LiftGammaGain = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.PaniniProjection
        >(globalVolume.profile, (val) => currentSettings.HDRP_PaniniProjection = val);

#if UNITY_6000_0_OR_NEWER
        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ScreenSpaceLensFlare
        >(globalVolume.profile, (val) => currentSettings.HDRP_ScreenSpaceLensFlare = val);
#endif

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ShadowsMidtonesHighlights
        >(globalVolume.profile, (val) => currentSettings.HDRP_ShadowsMidtonesHighlights = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.SplitToning
        >(globalVolume.profile, (val) => currentSettings.HDRP_SplitToning = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.Tonemapping
        >(globalVolume.profile, (val) => currentSettings.HDRP_Tonemapping = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.Vignette
        >(globalVolume.profile, (val) => currentSettings.HDRP_Vignette = val);

        SyncVolumeOverride<
            UnityEngine.Rendering.HighDefinition.WhiteBalance
        >(globalVolume.profile, (val) => currentSettings.HDRP_WhiteBalance = val);

        SyncVolumeOverride<
            ScreenSpaceAmbientOcclusion
        >(globalVolume.profile, (val) =>
            currentSettings.HDRP_ScreenSpaceAmbientOcclusion = val
        );
#endif

                Logger.Log(
            "UserSettingsManager: Synced global Volume override states.",
            LogLevel.Info
        );
    }
#endif
}

		// --------------------------------------------------------
		// Public Reference-Setting Methods (optional helpers)
		// --------------------------------------------------------
		public void SetMainCameraReference(Camera newCamera)
		{
			if (newCamera == null)
			{
				Logger.Log("UserSettingsManager: Attempted to set mainCamera to null.", LogCategory.UserSettingsManager, LogLevel.Warning);
				return;
			}
			mainCamera = newCamera;
			Logger.Log($"UserSettingsManager: MainCamera reference set to '{newCamera.name}'.", LogCategory.UserSettingsManager, LogLevel.Info);
			if (!mainCamera.orthographic && currentSettings != null)
			{
				mainCamera.fieldOfView = currentSettings.CameraFOV;
				Logger.Log($"UserSettingsManager: Applied FOV {currentSettings.CameraFOV} to new mainCamera.", LogCategory.UserSettingsManager, LogLevel.Info);
			}
		}

#if UNITY_POST_PROCESSING_STACK_V2
/// <summary>
/// Allows you to assign a new PPv2 PostProcessVolume at runtime.
/// </summary>
public void SetPostProcessVolumeReference(PostProcessVolume newVolume)
{
    if (newVolume == null)
    {
        Logger.Log(
            "UserSettingsManager: Attempted to set builtinPostProcessVolume to null.",
            LogLevel.Warning
        );
        return;
    }

    builtinPostProcessVolume = newVolume;
    Logger.Log(
        $"UserSettingsManager: builtinPostProcessVolume reference set to '{newVolume.name}'.",
        LogLevel.Info
    );

    if (currentSettings != null)
    {
        // Use the correct "OverrideEnabled" field name:
        ApplyPostProcessingStackV2Overrides(currentSettings.MotionBlurOverrideEnabled);
    }
}
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
/// <summary>
/// Allows you to assign a new URP/HDRP global Volume at runtime.
/// Immediately applies whatever volume‐override flags are stored in currentSettings.
/// </summary>
public void SetGlobalVolumeReference(Volume newVolume)
{
    if (newVolume == null)
    {
        Logger.Log(
            "UserSettingsManager: Attempted to set globalVolume to null.",
            LogLevel.Warning
        );
        return;
    }

    globalVolume = newVolume;
    Logger.Log(
        $"UserSettingsManager: globalVolume reference set to '{newVolume.name}'.",
        LogLevel.Info
    );

#if REMEMBERME_URP_PRESENT
    if (globalVolume != null && globalVolume.profile != null)
    {
        // Fully‐qualified URP types to eliminate ambiguity:
        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.MotionBlur
        >(globalVolume.profile,
          currentSettings.URP_MotionBlur,
          "Motion Blur (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.Bloom
        >(globalVolume.profile,
          currentSettings.URP_Bloom,
          "Bloom (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.FilmGrain
        >(globalVolume.profile,
          currentSettings.URP_FilmGrain,
          "Film Grain (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.ChannelMixer
        >(globalVolume.profile,
          currentSettings.URP_ChannelMixer,
          "Channel Mixer (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.ChromaticAberration
        >(globalVolume.profile,
          currentSettings.URP_ChromaticAberration,
          "Chromatic Aberration (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.ColorAdjustments
        >(globalVolume.profile,
          currentSettings.URP_ColorAdjustments,
          "Color Adjustments (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.ColorCurves
        >(globalVolume.profile,
          currentSettings.URP_ColorCurves,
          "Color Curves (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.ColorLookup
        >(globalVolume.profile,
          currentSettings.URP_ColorLookup,
          "Color Lookup (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.DepthOfField
        >(globalVolume.profile,
          currentSettings.URP_DepthOfField,
          "Depth Of Field (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.LensDistortion
        >(globalVolume.profile,
          currentSettings.URP_LensDistortion,
          "Lens Distortion (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.LiftGammaGain
        >(globalVolume.profile,
          currentSettings.URP_LiftGammaGain,
          "Lift Gamma Gain (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.PaniniProjection
        >(globalVolume.profile,
          currentSettings.URP_PaniniProjection,
          "Panini Projection (URP)");

#if UNITY_6000_0_OR_NEWER
        ApplyVolumeOverride<
                    UnityEngine.Rendering.Universal.ScreenSpaceLensFlare
                >(globalVolume.profile,
          currentSettings.URP_ScreenSpaceLensFlare,
          "Screen Space Lens Flare (URP)");
#endif

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.ShadowsMidtonesHighlights
        >(globalVolume.profile,
          currentSettings.URP_ShadowsMidtonesHighlights,
          "Shadows Midtones Highlights (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.SplitToning
        >(globalVolume.profile,
          currentSettings.URP_SplitToning,
          "Split Toning (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.Tonemapping
        >(globalVolume.profile,
          currentSettings.URP_Tonemapping,
          "Tone Mapping (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.Vignette
        >(globalVolume.profile,
          currentSettings.URP_Vignette,
          "Vignette (URP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.Universal.WhiteBalance
        >(globalVolume.profile,
          currentSettings.URP_WhiteBalance,
          "White Balance (URP)");
    }
#elif REMEMBERME_HDRP_PRESENT
    if (globalVolume != null && globalVolume.profile != null)
    {
        // Fully‐qualified HDRP types to eliminate ambiguity:
        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.MotionBlur
        >(globalVolume.profile,
          currentSettings.HDRP_MotionBlur,
          "Motion Blur (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.Bloom
        >(globalVolume.profile,
          currentSettings.HDRP_Bloom,
          "Bloom (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ChannelMixer
        >(globalVolume.profile,
          currentSettings.HDRP_ChannelMixer,
          "Channel Mixer (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ChromaticAberration
        >(globalVolume.profile,
          currentSettings.HDRP_ChromaticAberration,
          "Chromatic Aberration (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.FilmGrain
        >(globalVolume.profile,
          currentSettings.HDRP_FilmGrain,
          "Film Grain (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ColorCurves
        >(globalVolume.profile,
          currentSettings.HDRP_ColorCurves,
          "Color Curves (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ColorAdjustments
        >(globalVolume.profile,
          currentSettings.HDRP_ColorAdjustments,
          "Color Lookup (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.DepthOfField
        >(globalVolume.profile,
          currentSettings.HDRP_DepthOfField,
          "Depth Of Field (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.LensDistortion
        >(globalVolume.profile,
          currentSettings.HDRP_LensDistortion,
          "Lens Distortion (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.LiftGammaGain
        >(globalVolume.profile,
          currentSettings.HDRP_LiftGammaGain,
          "Lift Gamma Gain (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.PaniniProjection
        >(globalVolume.profile,
          currentSettings.HDRP_PaniniProjection,
          "Panini Projection (HDRP)");

#if UNITY_6000_0_OR_NEWER
        ApplyVolumeOverride<
                    UnityEngine.Rendering.HighDefinition.ScreenSpaceLensFlare
                >(globalVolume.profile,
          currentSettings.HDRP_ScreenSpaceLensFlare,
          "Screen Space Lens Flare (HDRP)");
#endif

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.ShadowsMidtonesHighlights
        >(globalVolume.profile,
          currentSettings.HDRP_ShadowsMidtonesHighlights,
          "Shadows Midtones Highlights (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.SplitToning
        >(globalVolume.profile,
          currentSettings.HDRP_SplitToning,
          "Split Toning (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.Tonemapping
        >(globalVolume.profile,
          currentSettings.HDRP_Tonemapping,
          "Tone Mapping (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.Vignette
        >(globalVolume.profile,
          currentSettings.HDRP_Vignette,
          "Vignette (HDRP)");

        ApplyVolumeOverride<
            UnityEngine.Rendering.HighDefinition.WhiteBalance
        >(globalVolume.profile,
          currentSettings.HDRP_WhiteBalance,
          "White Balance (HDRP)");
    }
#endif
}
#endif


		public void SetAudioMixerReference(AudioMixer newMixer)
		{
			if (newMixer == null)
			{
				Logger.Log("UserSettingsManager: Attempted to set audioMixer to null.", LogCategory.UserSettingsManager, LogLevel.Warning);
				return;
			}
			audioMixer = newMixer;
			Logger.Log($"UserSettingsManager: audioMixer reference set to '{newMixer.name}'.", LogCategory.UserSettingsManager, LogLevel.Info);
			if (currentSettings != null)
			{
				ApplyAudioMixerSettings();
			}
		}

		public void UpdateResolution(int width, int height, bool fullScreen)
		{
			currentSettings.ResolutionWidth = width;
			currentSettings.ResolutionHeight = height;
			currentSettings.FullScreen = fullScreen;
			Screen.SetResolution(width, height, fullScreen);
			Logger.Log($"UserSettingsManager: Resolution set to {width}x{height}, FullScreen={fullScreen}", LogCategory.UserSettingsManager, LogLevel.Info);
			//SaveSettings();
		}
		
		/// <summary>
		/// Updates the resolution with a specific fullscreen mode.
		/// </summary>
		/// <param name="width">Screen width in pixels</param>
		/// <param name="height">Screen height in pixels</param>
		/// <param name="mode">The fullscreen mode to use</param>
		public void UpdateResolution(int width, int height, FullScreenMode mode)
		{
			currentSettings.ResolutionWidth = width;
			currentSettings.ResolutionHeight = height;
			currentSettings.FullScreen = (mode != FullScreenMode.Windowed);
			currentSettings.FullScreenModeValue = (int)mode;
			Screen.SetResolution(width, height, mode);
			Logger.Log($"UserSettingsManager: Resolution set to {width}x{height}, Mode={mode}", LogCategory.UserSettingsManager, LogLevel.Info);
		}
		
		/// <summary>
		/// Sets the fullscreen mode without changing resolution.
		/// </summary>
		/// <param name="mode">The fullscreen mode to use</param>
		public void SetFullScreenMode(FullScreenMode mode)
		{
			currentSettings.FullScreen = (mode != FullScreenMode.Windowed);
			currentSettings.FullScreenModeValue = (int)mode;
			Screen.fullScreenMode = mode;
			Logger.Log($"UserSettingsManager: FullScreenMode set to {mode}", LogCategory.UserSettingsManager, LogLevel.Info);
		}
		
		/// <summary>
		/// Sets the window to borderless fullscreen (FullScreenWindow mode).
		/// This is the default "borderless window" mode.
		/// </summary>
		public void SetBorderlessWindow()
		{
			SetFullScreenMode(FullScreenMode.FullScreenWindow);
		}
		
		/// <summary>
		/// Sets the window to exclusive fullscreen mode.
		/// This gives the application exclusive control of the display.
		/// </summary>
		public void SetExclusiveFullScreen()
		{
			SetFullScreenMode(FullScreenMode.ExclusiveFullScreen);
		}
		
		/// <summary>
		/// Sets the window to windowed mode (with borders).
		/// </summary>
		public void SetWindowed()
		{
			SetFullScreenMode(FullScreenMode.Windowed);
		}
		
		/// <summary>
		/// Sets the window to maximized window mode.
		/// </summary>
		public void SetMaximizedWindow()
		{
			SetFullScreenMode(FullScreenMode.MaximizedWindow);
		}
		
		/// <summary>
		/// Gets the current fullscreen mode.
		/// </summary>
		public FullScreenMode GetFullScreenMode()
		{
			return Screen.fullScreenMode;
		}
		
		/// <summary>
		/// Gets the saved fullscreen mode from settings.
		/// </summary>
		public FullScreenMode GetSavedFullScreenMode()
		{
			return (FullScreenMode)currentSettings.FullScreenModeValue;
		}
		
		/// <summary>
		/// Sets the screen resolution to a specific aspect ratio.
		/// Common ratios: 16:9, 16:10, 21:9, 4:3
		/// </summary>
		/// <param name="aspectWidth">Width component of aspect ratio (e.g., 16 for 16:9)</param>
		/// <param name="aspectHeight">Height component of aspect ratio (e.g., 9 for 16:9)</param>
		/// <param name="targetHeight">Target height in pixels (width calculated from aspect ratio)</param>
		/// <param name="fullScreen">Whether to use fullscreen mode</param>
		public void SetAspectRatio(int aspectWidth, int aspectHeight, int targetHeight, bool fullScreen)
		{
			int targetWidth = Mathf.RoundToInt((float)targetHeight * aspectWidth / aspectHeight);
			UpdateResolution(targetWidth, targetHeight, fullScreen);
		}
		
		/// <summary>
		/// Sets the screen resolution to a predefined aspect ratio.
		/// </summary>
		/// <param name="ratio">The aspect ratio preset</param>
		/// <param name="targetHeight">Target height in pixels</param>
		/// <param name="fullScreen">Whether to use fullscreen mode</param>
		public void SetAspectRatio(AspectRatioPreset ratio, int targetHeight, bool fullScreen)
		{
			int targetWidth;
			switch (ratio)
			{
				case AspectRatioPreset.Ratio_4_3:
					targetWidth = Mathf.RoundToInt(targetHeight * 4f / 3f);
					break;
				case AspectRatioPreset.Ratio_16_10:
					targetWidth = Mathf.RoundToInt(targetHeight * 16f / 10f);
					break;
				case AspectRatioPreset.Ratio_21_9:
					targetWidth = Mathf.RoundToInt(targetHeight * 21f / 9f);
					break;
				case AspectRatioPreset.Ratio_32_9:
					targetWidth = Mathf.RoundToInt(targetHeight * 32f / 9f);
					break;
				case AspectRatioPreset.Ratio_16_9:
				default:
					targetWidth = Mathf.RoundToInt(targetHeight * 16f / 9f);
					break;
			}
			UpdateResolution(targetWidth, targetHeight, fullScreen);
		}
		
		/// <summary>
		/// Gets the current aspect ratio as width:height (e.g., "16:9").
		/// </summary>
		public string GetCurrentAspectRatioString()
		{
			int width = currentSettings.ResolutionWidth;
			int height = currentSettings.ResolutionHeight;
			int gcd = GCD(width, height);
			return $"{width / gcd}:{height / gcd}";
		}
		
		/// <summary>
		/// Gets the current aspect ratio as a float (width / height).
		/// </summary>
		public float GetCurrentAspectRatio()
		{
			return (float)currentSettings.ResolutionWidth / currentSettings.ResolutionHeight;
		}
		
		private static int GCD(int a, int b)
		{
			while (b != 0)
			{
				int temp = b;
				b = a % b;
				a = temp;
			}
			return a;
		}

		public void UpdateVSync(bool enable)
		{
			currentSettings.QualityVSyncCount = enable ? 1 : 0;
			//SaveSettings();
		}

		public void UpdateTargetFrameRate(int fps)
		{
			currentSettings.TargetFPS = fps; // Add a corresponding field in UserSettingsData.
											 //SaveSettings();
		}

		// --------------------------------------------------------
		// Private Methods (Camera & Volume References)
		// --------------------------------------------------------
		private void UpdateMainCameraReference()
		{
			if (autoReferenceMainCamera && mainCamera == null)
			{
				mainCamera = Camera.main;
				if (mainCamera != null)
					Logger.Log($"UserSettingsManager: Updated mainCamera to '{mainCamera.name}'.", LogCategory.UserSettingsManager, LogLevel.Info);
				else
					Logger.Log("UserSettingsManager: No main camera found in the scene.", LogCategory.UserSettingsManager, LogLevel.Warning);
			}
		}

#if UNITY_POST_PROCESSING_STACK_V2
        private void FindPostProcessVolume()
        {
            if (autoReferencePostProcessVolume && builtinPostProcessVolume == null)
            {
                builtinPostProcessVolume = FindFirstObjectByType<PostProcessVolume>();
                if (builtinPostProcessVolume != null)
                    Logger.Log($"UserSettingsManager: Found PostProcessVolume '{builtinPostProcessVolume.name}'.", LogCategory.UserSettingsManager, LogLevel.Info);
                else
                    Logger.Log("UserSettingsManager: No PostProcessVolume found in the scene.", LogCategory.UserSettingsManager, LogLevel.Warning);
            }
        }
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
		private void FindGlobalVolume()
		{
			if (autoReferenceGlobalVolume && globalVolume == null)
			{
#pragma warning disable CS0618 // Suppress FindFirstObjectByType deprecation warning for cross-version compatibility
				globalVolume = FindFirstObjectByType<Volume>();
#pragma warning restore CS0618
				if (globalVolume != null)
					Logger.Log($"UserSettingsManager: Found Global Volume '{globalVolume.name}'.", LogCategory.UserSettingsManager, LogLevel.Info);
				else
					Logger.Log("UserSettingsManager: No Global Volume found in the scene.", LogCategory.UserSettingsManager, LogLevel.Warning);
			}
		}
#endif

		// --------------------------------------------------------
		// Private Methods (Graphics Settings)
		// --------------------------------------------------------
		private void ApplyShadowQuality(int preset)
		{
			switch (preset)
			{
				case 0:
					QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
					break;
				case 1:
					QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
					break;
				case 2:
					QualitySettings.shadows = UnityEngine.ShadowQuality.All;
#if REMEMBERME_HDRP_PRESENT
            QualitySettings.shadowResolution = ShadowResolution.Medium;
#elif REMEMBERME_URP_PRESENT
            QualitySettings.shadowResolution = (UnityEngine.ShadowResolution)UnityEngine.Rendering.Universal.ShadowResolution._1024;
#endif
					break;
				case 3:
					QualitySettings.shadows = UnityEngine.ShadowQuality.All;
#if REMEMBERME_HDRP_PRESENT
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
#elif REMEMBERME_URP_PRESENT
            QualitySettings.shadowResolution = (UnityEngine.ShadowResolution)UnityEngine.Rendering.Universal.ShadowResolution._2048;
#else
					// Built-in render pipeline fallback: set resolution to very high.
					QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
#endif
					break;
				default:
					QualitySettings.shadows = UnityEngine.ShadowQuality.All;
#if REMEMBERME_HDRP_PRESENT
            QualitySettings.shadowResolution = ShadowResolution.High;
#elif REMEMBERME_URP_PRESENT
            QualitySettings.shadowResolution = (UnityEngine.ShadowResolution)UnityEngine.Rendering.Universal.ShadowResolution._4096;
#endif
					break;
			}
		}

// --------------------------------------------------------
// Private Methods (Built-In Pipeline / PPv2)
// --------------------------------------------------------
private void ApplyPostProcessingStackV2Overrides(bool enabled)
{
#if UNITY_POST_PROCESSING_STACK_V2
    // NOTE: Every type is fully qualified as
    // UnityEngine.Rendering.PostProcessing to avoid ambiguity.

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.MotionBlur
    >(currentSettings.MotionBlurOverrideEnabled, "Motion Blur");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.DepthOfField
    >(currentSettings.DepthOfFieldOverrideEnabled, "Depth of Field");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.Bloom
    >(currentSettings.BloomOverrideEnabled, "Bloom");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.LensDistortion
    >(currentSettings.LensDistortionOverrideEnabled, "Lens Distortion");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.ChromaticAberration
    >(currentSettings.ChromaticAberrationOverrideEnabled, "Chromatic Aberration");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.AutoExposure
    >(currentSettings.AutoExposureOverrideEnabled, "Auto Exposure");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.ColorGrading
    >(currentSettings.ColorGradingOverrideEnabled, "Color Grading");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.Vignette
    >(currentSettings.VignetteOverrideEnabled, "Vignette");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.Grain
    >(currentSettings.GrainOverrideEnabled, "Grain");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.ScreenSpaceReflections
    >(currentSettings.ScreenSpaceReflectionsOverrideEnabled, "Screen Space Reflections");

    ApplyPPv2Override<
        UnityEngine.Rendering.PostProcessing.AmbientOcclusion
    >(currentSettings.AmbientOcclusionOverrideEnabled, "Ambient Occlusion");
#else
    Logger.Log($"[Built-in pipeline] PPv2 not present. Can't apply overrides.", LogCategory.UserSettingsManager, LogLevel.Info);
#endif
}

#if UNITY_POST_PROCESSING_STACK_V2
/// <summary>
/// Toggle a specific PPv2 effect on/off inside a PostProcessVolume.
/// T must be a PostProcessEffectSettings from UnityEngine.Rendering.PostProcessing.
/// </summary>
private void ApplyPPv2Override<T>(bool enable, string effectName)
    where T : UnityEngine.Rendering.PostProcessing.PostProcessEffectSettings
{
    if (builtinPostProcessVolume == null || builtinPostProcessVolume.profile == null)
    {
        Logger.Log(
            $"[PPv2] No PostProcessVolume/profile found. Can't toggle {effectName}.",
            LogLevel.Warning
        );
        return;
    }

    var effect = builtinPostProcessVolume.profile.GetSetting<T>();
    if (effect != null)
    {
        effect.enabled.Override(enable);
        Logger.Log($"[PPv2] {effectName} => {(enable ? "Enabled" : "Disabled")}", LogCategory.UserSettingsManager, LogLevel.Info);
    }
    else
    {
        Logger.Log($"[PPv2] No {effectName} effect in profile.", LogCategory.UserSettingsManager, LogLevel.Info);
    }
}
#endif


		// --------------------------------------------------------
		// Private Methods (URP & HDRP)
		// --------------------------------------------------------
#if REMEMBERME_URP_PRESENT
        private void ApplyURPAntiAliasing(int aaLevel)
        {
            var urpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                Logger.Log("[URP] No UniversalRenderPipelineAsset found. Can't apply AA.", LogCategory.UserSettingsManager, LogLevel.Warning);
                return;
            }
            int clamped = (aaLevel == 2 || aaLevel == 4 || aaLevel == 8) ? aaLevel : 0;
            urpAsset.msaaSampleCount = clamped;
            Logger.Log($"[URP] MSAA set to {clamped}x.", LogCategory.UserSettingsManager, LogLevel.Info);
        }
#endif

		// --------------------------------------------------------
		// Private Methods (Audio Volume Conversions)
		// --------------------------------------------------------
		private float LinearToDecibels(float linearVolume)
		{
			if (linearVolume <= 0f) return -80f;
			return 20f * Mathf.Log10(linearVolume);
		}

		private float DecibelsToLinear(float decibels)
		{
			if (decibels <= -80f) return 0f;
			return Mathf.Pow(10f, decibels / 20f);
		}

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
		// Generic helper methods for Volume-based overrides
		private void SyncVolumeOverride<T>(VolumeProfile profile, System.Action<bool> updateAction) where T : VolumeComponent
		{
			if (profile.TryGet<T>(out T component))
			{
				updateAction(component.active);
			}
		}

		private void ApplyVolumeOverride<T>(VolumeProfile profile, bool enabled, string effectName) where T : VolumeComponent
		{
			if (profile.TryGet<T>(out T component))
			{
				component.active = enabled;
				Logger.Log($"[Volume] {effectName} => {(enabled ? "Enabled" : "Disabled")}", LogCategory.UserSettingsManager, LogLevel.Info);
			}
			else
			{
				Logger.Log($"[Volume] No {effectName} override found in Volume profile.", LogCategory.UserSettingsManager, LogLevel.Info);
			}
		}
#endif

		// --------------------------------------------------------
		// Optional Public Getter for HDRP ScreenSpaceAmbientOcclusion
		// --------------------------------------------------------
		public bool HDRP_ScreenSpaceAmbientOcclusion
		{
			get { return currentSettings != null ? currentSettings.HDRP_ScreenSpaceAmbientOcclusion : true; }
		}

		// PUBLIC GETTERS FOR USER SETTINGS DATA
		#region Public Getters

		public int ResolutionWidth
		{
			get { return currentSettings != null ? currentSettings.ResolutionWidth : 1920; }
		}

		public int ResolutionHeight
		{
			get { return currentSettings != null ? currentSettings.ResolutionHeight : 1080; }
		}

		public bool FullScreen
		{
			get { return currentSettings != null ? currentSettings.FullScreen : true; }
		}

		public int AntiAliasing
		{
			get { return currentSettings != null ? currentSettings.AntiAliasing : 0; }
		}

		public int QualityAntiAliasing
		{
			get { return currentSettings != null ? currentSettings.QualityAntiAliasing : 0; }
		}

		public float QualityShadowDistance
		{
			get { return currentSettings != null ? currentSettings.QualityShadowDistance : 150f; }
		}

		public int QualityVSyncCount
		{
			get { return currentSettings != null ? currentSettings.QualityVSyncCount : 1; }
		}

		public int ShadowQuality
		{
			get { return currentSettings != null ? currentSettings.ShadowQuality : 2; }
		}

		public int TextureQuality
		{
			get { return currentSettings != null ? currentSettings.TextureQuality : 0; }
		}

		public int TargetFPS
		{
			get { return currentSettings != null ? currentSettings.TargetFPS : -1; }
		}

		public string LocaleCode
		{
			get { return currentSettings != null ? currentSettings.LocaleCode : "en"; }
		}

		// URP Volume Overrides
		public bool URP_AmbientOcclusion
		{
			get { return currentSettings != null ? currentSettings.URP_AmbientOcclusion : true; }
		}
		public bool URP_MotionBlur
		{
			get { return currentSettings != null ? currentSettings.URP_MotionBlur : true; }
		}
		public bool URP_Bloom
		{
			get { return currentSettings != null ? currentSettings.URP_Bloom : true; }
		}
		public bool URP_FilmGrain
		{
			get { return currentSettings != null ? currentSettings.URP_FilmGrain : true; }
		}
		public bool URP_ChannelMixer
		{
			get { return currentSettings != null ? currentSettings.URP_ChannelMixer : true; }
		}
		public bool URP_ChromaticAberration
		{
			get { return currentSettings != null ? currentSettings.URP_ChromaticAberration : true; }
		}
		public bool URP_ColorAdjustments
		{
			get { return currentSettings != null ? currentSettings.URP_ColorAdjustments : true; }
		}
		public bool URP_ColorCurves
		{
			get { return currentSettings != null ? currentSettings.URP_ColorCurves : true; }
		}
		public bool URP_ColorLookup
		{
			get { return currentSettings != null ? currentSettings.URP_ColorLookup : true; }
		}
		public bool URP_DepthOfField
		{
			get { return currentSettings != null ? currentSettings.URP_DepthOfField : true; }
		}
		public bool URP_LensDistortion
		{
			get { return currentSettings != null ? currentSettings.URP_LensDistortion : true; }
		}
		public bool URP_LiftGammaGain
		{
			get { return currentSettings != null ? currentSettings.URP_LiftGammaGain : true; }
		}
		public bool URP_PaniniProjection
		{
			get { return currentSettings != null ? currentSettings.URP_PaniniProjection : true; }
		}
		public bool URP_ScreenSpaceLensFlare
		{
			get { return currentSettings != null ? currentSettings.URP_ScreenSpaceLensFlare : true; }
		}
		public bool URP_ShadowsMidtonesHighlights
		{
			get { return currentSettings != null ? currentSettings.URP_ShadowsMidtonesHighlights : true; }
		}
		public bool URP_SplitToning
		{
			get { return currentSettings != null ? currentSettings.URP_SplitToning : true; }
		}
		public bool URP_Tonemapping
		{
			get { return currentSettings != null ? currentSettings.URP_Tonemapping : true; }
		}
		public bool URP_Vignette
		{
			get { return currentSettings != null ? currentSettings.URP_Vignette : true; }
		}
		public bool URP_WhiteBalance
		{
			get { return currentSettings != null ? currentSettings.URP_WhiteBalance : true; }
		}

		// HDRP Volume Overrides
		public bool HDRP_AmbientOcclusion
		{
			get { return currentSettings != null ? currentSettings.HDRP_AmbientOcclusion : true; }
		}
		public bool HDRP_MotionBlur
		{
			get { return currentSettings != null ? currentSettings.HDRP_MotionBlur : true; }
		}
		public bool HDRP_Bloom
		{
			get { return currentSettings != null ? currentSettings.HDRP_Bloom : true; }
		}
		public bool HDRP_FilmGrain
		{
			get { return currentSettings != null ? currentSettings.HDRP_FilmGrain : true; }
		}
		public bool HDRP_ChannelMixer
		{
			get { return currentSettings != null ? currentSettings.HDRP_ChannelMixer : true; }
		}
		public bool HDRP_ChromaticAberration
		{
			get { return currentSettings != null ? currentSettings.HDRP_ChromaticAberration : true; }
		}
		public bool HDRP_ColorCurves
		{
			get { return currentSettings != null ? currentSettings.HDRP_ColorCurves : true; }
		}
		public bool HDRP_ColorAdjustments
		{
			get { return currentSettings != null ? currentSettings.HDRP_ColorAdjustments : true; }
		}
		public bool HDRP_DepthOfField
		{
			get { return currentSettings != null ? currentSettings.HDRP_DepthOfField : true; }
		}
		public bool HDRP_LensDistortion
		{
			get { return currentSettings != null ? currentSettings.HDRP_LensDistortion : true; }
		}
		public bool HDRP_LiftGammaGain
		{
			get { return currentSettings != null ? currentSettings.HDRP_LiftGammaGain : true; }
		}
		public bool HDRP_PaniniProjection
		{
			get { return currentSettings != null ? currentSettings.HDRP_PaniniProjection : true; }
		}
		public bool HDRP_ScreenSpaceLensFlare
		{
			get { return currentSettings != null ? currentSettings.HDRP_ScreenSpaceLensFlare : true; }
		}
		public bool HDRP_ShadowsMidtonesHighlights
		{
			get { return currentSettings != null ? currentSettings.HDRP_ShadowsMidtonesHighlights : true; }
		}
		public bool HDRP_SplitToning
		{
			get { return currentSettings != null ? currentSettings.HDRP_SplitToning : true; }
		}
		public bool HDRP_Tonemapping
		{
			get { return currentSettings != null ? currentSettings.HDRP_Tonemapping : true; }
		}
		public bool HDRP_Vignette
		{
			get { return currentSettings != null ? currentSettings.HDRP_Vignette : true; }
		}
		public bool HDRP_WhiteBalance
		{
			get { return currentSettings != null ? currentSettings.HDRP_WhiteBalance : true; }
		}

		// Audio Settings
		public float MasterVolume
		{
			get { return currentSettings != null ? currentSettings.MasterVolume : 1.0f; }
		}
		public float MusicVolume
		{
			get { return currentSettings != null ? currentSettings.MusicVolume : 0.8f; }
		}
		public float SfxVolume
		{
			get { return currentSettings != null ? currentSettings.SfxVolume : 0.8f; }
		}
		public float VoiceVolume
		{
			get { return currentSettings != null ? currentSettings.VoiceVolume : 0.8f; }
		}

		// Camera FOV
		public float CameraFOV
		{
			get { return currentSettings != null ? currentSettings.CameraFOV : 60f; }
		}

		// Main Camera HDRP Settings
		public bool MainCamera_DynamicResolutionEnabled
		{
			get { return currentSettings != null ? currentSettings.MainCamera_DynamicResolutionEnabled : false; }
		}
		public bool MainCamera_AllowDeepLearningSuperSampling
		{
			get { return currentSettings != null ? currentSettings.MainCamera_AllowDeepLearningSuperSampling : true; }
		}
		public bool MainCamera_DeepLearningSuperSamplingUseCustomQualitySettings
		{
			get { return currentSettings != null ? currentSettings.MainCamera_DeepLearningSuperSamplingUseCustomQualitySettings : false; }
		}
#if REMEMBERME_NVIDIA_DLSS_PRESENT && REMEMBERME_HDRP_PRESENT
		public uint MainCamera_DeepLearningSuperSamplingQuality
		{
			get { return currentSettings != null ? currentSettings.MainCamera_DeepLearningSuperSamplingQuality : (uint)DLSSQuality.Balanced; }
		}
#endif
		public bool MainCamera_AllowFidelityFX2SuperResolution
		{
			get { return currentSettings != null ? currentSettings.MainCamera_AllowFidelityFX2SuperResolution : true; }
		}
#if REMEMBERME_HDRP_PRESENT
		public HDAdditionalCameraData.AntialiasingMode MainCamera_Antialiasing
		{
			get { return currentSettings != null ? currentSettings.MainCamera_Antialiasing : HDAdditionalCameraData.AntialiasingMode.None; }
		}
#endif

		// Main Camera URP Settings
#if REMEMBERME_URP_PRESENT
        public UnityEngine.Rendering.Universal.AntialiasingMode MainCamera_UrpAntialiasing
        {
            get { return currentSettings != null ? currentSettings.MainCamera_UrpAntialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None; }
        }
#endif

		// Post-Processing Stack v2 Overrides
		public bool MotionBlurOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.MotionBlurOverrideEnabled : true; }
		}
		public bool DepthOfFieldOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.DepthOfFieldOverrideEnabled : true; }
		}
		public bool BloomOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.BloomOverrideEnabled : true; }
		}
		public bool LensDistortionOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.LensDistortionOverrideEnabled : true; }
		}
		public bool ChromaticAberrationOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.ChromaticAberrationOverrideEnabled : true; }
		}
		public bool AutoExposureOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.AutoExposureOverrideEnabled : true; }
		}
		public bool ColorGradingOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.ColorGradingOverrideEnabled : true; }
		}
		public bool VignetteOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.VignetteOverrideEnabled : true; }
		}
		public bool GrainOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.GrainOverrideEnabled : true; }
		}
		public bool ScreenSpaceReflectionsOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.ScreenSpaceReflectionsOverrideEnabled : true; }
		}
		public bool AmbientOcclusionOverrideEnabled
		{
			get { return currentSettings != null ? currentSettings.AmbientOcclusionOverrideEnabled : true; }
		}
        public UserSettingsData CurrentSettings
        {
                get { return currentSettings; }
        }

                #endregion
        }
        
    /// <summary>
    /// Common aspect ratio presets for screen resolution.
    /// </summary>
    public enum AspectRatioPreset
    {
        /// <summary>4:3 - Traditional/older monitors</summary>
        Ratio_4_3,
        /// <summary>16:9 - Standard widescreen (1080p, 1440p, 4K)</summary>
        Ratio_16_9,
        /// <summary>16:10 - Common laptop/workstation ratio</summary>
        Ratio_16_10,
        /// <summary>21:9 - Ultrawide monitors</summary>
        Ratio_21_9,
        /// <summary>32:9 - Super ultrawide monitors</summary>
        Ratio_32_9
    }
}
#endif
