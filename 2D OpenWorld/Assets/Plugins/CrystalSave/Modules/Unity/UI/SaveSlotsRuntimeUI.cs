#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Arawn.CrystalSave.Runtime;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Authentication;
using Unity.Services.Core;
#endif

namespace Arawn.CrystalSave.UI
{
    /// <summary>
    /// Runtime canvas-based UI that lists all save slots defined in <see cref="SaveSettings"/>.
    /// Generates entry elements dynamically and supports vertical scrolling.
    /// Game designers can toggle individual UI features in the Inspector.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class SaveSlotsRuntimeUI : MonoBehaviour
    {
        [Header("UI Setup")]
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] SaveSlotEntryUI slotPrefab;
        [SerializeField] Image panel;
        [SerializeField] Image titleBackgroundPanel;
        [SerializeField] Text  titleSaveGameText;

        [Header("Theme")]
        [SerializeField] SaveSlotsRuntimeTheme theme;

        [Header("Feature Toggles")]
        [SerializeField] bool showScreenshots = true;
        [SerializeField] bool showMetadata    = true;
        [SerializeField] bool showCustomMetadata = true;
        [SerializeField] bool showRename      = true;
        [SerializeField] bool showLoad        = true;
        [SerializeField] bool showSave        = true;
        [SerializeField] bool showDelete      = true;

        [Header("Save Behaviour")]
        [SerializeField, Tooltip("When enabled, saving into an occupied slot preserves its existing slot name instead of overwriting it with the scene name.")]
        bool preserveSlotName = true;

    [Header("Filtering")]
    [SerializeField, Tooltip("Comma-separated list of slot numbers to hide in the UI (e.g., '3,4,5'). Only affects rendering; underlying slots remain functional.")]
    string hiddenSlotNumbers = string.Empty;

        [Header("Slot Naming")]
        [SerializeField, Tooltip("Metadata key used as slot name when saving. Set to empty or Ignore to use the scene name.")]
        string slotNameMetadataKey = string.Empty;

        /// <summary>
        /// Gets the metadata key used as slot name when saving.
        /// </summary>
        public string SlotNameMetadataKey => slotNameMetadataKey;

        readonly List<SaveSlotEntryUI> entries = new();

        Canvas canvas;

        void Awake()
        {
            EnsureEventSystem();
            canvas = GetComponentInParent<Canvas>();
        }

        public static void EnsureEventSystem()
        {
#pragma warning disable CS0618 // Suppress FindFirstObjectByType deprecation warning for cross-version compatibility
#if UNITY_2023_1_OR_NEWER
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
            var eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
#pragma warning restore CS0618
            if (eventSystem != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if USE_NEW_INPUT || (ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER)
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            var module = go.AddComponent<StandaloneInputModule>();
#if !UNITY_2020_1_OR_NEWER
            module.forceModuleActive = true;
#endif
#endif
        }

        void OnEnable()
        {
            Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: OnEnable called", LogCategory.Other, LogLevel.Info);
            
            // Ensure this UI canvas is rendered on top of others when the prefab
            // is dropped into the scene.
            transform.SetAsLastSibling();
            ApplyTheme();
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            AuthEventsRelay.PlayerSignedIn += OnAuthSignedIn;
            // In some platforms (e.g. WebGL) authentication may already be
            // completed before this component subscribes to the event. Ensure
            // the UI refreshes immediately if the player is already signed in.
            // Access the authentication service only after Unity Services are
            // initialized to prevent runtime exceptions.
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                _ = ForceRefreshSlotsAsync();
            }
#endif
#if UNITY_EDITOR
            SaveSlotEntryTheme.ThemeChanged    += OnEntryThemeChanged;
            SaveSlotsRuntimeTheme.ThemeChanged += OnRuntimeThemeChanged;
#endif
            SubscribeScreenshotEvents();
        }

