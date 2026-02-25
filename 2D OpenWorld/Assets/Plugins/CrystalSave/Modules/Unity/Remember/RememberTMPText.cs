#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Saves and restores TextMeshPro text component properties.
	/// Works with both TextMeshProUGUI (UI) and TextMeshPro (3D) components.
	/// </summary>
	[AddComponentMenu("Crystal Save/Remember Components/Remember TMP Text")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Text))]
	public class RememberTMPText : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of the TMP_Text reference to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Header("Save Optimization")]
		[Tooltip("Skip serialization when the captured data did not change since the last save.")]
		[SerializeField] private bool skipSavingWhenUnchanged = false;

		[Header("Text Content")]
		[Tooltip("Remember the text content.")]
		public bool RememberText = true;

		[Header("Font Properties")]
		[Tooltip("Remember the font size.")]
		public bool RememberFontSize = true;

		[Tooltip("Remember the font style (bold, italic, etc.).")]
		public bool RememberFontStyle = false;

		[Tooltip("Remember the font asset reference.")]
		public bool RememberFontAsset = false;

		[Header("Color Properties")]
		[Tooltip("Remember the main text color.")]
		public bool RememberColor = true;

		[Tooltip("Remember the color gradient.")]
		public bool RememberColorGradient = false;

		[Tooltip("Remember vertex color gradient settings.")]
		public bool RememberEnableVertexGradient = false;

		[Header("Spacing Properties")]
		[Tooltip("Remember character spacing.")]
		public bool RememberCharacterSpacing = false;

		[Tooltip("Remember word spacing.")]
		public bool RememberWordSpacing = false;

		[Tooltip("Remember line spacing.")]
		public bool RememberLineSpacing = false;

		[Tooltip("Remember paragraph spacing.")]
		public bool RememberParagraphSpacing = false;

		[Header("Alignment & Layout")]
		[Tooltip("Remember text alignment.")]
		public bool RememberAlignment = false;

#if UNITY_6000_0_OR_NEWER
		[Tooltip("Remember word wrapping enabled state.")]
		public bool RememberWordWrapping = false;
