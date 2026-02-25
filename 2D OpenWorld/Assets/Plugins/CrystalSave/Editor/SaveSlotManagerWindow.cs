#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Logger = Arawn.CrystalSave.Runtime.Logger;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
#if REMEMBERME_CLOUDSAVE_PRESENT
using Unity.Services.CloudSave;
#endif
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Authentication;
#endif
using System;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	public class SaveSlotManagerWindow : EditorWindow
	{
		private SaveManager saveManager;
		private List<SaveSlot> saveSlots;
        private Dictionary<string, Texture2D> screenshotCache = new Dictionary<string, Texture2D>();
        private Dictionary<string, Task<Texture2D>> screenshotTasks = new Dictionary<string, Task<Texture2D>>();
        private TimeSpan screenshotTimeout = TimeSpan.FromSeconds(10);

                // Scroll position for the main window area. The scroll bar will
                // appear automatically when the content exceeds the view height.
                private Vector2 scrollPosition = Vector2.zero;
                private SupabaseSaveSystem supabase;
                private MySqlSaveSystem mysql;
		
		private bool isSubscribedToPlayMode = false;

		// Load Options
		private bool restoreLastActiveScene = true;
		private bool loadAsync = false;
		private bool allowSceneActivation = true;

		// Feedback Messages
		private string feedbackMessage = "";
		private MessageType feedbackMessageType = MessageType.Info;

		// Path to the save games folder
		private string saveGamesFolderPath;

		// Load Operation Tracking
		private bool isLoading = false;
                private int loadingSlotNumber = -1;

		/// <summary>
		/// Detects if the project is using cloud save backends that require async operations
		/// </summary>
                private bool IsUsingCloudSave()
                {
#if REMEMBERME_CLOUDSAVE_PRESENT
                        return true; // Unity Cloud Save is enabled
#else
                        if (saveManager?.SaveSettings != null)
                        {
                                if (saveManager.SaveSettings.enableCloudSave)
                                        return true;

                                switch (saveManager.SaveSettings.backend)
                                {
                                        case SaveBackend.Supabase:
                                        case SaveBackend.MySQL:
                                        case SaveBackend.Firebase:
                                                return true;
                                }
                        }

                        return false;
#endif
                }

		/// <summary>
		/// Checks if the project is targeting WebGL
		/// </summary>
		private bool IsWebGLTarget()
		{
			return EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
		}
                private CancellationTokenSource loadCancellationTokenSource;
                private TimeSpan loadTimeout = TimeSpan.FromSeconds(30); // Example timeout of 30 seconds

                private bool isConnectingToCloud = false;
                private bool isInitializing      = false;

                // Operation Tracking
                private bool isSaving = false;
                private bool isDeleting = false;
                private bool isRenaming = false;

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                private static bool AuthSignedIn => AuthenticationService.Instance.IsSignedIn;
#else
                private static bool AuthSignedIn => false;
#endif

		[MenuItem("Tools/Crystal Save/Runtime Debug/Manage Save Slots (Use In Runtime Only)")]
		public static void ShowWindow()
		{
			GetWindow<SaveSlotManagerWindow>("Save Slot Manager");
		}

		private void OnEnable()
		{
                        isLoading = false;
                        loadingSlotNumber = -1;
                        isSaving = false;
                        isDeleting = false;
                        isRenaming = false;

                       if (EditorApplication.isPlaying)
                       {
                               if (SaveManager.IsInitialized)
                                       OnSaveManagerInitialized(SaveManager.Instance);
                               else
                               {
                                       isInitializing = true;
                                       ShowNotification(new GUIContent("Initializing SaveManager..."));
                               }
                       }

                      SaveManager.Initialized += OnSaveManagerInitialized;
                      SaveManager.SaveSlotsInitialized += OnSlotsInitialized;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                       AuthEventsRelay.PlayerSignedIn += OnAuthSignedIn;
#endif

			if (!isSubscribedToPlayMode)
			{
				EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
				isSubscribedToPlayMode = true;
			}

                        // Subscribe to SaveManager events if SaveManager is available
                        if (saveManager != null)
                        {
                                saveManager.OnSaveSlotsUpdated += RefreshSaveSlots;
                                saveManager.OnLoadCompleted    += OnLoadCompleted;
                                saveManager.OnLoadFailed       += OnLoadFailed;
                                saveManager.OnSaveCompleted    += OnSaveCompleted;
                                saveManager.OnSaveFailed       += OnSaveFailed;
                                saveManager.OnDeleteCompleted  += OnDeleteCompleted;
                                saveManager.OnDeleteFailed     += OnDeleteFailed;
                                saveManager.OnRenameSlotCompleted += OnRenameSlotCompleted;
                                saveManager.OnRenameSlotFailed    += OnRenameSlotFailed;
                        }

			// Initialize the save games folder path
			InitializeSaveGamesFolderPath();
		}

		private void OnDisable()
		{
			if (isSubscribedToPlayMode)
			{
				EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
				isSubscribedToPlayMode = false;
			}

                       if (saveManager != null)
                       {
                               saveManager.OnSaveSlotsUpdated -= RefreshSaveSlots;
                               saveManager.OnLoadCompleted    -= OnLoadCompleted;
                               saveManager.OnLoadFailed       -= OnLoadFailed;
                               saveManager.OnSaveCompleted    -= OnSaveCompleted;
                               saveManager.OnSaveFailed       -= OnSaveFailed;
                               saveManager.OnDeleteCompleted  -= OnDeleteCompleted;
                               saveManager.OnDeleteFailed     -= OnDeleteFailed;
                               saveManager.OnRenameSlotCompleted -= OnRenameSlotCompleted;
                               saveManager.OnRenameSlotFailed    -= OnRenameSlotFailed;
                       }

                      SaveManager.Initialized -= OnSaveManagerInitialized;
                      SaveManager.SaveSlotsInitialized -= OnSlotsInitialized;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                       AuthEventsRelay.PlayerSignedIn -= OnAuthSignedIn;
#endif

                       ClearScreenshotCache();

                        isInitializing = false;

                       // Cancel any ongoing load operations when the window is disabled
                       CancelOngoingLoad();
		}

		private void InitializeSaveGamesFolderPath()
		{
			if (saveManager != null)
			{
				// Assuming SaveManager has a method or property to get the save path
				// Implement GetSaveGamesPath() in SaveSystem or SaveManager if not present
				saveGamesFolderPath = saveManager.SaveSystem.GetSaveGamesPath();
			}
			else
			{
				// Default path if SaveManager is not available
                            saveGamesFolderPath = SaveManager.Instance?.RootPath ?? Application.persistentDataPath;
			}
		}

		private void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			switch (state)
			{
                               case PlayModeStateChange.EnteredPlayMode:
                                       isLoading = false;
                                       loadingSlotNumber = -1;
                                       // SaveManager.ResetStatics clears static events like Initialized when
                                       // entering Play Mode. Re-subscribe here (first removing any lingering
                                       // subscriptions) to ensure we receive the callbacks even if the window
                                       // was already open before Play was pressed.
                                       SaveManager.Initialized -= OnSaveManagerInitialized;
                                       SaveManager.Initialized += OnSaveManagerInitialized;
                                       SaveManager.SaveSlotsInitialized -= OnSlotsInitialized;
                                       SaveManager.SaveSlotsInitialized += OnSlotsInitialized;
                                       if (SaveManager.IsInitialized)
                                               OnSaveManagerInitialized(SaveManager.Instance);
                                       else
                                       {
                                               isInitializing = true;
                                               ShowNotification(new GUIContent("Initializing SaveManager..."));
                                       }
                                       break;

                                case PlayModeStateChange.ExitingPlayMode:
                                        if (saveManager != null)
                                        {
                                                saveManager.OnSaveSlotsUpdated -= RefreshSaveSlots;
                                                saveManager.OnLoadCompleted    -= OnLoadCompleted;
                                                saveManager.OnLoadFailed       -= OnLoadFailed;
                                                saveManager.OnSaveCompleted    -= OnSaveCompleted;
                                                saveManager.OnSaveFailed       -= OnSaveFailed;
                                                saveManager.OnDeleteCompleted  -= OnDeleteCompleted;
                                                saveManager.OnDeleteFailed     -= OnDeleteFailed;
                                                saveManager.OnRenameSlotCompleted -= OnRenameSlotCompleted;
                                                saveManager.OnRenameSlotFailed    -= OnRenameSlotFailed;
                                                saveManager = null;
                                        }

                                        saveSlots = null;
                                        ClearScreenshotCache();
                                        isInitializing = false;
                                        Repaint();
                                        break;
			}
		}
		
		/// <summary>
		/// Keeps <see cref="saveSlots"/> in a sane state:
		/// <list type="bullet">
		/// <item>removes entries with SlotNumber&nbsp;&lt;&nbsp;1&nbsp;or&nbsp;&gt;&nbsp;max</item>
		/// <item>collapses duplicates – latest <c>LastSaved</c> wins</item>
		/// <item>inserts empty placeholders for missing numbers 1‥max</item>
		/// <item>sorts ascending by <c>SlotNumber</c></item>
		/// </list>
		/// Call this any time <c>saveSlots</c> might have changed.
		/// </summary>
		private void NormaliseSlotList()
		{
			if (saveManager == null || saveSlots == null) return;

			// Use runtime slot count instead of static settings
			int max = saveManager.CurrentSaveSlotCount > 0 
				? saveManager.CurrentSaveSlotCount 
				: saveManager.GetSaveSettings().numberOfSaveSlots;

			/* 1️⃣  throw away illegal indices */
			saveSlots = saveSlots
				.Where(s => s.SlotNumber >= 1 && s.SlotNumber <= max)
				.ToList();

			/* 2️⃣  collapse duplicates (newest wins) */
			saveSlots = saveSlots
				.GroupBy(s => s.SlotNumber)
				.Select(g => g.OrderByDescending(x => x.LastSaved).First())
				.ToList();

			/* 3️⃣  add placeholders for gaps */
			var present = new HashSet<int>(saveSlots.Select(s => s.SlotNumber));
			for (int n = 1; n <= max; n++)
			{
				if (present.Contains(n)) continue;

				saveSlots.Add(new SaveSlot(
					slotNumber:        n,
					slotName:          $"Slot {n}",
					lastSaved:         DateTime.MinValue,
					screenshotFileName: "",
					lastActiveScene:   ""
				));
			}

			/* 4️⃣  final nice ordering */
			saveSlots = saveSlots
				.OrderBy(s => s.SlotNumber)
				.ToList();
		}

                private async void InitializeSaveManager()
                {
                    try
                    {
                        isConnectingToCloud = true;
                        ShowNotification(new GUIContent("Establishing Connection to Cloud Backend..."));

                        // SaveManager is spawned during RuntimeInitializeOnLoad
                        // which can occur slightly later than this callback
                        const int maxWaitMs = 5000;
                        int waited = 0;
                        while ((saveManager = SaveManager.Instance) == null && waited < maxWaitMs)
                        {
                                await Task.Delay(100);
                                waited += 100;
                        }

                        if (saveManager == null)
                        {
                                feedbackMessage     = "SaveManager instance not found in the scene.";
                                feedbackMessageType = MessageType.Error;
                                Logger.Log(feedbackMessage, LogLevel.Error);
                                isConnectingToCloud = false;
                                RemoveNotification();
                                Repaint();
                                return;
                        }

			/* local first */
			saveSlots = saveManager.GetSaveSlots();
			NormaliseSlotList();

                        /* subscribe */
                        saveManager.OnSaveSlotsUpdated += RefreshSaveSlots;
                        saveManager.OnLoadCompleted    += OnLoadCompleted;
                        saveManager.OnLoadFailed       += OnLoadFailed;
                        saveManager.OnSaveCompleted    += OnSaveCompleted;
                        saveManager.OnSaveFailed       += OnSaveFailed;
                        saveManager.OnDeleteCompleted  += OnDeleteCompleted;
                        saveManager.OnDeleteFailed     += OnDeleteFailed;
                        saveManager.OnRenameSlotCompleted += OnRenameSlotCompleted;
                        saveManager.OnRenameSlotFailed    += OnRenameSlotFailed;

                        /* cloud merge (no local mirror) */
                        SaveSettings ss = saveManager.GetSaveSettings();
                        if (ss.backend == SaveBackend.Supabase && !ss.keepLocalMirror)
                        {
                                supabase = saveManager.SaveSystem as SupabaseSaveSystem;

                                try
                                {
                                        var remote = await LoadRemoteSlotsAsync();
                                        MergeRemoteIntoLocal(remote);        // normalises inside
                                        NormaliseSlotList();
                                }
                                catch (IOException io) when (io.Message.Contains("400"))
                                {
                                        Logger.Log("Supabase bucket empty – continuing.", LogLevel.Info);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"Remote-slot fetch failed – {ex.Message}", LogLevel.Warning);
                                }
                        }
                        else if (ss.backend == SaveBackend.MySQL && !ss.keepLocalMirror)
                        {
                                mysql = saveManager.SaveSystem as MySqlSaveSystem;
                                try
                                {
                                        var remote = await LoadRemoteSlotsAsync();
                                        MergeRemoteIntoLocal(remote);
                                        NormaliseSlotList();
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"Remote-slot fetch failed – {ex.Message}", LogLevel.Warning);
                                }
                        }
                        else if (ss.backend == SaveBackend.UnityCloudSave && !ss.keepLocalMirror)
                        {
#if REMEMBERME_CLOUDSAVE_PRESENT
                                try
                                {
                                        await saveManager.RefreshRemoteSlotsAsync();
                                        saveSlots = saveManager.GetSaveSlots();
                                        NormaliseSlotList();
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"Remote-slot fetch failed – {ex.Message}", LogLevel.Warning);
                                }