        void OnDisable()
        {
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            AuthEventsRelay.PlayerSignedIn -= OnAuthSignedIn;
#endif
#if UNITY_EDITOR
            SaveSlotEntryTheme.ThemeChanged    -= OnEntryThemeChanged;
            SaveSlotsRuntimeTheme.ThemeChanged -= OnRuntimeThemeChanged;
#endif
            UnsubscribeScreenshotEvents();
        }

#if UNITY_EDITOR
        void OnRuntimeThemeChanged(SaveSlotsRuntimeTheme changed)
        {
            if (!Application.isPlaying && changed == theme)
                ApplyTheme();
        }

        void OnEntryThemeChanged(SaveSlotEntryTheme changed)
        {
            if (!Application.isPlaying && theme != null && changed == theme.entryTheme)
            {
                foreach (var entry in GetComponentsInChildren<SaveSlotEntryUI>(true))
                    entry.ApplyTheme(changed);
            }
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyTheme();
                foreach (var entry in GetComponentsInChildren<SaveSlotEntryUI>(true))
                    entry.ApplyTheme(theme != null ? theme.entryTheme : null);
            }
        }
#endif

        async void Start()
        {
            Arawn.CrystalSave.Runtime.Logger.Log($"SaveSlotsRuntimeUI: Start called, SaveManager.IsInitialized: {SaveManager.IsInitialized}", LogCategory.Other, LogLevel.Info);
            
            if (!SaveManager.IsInitialized)
            {
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: SaveManager not initialized, subscribing to Initialized event", LogCategory.Other, LogLevel.Info);
                SaveManager.Initialized += OnInitialised;
            }
            else
            {
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: SaveManager already initialized, calling SetupAsync directly", LogCategory.Other, LogLevel.Info);
                await SetupAsync();
            }
        }

        async void OnInitialised(SaveManager mgr)
        {
            Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: OnInitialised callback triggered", LogCategory.Other, LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[SaveSlotsRuntimeUI] WebGL: OnInitialised callback triggered");
#endif
            SaveManager.Initialized -= OnInitialised;
            await SetupAsync();
        }

        async Task SetupAsync()
        {
            Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: SetupAsync started", LogCategory.Other, LogLevel.Info);
            
            var mgr = SaveManager.Instance;
            if (mgr == null)
            {
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: SaveManager.Instance is null in SetupAsync!", LogCategory.Other, LogLevel.Error);
                return;
            }
            // Wait for the SaveManager to establish a connection to the cloud
            // provider when Cloud Save is enabled. This avoids null reference
            // errors when slot data is not yet available.

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[SaveSlotsRuntimeUI] WebGL: Starting SetupAsync");
#endif

            try
            {
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: Starting WaitForCloudConnectionAsync", LogCategory.Other, LogLevel.Info);
                await mgr.WaitForCloudConnectionAsync();
                
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: Starting InitializeSaveSlotsAsync", LogCategory.Other, LogLevel.Info);
                await mgr.InitializeSaveSlotsAsync(mgr.SaveSettings.numberOfSaveSlots);
                
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: Starting InitializeQuickSaveSlotsAsync", LogCategory.Other, LogLevel.Info);
                await mgr.InitializeQuickSaveSlotsAsync(mgr.SaveSettings.numberOfQuickSaveSlots);
                await mgr.RefreshRemoteSlotsAsync();
                await mgr.WaitForSaveSlotsAsync();
                await mgr.WaitForQuickSlotsAsync();
                
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: Subscribing to OnSaveSlotsUpdated event", LogCategory.Other, LogLevel.Info);
                mgr.OnSaveSlotsUpdated += OnSaveSlotsUpdated;
                
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: Calling initial RefreshUI", LogCategory.Other, LogLevel.Info);
                RefreshUI();

                // Force refresh after a small delay to ensure any late initialization events are captured
                await Task.Delay(500);
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: Calling delayed RefreshUI", LogCategory.Other, LogLevel.Info);
                RefreshUI();

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: SetupAsync completed successfully");
#endif
            }
            catch (System.Exception ex)
            {
                Arawn.CrystalSave.Runtime.Logger.Log($"SaveSlotsRuntimeUI: Exception in SetupAsync: {ex.Message}", LogCategory.Other, LogLevel.Error);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogError($"[SaveSlotsRuntimeUI] WebGL: Error during SetupAsync: {ex.Message}\n{ex.StackTrace}");
#endif
                Debug.LogError($"SaveSlotsRuntimeUI: Failed to setup: {ex.Message}");
                
                // Still try to set up event handler and refresh UI
                mgr.OnSaveSlotsUpdated += OnSaveSlotsUpdated;
                RefreshUI();
            }
        }

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        async void OnAuthSignedIn(AuthEventsRelay.SignedInArgs args)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotsRuntimeUI] WebGL: OnAuthSignedIn called with provider: {args.Provider}, playerId: {args.PlayerId}");
#endif
            var ss = SaveManager.Instance?.SaveSettings;
            if (ss != null && ss.backend == SaveBackend.UnityCloudSave)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Backend is UnityCloudSave, calling ForceRefreshSlotsAsync");
