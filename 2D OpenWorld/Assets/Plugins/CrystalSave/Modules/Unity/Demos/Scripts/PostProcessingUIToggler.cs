#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Audio;

#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering;
#endif

#if REMEMBERME_URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

#if REMEMBERME_HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Arawn.CrystalSave.Runtime.UI
{
	public class PostProcessingUIToggler : MonoBehaviour
	{
		[SerializeField] private GameObject targetVolume;
		[SerializeField] private Canvas uiCanvas;
		[SerializeField] private float elementSpacing = 50f;

		private List<Toggle> toggles = new List<Toggle>();
		private Dictionary<PostProcessingOverride, bool> overrideStates = new Dictionary<PostProcessingOverride, bool>();
		private Camera mainCamera;

		// -- AUDIO SLIDERS --
		private Slider masterSlider;
		private Slider musicSlider;
		private Slider sfxSlider;
		private Slider voiceSlider;

		public enum PostProcessingOverride
		{
#if REMEMBERME_HDRP_PRESENT || UNITY_POST_PROCESSING_STACK_V2
			AmbientOcclusion,
#endif
			AutoExposure,
			Bloom,
			ChannelMixer,
			ChromaticAberration,
			ColorAdjustments,
			DepthOfField,
			LensDistortion,
			LiftGammaGain,
			MotionBlur,
			Tonemapping,
			Vignette,
			WhiteBalance,
			ColorCurves,
			FilmGrain,
			PaniniProjection,
			ScreenSpaceLensFlare,
			ShadowsMidtonesHighlights,
			SplitToning
		}

		public enum FPSLimit
		{
			Unlimited,
			FPS30 = 30,
			FPS60 = 60,
			FPS120 = 120,
			FPS144 = 144,
			FPS240 = 240
		}

		public enum ResolutionOptions
		{
			R800x600,
			R1024x768,
			R1280x960,
			R1600x1200,
			R1366x768,
			R1920x1080,
			R2560x1440,
			R3440x1440,
			R3840x2160,
			R5120x1440
		}

		public enum UpscalerOption
		{
			DLSS,
			FSR2
		}

		[Header("Audio Mixer")]
		[SerializeField] private AudioMixer audioMixer;

		// Exposed parameter names
		[SerializeField] private string MasterVolumeParam = "Master";
		[SerializeField] private string MusicVolumeParam = "Music";
		[SerializeField] private string SfxVolumeParam = "SFX";
		[SerializeField] private string VoiceVolumeParam = "Voice";

		private readonly Color sciFiBackground = new Color(0.1f, 0.1f, 0.15f);
		private readonly Color sciFiText = new Color(0.7f, 0.9f, 1f);
		private readonly Color sciFiAccent = new Color(0.4f, 0.2f, 0.8f);
		private readonly Color sciFiInactive = new Color(0.4f, 0.4f, 0.45f);
		private readonly Color buttonClickColor = new Color(0.2f, 0.1f, 0.4f);

		void Start()
		{
			if (uiCanvas == null)
			{
				Debug.LogError("UI Canvas reference is not set!");
				return;
			}

			if (targetVolume == null)
			{
				FindTargetVolumeInScene();
			}

			mainCamera = Camera.main;
			if (mainCamera == null)
			{
				Debug.LogWarning("Main Camera not found in the scene.");
			}

			// 1) Build all UI elements (including volume sliders).
			GenerateUI();

			// 2) Now that the sliders exist, sync them with the manager�s current volumes (if available).
			SyncSlidersWithManager();
		}

		private void SyncSlidersWithManager()
		{
			// If the manager or its settings aren�t loaded yet, skip.
			if (UserSettingsManager.Instance == null) return;

			// The manager stores volumes in a [0..1] range. Just apply them:
			masterSlider.SetValueWithoutNotify(UserSettingsManager.Instance.MasterVolume);
			musicSlider.SetValueWithoutNotify(UserSettingsManager.Instance.MusicVolume);
			sfxSlider.SetValueWithoutNotify(UserSettingsManager.Instance.SfxVolume);
			voiceSlider.SetValueWithoutNotify(UserSettingsManager.Instance.VoiceVolume);

			// This ensures the slider handles visually match the actual volume
			// that was loaded from disk at Awake().
		}

        private void FindTargetVolumeInScene()
        {
            var currentPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            bool isURP = currentPipeline != null && currentPipeline.GetType().Name.Contains("Universal");
            bool isHDRP = currentPipeline != null && currentPipeline.GetType().Name.Contains("HighDefinition");

    #if REMEMBERME_HDRP_PRESENT
            if (isHDRP)
            {
                Volume volume = Object.FindFirstObjectByType<Volume>();
                if (volume != null && volume.profile != null)
                {
                    targetVolume = volume.gameObject;
                    Debug.Log($"Found HDRP Volume on {targetVolume.name}");
                    return;
                }
            }
    #endif

    #if REMEMBERME_URP_PRESENT
            if (isURP)
            {
#pragma warning disable CS0618 // Suppress FindFirstObjectByType deprecation warning for cross-version compatibility
                Volume volume = Object.FindFirstObjectByType<Volume>();
#pragma warning restore CS0618
                if (volume != null && volume.profile != null)
                {
                    targetVolume = volume.gameObject;
                    Debug.Log($"Found URP Volume on {targetVolume.name}");
                    return;
                }
            }
    #endif

    #if UNITY_POST_PROCESSING_STACK_V2 && !REMEMBERME_URP_PRESENT && !REMEMBERME_HDRP_PRESENT
            // Fallback for PPv2 if no URP/HDRP is present:
            PostProcessVolume ppVolume = Object.FindFirstObjectByType<PostProcessVolume>();
            if (ppVolume != null && ppVolume.profile != null)
            {
                targetVolume = ppVolume.gameObject;
                Debug.Log($"Found PPv2 PostProcessVolume on {targetVolume.name}");
                return;
            }
    #endif

            Debug.LogWarning("No post-processing volume of any supported type found in scene.");
        }

		private void GenerateUI()
		{
			GameObject uiPanel = CreateGradientPanel("UI Panel", new Vector2(0, 0), new Vector2(Screen.width, Screen.height), sciFiBackground);
			uiPanel.transform.SetParent(uiCanvas.transform, false);
			RectTransform uiPanelRect = uiPanel.GetComponent<RectTransform>();
			uiPanelRect.anchorMin = new Vector2(0, 0);
			uiPanelRect.anchorMax = new Vector2(1, 1);
			uiPanelRect.sizeDelta = Vector2.zero;

			//float sectionWidth = 480f;
			Vector2 leftSectionPos = new Vector2(0.15f * 1920f, -50f);  // or just 240, etc.
			Vector2 centerSectionPos = new Vector2(0.45f * 1920f, -50f);  // ~960 in reference space
			Vector2 rightSectionPos = new Vector2(0.75f * 1920f, -50f);  // ~1440 in reference space

			float yOffset = -50;

			// Post Processing
			if (targetVolume != null)
			{
				yOffset = AddSectionTitle("Post Processing", uiPanel.transform, leftSectionPos, yOffset);
				yOffset = AddPostProcessingToggles(yOffset, uiPanel.transform, leftSectionPos);
			}

			// Performance Settings
			yOffset = -50;
			yOffset = AddSectionTitle("Performance Settings", uiPanel.transform, rightSectionPos, yOffset);
			yOffset = AddFPSLimitDropdown(yOffset, uiPanel.transform, rightSectionPos);
			yOffset = AddResolutionDropdown(yOffset, uiPanel.transform, rightSectionPos);
			yOffset = AddVSyncToggle(yOffset, uiPanel.transform, rightSectionPos);

			// Quality Settings dropdown
			yOffset = AddQualityDropdown(yOffset, uiPanel.transform, rightSectionPos);

#if REMEMBERME_HDRP_PRESENT
			// HDRP Advanced Settings
			if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.GetType().Name.Contains("HighDefinition") == true)
			{
				yOffset = -50;
				yOffset = AddSectionTitle("HDRP Advanced Settings", uiPanel.transform, centerSectionPos, yOffset);
				yOffset = AddDynamicResolutionToggle(yOffset, uiPanel.transform, centerSectionPos);
				GenerateUpscalingControls(uiPanel.transform, centerSectionPos, ref yOffset);
			}
#endif

			// Audio Settings
			float audioYOffset = -380f;  // position below everything else
			Vector2 audioSectionPos = rightSectionPos;

			audioYOffset = AddSectionTitle("Audio Settings", uiPanel.transform, audioSectionPos, audioYOffset);

			// Create the 4 volume sliders
			masterSlider = CreateVolumeSlider(MasterVolumeParam, "Master", ref audioYOffset, uiPanel.transform, audioSectionPos);
			musicSlider = CreateVolumeSlider(MusicVolumeParam, "Music", ref audioYOffset, uiPanel.transform, audioSectionPos);
			sfxSlider = CreateVolumeSlider(SfxVolumeParam, "SFX", ref audioYOffset, uiPanel.transform, audioSectionPos);
			voiceSlider = CreateVolumeSlider(VoiceVolumeParam, "Voice", ref audioYOffset, uiPanel.transform, audioSectionPos);

			AddSaveLoadButtons(uiPanel.transform);
		}

		#region Upscaling
		private void GenerateUpscalingControls(Transform parent, Vector2 sectionPos, ref float yOffset)
		{
			if (mainCamera == null) return;

			string vendor = SystemInfo.graphicsDeviceVendor.ToLower();
			bool isNvidia = vendor.Contains("nvidia") && SystemInfo.supportsRayTracing;
			bool isAmd = vendor.Contains("amd") || vendor.Contains("advanced micro devices");

			if (isNvidia)
			{
				yOffset = AddUpscalingControls("DLSS", "NVIDIA DLSS", "DLSS Quality", OnDLSSChanged, OnDLSSQualityChanged, yOffset, parent, sectionPos);
			}
			else if (isAmd)
			{
				yOffset = AddUpscalingControls("FSR2", "AMD FSR2", "FSR2 Quality", OnFSR2Changed, OnFSR2QualityChanged, yOffset, parent, sectionPos);
			}
			else
			{
				Debug.Log("No supported upscaling technology detected for this GPU.");
			}
		}

		private float AddUpscalingControls(string controlType, string toggleLabel, string qualityLabel,
										   System.Action<bool> onToggleChanged, System.Action<int> onQualityChanged,
										   float yOffset, Transform parent, Vector2 sectionPos)
		{
			GameObject toggleGO = CreateToggle($"{controlType} Toggle", parent, new Vector2(sectionPos.x, yOffset), toggleLabel, false, onToggleChanged);
			toggles.Add(toggleGO.GetComponent<Toggle>());

#if REMEMBERME_HDRP_PRESENT
			if (mainCamera != null)
			{
				var hdData = mainCamera.GetComponent<HDAdditionalCameraData>();
				if (hdData != null)
				{
					var toggle = toggleGO.GetComponent<Toggle>();

					if (controlType == "DLSS")
					{
						toggle.isOn = hdData.allowDeepLearningSuperSampling;
					}
					else
					{
	#if UNITY_6000_0_OR_NEWER
						toggle.isOn = hdData.allowFidelityFX2SuperResolution;
	#else
						// Unity 2022 HDRP does not support FSR 2.0 toggle
						toggle.isOn = false; // Or hide/disable the toggle entirely
	#endif
					}
				}

			}
#endif

			yOffset -= elementSpacing;

			GameObject dropdownGO = CreateTMPDropdown($"{controlType} Quality Dropdown", parent, new Vector2(sectionPos.x, yOffset));
			TMP_Dropdown dropdown = dropdownGO.GetComponent<TMP_Dropdown>();
			List<string> qualityOptions = new List<string> { "Quality", "Balanced", "Performance", "Ultra Performance" };
			PopulateTMPDropdown(dropdown, qualityOptions, "Quality");
			dropdown.onValueChanged.AddListener((int value) => onQualityChanged(value));
			AddLabel(dropdownGO.transform, qualityLabel, new Vector2(-200, 0));

#if REMEMBERME_HDRP_PRESENT
			if (mainCamera != null)
			{
				HDAdditionalCameraData hdData = mainCamera.GetComponent<HDAdditionalCameraData>();
				if (hdData != null && controlType == "DLSS")
				{
					dropdown.value = (int)hdData.deepLearningSuperSamplingQuality;
				}
			}
#endif
			yOffset -= elementSpacing;
			return yOffset;
		}

		private void OnDLSSChanged(bool isOn)
		{
#if REMEMBERME_HDRP_PRESENT
			if (mainCamera == null) return;
			HDAdditionalCameraData hdCameraData = mainCamera.GetComponent<HDAdditionalCameraData>();
			if (hdCameraData == null)
			{
				Debug.LogWarning("HDAdditionalCameraData not found on Main Camera.");
				return;
			}

			hdCameraData.allowDynamicResolution = true;
			hdCameraData.allowDeepLearningSuperSampling = isOn;
	#if UNITY_6000_0_OR_NEWER
			hdCameraData.allowFidelityFX2SuperResolution = false;
	#endif
			Debug.Log($"DLSS {(isOn ? "enabled" : "disabled")} on Main Camera.");
#endif
		}

		private void OnFSR2Changed(bool isOn)
		{
#if REMEMBERME_HDRP_PRESENT
			if (mainCamera == null) return;
			HDAdditionalCameraData hdCameraData = mainCamera.GetComponent<HDAdditionalCameraData>();
			if (hdCameraData == null)
			{
				Debug.LogWarning("HDAdditionalCameraData not found on Main Camera.");
				return;
			}

			hdCameraData.allowDynamicResolution = true;
	#if UNITY_6000_0_OR_NEWER
			hdCameraData.allowFidelityFX2SuperResolution = isOn;
	#endif
			hdCameraData.allowDeepLearningSuperSampling   = false;
			Debug.Log($"FSR2 {(isOn ? "enabled" : "disabled")} on Main Camera.");
#endif
		}

		private void OnDLSSQualityChanged(int index)
		{
#if REMEMBERME_HDRP_PRESENT
			if (mainCamera == null) return;
			HDAdditionalCameraData hdCameraData = mainCamera.GetComponent<HDAdditionalCameraData>();
			if (hdCameraData == null)
			{
				Debug.LogWarning("HDAdditionalCameraData not found on Main Camera.");
				return;
			}

			hdCameraData.deepLearningSuperSamplingQuality = (uint)index;
			Debug.Log($"DLSS Quality set to: {index}");
#endif
		}

		private void OnFSR2QualityChanged(int index)
		{
#if REMEMBERME_HDRP_PRESENT
			if (mainCamera == null) return;
			HDAdditionalCameraData hdCameraData = mainCamera.GetComponent<HDAdditionalCameraData>();
			if (hdCameraData == null)
			{
				Debug.LogWarning("HDAdditionalCameraData not found on Main Camera.");
				return;
			}

			Debug.Log($"FSR2 Quality set to: {index} (adjust as needed)");
#endif
		}
		#endregion

		private bool GetInitialState(PostProcessingOverride overrideType)
{
#if UNITY_POST_PROCESSING_STACK_V2 && !REMEMBERME_URP_PRESENT && !REMEMBERME_HDRP_PRESENT
    // If only PPv2 is present (neither URP nor HDRP), query the PostProcessVolume
    PostProcessVolume ppVolume = targetVolume?.GetComponent<PostProcessVolume>();
    if (ppVolume != null && ppVolume.profile != null)
    {
        return GetPPv2State(ppVolume.profile, overrideType);
    }
#endif

#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
    // If URP or HDRP is present, query the VolumeProfile
    Volume volume = targetVolume?.GetComponent<Volume>();
    if (volume != null && volume.profile != null)
    {
        return GetVolumeState(volume.profile, overrideType);
    }
#endif

    return false;
}

				private GameObject CreateGradientPanel(string name, Vector2 position, Vector2 size, Color baseColor)
				{
					GameObject panelGO = new GameObject(name, typeof(RectTransform), typeof(Image));
					RectTransform rect = panelGO.GetComponent<RectTransform>();
					rect.anchorMin = new Vector2(0, 1);
					rect.anchorMax = new Vector2(0, 1);
					rect.pivot = new Vector2(0, 1);
					rect.anchoredPosition = position;
					rect.sizeDelta = size;

					Image bgImage = panelGO.GetComponent<Image>();
					bgImage.color = baseColor;

					return panelGO;
				}

				private float AddSectionTitle(string title, Transform parent, Vector2 sectionPos, float yOffset)
				{
					GameObject titleGO = CreateText($"{title} Title", parent, new Vector2(sectionPos.x, yOffset), title, 32, sciFiText, FontStyles.Bold);
					titleGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(sectionPos.x, yOffset);
					return yOffset - elementSpacing;
				}

				private float AddPostProcessingToggles(float yOffset, Transform parent, Vector2 sectionPos)
				{
					var overrideTypes = System.Enum.GetValues(typeof(PostProcessingOverride)).Cast<PostProcessingOverride>();
					float initialX = sectionPos.x - 150f;
					float currentX = initialX;
					float currentY = yOffset - elementSpacing;
					float columnWidth = 300f;
					float rowHeight = elementSpacing;
					int itemsPerColumn = 10;

					int columnIndex = 0;
					int rowIndex = 0;

					foreach (var overrideType in overrideTypes)
					{
						if (!IsOverrideSupported(overrideType)) continue;

						string labelText = GetOverrideLabel(overrideType);
						GameObject toggleGO = CreateToggle($"{overrideType} Toggle", parent,
							new Vector2(currentX, currentY), labelText,
							GetInitialState(overrideType),
							(isOn) => OnToggleChanged(overrideType, isOn));
						toggles.Add(toggleGO.GetComponent<Toggle>());

						rowIndex++;
						if (rowIndex >= itemsPerColumn)
						{
							rowIndex = 0;
							columnIndex++;
							currentX = initialX + (columnWidth * columnIndex);
							currentY = yOffset - elementSpacing;
						}
						else
						{
							currentY -= rowHeight;
						}
					}
					return currentY + rowHeight;
				}

				private string GetOverrideLabel(PostProcessingOverride overrideType)
				{
					switch (overrideType)
					{
		#if REMEMBERME_HDRP_PRESENT || UNITY_POST_PROCESSING_STACK_V2
						case PostProcessingOverride.AmbientOcclusion: return "Ambient Occlusion";
		#endif
						case PostProcessingOverride.AutoExposure: return "Auto Exposure";
						case PostProcessingOverride.Bloom: return "Bloom";
						case PostProcessingOverride.ChannelMixer: return "Channel Mixer";
						case PostProcessingOverride.ChromaticAberration: return "Chromatic Aberration";
						case PostProcessingOverride.ColorAdjustments: return "Color Adjustments";
						case PostProcessingOverride.DepthOfField: return "Depth of Field";
						case PostProcessingOverride.LensDistortion: return "Lens Distortion";
						case PostProcessingOverride.LiftGammaGain: return "Lift Gamma Gain";
						case PostProcessingOverride.MotionBlur: return "Motion Blur";
						case PostProcessingOverride.Tonemapping: return "Tone Mapping";
						case PostProcessingOverride.Vignette: return "Vignette";
						case PostProcessingOverride.WhiteBalance: return "White Balance";
						case PostProcessingOverride.ColorCurves: return "Color Curves";
						case PostProcessingOverride.FilmGrain: return "Film Grain";
						case PostProcessingOverride.PaniniProjection: return "Panini Projection";
						case PostProcessingOverride.ScreenSpaceLensFlare: return "Screen Space Lens Flare";
						case PostProcessingOverride.ShadowsMidtonesHighlights: return "Shadows Midtones Highlights";
						case PostProcessingOverride.SplitToning: return "Split Toning";
						default: return overrideType.ToString();
					}
				}

				private float AddFPSLimitDropdown(float yOffset, Transform parent, Vector2 sectionPos)
				{
					GameObject dropdownGO = CreateTMPDropdown("FPS Limit Dropdown", parent, new Vector2(sectionPos.x, yOffset));
					TMP_Dropdown dropdown = dropdownGO.GetComponent<TMP_Dropdown>();

					List<string> options = System.Enum.GetNames(typeof(FPSLimit))
						.Select(fps => fps.StartsWith("FPS") ? fps.Substring(3) : fps).ToList();
					PopulateTMPDropdown(dropdown, options,
						Application.targetFrameRate == -1 ? "Unlimited" :
						Application.targetFrameRate.ToString());

					dropdown.onValueChanged.AddListener((value) =>
					{
						var limit = (FPSLimit)System.Enum.Parse(typeof(FPSLimit),
							System.Enum.GetNames(typeof(FPSLimit))[value]);
						OnFPSLimitChanged(limit);
					});

					AddLabel(dropdownGO.transform, "FPS Limit", new Vector2(-200, 0));
					return yOffset - elementSpacing;
				}

				private float AddResolutionDropdown(float yOffset, Transform parent, Vector2 sectionPos)
				{
					GameObject dropdownGO = CreateTMPDropdown("Resolution Dropdown", parent, new Vector2(sectionPos.x, yOffset));
					TMP_Dropdown dropdown = dropdownGO.GetComponent<TMP_Dropdown>();

					List<string> options = System.Enum.GetNames(typeof(ResolutionOptions))
						.Select(r => r.Substring(1).Replace("x", " x "))
						.ToList();

					Resolution currentRes = Screen.currentResolution;
					string currentResStr = $"{currentRes.width} x {currentRes.height}";

					PopulateTMPDropdown(dropdown, options,
						options.Contains(currentResStr) ? currentResStr : "1920 x 1080");

					dropdown.onValueChanged.AddListener((value) =>
					{
						string parsed = "R" + options[value].Replace(" x ", "x");
						var resOption = (ResolutionOptions)System.Enum.Parse(typeof(ResolutionOptions), parsed);
						OnResolutionChanged(resOption);
					});

					AddLabel(dropdownGO.transform, "Resolution", new Vector2(-200, 0));
					return yOffset - elementSpacing;
				}

				private float AddVSyncToggle(float yOffset, Transform parent, Vector2 sectionPos)
				{
					GameObject toggleGO = CreateToggle("VSync Toggle", parent,
						new Vector2(sectionPos.x, yOffset), "VSync",
						QualitySettings.vSyncCount > 0, OnVSyncChanged);
					toggles.Add(toggleGO.GetComponent<Toggle>());
					return yOffset - elementSpacing;
				}

				private float AddDynamicResolutionToggle(float yOffset, Transform parent, Vector2 sectionPos)
				{
					GameObject toggleGO = CreateToggle("Dynamic Resolution Toggle", parent,
						new Vector2(sectionPos.x, yOffset), "Dynamic Resolution",
						false, OnDynamicResolutionChanged);
					toggles.Add(toggleGO.GetComponent<Toggle>());

		#if REMEMBERME_HDRP_PRESENT
					if (mainCamera != null)
					{
						HDAdditionalCameraData hdData = mainCamera.GetComponent<HDAdditionalCameraData>();
						if (hdData != null)
							toggleGO.GetComponent<Toggle>().isOn = hdData.allowDynamicResolution;
					}
		#endif
					return yOffset - elementSpacing;
				}

				private GameObject CreateTMPDropdown(string name, Transform parent, Vector2 position)
				{
					GameObject dropdownGO = new GameObject(name, typeof(RectTransform));
					dropdownGO.transform.SetParent(parent, false);

					TMP_Dropdown dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
					RectTransform dropdownRect = dropdownGO.GetComponent<RectTransform>();
					dropdownRect.anchorMin = new Vector2(0, 1);
					dropdownRect.anchorMax = new Vector2(0, 1);
					dropdownRect.pivot = new Vector2(0, 1);
					dropdownRect.anchoredPosition = position;
					dropdownRect.sizeDelta = new Vector2(300, 40);

					Image bgImage = dropdownGO.AddComponent<Image>();
					bgImage.color = sciFiAccent;

					GameObject captionGO = new GameObject("Caption", typeof(RectTransform));
					captionGO.transform.SetParent(dropdownGO.transform, false);
					TextMeshProUGUI captionText = captionGO.AddComponent<TextMeshProUGUI>();
					captionText.fontSize = 24;
					captionText.color = sciFiText;
					captionText.alignment = TextAlignmentOptions.MidlineLeft;

					RectTransform captionRect = captionGO.GetComponent<RectTransform>();
					captionRect.anchorMin = new Vector2(0, 0);
					captionRect.anchorMax = new Vector2(1, 1);
					captionRect.sizeDelta = Vector2.zero;
					captionRect.offsetMin = new Vector2(10, 0);

					dropdown.captionText = captionText;

					GameObject template = new GameObject("Template", typeof(RectTransform), typeof(ScrollRect));
					template.transform.SetParent(dropdownGO.transform, false);

					RectTransform templateRect = template.GetComponent<RectTransform>();
					templateRect.anchorMin = new Vector2(0, 0);
					templateRect.anchorMax = new Vector2(1, 0);
					templateRect.pivot = new Vector2(0, 1);
					templateRect.anchoredPosition = new Vector2(0, -40);
					templateRect.sizeDelta = new Vector2(0, 150);
					template.SetActive(false);

					GameObject content = new GameObject("Content", typeof(RectTransform));
					content.transform.SetParent(template.transform, false);
					RectTransform contentRect = content.GetComponent<RectTransform>();
					contentRect.anchorMin = new Vector2(0, 1);
					contentRect.anchorMax = new Vector2(1, 1);
					contentRect.sizeDelta = new Vector2(0, 40);

					GameObject item = new GameObject("Item", typeof(RectTransform));
					item.transform.SetParent(content.transform, false);
					Toggle itemToggle = item.AddComponent<Toggle>();
					RectTransform itemRect = item.GetComponent<RectTransform>();
					itemRect.anchorMin = new Vector2(0, 0);
					itemRect.anchorMax = new Vector2(1, 1);
					itemRect.sizeDelta = Vector2.zero;

					GameObject itemBackground = new GameObject("Background", typeof(Image));
					itemBackground.transform.SetParent(item.transform, false);
					Image itemBgImage = itemBackground.GetComponent<Image>();
					itemBgImage.color = sciFiInactive;
					RectTransform itemBgRect = itemBackground.GetComponent<RectTransform>();
					itemBgRect.anchorMin = new Vector2(0, 0);
					itemBgRect.anchorMax = new Vector2(1, 1);
					itemBgRect.sizeDelta = Vector2.zero;

					GameObject itemLabel = new GameObject("Label", typeof(RectTransform));
					itemLabel.transform.SetParent(item.transform, false);
					TextMeshProUGUI itemLabelText = itemLabel.AddComponent<TextMeshProUGUI>();
					itemLabelText.fontSize = 20;
					itemLabelText.color = sciFiText;
					RectTransform itemLabelRect = itemLabel.GetComponent<RectTransform>();
					itemLabelRect.anchorMin = new Vector2(0, 0);
					itemLabelRect.anchorMax = new Vector2(1, 1);
					itemLabelRect.sizeDelta = Vector2.zero;

					itemToggle.targetGraphic = itemBgImage;
					dropdown.itemText = itemLabelText;
					dropdown.template = templateRect;
					return dropdownGO;
				}

				private void PopulateTMPDropdown(TMP_Dropdown dropdown, List<string> options, string initialValue)
				{
					dropdown.options = options
						.Select(opt => new TMP_Dropdown.OptionData(opt))
						.ToList();

					int initialIndex = options.IndexOf(initialValue);
					dropdown.value = initialIndex != -1 ? initialIndex : 0;
					dropdown.RefreshShownValue();
				}

				private GameObject CreateToggle(string name, Transform parent, Vector2 position,
												string labelText, bool initialState,
												System.Action<bool> onValueChanged)
				{
					GameObject toggleGO = new GameObject(name, typeof(RectTransform));
					toggleGO.transform.SetParent(parent, false);

					Toggle toggle = toggleGO.AddComponent<Toggle>();
					RectTransform toggleRect = toggleGO.GetComponent<RectTransform>();
					toggleRect.anchorMin = new Vector2(0, 1);
					toggleRect.anchorMax = new Vector2(0, 1);
					toggleRect.pivot = new Vector2(0, 1);
					toggleRect.anchoredPosition = position;
					toggleRect.sizeDelta = new Vector2(40, 40);

					GameObject bgGO = new GameObject("Background", typeof(Image));
					bgGO.transform.SetParent(toggleGO.transform, false);
					Image bgImage = bgGO.GetComponent<Image>();
					bgImage.color = sciFiInactive;

					RectTransform bgRect = bgGO.GetComponent<RectTransform>();
					bgRect.sizeDelta = new Vector2(40, 40);

					GameObject checkGO = new GameObject("Checkmark", typeof(Image));
					checkGO.transform.SetParent(toggleGO.transform, false);
					Image checkImage = checkGO.GetComponent<Image>();
					checkImage.color = sciFiAccent;

					RectTransform checkRect = checkGO.GetComponent<RectTransform>();
					checkRect.sizeDelta = new Vector2(32, 32);

					toggle.targetGraphic = bgImage;
					toggle.graphic = checkImage;
					toggle.isOn = initialState;

					toggle.onValueChanged.AddListener(
						new UnityEngine.Events.UnityAction<bool>(onValueChanged));

					CreateText("Label", toggleGO.transform, new Vector2(50, 0),
						labelText, 24, sciFiText);

					return toggleGO;
				}

				private GameObject CreateText(string name, Transform parent, Vector2 position,
											string text, float fontSize, Color color,
											FontStyles style = FontStyles.Normal)
				{
					GameObject textGO = new GameObject(name, typeof(RectTransform));
					textGO.transform.SetParent(parent, false);

					TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();
					tmpText.text = text;
					tmpText.fontSize = fontSize;
					tmpText.color = color;
					tmpText.fontStyle = style;
					tmpText.alignment = TextAlignmentOptions.Left;

					RectTransform rect = textGO.GetComponent<RectTransform>();
					rect.anchorMin = new Vector2(0, 1);
					rect.anchorMax = new Vector2(0, 1);
					rect.pivot = new Vector2(0, 1);
					rect.anchoredPosition = position;
					rect.sizeDelta = new Vector2(500, 40);

					return textGO;
				}

				private void AddLabel(Transform parent, string text, Vector2 offset)
				{
					CreateText("Label", parent, offset, text, 24, sciFiText);
				}

				private GameObject CreateButton(string name, Transform parent, Vector2 position,
												Vector2 size, string labelText,
												System.Action onClick)
				{
					GameObject buttonGO = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
					buttonGO.transform.SetParent(parent, false);

					RectTransform rect = buttonGO.GetComponent<RectTransform>();
					rect.anchorMin = new Vector2(0, 1);
					rect.anchorMax = new Vector2(0, 1);
					rect.pivot = new Vector2(0, 1);
					rect.anchoredPosition = position;
					rect.sizeDelta = size;

					Image bgImage = buttonGO.GetComponent<Image>();
					bgImage.color = sciFiAccent;

					Button button = buttonGO.GetComponent<Button>();
					button.onClick.AddListener(() => onClick());
					button.onClick.AddListener(() => StartCoroutine(ClickFeedback(bgImage)));

					GameObject labelGO = CreateText("Label", buttonGO.transform, new Vector2(0, 0), labelText, 24, sciFiText);
					RectTransform labelRect = labelGO.GetComponent<RectTransform>();
					labelRect.anchorMin = new Vector2(0, 0);
					labelRect.anchorMax = new Vector2(1, 1);
					labelRect.sizeDelta = Vector2.zero;
					labelRect.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

					return buttonGO;
				}

				private System.Collections.IEnumerator ClickFeedback(Image buttonImage)
				{
					Color originalColor = buttonImage.color;
					buttonImage.color = buttonClickColor;
					yield return new WaitForSeconds(0.1f);
					buttonImage.color = originalColor;
				}

				private void AddSaveLoadButtons(Transform parent)
				{
					// Step 1: Create a container anchored at bottom-center
					GameObject buttonContainer = new GameObject("BottomCenterContainer", typeof(RectTransform));
					RectTransform containerRect = buttonContainer.GetComponent<RectTransform>();
					containerRect.SetParent(parent, false);

					// Anchor/pivot at bottom-center
					containerRect.anchorMin = new Vector2(0.5f, 0f);
					containerRect.anchorMax = new Vector2(0.5f, 0f);
					containerRect.pivot = new Vector2(0.5f, 0.5f);

					// Position it 50 px above the bottom in reference space
					containerRect.anchoredPosition = new Vector2(0f, 50f);

					// Step 2: Create the two buttons as children of this container
					float buttonWidth = 200f;
					float buttonHeight = 50f;
					float buttonSpacing = 20f;

					Vector2 saveButtonPos = new Vector2(-((buttonWidth + buttonSpacing) / 2f), 0f);
					Vector2 loadButtonPos = new Vector2(((buttonWidth + buttonSpacing) / 2f), 0f);

					CreateButton("Save Button", containerRect,
						saveButtonPos,
						new Vector2(buttonWidth, buttonHeight),
						"Save", OnSaveButtonClicked);

					CreateButton("Load Button", containerRect,
						loadButtonPos,
						new Vector2(buttonWidth, buttonHeight),
						"Load", OnLoadButtonClicked);
				}

				private void OnSaveButtonClicked()
				{
					if (UserSettingsManager.Instance != null)
					{
						UserSettingsManager.Instance.SaveSettings();
						Debug.Log("User settings saved!");
					}
					else
					{
						Debug.LogWarning("UserSettingsManager instance not found!");
					}
				}

				private void OnLoadButtonClicked()
				{
					if (UserSettingsManager.Instance != null)
					{
						UserSettingsManager.Instance.LoadSettings();
						Debug.Log("User settings loaded!");

						// (1) The manager stores volumes as linear floats in [0..1].
						// So we can directly set the slider values without any decibel conversion:
						masterSlider.SetValueWithoutNotify(UserSettingsManager.Instance.MasterVolume);
						musicSlider.SetValueWithoutNotify(UserSettingsManager.Instance.MusicVolume);
						sfxSlider.SetValueWithoutNotify(UserSettingsManager.Instance.SfxVolume);
						voiceSlider.SetValueWithoutNotify(UserSettingsManager.Instance.VoiceVolume);
					}
					else
					{
						Debug.LogWarning("UserSettingsManager instance not found!");
					}
				}

				private bool IsOverrideSupported(PostProcessingOverride overrideType)
				{
					var currentPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
		#if !UNITY_POST_PROCESSING_STACK_V2
					if (overrideType == PostProcessingOverride.AutoExposure)
						return false;
		#endif
		#if REMEMBERME_HDRP_PRESENT
					if (currentPipeline != null && currentPipeline.GetType().Name.Contains("HighDefinition"))
					{
						switch (overrideType)
						{
							case PostProcessingOverride.ColorCurves:
							case PostProcessingOverride.FilmGrain:
							case PostProcessingOverride.PaniniProjection:
							case PostProcessingOverride.ScreenSpaceLensFlare:
							case PostProcessingOverride.ShadowsMidtonesHighlights:
							case PostProcessingOverride.SplitToning:
								return true;
						}
					}
		#endif
					return true;
				}
		
		/// <summary>
		/// This method is called whenever a UI toggle changes.
		/// It dispatches either to PPv2 or to URP/HDRP volume logic.
		/// </summary>
		private void OnToggleChanged(PostProcessingOverride overrideType, bool isOn)
		{
			overrideStates[overrideType] = isOn;

		#if UNITY_POST_PROCESSING_STACK_V2 && !REMEMBERME_URP_PRESENT && !REMEMBERME_HDRP_PRESENT
			// If only PPv2 is present (no URP/HDRP), toggle the PPv2 effect:
			PostProcessVolume ppVolume = targetVolume?.GetComponent<PostProcessVolume>();
			if (ppVolume != null && ppVolume.profile != null)
			{
				TogglePPv2(ppVolume.profile, overrideType, isOn);
				return;
			}
		#endif

		#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
			// If URP or HDRP is present, toggle the appropriate VolumeComponent:
			Volume volume = targetVolume?.GetComponent<Volume>();
			if (volume != null && volume.profile != null)
			{
				ToggleVolume(volume.profile, overrideType, isOn);
				return;
			}
		#endif

			Debug.LogWarning("No valid post processing component found.");
		}

		#if UNITY_POST_PROCESSING_STACK_V2 && !REMEMBERME_URP_PRESENT && !REMEMBERME_HDRP_PRESENT
		/// <summary>
		/// Toggle PPv2 settings (PostProcessEffectSettings) directly on a PostProcessProfile.
		/// Only compiled when PPv2 is installed and neither URP nor HDRP are present.
		/// </summary>
		private void TogglePPv2(PostProcessProfile profile, PostProcessingOverride overrideType, bool state)
		{
			switch (overrideType)
			{
				#if !UNITY_POST_PROCESSING_STACK_V2 
				case PostProcessingOverride.AmbientOcclusion:
				{
					var ao = profile.GetSetting<UnityEngine.Rendering.PostProcessing.ScreenSpaceAmbientOcclusion>();
					if (ao != null) ao.enabled.Override(state);
					break;
				}
				#endif
				case PostProcessingOverride.AutoExposure:
				{
					var ae = profile.GetSetting<UnityEngine.Rendering.PostProcessing.AutoExposure>();
					if (ae != null) ae.enabled.Override(state);
					break;
				}
				case PostProcessingOverride.Bloom:
				{
					var bloom = profile.GetSetting<UnityEngine.Rendering.PostProcessing.Bloom>();
					if (bloom != null) bloom.enabled.Override(state);
					break;
				}
				case PostProcessingOverride.ChromaticAberration:
				{
					var ca = profile.GetSetting<UnityEngine.Rendering.PostProcessing.ChromaticAberration>();
					if (ca != null) ca.enabled.Override(state);
					break;
				}
				case PostProcessingOverride.DepthOfField:
				{
					var dof = profile.GetSetting<UnityEngine.Rendering.PostProcessing.DepthOfField>();
					if (dof != null) dof.enabled.Override(state);
					break;
				}
				case PostProcessingOverride.LensDistortion:
				{
					var ld = profile.GetSetting<UnityEngine.Rendering.PostProcessing.LensDistortion>();
					if (ld != null) ld.enabled.Override(state);
					break;
				}
				case PostProcessingOverride.MotionBlur:
				{
					var mb = profile.GetSetting<UnityEngine.Rendering.PostProcessing.MotionBlur>();
					if (mb != null) mb.enabled.Override(state);
					break;
				}
				case PostProcessingOverride.Vignette:
				{
					var vg = profile.GetSetting<UnityEngine.Rendering.PostProcessing.Vignette>();
					if (vg != null) vg.enabled.Override(state);
					break;
				}
				default:
					Debug.LogWarning($"[PPv2] Override {overrideType} not supported.");
					break;
			}
		}

		/// <summary>
		/// Reads PPv2 state from a PostProcessProfile (PostProcessEffectSettings).
		/// Only used when PPv2 is installed and neither URP nor HDRP are present.
		/// </summary>
		private bool GetPPv2State(PostProcessProfile profile, PostProcessingOverride overrideType)
		{
			switch (overrideType)
			{
				#if !UNITY_POST_PROCESSING_STACK_V2
				case PostProcessingOverride.AmbientOcclusion:
				{
					var ao = profile.GetSetting<UnityEngine.Rendering.PostProcessing.ScreenSpaceAmbientOcclusion>();
					return ao != null && ao.enabled.value;
				}
				#endif
				case PostProcessingOverride.AutoExposure:
				{
					var ae = profile.GetSetting<UnityEngine.Rendering.PostProcessing.AutoExposure>();
					return ae != null && ae.enabled.value;
				}
				case PostProcessingOverride.Bloom:
				{
					var bloom = profile.GetSetting<UnityEngine.Rendering.PostProcessing.Bloom>();
					return bloom != null && bloom.enabled.value;
				}
				case PostProcessingOverride.ChromaticAberration:
				{
					var ca = profile.GetSetting<UnityEngine.Rendering.PostProcessing.ChromaticAberration>();
					return ca != null && ca.enabled.value;
				}
				case PostProcessingOverride.DepthOfField:
				{
					var dof = profile.GetSetting<UnityEngine.Rendering.PostProcessing.DepthOfField>();
					return dof != null && dof.enabled.value;
				}
				case PostProcessingOverride.LensDistortion:
				{
					var ld = profile.GetSetting<UnityEngine.Rendering.PostProcessing.LensDistortion>();
					return ld != null && ld.enabled.value;
				}
				case PostProcessingOverride.MotionBlur:
				{
					var mb = profile.GetSetting<UnityEngine.Rendering.PostProcessing.MotionBlur>();
					return mb != null && mb.enabled.value;
				}
				case PostProcessingOverride.Vignette:
				{
					var vg = profile.GetSetting<UnityEngine.Rendering.PostProcessing.Vignette>();
					return vg != null && vg.enabled.value;
				}
				default:
					return false;
			}
		}
		#endif

		#if REMEMBERME_URP_PRESENT || REMEMBERME_HDRP_PRESENT
		/// <summary>
		/// Toggle URP/HDRP VolumeComponent overrides on a VolumeProfile.
		/// Only compiled when URP or HDRP is present.
		/// </summary>
		private void ToggleVolume(VolumeProfile profile, PostProcessingOverride overrideType, bool state)
		{
			switch (overrideType)
			{
		#if REMEMBERME_HDRP_PRESENT
				case PostProcessingOverride.AmbientOcclusion:
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.ScreenSpaceAmbientOcclusion>(out var hdrpAo))
						hdrpAo.active = state;
					break;
		#endif
				case PostProcessingOverride.Bloom:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var urpBloom))
						urpBloom.active = state;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.Bloom>(out var hdrpBloom))
						hdrpBloom.active = state;
		#endif
					break;

				case PostProcessingOverride.ChannelMixer:
					if (profile.TryGet<ChannelMixer>(out var cm))
						cm.active = state;
					break;

				case PostProcessingOverride.ChromaticAberration:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.ChromaticAberration>(out var urpCa))
						urpCa.active = state;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.ChromaticAberration>(out var hdrpCa))
						hdrpCa.active = state;
		#endif
					break;

				case PostProcessingOverride.ColorAdjustments:
					if (profile.TryGet<ColorAdjustments>(out var colorAdj))
						colorAdj.active = state;
					break;

				case PostProcessingOverride.DepthOfField:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var urpDof))
						urpDof.active = state;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.DepthOfField>(out var hdrpDof))
						hdrpDof.active = state;
		#endif
					break;

				case PostProcessingOverride.LensDistortion:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.LensDistortion>(out var urpLd))
						urpLd.active = state;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.LensDistortion>(out var hdrpLd))
						hdrpLd.active = state;
		#endif
					break;

				case PostProcessingOverride.LiftGammaGain:
					if (profile.TryGet<LiftGammaGain>(out var lgg))
						lgg.active = state;
					break;

				case PostProcessingOverride.MotionBlur:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.MotionBlur>(out var urpMb))
						urpMb.active = state;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.MotionBlur>(out var hdrpMb))
						hdrpMb.active = state;
		#endif
					break;

				case PostProcessingOverride.Tonemapping:
					if (profile.TryGet<Tonemapping>(out var tm))
						tm.active = state;
					break;

				case PostProcessingOverride.Vignette:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var urpVig))
						urpVig.active = state;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.Vignette>(out var hdrpVig))
						hdrpVig.active = state;
		#endif
					break;

				case PostProcessingOverride.WhiteBalance:
					if (profile.TryGet<WhiteBalance>(out var wb))
						wb.active = state;
					break;

				case PostProcessingOverride.ColorCurves:
					if (profile.TryGet<ColorCurves>(out var cc))
						cc.active = state;
					break;

				case PostProcessingOverride.FilmGrain:
					if (profile.TryGet<FilmGrain>(out var fg))
						fg.active = state;
					break;

				case PostProcessingOverride.PaniniProjection:
					if (profile.TryGet<PaniniProjection>(out var pp))
						pp.active = state;
					break;