#endif
                        }

                        feedbackMessage     = "SaveManager initialised.";
                        feedbackMessageType = MessageType.Info;
                        isConnectingToCloud = false;
                        RemoveNotification();
                        Repaint();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"InitializeSaveManager failed: {ex.Message}", LogLevel.Error);
                        feedbackMessage     = $"Initialization error: {ex.Message}";
                        feedbackMessageType = MessageType.Error;
                        isConnectingToCloud = false;
                        if (this != null) { RemoveNotification(); Repaint(); }
                    }
                }

                private async void OnSaveManagerInitialized(SaveManager mgr)
                {
                    try
                    {
                        saveManager   = mgr;
                        isInitializing = false;
                        RemoveNotification();

                        InitializeSaveManager();
                        await ForceRefreshSlotsAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"OnSaveManagerInitialized failed: {ex.Message}", LogLevel.Error);
                        if (this != null) Repaint();
                    }
                }

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                private async void OnAuthSignedIn(AuthEventsRelay.SignedInArgs args)
                {
                    try
                    {
                        SaveSettings ss = SaveManager.Instance?.SaveSettings;
                        if (ss != null && ss.backend == SaveBackend.UnityCloudSave)
                        {
                                if (saveManager == null && SaveManager.IsInitialized)
                                        OnSaveManagerInitialized(SaveManager.Instance);

                                if (saveManager != null)
                                        await ForceRefreshSlotsAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"OnAuthSignedIn failed: {ex.Message}", LogLevel.Error);
                        if (this != null) Repaint();
                    }
                }