#endif

		[Tooltip("Remember text overflow mode.")]
		public bool RememberOverflowMode = false;

		[Tooltip("Remember margins.")]
		public bool RememberMargins = false;

		[Header("Other Properties")]
		[Tooltip("Remember max visible characters.")]
		public bool RememberMaxVisibleCharacters = false;

		[Tooltip("Remember rich text enabled state.")]
		public bool RememberRichText = false;

		[Tooltip("Remember enabled state.")]
		public bool RememberEnabled = false;

		[Tooltip("Remember alpha (transparency).")]
		public bool RememberAlpha = false;

		private TMP_Text tmpText;
		private TMPTextSnapshot cachedSnapshot;
		private bool hasCachedSnapshot;
		private byte[] cachedSerializedData;

		private struct TMPTextSnapshot
		{
			public string Text;
			public float FontSize;
			public FontStyles FontStyle;
			public string FontAssetName;
			public Color Color;
			public bool EnableVertexGradient;
			public VertexGradient ColorGradient;
			public float CharacterSpacing;
			public float WordSpacing;
			public float LineSpacing;
			public float ParagraphSpacing;
			public TextAlignmentOptions Alignment;
#if UNITY_6000_0_OR_NEWER
			public TextWrappingModes TextWrappingMode;
#endif
			public TextOverflowModes OverflowMode;
			public Vector4 Margins;
			public int MaxVisibleCharacters;
			public bool RichText;
			public bool Enabled;
			public float Alpha;

			public TMPTextSnapshot Clone()
			{
				return new TMPTextSnapshot
				{
					Text = Text,
					FontSize = FontSize,
					FontStyle = FontStyle,
					FontAssetName = FontAssetName,
					Color = Color,
					EnableVertexGradient = EnableVertexGradient,
					ColorGradient = ColorGradient,
					CharacterSpacing = CharacterSpacing,
					WordSpacing = WordSpacing,
					LineSpacing = LineSpacing,
					ParagraphSpacing = ParagraphSpacing,
					Alignment = Alignment,
#if UNITY_6000_0_OR_NEWER
					TextWrappingMode = TextWrappingMode,
#endif
					OverflowMode = OverflowMode,
					Margins = Margins,
					MaxVisibleCharacters = MaxVisibleCharacters,
					RichText = RichText,
					Enabled = Enabled,
					Alpha = Alpha
				};
			}
		}

		protected override void Awake()
		{
			base.Awake();
			tmpText = GetComponent<TMP_Text>();

			if (tmpText == null)
			{
				Logger.Log($"RememberTMPText: No TMP_Text component found on '{gameObject.name}'. Disabling component.", LogCategory.RememberTMPText, LogLevel.Warning);
				enabled = false;
				return;
			}

			if (skipSavingWhenUnchanged && TryCaptureCurrentState(out TMPTextSnapshot snapshot, false))
			{
				cachedSnapshot = snapshot.Clone();
				hasCachedSnapshot = true;
			}
		}

		protected override byte[] SerializeComponentData()
		{
			if (!TryCaptureCurrentState(out TMPTextSnapshot currentSnapshot, true))
			{
				if (skipSavingWhenUnchanged)
				{
					cachedSnapshot = default;
					hasCachedSnapshot = false;
				}
				return null;
			}

			if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, currentSnapshot))
			{
				if (cachedSerializedData != null && cachedSerializedData.Length > 0)
				{
					return cachedSerializedData;
				}
			}

			TMPTextData data = ConvertSnapshotToData(currentSnapshot);

			try
			{
				byte[] serializedData = Serializer.Serialize<TMPTextData>(data);
				Logger.Log($"RememberTMPText: Successfully serialized TMP text data for '{gameObject.name}'.", LogCategory.RememberTMPText, LogLevel.Info);

				if (skipSavingWhenUnchanged)
				{
					cachedSnapshot = currentSnapshot.Clone();
					hasCachedSnapshot = true;
					cachedSerializedData = serializedData;
				}

				return serializedData;
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberTMPText: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberTMPText, LogLevel.Error);
				return null;
			}
		}

		protected override void DeserializeComponentData(byte[] data)
		{
			TMP_Text text = enablePerformanceCaching ? tmpText : GetComponent<TMP_Text>();

			if (text == null)
			{
				Logger.Log($"DeserializeComponentData: No TMP_Text on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberTMPText, LogLevel.Warning);
				return;
			}

			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberTMPText, LogLevel.Warning);
				return;
			}

			try
			{
				TMPTextData deserializedData = Serializer.Deserialize<TMPTextData>(data);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberTMPText, LogLevel.Warning);
					return;
				}

				if (RememberText && deserializedData.HasText)
				{
					text.text = deserializedData.Text;
				}

				if (RememberFontSize && deserializedData.HasFontSize)
				{
					text.fontSize = deserializedData.FontSize;
				}

				if (RememberFontStyle && deserializedData.HasFontStyle)
				{
					text.fontStyle = deserializedData.FontStyle;
				}

				if (RememberFontAsset && deserializedData.HasFontAsset && !string.IsNullOrEmpty(deserializedData.FontAssetName))
				{
					TMP_FontAsset fontAsset = AssetProvider.Load<TMP_FontAsset>(deserializedData.FontAssetName);
					if (fontAsset != null)
					{
						text.font = fontAsset;
					}
					else
					{
						Logger.Log($"RememberTMPText: Could not find font asset '{deserializedData.FontAssetName}' for '{gameObject.name}'.", LogCategory.RememberTMPText, LogLevel.Warning);
					}
				}

				if (RememberColor && deserializedData.HasColor)
				{
					text.color = deserializedData.Color;
				}

				if (RememberEnableVertexGradient && deserializedData.HasEnableVertexGradient)
				{
					text.enableVertexGradient = deserializedData.EnableVertexGradient;
				}

				if (RememberColorGradient && deserializedData.HasColorGradient)
				{
					text.colorGradient = new VertexGradient(
						deserializedData.GradientTopLeft,
						deserializedData.GradientTopRight,
						deserializedData.GradientBottomLeft,
						deserializedData.GradientBottomRight
					);
				}

				if (RememberCharacterSpacing && deserializedData.HasCharacterSpacing)
				{
					text.characterSpacing = deserializedData.CharacterSpacing;
				}

				if (RememberWordSpacing && deserializedData.HasWordSpacing)
				{
					text.wordSpacing = deserializedData.WordSpacing;
				}

				if (RememberLineSpacing && deserializedData.HasLineSpacing)
				{
					text.lineSpacing = deserializedData.LineSpacing;
				}

				if (RememberParagraphSpacing && deserializedData.HasParagraphSpacing)
				{
					text.paragraphSpacing = deserializedData.ParagraphSpacing;
				}

				if (RememberAlignment && deserializedData.HasAlignment)
				{
					text.alignment = deserializedData.Alignment;
				}

#if UNITY_6000_0_OR_NEWER
				if (RememberWordWrapping && deserializedData.HasWordWrapping)
				{
					text.textWrappingMode = deserializedData.TextWrappingMode;
				}
