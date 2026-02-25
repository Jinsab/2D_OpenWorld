#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Saves and restores TMP_InputField component properties.
	/// </summary>
	[AddComponentMenu("Crystal Save/Remember Components/Remember TMP InputField")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(TMP_InputField))]
	public class RememberTMPInputField : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of component references to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Header("Save Optimization")]
		[Tooltip("Skip serialization when the captured data did not change since the last save.")]
		[SerializeField] private bool skipSavingWhenUnchanged = false;

		[Header("Text Content")]
		[Tooltip("Remember the input field text content.")]
		public bool RememberText = true;

		[Header("Input Field Properties")]
		[Tooltip("Remember the interactable state.")]
		public bool RememberInteractable = true;

		[Tooltip("Remember the read-only state.")]
		public bool RememberReadOnly = false;

		[Tooltip("Remember the rich text enabled state.")]
		public bool RememberRichText = false;

		[Tooltip("Remember the character limit.")]
		public bool RememberCharacterLimit = false;

		[Tooltip("Remember the content type.")]
		public bool RememberContentType = false;

		[Tooltip("Remember the line type.")]
		public bool RememberLineType = false;

		[Tooltip("Remember the input type.")]
		public bool RememberInputType = false;

		[Tooltip("Remember the keyboard type.")]
		public bool RememberKeyboardType = false;

		[Tooltip("Remember the character validation type.")]
		public bool RememberCharacterValidation = false;

		[Header("Caret Properties")]
		[Tooltip("Remember the caret blink rate.")]
		public bool RememberCaretBlinkRate = false;

		[Tooltip("Remember the caret width.")]
		public bool RememberCaretWidth = false;

		[Tooltip("Remember the caret color.")]
		public bool RememberCaretColor = false;

		[Tooltip("Remember custom caret color enabled state.")]
		public bool RememberCustomCaretColor = false;

		[Header("Selection Properties")]
		[Tooltip("Remember the selection color.")]
		public bool RememberSelectionColor = false;

		[Header("Placeholder Properties")]
		[Tooltip("Remember the placeholder text.")]
		public bool RememberPlaceholderText = false;

		[Tooltip("Remember the placeholder text color.")]
		public bool RememberPlaceholderColor = false;

		[Header("Text Component Properties")]
		[Tooltip("Remember the text component font size.")]
		public bool RememberFontSize = false;

		[Tooltip("Remember the text component color.")]
		public bool RememberTextColor = false;

		[Header("Colors")]
		[Tooltip("Remember the selectable colors (normal, highlighted, pressed, etc.).")]
		public bool RememberColors = false;

		[Header("Load Behaviour")]
		[Tooltip("Apply the loaded text without notifying listeners.")]
		[SerializeField] private bool applyWithoutNotify = true;

		private TMP_InputField inputField;
		private TMPInputFieldSnapshot cachedSnapshot;
		private bool hasCachedSnapshot;
		private byte[] cachedSerializedData;

		private struct TMPInputFieldSnapshot
		{
			public string Text;
			public bool Interactable;
			public bool ReadOnly;
			public bool RichText;
			public int CharacterLimit;
			public TMP_InputField.ContentType ContentType;
			public TMP_InputField.LineType LineType;
			public TMP_InputField.InputType InputType;
			public TouchScreenKeyboardType KeyboardType;
			public TMP_InputField.CharacterValidation CharacterValidation;
			public float CaretBlinkRate;
			public int CaretWidth;
			public Color CaretColor;
			public bool CustomCaretColor;
			public Color SelectionColor;
			public string PlaceholderText;
			public Color PlaceholderColor;
			public float FontSize;
			public Color TextColor;
			public ColorBlock Colors;

			public TMPInputFieldSnapshot Clone()
			{
				return new TMPInputFieldSnapshot
				{
					Text = Text,
					Interactable = Interactable,
					ReadOnly = ReadOnly,
					RichText = RichText,
					CharacterLimit = CharacterLimit,
					ContentType = ContentType,
					LineType = LineType,
					InputType = InputType,
					KeyboardType = KeyboardType,
					CharacterValidation = CharacterValidation,
					CaretBlinkRate = CaretBlinkRate,
					CaretWidth = CaretWidth,
					CaretColor = CaretColor,
					CustomCaretColor = CustomCaretColor,
					SelectionColor = SelectionColor,
					PlaceholderText = PlaceholderText,
					PlaceholderColor = PlaceholderColor,
					FontSize = FontSize,
					TextColor = TextColor,
					Colors = Colors
				};
			}
		}

		protected override void Awake()
		{
			base.Awake();
			inputField = GetComponent<TMP_InputField>();

			if (inputField == null)
			{
				Logger.Log($"RememberTMPInputField: No TMP_InputField component found on '{gameObject.name}'. Disabling component.", LogCategory.RememberTMPInputField, LogLevel.Warning);
				enabled = false;
				return;
			}

			if (skipSavingWhenUnchanged && TryCaptureCurrentState(out TMPInputFieldSnapshot snapshot, false))
			{
				cachedSnapshot = snapshot.Clone();
				hasCachedSnapshot = true;
			}
		}

		protected override byte[] SerializeComponentData()
		{
			if (!TryCaptureCurrentState(out TMPInputFieldSnapshot currentSnapshot, true))
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

			TMPInputFieldData data = ConvertSnapshotToData(currentSnapshot);

			try
			{
				byte[] serializedData = Serializer.Serialize<TMPInputFieldData>(data);
				Logger.Log($"RememberTMPInputField: Successfully serialized input field data for '{gameObject.name}'.", LogCategory.RememberTMPInputField, LogLevel.Info);

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
				Logger.Log($"RememberTMPInputField: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberTMPInputField, LogLevel.Error);
				return null;
			}
		}

		protected override void DeserializeComponentData(byte[] data)
		{
			TMP_InputField field = enablePerformanceCaching ? inputField : GetComponent<TMP_InputField>();

			if (field == null)
			{
				Logger.Log($"DeserializeComponentData: No TMP_InputField on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberTMPInputField, LogLevel.Warning);
				return;
			}

			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberTMPInputField, LogLevel.Warning);
				return;
			}

			try
			{
				TMPInputFieldData deserializedData = Serializer.Deserialize<TMPInputFieldData>(data);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberTMPInputField, LogLevel.Warning);
					return;
				}

				if (RememberInteractable && deserializedData.HasInteractable)
				{
					field.interactable = deserializedData.Interactable;
				}

				if (RememberReadOnly && deserializedData.HasReadOnly)
				{
					field.readOnly = deserializedData.ReadOnly;
				}

				if (RememberRichText && deserializedData.HasRichText)
				{
					field.richText = deserializedData.RichText;
				}

				if (RememberCharacterLimit && deserializedData.HasCharacterLimit)
				{
					field.characterLimit = deserializedData.CharacterLimit;
				}

				if (RememberContentType && deserializedData.HasContentType)
				{
					field.contentType = deserializedData.ContentType;
				}

				if (RememberLineType && deserializedData.HasLineType)
				{
					field.lineType = deserializedData.LineType;
				}

				if (RememberInputType && deserializedData.HasInputType)
				{
					field.inputType = deserializedData.InputType;
				}

				if (RememberKeyboardType && deserializedData.HasKeyboardType)
				{
					field.keyboardType = deserializedData.KeyboardType;
				}

				if (RememberCharacterValidation && deserializedData.HasCharacterValidation)
				{
					field.characterValidation = deserializedData.CharacterValidation;
				}

				if (RememberCaretBlinkRate && deserializedData.HasCaretBlinkRate)
				{
					field.caretBlinkRate = deserializedData.CaretBlinkRate;
				}

				if (RememberCaretWidth && deserializedData.HasCaretWidth)
				{
					field.caretWidth = deserializedData.CaretWidth;
				}

				if (RememberCustomCaretColor && deserializedData.HasCustomCaretColor)
				{
					field.customCaretColor = deserializedData.CustomCaretColor;
				}

				if (RememberCaretColor && deserializedData.HasCaretColor)
				{
					field.caretColor = deserializedData.CaretColor;
				}

				if (RememberSelectionColor && deserializedData.HasSelectionColor)
				{
					field.selectionColor = deserializedData.SelectionColor;
				}

				if (RememberColors && deserializedData.HasColors)
				{
					ColorBlock colors = field.colors;
					colors.normalColor = deserializedData.NormalColor;
					colors.highlightedColor = deserializedData.HighlightedColor;
					colors.pressedColor = deserializedData.PressedColor;
					colors.selectedColor = deserializedData.SelectedColor;
					colors.disabledColor = deserializedData.DisabledColor;
					colors.colorMultiplier = deserializedData.ColorMultiplier;
					colors.fadeDuration = deserializedData.FadeDuration;
					field.colors = colors;
				}

				// Placeholder
				if (field.placeholder != null && field.placeholder is TMP_Text placeholderText)
				{
					if (RememberPlaceholderText && deserializedData.HasPlaceholderText)
					{
						placeholderText.text = deserializedData.PlaceholderText;
					}

					if (RememberPlaceholderColor && deserializedData.HasPlaceholderColor)
					{
						placeholderText.color = deserializedData.PlaceholderColor;
					}
				}

				// Text component
				if (field.textComponent != null)
				{
					if (RememberFontSize && deserializedData.HasFontSize)
					{
						field.textComponent.fontSize = deserializedData.FontSize;
					}

					if (RememberTextColor && deserializedData.HasTextColor)
					{
						field.textComponent.color = deserializedData.TextColor;
					}
				}

				// Text content (apply last to respect any content type changes)
				if (RememberText && deserializedData.HasText)
				{
					if (applyWithoutNotify)
					{
						field.SetTextWithoutNotify(deserializedData.Text);
					}
					else
					{
						field.text = deserializedData.Text;
					}
				}

				if (skipSavingWhenUnchanged)
				{
					if (TryCaptureCurrentState(out TMPInputFieldSnapshot snapshot, false))
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

				Logger.Log($"RememberTMPInputField: Successfully loaded input field data for '{gameObject.name}'.", LogCategory.RememberTMPInputField, LogLevel.Info);
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberTMPInputField: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberTMPInputField, LogLevel.Error);
			}
		}

		private bool TryCaptureCurrentState(out TMPInputFieldSnapshot snapshot, bool logWarnings)
		{
			snapshot = default;

			TMP_InputField field = enablePerformanceCaching ? inputField : GetComponent<TMP_InputField>();

			if (field == null)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentState: No TMP_InputField on '{gameObject.name}'. Skipping.", LogCategory.RememberTMPInputField, LogLevel.Warning);
				}
				return false;
			}

			TMPInputFieldSnapshot tempSnapshot = new TMPInputFieldSnapshot();
			bool capturedAny = false;

			if (RememberText)
			{
				tempSnapshot.Text = field.text;
				capturedAny = true;
			}

			if (RememberInteractable)
			{
				tempSnapshot.Interactable = field.interactable;
				capturedAny = true;
			}

			if (RememberReadOnly)
			{
				tempSnapshot.ReadOnly = field.readOnly;
				capturedAny = true;
			}

			if (RememberRichText)
			{
				tempSnapshot.RichText = field.richText;
				capturedAny = true;
			}

			if (RememberCharacterLimit)
			{
				tempSnapshot.CharacterLimit = field.characterLimit;
				capturedAny = true;
			}

			if (RememberContentType)
			{
				tempSnapshot.ContentType = field.contentType;
				capturedAny = true;
			}

			if (RememberLineType)
			{
				tempSnapshot.LineType = field.lineType;
				capturedAny = true;
			}

			if (RememberInputType)
			{
				tempSnapshot.InputType = field.inputType;
				capturedAny = true;
			}

			if (RememberKeyboardType)
			{
				tempSnapshot.KeyboardType = field.keyboardType;
				capturedAny = true;
			}

			if (RememberCharacterValidation)
			{
				tempSnapshot.CharacterValidation = field.characterValidation;
				capturedAny = true;
			}

			if (RememberCaretBlinkRate)
			{
				tempSnapshot.CaretBlinkRate = field.caretBlinkRate;
				capturedAny = true;
			}

			if (RememberCaretWidth)
			{
				tempSnapshot.CaretWidth = field.caretWidth;
				capturedAny = true;
			}

			if (RememberCaretColor)
			{
				tempSnapshot.CaretColor = field.caretColor;
				capturedAny = true;
			}

			if (RememberCustomCaretColor)
			{
				tempSnapshot.CustomCaretColor = field.customCaretColor;
				capturedAny = true;
			}

			if (RememberSelectionColor)
			{
				tempSnapshot.SelectionColor = field.selectionColor;
				capturedAny = true;
			}

			if (RememberColors)
			{
				tempSnapshot.Colors = field.colors;
				capturedAny = true;
			}

			if (field.placeholder != null && field.placeholder is TMP_Text placeholderText)
			{
				if (RememberPlaceholderText)
				{
					tempSnapshot.PlaceholderText = placeholderText.text;
					capturedAny = true;
				}

				if (RememberPlaceholderColor)
				{
					tempSnapshot.PlaceholderColor = placeholderText.color;
					capturedAny = true;
				}
			}

			if (field.textComponent != null)
			{
				if (RememberFontSize)
				{
					tempSnapshot.FontSize = field.textComponent.fontSize;
					capturedAny = true;
				}

				if (RememberTextColor)
				{
					tempSnapshot.TextColor = field.textComponent.color;
					capturedAny = true;
				}
			}

			if (!capturedAny)
			{
				return false;
			}

			snapshot = tempSnapshot;
			return true;
		}

		private TMPInputFieldData ConvertSnapshotToData(TMPInputFieldSnapshot snapshot)
		{
			TMPInputFieldData data = new TMPInputFieldData();

			if (RememberText)
			{
				data.Text = snapshot.Text;
				data.HasText = true;
			}

			if (RememberInteractable)
			{
				data.Interactable = snapshot.Interactable;
				data.HasInteractable = true;
			}

			if (RememberReadOnly)
			{
				data.ReadOnly = snapshot.ReadOnly;
				data.HasReadOnly = true;
			}

			if (RememberRichText)
			{
				data.RichText = snapshot.RichText;
				data.HasRichText = true;
			}

			if (RememberCharacterLimit)
			{
				data.CharacterLimit = snapshot.CharacterLimit;
				data.HasCharacterLimit = true;
			}

			if (RememberContentType)
			{
				data.ContentType = snapshot.ContentType;
				data.HasContentType = true;
			}

			if (RememberLineType)
			{
				data.LineType = snapshot.LineType;
				data.HasLineType = true;
			}

			if (RememberInputType)
			{
				data.InputType = snapshot.InputType;
				data.HasInputType = true;
			}

			if (RememberKeyboardType)
			{
				data.KeyboardType = snapshot.KeyboardType;
				data.HasKeyboardType = true;
			}

			if (RememberCharacterValidation)
			{
				data.CharacterValidation = snapshot.CharacterValidation;
				data.HasCharacterValidation = true;
			}

			if (RememberCaretBlinkRate)
			{
				data.CaretBlinkRate = snapshot.CaretBlinkRate;
				data.HasCaretBlinkRate = true;
			}

			if (RememberCaretWidth)
			{
				data.CaretWidth = snapshot.CaretWidth;
				data.HasCaretWidth = true;
			}

			if (RememberCaretColor)
			{
				data.CaretColor = snapshot.CaretColor;
				data.HasCaretColor = true;
			}

			if (RememberCustomCaretColor)
			{
				data.CustomCaretColor = snapshot.CustomCaretColor;
				data.HasCustomCaretColor = true;
			}

			if (RememberSelectionColor)
			{
				data.SelectionColor = snapshot.SelectionColor;
				data.HasSelectionColor = true;
			}

			if (RememberColors)
			{
				data.NormalColor = snapshot.Colors.normalColor;
				data.HighlightedColor = snapshot.Colors.highlightedColor;
				data.PressedColor = snapshot.Colors.pressedColor;
				data.SelectedColor = snapshot.Colors.selectedColor;
				data.DisabledColor = snapshot.Colors.disabledColor;
				data.ColorMultiplier = snapshot.Colors.colorMultiplier;
				data.FadeDuration = snapshot.Colors.fadeDuration;
				data.HasColors = true;
			}

			if (RememberPlaceholderText)
			{
				data.PlaceholderText = snapshot.PlaceholderText;
				data.HasPlaceholderText = true;
			}

			if (RememberPlaceholderColor)
			{
				data.PlaceholderColor = snapshot.PlaceholderColor;
				data.HasPlaceholderColor = true;
			}

			if (RememberFontSize)
			{
				data.FontSize = snapshot.FontSize;
				data.HasFontSize = true;
			}

			if (RememberTextColor)
			{
				data.TextColor = snapshot.TextColor;
				data.HasTextColor = true;
			}

			return data;
		}

		private bool AreEquivalent(TMPInputFieldSnapshot cached, TMPInputFieldSnapshot current)
		{
			const float tolerance = 0.0001f;

			if (RememberText && !string.Equals(cached.Text, current.Text, StringComparison.Ordinal))
				return false;

			if (RememberInteractable && cached.Interactable != current.Interactable)
				return false;

			if (RememberReadOnly && cached.ReadOnly != current.ReadOnly)
				return false;

			if (RememberRichText && cached.RichText != current.RichText)
				return false;

			if (RememberCharacterLimit && cached.CharacterLimit != current.CharacterLimit)
				return false;

			if (RememberContentType && cached.ContentType != current.ContentType)
				return false;

			if (RememberLineType && cached.LineType != current.LineType)
				return false;

			if (RememberInputType && cached.InputType != current.InputType)
				return false;

			if (RememberKeyboardType && cached.KeyboardType != current.KeyboardType)
				return false;

			if (RememberCharacterValidation && cached.CharacterValidation != current.CharacterValidation)
				return false;

			if (RememberCaretBlinkRate && Mathf.Abs(cached.CaretBlinkRate - current.CaretBlinkRate) > tolerance)
				return false;

			if (RememberCaretWidth && cached.CaretWidth != current.CaretWidth)
				return false;

			if (RememberCaretColor && !ColorsApproximatelyEqual(cached.CaretColor, current.CaretColor))
				return false;

			if (RememberCustomCaretColor && cached.CustomCaretColor != current.CustomCaretColor)
				return false;

			if (RememberSelectionColor && !ColorsApproximatelyEqual(cached.SelectionColor, current.SelectionColor))
				return false;

			if (RememberColors && !ColorBlocksApproximatelyEqual(cached.Colors, current.Colors))
				return false;

			if (RememberPlaceholderText && !string.Equals(cached.PlaceholderText, current.PlaceholderText, StringComparison.Ordinal))
				return false;

			if (RememberPlaceholderColor && !ColorsApproximatelyEqual(cached.PlaceholderColor, current.PlaceholderColor))
				return false;

			if (RememberFontSize && Mathf.Abs(cached.FontSize - current.FontSize) > tolerance)
				return false;

			if (RememberTextColor && !ColorsApproximatelyEqual(cached.TextColor, current.TextColor))
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

		private static bool ColorBlocksApproximatelyEqual(ColorBlock a, ColorBlock b)
		{
			return ColorsApproximatelyEqual(a.normalColor, b.normalColor) &&
			       ColorsApproximatelyEqual(a.highlightedColor, b.highlightedColor) &&
			       ColorsApproximatelyEqual(a.pressedColor, b.pressedColor) &&
			       ColorsApproximatelyEqual(a.selectedColor, b.selectedColor) &&
			       ColorsApproximatelyEqual(a.disabledColor, b.disabledColor) &&
			       Mathf.Approximately(a.colorMultiplier, b.colorMultiplier) &&
			       Mathf.Approximately(a.fadeDuration, b.fadeDuration);
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
	/// Data structure for TMP_InputField serialization.
	/// </summary>
	[MemoryPackable]
	public partial class TMPInputFieldData
	{
		// Text content
		public bool HasText { get; set; }
		public string Text { get; set; }

		// Input field properties
		public bool HasInteractable { get; set; }
		public bool Interactable { get; set; }

		public bool HasReadOnly { get; set; }
		public bool ReadOnly { get; set; }

		public bool HasRichText { get; set; }
		public bool RichText { get; set; }

		public bool HasCharacterLimit { get; set; }
		public int CharacterLimit { get; set; }

		public bool HasContentType { get; set; }
		public TMP_InputField.ContentType ContentType { get; set; }

		public bool HasLineType { get; set; }
		public TMP_InputField.LineType LineType { get; set; }

		public bool HasInputType { get; set; }
		public TMP_InputField.InputType InputType { get; set; }

		public bool HasKeyboardType { get; set; }
		public TouchScreenKeyboardType KeyboardType { get; set; }

		public bool HasCharacterValidation { get; set; }
		public TMP_InputField.CharacterValidation CharacterValidation { get; set; }

		// Caret properties
		public bool HasCaretBlinkRate { get; set; }
		public float CaretBlinkRate { get; set; }

		public bool HasCaretWidth { get; set; }
		public int CaretWidth { get; set; }

		public bool HasCaretColor { get; set; }
		public Color CaretColor { get; set; }

		public bool HasCustomCaretColor { get; set; }
		public bool CustomCaretColor { get; set; }

		// Selection
		public bool HasSelectionColor { get; set; }
		public Color SelectionColor { get; set; }

		// Colors
		public bool HasColors { get; set; }
		public Color NormalColor { get; set; }
		public Color HighlightedColor { get; set; }
		public Color PressedColor { get; set; }
		public Color SelectedColor { get; set; }
		public Color DisabledColor { get; set; }
		public float ColorMultiplier { get; set; }
		public float FadeDuration { get; set; }

		// Placeholder
		public bool HasPlaceholderText { get; set; }
		public string PlaceholderText { get; set; }

		public bool HasPlaceholderColor { get; set; }
		public Color PlaceholderColor { get; set; }

		// Text component
		public bool HasFontSize { get; set; }
		public float FontSize { get; set; }

		public bool HasTextColor { get; set; }
		public Color TextColor { get; set; }

		public TMPInputFieldData() { }
	}
}
#endif