#endif
                await ForceRefreshSlotsAsync();
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            else
            {
                Debug.Log($"[SaveSlotsRuntimeUI] WebGL: Backend is not UnityCloudSave (backend: {ss?.backend}), skipping refresh");
            }
#endif
        }
#endif

        public async Task ForceRefreshSlotsAsync()
        {
            var mgr = SaveManager.Instance;
            if (mgr == null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogError("[SaveSlotsRuntimeUI] WebGL: SaveManager.Instance is null");
#endif
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[SaveSlotsRuntimeUI] WebGL: Starting ForceRefreshSlotsAsync");
            Debug.Log($"[SaveSlotsRuntimeUI] WebGL: SaveSettings - backend: {mgr.SaveSettings.backend}, enableCloudSave: {mgr.SaveSettings.enableCloudSave}, cloudSaveMetadata: {mgr.SaveSettings.cloudSaveMetadata}, keepLocalMirror: {mgr.SaveSettings.keepLocalMirror}");
#endif

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Step 1 - WaitForCloudConnectionAsync");
#endif
                await mgr.WaitForCloudConnectionAsync();

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Step 2 - InitializeSaveSlotsAsync");
#endif
                await mgr.InitializeSaveSlotsAsync(mgr.SaveSettings.numberOfSaveSlots);

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Step 3 - InitializeQuickSaveSlotsAsync");
#endif
                await mgr.InitializeQuickSaveSlotsAsync(mgr.SaveSettings.numberOfQuickSaveSlots);

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Step 4 - RefreshRemoteSlotsAsync");
#endif
                try
                {
                    // Add timeout for WebGL builds where cloud operations might hang
                    var refreshTask = mgr.RefreshRemoteSlotsAsync();
                    var timeoutTask = Task.Delay(10000); // 10 second timeout
                    
                    var completedTask = await Task.WhenAny(refreshTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.LogWarning("[SaveSlotsRuntimeUI] WebGL: RefreshRemoteSlotsAsync timed out after 10 seconds, continuing without cloud sync");
#endif
                    }
                    else
                    {
                        await refreshTask; // Await the actual task to get any exceptions
#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log("[SaveSlotsRuntimeUI] WebGL: RefreshRemoteSlotsAsync completed successfully");
#endif
                    }
                }
                catch (System.Exception ex)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.LogError($"[SaveSlotsRuntimeUI] WebGL: RefreshRemoteSlotsAsync failed: {ex.Message}\n{ex.StackTrace}");
#endif
                    Debug.LogError($"SaveSlotsRuntimeUI: RefreshRemoteSlotsAsync failed: {ex.Message}");
                    // Continue with the rest of the process even if cloud refresh fails
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Step 5 - WaitForSaveSlotsAsync");
#endif
                await mgr.WaitForSaveSlotsAsync();

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Step 6 - WaitForQuickSlotsAsync");
#endif
                await mgr.WaitForQuickSlotsAsync();

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: Step 7 - RefreshUI");
#endif
                RefreshUI();

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SaveSlotsRuntimeUI] WebGL: ForceRefreshSlotsAsync completed successfully");
#endif
            }
            catch (System.Exception ex)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogError($"[SaveSlotsRuntimeUI] WebGL: Error during ForceRefreshSlotsAsync: {ex.Message}\n{ex.StackTrace}");