#endif

				if (RememberOverflowMode && deserializedData.HasOverflowMode)
				{
					text.overflowMode = deserializedData.OverflowMode;
				}

				if (RememberMargins && deserializedData.HasMargins)
				{
					text.margin = deserializedData.Margins;
				}

				if (RememberMaxVisibleCharacters && deserializedData.HasMaxVisibleCharacters)
				{
					text.maxVisibleCharacters = deserializedData.MaxVisibleCharacters;
				}

				if (RememberRichText && deserializedData.HasRichText)
				{
					text.richText = deserializedData.RichText;
				}

				if (RememberEnabled && deserializedData.HasEnabled)
				{
					text.enabled = deserializedData.Enabled;
				}

				if (RememberAlpha && deserializedData.HasAlpha)
				{
					text.alpha = deserializedData.Alpha;
				}

				if (skipSavingWhenUnchanged)
				{
					if (TryCaptureCurrentState(out TMPTextSnapshot snapshot, false))
					{
						cachedSnapshot = snapshot.Clone();
						hasCachedSnapshot = true;
					}
					else
					{
						cachedSnapshot = default;
						hasCachedSnapshot = false;
					}
				}

				Logger.Log($"RememberTMPText: Successfully loaded TMP text data for '{gameObject.name}'.", LogCategory.RememberTMPText, LogLevel.Info);
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberTMPText: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberTMPText, LogLevel.Error);
			}
		}

		private bool TryCaptureCurrentState(out TMPTextSnapshot snapshot, bool logWarnings)
		{
			snapshot = default;

			TMP_Text text = enablePerformanceCaching ? tmpText : GetComponent<TMP_Text>();

			if (text == null)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentState: No TMP_Text on '{gameObject.name}'. Skipping.", LogCategory.RememberTMPText, LogLevel.Warning);
				}
				return false;
			}

			TMPTextSnapshot tempSnapshot = new TMPTextSnapshot();
			bool capturedAny = false;

			if (RememberText)
			{
				tempSnapshot.Text = text.text;
				capturedAny = true;
			}

			if (RememberFontSize)
			{
				tempSnapshot.FontSize = text.fontSize;
				capturedAny = true;
			}

			if (RememberFontStyle)
			{
				tempSnapshot.FontStyle = text.fontStyle;
				capturedAny = true;
			}

			if (RememberFontAsset)
			{
				tempSnapshot.FontAssetName = text.font != null ? text.font.name : string.Empty;
				capturedAny = true;
			}

			if (RememberColor)
			{
				tempSnapshot.Color = text.color;
				capturedAny = true;
			}

			if (RememberEnableVertexGradient)
			{
				tempSnapshot.EnableVertexGradient = text.enableVertexGradient;
				capturedAny = true;
			}

			if (RememberColorGradient)
			{
				tempSnapshot.ColorGradient = text.colorGradient;
				capturedAny = true;
			}

			if (RememberCharacterSpacing)
			{
				tempSnapshot.CharacterSpacing = text.characterSpacing;
				capturedAny = true;
			}

			if (RememberWordSpacing)
			{
				tempSnapshot.WordSpacing = text.wordSpacing;
				capturedAny = true;
			}

			if (RememberLineSpacing)
			{
				tempSnapshot.LineSpacing = text.lineSpacing;
				capturedAny = true;
			}

			if (RememberParagraphSpacing)
			{
				tempSnapshot.ParagraphSpacing = text.paragraphSpacing;
				capturedAny = true;
			}

			if (RememberAlignment)
			{
				tempSnapshot.Alignment = text.alignment;
				capturedAny = true;
			}

#if UNITY_6000_0_OR_NEWER
			if (RememberWordWrapping)
			{
				tempSnapshot.TextWrappingMode = text.textWrappingMode;
				capturedAny = true;
			}