#if UNITY_6000_0_OR_NEWER
				case PostProcessingOverride.ScreenSpaceLensFlare:
					if (profile.TryGet<ScreenSpaceLensFlare>(out var sslf))
						sslf.active = state;
					break;
#endif

				case PostProcessingOverride.ShadowsMidtonesHighlights:
					if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh))
						smh.active = state;
					break;

				case PostProcessingOverride.SplitToning:
					if (profile.TryGet<SplitToning>(out var st))
						st.active = state;
					break;

				default:
					Debug.LogWarning($"[Volume] Override {overrideType} not supported.");
					break;
			}
		}

		/// <summary>
		/// Read the current state of a URP/HDRP VolumeComponent from a VolumeProfile.
		/// Only compiled when URP or HDRP is present.
		/// </summary>
		private bool GetVolumeState(VolumeProfile profile, PostProcessingOverride overrideType)
		{
			switch (overrideType)
			{
		#if REMEMBERME_HDRP_PRESENT
				case PostProcessingOverride.AmbientOcclusion:
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.ScreenSpaceAmbientOcclusion>(out var hdrpAo))
						return hdrpAo.active;
					break;
		#endif
				case PostProcessingOverride.Bloom:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var urpBloom))
						return urpBloom.active;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.Bloom>(out var hdrpBloom))
						return hdrpBloom.active;
		#endif
					break;

				case PostProcessingOverride.ChannelMixer:
					if (profile.TryGet<ChannelMixer>(out var cm))
						return cm.active;
					break;

				case PostProcessingOverride.ChromaticAberration:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.ChromaticAberration>(out var urpCa))
						return urpCa.active;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.ChromaticAberration>(out var hdrpCa))
						return hdrpCa.active;
		#endif
					break;

				case PostProcessingOverride.ColorAdjustments:
					if (profile.TryGet<ColorAdjustments>(out var colorAdj))
						return colorAdj.active;
					break;

				case PostProcessingOverride.DepthOfField:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var urpDof))
						return urpDof.active;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.DepthOfField>(out var hdrpDof))
						return hdrpDof.active;
		#endif
					break;

				case PostProcessingOverride.LensDistortion:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.LensDistortion>(out var urpLd))
						return urpLd.active;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.LensDistortion>(out var hdrpLd))
						return hdrpLd.active;
		#endif
					break;

				case PostProcessingOverride.LiftGammaGain:
					if (profile.TryGet<LiftGammaGain>(out var lgg))
						return lgg.active;
					break;

				case PostProcessingOverride.MotionBlur:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.MotionBlur>(out var urpMb))
						return urpMb.active;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.MotionBlur>(out var hdrpMb))
						return hdrpMb.active;
		#endif
					break;

				case PostProcessingOverride.Tonemapping:
					if (profile.TryGet<Tonemapping>(out var tm))
						return tm.active;
					break;

				case PostProcessingOverride.Vignette:
		#if REMEMBERME_URP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var urpVig))
						return urpVig.active;
		#elif REMEMBERME_HDRP_PRESENT
					if (profile.TryGet<UnityEngine.Rendering.HighDefinition.Vignette>(out var hdrpVig))
						return hdrpVig.active;
		#endif
					break;

				case PostProcessingOverride.WhiteBalance:
					if (profile.TryGet<WhiteBalance>(out var wb))
						return wb.active;
					break;

				case PostProcessingOverride.ColorCurves:
					if (profile.TryGet<ColorCurves>(out var cc))
						return cc.active;
					break;

				case PostProcessingOverride.FilmGrain:
					if (profile.TryGet<FilmGrain>(out var fg))
						return fg.active;
					break;

				case PostProcessingOverride.PaniniProjection:
					if (profile.TryGet<PaniniProjection>(out var pp))
						return pp.active;
					break;

