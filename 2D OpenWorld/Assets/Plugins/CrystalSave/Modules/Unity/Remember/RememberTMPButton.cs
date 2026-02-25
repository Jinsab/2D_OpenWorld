#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Saves and restores Button component properties along with its TMP text child.
	/// </summary>
	[AddComponentMenu("Crystal Save/Remember Components/Remember TMP Button")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Button))]
	public class RememberTMPButton : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of component references to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Header("Save Optimization")]
		[Tooltip("Skip serialization when the captured data did not change since the last save.")]
		[SerializeField] private bool skipSavingWhenUnchanged = false;

		[Header("Button Properties")]
		[Tooltip("Remember the interactable state.")]
		public bool RememberInteractable = true;

		[Tooltip("Remember the button transition type.")]
		public bool RememberTransition = false;

		[Tooltip("Remember the button colors (normal, highlighted, pressed, selected, disabled).")]
		public bool RememberColors = false;

		[Header("Associated Text")]
		[Tooltip("Remember the button's child TMP_Text component text content.")]
		public bool RememberButtonText = true;

		[Tooltip("Remember the button's child TMP_Text font size.")]
		public bool RememberButtonTextFontSize = false;

		[Tooltip("Remember the button's child TMP_Text color.")]
		public bool RememberButtonTextColor = false;

		[Header("Text Reference")]
		[Tooltip("Optional: Explicitly assign the TMP_Text component. If null, will search in children.")]
		[SerializeField] private TMP_Text buttonTextComponent;

		private Button button;
		private TMP_Text buttonText;
		private TMPButtonSnapshot cachedSnapshot;
		private bool hasCachedSnapshot;
		private byte[] cachedSerializedData;

		private struct TMPButtonSnapshot
		{
			public bool Interactable;
			public Selectable.Transition Transition;
			public ColorBlock Colors;
			public string ButtonText;
			public float ButtonTextFontSize;
			public Color ButtonTextColor;

			public TMPButtonSnapshot Clone()
			{
				return new TMPButtonSnapshot
				{
					Interactable = Interactable,
					Transition = Transition,
					Colors = Colors,
					ButtonText = ButtonText,
					ButtonTextFontSize = ButtonTextFontSize,
					ButtonTextColor = ButtonTextColor
				};
			}
		}

		protected override void Awake()
		{
			base.Awake();
			button = GetComponent<Button>();

			if (button == null)
			{
				Logger.Log($"RememberTMPButton: No Button component found on '{gameObject.name}'. Disabling component.", LogCategory.RememberTMPButton, LogLevel.Warning);
				enabled = false;
				return;
			}

			// Find or use assigned TMP_Text
			buttonText = buttonTextComponent != null ? buttonTextComponent : GetComponentInChildren<TMP_Text>();

			if (skipSavingWhenUnchanged && TryCaptureCurrentState(out TMPButtonSnapshot snapshot, false))
			{
				cachedSnapshot = snapshot.Clone();
				hasCachedSnapshot = true;
			}
		}

		protected override byte[] SerializeComponentData()
		{
			if (!TryCaptureCurrentState(out TMPButtonSnapshot currentSnapshot, true))
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

			TMPButtonData data = ConvertSnapshotToData(currentSnapshot);

			try
			{
				byte[] serializedData = Serializer.Serialize<TMPButtonData>(data);
				Logger.Log($"RememberTMPButton: Successfully serialized button data for '{gameObject.name}'.", LogCategory.RememberTMPButton, LogLevel.Info);

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
				Logger.Log($"RememberTMPButton: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberTMPButton, LogLevel.Error);
				return null;
			}
		}

		protected override void DeserializeComponentData(byte[] data)
		{
			Button btn = enablePerformanceCaching ? button : GetComponent<Button>();

			if (btn == null)
			{
				Logger.Log($"DeserializeComponentData: No Button on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberTMPButton, LogLevel.Warning);
				return;
			}

			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberTMPButton, LogLevel.Warning);
				return;
			}

			try
			{
				TMPButtonData deserializedData = Serializer.Deserialize<TMPButtonData>(data);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberTMPButton, LogLevel.Warning);
					return;
				}

				if (RememberInteractable && deserializedData.HasInteractable)
				{
					btn.interactable = deserializedData.Interactable;
				}

				if (RememberTransition && deserializedData.HasTransition)
				{
					btn.transition = deserializedData.Transition;
				}

				if (RememberColors && deserializedData.HasColors)
				{
					ColorBlock colors = btn.colors;
					colors.normalColor = deserializedData.NormalColor;
					colors.highlightedColor = deserializedData.HighlightedColor;
					colors.pressedColor = deserializedData.PressedColor;
					colors.selectedColor = deserializedData.SelectedColor;
					colors.disabledColor = deserializedData.DisabledColor;
					colors.colorMultiplier = deserializedData.ColorMultiplier;
					colors.fadeDuration = deserializedData.FadeDuration;
					btn.colors = colors;
				}

				// Get text component
				TMP_Text text = enablePerformanceCaching ? buttonText : (buttonTextComponent != null ? buttonTextComponent : GetComponentInChildren<TMP_Text>());

				if (text != null)
				{
					if (RememberButtonText && deserializedData.HasButtonText)
					{
						text.text = deserializedData.ButtonText;
					}

					if (RememberButtonTextFontSize && deserializedData.HasButtonTextFontSize)
					{
						text.fontSize = deserializedData.ButtonTextFontSize;
					}

					if (RememberButtonTextColor && deserializedData.HasButtonTextColor)
					{
						text.color = deserializedData.ButtonTextColor;
					}
				}

				if (skipSavingWhenUnchanged)
				{
					if (TryCaptureCurrentState(out TMPButtonSnapshot snapshot, false))
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

				Logger.Log($"RememberTMPButton: Successfully loaded button data for '{gameObject.name}'.", LogCategory.RememberTMPButton, LogLevel.Info);
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberTMPButton: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberTMPButton, LogLevel.Error);
			}
		}

		private bool TryCaptureCurrentState(out TMPButtonSnapshot snapshot, bool logWarnings)
		{
			snapshot = default;

			Button btn = enablePerformanceCaching ? button : GetComponent<Button>();

			if (btn == null)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentState: No Button on '{gameObject.name}'. Skipping.", LogCategory.RememberTMPButton, LogLevel.Warning);
				}
				return false;
			}

			TMPButtonSnapshot tempSnapshot = new TMPButtonSnapshot();
			bool capturedAny = false;

			if (RememberInteractable)
			{
				tempSnapshot.Interactable = btn.interactable;
				capturedAny = true;
			}

			if (RememberTransition)
			{
				tempSnapshot.Transition = btn.transition;
				capturedAny = true;
			}

			if (RememberColors)
			{
				tempSnapshot.Colors = btn.colors;
				capturedAny = true;
			}

			TMP_Text text = enablePerformanceCaching ? buttonText : (buttonTextComponent != null ? buttonTextComponent : GetComponentInChildren<TMP_Text>());

			if (text != null)
			{
				if (RememberButtonText)
				{
					tempSnapshot.ButtonText = text.text;
					capturedAny = true;
				}

				if (RememberButtonTextFontSize)
				{
					tempSnapshot.ButtonTextFontSize = text.fontSize;
					capturedAny = true;
				}

				if (RememberButtonTextColor)
				{
					tempSnapshot.ButtonTextColor = text.color;
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

		private TMPButtonData ConvertSnapshotToData(TMPButtonSnapshot snapshot)
		{
			TMPButtonData data = new TMPButtonData();

			if (RememberInteractable)
			{
				data.Interactable = snapshot.Interactable;
				data.HasInteractable = true;
			}

			if (RememberTransition)
			{
				data.Transition = snapshot.Transition;
				data.HasTransition = true;
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

			if (RememberButtonText)
			{
				data.ButtonText = snapshot.ButtonText;
				data.HasButtonText = true;
			}

			if (RememberButtonTextFontSize)
			{
				data.ButtonTextFontSize = snapshot.ButtonTextFontSize;
				data.HasButtonTextFontSize = true;
			}

			if (RememberButtonTextColor)
			{
				data.ButtonTextColor = snapshot.ButtonTextColor;
				data.HasButtonTextColor = true;
			}

			return data;
		}

		private bool AreEquivalent(TMPButtonSnapshot cached, TMPButtonSnapshot current)
		{
			const float tolerance = 0.0001f;

			if (RememberInteractable && cached.Interactable != current.Interactable)
				return false;

			if (RememberTransition && cached.Transition != current.Transition)
				return false;

			if (RememberColors && !ColorBlocksApproximatelyEqual(cached.Colors, current.Colors))
				return false;

			if (RememberButtonText && !string.Equals(cached.ButtonText, current.ButtonText, StringComparison.Ordinal))
				return false;

			if (RememberButtonTextFontSize && Mathf.Abs(cached.ButtonTextFontSize - current.ButtonTextFontSize) > tolerance)
				return false;

			if (RememberButtonTextColor && !ColorsApproximatelyEqual(cached.ButtonTextColor, current.ButtonTextColor))
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
	/// Data structure for Button with TMP serialization.
	/// </summary>
	[MemoryPackable]
	public partial class TMPButtonData
	{
		// Button properties
		public bool HasInteractable { get; set; }
		public bool Interactable { get; set; }

		public bool HasTransition { get; set; }
		public Selectable.Transition Transition { get; set; }

		// Color block
		public bool HasColors { get; set; }
		public Color NormalColor { get; set; }
		public Color HighlightedColor { get; set; }
		public Color PressedColor { get; set; }
		public Color SelectedColor { get; set; }
		public Color DisabledColor { get; set; }
		public float ColorMultiplier { get; set; }
		public float FadeDuration { get; set; }

		// Text properties
		public bool HasButtonText { get; set; }
		public string ButtonText { get; set; }

		public bool HasButtonTextFontSize { get; set; }
		public float ButtonTextFontSize { get; set; }

		public bool HasButtonTextColor { get; set; }
		public Color ButtonTextColor { get; set; }

		public TMPButtonData() { }
	}
}
#endif
