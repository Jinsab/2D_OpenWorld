#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

#if TMP_PRESENT
using TMPro;
#endif
using Arawn.CrystalSave.Runtime;

namespace CrystalSave.UI
{
    /// <summary>
    /// Scene UI that shows save slots distributed across multiple dropdowns per category.
    /// Works in Edit mode so you can lay it out visually.
    /// </summary>
    public class GroupedSaveSlotsUI : MonoBehaviour
    {
        public enum SlotCategory { Regular, Quick, Auto }

        [Serializable]
        public class SlotActionEvent : UnityEvent<SlotCategory, int /*realIndex*/, string /*displayedName*/> { }

    // Slot counts are sourced solely from SaveManager.SaveSettings

        [Header("Grouping")]
        [Min(1)] public int slotsPerDropdown = 5;

    [Header("Visibility")]
    [Tooltip("Show the Quick Save dropdown groups in the UI.")]
    public bool showQuickSaves = true;
    [Tooltip("Show the Auto Save dropdown groups in the UI.")]
    public bool showAutoSaves = true;

        [Header("UI References")]
        public Transform regularContainer;
        public Transform quickContainer;
        public Transform autoContainer;

        [Tooltip("A prefab with a Dropdown (or TMP_Dropdown) component. We’ll instantiate copies under each container.")]
        public GameObject dropdownPrefab;

    [Header("Slot Naming")]
    [SerializeField, Tooltip("Pattern for slot names. Supports {n} and {meta:key} / {metadata:key}. Leave empty to use SlotName or default labels.")]
    private string slotNamePattern = string.Empty;

    [Header("Save Behaviour")]
    [SerializeField, Tooltip("When enabled, saving into an occupied slot preserves its existing slot name instead of recomputing it from pattern/current context.")]
    private bool preserveSlotName = true;

        [Tooltip("Optional input for a custom slot name; if null we’ll use the current option text.")]
#if TMP_PRESENT
        public TMP_InputField slotNameInput;
#else
        public InputField slotNameInput;
#endif

        public Button saveButton;
        public Button loadButton;

        [Header("Events (wire these to your Save/Load API)")]
        public SlotActionEvent OnSaveRequested;
        public SlotActionEvent OnLoadRequested;

        // --- Internal state
        readonly List<DropdownBundle> _regular = new();
        readonly List<DropdownBundle> _quick = new();
        readonly List<DropdownBundle> _auto = new();

        // Keep track of the last selection so buttons know what to operate on
        private SlotCategory _lastCategory = SlotCategory.Regular;
        private int _lastRealIndex = 0;
        private Dropdown _lastUGUIDropdown;
    private bool _suppressDropdownEvents = false; // guard to avoid recursive event storms when resetting siblings

#if TMP_PRESENT
        private TMP_Dropdown _lastTMPDropdown;
#endif

        // ---- Helper: bundle both dropdown types behind a tiny wrapper
        [Serializable]
        private class DropdownBundle
        {
            public GameObject root;
            public Dropdown ugui;
#if TMP_PRESENT
            public TMP_Dropdown tmp;
#endif

            public int StartRealIndex; // offset into real slots for this dropdown (0-based)
            public int Count;          // number of entries in this dropdown (<= slotsPerDropdown)

            public void SetOptions(List<string> texts)
            {
                if (ugui)
                {
                    ugui.ClearOptions();
                    ugui.AddOptions(texts);
                }
#if TMP_PRESENT
                if (tmp)
                {
                    tmp.ClearOptions();
                    var opts = new List<TMP_Dropdown.OptionData>(texts.Count);
                    foreach (var t in texts) opts.Add(new TMP_Dropdown.OptionData(t));
                    tmp.AddOptions(opts);
                }
#endif
            }

            public int value
            {
                get
                {
                    if (ugui) return ugui.value;
#if TMP_PRESENT
                    if (tmp) return tmp.value;
#endif
                    return 0;
                }
                set
                {
                    if (ugui) ugui.value = Mathf.Clamp(value, 0, Mathf.Max(0, (ugui.options.Count - 1)));
#if TMP_PRESENT
                    if (tmp) tmp.value = Mathf.Clamp(value, 0, Mathf.Max(0, (tmp.options.Count - 1)));
#endif
                }
            }