#if UNITY_6000_0_OR_NEWER
				case PostProcessingOverride.ScreenSpaceLensFlare:
					if (profile.TryGet<ScreenSpaceLensFlare>(out var sslf))
						return sslf.active;
					break;
#endif

				case PostProcessingOverride.ShadowsMidtonesHighlights:
					if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh))
						return smh.active;
					break;

				case PostProcessingOverride.SplitToning:
					if (profile.TryGet<SplitToning>(out var st))
						return st.active;
					break;
			}
			return false;
		}
		#endif


		private void OnFPSLimitChanged(FPSLimit limit)
		{
			int targetFPS = (limit == FPSLimit.Unlimited) ? -1 : (int)limit;
			Application.targetFrameRate = targetFPS;
			Debug.Log($"FPS Limit set to {(targetFPS == -1 ? "Unlimited" : targetFPS.ToString())}");
		}

		private void OnResolutionChanged(ResolutionOptions resolution)
		{
			bool isFullScreen = Screen.fullScreen;
			int width = 1920, height = 1080;

			switch (resolution)
			{
				case ResolutionOptions.R800x600: width = 800; height = 600; break;
				case ResolutionOptions.R1024x768: width = 1024; height = 768; break;
				case ResolutionOptions.R1280x960: width = 1280; height = 960; break;
				case ResolutionOptions.R1600x1200: width = 1600; height = 1200; break;
				case ResolutionOptions.R1366x768: width = 1366; height = 768; break;
				case ResolutionOptions.R1920x1080: width = 1920; height = 1080; break;
				case ResolutionOptions.R2560x1440: width = 2560; height = 1440; break;
				case ResolutionOptions.R3440x1440: width = 3440; height = 1440; break;
				case ResolutionOptions.R3840x2160: width = 3840; height = 2160; break;
				case ResolutionOptions.R5120x1440: width = 5120; height = 1440; break;
			}

			Screen.SetResolution(width, height, isFullScreen);
			Debug.Log($"Screen resolution set to {width} x {height}, Fullscreen: {isFullScreen}");
		}

		private void OnVSyncChanged(bool enable)
		{
			QualitySettings.vSyncCount = enable ? 1 : 0;
			Debug.Log($"VSync {(enable ? "Enabled" : "Disabled")}.");
		}

		private void OnDynamicResolutionChanged(bool enable)
		{
#if REMEMBERME_HDRP_PRESENT
			if (mainCamera == null)
			{
				Debug.LogWarning("Main Camera not found.");
				return;
			}

			HDAdditionalCameraData hdCameraData = mainCamera.GetComponent<HDAdditionalCameraData>();
			if (hdCameraData == null)
			{
				Debug.LogWarning("HDAdditionalCameraData not found on Main Camera.");
				return;
			}

			hdCameraData.allowDynamicResolution = enable;
			Debug.Log($"Main Camera dynamic resolution {(enable ? "enabled" : "disabled")}.");
#else
			Debug.LogWarning("Dynamic Resolution is only supported in HDRP.");
#endif
		}

		// Creates a slider for the AudioMixer parameter. Expects (0..1) range.
		private Slider CreateVolumeSlider(
			string mixerParamName,
			string labelText,
			ref float yOffset,
			Transform parent,
			Vector2 sectionPos)
		{
			GameObject sliderRootGO = new GameObject($"{labelText} Slider", typeof(RectTransform));
			sliderRootGO.transform.SetParent(parent, false);

			RectTransform sliderRootRect = sliderRootGO.GetComponent<RectTransform>();
			sliderRootRect.anchorMin = new Vector2(0, 1);
			sliderRootRect.anchorMax = new Vector2(0, 1);
			sliderRootRect.pivot = new Vector2(0, 1);
			sliderRootRect.anchoredPosition = new Vector2(sectionPos.x, yOffset);
			sliderRootRect.sizeDelta = new Vector2(300, 60);

			GameObject labelGO = CreateText($"{labelText} Label", sliderRootGO.transform,
				new Vector2(0, 0), labelText, 24, sciFiText);
			RectTransform labelRect = labelGO.GetComponent<RectTransform>();
			labelRect.anchoredPosition = new Vector2(0, -10);
			labelRect.sizeDelta = new Vector2(100, 40);

			// Slider background + component
			GameObject sliderBGGO = new GameObject("Slider Background",
				typeof(RectTransform), typeof(Image), typeof(Slider));
			sliderBGGO.transform.SetParent(sliderRootGO.transform, false);

			RectTransform sliderRect = sliderBGGO.GetComponent<RectTransform>();
			sliderRect.anchorMin = new Vector2(0, 1);
			sliderRect.anchorMax = new Vector2(0, 1);
			sliderRect.pivot = new Vector2(0, 1);
			sliderRect.anchoredPosition = new Vector2(110, 0);
			sliderRect.sizeDelta = new Vector2(180, 20);

			Image bgImage = sliderBGGO.GetComponent<Image>();
			bgImage.color = sciFiInactive;

			Slider slider = sliderBGGO.GetComponent<Slider>();
			slider.minValue = 0f;
			slider.maxValue = 1f;
			slider.value = 1f; // start at full volume
			slider.direction = Slider.Direction.LeftToRight;

			// Fill area
			GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
			fillAreaGO.transform.SetParent(sliderBGGO.transform, false);
			RectTransform fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
			fillAreaRect.anchorMin = new Vector2(0, 0);
			fillAreaRect.anchorMax = new Vector2(1, 1);
			fillAreaRect.sizeDelta = new Vector2(-20, 0);
			fillAreaRect.anchoredPosition = Vector2.zero;

			GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
			fillGO.transform.SetParent(fillAreaGO.transform, false);
			RectTransform fillRect = fillGO.GetComponent<RectTransform>();
			fillRect.anchorMin = new Vector2(0, 0);
			fillRect.anchorMax = new Vector2(1, 1);
			fillRect.sizeDelta = Vector2.zero;
			fillRect.anchoredPosition = Vector2.zero;

			Image fillImage = fillGO.GetComponent<Image>();
			fillImage.color = sciFiAccent;
			slider.fillRect = fillRect;

			// Handle area
			GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
			handleAreaGO.transform.SetParent(sliderBGGO.transform, false);
			RectTransform handleAreaRect = handleAreaGO.GetComponent<RectTransform>();
			handleAreaRect.anchorMin = new Vector2(0, 0);
			handleAreaRect.anchorMax = new Vector2(1, 1);
			handleAreaRect.sizeDelta = new Vector2(-20, 0);
			handleAreaRect.anchoredPosition = Vector2.zero;

			GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
			handleGO.transform.SetParent(handleAreaGO.transform, false);
			RectTransform handleRect = handleGO.GetComponent<RectTransform>();
			handleRect.anchorMin = new Vector2(1, 0.5f);
			handleRect.anchorMax = new Vector2(1, 0.5f);
			handleRect.sizeDelta = new Vector2(20, 40);
			handleRect.anchoredPosition = Vector2.zero;

			Image handleImage = handleGO.GetComponent<Image>();
			handleImage.color = sciFiAccent;
			slider.handleRect = handleRect;
			slider.targetGraphic = handleImage;

			// Volume callback
			slider.onValueChanged.AddListener((val) => OnVolumeSliderChanged(mixerParamName, val));

			yOffset -= elementSpacing;
			return slider;
		}

		private void OnVolumeSliderChanged(string mixerParamName, float normalizedValue)
		{
			if (audioMixer == null) return;

			float volumeDb;
			if (normalizedValue <= 0f)
			{
				volumeDb = -80f; // or -80 dB to effectively mute
			}
			else
			{
				volumeDb = 20f * Mathf.Log10(normalizedValue);
				if (volumeDb < -80f) volumeDb = -80f; // clamp
			}
			audioMixer.SetFloat(mixerParamName, volumeDb);
		}

		private float AddQualityDropdown(float yOffset, Transform parent, Vector2 sectionPos)
		{
			// Create the dropdown UI element using your helper
			GameObject dropdownGO = CreateTMPDropdown("Quality Dropdown", parent, new Vector2(sectionPos.x, yOffset));
			TMP_Dropdown dropdown = dropdownGO.GetComponent<TMP_Dropdown>();

			// Get available quality options from QualitySettings.names
			List<string> qualityOptions = new List<string>(QualitySettings.names);

			// Determine the current quality level:
			int currentQuality = QualitySettings.GetQualityLevel();
			if (UserSettingsManager.Instance != null && UserSettingsManager.Instance.CurrentSettings != null)
			{
				currentQuality = UserSettingsManager.Instance.CurrentSettings.QualityLevel;
			}

			// Clamp the quality index so that it is always valid.
			currentQuality = Mathf.Clamp(currentQuality, 0, qualityOptions.Count - 1);

			// Populate the dropdown with options and set its initial value
			PopulateTMPDropdown(dropdown, qualityOptions, qualityOptions[currentQuality]);

			// Register listener to update quality settings when selection changes
			dropdown.onValueChanged.AddListener((int value) =>
			{
				QualitySettings.SetQualityLevel(value, true);
				if (UserSettingsManager.Instance != null && UserSettingsManager.Instance.CurrentSettings != null)
				{
					UserSettingsManager.Instance.CurrentSettings.QualityLevel = value;
				}
				Debug.Log($"Quality level set to: {QualitySettings.names[value]}");
			});

			// Add a label to the dropdown for clarity
			AddLabel(dropdownGO.transform, "Quality Level", new Vector2(-200, 0));

			// Update the vertical offset for the next UI element
			return yOffset - elementSpacing;
		}

	}
}
#endif