#endif

			if (RememberOverflowMode)
			{
				tempSnapshot.OverflowMode = text.overflowMode;
				capturedAny = true;
			}

			if (RememberMargins)
			{
				tempSnapshot.Margins = text.margin;
				capturedAny = true;
			}

			if (RememberMaxVisibleCharacters)
			{
				tempSnapshot.MaxVisibleCharacters = text.maxVisibleCharacters;
				capturedAny = true;
			}

			if (RememberRichText)
			{
				tempSnapshot.RichText = text.richText;
				capturedAny = true;
			}

			if (RememberEnabled)
			{
				tempSnapshot.Enabled = text.enabled;
				capturedAny = true;
			}

			if (RememberAlpha)
			{
				tempSnapshot.Alpha = text.alpha;
				capturedAny = true;
			}

			if (!capturedAny)
			{
				return false;
			}

			snapshot = tempSnapshot;
			return true;
		}

		private TMPTextData ConvertSnapshotToData(TMPTextSnapshot snapshot)
		{
			TMPTextData data = new TMPTextData();

			if (RememberText)
			{
				data.Text = snapshot.Text;
				data.HasText = true;
			}

			if (RememberFontSize)
			{
				data.FontSize = snapshot.FontSize;
				data.HasFontSize = true;
			}

			if (RememberFontStyle)
			{
				data.FontStyle = snapshot.FontStyle;
				data.HasFontStyle = true;
			}

			if (RememberFontAsset)
			{
				data.FontAssetName = snapshot.FontAssetName;
				data.HasFontAsset = true;
			}

			if (RememberColor)
			{
				data.Color = snapshot.Color;
				data.HasColor = true;
			}

			if (RememberEnableVertexGradient)
			{
				data.EnableVertexGradient = snapshot.EnableVertexGradient;
				data.HasEnableVertexGradient = true;
			}

			if (RememberColorGradient)
			{
				data.GradientTopLeft = snapshot.ColorGradient.topLeft;
				data.GradientTopRight = snapshot.ColorGradient.topRight;
				data.GradientBottomLeft = snapshot.ColorGradient.bottomLeft;
				data.GradientBottomRight = snapshot.ColorGradient.bottomRight;
				data.HasColorGradient = true;
			}

			if (RememberCharacterSpacing)
			{
				data.CharacterSpacing = snapshot.CharacterSpacing;
				data.HasCharacterSpacing = true;
			}

			if (RememberWordSpacing)
			{
				data.WordSpacing = snapshot.WordSpacing;
				data.HasWordSpacing = true;
			}

			if (RememberLineSpacing)
			{
				data.LineSpacing = snapshot.LineSpacing;
				data.HasLineSpacing = true;
			}

			if (RememberParagraphSpacing)
			{
				data.ParagraphSpacing = snapshot.ParagraphSpacing;
				data.HasParagraphSpacing = true;
			}

			if (RememberAlignment)
			{
				data.Alignment = snapshot.Alignment;
				data.HasAlignment = true;
			}

#if UNITY_6000_0_OR_NEWER
			if (RememberWordWrapping)
			{
				data.TextWrappingMode = snapshot.TextWrappingMode;
				data.HasWordWrapping = true;
			}
#endif

			if (RememberOverflowMode)
			{
				data.OverflowMode = snapshot.OverflowMode;
				data.HasOverflowMode = true;
			}

			if (RememberMargins)
			{
				data.Margins = snapshot.Margins;
				data.HasMargins = true;
			}

			if (RememberMaxVisibleCharacters)
			{
				data.MaxVisibleCharacters = snapshot.MaxVisibleCharacters;
				data.HasMaxVisibleCharacters = true;
			}

			if (RememberRichText)
			{
				data.RichText = snapshot.RichText;
				data.HasRichText = true;
			}

			if (RememberEnabled)
			{
				data.Enabled = snapshot.Enabled;
				data.HasEnabled = true;
			}

			if (RememberAlpha)
			{
				data.Alpha = snapshot.Alpha;
				data.HasAlpha = true;
			}

			return data;
		}

		private bool AreEquivalent(TMPTextSnapshot cached, TMPTextSnapshot current)
		{
			const float tolerance = 0.0001f;

			if (RememberText && !string.Equals(cached.Text, current.Text, StringComparison.Ordinal))
				return false;

			if (RememberFontSize && Mathf.Abs(cached.FontSize - current.FontSize) > tolerance)
				return false;

			if (RememberFontStyle && cached.FontStyle != current.FontStyle)
				return false;

			if (RememberFontAsset && !string.Equals(cached.FontAssetName, current.FontAssetName, StringComparison.Ordinal))
				return false;

			if (RememberColor && !ColorsApproximatelyEqual(cached.Color, current.Color))
				return false;

			if (RememberEnableVertexGradient && cached.EnableVertexGradient != current.EnableVertexGradient)
				return false;

			if (RememberColorGradient && !GradientsApproximatelyEqual(cached.ColorGradient, current.ColorGradient))
				return false;

			if (RememberCharacterSpacing && Mathf.Abs(cached.CharacterSpacing - current.CharacterSpacing) > tolerance)
				return false;

			if (RememberWordSpacing && Mathf.Abs(cached.WordSpacing - current.WordSpacing) > tolerance)
				return false;

			if (RememberLineSpacing && Mathf.Abs(cached.LineSpacing - current.LineSpacing) > tolerance)
				return false;

			if (RememberParagraphSpacing && Mathf.Abs(cached.ParagraphSpacing - current.ParagraphSpacing) > tolerance)
				return false;

			if (RememberAlignment && cached.Alignment != current.Alignment)
				return false;

#if UNITY_6000_0_OR_NEWER
			if (RememberWordWrapping && cached.TextWrappingMode != current.TextWrappingMode)
				return false;
#endif

			if (RememberOverflowMode && cached.OverflowMode != current.OverflowMode)
				return false;

			if (RememberMargins && !Vector4sApproximatelyEqual(cached.Margins, current.Margins))
				return false;

			if (RememberMaxVisibleCharacters && cached.MaxVisibleCharacters != current.MaxVisibleCharacters)
				return false;

			if (RememberRichText && cached.RichText != current.RichText)
				return false;

			if (RememberEnabled && cached.Enabled != current.Enabled)
				return false;

			if (RememberAlpha && Mathf.Abs(cached.Alpha - current.Alpha) > tolerance)
				return false;

			return true;
		}

		private static bool ColorsApproximatelyEqual(Color a, Color b, float tolerance = 0.001f)
		{
			return Mathf.Abs(a.r - b.r) <= tolerance &&
			       Mathf.Abs(a.g - b.g) <= tolerance &&
			       Mathf.Abs(a.b - b.b) <= tolerance &&
			       Mathf.Abs(a.a - b.a) <= tolerance;
		}

		private static bool GradientsApproximatelyEqual(VertexGradient a, VertexGradient b)
		{
			return ColorsApproximatelyEqual(a.topLeft, b.topLeft) &&
			       ColorsApproximatelyEqual(a.topRight, b.topRight) &&
			       ColorsApproximatelyEqual(a.bottomLeft, b.bottomLeft) &&
			       ColorsApproximatelyEqual(a.bottomRight, b.bottomRight);
		}

		private static bool Vector4sApproximatelyEqual(Vector4 a, Vector4 b, float tolerance = 0.0001f)
		{
			return Mathf.Abs(a.x - b.x) <= tolerance &&
			       Mathf.Abs(a.y - b.y) <= tolerance &&
			       Mathf.Abs(a.z - b.z) <= tolerance &&
			       Mathf.Abs(a.w - b.w) <= tolerance;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			base.OnDisable();
		}
	}

	/// <summary>
	/// Data structure for TMP_Text serialization.
	/// </summary>
	[MemoryPackable]
	public partial class TMPTextData
	{
		// Text content
		public bool HasText { get; set; }
		public string Text { get; set; }

		// Font properties
		public bool HasFontSize { get; set; }
		public float FontSize { get; set; }

		public bool HasFontStyle { get; set; }
		public FontStyles FontStyle { get; set; }

		public bool HasFontAsset { get; set; }
		public string FontAssetName { get; set; }

		// Color properties
		public bool HasColor { get; set; }
		public Color Color { get; set; }

		public bool HasEnableVertexGradient { get; set; }
		public bool EnableVertexGradient { get; set; }

		public bool HasColorGradient { get; set; }
		public Color GradientTopLeft { get; set; }
		public Color GradientTopRight { get; set; }
		public Color GradientBottomLeft { get; set; }
		public Color GradientBottomRight { get; set; }

		// Spacing properties
		public bool HasCharacterSpacing { get; set; }
		public float CharacterSpacing { get; set; }

		public bool HasWordSpacing { get; set; }
		public float WordSpacing { get; set; }

		public bool HasLineSpacing { get; set; }
		public float LineSpacing { get; set; }

		public bool HasParagraphSpacing { get; set; }
		public float ParagraphSpacing { get; set; }

		// Alignment & Layout
		public bool HasAlignment { get; set; }
		public TextAlignmentOptions Alignment { get; set; }

#if UNITY_6000_0_OR_NEWER
		public bool HasWordWrapping { get; set; }
		public TextWrappingModes TextWrappingMode { get; set; }
#endif

		public bool HasOverflowMode { get; set; }
		public TextOverflowModes OverflowMode { get; set; }

		public bool HasMargins { get; set; }
		public Vector4 Margins { get; set; }

		// Other properties
		public bool HasMaxVisibleCharacters { get; set; }
		public int MaxVisibleCharacters { get; set; }

		public bool HasRichText { get; set; }
		public bool RichText { get; set; }

		public bool HasEnabled { get; set; }
		public bool Enabled { get; set; }

		public bool HasAlpha { get; set; }
		public float Alpha { get; set; }

		public TMPTextData() { }
	}
}
#endif
