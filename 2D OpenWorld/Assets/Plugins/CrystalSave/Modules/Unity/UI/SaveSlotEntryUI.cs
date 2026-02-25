/*
 * Provides the runtime UI element used by SaveSlotsRuntimeUI to
 * display a single save slot. Each instance shows the slot number,
 * metadata and optional screenshot while exposing buttons to load,
 * save, rename or delete that slot. The script relies on the
 * standard UnityEngine.UI components such as Text, Image, Button
 * and InputField. If TextMesh Pro is available (TMP_PRESENT
 * scripting define), the script also supports TMP_Text and
 * TMP_InputField components.
*/
#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
#if REMEMBERME_CLOUDSAVE_PRESENT
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
#endif
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Authentication;
using Unity.Services.Core;
#endif
#if TMP_PRESENT
using TMPro;
#endif
using Arawn.CrystalSave.Runtime;
using UnityEngine.EventSystems;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.UI
{
    /// <summary>
    /// Runtime UI element representing a single save slot.
    /// </summary>
    public class SaveSlotEntryUI : MonoBehaviour
    {
        // Global tracking for screenshot downloads to prevent duplicates
        private static readonly HashSet<string> downloadingScreenshots = new HashSet<string>();
        private static readonly object downloadLock = new object();
        
#if TMP_PRESENT
        /// <summary>
        /// Determines which set of UI components should be used when TextMesh
        /// Pro is available.
        /// </summary>
        public enum TextMode { Legacy, TextMeshPro }

        [Header("Text Mode")]
        public TextMode textMode = TextMode.Legacy;
#endif
        [Header("UI References")]
        public Image background;
        public Image screenshotImage;
        public Text  slotNameText;
#if TMP_PRESENT
        public TMP_Text slotNameTMP;
#endif
        public Text  metadataText;
#if TMP_PRESENT
        public TMP_Text metadataTMP;
#endif
        [Header("Dynamic Metadata UI")]
        public RectTransform customMetadataContainer;
        public Text customMetadataLinePrefab;
#if TMP_PRESENT
        public TMP_Text customMetadataLineTMPPrefab;
#endif
        public InputField renameInput;
#if TMP_PRESENT
        public TMP_InputField renameInputTMP;
#endif
        [Tooltip("Hide the rename input field on start. Clicking Rename shows it and pressing Enter/Return confirms the new name.")]
        public bool hideRenameInputOnStart = false;
        public Button renameButton;
        public Button loadButton;
        public Button saveButton;
        public Button deleteButton;
        public Image panel;
        public Image syncOverlay;

        [Header("Delete Confirmation")]
        public GameObject deleteConfirmationWindow;
        public Button confirmDeleteButton;
        public Button cancelDeleteButton;

        [Header("Theme")]
        public SaveSlotEntryTheme theme;

        SaveSlotsRuntimeUI parent;
        SaveSlot slot;
        bool isDownloadingScreenshot;
        Sprite defaultScreenshotSprite;
        Canvas canvas;
    bool isSaving; // Tracks if a save operation is currently in progress for this slot
    Coroutine saveWatchdog; // Fallback coroutine to recover UI if completion event never arrives
    // Optional compact numbering index provided by SaveSlotsRuntimeUI for display purposes only.
    int? displayIndexOverride;

        void SetOverlayActive(bool active)
        {
            if (syncOverlay != null)
                syncOverlay.gameObject.SetActive(active);
        }

        void Awake()
        {
            SaveSlotsRuntimeUI.EnsureEventSystem();
            if (screenshotImage != null)
                defaultScreenshotSprite = screenshotImage.sprite;
            canvas = GetComponentInParent<Canvas>();
        }

        void OnEnable()
        {
            // Move this UI element to the end of the hierarchy so it renders on
            // top when used as a prefab drop-in.
            transform.SetAsLastSibling();
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            AuthEventsRelay.PlayerSignedIn += OnAuthSignedIn;
            // Authentication may finish before this component subscribes to the
            // event (e.g. in WebGL builds). If already signed in, refresh the slot
            // immediately so metadata and screenshots are visible. Access the
            // authentication service only once Unity Services are initialized to
            // avoid runtime exceptions.
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                OnAuthSignedIn(default);
            }
#endif
#if UNITY_EDITOR
            SaveSlotEntryTheme.ThemeChanged += OnThemeChanged;
#endif
            SubscribeScreenshotEvents();
        }

        void OnDisable()
        {
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            AuthEventsRelay.PlayerSignedIn -= OnAuthSignedIn;
#endif
#if UNITY_EDITOR
            SaveSlotEntryTheme.ThemeChanged -= OnThemeChanged;
#endif
            UnsubscribeScreenshotEvents();
        }

#if UNITY_EDITOR
        void OnThemeChanged(SaveSlotEntryTheme changed)
        {
            if (!Application.isPlaying && changed == theme)
                ApplyTheme(theme);
        }
#endif

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyTheme(theme);
        }