#endif
                Debug.LogError($"SaveSlotsRuntimeUI: Failed to refresh slots after login: {ex.Message}");
                
                // Still try to refresh UI with whatever slots are available
                RefreshUI();
            }
        }

        void OnDestroy()
        {
            var mgr = SaveManager.Instance;
            if (mgr != null)
                mgr.OnSaveSlotsUpdated -= OnSaveSlotsUpdated;
        }

        void OnSaveSlotsUpdated()
        {
            Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: OnSaveSlotsUpdated event triggered, calling RefreshUI", LogCategory.Other, LogLevel.Info);
            RefreshUI();
        }

        void RefreshUI()
        {
            var mgr   = SaveManager.Instance;
            if (mgr == null) 
            {
                Arawn.CrystalSave.Runtime.Logger.Log("SaveSlotsRuntimeUI: RefreshUI called but SaveManager.Instance is null", LogCategory.Other, LogLevel.Warning);
                return;
            }

            int total = mgr.SaveSettings.numberOfSaveSlots;
            Arawn.CrystalSave.Runtime.Logger.Log($"SaveSlotsRuntimeUI: RefreshUI called, total slot count: {total}, SaveManager has {mgr.GetSaveSlots()?.Count ?? 0} total slots", LogCategory.Other, LogLevel.Info);

            // Compute visible slots based on hide list
            var hidden = ParseHiddenSlots();
            var visibleSlots = new List<SaveSlot>(total);
            for (int n = 1; n <= total; n++)
            {
                if (hidden.Contains(n))
                    continue;
                var slot = mgr.GetSaveSlotByNumber(n);
                visibleSlots.Add(slot);
            }

            // Ensure we have enough entry instances, but reuse existing objects to avoid losing async screenshot/download state.
            for (int i = 0; i < visibleSlots.Count; i++)
            {
                if (i >= entries.Count)
                {
                    var newEntry = Instantiate(slotPrefab, scrollRect.content);
                    entries.Add(newEntry);
                }
                var slot = visibleSlots[i];
                var entry = entries[i];
                // Always re-initialize with the current slot and UI toggles
                entry.Initialise(slot,
                                 this, showScreenshots, showMetadata, showCustomMetadata,
                                 showRename, showLoad, showSave, showDelete,
                                 theme != null ? theme.entryTheme : null);
                entry.SetDisplayIndex(i + 1); // UI-only numbering
                entry.ApplyTheme(theme != null ? theme.entryTheme : null);
                if (!entry.gameObject.activeSelf)
                    entry.gameObject.SetActive(true);
            }
            // Deactivate any extra pre-existing entries
            for (int j = visibleSlots.Count; j < entries.Count; j++)
            {
                if (entries[j].gameObject.activeSelf)
                    entries[j].gameObject.SetActive(false);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotsRuntimeUI] WebGL: RefreshUI called, visible slot count: {visibleSlots.Count} (hidden: {string.Join(",", hidden)})");
            Debug.Log($"[SaveSlotsRuntimeUI] WebGL: SaveManager has {mgr.GetSaveSlots()?.Count ?? 0} total slots");
#endif

            // Log mapping for diagnostics
            for (int i = 0; i < visibleSlots.Count; i++)
            {
                var slot = visibleSlots[i];
                Arawn.CrystalSave.Runtime.Logger.Log($"SaveSlotsRuntimeUI: Visible index {i} (display {i + 1}) mapped to real slot {slot?.SlotNumber}", LogCategory.Other, LogLevel.Info);
#if UNITY_WEBGL && !UNITY_EDITOR
                if (slot != null)
                    Debug.Log($"[SaveSlotsRuntimeUI] WebGL: Visible index {i} -> real slot {slot.SlotNumber}, SlotName='{slot.SlotName}', LastSaved: {slot.LastSaved}");
#endif
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotsRuntimeUI] WebGL: RefreshUI completed");
#endif
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
            // Also remove fallback subscription safely
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

    void EnsureEntryCount(int count)
        {
            Arawn.CrystalSave.Runtime.Logger.Log($"SaveSlotsRuntimeUI: EnsureEntryCount called with count={count}, current entries.Count={entries.Count}", LogCategory.Other, LogLevel.Info);
            
            while (entries.Count < count)
            {
                var entry = Instantiate(slotPrefab, scrollRect.content);
                var slot = SaveManager.Instance.GetSaveSlotByNumber(entries.Count + 1);
                if (slot == null)
                {
                    Arawn.CrystalSave.Runtime.Logger.Log($"SaveSlotsRuntimeUI: Save slot {entries.Count + 1} is not available during EnsureEntryCount", LogCategory.Other, LogLevel.Warning);
                    Debug.LogWarning($"Save slot {entries.Count + 1} is not available.");
                }
                entry.Initialise(slot,
                                 this, showScreenshots, showMetadata, showCustomMetadata,
                                 showRename, showLoad, showSave, showDelete,
                                 theme != null ? theme.entryTheme : null);
                entry.ApplyTheme(theme != null ? theme.entryTheme : null);
                entries.Add(entry);
            }
            for (int i = 0; i < entries.Count; i++)
                entries[i].gameObject.SetActive(i < count);
        }

    // Note: we intentionally reuse entry instances across refreshes to preserve any async state in child components.

        HashSet<int> ParseHiddenSlots()
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(hiddenSlotNumbers)) return set;
            var parts = hiddenSlotNumbers.Split(',');
            foreach (var p in parts)
            {
                if (int.TryParse(p.Trim(), out int n) && n > 0)
                    set.Add(n);
            }
            return set;
        }

        // Called by SaveSlotEntryUI
        public async void SaveSlot(int number)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSlotsRuntimeUI] WebGL: SaveSlot called for slot {number}");