            public void SetLabelAt(int optionIndex, string text)
            {
                if (ugui)
                {
                    ugui.options[optionIndex].text = text;
                    ugui.RefreshShownValue();
                }
#if TMP_PRESENT
                if (tmp)
                {
                    tmp.options[optionIndex].text = text;
                    tmp.RefreshShownValue();
                }
#endif
            }
        }

        private void OnEnable()
        {
            // Only build and wire runtime UI in Play Mode.
            if (!Application.isPlaying) return;
            if (!SaveManager.IsInitialized || SaveManager.Instance == null)
            {
                SaveManager.Initialized += OnManagerInitialized;
            }
            else
            {
                // Subscribe to updates so labels refresh after saves
                var mgr = SaveManager.Instance;
                if (mgr != null)
                {
                    RebuildAll();
                    mgr.OnSaveSlotsUpdated += HandleSaveSlotsUpdated;
                    mgr.OnSaveCompleted    += HandleSaveCompleted;
                    mgr.OnQuickSlotsUpdated += HandleQuickSlotsUpdated;
                }
            }
            HookButtons();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Do not create/destroy UI under Edit Mode. Build only during Play Mode.
            if (!Application.isPlaying) return;
            if (!isActiveAndEnabled) return;
            RebuildAll();
        }
#endif