#endif

        public void Initialise(SaveSlot slot,
                               SaveSlotsRuntimeUI parent,
                               bool showScreenshot,
                               bool showMetadata,
                               bool showCustomMetadata,
                               bool showRename,
                               bool showLoad,
                               bool showSave,
                               bool showDelete,
                               SaveSlotEntryTheme theme = null)
        {
            this.slot   = slot;
            this.parent = parent;
            if (theme != null)
                this.theme = theme;

            if (screenshotImage != null)
                screenshotImage.gameObject.SetActive(showScreenshot);
            if (metadataText != null)
                metadataText.gameObject.SetActive(showMetadata
#if TMP_PRESENT
                    && textMode == TextMode.Legacy
#endif
                );
#if TMP_PRESENT
            if (metadataTMP != null)
                metadataTMP.gameObject.SetActive(showMetadata && textMode == TextMode.TextMeshPro);
#endif
            if (customMetadataContainer != null)
                customMetadataContainer.gameObject.SetActive(showCustomMetadata);
            if (renameInput != null)
            {
                bool active = showRename
#if TMP_PRESENT
                    && textMode == TextMode.Legacy
#endif
                    && !hideRenameInputOnStart;
                renameInput.gameObject.SetActive(active);
                renameInput.onEndEdit.RemoveListener(OnRenameInputEndEdit);
                if (hideRenameInputOnStart)
                    renameInput.onEndEdit.AddListener(OnRenameInputEndEdit);
            }
#if TMP_PRESENT
            if (renameInputTMP != null)
            {
                bool active = showRename && textMode == TextMode.TextMeshPro && !hideRenameInputOnStart;
                renameInputTMP.gameObject.SetActive(active);
                renameInputTMP.onEndEdit.RemoveListener(OnRenameTMPEndEdit);
                if (hideRenameInputOnStart)
                    renameInputTMP.onEndEdit.AddListener(OnRenameTMPEndEdit);
            }
#endif
            if (renameButton != null)
                renameButton.gameObject.SetActive(showRename);
            if (loadButton != null)
                loadButton.gameObject.SetActive(showLoad);
            if (saveButton != null)
                saveButton.gameObject.SetActive(showSave);
            if (deleteButton != null)
                deleteButton.gameObject.SetActive(showDelete);
            if (deleteConfirmationWindow != null)
                deleteConfirmationWindow.SetActive(false);
            if (confirmDeleteButton != null)
            {
                confirmDeleteButton.onClick.RemoveListener(OnConfirmDeleteClicked);
                confirmDeleteButton.onClick.AddListener(OnConfirmDeleteClicked);
            }
            if (cancelDeleteButton != null)
            {
                cancelDeleteButton.onClick.RemoveListener(OnCancelDeleteClicked);
                cancelDeleteButton.onClick.AddListener(OnCancelDeleteClicked);
            }

            if (syncOverlay != null)
                syncOverlay.gameObject.SetActive(false);

            ApplyTheme(this.theme);
            Refresh(slot);
        }

        void SubscribeScreenshotEvents()
        {
            if (SaveManager.IsInitialized && SaveManager.Instance != null)
            {
                var mgr = SaveManager.Instance;
                mgr.OnScreenshotCaptureStarted += HideForScreenshot;
                mgr.OnScreenshotCaptureFinished += ShowAfterScreenshot;
            }
            else
            {
                SaveManager.Initialized += OnManagerInitializedForScreenshot;
            }
        }

        void OnManagerInitializedForScreenshot(SaveManager mgr)
        {
            SaveManager.Initialized -= OnManagerInitializedForScreenshot;
            mgr.OnScreenshotCaptureStarted += HideForScreenshot;
            mgr.OnScreenshotCaptureFinished += ShowAfterScreenshot;
        }

        void UnsubscribeScreenshotEvents()
        {
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                mgr.OnScreenshotCaptureStarted -= HideForScreenshot;
                mgr.OnScreenshotCaptureFinished -= ShowAfterScreenshot;
            }
            // Always try to remove the fallback subscription; safe even if not subscribed
            SaveManager.Initialized -= OnManagerInitializedForScreenshot;
        }

        void HideForScreenshot()
        {
            if (canvas != null)
                canvas.enabled = false;
        }

        void ShowAfterScreenshot()
        {
            if (canvas != null)
                canvas.enabled = true;
        }

        public void Refresh(SaveSlot data)
        {
            Arawn.CrystalSave.Runtime.Logger.Log($"SaveSlotEntryUI: Refresh called for slot {data?.SlotNumber ?? -1}, data is null: {data == null}", LogCategory.Other, LogLevel.Info);
            
            if (data == null)
                return;
            slot = data;
            UpdateSlotTitle();

            if (metadataText != null
#if TMP_PRESENT
                && textMode == TextMode.Legacy
#endif
            )
            {
                if (slot.LastSaved > DateTime.MinValue)
                    metadataText.text = slot.LastSaved.ToLocalTime().ToString("g");
                else
                    metadataText.text = "<empty>";
            }
#if TMP_PRESENT
            if (metadataTMP != null && textMode == TextMode.TextMeshPro)
            {
                if (slot.LastSaved > DateTime.MinValue)
                    metadataTMP.text = slot.LastSaved.ToLocalTime().ToString("g");
                else
                    metadataTMP.text = "<empty>";
            }
#endif

            var customLines = GetCustomMetadataLines(slot);
            if (customMetadataContainer != null)
            {
                foreach (Transform child in customMetadataContainer)
                    Destroy(child.gameObject);

                foreach (var line in customLines)
                {
#if TMP_PRESENT
                    if (textMode == TextMode.TextMeshPro && customMetadataLineTMPPrefab != null)
                    {
                        var tmp = Instantiate(customMetadataLineTMPPrefab, customMetadataContainer);
                        tmp.text = line;
                        if (theme != null)
                            tmp.color = theme.textColor;
                    }
                    else
#endif
                    if (customMetadataLinePrefab != null)
                    {
                        var txt = Instantiate(customMetadataLinePrefab, customMetadataContainer);
                        txt.text = line;
                        if (theme != null)
                            txt.color = theme.textColor;
                    }
                }
            }

            if (renameInput != null
#if TMP_PRESENT
                && textMode == TextMode.Legacy
#endif
            )
                renameInput.text = slot.SlotName;
#if TMP_PRESENT
            if (renameInputTMP != null && textMode == TextMode.TextMeshPro)
                renameInputTMP.text = slot.SlotName;
#endif

            bool hasData = slot.LastSaved > DateTime.MinValue;
            if (loadButton != null)
                loadButton.interactable = hasData;
            if (deleteButton != null)
                deleteButton.interactable = hasData;
            if (renameButton != null)
                renameButton.interactable = hasData;
            if (renameInput != null)
                renameInput.interactable = hasData;
#if TMP_PRESENT
            if (renameInputTMP != null)
                renameInputTMP.interactable = hasData;
#endif

            if (screenshotImage != null && screenshotImage.gameObject.activeSelf)
            {
                Texture2D tex = SaveManager.Instance.GetScreenshot(slot);
                Logger.Log($"SaveSlotEntryUI: UpdateUI for slot {slot.SlotNumber} - backend: {SaveManager.Instance?.SaveSettings?.backend}, tex is null: {tex == null}, ScreenshotFileName: '{slot.ScreenshotFileName ?? "null"}', screenshotImage active: {screenshotImage.gameObject.activeSelf}", LogCategory.Other, LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Slot {slot.SlotNumber} screenshot check - backend: {SaveManager.Instance?.SaveSettings?.backend}, tex null: {tex == null}, ScreenshotFileName: '{slot.ScreenshotFileName ?? "null"}'");
#endif
                if (tex != null)
                {
                    screenshotImage.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                    Logger.Log($"SaveSlotEntryUI: Using local screenshot texture for slot {slot.SlotNumber}", LogCategory.Other, LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Slot {slot.SlotNumber} using local screenshot texture");
#endif
                }
                else
                {
                    screenshotImage.sprite = defaultScreenshotSprite;
                    Logger.Log($"SaveSlotEntryUI: Local screenshot not available for slot {slot.SlotNumber}, calling TryDownloadScreenshot", LogCategory.Other, LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Slot {slot.SlotNumber} local screenshot not available, trying download");
#endif
                    TryDownloadScreenshot();
                }
            }
            else
            {
                Logger.Log($"SaveSlotEntryUI: Screenshot image not available for slot {slot.SlotNumber} - screenshotImage null: {screenshotImage == null}, active: {screenshotImage?.gameObject.activeSelf}", LogCategory.Other, LogLevel.Info);
            }
        }

        /// <summary>
        /// Sets a custom display index (1-based) used for the UI title only. Does not affect the real slot number or any operations.
        /// Pass null to clear and revert to the real slot number.
        /// </summary>
        public void SetDisplayIndex(int? displayIndex)
        {
            displayIndexOverride = displayIndex;
            UpdateSlotTitle();
        }

        void UpdateSlotTitle()
        {
            if (slot == null) return;
            int labelNumber = displayIndexOverride ?? slot.SlotNumber;
            string label = $"Slot {labelNumber}: {slot.SlotName}";

            if (slotNameText != null)
            {
#if TMP_PRESENT
                if (textMode == TextMode.Legacy)
#endif
                    slotNameText.text = label;
            }
#if TMP_PRESENT
            if (slotNameTMP != null && textMode == TextMode.TextMeshPro)
                slotNameTMP.text = label;
#endif
        }

        IEnumerable<string> GetCustomMetadataLines(SaveSlot s)
        {
            if (s == null) yield break;
            if (s.CustomMetadata != null && s.CustomMetadata.Count > 0)
            {
                foreach (var kvp in s.CustomMetadata)
                    yield return $"{kvp.Key}: {kvp.Value}";
                yield break;
            }

            var settings = SaveManager.Instance?.SaveSettings;
            if (settings != null && settings.defaultSlotMetadata != null)
            {
                var dict = settings.defaultSlotMetadata.ToDictionary();
                foreach (var kvp in dict)
                    yield return $"{kvp.Key}: {kvp.Value}";
            }
        }

        async void OnRenameInputEndEdit(string value)
        {
            if (!hideRenameInputOnStart) return;
            
            // Subscribe to rename events before starting the operation
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                UnsubscribeRenameEvents();
                mgr.OnRenameSlotCompleted += OnRenameCompleted;
                mgr.OnRenameSlotFailed    += OnRenameFailed;
            }
            
            await parent.RenameSlotAsync(slot.SlotNumber, value);
            if (renameInput != null)
                renameInput.gameObject.SetActive(false);
        }
#if TMP_PRESENT
        async void OnRenameTMPEndEdit(string value)
        {
            if (!hideRenameInputOnStart) return;
            
            // Subscribe to rename events before starting the operation
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                UnsubscribeRenameEvents();
                mgr.OnRenameSlotCompleted += OnRenameCompleted;
                mgr.OnRenameSlotFailed    += OnRenameFailed;
            }
            
            await parent.RenameSlotAsync(slot.SlotNumber, value);
            if (renameInputTMP != null)
                renameInputTMP.gameObject.SetActive(false);
        }
#endif

        void OnRenameCompleted(object sender, RenameSlotEventArgs e)
        {
            if (e.Slot?.SlotNumber != slot?.SlotNumber)
                return;

            // Preserve screenshot filename and custom metadata in case the rename operation clears them.
            string originalScreenshotFileName = slot?.ScreenshotFileName;
            Dictionary<string, string> originalMetadata = null;
            if (slot?.CustomMetadata != null && slot.CustomMetadata.Count > 0)
                originalMetadata = new Dictionary<string, string>(slot.CustomMetadata);

            // Refresh with the updated slot data to ensure screenshot and metadata are preserved
            SaveSlot updatedSlot = e.Slot;

            // Check if screenshot filename was lost during the rename operation
            if (!string.IsNullOrEmpty(originalScreenshotFileName) && string.IsNullOrEmpty(updatedSlot.ScreenshotFileName))
            {
                // Preserve the original screenshot filename
                updatedSlot.ScreenshotFileName = originalScreenshotFileName;
                Logger.Log($"SaveSlotEntryUI: Preserved screenshot filename '{originalScreenshotFileName}' for slot {updatedSlot.SlotNumber} after rename.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Preserved screenshot filename '{originalScreenshotFileName}' for slot {updatedSlot.SlotNumber} after rename.");
#endif
            }

            // Preserve custom metadata if it was lost
            if (originalMetadata != null && (updatedSlot.CustomMetadata == null || updatedSlot.CustomMetadata.Count == 0))
                updatedSlot.CustomMetadata = originalMetadata;

            Refresh(updatedSlot);
            UnsubscribeRenameEvents();

            // Force the parent UI to refresh all slots so other entries don't
            // lose their metadata or screenshot references after a rename.
            _ = parent?.ForceRefreshSlotsAsync();
        }

        void OnRenameFailed(object sender, OperationFailedEventArgs e)
        {
            if (e.Slot?.SlotNumber != slot?.SlotNumber)
                return;

            UnsubscribeRenameEvents();
        }

        void UnsubscribeRenameEvents()
        {
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                mgr.OnRenameSlotCompleted -= OnRenameCompleted;
                mgr.OnRenameSlotFailed    -= OnRenameFailed;
            }
        }

        void OnDestroy()
        {
            if (renameInput != null)
                renameInput.onEndEdit.RemoveListener(OnRenameInputEndEdit);
#if TMP_PRESENT
            if (renameInputTMP != null)
                renameInputTMP.onEndEdit.RemoveListener(OnRenameTMPEndEdit);
#endif
            if (confirmDeleteButton != null)
                confirmDeleteButton.onClick.RemoveListener(OnConfirmDeleteClicked);
            if (cancelDeleteButton != null)
                cancelDeleteButton.onClick.RemoveListener(OnCancelDeleteClicked);
            UnsubscribeSaveEvents();
            UnsubscribeRenameEvents();
            UnsubscribeDeleteEvents();
        }

        public void ApplyTheme(SaveSlotEntryTheme t)
        {
            if (screenshotImage != null && (screenshotImage.sprite == null || screenshotImage.sprite == defaultScreenshotSprite))
            {
                if (t != null && t.screenshotPlaceholder != null)
                    screenshotImage.sprite = t.screenshotPlaceholder;
                else
                    screenshotImage.sprite = defaultScreenshotSprite;
            }

            if (t == null)
                return;

            if (t.panel != null)
                panel = t.panel;

            if (panel != null)
            {
                panel.color = t.panelColor;
                panel.sprite = t.panelSprite;
            }

            if (background != null)
            {
                background.color = t.backgroundColor;
                background.sprite = t.backgroundSprite;
            }

            if (syncOverlay != null)
            {
                syncOverlay.color = t.overlayColor;
                foreach (var txt in syncOverlay.GetComponentsInChildren<Text>(true))
                    txt.color = t.textColor;
#if TMP_PRESENT
                foreach (var tmp in syncOverlay.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.textColor;
#endif
            }

            if (renameButton != null)
            {
                var img = renameButton.GetComponent<Image>();
                if (img != null)
                    img.sprite = t.renameButtonSprite;
                foreach (var txt in renameButton.GetComponentsInChildren<Text>(true))
                    txt.color = t.buttonTextColor;
#if TMP_PRESENT
                foreach (var tmp in renameButton.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.buttonTextColor;
#endif
            }
            if (loadButton != null)
            {
                var img = loadButton.GetComponent<Image>();
                if (img != null)
                    img.sprite = t.loadButtonSprite;
                foreach (var txt in loadButton.GetComponentsInChildren<Text>(true))
                    txt.color = t.buttonTextColor;
#if TMP_PRESENT
                foreach (var tmp in loadButton.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.buttonTextColor;
#endif
            }
            if (saveButton != null)
            {
                var img = saveButton.GetComponent<Image>();
                if (img != null)
                    img.sprite = t.saveButtonSprite;
                foreach (var txt in saveButton.GetComponentsInChildren<Text>(true))
                    txt.color = t.buttonTextColor;
#if TMP_PRESENT
                foreach (var tmp in saveButton.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.buttonTextColor;
#endif
            }
            if (deleteButton != null)
            {
                var img = deleteButton.GetComponent<Image>();
                if (img != null)
                    img.sprite = t.deleteButtonSprite;
                foreach (var txt in deleteButton.GetComponentsInChildren<Text>(true))
                    txt.color = t.buttonTextColor;
#if TMP_PRESENT
                foreach (var tmp in deleteButton.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.buttonTextColor;
#endif
            }
            if (confirmDeleteButton != null)
            {
                var img = confirmDeleteButton.GetComponent<Image>();
                if (img != null)
                    img.sprite = t.confirmDeleteButtonSprite;
                foreach (var txt in confirmDeleteButton.GetComponentsInChildren<Text>(true))
                    txt.color = t.buttonTextColor;
#if TMP_PRESENT
                foreach (var tmp in confirmDeleteButton.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.buttonTextColor;
#endif
            }
            if (cancelDeleteButton != null)
            {
                var img = cancelDeleteButton.GetComponent<Image>();
                if (img != null)
                    img.sprite = t.cancelDeleteButtonSprite;
                foreach (var txt in cancelDeleteButton.GetComponentsInChildren<Text>(true))
                    txt.color = t.buttonTextColor;
#if TMP_PRESENT
                foreach (var tmp in cancelDeleteButton.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.buttonTextColor;
#endif
            }

            if (slotNameText != null)
                slotNameText.color = t.textColor;
#if TMP_PRESENT
            if (slotNameTMP != null)
                slotNameTMP.color = t.textColor;
#endif
            if (metadataText != null)
                metadataText.color = t.textColor;
#if TMP_PRESENT
            if (metadataTMP != null)
                metadataTMP.color = t.textColor;
#endif

            if (customMetadataContainer != null)
            {
                foreach (var txt in customMetadataContainer.GetComponentsInChildren<Text>(true))
                    txt.color = t.textColor;
#if TMP_PRESENT
                foreach (var tmp in customMetadataContainer.GetComponentsInChildren<TMP_Text>(true))
                    tmp.color = t.textColor;
#endif
            }

            // Ensure the rename input field text/placeholder/caret colors and background are themed too.
            if (renameInput != null)
            {
                try
                {
                    // Background
                    var bgImg = renameInput.GetComponent<Image>();
                    if (bgImg != null)
                    {
                        if (t.inputBackgroundSprite != null)
                            bgImg.sprite = t.inputBackgroundSprite;
                        if (t.inputBackgroundColor.a > 0f)
                            bgImg.color = t.inputBackgroundColor;
                    }

                    // Text
                    if (renameInput.textComponent != null && t.inputTextColor.a > 0f)
                        renameInput.textComponent.color = t.inputTextColor;
                    if (renameInput.placeholder is Text phText)
                    {
                        if (t.inputPlaceholderColor.a > 0f)
                        {
                            phText.color = t.inputPlaceholderColor;
                        }
                        else if (t.inputTextColor.a > 0f)
                        {
                            var c = t.inputTextColor;
                            c.a = Mathf.Clamp01(c.a * 0.6f);
                            phText.color = c;
                        }
                        // else leave as-is
                    }
                    // Caret and selection colors to be visible on dark/light themes
                    if (t.inputCaretColor.a > 0f)
                    {
                        renameInput.caretColor = t.inputCaretColor;
                    }
                    else if (t.inputTextColor.a > 0f)
                    {
                        renameInput.caretColor = t.inputTextColor;
                    }

                    if (t.inputSelectionColor.a > 0f)
                    {
                        renameInput.selectionColor = t.inputSelectionColor;
                    }
                    else if (t.inputTextColor.a > 0f)
                    {
                        var sel = t.inputTextColor;
                        sel.a = 0.35f;
                        renameInput.selectionColor = sel;
                    }
                }
                catch { /* ignore non-critical theming errors */ }
            }
#if TMP_PRESENT
            if (renameInputTMP != null)
            {
                try
                {
                    // Background
                    var bgImg = renameInputTMP.GetComponent<Image>();
                    if (bgImg != null)
                    {
                        if (t.inputBackgroundSprite != null)
                            bgImg.sprite = t.inputBackgroundSprite;
                        if (t.inputBackgroundColor.a > 0f)
                            bgImg.color = t.inputBackgroundColor;
                    }

                    // Text
                    if (renameInputTMP.textComponent != null && t.inputTextColor.a > 0f)
                        renameInputTMP.textComponent.color = t.inputTextColor;
                    if (renameInputTMP.placeholder is TMP_Text phTmp)
                    {
                        if (t.inputPlaceholderColor.a > 0f)
                        {
                            phTmp.color = t.inputPlaceholderColor;
                        }
                        else if (t.inputTextColor.a > 0f)
                        {
                            var c = t.inputTextColor;
                            c.a = Mathf.Clamp01(c.a * 0.6f);
                            phTmp.color = c;
                        }
                        // else leave as-is
                    }
                    if (t.inputCaretColor.a > 0f)
                    {
                        renameInputTMP.caretColor = t.inputCaretColor;
                    }
                    else if (t.inputTextColor.a > 0f)
                    {
                        renameInputTMP.caretColor = t.inputTextColor;
                    }

                    if (t.inputSelectionColor.a > 0f)
                    {
                        renameInputTMP.selectionColor = t.inputSelectionColor;
                    }
                    else if (t.inputTextColor.a > 0f)
                    {
                        var sel = t.inputTextColor;
                        sel.a = 0.35f;
                        renameInputTMP.selectionColor = sel;
                    }
                }
                catch { /* ignore non-critical theming errors */ }
            }
#endif

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                if (t.sizeDelta != Vector2.zero)
                    rt.sizeDelta = t.sizeDelta;
                if (t.positionOffset != Vector3.zero)
                    rt.localPosition += t.positionOffset;
            }
        }

        /// <summary>
        /// Gets the custom slot name from metadata if the parent has a slot name metadata key configured.
        /// </summary>
        /// <returns>The custom slot name or null if not available.</returns>
        string GetCustomSlotName()
        {
            if (parent == null || string.IsNullOrEmpty(parent.SlotNameMetadataKey) || slot == null)
                return null;

            // Prefer the live default value from SaveSettings first (designer-controlled)
            var settings = SaveManager.Instance?.SaveSettings;
            var defaultMetadata = settings?.defaultSlotMetadata?.ToDictionary();
            if (defaultMetadata != null &&
                defaultMetadata.TryGetValue(parent.SlotNameMetadataKey, out var customName) && !string.IsNullOrEmpty(customName))
            {
                return customName;
            }

            // Fall back to the slot's own custom metadata when default is not set
            if (slot.CustomMetadata != null && slot.CustomMetadata.TryGetValue(parent.SlotNameMetadataKey, out customName))
            {
                return customName;
            }

            return null;
        }

        // Button callbacks
        public void OnSaveClicked()
        {
            if (isSaving)
            {
                Logger.Log($"SaveSlotEntryUI: Ignoring save click for slot {slot?.SlotNumber} because a save is already in progress.", LogLevel.Warning);
                return;
            }

            SetOverlayActive(true);
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                UnsubscribeSaveEvents();
                mgr.OnSaveCompleted += OnSaveCompleted;
                mgr.OnSaveFailed    += OnSaveFailed;
            }

            // Mark saving state & disable save button explicitly (overlay alone may not visually communicate state on WebGL)
            isSaving = true;
            if (saveButton != null)
                saveButton.interactable = false;
            // Start watchdog to ensure UI recovers even if events never fire (e.g. platform-specific coroutine abort)
            if (saveWatchdog != null)
                StopCoroutine(saveWatchdog);
            saveWatchdog = StartCoroutine(SaveInProgressWatchdog());

            // Check if parent has a custom slot name metadata key configured
            string customSlotName = GetCustomSlotName();
            if (!string.IsNullOrEmpty(customSlotName))
            {
                parent.SaveSlot(slot.SlotNumber, customSlotName);
            }
            else
            {
                parent.SaveSlot(slot.SlotNumber);
            }
        }

        void OnSaveCompleted(object sender, SaveLoadEventArgs e)
        {
            if (e.Slot?.SlotNumber != slot?.SlotNumber)
                return;

            Refresh(e.Slot);
            UnsubscribeSaveEvents();
            SetOverlayActive(false);
            CompleteSaveUIRecovery();

            // Don't trigger full refresh for Supabase backend immediately after save
            // to avoid race conditions with remote slot synchronization
            if (SaveManager.Instance?.SaveSettings?.backend != SaveBackend.Supabase)
            {
                // Ensure other slots get updated so newly saved entries are
                // recognized by load buttons and metadata displays.
                _ = parent?.ForceRefreshSlotsAsync();
            }
        }

        void OnSaveFailed(object sender, OperationFailedEventArgs e)
        {
            if (e.Slot?.SlotNumber != slot?.SlotNumber)
                return;

            UnsubscribeSaveEvents();
            SetOverlayActive(false);
            CompleteSaveUIRecovery();
        }

        void UnsubscribeSaveEvents()
        {
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                mgr.OnSaveCompleted -= OnSaveCompleted;
                mgr.OnSaveFailed    -= OnSaveFailed;
            }
        }

        void CompleteSaveUIRecovery()
        {
            if (!isSaving)
                return;
            isSaving = false;
            if (saveButton != null)
                saveButton.interactable = true;
            if (saveWatchdog != null)
            {
                StopCoroutine(saveWatchdog);
                saveWatchdog = null;
            }
        }

        IEnumerator SaveInProgressWatchdog()
        {
            // Wait up to 15 seconds; if still saving then recover UI.
            const float timeout = 15f;
            float elapsed = 0f;
            while (elapsed < timeout && isSaving)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (isSaving)
            {
                Logger.Log($"SaveSlotEntryUI: Watchdog timeout after {timeout}s for slot {slot?.SlotNumber}. Forcing UI recovery.", LogLevel.Warning);
                SetOverlayActive(false);
                CompleteSaveUIRecovery();
            }
        }

        public void OnLoadClicked()
        {
            if (slot == null || slot.LastSaved <= DateTime.MinValue)
                return;
            // Check if parent has a custom slot name metadata key configured
            string customSlotName = GetCustomSlotName();
            if (!string.IsNullOrEmpty(customSlotName))
            {
                parent.LoadSlot(slot.SlotNumber, customSlotName);
            }
            else
            {
                parent.LoadSlot(slot.SlotNumber);
            }
        }
        public void OnDeleteClicked()
        {
            if (slot == null || slot.LastSaved <= DateTime.MinValue)
                return;
            if (deleteConfirmationWindow != null)
            {
                deleteConfirmationWindow.SetActive(true);
                // Ensure the confirmation window appears above the other UI
                // elements by moving it to the end of the sibling list.
                deleteConfirmationWindow.transform.SetAsLastSibling();
            }
            else
                OnConfirmDeleteClicked();
        }

        void OnConfirmDeleteClicked()
        {
            if (deleteConfirmationWindow != null)
                deleteConfirmationWindow.SetActive(false);

            SetOverlayActive(true);
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                UnsubscribeDeleteEvents();
                mgr.OnDeleteCompleted += OnDeleteCompleted;
                mgr.OnDeleteFailed    += OnDeleteFailed;
            }
            // Check if parent has a custom slot name metadata key configured
            if (parent == null)
            {
                Logger.Log("SaveSlotEntryUI: OnConfirmDeleteClicked - parent is null!", LogLevel.Error);
                SetOverlayActive(false);
                return;
            }
            
            string customSlotName = GetCustomSlotName();
            if (!string.IsNullOrEmpty(customSlotName))
            {
                Logger.Log($"SaveSlotEntryUI: Deleting slot {slot.SlotNumber} with custom name '{customSlotName}'", LogCategory.Other, LogLevel.Info);
                parent.DeleteSlot(slot.SlotNumber, customSlotName);
            }
            else
            {
                Logger.Log($"SaveSlotEntryUI: Deleting slot {slot.SlotNumber}", LogCategory.Other, LogLevel.Info);
                parent.DeleteSlot(slot.SlotNumber);
            }
        }

        void OnCancelDeleteClicked()
        {
            if (deleteConfirmationWindow != null)
                deleteConfirmationWindow.SetActive(false);
        }

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        async void OnAuthSignedIn(AuthEventsRelay.SignedInArgs args)
        {
            // Early exit if slot is not initialized yet
            if (slot == null)
                return;

            var mgr = SaveManager.Instance;
            var ss = mgr?.SaveSettings;
            if (ss != null && ss.backend == SaveBackend.UnityCloudSave)
            {
                await mgr.WaitForCloudConnectionAsync();
                await mgr.WaitForSaveSlotsAsync();
                var updatedSlot = mgr.GetSaveSlotByNumber(slot.SlotNumber);
                Refresh(updatedSlot);
            }
        }
#endif

        void TryDownloadScreenshot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotEntryUI] WebGL: TryDownloadScreenshot called for slot {slot?.SlotNumber}");
#endif
            
            if (isDownloadingScreenshot)
            {
                Logger.Log($"SaveSlotEntryUI: Screenshot download already in progress for slot {slot?.SlotNumber}, skipping duplicate download", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Download already in progress for slot {slot?.SlotNumber}");
#endif
                return;
            }
            
            if (slot == null)
            {
                Logger.Log("SaveSlotEntryUI: Screenshot download skipped – slot is null.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Download skipped - slot is null");
#endif
                return;
            }

            var mgr = SaveManager.Instance;
            var settings = mgr?.SaveSettings;
            if (settings == null)
            {
                Logger.Log("SaveSlotEntryUI: Screenshot download skipped – settings unavailable.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Download skipped - settings null: {settings == null}");
#endif
                return;
            }

            // Require Unity Services to be initialized and the player to be signed-in before cloud ops
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            if (settings.backend == SaveBackend.UnityCloudSave && settings.enableCloudSave)
            {
                if (Unity.Services.Core.UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized ||
                    !Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
                {
                    Logger.Log("SaveSlotEntryUI: Skipping screenshot download – Unity Services not initialized or player not signed-in.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SaveSlotEntryUI] WebGL: Skipping screenshot download – services not initialized or not signed-in");
#endif
                    return;
                }
            }
#endif

            // If cloud save is disabled, we expect the screenshot to be local only
            // so skip remote download attempts and log the expected local path for verification
            if (!settings.enableCloudSave)
            {
                string localFolder = SaveManager.Instance?.RootPath != null 
                    ? System.IO.Path.Combine(SaveManager.Instance.RootPath, settings.screenshotFolderName)
                    : "<unknown>";
                string expectedFile = slot?.ScreenshotFileName ?? "<null>";
                string expectedPath = localFolder != "<unknown>" && !string.IsNullOrEmpty(expectedFile)
                    ? System.IO.Path.Combine(localFolder, expectedFile)
                    : "<unknown>";
                Logger.Log($"SaveSlotEntryUI: Cloud disabled – not downloading screenshot. Expected local path: '{expectedPath}' (folder: '{localFolder}', file: '{expectedFile}')", LogCategory.Other, LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Cloud disabled – not downloading screenshot. Expected local path: '{expectedPath}'");
#endif
                return;
            }

            // Download policy:
            // - Supabase: always download
            // - Unity Cloud Save: allow download even when Keep Local Mirror is ON (acts as fallback after reload)
            // - Other backends: skip when Keep Local Mirror is ON
            if (settings.enableCloudSave && settings.keepLocalMirror &&
                settings.backend != SaveBackend.Supabase && settings.backend != SaveBackend.UnityCloudSave)
            {
                Logger.Log("SaveSlotEntryUI: Screenshot download skipped – keepLocalMirror enabled for this backend.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Download skipped - keepLocalMirror: {settings.keepLocalMirror}, backend: {settings.backend}");
#endif
                return;
            }

            if (string.IsNullOrEmpty(slot.ScreenshotFileName))
            {
                Logger.Log("SaveSlotEntryUI: Screenshot download skipped – no screenshot file name present.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Download skipped - ScreenshotFileName is null or empty: '{slot.ScreenshotFileName}'");
#endif
                return;
            }

            // Check global download tracking to prevent multiple simultaneous downloads of the same file
            lock (downloadLock)
            {
                if (downloadingScreenshots.Contains(slot.ScreenshotFileName))
                {
                    Logger.Log($"SaveSlotEntryUI: Screenshot '{slot.ScreenshotFileName}' is already being downloaded globally, skipping duplicate.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Screenshot '{slot.ScreenshotFileName}' already downloading globally");
#endif
                    return;
                }
                downloadingScreenshots.Add(slot.ScreenshotFileName);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotEntryUI] WebGL: Starting screenshot download for slot {slot.SlotNumber}, key: '{slot.ScreenshotFileName}', backend: {settings.backend}");
#endif
            Logger.Log($"SaveSlotEntryUI: Initiating screenshot download for '{slot.ScreenshotFileName}'.", LogLevel.Info);
            // Sanitize key: remove/replace whitespace to avoid Data API rejections
            string sanitizedKey = Regex.Replace(slot.ScreenshotFileName, "\\s+", "_");
            StartCoroutine(DownloadScreenshotCoroutine(settings, sanitizedKey));
        }

        System.Collections.IEnumerator DownloadScreenshotCoroutine(SaveSettings settings, string key)
        {
            isDownloadingScreenshot = true;
            SetOverlayActive(true);
            Logger.Log($"SaveSlotEntryUI: Starting download of screenshot '{key}' using backend {settings.backend}.", LogLevel.Info);
            byte[] data = null;

            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries && data == null; attempt++)
            {
                System.Threading.Tasks.Task<byte[]> task = null;

                if (settings.backend == SaveBackend.Supabase &&
                    SaveManager.Instance.SaveSystem is SupabaseSaveSystem supabase)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Attempt {attempt + 1}: Calling supabase.DownloadScreenshotAsync('{key}')");
#endif
                    task = supabase.DownloadScreenshotAsync(key);
                }
                else if (settings.backend == SaveBackend.Firebase &&
                         SaveManager.Instance.SaveSystem is FirebaseSaveSystem firebase)
                {
                    task = firebase.DownloadScreenshotAsync(key);
                }
#if REMEMBERME_CLOUDSAVE_PRESENT
                else if (settings.backend == SaveBackend.UnityCloudSave && settings.cloudSaveScreenshots)
                {
                    // Use coroutine-based approach for WebGL compatibility (no Task.Run support)
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<byte[]>();
                    task = tcs.Task;
                    StartCoroutine(DownloadUnityCloudSaveScreenshotCoroutine(key, tcs));
                }
#endif
                else if (settings.backend == SaveBackend.MySQL &&
                         SaveManager.Instance.SaveSystem is MySqlSaveSystem mysql)
                {
                    task = mysql.DownloadScreenshotAsync(key);
                }

                if (task == null)
                    break;

                while (!task.IsCompleted)
                    yield return null;

                bool retry = false;
                try
                {
                    if (task.IsFaulted)
                    {
                        throw task.Exception?.GetBaseException() ?? new Exception("Task faulted with unknown error");
                    }
                    else if (task.IsCompletedSuccessfully)
                    {
                        data = task.Result;
#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log($"[SaveSlotEntryUI] WebGL: Download task completed, data length: {data?.Length ?? 0} bytes");
#endif
                    }
                    else
                    {
                        throw new Exception("Task completed but not successfully");
                    }
                }
                catch (Exception ex)
                {
                    bool notFound = ex.Message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (notFound && attempt < maxRetries - 1)
                    {
                        retry = true;
                    }
                    else
                    {
                        Logger.Log($"SaveSlotEntryUI: Failed to download screenshot '{key}' – {ex.Message}", LogLevel.Warning);
                        break;
                    }
                }

                if (retry)
                    yield return new WaitForSeconds(0.5f);
            }

            if (data != null && data.Length > 0 && screenshotImage != null)
            {
                // Decrypt if encryption is enabled for screenshots
                try
                {
                    var enc = SaveManager.Instance?.EncryptionService;
                    if (enc?.UseEncryption == true && settings.enableEncryption && settings.encryptScreenshots)
                    {
                        data = enc.MaybeDecrypt(data);
                    }
                }
                catch {}

                Texture2D remoteTex = new Texture2D(2, 2);
                if (remoteTex.LoadImage(data))
                {
                    screenshotImage.sprite = Sprite.Create(remoteTex,
                        new Rect(0, 0, remoteTex.width, remoteTex.height),
                        new Vector2(0.5f, 0.5f));
                    
                    // Force UI refresh without changing size (preserve editor-set dimensions)
                    Canvas.ForceUpdateCanvases();
                    
#if UNITY_WEBGL && !UNITY_EDITOR
                    // Cache downloaded data in memory for future loads in WebGL
                    if (settings.backend == SaveBackend.Supabase)
                    {
                        Arawn.CrystalSave.Runtime.ScreenshotManager.CacheScreenshotInWebGLMemory(key, data);
                        Debug.Log($"[SaveSlotEntryUI] WebGL: Cached screenshot '{key}' in memory ({data.Length} bytes)");
                    }
#endif
                    
                    Logger.Log($"SaveSlotEntryUI: Successfully downloaded and displayed screenshot '{key}' ({data.Length} bytes). ScreenshotImage active: {screenshotImage.gameObject.activeSelf}, enabled: {screenshotImage.enabled}.", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Screenshot '{key}' successfully displayed. Image active: {screenshotImage.gameObject.activeSelf}, enabled: {screenshotImage.enabled}");
#endif
                }
                else
                {
                    Logger.Log($"SaveSlotEntryUI: Failed to load image data for screenshot '{key}'.", LogLevel.Warning);
                    UnityEngine.Object.Destroy(remoteTex);
                }
            }
            else
            {
                if (data == null || data.Length == 0)
                    Logger.Log($"SaveSlotEntryUI: Screenshot '{key}' download returned no data.", LogLevel.Warning);
                if (screenshotImage == null)
                    Logger.Log($"SaveSlotEntryUI: Screenshot '{key}' downloaded but screenshotImage is null.", LogLevel.Warning);
            }

            isDownloadingScreenshot = false;
            SetOverlayActive(false);

            // Remove from global download tracking
            lock (downloadLock)
            {
                downloadingScreenshots.Remove(key);
            }
        }

#if REMEMBERME_CLOUDSAVE_PRESENT
    System.Collections.IEnumerator DownloadUnityCloudSaveScreenshotCoroutine(string key, System.Threading.Tasks.TaskCompletionSource<byte[]> tcs)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotEntryUI] WebGL: Starting Unity Cloud Save screenshot download for '{key}'");
#endif
            Logger.Log($"SaveSlotEntryUI: Starting Unity Cloud Save screenshot download for '{key}'", LogLevel.Info);
            
            // Try Data API first (new structured format used by WebGL)
            string dataApiKey;
            if (key.StartsWith("screenshot_"))
            {
                // Key already has the screenshot prefix (new format)
                dataApiKey = key;
            }
            else
            {
                // Key doesn't have prefix, add it (old format compatibility)
                string baseKey = key.Contains(".") ? 
                    key.Substring(0, key.LastIndexOf('.')) : key;
                dataApiKey = $"screenshot_{baseKey}";
            }

            // Replace whitespace to ensure Cloud Save Data API compatibility on WebGL
            if (!string.IsNullOrEmpty(dataApiKey))
                dataApiKey = Regex.Replace(dataApiKey, "\\s+", "_");

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotEntryUI] WebGL: Constructed Data API key: '{dataApiKey}' from original key: '{key}'");
#endif
            
            bool dataApiSuccess = false;
            byte[] resultData = null;
            
            // Try Data API
            System.Threading.Tasks.Task dataDownloadTask = null;
            
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: About to call UnityCloudSaveService.Instance.Data.Player.LoadAsync");
#endif
                dataDownloadTask = UnityCloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { dataApiKey });
                Logger.Log($"SaveSlotEntryUI: Attempting Data API download for screenshot: {dataApiKey}", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Data API LoadAsync task created successfully");
#endif
            }
            catch (Exception ex)
            {
                Logger.Log($"SaveSlotEntryUI: Data API setup failed for {dataApiKey}: {ex.Message}", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Exception during Data API setup: {ex.Message}");
#endif
            }            // Wait for Data API completion
            if (dataDownloadTask != null)
            {
                // Wait with timeout to avoid hanging overlay if the request stalls
                const float timeoutSeconds = 10f;
                float waited = 0f;
                while (!dataDownloadTask.IsCompleted && waited < timeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!dataDownloadTask.IsCompleted)
                {
                    Logger.Log($"SaveSlotEntryUI: Data API download timed out for {dataApiKey}", LogLevel.Warning);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Data API download timed out for {dataApiKey}");
#endif
                }
                
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Data API download task completed for {dataApiKey}");
#endif
                
                if (dataDownloadTask.Exception != null)
                {
                    Logger.Log($"SaveSlotEntryUI: Data API download failed: {dataDownloadTask.Exception.GetBaseException().Message}", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Data API download exception: {dataDownloadTask.Exception.GetBaseException().Message}");
#endif
                }
                else
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotEntryUI] WebGL: Data API download succeeded, parsing result...");
#endif
                    try
                    {
                        // Cast to the correct type that Unity Cloud Save API returns
                        var typedTask = (System.Threading.Tasks.Task<Dictionary<string, Unity.Services.CloudSave.Models.Item>>)dataDownloadTask;
                        var result = typedTask.Result;
#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log($"[SaveSlotEntryUI] WebGL: Got Data API result with {result.Count} items");
#endif
                        if (result.TryGetValue(dataApiKey, out var item))
                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                            Debug.Log($"[SaveSlotEntryUI] WebGL: Found item for key {dataApiKey}, type: {item.GetType().Name}");
#endif
                            try
                            {
                                // Get the value from the Item
                                var itemValue = item.Value.GetAs<object>();
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log($"[SaveSlotEntryUI] WebGL: Item value type: {itemValue?.GetType().Name}");
#endif
                                
                                // Handle JObject (Newtonsoft.Json.Linq.JObject)
                                if (itemValue is Newtonsoft.Json.Linq.JObject jObject)
                                {
#if UNITY_WEBGL && !UNITY_EDITOR
                                    Debug.Log($"[SaveSlotEntryUI] WebGL: Processing JObject with {jObject.Count} properties");
                                    Debug.Log($"[SaveSlotEntryUI] WebGL: JObject properties: {string.Join(", ", jObject.Properties().Select(p => p.Name))}");
#endif
                                    
                                    // Try to get the "data" property from the JObject
                                    if (jObject.TryGetValue("data", out var dataToken))
                                    {
                                        string base64Data = dataToken.ToString();
                                        if (!string.IsNullOrEmpty(base64Data))
                                        {
                                            resultData = Convert.FromBase64String(base64Data);
                                            dataApiSuccess = true;
                                            Logger.Log($"SaveSlotEntryUI: Screenshot downloaded via Data API with JObject format: {resultData.Length} bytes", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                                            Debug.Log($"[SaveSlotEntryUI] WebGL: Successfully parsed JObject screenshot data: {resultData.Length} bytes");
#endif
                                        }
                                        else
                                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                                            Debug.Log($"[SaveSlotEntryUI] WebGL: JObject 'data' property is empty");
#endif
                                        }
                                    }
                                    else
                                    {
#if UNITY_WEBGL && !UNITY_EDITOR
                                        Debug.Log($"[SaveSlotEntryUI] WebGL: JObject does not contain 'data' property");
#endif
                                    }
                                }
                                // Try to parse as structured data (Dictionary)
                                else if (itemValue is Dictionary<string, object> screenshotData)
                                {
                                    if (screenshotData.TryGetValue("data", out var base64Obj))
                                    {
                                        string base64Data = base64Obj.ToString();
                                        if (!string.IsNullOrEmpty(base64Data))
                                        {
                                            resultData = Convert.FromBase64String(base64Data);
                                            dataApiSuccess = true;
                                            Logger.Log($"SaveSlotEntryUI: Screenshot downloaded via Data API with structured format: {resultData.Length} bytes", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                                            Debug.Log($"[SaveSlotEntryUI] WebGL: Successfully parsed structured screenshot data: {resultData.Length} bytes");
#endif
                                        }
                                    }
                                }
                                else
                                {
                                    // Fallback: try as direct base64 string
                                    string base64Data = itemValue?.ToString();
                                    if (!string.IsNullOrEmpty(base64Data))
                                    {
                                        resultData = Convert.FromBase64String(base64Data);
                                        dataApiSuccess = true;
                                        Logger.Log($"SaveSlotEntryUI: Screenshot downloaded via Data API with legacy format: {resultData.Length} bytes", LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                                        Debug.Log($"[SaveSlotEntryUI] WebGL: Successfully parsed legacy screenshot data: {resultData.Length} bytes");
#endif
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"SaveSlotEntryUI: Error parsing Data API screenshot data for {dataApiKey}: {ex.Message}", LogLevel.Warning);
                            }
                        }
                        else
                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                            Debug.Log($"[SaveSlotEntryUI] WebGL: No item found for key {dataApiKey} in Data API result");
                            Debug.Log($"[SaveSlotEntryUI] WebGL: Available keys: {string.Join(", ", result.Keys)}");
#endif
                            Logger.Log($"SaveSlotEntryUI: No item found for key {dataApiKey} in Data API result", LogLevel.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"SaveSlotEntryUI: Error processing Data API result for {dataApiKey}: {ex.Message}", LogLevel.Warning);
#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log($"[SaveSlotEntryUI] WebGL: Error processing Data API result: {ex.Message}");
#endif
                    }
                }
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            // Fallback to Files API for non-WebGL platforms or screenshots uploaded via Files API
            if (!dataApiSuccess)
            {
                System.Threading.Tasks.Task<byte[]> filesDownloadTask = null;
                
                try
                {
                    Logger.Log($"SaveSlotEntryUI: Attempting Files API download for screenshot: {key}", LogLevel.Info);
                    filesDownloadTask = UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(key);
                }
                catch (Exception ex)
                {
                    Logger.Log($"SaveSlotEntryUI: Files API setup failed for {key}: {ex.Message}", LogLevel.Warning);
                }
                
                // Wait for Files API completion
                if (filesDownloadTask != null)
                {
                    while (!filesDownloadTask.IsCompleted)
                        yield return null;
                    
                    if (filesDownloadTask.Exception != null)
                    {
                        Logger.Log($"SaveSlotEntryUI: Files API download failed: {filesDownloadTask.Exception.GetBaseException().Message}", LogLevel.Warning);
                    }
                    else
                    {
                        resultData = filesDownloadTask.Result;
                        if (resultData != null && resultData.Length > 0)
                        {
                            Logger.Log($"SaveSlotEntryUI: Screenshot downloaded via Files API: {resultData.Length} bytes", LogLevel.Info);
                        }
                    }
                }
            }
#else
            if (!dataApiSuccess)
            {
                Logger.Log($"SaveSlotEntryUI: WebGL platform: skipping Files API fallback for {key}", LogLevel.Info);
            }
#endif

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Setting TaskCompletionSource result, data length: {resultData?.Length ?? 0}");
#endif
                tcs.SetResult(resultData);
            }
            catch (Exception ex)
            {
                Logger.Log($"SaveSlotEntryUI: Failed to set result for screenshot '{key}': {ex.Message}", LogLevel.Error);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotEntryUI] WebGL: Exception setting result: {ex.Message}");
#endif
                tcs.SetException(ex);
            }
        }