#endif

            string sceneName = SceneManager.GetActiveScene().name;
            string customSlotName = null;

            // Preserve existing slot name when re-saving occupied slots.
            if (preserveSlotName)
            {
                var existingSlot = SaveManager.Instance.GetSaveSlotByNumber(number);
                if (existingSlot != null && !string.IsNullOrEmpty(existingSlot.SlotName))
                    customSlotName = existingSlot.SlotName;
            }

            // When a metadata key is specified, attempt to retrieve the
            // corresponding value from the current save slot's custom
            // metadata and use it as the slot name. The key itself is just
            // a reference for designers and shouldn't be passed to the Save
            // method directly.
            if (string.IsNullOrEmpty(customSlotName) && !string.IsNullOrEmpty(slotNameMetadataKey))
            {
                // Prefer live defaults first (designer-set value), then fall back to the specific slot's metadata
                var settings = SaveManager.Instance?.SaveSettings;
                var defaultMetadata = settings?.defaultSlotMetadata?.ToDictionary();
                if (defaultMetadata != null)
                    defaultMetadata.TryGetValue(slotNameMetadataKey, out customSlotName);

                if (string.IsNullOrEmpty(customSlotName))
                {
                    var slotForName = SaveManager.Instance.GetSaveSlotByNumber(number);
                    if (slotForName?.CustomMetadata != null)
                        slotForName.CustomMetadata.TryGetValue(slotNameMetadataKey, out customSlotName);
                }
            }

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotsRuntimeUI] WebGL: Calling SaveAsync with scene name: {sceneName}, custom slot name: {customSlotName}");
#endif
                // Use async save to prevent browser hanging in WebGL
                // Pass the custom slot name directly to SaveAsync
                await SaveManager.Instance.SaveAsync(number, sceneName, customSlotName);