#endif

                private void OnSlotsInitialized()
                {
                        if (saveManager == null) return;

                        // Keep this lightweight to avoid recursive reinitialization loops.
                        ClearScreenshotCache();
                        RefreshSaveSlots();
                }



                private void ClearScreenshotCache()
                {
                        foreach (var texture in screenshotCache.Values)
                        {
                                DestroyImmediate(texture);
                        }
                        screenshotCache.Clear();
                        screenshotTasks.Clear();
                        Logger.Log("SaveSlotManagerWindow: Cleared all cached screenshots.", LogLevel.Off);
                }

                /// <summary>
                /// Removes any cached screenshot and pending download task for the given slot number
                /// based on the filename currently associated with that slot in the local list.
                /// Safe no-op if the slot or filename is not present.
                /// </summary>
                private void InvalidateScreenshotForSlot(int slotNumber)
                {
                        if (saveSlots == null) return;
                        var slot = saveSlots.FirstOrDefault(s => s.SlotNumber == slotNumber);
                        var key = slot?.ScreenshotFileName;
                        if (!string.IsNullOrEmpty(key))
                        {
                                if (screenshotCache.Remove(key, out var tex) && tex != null)
                                        DestroyImmediate(tex);
                                screenshotTasks.Remove(key);
                                Logger.Log($"SaveSlotManagerWindow: Invalidated screenshot cache for slot {slotNumber} (key: {key}).", LogCategory.Other, LogLevel.Info);
                        }
                }

		private Texture2D GetCachedScreenshot(SaveSlot slot)
		{
			if (slot == null ||
			    !saveManager.GetSaveSettings().enableScreenshots ||
			    string.IsNullOrEmpty(slot.ScreenshotFileName))
				return null;

			string key = slot.ScreenshotFileName;

			if (screenshotCache.TryGetValue(key, out var tex))
				return tex;

			/* ------------------------------------------------------------------ */
			/* 1) try local disk (works when Keep Local Mirror is on)             */
			/* ------------------------------------------------------------------ */
			tex = saveManager.GetScreenshot(slot);
			if (tex != null)
			{
				screenshotCache[key] = tex;
				return tex;
			}

                        /* --------------------------------------------------------------- */
                        /* 2) fallback: download from cloud backend when mirror is off        */
                        /* --------------------------------------------------------------- */
                        SaveSettings ss = saveManager.GetSaveSettings();
                        // If Cloud Save is disabled, do not attempt any remote download
                        if (ss == null || !ss.enableCloudSave)
                                return null;
                        if (ss.backend == SaveBackend.Supabase && !ss.keepLocalMirror && supabase != null)
                        {
                                if (!screenshotTasks.TryGetValue(key, out var task))
                                {
                                        screenshotTasks[key] = DownloadScreenshotAsync(key);
                                }
                                else if (task.IsCompleted)
                                {
                                        screenshotTasks.Remove(key);
                                        if (task.Result != null)
                                        {
                                                screenshotCache[key] = task.Result;
                                                EditorApplication.delayCall += Repaint;
                                                return task.Result;
                                        }
                                }
                        }
                        else if (ss.backend == SaveBackend.MySQL && !ss.keepLocalMirror && mysql != null)
                        {
                                if (!screenshotTasks.TryGetValue(key, out var task))
                                {
                                        screenshotTasks[key] = DownloadMySqlScreenshotAsync(key);
                                }
                                else if (task.IsCompleted)
                                {
                                        screenshotTasks.Remove(key);
                                        if (task.Result != null)
                                        {
                                                screenshotCache[key] = task.Result;
                                                EditorApplication.delayCall += Repaint;
                                                return task.Result;
                                        }
                                }
                        }
#if REMEMBERME_CLOUDSAVE_PRESENT
                        else if (ss.backend == SaveBackend.UnityCloudSave && !ss.keepLocalMirror && ss.cloudSaveScreenshots && AuthSignedIn)
                        {
                                if (!screenshotTasks.TryGetValue(key, out var task))
                                {
                                        screenshotTasks[key] = DownloadUcsScreenshotAsync(key);
                                }
                                else if (task.IsCompleted)
                                {
                                        screenshotTasks.Remove(key);
                                        if (task.Result != null)
                                        {
                                                screenshotCache[key] = task.Result;
                                                EditorApplication.delayCall += Repaint;
                                                return task.Result;
                                        }
                                }
                        }
#endif

                        return null; // give up
               }

                private async Task<Texture2D> DownloadScreenshotAsync(string key)
                {
                        try
                        {
                                var downloadTask = supabase.DownloadScreenshotAsync(key);
                                if (await Task.WhenAny(downloadTask, Task.Delay(screenshotTimeout)) == downloadTask)
                                {
                                        byte[] data = await downloadTask;
                                        if (data != null)
                                        {
                                                Texture2D remoteTex = new Texture2D(2, 2);
                                                if (remoteTex.LoadImage(data))
                                                        return remoteTex;
                                                DestroyImmediate(remoteTex);
                                        }
                                }
                                else
                                {
                                        Logger.Log($"Screenshot download timed out for '{key}'", LogLevel.Warning);
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SaveSlotManagerWindow: Failed to download screenshot '{key}' – {ex.Message}", LogLevel.Off);
                        }
                        return null;
                }
                private async Task<Texture2D> DownloadMySqlScreenshotAsync(string key)
                {
                        try
                        {
                                Logger.Log($"SaveSlotManagerWindow: Starting MySQL screenshot download for '{key}'.", LogLevel.Info);
                                var downloadTask = mysql.DownloadScreenshotAsync(key);
                                if (await Task.WhenAny(downloadTask, Task.Delay(screenshotTimeout)) == downloadTask)
                                {
                                        byte[] data = await downloadTask;
                                        if (data != null)
                                        {
                                                Logger.Log($"SaveSlotManagerWindow: Downloaded {data.Length} bytes for screenshot '{key}'.", LogLevel.Info);
                                                Texture2D remoteTex = new Texture2D(2, 2);
                                                if (remoteTex.LoadImage(data))
                                                        return remoteTex;
                                                DestroyImmediate(remoteTex);
                                        }
                                        else
                                        {
                                                Logger.Log($"SaveSlotManagerWindow: Screenshot '{key}' returned no data.", LogLevel.Warning);
                                        }
                                }
                                else
                                {
                                        Logger.Log($"Screenshot download timed out for '{key}'", LogLevel.Warning);
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SaveSlotManagerWindow: Failed to download screenshot '{key}' – {ex.Message}", LogLevel.Warning);
                        }
                        return null;
                }
#if REMEMBERME_CLOUDSAVE_PRESENT
                private async Task<Texture2D> DownloadUcsScreenshotAsync(string key)
                {
                        try
                        {
                                        // Double-check settings and sign-in state to avoid access token errors
                                        var ss = saveManager?.GetSaveSettings();
                                        if (ss == null || !ss.enableCloudSave || !ss.cloudSaveScreenshots || ss.backend != SaveBackend.UnityCloudSave || !AuthSignedIn)
                                        {
                                                return null;
                                        }

                                // Try Data API first (new structured format used by WebGL)
                                string dataApiKey = key.Contains(".") ? 
                                    key.Substring(0, key.LastIndexOf('.')) : key;
                                dataApiKey = $"screenshot_{dataApiKey}";
                                
                                try
                                {
                                    var dataDownloadTask = Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(
                                        new HashSet<string> { dataApiKey });
                                    UnityEngine.Debug.Log($"Editor: Attempting Data API download for screenshot: {dataApiKey}");
                                    
                                    if (await Task.WhenAny(dataDownloadTask, Task.Delay(screenshotTimeout)) == dataDownloadTask)
                                    {
                                        var result = await dataDownloadTask;
                                        if (result.TryGetValue(dataApiKey, out var item))
                                        {
                                            try
                                            {
                                                // Try to parse as structured data first
                                                var screenshotData = item.Value.GetAs<Dictionary<string, object>>();
                                                if (screenshotData != null && screenshotData.TryGetValue("data", out var base64Obj))
                                                {
                                                    string base64Data = base64Obj.ToString();
                                                    if (!string.IsNullOrEmpty(base64Data))
                                                    {
                                                        byte[] data = Convert.FromBase64String(base64Data);
                                                        UnityEngine.Debug.Log($"Editor: Screenshot downloaded via Data API with structured format: {data.Length} bytes");
                                                        
                                                        Texture2D remoteTex = new Texture2D(2, 2);
                                                        if (remoteTex.LoadImage(data))
                                                            return remoteTex;
                                                        DestroyImmediate(remoteTex);
                                                    }
                                                }
                                                else
                                                {
                                                    // Fallback: try as direct base64 string
                                                    string base64Data = item.Value.GetAs<string>();
                                                    if (!string.IsNullOrEmpty(base64Data))
                                                    {
                                                        byte[] data = Convert.FromBase64String(base64Data);
                                                        UnityEngine.Debug.Log($"Editor: Screenshot downloaded via Data API with legacy format: {data.Length} bytes");
                                                        
                                                        Texture2D remoteTex = new Texture2D(2, 2);
                                                        if (remoteTex.LoadImage(data))
                                                            return remoteTex;
                                                        DestroyImmediate(remoteTex);
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                UnityEngine.Debug.LogWarning($"Editor: Error parsing Data API screenshot data for {dataApiKey}: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    UnityEngine.Debug.Log($"Editor: Data API download failed for {dataApiKey}, trying Files API fallback: {ex.Message}");
                                }

                                // Fallback to Files API for screenshots uploaded via Files API
                                        var downloadTask = Unity.Services.CloudSave.CloudSaveService.Instance.Files.Player.LoadBytesAsync(key);
                                if (await Task.WhenAny(downloadTask, Task.Delay(screenshotTimeout)) == downloadTask)
                                {
                                        byte[] data = await downloadTask;
                                        if (data != null)
                                        {
                                                UnityEngine.Debug.Log($"Editor: Screenshot downloaded via Files API: {data.Length} bytes");
                                                Texture2D remoteTex = new Texture2D(2, 2);
                                                if (remoteTex.LoadImage(data))
                                                        return remoteTex;
                                                DestroyImmediate(remoteTex);
                                        }
                                }
                                else
                                {
                                        Logger.Log($"Screenshot download timed out for '{key}'", LogLevel.Warning);
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SaveSlotManagerWindow: Failed to download screenshot '{key}' – {ex.Message}", LogLevel.Warning);
                        }
                        return null;
                }
#endif
		
                private async Task<List<SaveSlot>> LoadRemoteSlotsAsync()
                {
                        SaveSettings ss = saveManager.GetSaveSettings();
                        if (ss.backend == SaveBackend.Supabase && supabase != null)
                                return await supabase.ListRemoteSlotsAsync();
                        if (ss.backend == SaveBackend.MySQL && mysql != null)
                                return await mysql.ListRemoteSlotsAsync();
                        return null;
                }

		private void MergeRemoteIntoLocal(List<SaveSlot> remote)
		{
			if (remote == null || remote.Count == 0) return;

			if (saveSlots == null)
				saveSlots = remote;
			else
			{
				var dict = new Dictionary<int, SaveSlot>();
				foreach (var s in saveSlots) dict[s.SlotNumber] = s; // local first
				foreach (var r in remote)    dict[r.SlotNumber] = r; // remote wins
				saveSlots = dict.Values.ToList();
			}

			NormaliseSlotList();
			Repaint();
		}

                private void OnGUI()
                {
                        if (isInitializing)
                        {
                                EditorGUILayout.HelpBox("Initializing SaveManager...", MessageType.Info);
                                return;
                        }

                        if (isConnectingToCloud)
                        {
                                EditorGUILayout.HelpBox("Establishing Connection to Cloud Backend...", MessageType.Info);
                                return;
                        }

                        if (!string.IsNullOrEmpty(feedbackMessage))
                        {
                                EditorGUILayout.HelpBox(feedbackMessage, feedbackMessageType);
                                EditorGUILayout.Space();
                        }

                        bool scrollViewStarted = false;
                        try
                        {
                                // Use a scroll view so that overflowing content becomes scrollable.
                                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                                scrollViewStarted = true;

                                if (EditorApplication.isPlaying)
                                {
                                        if (saveManager == null)
                                        {
                                                EditorGUILayout.HelpBox("SaveManager instance not found in the scene.", MessageType.Error);
                                                return;
                                        }

                                        // Load Options Section
                                        EditorGUILayout.LabelField("Load Options", EditorStyles.boldLabel);
                                        EditorGUI.indentLevel++;
                                        GUIContent restoreSceneContent = new GUIContent(
                                                "Restore Last Active Scene",
                                                "Loads the scene that was active when the save was created. Disable to load in the current scene.");
                                        restoreLastActiveScene = EditorGUILayout.Toggle(restoreSceneContent, restoreLastActiveScene);
                                        loadAsync = EditorGUILayout.Toggle("Load Asynchronously", loadAsync);

                                        // If LoadAsync is enabled, allow scene activation toggle
                                        if (loadAsync)
                                        {
                                                allowSceneActivation = EditorGUILayout.Toggle("Allow Scene Activation", allowSceneActivation);
                                        }
                                        else
                                        {
                                                // If not async, scene activation is not applicable
                                                allowSceneActivation = true;
                                        }
                                        EditorGUI.indentLevel--;

                                        EditorGUILayout.Space();

                                        EditorGUILayout.LabelField("Save Slots", EditorStyles.boldLabel);

                                        if (saveSlots == null || saveSlots.Count == 0)
                                        {
                                                EditorGUILayout.HelpBox("No save slots available.", MessageType.Info);
                                        }
                                        else
                                        {
                                                foreach (var slot in saveSlots)
                                                {
                                                        if (slot == null)
                                                                continue;

                                                        EditorGUILayout.BeginVertical("box");
                                                        try
                                                        {
                                                                EditorGUILayout.LabelField($"Slot {slot.SlotNumber}: {slot.SlotName}", EditorStyles.boldLabel);
                                                                EditorGUILayout.LabelField($"Last Saved: {slot.LastSaved}", EditorStyles.label);
                                                                EditorGUILayout.LabelField($"Last Active Scene: {slot.LastActiveScene}", EditorStyles.label);

                                                                // Custom Metadata Display
                                                                var settings = saveManager.GetSaveSettings();
                                                                if (slot.CustomMetadata != null && slot.CustomMetadata.Count > 0)
                                                                {
                                                                        EditorGUILayout.LabelField("Slot Metadata", EditorStyles.boldLabel);
                                                                        EditorGUI.indentLevel++;
                                                                        foreach (var kvp in slot.CustomMetadata)
                                                                                EditorGUILayout.LabelField(kvp.Key, kvp.Value);
                                                                        EditorGUI.indentLevel--;
                                                                }
                                                                else if (settings != null && settings.defaultSlotMetadata != null &&
                                                                         settings.defaultSlotMetadata.entries != null &&
                                                                         settings.defaultSlotMetadata.entries.Count > 0)
                                                                {
                                                                        EditorGUILayout.LabelField("Slot Metadata", EditorStyles.boldLabel);
                                                                        EditorGUI.indentLevel++;
                                                                        foreach (var entry in settings.defaultSlotMetadata.entries)
                                                                        {
                                                                                if (!string.IsNullOrEmpty(entry.key))
                                                                                        EditorGUILayout.LabelField(entry.key, entry.GetValue());
                                                                        }
                                                                        EditorGUI.indentLevel--;
                                                                }

                                                                // Updated Screenshot Handling
                                                                if (saveManager.GetSaveSettings().enableScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
                                                                {
                                                                        Texture2D texture = GetCachedScreenshot(slot);
                                                                        if (texture != null)
                                                                        {
                                                                                GUILayout.Label(texture, GUILayout.Width(200), GUILayout.Height(150));
                                                                        }
                                                                        else
                                                                        {
                                                                                GUILayout.Label("Screenshot not available.", EditorStyles.label);
                                                                        }
                                                                }
                                                                else
                                                                {
                                                                        GUILayout.Label("Screenshot not found.", EditorStyles.label);
                                                                }

                                                                EditorGUILayout.BeginHorizontal();
                                                                try
                                                                {
                                                                        // Disable action buttons if any operation is running locally
                                                                        // or if SaveManager itself is currently loading
                                                                        GUI.enabled = EditorApplication.isPlaying &&
                                                                                      !isLoading &&
                                                                                      !isSaving &&
                                                                                      !isDeleting &&
                                                                                      !isRenaming &&
                                                                                      (saveManager == null || !saveManager.IsLoading);

                                                                        if (GUILayout.Button("Save"))
                                                                        {
                                                                                if (isSaving)
                                                                                {
                                                                                        Logger.Log("SaveSlotManagerWindow: Another save operation is already in progress. Aborting new save request.", LogLevel.Warning);
                                                                                        feedbackMessage = "Another save operation is already in progress.";
                                                                                        feedbackMessageType = MessageType.Warning;
                                                                                }
                                                                                else
                                                                                {
                                                                                        if (slot.SlotNumber > 0)
                                                                                        {
                                                                                                isSaving = true;

                                                                                                // Use async methods for cloud save backends to prevent freezing
                                                                                                if (IsUsingCloudSave() || IsWebGLTarget())
                                                                                                {
                                                                                                        Logger.Log($"SaveSlotManagerWindow: Using async save for slot {slot.SlotNumber} (Cloud Save or WebGL detected).", LogLevel.Info);
                                                                                                        _ = saveManager.SaveAsync(slot.SlotNumber);
                                                                                                        feedbackMessage = $"Initiated async save for slot {slot.SlotNumber} (Cloud Save compatible).";
                                                                                                }
                                                                                                else
                                                                                                {
                                                                                                        Logger.Log($"SaveSlotManagerWindow: Using synchronous save for slot {slot.SlotNumber}.", LogCategory.Other, LogLevel.Info);
                                                                                                        saveManager.SaveAsync(slot.SlotNumber);
                                                                                                        feedbackMessage = $"Initiated save for slot {slot.SlotNumber}.";
                                                                                                }
                                                                                                feedbackMessageType = MessageType.Info;
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                                Logger.Log("Ignoring Save-click for slot 0.", LogLevel.Warning);
                                                                                        }
                                                                                }
                                                                        }

                                                                        if (GUILayout.Button("Load"))
                                                                        {
                                                                                // Initiate Load Operation
                                                                                if (isLoading)
                                                                                {
                                                                                        Logger.Log("SaveSlotManagerWindow: Another load operation is already in progress. Aborting new load request.", LogLevel.Warning);
                                                                                        feedbackMessage = "Another load operation is already in progress.";
                                                                                        feedbackMessageType = MessageType.Warning;
                                                                                }
                                                                                else if (slot.SlotNumber > 0)
                                                                                {
                                                                                        // Use async methods for cloud save backends to prevent freezing
                                                                                        if (IsUsingCloudSave() || IsWebGLTarget())
                                                                                        {
                                                                                                Logger.Log($"SaveSlotManagerWindow: Using async load for slot {slot.SlotNumber} (Cloud Save or WebGL detected).", LogLevel.Info);
                                                                                                StartLoadOperationAsync(slot.SlotNumber);
                                                                                                feedbackMessage = $"Initiated async load for slot {slot.SlotNumber} (Cloud Save compatible).";
                                                                                                feedbackMessageType = MessageType.Info;
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                                Logger.Log($"SaveSlotManagerWindow: Using synchronous load for slot {slot.SlotNumber}.", LogCategory.Other, LogLevel.Info);
                                                                                                StartLoadOperation(slot.SlotNumber);
                                                                                                feedbackMessage = $"Initiated load for slot {slot.SlotNumber}.";
                                                                                                feedbackMessageType = MessageType.Info;
                                                                                        }
                                                                                }
                                                                        }

                                                                        if (GUILayout.Button("Delete"))
                                                                        {
                                                                                if (isDeleting)
                                                                                {
                                                                                        Logger.Log("SaveSlotManagerWindow: Another delete operation is already in progress. Aborting new delete request.", LogLevel.Warning);
                                                                                        feedbackMessage = "Another delete operation is already in progress.";
                                                                                        feedbackMessageType = MessageType.Warning;
                                                                                }
                                                                                else if (slot.SlotNumber > 0)
                                                                                {
                                                                                        if (EditorUtility.DisplayDialog("Confirm Delete", $"Are you sure you want to delete save slot {slot.SlotNumber}?", "Yes", "No"))
                                                                                        {
                                                                                                isDeleting = true;

                                                                                                // Use async methods for cloud save backends to prevent freezing
                                                                                                if (IsUsingCloudSave() || IsWebGLTarget())
                                                                                                {
                                                                                                        Logger.Log($"SaveSlotManagerWindow: Using async delete for slot {slot.SlotNumber} (Cloud Save or WebGL detected).", LogLevel.Info);
                                                                                                        saveManager.Delete(slot.SlotNumber); // Delete method itself handles async via SlotManager.DeleteAsync
                                                                                                        feedbackMessage = $"Initiated async delete for slot {slot.SlotNumber} (Cloud Save compatible).";
                                                                                                        feedbackMessageType = MessageType.Info;
                                                                                                }
                                                                                                else
                                                                                                {
                                                                                                        Logger.Log($"SaveSlotManagerWindow: Using synchronous delete for slot {slot.SlotNumber}.", LogLevel.Info);
                                                                                                        saveManager.Delete(slot.SlotNumber);
                                                                                                        feedbackMessage = $"Initiated delete for slot {slot.SlotNumber}.";
                                                                                                        feedbackMessageType = MessageType.Info;
                                                                                                }

                                                                                                // Remove the screenshot from the cache if it exists
                                                                                                if (!string.IsNullOrEmpty(slot.ScreenshotFileName))
                                                                                                {
                                                                                                        screenshotCache.Remove(slot.ScreenshotFileName);
                                                                                                }
                                                                                        }
                                                                                }
                                                                        }
                                                                }
                                                                finally
                                                                {
                                                                        GUI.enabled = true;
                                                                        EditorGUILayout.EndHorizontal();
                                                                }

                                                                EditorGUILayout.BeginHorizontal();
                                                                try
                                                                {
                                                                        GUILayout.Label($"Name: {slot.SlotName}");

                                                                        if (GUILayout.Button("Rename"))
                                                                        {
                                                                                RenameSaveSlot(slot);
                                                                        }
                                                                }
                                                                finally
                                                                {
                                                                        EditorGUILayout.EndHorizontal();
                                                                }
                                                        }
                                                        finally
                                                        {
                                                                EditorGUILayout.EndVertical();
                                                                EditorGUILayout.Space();
                                                        }
                                                }
                                        }

                                        // Display Cancel Button if a load operation is in progress
                                        if (isLoading)
                                        {
                                                EditorGUILayout.BeginHorizontal();
                                                try
                                                {
                                                        GUILayout.FlexibleSpace();
                                                        if (GUILayout.Button("Cancel Load"))
                                                        {
                                                                CancelOngoingLoad();
                                                                feedbackMessage = $"Load operation for slot {loadingSlotNumber} has been cancelled.";
                                                                feedbackMessageType = MessageType.Info;
                                                        }
                                                }
                                                finally
                                                {
                                                        EditorGUILayout.EndHorizontal();
                                                }
                                        }

                                        EditorGUILayout.BeginHorizontal();
                                        try
                                        {
                                                GUILayout.FlexibleSpace();
                                                if (GUILayout.Button("Refresh"))
                                                {
                                                        _ = ForceRefreshSlotsAsync();
                                                        feedbackMessage = "Refreshing save slots...";
                                                        feedbackMessageType = MessageType.Info;
                                                }
                                        }
                                        finally
                                        {
                                                EditorGUILayout.EndHorizontal();
                                        }

                                        // Helper Box to Display Paths
                                        EditorGUILayout.HelpBox(
                                                $"**Persistent Path:**\n{SaveManager.Instance?.RootPath ?? Application.persistentDataPath}\n\n" +
                                                $"**Save Games Folder Path:**\n{saveGamesFolderPath}",
                                                MessageType.Info
                                        );

                                        EditorGUILayout.Space();

                                        // Add a separator before the new button
                                        EditorGUILayout.Space();
                                        EditorGUILayout.LabelField("Additional Options", EditorStyles.boldLabel);

                                        EditorGUILayout.BeginHorizontal();
                                        try
                                        {
                                                GUILayout.FlexibleSpace();
                                                if (GUILayout.Button("Open Save Folder", GUILayout.Width(150)))
                                                {
                                                        OpenSaveFolder();
                                                }
                                                GUILayout.FlexibleSpace();
                                        }
                                        finally
                                        {
                                                EditorGUILayout.EndHorizontal();
                                        }
                                }
                                else
                                {
                                        EditorGUILayout.BeginVertical();
                                        try
                                        {
                                                EditorGUILayout.HelpBox(
                                                        "⚠️ **Runtime-Only Window**\n\n" +
                                                        "This window is intended for use **only during Play Mode** (runtime).\n" +
                                                        "- **Screenshots and save slot information will be lost upon exiting Play Mode.**\n" +
                                                        "- **Use this window solely for debugging purposes.**\n\n" +
                                                        "When you re-enter Play Mode, you can use the **Load** button to load a saved game if a save exists in that slot, even if no save slots are currently displayed.",
                                                        MessageType.Info);
                                        }
                                        finally
                                        {
                                                EditorGUILayout.EndVertical();
                                        }

                                        GUI.enabled = false;
                                }
                        }
                        finally
                        {
                                GUI.enabled = true;
                                if (scrollViewStarted)
                                        EditorGUILayout.EndScrollView();
                        }
                }

		/// <summary>
		/// Opens the folder where save games are stored.
		/// </summary>
		private void OpenSaveFolder()
		{
			if (string.IsNullOrEmpty(saveGamesFolderPath))
			{
				feedbackMessage = "Save games folder path is not set.";
				feedbackMessageType = MessageType.Error;
				Repaint();
				return;
			}

			try
			{
				if (Directory.Exists(saveGamesFolderPath))
				{
					// Determine the platform and open the folder accordingly
#if UNITY_EDITOR_WIN
					Process.Start("explorer.exe", $"\"{saveGamesFolderPath}\"");
#elif UNITY_EDITOR_OSX
                    Process.Start("open", $"\"{saveGamesFolderPath}\"");
#elif UNITY_EDITOR_LINUX
                    Process.Start("xdg-open", $"\"{saveGamesFolderPath}\"");
#else
                    EditorUtility.RevealInFinder(saveGamesFolderPath); // Fallback
#endif
					feedbackMessage = $"Opened save folder: {saveGamesFolderPath}";
					feedbackMessageType = MessageType.Info;
				}
				else
				{
					EditorUtility.DisplayDialog("Folder Not Found", $"The save folder does not exist:\n{saveGamesFolderPath}", "OK");
				}
			}
			catch (Exception ex)
			{
				feedbackMessage = $"Failed to open save folder: {ex.Message}";
				feedbackMessageType = MessageType.Error;
				Logger.Log($"SaveSlotManagerWindow: Exception in OpenSaveFolder: {ex.Message}", LogLevel.Error);
			}
		}

		/// <summary>
		/// Initiates a load operation using SaveManager.Load or async equivalent for cloud save.
		/// </summary>
		/// <param name="slotNumber">The save slot number to load.</param>
		private void StartLoadOperation(int slotNumber)
		{
			if (isLoading || (saveManager != null && saveManager.IsLoading))
			{
				Logger.Log("SaveSlotManagerWindow: Another load operation is already in progress. Aborting new load request.", LogLevel.Warning);
				feedbackMessage = "Another load operation is already in progress.";
				feedbackMessageType = MessageType.Warning;
				Repaint();
				return;
			}

			Logger.Log($"SaveSlotManagerWindow: Initiating load operation for slot {slotNumber}.", LogCategory.Other, LogLevel.Info);

			isLoading = true;
			loadingSlotNumber = slotNumber;
			loadCancellationTokenSource = new CancellationTokenSource();
			loadCancellationTokenSource.CancelAfter(loadTimeout); // Set timeout

			try
			{
				// Use async methods for cloud save backends to prevent freezing
				if (IsUsingCloudSave() || IsWebGLTarget())
				{
					Logger.Log($"SaveSlotManagerWindow: Using async load for slot {slotNumber} (Cloud Save or WebGL detected).", LogLevel.Info);
					_ = saveManager.LoadSaveSlotAsync(slotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation, loadTimeout, loadCancellationTokenSource.Token);
					feedbackMessage = $"Loading slot {slotNumber} (async, Cloud Save compatible)...";
				}
				else
				{
					Logger.Log($"SaveSlotManagerWindow: Using synchronous load for slot {slotNumber}.", LogCategory.Other, LogLevel.Info);
					saveManager.Load(
						slotNumber: slotNumber,
						restoreLastActiveScene: restoreLastActiveScene,
						loadAsync: loadAsync,
						allowSceneActivation: allowSceneActivation,
						cancellationToken: loadCancellationTokenSource.Token
					);
					feedbackMessage = $"Loading slot {slotNumber}...";
				}
				feedbackMessageType = MessageType.Info;
			}
			catch (Exception ex)
			{
				feedbackMessage = $"Failed to initiate load: {ex.Message}";
				feedbackMessageType = MessageType.Error;
				Logger.Log($"SaveSlotManagerWindow: Exception in StartLoadOperation: {ex.Message}", LogLevel.Error);
				isLoading = false;
				loadingSlotNumber = -1;
				loadCancellationTokenSource = null;
			}

			Repaint();
		}

		/// <summary>
		/// Initiates an async load operation for the specified save slot.
		/// This method uses LoadSaveSlotAsync to prevent freezing with cloud save backends.
		/// </summary>
		/// <param name="slotNumber">The save slot number to load.</param>
		private async void StartLoadOperationAsync(int slotNumber)
		{
			if (saveManager == null)
			{
				feedbackMessage = "SaveManager instance is not available.";
				feedbackMessageType = MessageType.Error;
				Repaint();
				return;
			}

			Logger.Log($"SaveSlotManagerWindow: Initiating async load operation for slot {slotNumber}.", LogLevel.Info);

			isLoading = true;
			loadingSlotNumber = slotNumber;
			loadCancellationTokenSource = new CancellationTokenSource();
			loadCancellationTokenSource.CancelAfter(loadTimeout); // Set timeout

			try
			{
				feedbackMessage = $"Loading slot {slotNumber} (async, Cloud Save compatible)...";
				feedbackMessageType = MessageType.Info;
				Repaint();

				var result = await saveManager.LoadSaveSlotAsync(
					slotNumber: slotNumber,
					restoreLastActiveScene: restoreLastActiveScene,
					loadAsync: loadAsync,
					allowSceneActivation: allowSceneActivation,
					timeout: loadTimeout,
					cancellationToken: loadCancellationTokenSource.Token
				);

				// Handle the result
				if (result.Success)
				{
					Logger.Log($"SaveSlotManagerWindow: Async load completed successfully for slot {slotNumber}.", LogLevel.Info);
					feedbackMessage = $"Successfully loaded slot {slotNumber}.";
					feedbackMessageType = MessageType.Info;
				}
				else
				{
					Logger.Log($"SaveSlotManagerWindow: Async load failed for slot {slotNumber}: {result.ErrorMessage}", LogLevel.Error);
					feedbackMessage = $"Failed to load slot {slotNumber}: {result.ErrorMessage}";
					feedbackMessageType = MessageType.Error;
				}
			}
			catch (OperationCanceledException)
			{
				Logger.Log($"SaveSlotManagerWindow: Async load operation for slot {slotNumber} was cancelled.", LogLevel.Warning);
				feedbackMessage = $"Load operation for slot {slotNumber} was cancelled.";
				feedbackMessageType = MessageType.Warning;
			}
			catch (Exception ex)
			{
				Logger.Log($"SaveSlotManagerWindow: Exception in async load operation for slot {slotNumber}: {ex.Message}", LogLevel.Error);
				feedbackMessage = $"Failed to load slot {slotNumber}: {ex.Message}";
				feedbackMessageType = MessageType.Error;
			}
			finally
			{
				isLoading = false;
				loadingSlotNumber = -1;
				loadCancellationTokenSource = null;
				Repaint();
			}
		}

		/// <summary>
		/// Callback invoked when a load operation completes successfully.
		/// </summary>
		/// <param name="sender">The SaveManager instance.</param>
		/// <param name="e">Event arguments containing load details.</param>
                        private void OnLoadCompleted(object sender, SaveLoadEventArgs e)
		{
                                // Process event regardless of local isLoading flag to avoid missing UI refreshes
                                if (e.Slot.SlotNumber != loadingSlotNumber)
                                {
                                        // Only warn if we actually had an in-flight load tracked.
                                        // If loadingSlotNumber == -1, the load likely originated elsewhere
                                        // (e.g., via API or another UI) and this window is just observing.
                                        if (loadingSlotNumber != -1)
                                                Logger.Log($"SaveSlotManagerWindow: Load completed for slot {e.Slot.SlotNumber}, but tracked loading slot was {loadingSlotNumber}.", LogCategory.Other, LogLevel.Warning);
                                        else
                                                Logger.Log($"SaveSlotManagerWindow: Load completed for slot {e.Slot.SlotNumber} (no local load was tracked; external trigger).", LogCategory.Other, LogLevel.Info);
                                }

                                if (e.Success)
                                {
                                        feedbackMessage = $"Successfully loaded slot {e.Slot.SlotNumber}.";
                                        feedbackMessageType = MessageType.Info;
                                        Logger.Log($"SaveSlotManagerWindow: Successfully loaded slot {e.Slot.SlotNumber}.", LogCategory.Other, LogLevel.Info);
                                }
                                else
                                {
                                        feedbackMessage = $"Failed to load slot {e.Slot.SlotNumber}: {e.Message}";
                                        feedbackMessageType = MessageType.Error;
                                        Logger.Log($"SaveSlotManagerWindow: Failed to load slot {e.Slot.SlotNumber}: {e.Message}", LogLevel.Error);
                                }

                                // Clear local tracking flags even if a mismatched completion arrived (prevents being stuck in loading state)
                                isLoading = false;
                                loadingSlotNumber = -1;
                                loadCancellationTokenSource = null;

                                    // Invalidate screenshot cache for this slot to ensure fresh image after load
                                    InvalidateScreenshotForSlot(e.Slot.SlotNumber);

                                    // Immediate local refresh so the list doesn't appear empty, followed by a full
                                    // refresh on the next editor tick to sync remote/backends (same as pressing Refresh)
                                    ClearScreenshotCache();
                                    RefreshSaveSlots();
                                    EditorApplication.delayCall += async () => { await ForceRefreshSlotsAsync(); };

                                    Repaint();
		}

		/// <summary>
		/// Callback invoked when a load operation fails.
		/// </summary>
		/// <param name="sender">The SaveManager instance.</param>
		/// <param name="e">Event arguments containing failure details.</param>
                private void OnLoadFailed(object sender, OperationFailedEventArgs e)
                {
                        // Process event regardless of local isLoading flag; always clear state
                        if (e.Slot.SlotNumber != loadingSlotNumber)
                        {
                                if (loadingSlotNumber != -1)
                                        Logger.Log($"SaveSlotManagerWindow: Load failed for slot {e.Slot.SlotNumber}, but tracked loading slot was {loadingSlotNumber}.", LogLevel.Warning);
                                else
                                        Logger.Log($"SaveSlotManagerWindow: Load failed for slot {e.Slot.SlotNumber} (no local load was tracked; external trigger).", LogLevel.Info);
                        }

                        feedbackMessage = $"Failed to load slot {e.Slot.SlotNumber}: {e.OperationName} failed with error: {e.ErrorMessage}";
                        feedbackMessageType = MessageType.Error;
                        Logger.Log($"SaveSlotManagerWindow: Load failed for slot {e.Slot.SlotNumber}: {e.OperationName} failed with error: {e.ErrorMessage}", LogLevel.Error);

                        isLoading = false;
                        loadingSlotNumber = -1;
                        loadCancellationTokenSource = null;

                        // Keep the overall list as-is; just repaint the status (don't invalidate screenshots on failure)
                        Repaint();
                }

                /// <summary>
                /// Fetch fresh metadata for a specific slot after a successful load and update the UI.
                /// Always queries the authoritative source (cloud/local) instead of relying on event payload.
                /// </summary>
                private async void RefreshSlotAfterLoadAsync(int slotNumber)
                {
                        try
                        {
                                if (saveManager == null) return;

                                SaveSlot freshSlot = null;
                                if (IsUsingCloudSave() && !IsWebGLTarget())
                                {
                                        Logger.Log($"SaveSlotManagerWindow: Fetching fresh cloud metadata for slot {slotNumber} after load.", LogLevel.Info);
                                        freshSlot = await saveManager.GetSlotMetadataAsync(slotNumber);
                                }
                                else
                                {
                                        Logger.Log($"SaveSlotManagerWindow: Fetching fresh local metadata for slot {slotNumber} after load.", LogLevel.Info);
                                        freshSlot = saveManager.GetSaveSlotByNumber(slotNumber);
                                }

                                if (freshSlot == null)
                                {
                                        Logger.Log($"SaveSlotManagerWindow: Fresh metadata not available for slot {slotNumber}; falling back to full refresh.", LogLevel.Warning);
                                        RefreshSaveSlots();
                                        return;
                                }

                                if (saveSlots == null)
                                        saveSlots = new List<SaveSlot>();

                                bool replaced = false;
                                for (int i = 0; i < saveSlots.Count; i++)
                                {
                                        if (saveSlots[i].SlotNumber == slotNumber)
                                        {
                                                saveSlots[i] = freshSlot;
                                                replaced = true;
                                                break;
                                        }
                                }
                                if (!replaced)
                                        saveSlots.Add(freshSlot);

                                NormaliseSlotList();

                                // Prime screenshot load (local read or kick off cloud download) on next repaint
                                EditorApplication.delayCall += () =>
                                {
                                        try { _ = GetCachedScreenshot(freshSlot); }
                                        catch { /* ignore */ }
                                        Repaint();
                                };
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SaveSlotManagerWindow: Exception while refreshing slot {slotNumber} after load – {ex.Message}", LogLevel.Warning);
                                RefreshSaveSlots();
                        }
                }

                private void OnSaveCompleted(object sender, SaveLoadEventArgs e)
                {
                        isSaving = false;

                        // Update the specific slot with fresh metadata
                        UpdateSlotWithFreshMetadata(e.Slot);

                        // Invalidate cached screenshot so the next draw picks up
                        // the new image written on disk or fetched from the backend
                        if (!string.IsNullOrEmpty(e.Slot.ScreenshotFileName))
                                screenshotCache.Remove(e.Slot.ScreenshotFileName);

                        Repaint();
                }

                private void OnSaveFailed(object sender, OperationFailedEventArgs e)
                {
                        isSaving = false;
                        feedbackMessage = $"Failed to save slot {e.Slot?.SlotNumber}: {e.ErrorMessage}";
                        feedbackMessageType = MessageType.Error;
                        RefreshSaveSlots();
                        Repaint();
                }

                private void OnDeleteCompleted(object sender, SaveManagerEventArgs e)
                {
                        isDeleting = false;
                        feedbackMessage = $"Deleted save slot {e.Slot.SlotNumber}.";
                        feedbackMessageType = MessageType.Info;
                        RefreshSaveSlots();
                        Repaint();
                }

                private void OnDeleteFailed(object sender, OperationFailedEventArgs e)
                {
                        isDeleting = false;
                        feedbackMessage = $"Failed to delete slot {e.Slot?.SlotNumber}: {e.ErrorMessage}";
                        feedbackMessageType = MessageType.Error;
                        RefreshSaveSlots();
                        Repaint();
                }

                private void OnRenameSlotCompleted(object sender, RenameSlotEventArgs e)
                {
                        isRenaming = false;
                        feedbackMessage = $"Renamed slot {e.Slot.SlotNumber} to '{e.NewName}'.";
                        feedbackMessageType = MessageType.Info;
                        
                        // Update the specific slot with fresh metadata after rename
                        UpdateSlotWithFreshMetadata(e.Slot);
                        
                        Repaint();
                }

                private void OnRenameSlotFailed(object sender, OperationFailedEventArgs e)
                {
                        isRenaming = false;
                        feedbackMessage = $"Failed to rename slot {e.Slot?.SlotNumber}: {e.ErrorMessage}";
                        feedbackMessageType = MessageType.Error;
                        RefreshSaveSlots();
                        Repaint();
                }

		/// <summary>
		/// Cancels any ongoing load operation.
		/// </summary>
		private void CancelOngoingLoad()
		{
			if (isLoading && loadCancellationTokenSource != null && !loadCancellationTokenSource.IsCancellationRequested)
			{
				loadCancellationTokenSource.Cancel();
				Logger.Log($"SaveSlotManagerWindow: Cancelled load operation for slot {loadingSlotNumber}.", LogLevel.Info);
			}
		}

		/// <summary>
		/// Refreshes the list of save slots from the SaveManager.
		/// </summary>
                private void RefreshSaveSlots()
                {
                        if (saveManager == null) return;

                        saveSlots = saveManager.GetSaveSlots();
                        NormaliseSlotList();
                        Repaint();
                }

                /// <summary>
                /// Updates a specific slot in the local saveSlots list with fresh metadata from SaveManager.
                /// This ensures immediate UI updates after save operations without needing full slot refresh.
                /// </summary>
                /// <param name="updatedSlot">The slot that was just saved/updated (typically from event data).</param>
                private async void UpdateSlotWithFreshMetadata(SaveSlot updatedSlot)
                {
                        if (saveManager == null || updatedSlot == null || saveSlots == null) return;

                        try
                        {
                                Logger.Log($"SaveSlotManagerWindow: Starting metadata refresh for slot {updatedSlot.SlotNumber}. IsUsingCloudSave: {IsUsingCloudSave()}, IsWebGLTarget: {IsWebGLTarget()}", LogCategory.Other, LogLevel.Info);
                                
                                SaveSlot freshSlot = updatedSlot;
                                
                                // First, try to use the slot data passed in (typically from save event)
                                // This should already be the most up-to-date version
                                if (updatedSlot.LastSaved > DateTime.MinValue)
                                {
                                        Logger.Log($"SaveSlotManagerWindow: Using provided slot data for slot {updatedSlot.SlotNumber} (from save event). LastSaved: {updatedSlot.LastSaved}, Screenshot: {updatedSlot.ScreenshotFileName ?? "null"}", LogCategory.Other, LogLevel.Info);
                                        freshSlot = updatedSlot;
                                }
                                else
                                {
                                        // Fallback: If the provided slot seems incomplete, fetch fresh metadata
                                        Logger.Log($"SaveSlotManagerWindow: Provided slot data seems incomplete, fetching fresh metadata for slot {updatedSlot.SlotNumber}", LogLevel.Info);
                                        
                                        if (IsUsingCloudSave() && !IsWebGLTarget())
                                        {
                                                // For cloud saves in actual builds, use async method to get latest metadata
                                                Logger.Log($"SaveSlotManagerWindow: Using async GetSlotMetadataAsync for slot {updatedSlot.SlotNumber} (cloud save detected)", LogLevel.Info);
                                                freshSlot = await saveManager.GetSlotMetadataAsync(updatedSlot.SlotNumber);
                                        }
                                        else
                                        {
                                                // For local saves or WebGL in editor, get updated slot from SaveManager
                                                Logger.Log($"SaveSlotManagerWindow: Using local GetSaveSlotByNumber for slot {updatedSlot.SlotNumber} (local save or WebGL in editor)", LogLevel.Info);
                                                freshSlot = saveManager.GetSaveSlotByNumber(updatedSlot.SlotNumber);
                                        }
                                }

                                if (freshSlot != null)
                                {
                                        Logger.Log($"SaveSlotManagerWindow: Retrieved fresh metadata for slot {updatedSlot.SlotNumber}. LastSaved: {freshSlot.LastSaved}, Screenshot: {freshSlot.ScreenshotFileName ?? "null"}", LogCategory.Other, LogLevel.Info);
                                        
                                        // Find the slot in our local list and replace it
                                        for (int i = 0; i < saveSlots.Count; i++)
                                        {
                                                if (saveSlots[i].SlotNumber == updatedSlot.SlotNumber)
                                                {
                                                        var oldSlot = saveSlots[i];
                                                        saveSlots[i] = freshSlot;
                                                        Logger.Log($"SaveSlotManagerWindow: Updated slot {updatedSlot.SlotNumber} - OLD: {oldSlot.LastSaved}, NEW: {freshSlot.LastSaved}", LogCategory.Other, LogLevel.Info);
                                                        break;
                                                }
                                        }
                                        
                                        // Re-normalize and repaint to show updated information
                                        NormaliseSlotList();
                                        Repaint();
                                }
                                else
                                {
                                        Logger.Log($"SaveSlotManagerWindow: Could not retrieve fresh metadata for slot {updatedSlot.SlotNumber}, falling back to full refresh.", LogLevel.Warning);
                                        // Fallback to full refresh if we can't get specific slot metadata
                                        RefreshSaveSlots();
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SaveSlotManagerWindow: Exception while updating slot metadata: {ex.Message}. Falling back to full refresh.", LogLevel.Error);
                                // Fallback to full refresh if async operation fails
                                RefreshSaveSlots();
                        }
                }

                /// <summary>
                /// Forces a reinitialisation of the save slots by asking the
                /// <see cref="SaveManager"/> to reload all slot metadata. This
                /// is useful when slots might have changed on disk between play
                /// sessions.
                /// </summary>
                private async Task ForceRefreshSlotsAsync()
                {
                        if (saveManager == null)
                                return;
				try
				{
						isConnectingToCloud = true;
						ShowNotification(new GUIContent("Establishing Connection to Cloud Backend..."));

						ClearScreenshotCache();

						// Use runtime slot count, fallback to settings if not yet initialized
						int slotCount = saveManager.CurrentSaveSlotCount > 0 
							? saveManager.CurrentSaveSlotCount 
							: saveManager.GetSaveSettings().numberOfSaveSlots;
						await saveManager.InitializeSaveSlotsAsync(slotCount);

						await saveManager.RefreshRemoteSlotsAsync();                                RefreshSaveSlots();
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"ForceRefreshSlotsAsync failed: {ex.Message}", LogLevel.Warning);
                                feedbackMessage = $"Cloud refresh failed: {ex.Message}";
                                feedbackMessageType = MessageType.Warning;
                        }
                        finally
                        {
                                isConnectingToCloud = false;
                                RemoveNotification();
                                Repaint();
                        }
                }

		/// <summary>
		/// Opens a window to rename a specific save slot.
		/// </summary>
		/// <param name="slot">The save slot to rename.</param>
		private void RenameSaveSlot(SaveSlot slot)
		{
			RenameSaveSlotWindow.ShowWindow(slot, this);
		}

		/// <summary>
		/// Synchronously renames a save slot and updates the UI.
		/// </summary>
		/// <param name="slot">The save slot to rename.</param>
		/// <param name="newName">The new name for the save slot.</param>
		public void RenameSaveSlot(SaveSlot slot, string newName)
		{
			if (saveManager == null)
			{
				feedbackMessage = "SaveManager instance is not available.";
				feedbackMessageType = MessageType.Error;
				Repaint();
				return;
			}

                        try
                        {
                                isRenaming = true;
                                // Initiate the asynchronous renaming operation
                                _ = saveManager.RenameSaveSlotAsync(slot.SlotNumber, newName);
                        }
			catch (Exception ex)
			{
				feedbackMessage = $"Exception during renaming: {ex.Message}";
				feedbackMessageType = MessageType.Error;
				Logger.Log($"SaveSlotManagerWindow: Exception during RenameSaveSlot: {ex.Message}", LogLevel.Error);
				Repaint();
			}
		}

		/// <summary>
		/// Editor window for renaming a save slot.
		/// </summary>
		private class RenameSaveSlotWindow : EditorWindow
		{
			private SaveSlot slotToRename;
			private SaveSlotManagerWindow parentWindow;
			private string newName = "";

			public static void ShowWindow(SaveSlot slot, SaveSlotManagerWindow parent)
			{
				RenameSaveSlotWindow window = CreateInstance<RenameSaveSlotWindow>();
				window.slotToRename = slot;
				window.parentWindow = parent;
				window.titleContent = new GUIContent("Rename Save Slot");

				Rect parentRect = parent.position;
				float windowWidth = 300;
				float windowHeight = 100;
				window.position = new Rect(
					parentRect.x + parentRect.width / 2 - windowWidth / 2,
					parentRect.y + parentRect.height / 2 - windowHeight / 2,
					windowWidth,
					windowHeight
				);

				window.Show();
			}

			private void OnGUI()
			{
				GUILayout.Label($"Rename Save Slot {slotToRename.SlotNumber}", EditorStyles.boldLabel);
				newName = EditorGUILayout.TextField("New Name:", newName);

				GUILayout.Space(10);

				EditorGUILayout.BeginHorizontal();

				if (GUILayout.Button("OK"))
				{
					if (!string.IsNullOrEmpty(newName))
					{
						// Call the asynchronous RenameSaveSlot method
						parentWindow.RenameSaveSlot(slotToRename, newName);
						Close();
					}
					else
					{
						EditorUtility.DisplayDialog("Invalid Name", "Save slot name cannot be empty.", "OK");
					}
				}

				if (GUILayout.Button("Cancel"))
				{
					Close();
				}

				EditorGUILayout.EndHorizontal();
			}
		}
	}
}
#endif
#endif