#endif

        void OnDeleteCompleted(object sender, SaveManagerEventArgs e)
        {
            if (e.Slot?.SlotNumber != slot?.SlotNumber)
                return;

            Refresh(e.Slot);
            UnsubscribeDeleteEvents();
            SetOverlayActive(false);

            // Ensure other slots keep their metadata and screenshots after a
            // deletion by forcing the parent UI to refresh all entries.
            _ = parent?.ForceRefreshSlotsAsync();
        }

        void OnDeleteFailed(object sender, OperationFailedEventArgs e)
        {
            if (e.Slot?.SlotNumber != slot?.SlotNumber)
                return;

            UnsubscribeDeleteEvents();
            SetOverlayActive(false);
        }

        void UnsubscribeDeleteEvents()
        {
            var mgr = SaveManager.Instance;
            if (mgr != null)
            {
                mgr.OnDeleteCompleted -= OnDeleteCompleted;
                mgr.OnDeleteFailed    -= OnDeleteFailed;
            }
        }
        public void OnRenameClicked()
        {
            if (slot == null || slot.LastSaved <= DateTime.MinValue)
                return;
            if (hideRenameInputOnStart)
            {
#if TMP_PRESENT
                if (textMode == TextMode.TextMeshPro && renameInputTMP != null)
                {
                    if (!renameInputTMP.gameObject.activeSelf)
                    {
                        renameInputTMP.gameObject.SetActive(true);
                        renameInputTMP.ActivateInputField();
                    }
                    return;
                }
#endif
                if (renameInput != null)
                {
                    if (!renameInput.gameObject.activeSelf)
                    {
                        renameInput.gameObject.SetActive(true);
                        renameInput.ActivateInputField();
                    }
                    return;
                }
            }

            if (renameInput != null
#if TMP_PRESENT
                && textMode == TextMode.Legacy
#endif
               )
            {
                _ = parent.RenameSlotAsync(slot.SlotNumber, renameInput.text);
            }
#if TMP_PRESENT
            else if (renameInputTMP != null && textMode == TextMode.TextMeshPro)
            {
                _ = parent.RenameSlotAsync(slot.SlotNumber, renameInputTMP.text);
            }
#endif
        }
    }
}
#endif