#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotsRuntimeUI] WebGL: SaveSlot completed successfully for slot {number}");
#endif
            }
            catch (System.Exception ex)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogError($"[SaveSlotsRuntimeUI] WebGL: SaveSlot failed for slot {number}: {ex.Message}");
#endif
                Debug.LogError($"SaveSlotsRuntimeUI: Save failed for slot {number}: {ex.Message}");
            }
        }
        public void LoadSlot(int number)   => SaveManager.Instance.Load(number, true);
        public void DeleteSlot(int number) => SaveManager.Instance.Delete(number);
        public void RenameSlot(int number, string newName)
            => SaveManager.Instance.RenameSlot(number, newName);

        // Overloaded methods that accept custom slot names for use by SaveSlotEntryUI
        public void SaveSlot(int number, string customSlotName)
        {
            if (string.IsNullOrEmpty(customSlotName))
            {
                if (preserveSlotName)
                {
                    var existingSlot = SaveManager.Instance.GetSaveSlotByNumber(number);
                    if (existingSlot != null && !string.IsNullOrEmpty(existingSlot.SlotName))
                    {
                        SaveManager.Instance.SaveAsync(number, null, existingSlot.SlotName);
                        return;
                    }
                }

                SaveSlot(number); // Use the default implementation
                return;
            }

            // Save with custom slot name using the overloaded SaveAsync method
            SaveManager.Instance.SaveAsync(number, null, customSlotName);
        }

        public void LoadSlot(int number, string customSlotName)
        {
            // Load operations typically don't use custom slot names, but included for consistency
            LoadSlot(number);
        }

        public void DeleteSlot(int number, string customSlotName)
        {
            // Delete operations typically don't use custom slot names, but included for consistency
            DeleteSlot(number);
        }

        public async Task RenameSlotAsync(int number, string newName)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSlotsRuntimeUI] WebGL: Starting async rename for slot {number} to '{newName}'");
#endif
                bool success = await SaveManager.Instance.RenameSaveSlotAsync(number, newName);
                
                if (success)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SaveSlotsRuntimeUI] WebGL: Rename slot {number} completed successfully");
#endif
                }
                else
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.LogError($"[SaveSlotsRuntimeUI] WebGL: Rename slot {number} failed");
#endif
                    Debug.LogError($"SaveSlotsRuntimeUI: Rename failed for slot {number}");
                }
            }
            catch (Exception ex)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogError($"[SaveSlotsRuntimeUI] WebGL: Exception during rename slot {number}: {ex.Message}");
#endif
                Debug.LogError($"SaveSlotsRuntimeUI: Rename failed for slot {number}: {ex.Message}");
            }
        }

        void ApplyTheme()
        {
            if (theme == null)
                return;

            if (theme.panel != null)
                panel = theme.panel;

            if (theme.titleBackgroundPanel != null)
                titleBackgroundPanel = theme.titleBackgroundPanel;

            if (theme.titleSaveGameText != null)
                titleSaveGameText = theme.titleSaveGameText;

            if (panel != null)
            {
                panel.color = theme.panelColor;
                panel.sprite = theme.panelSprite;
            }

            if (titleBackgroundPanel != null)
            {
                titleBackgroundPanel.color = theme.titleBackgroundColor;
                titleBackgroundPanel.sprite = theme.titleBackgroundSprite;
            }

            if (titleSaveGameText != null)
                titleSaveGameText.color = theme.titleTextColor;

            if (scrollRect != null)
            {
                var img = scrollRect.GetComponent<Image>();
                if (img != null)
                {
                    img.color = theme.backgroundColor;
                    img.sprite = theme.backgroundSprite;
                }
            }
        }
    }
}
#endif
