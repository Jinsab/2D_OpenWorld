#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Saves and restores TMP_Dropdown component properties including options and selected value.
	/// </summary>
	[AddComponentMenu("Crystal Save/Remember Components/Remember TMP Dropdown")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(TMP_Dropdown))]
	public class RememberTMPDropdown : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of component references to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Header("Save Optimization")]
		[Tooltip("Skip serialization when the captured data did not change since the last save.")]
		[SerializeField] private bool skipSavingWhenUnchanged = false;

		[Header("Dropdown Properties")]
		[Tooltip("Remember the selected value (index).")]
		public bool RememberValue = true;

		[Tooltip("Remember the interactable state.")]
		public bool RememberInteractable = true;

		[Tooltip("Remember the list of dropdown options (text only).")]
		public bool RememberOptions = false;

		[Tooltip("Remember the dropdown colors.")]
		public bool RememberColors = false;

		[Header("Caption Text")]
		[Tooltip("Remember the caption text content.")]
		public bool RememberCaptionText = false;

		[Tooltip("Remember the caption text font size.")]
		public bool RememberCaptionFontSize = false;

		[Tooltip("Remember the caption text color.")]
		public bool RememberCaptionColor = false;

		[Header("Item Text")]
		[Tooltip("Remember the item text font size (template).")]
		public bool RememberItemFontSize = false;

		[Tooltip("Remember the item text color (template).")]
		public bool RememberItemColor = false;

		[Header("Load Behaviour")]
		[Tooltip("Apply the loaded value without notifying listeners.")]
		[SerializeField] private bool applyWithoutNotify = true;

		private TMP_Dropdown dropdown;
		private TMPDropdownSnapshot cachedSnapshot;
		private bool hasCachedSnapshot;
		private byte[] cachedSerializedData;

		private struct TMPDropdownSnapshot
		{
			public int Value;
			public bool Interactable;
			public List<string> Options;
			public ColorBlock Colors;
			public string CaptionText;
			public float CaptionFontSize;
			public Color CaptionColor;
			public float ItemFontSize;
			public Color ItemColor;

			public TMPDropdownSnapshot Clone()
			{
				return new TMPDropdownSnapshot
				{
					Value = Value,
					Interactable = Interactable,
					Options = Options != null ? new List<string>(Options) : null,
					Colors = Colors,
					CaptionText = CaptionText,
					CaptionFontSize = CaptionFontSize,
					CaptionColor = CaptionColor,
					ItemFontSize = ItemFontSize,
					ItemColor = ItemColor
				};
			}
		}

		protected override void Awake()
		{
			base.Awake();
			dropdown = GetComponent<TMP_Dropdown>();

			if (dropdown == null)
			{
				Logger.Log($"RememberTMPDropdown: No TMP_Dropdown component found on '{gameObject.name}'. Disabling component.", LogCategory.RememberTMPDropdown, LogLevel.Warning);
				enabled = false;
				return;
			}

			if (skipSavingWhenUnchanged && TryCaptureCurrentState(out TMPDropdownSnapshot snapshot, false))
			{
				cachedSnapshot = snapshot.Clone();
				hasCachedSnapshot = true;
			}
		}

		protected override byte[] SerializeComponentData()
		{
			if (!TryCaptureCurrentState(out TMPDropdownSnapshot currentSnapshot, true))
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

			TMPDropdownData data = ConvertSnapshotToData(currentSnapshot);

			try
			{
				byte[] serializedData = Serializer.Serialize<TMPDropdownData>(data);
				Logger.Log($"RememberTMPDropdown: Successfully serialized dropdown data for '{gameObject.name}'.", LogCategory.RememberTMPDropdown, LogLevel.Info);

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
				Logger.Log($"RememberTMPDropdown: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberTMPDropdown, LogLevel.Error);
				return null;
			}
		}

		protected override void DeserializeComponentData(byte[] data)
		{
			TMP_Dropdown dd = enablePerformanceCaching ? dropdown : GetComponent<TMP_Dropdown>();

			if (dd == null)
			{
				Logger.Log($"DeserializeComponentData: No TMP_Dropdown on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberTMPDropdown, LogLevel.Warning);
				return;
			}

			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberTMPDropdown, LogLevel.Warning);
				return;
			}

			try
			{
				TMPDropdownData deserializedData = Serializer.Deserialize<TMPDropdownData>(data);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberTMPDropdown, LogLevel.Warning);
					return;
				}

				if (RememberInteractable && deserializedData.HasInteractable)
				{
					dd.interactable = deserializedData.Interactable;
				}

				if (RememberColors && deserializedData.HasColors)
				{
					ColorBlock colors = dd.colors;
					colors.normalColor = deserializedData.NormalColor;
					colors.highlightedColor = deserializedData.HighlightedColor;
					colors.pressedColor = deserializedData.PressedColor;
					colors.selectedColor = deserializedData.SelectedColor;
					colors.disabledColor = deserializedData.DisabledColor;
					colors.colorMultiplier = deserializedData.ColorMultiplier;
					colors.fadeDuration = deserializedData.FadeDuration;
					dd.colors = colors;
				}

				// Options must be restored before value to ensure valid index
				if (RememberOptions && deserializedData.HasOptions && deserializedData.Options != null)
				{
					dd.ClearOptions();
					dd.AddOptions(deserializedData.Options);
				}

				if (RememberValue && deserializedData.HasValue)
				{
					int maxIndex = dd.options.Count - 1;
					int valueToSet = Mathf.Clamp(deserializedData.Value, 0, Mathf.Max(0, maxIndex));

					if (applyWithoutNotify)
					{
						dd.SetValueWithoutNotify(valueToSet);
					}
					else
					{
						dd.value = valueToSet;
					}
				}

				// Caption text properties
				if (dd.captionText != null)
				{
					if (RememberCaptionText && deserializedData.HasCaptionText)
					{
						dd.captionText.text = deserializedData.CaptionText;
					}

					if (RememberCaptionFontSize && deserializedData.HasCaptionFontSize)
					{
						dd.captionText.fontSize = deserializedData.CaptionFontSize;
					}

					if (RememberCaptionColor && deserializedData.HasCaptionColor)
					{
						dd.captionText.color = deserializedData.CaptionColor;
					}
				}

				// Item text properties (from template)
				if (dd.itemText != null)
				{
					if (RememberItemFontSize && deserializedData.HasItemFontSize)
					{
						dd.itemText.fontSize = deserializedData.ItemFontSize;
					}

					if (RememberItemColor && deserializedData.HasItemColor)
					{
						dd.itemText.color = deserializedData.ItemColor;
					}
				}

				if (skipSavingWhenUnchanged)
				{
					if (TryCaptureCurrentState(out TMPDropdownSnapshot snapshot, false))
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

				Logger.Log($"RememberTMPDropdown: Successfully loaded dropdown data for '{gameObject.name}'.", LogCategory.RememberTMPDropdown, LogLevel.Info);
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberTMPDropdown: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberTMPDropdown, LogLevel.Error);
			}
		}

		private bool TryCaptureCurrentState(out TMPDropdownSnapshot snapshot, bool logWarnings)
		{
			snapshot = default;

			TMP_Dropdown dd = enablePerformanceCaching ? dropdown : GetComponent<TMP_Dropdown>();

			if (dd == null)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentState: No TMP_Dropdown on '{gameObject.name}'. Skipping.", LogCategory.RememberTMPDropdown, LogLevel.Warning);
				}
				return false;
			}

			TMPDropdownSnapshot tempSnapshot = new TMPDropdownSnapshot();
			bool capturedAny = false;

			if (RememberValue)
			{
				tempSnapshot.Value = dd.value;
				capturedAny = true;
			}

			if (RememberInteractable)
			{
				tempSnapshot.Interactable = dd.interactable;
				capturedAny = true;
			}

			if (RememberOptions)
			{
				tempSnapshot.Options = dd.options.Select(o => o.text).ToList();
				capturedAny = true;
			}

			if (RememberColors)
			{
				tempSnapshot.Colors = dd.colors;
				capturedAny = true;
			}

			if (dd.captionText != null)
			{
				if (RememberCaptionText)
				{
					tempSnapshot.CaptionText = dd.captionText.text;
					capturedAny = true;
				}

				if (RememberCaptionFontSize)
				{
					tempSnapshot.CaptionFontSize = dd.captionText.fontSize;
					capturedAny = true;
				}

				if (RememberCaptionColor)
				{
					tempSnapshot.CaptionColor = dd.captionText.color;
					capturedAny = true;
				}
			}

			if (dd.itemText != null)
			{
				if (RememberItemFontSize)
				{
					tempSnapshot.ItemFontSize = dd.itemText.fontSize;
					capturedAny = true;
				}

				if (RememberItemColor)
				{
					tempSnapshot.ItemColor = dd.itemText.color;
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

		private TMPDropdownData ConvertSnapshotToData(TMPDropdownSnapshot snapshot)
		{
			TMPDropdownData data = new TMPDropdownData();

			if (RememberValue)
			{
				data.Value = snapshot.Value;
				data.HasValue = true;
			}

			if (RememberInteractable)
			{
				data.Interactable = snapshot.Interactable;
				data.HasInteractable = true;
			}

			if (RememberOptions)
			{
				data.Options = snapshot.Options;
				data.HasOptions = true;
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

			if (RememberCaptionText)
			{
				data.CaptionText = snapshot.CaptionText;
				data.HasCaptionText = true;
			}

			if (RememberCaptionFontSize)
			{
				data.CaptionFontSize = snapshot.CaptionFontSize;
				data.HasCaptionFontSize = true;
			}

			if (RememberCaptionColor)
			{
				data.CaptionColor = snapshot.CaptionColor;
				data.HasCaptionColor = true;
			}

			if (RememberItemFontSize)
			{
				data.ItemFontSize = snapshot.ItemFontSize;
				data.HasItemFontSize = true;
			}

			if (RememberItemColor)
			{
				data.ItemColor = snapshot.ItemColor;
				data.HasItemColor = true;
			}

			return data;
		}

		private bool AreEquivalent(TMPDropdownSnapshot cached, TMPDropdownSnapshot current)
		{
			const float tolerance = 0.0001f;

			if (RememberValue && cached.Value != current.Value)
				return false;

			if (RememberInteractable && cached.Interactable != current.Interactable)
				return false;

			if (RememberOptions && !OptionsEqual(cached.Options, current.Options))
				return false;

			if (RememberColors && !ColorBlocksApproximatelyEqual(cached.Colors, current.Colors))
				return false;

			if (RememberCaptionText && !string.Equals(cached.CaptionText, current.CaptionText, StringComparison.Ordinal))
				return false;

			if (RememberCaptionFontSize && Mathf.Abs(cached.CaptionFontSize - current.CaptionFontSize) > tolerance)
				return false;

			if (RememberCaptionColor && !ColorsApproximatelyEqual(cached.CaptionColor, current.CaptionColor))
				return false;

			if (RememberItemFontSize && Mathf.Abs(cached.ItemFontSize - current.ItemFontSize) > tolerance)
				return false;

			if (RememberItemColor && !ColorsApproximatelyEqual(cached.ItemColor, current.ItemColor))
				return false;

			return true;
		}

		private static bool OptionsEqual(List<string> a, List<string> b)
		{
			if (a == null && b == null) return true;
			if (a == null || b == null) return false;
			if (a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
			{
				if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
					return false;
			}
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
	/// Data structure for TMP_Dropdown serialization.
	/// </summary>
	[MemoryPackable]
	public partial class TMPDropdownData
	{
		// Dropdown value
		public bool HasValue { get; set; }
		public int Value { get; set; }

		// Interactable
		public bool HasInteractable { get; set; }
		public bool Interactable { get; set; }

		// Options
		public bool HasOptions { get; set; }
		public List<string> Options { get; set; }

		// Colors
		public bool HasColors { get; set; }
		public Color NormalColor { get; set; }
		public Color HighlightedColor { get; set; }
		public Color PressedColor { get; set; }
		public Color SelectedColor { get; set; }
		public Color DisabledColor { get; set; }
		public float ColorMultiplier { get; set; }
		public float FadeDuration { get; set; }

		// Caption text
		public bool HasCaptionText { get; set; }
		public string CaptionText { get; set; }

		public bool HasCaptionFontSize { get; set; }
		public float CaptionFontSize { get; set; }

		public bool HasCaptionColor { get; set; }
		public Color CaptionColor { get; set; }

		// Item text
		public bool HasItemFontSize { get; set; }
		public float ItemFontSize { get; set; }

		public bool HasItemColor { get; set; }
		public Color ItemColor { get; set; }

		public TMPDropdownData() { }
	}
}
#endif