    private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SaveManager.Initialized -= OnManagerInitialized;
        var mgr = SaveManager.Instance;
        if (mgr != null)
                {
            mgr.OnSaveSlotsUpdated -= HandleSaveSlotsUpdated;
            mgr.OnSaveCompleted    -= HandleSaveCompleted;
            mgr.OnQuickSlotsUpdated -= HandleQuickSlotsUpdated;
                }
            }
        }

        private void OnManagerInitialized(SaveManager mgr)
        {
            SaveManager.Initialized -= OnManagerInitialized;
            RebuildAll();
            // Now that we have a manager, subscribe to updates
            mgr.OnSaveSlotsUpdated += HandleSaveSlotsUpdated;
            mgr.OnSaveCompleted    += HandleSaveCompleted;
            mgr.OnQuickSlotsUpdated += HandleQuickSlotsUpdated;
        }

        private void HandleSaveSlotsUpdated()
        {
            RefreshLabels();
        }

        private void HandleSaveCompleted(object sender, SaveLoadEventArgs e)
        {
            if (e?.Slot != null)
            {
                // Prefer the actual saved SlotName; fallback to resolving the pattern to avoid stale display
                string display = !string.IsNullOrWhiteSpace(e.Slot.SlotName)
                    ? e.Slot.SlotName
                    : ResolveSavePattern(e.Slot) ?? $"Slot {e.Slot.SlotNumber}";

                UpdateSlotOptionLabel(SlotCategory.Regular, e.Slot.SlotNumber, display);
            }

            // Also refresh all labels from the current manager state
            RefreshLabels();
        }

        private void HandleQuickSlotsUpdated()
        {
            // Currently we refresh regular labels; extend here if quick labels are exposed via API
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (!Application.isPlaying || !SaveManager.IsInitialized) return;
            var mgr = SaveManager.Instance;

            // Regular category labels
            foreach (var bundle in _regular)
            {
                if (bundle == null) continue;
                for (int j = 0; j < bundle.Count; j++)
                {
                    int slotNumber = bundle.StartRealIndex + j + 1;
                    var slot = mgr.GetSaveSlotByNumber(slotNumber);
                    var label = ResolveSlotDisplayName(slot);
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        bundle.SetLabelAt(j + 1, label); // +1 offset for Data X placeholder
                    }
                }
            }
        }

        private void HookButtons()
        {
            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(async () =>
                {
                    // If the placeholder ("Data X") is selected, block saving silently.
                    if (IsPlaceholderSelected())
                        return;

                    // Compute desired name first.
                    // Priority: explicit input > preserved existing name > pattern > current displayed text
                    string inputName = slotNameInput ? slotNameInput.text : null;
                    Arawn.CrystalSave.Runtime.SaveSlot slotForName = null;
                    if (SaveManager.IsInitialized)
                        slotForName = SaveManager.Instance.GetSaveSlotByNumber(_lastRealIndex + 1);

                    string preservedSlotName = null;
                    if (preserveSlotName && slotForName != null && !string.IsNullOrWhiteSpace(slotForName.SlotName))
                        preservedSlotName = slotForName.SlotName;

                    string patternResolved = null;
                    if (string.IsNullOrWhiteSpace(preservedSlotName) && !string.IsNullOrWhiteSpace(slotNamePattern))
                        patternResolved = ResolveSavePattern(slotForName);

                    // Priority: explicit input > preserved existing name > pattern > current displayed text
                    var nameToUse = !string.IsNullOrWhiteSpace(inputName)
                        ? inputName
                        : (!string.IsNullOrWhiteSpace(preservedSlotName)
                            ? preservedSlotName
                            : (!string.IsNullOrWhiteSpace(patternResolved) ? patternResolved : CurrentDisplayedName()));

                    // Fire external event
                    OnSaveRequested?.Invoke(_lastCategory, _lastRealIndex, nameToUse);

                    if (!SaveManager.IsInitialized)
                        return;

                    // Disable the button explicitly to avoid it getting stuck if a different selectable is captured
                    bool restoreInteractable = saveButton.interactable;
                    saveButton.interactable = false;
                    try
                    {
                        var sceneName = SceneManager.GetActiveScene().name;
                        int slotNumber = _lastRealIndex + 1;
                        await SaveManager.Instance.SaveAsync(slotNumber, sceneName, nameToUse);

                        // If the placeholder ("Data X") is currently selected, do NOT rename it.
                        // Instead, update the label of the slot option that was saved.
                        if (IsPlaceholderSelected())
                        {
                            UpdateSlotOptionLabel(_lastCategory, slotNumber, nameToUse);
                        }
                        else
                        {
                            // Rename the currently selected option (which should be the slot option)
                            UpdateCurrentOptionLabel(nameToUse);
                        }

                        RefreshLabels();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GroupedSaveSlotsUI] Save failed: {ex.Message}");
                    }
                    finally
                    {
                        saveButton.interactable = restoreInteractable;
                    }
                });
            }

            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(() =>
                {
                    var nameToUse = CurrentDisplayedName();
                    OnLoadRequested?.Invoke(_lastCategory, _lastRealIndex, nameToUse);

                    if (SaveManager.IsInitialized)
                        SaveManager.Instance.Load(_lastRealIndex + 1, true);
                });
            }
        }

        private bool IsPlaceholderSelected()
        {
#if TMP_PRESENT
            if (_lastTMPDropdown) return _lastTMPDropdown.value == 0;
#endif
            if (_lastUGUIDropdown) return _lastUGUIDropdown.value == 0;
            return false;
        }

        private void UpdateSlotOptionLabel(SlotCategory category, int slotNumber, string newText)
        {
            // Determine the dropdown list by category
            var list = category == SlotCategory.Regular ? _regular
                      : category == SlotCategory.Quick   ? _quick
                      : _auto;

            int realIndex = slotNumber - 1;
            foreach (var b in list)
            {
                if (b == null) continue;
                // Check if this bundle contains the slot index
                if (realIndex >= b.StartRealIndex && realIndex < b.StartRealIndex + b.Count)
                {
                    int local = (realIndex - b.StartRealIndex) + 1; // +1 to skip placeholder at index 0
                    b.SetLabelAt(local, newText);
                    break;
                }
            }
        }

        // Resolve a save-time name from the configured pattern, ignoring any persisted SlotName
        private string ResolveSavePattern(Arawn.CrystalSave.Runtime.SaveSlot slot)
        {
            if (slot == null) return null;
            if (string.IsNullOrWhiteSpace(slotNamePattern)) return null;

            string pattern = Regex.Replace(slotNamePattern, "\\{metadata:(.+?)\\}", "{meta:$1}");

            // Resolve with current defaults first (designer-controlled), then fall back to the slot's own metadata.
            // This guarantees that when the SaveSlotMetadataSO changes (e.g., 'Airport' -> 'Hamburg' -> 'Frankfurt'),
            // subsequent saves compute the new name immediately.
            var settings = SaveManager.Instance?.SaveSettings;
            var defaultMetadata = settings?.defaultSlotMetadata?.ToDictionary();

            string resolved = pattern.Replace("{n}", (slot?.SlotNumber ?? (_lastRealIndex + 1)).ToString());
            resolved = Regex.Replace(resolved, "\\{meta:([^}]+)\\}", m =>
            {
                var key = m.Groups[1].Value;
                string val = null;
                if (defaultMetadata != null && defaultMetadata.TryGetValue(key, out var dv) && !string.IsNullOrEmpty(dv))
                    val = dv;
                if (string.IsNullOrEmpty(val) && slot?.CustomMetadata != null && slot.CustomMetadata.TryGetValue(key, out var sv) && !string.IsNullOrEmpty(sv))
                    val = sv;
                return val ?? string.Empty;
            });
            return resolved;
        }

        private string CurrentDisplayedName()
        {
#if TMP_PRESENT
            if (_lastTMPDropdown)
                return _lastTMPDropdown.options[_lastTMPDropdown.value].text;
#endif
            if (_lastUGUIDropdown)
                return _lastUGUIDropdown.options[_lastUGUIDropdown.value].text;

            return $"Slot {_lastRealIndex + 1}";
        }

        private void UpdateCurrentOptionLabel(string newText)
        {
#if TMP_PRESENT
            if (_lastTMPDropdown)
            {
                _lastTMPDropdown.options[_lastTMPDropdown.value].text = string.IsNullOrWhiteSpace(newText)
                    ? _lastTMPDropdown.options[_lastTMPDropdown.value].text
                    : newText;
                _lastTMPDropdown.RefreshShownValue();
                return;
            }
#endif
            if (_lastUGUIDropdown)
            {
                _lastUGUIDropdown.options[_lastUGUIDropdown.value].text = string.IsNullOrWhiteSpace(newText)
                    ? _lastUGUIDropdown.options[_lastUGUIDropdown.value].text
                    : newText;
                _lastUGUIDropdown.RefreshShownValue();
            }
        }

        // Build a display name for a slot based on pattern/metadata/slot name
        private string ResolveSlotDisplayName(Arawn.CrystalSave.Runtime.SaveSlot slot)
        {
            if (slot == null) return null;

            // First prefer an explicitly stored SlotName – this “freezes” the label at save time
            if (!string.IsNullOrWhiteSpace(slot.SlotName))
                return slot.SlotName;

            // If a pattern is provided, use it with NamePatternResolver. Support {metadata:key} as alias for {meta:key}.
            if (!string.IsNullOrWhiteSpace(slotNamePattern))
            {
                string pattern = Regex.Replace(slotNamePattern, "\\{metadata:(.+?)\\}", "{meta:$1}");
#if MEMORYPACK && ARAWN_REMEMBERME
                return NamePatternResolver.Resolve(pattern, slot);
#else
                // Minimal manual replacement for {n} and {meta:key} when resolver isn’t available
                string resolved = pattern.Replace("{n}", slot.SlotNumber.ToString());
                resolved = Regex.Replace(resolved, "\\{meta:([^}]+)\\}", m =>
                {
                    var key = m.Groups[1].Value;
                    if (slot.CustomMetadata != null && slot.CustomMetadata.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                        return v;
                    return string.Empty;
                });
                return resolved;
#endif
            }
            // No pattern and no stored name
            return null;
        }

        // ==== BUILD ====
    public void RebuildAll()
        {
            if (!Application.isPlaying) return; // guard: never build in Edit mode
            if (!SaveManager.IsInitialized || SaveManager.Instance?.SaveSettings == null)
            {
                Debug.Log("[GroupedSaveSlotsUI] SaveManager not initialized yet; delaying build.");
                return;
            }
            ReadCountsFromSaveSettings(out var reg, out var quick, out var auto);

            // Quick Saves visibility
            if (showQuickSaves)
            {
                BuildCategory(SlotCategory.Quick, quickContainer, quick, _quick);
                if (quickContainer) quickContainer.gameObject.SetActive(true);
            }
            else
            {
                ClearCategory(_quick);
                if (quickContainer) quickContainer.gameObject.SetActive(false);
            }

            // Auto Saves visibility
            if (showAutoSaves)
            {
                BuildCategory(SlotCategory.Auto,  autoContainer,  auto,  _auto);
                if (autoContainer) autoContainer.gameObject.SetActive(true);
            }
            else
            {
                ClearCategory(_auto);
                if (autoContainer) autoContainer.gameObject.SetActive(false);
            }
            BuildCategory(SlotCategory.Regular, regularContainer, reg, _regular);

            // default selection
            SelectFirstAvailable();
        }

    private void BuildCategory(SlotCategory cat, Transform parent, int total, List<DropdownBundle> cache)
        {
        if (!Application.isPlaying) return; // guard: never touch scene hierarchy in Edit mode
            // Wipe old
            foreach (var b in cache)
                if (b != null && b.root != null)
                {
            Destroy(b.root);
                }
            cache.Clear();

            if (parent == null || dropdownPrefab == null || total <= 0) return;

            int numDropdowns = Mathf.CeilToInt(total / (float)slotsPerDropdown);
            for (int i = 0; i < numDropdowns; i++)
            {
                var go = Instantiate(dropdownPrefab, parent);
                go.name = $"{cat} Dropdown {IndexToAlpha(i)}"; // e.g., Alpha, Beta, Gamma

                var bundle = new DropdownBundle { root = go };

                // Support UGUI or TMP
                bundle.ugui = go.GetComponentInChildren<Dropdown>(true);
#if TMP_PRESENT
                bundle.tmp = go.GetComponentInChildren<TMP_Dropdown>(true);
#endif
                if (!bundle.ugui
#if TMP_PRESENT
                    && !bundle.tmp
#endif
                    )
                {
                    Debug.LogError("Dropdown prefab must contain a Dropdown or TMP_Dropdown component.");
                    DestroyImmediate(go);
                    continue;
                }

                int start = i * slotsPerDropdown;
                int count = Mathf.Min(slotsPerDropdown, total - start);
                bundle.StartRealIndex = start;
                bundle.Count = count;

                // Build options with a default placeholder as first entry: "Data A/B/C...", then Slot 1..N
                var opts = new List<string>(count + 1);
                opts.Add($"Data {IndexToLetter(i)}"); // default option at index 0
                for (int j = 0; j < count; j++) opts.Add($"Slot {j + 1}");
                bundle.SetOptions(opts);

                // Prefill option labels from existing pattern/metadata/slot names when available
                if (SaveManager.IsInitialized)
                {
                    var mgr = SaveManager.Instance;
                    for (int j = 0; j < count; j++)
                    {
                        int realIndex = start + j; // 0-based
                        int slotNumber = realIndex + 1;
                        string label = null;
                        // We support only regular slots lookup via public API here
                        if (cat == SlotCategory.Regular)
                        {
                            var slot = mgr.GetSaveSlotByNumber(slotNumber);
                            label = ResolveSlotDisplayName(slot);
                        }
                        // Apply label if found (option index j+1 since 0 is the Data placeholder)
                        if (!string.IsNullOrWhiteSpace(label))
                        {
                            bundle.SetLabelAt(j + 1, label);
                        }
                    }
                }

                // Hook selection -> compute real index
                void OnChanged(int _)
                {
                    if (_suppressDropdownEvents) return;
                    int local = bundle.value; // 0..count (0 = default "Data X")
                    if (local == 0)
                    {
                        // Default selection means this dropdown is not choosing any slot.
                        return;
                    }
                    // Adjust for the default entry offset
                    int realIndex = bundle.StartRealIndex + (local - 1); // 0-based slot index
                    _lastCategory = cat;
                    _lastRealIndex = realIndex;

                    _lastUGUIDropdown = bundle.ugui;
#if TMP_PRESENT
                    _lastTMPDropdown = bundle.tmp;
#endif

                    // Reset all sibling dropdowns in this category to their default option
                    _suppressDropdownEvents = true;
                    foreach (var sib in cache)
                    {
                        if (sib == null || sib == bundle) continue;
                        sib.value = 0; // selects "Data X" option
                    }
                    _suppressDropdownEvents = false;
                }

                if (bundle.ugui)
                {
                    bundle.ugui.onValueChanged.RemoveAllListeners();
                    bundle.ugui.onValueChanged.AddListener(OnChanged);
                }
#if TMP_PRESENT
                if (bundle.tmp)
                {
                    bundle.tmp.onValueChanged.RemoveAllListeners();
                    bundle.tmp.onValueChanged.AddListener(OnChanged);
                }
#endif

                // Initialize default selection to the placeholder (index 0)
                bundle.value = 0; // don't trigger change logic for default

                cache.Add(bundle);
            }
        }

        private void ClearCategory(List<DropdownBundle> cache)
        {
            if (!Application.isPlaying) return;
            foreach (var b in cache)
            {
                if (b != null && b.root != null)
                    Destroy(b.root);
            }
            cache.Clear();
        }

        private void SelectFirstAvailable()
        {
            // Prefer Quick -> Auto -> Regular (arbitrary but deterministic)
            if (SelectFirstIn(_quick, SlotCategory.Quick)) return;
            if (SelectFirstIn(_auto,  SlotCategory.Auto))  return;
            SelectFirstIn(_regular, SlotCategory.Regular);
        }

        private bool SelectFirstIn(List<DropdownBundle> list, SlotCategory cat)
        {
            if (list.Count == 0) return false;
            var b = list[0];
            if (b.Count == 0) return false;
            b.value = 0;
            _lastCategory = cat;
            _lastRealIndex = 0 + b.StartRealIndex;
            _lastUGUIDropdown = b.ugui;
#if TMP_PRESENT
            _lastTMPDropdown = b.tmp;
#endif
            return true;
        }

        private static string IndexToAlpha(int i)
        {
            // 0 -> Alpha, 1 -> Beta, 2 -> Gamma, 3 -> Delta, 4 -> Epsilon ...
            string[] names = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa" };
            return i < names.Length ? names[i] : $"Group {i + 1}";
        }

        private static string IndexToLetter(int i)
        {
            // 0 -> A, 1 -> B, 2 -> C ...
            int letter = (i % 26);
            return ((char)('A' + letter)).ToString();
        }

        private void ReadCountsFromSaveSettings(out int reg, out int quick, out int auto)
        {
            // Prefer counts from SaveManager's SaveSettings when available.
            var mgr = SaveManager.IsInitialized ? SaveManager.Instance : null;
            var ss = mgr?.SaveSettings;
            reg = ss != null ? Mathf.Max(0, ss.numberOfSaveSlots) : 0;
            quick = ss != null ? Mathf.Max(0, ss.numberOfQuickSaveSlots) : 0;
            auto = ss != null
                ? Mathf.Max(0, ss.numberOfAutoSaveSlots > 0 ? ss.numberOfAutoSaveSlots : (ss.autoSaveSlotNumber > 0 ? 1 : 0))
                : 0;
        }

        // ========== Public helpers ==========
        /// <summary>Programmatically rename the currently selected displayed option (e.g., right after saving).</summary>
        public void RenameCurrentDisplayedOption(string newDisplayName) => UpdateCurrentOptionLabel(newDisplayName);

        /// <summary>Translate a (category, dropdownIndex, localOptionIndex) to a real slot index (0-based).</summary>
        public int ToRealIndex(SlotCategory category, int dropdownIndex, int localOptionIndex)
        {
            var list = category == SlotCategory.Regular ? _regular
                      : category == SlotCategory.Quick   ? _quick
                      : _auto;

            if (dropdownIndex < 0 || dropdownIndex >= list.Count) return 0;
            var b = list[dropdownIndex];
            // Account for default placeholder at index 0; slots start at option index 1
            localOptionIndex = Mathf.Clamp(localOptionIndex, 1, b.Count); // 1..Count
            return b.StartRealIndex + (localOptionIndex - 1);
        }
    }
}
#endif
