#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Authentication;
using Unity.Services.Core;
#endif
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Demo
{
	public class SaveLoadUI : MonoBehaviour 
	{
		[Header("UI Buttons")]
		[Tooltip("Button to trigger saving.")]
		public Button saveButton;

		[Tooltip("Button to trigger loading.")]
		public Button loadButton;
		
		[Tooltip("Button to check cloud connection status.")]
		public Button cloudStatusButton;
		
		[Tooltip("Button to manually authenticate with cloud services.")]
		public Button authenticateButton;

		[Header("Status Display")]
		[Tooltip("Text to display current save backend and connection status.")]
		public Text statusText;
		
		[Tooltip("Text to display operation progress and results.")]
		public Text operationStatusText;

		[Header("Save Properties")]
		[Tooltip("Slot number to save to. Default is 1.")]
		public int saveSlotNumber = 1;

		[Header("Load Properties")]
		[Tooltip("Slot number to load from. Default is 1.")]
		public int loadSlotNumber = 1;

		[Tooltip("Restore the last active scene upon loading.")]
		public bool loadRestoreLastActiveScene = false;

		[Tooltip("Load the scene asynchronously.")]
		public bool loadAsync = false;

		[Tooltip("Allow scene activation during asynchronous loading.")]
		public bool allowSceneActivation = true;
		
		[Header("WebGL/Cloud Specific")]
		[Tooltip("Show additional debug info for WebGL and cloud operations.")]
		public bool verboseLogging = true;
		
		private bool isOperationInProgress = false;

		private void Start()
		{
			StartCoroutine(InitializeAsync());
		}
		
		private IEnumerator InitializeAsync()
		{
			Logger.Log("[SaveLoadUI] InitializeAsync started", LogCategory.Other, LogLevel.Info);
			UpdateOperationStatus("Initializing SaveManager...");
			
			// Wait for SaveManager to be initialized
			int waitFrames = 0;
			const int maxWaitFrames = 600; // 60 seconds at 60fps
			while (SaveManager.Instance == null || !SaveManager.IsInitialized)
			{
				waitFrames++;
				if (waitFrames % 60 == 0) // Log every 60 frames
				{
					Logger.Log($"[SaveLoadUI] Waiting for SaveManager initialization... Frame: {waitFrames}, Instance: {SaveManager.Instance}, Initialized: {SaveManager.IsInitialized}", LogCategory.Other, LogLevel.Info);
					
					// Add detailed debugging about SaveManager state
					if (SaveManager.Instance != null)
					{
						Logger.Log($"[SaveLoadUI] SaveManager exists but not initialized. SaveSettings: {SaveManager.Instance.SaveSettings}", LogCategory.Other, LogLevel.Info);
						if (SaveManager.Instance.SaveSettings != null)
						{
							Logger.Log($"[SaveLoadUI] SaveSettings found - Backend: {SaveManager.Instance.SaveSettings.backend}, EnableCloudSave: {SaveManager.Instance.SaveSettings.enableCloudSave}", LogCategory.Other, LogLevel.Info);
						}
					}
				}
				
				// Timeout after reasonable wait time
				if (waitFrames >= maxWaitFrames)
				{
					Logger.Log("[SaveLoadUI] SaveManager initialization timeout! Setting up UI anyway...", LogCategory.Other, LogLevel.Error);
					break;
				}
				
				yield return new WaitForSeconds(0.1f);
			}
			
			if (SaveManager.IsInitialized)
			{
				Logger.Log($"[SaveLoadUI] SaveManager initialized successfully! Instance: {SaveManager.Instance}, Backend: {SaveManager.Instance?.SaveSettings?.backend}", LogCategory.Other, LogLevel.Info);
			}
			else
			{
				Logger.Log("[SaveLoadUI] SaveManager failed to initialize, but continuing with UI setup...", LogCategory.Other, LogLevel.Warning);
			}

#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log("SaveLoadUI: WebGL build detected, using cloud-optimized initialization", LogCategory.Other, LogLevel.Info);
#endif

			// Subscribe to SaveManager events for better feedback (only if initialized)
			if (SaveManager.IsInitialized)
			{
				SubscribeToSaveManagerEvents();
			}
			
			// Setup UI button listeners regardless of SaveManager state
			SetupButtonListeners();
			
			// Check cloud connection status (only if SaveManager is ready)
			if (SaveManager.IsInitialized)
			{
				yield return StartCoroutine(CheckCloudConnectionAsync());
			}
			else
			{
				UpdateOperationStatus("SaveManager not initialized - some features may not work");
			}
			
			// Update initial status
			UpdateStatusDisplay();
			if (SaveManager.IsInitialized)
			{
				UpdateOperationStatus("Ready");
			}
			
			Logger.Log("[SaveLoadUI] Initialization complete", LogCategory.Other, LogLevel.Info);
			Logger.Log("SaveLoadUI: Initialization complete", LogCategory.Other, LogLevel.Info);
		}
		
		private void SubscribeToSaveManagerEvents()
		{
			var manager = SaveManager.Instance;
			if (manager != null)
			{
				manager.OnSaveCompleted += OnSaveCompleted;
				manager.OnSaveFailed += OnSaveFailed;
				manager.OnLoadCompleted += OnLoadCompleted;
				manager.OnLoadFailed += OnLoadFailed;
			}
		}
		
		private void UnsubscribeFromSaveManagerEvents()
		{
			var manager = SaveManager.Instance;
			if (manager != null)
			{
				manager.OnSaveCompleted -= OnSaveCompleted;
				manager.OnSaveFailed -= OnSaveFailed;
				manager.OnLoadCompleted -= OnLoadCompleted;
				manager.OnLoadFailed -= OnLoadFailed;
			}
		}
		
		private void SetupButtonListeners()
		{
			Logger.Log("[SaveLoadUI] Setting up button listeners...", LogCategory.Other, LogLevel.Info);
			
			if (saveButton != null)
			{
				Logger.Log($"[SaveLoadUI] Save button found: {saveButton.name}, setting up listener", LogCategory.Other, LogLevel.Info);
				saveButton.onClick.AddListener(OnSaveButtonClicked);
				Logger.Log("[SaveLoadUI] Save button listener added successfully", LogCategory.Other, LogLevel.Info);
			}
			else
			{
				Logger.Log("SaveLoadUI: Save button is not assigned.", LogCategory.Other, LogLevel.Warning);
			}

			if (loadButton != null)
			{
				Logger.Log($"[SaveLoadUI] Load button found: {loadButton.name}, setting up listener", LogCategory.Other, LogLevel.Info);
				loadButton.onClick.AddListener(OnLoadButtonClicked);
			}
			else
			{
				Logger.Log("SaveLoadUI: Load button is not assigned.", LogCategory.Other, LogLevel.Warning);
			}
			
			if (cloudStatusButton != null)
			{
				cloudStatusButton.onClick.AddListener(OnCloudStatusButtonClicked);
			}
			
			if (authenticateButton != null)
			{
				authenticateButton.onClick.AddListener(OnAuthenticateButtonClicked);
			}
			
			Logger.Log("[SaveLoadUI] Button listener setup completed", LogCategory.Other, LogLevel.Info);
		}

		private void OnSaveButtonClicked()
		{
			// Add early debug logging that will always show
			Logger.Log("[SaveLoadUI] SAVE BUTTON CLICKED - Entry point", LogCategory.Other, LogLevel.Info);
			Logger.Log("[SaveLoadUI] Unity Debug: Save button clicked!", LogCategory.Other, LogLevel.Info);
			
#if UNITY_WEBGL && !UNITY_EDITOR
			Logger.Log("[SaveLoadUI] WebGL: Save button clicked!", LogCategory.Other, LogLevel.Info);
#endif

			// Check if SaveManager is initialized
			if (SaveManager.Instance == null || !SaveManager.IsInitialized)
			{
				Logger.Log("[SaveLoadUI] SaveManager is not initialized! Cannot save.", LogCategory.Other, LogLevel.Error);
				UpdateOperationStatus("SaveManager not initialized - cannot save");
				return;
			}

			if (isOperationInProgress)
			{
				Logger.Log("[SaveLoadUI] Operation already in progress, returning", LogCategory.Other, LogLevel.Info);
				UpdateOperationStatus("Save operation already in progress...");
				return;
			}
			
#if UNITY_WEBGL && !UNITY_EDITOR
			Logger.Log("[SaveLoadUI] WebGL: Starting SaveAsync coroutine...", LogCategory.Other, LogLevel.Info);
#endif
			Logger.Log("[SaveLoadUI] About to start SaveAsync coroutine", LogCategory.Other, LogLevel.Info);
			StartCoroutine(SaveAsync());
			Logger.Log("[SaveLoadUI] SaveAsync coroutine started", LogCategory.Other, LogLevel.Info);
		}
		
		private IEnumerator SaveAsync()
		{
			// Immediate logging to trace coroutine execution
			Logger.Log("[SaveLoadUI] SaveAsync coroutine STARTED - First line executed", LogCategory.Other, LogLevel.Info);
			Logger.Log("[SaveLoadUI] Unity Debug: SaveAsync coroutine started", LogCategory.Other, LogLevel.Info);
			
#if UNITY_WEBGL && !UNITY_EDITOR
			Logger.Log("[SaveLoadUI] WebGL: SaveAsync coroutine started!", LogCategory.Other, LogLevel.Info);
#endif
			isOperationInProgress = true;
			UpdateButtonStates(false);
			UpdateOperationStatus($"Saving to slot {saveSlotNumber}...");
			
			Logger.Log("[SaveLoadUI] SaveAsync: Basic setup completed, checking SaveManager", LogCategory.Other, LogLevel.Info);
			
#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log($"SaveLoadUI: WebGL save initiated for slot {saveSlotNumber}", LogCategory.Other, LogLevel.Info);
#endif

			// Check cloud connection before saving
			if (SaveManager.Instance.SaveSettings.enableCloudSave)
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log("[SaveLoadUI] WebGL: Cloud save is enabled, checking connection...", LogCategory.Other, LogLevel.Info);
#endif
				Logger.Log("[SaveLoadUI] SaveAsync: Cloud save enabled, checking connection", LogCategory.Other, LogLevel.Info);
				UpdateOperationStatus("Checking cloud connection...");
				var cloudConnected = SaveManager.Instance.WaitForCloudConnectionAsync();
				yield return new WaitUntil(() => cloudConnected.IsCompleted);
				
#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log($"[SaveLoadUI] WebGL: Cloud connection result: {cloudConnected.Result}", LogCategory.Other, LogLevel.Info);
#endif
				Logger.Log($"[SaveLoadUI] SaveAsync: Cloud connection completed, result: {cloudConnected.Result}", LogCategory.Other, LogLevel.Info);
				
				if (!cloudConnected.Result)
				{
					Logger.Log("[SaveLoadUI] SaveAsync: Cloud connection failed, aborting save", LogCategory.Other, LogLevel.Warning);
					UpdateOperationStatus("Cloud connection failed! Cannot save.");
					isOperationInProgress = false;
					UpdateButtonStates(true);
					yield break;
				}
			}
#if UNITY_WEBGL && !UNITY_EDITOR
			else
			{
				Logger.Log("[SaveLoadUI] WebGL: Cloud save is disabled, proceeding with local save", LogCategory.Other, LogLevel.Info);
			}
#endif

			Logger.Log($"[SaveLoadUI] SaveAsync: About to trigger save for slot {saveSlotNumber}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"SaveLoadUI: Triggering async save: Slot {saveSlotNumber}", LogCategory.Other, LogLevel.Info);
			
			// CRITICAL: Use SaveAsync instead of Save to prevent WebGL freezing with cloud backends
			// The synchronous Save() method can cause browser hanging in WebGL builds
			bool saveCompleted = false;
			bool saveFailed = false;
			string saveError = null;
			
			// Get current scene name for the save
			string currentSceneName = SceneManager.GetActiveScene().name;
			
			// Debug logging for WebGL
#if UNITY_WEBGL && !UNITY_EDITOR
			Logger.Log($"[SaveLoadUI] WebGL: About to call SaveManager.Instance.SaveAsync({saveSlotNumber}, '{currentSceneName}')", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: SaveManager.Instance = {SaveManager.Instance}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: SaveSettings = {SaveManager.Instance?.SaveSettings}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: Backend = {SaveManager.Instance?.SaveSettings?.backend}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: EnableCloudSave = {SaveManager.Instance?.SaveSettings?.enableCloudSave}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: SaveOperations = {SaveManager.Instance?.SaveOperations}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: SaveSystem = {SaveManager.Instance?.SaveSystem}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: IsInitialized = {SaveManager.IsInitialized}", LogCategory.Other, LogLevel.Info);
#endif
			
			// Start the async save operation
			Task saveTask = null;
			try
			{
				saveTask = SaveManager.Instance.SaveAsync(saveSlotNumber, currentSceneName);
#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log($"[SaveLoadUI] WebGL: SaveAsync task created successfully: {saveTask}", LogCategory.Other, LogLevel.Info);
#endif
			}
			catch (System.Exception ex)
			{

#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log($"[SaveLoadUI] WebGL: Exception creating SaveAsync task: {ex}", LogCategory.Other, LogLevel.Error);
#endif
				UpdateOperationStatus($"Save failed: {ex.Message}");
				isOperationInProgress = false;
				UpdateButtonStates(true);
				yield break;
			}
			
			if (saveTask == null)
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				Logger.Log("[SaveLoadUI] WebGL: SaveAsync returned null task!", LogCategory.Other, LogLevel.Error);
#endif
				UpdateOperationStatus("Save failed: SaveAsync returned null");
				isOperationInProgress = false;
				UpdateButtonStates(true);
				yield break;
			}
			
#if UNITY_WEBGL && !UNITY_EDITOR
			Logger.Log($"[SaveLoadUI] WebGL: SaveAsync task created: {saveTask}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: Task status: {saveTask.Status}", LogCategory.Other, LogLevel.Info);
#endif
			
			// Wait for completion in a WebGL-friendly way
#if UNITY_WEBGL && !UNITY_EDITOR
			int waitCounter = 0;
#endif
			while (!saveTask.IsCompleted)
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				waitCounter++;
				if (waitCounter % 60 == 0) // Log every 60 frames (roughly 1 second)
				{
					Logger.Log($"[SaveLoadUI] WebGL: Still waiting for save task... Status: {saveTask.Status}, Counter: {waitCounter}", LogCategory.Other, LogLevel.Info);
				}
#endif
				yield return null;
			}
			
#if UNITY_WEBGL && !UNITY_EDITOR
			Logger.Log($"[SaveLoadUI] WebGL: Save task completed! Final status: {saveTask.Status}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: IsFaulted: {saveTask.IsFaulted}", LogCategory.Other, LogLevel.Info);
			Logger.Log($"[SaveLoadUI] WebGL: IsCompletedSuccessfully: {saveTask.IsCompletedSuccessfully}", LogCategory.Other, LogLevel.Info);
			if (saveTask.Exception != null)
			{
				Logger.Log($"[SaveLoadUI] WebGL: Task exception: {saveTask.Exception}", LogCategory.Other, LogLevel.Error);
			}
#endif
			
			// Check the result
			if (saveTask.IsFaulted)
			{
				saveFailed = true;
				saveError = saveTask.Exception?.GetBaseException()?.Message ?? "Unknown save error";
			}
			else
			{
				saveCompleted = true;
			}
			
			// Handle completion (this might be redundant with events, but ensures UI recovery)
			if (saveCompleted)
			{
				UpdateOperationStatus($"Save completed successfully! Slot: {saveSlotNumber}");
			}
			else if (saveFailed)
			{
				UpdateOperationStatus($"Save failed: {saveError}");
			}
			
			isOperationInProgress = false;
			UpdateButtonStates(true);
		}

		private void OnLoadButtonClicked()
		{
			if (isOperationInProgress)
			{
				UpdateOperationStatus("Load operation already in progress...");
				return;
			}
			
			StartCoroutine(LoadAsync());
		}
		
		private IEnumerator LoadAsync()
		{
			isOperationInProgress = true;
			UpdateButtonStates(false);
			UpdateOperationStatus($"Loading from slot {loadSlotNumber}...");
			
#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log($"SaveLoadUI: WebGL load initiated for slot {loadSlotNumber}", LogCategory.Other, LogLevel.Info);
#endif

			// Check cloud connection before loading
			if (SaveManager.Instance.SaveSettings.enableCloudSave)
			{
				UpdateOperationStatus("Checking cloud connection...");
				var cloudConnected = SaveManager.Instance.WaitForCloudConnectionAsync();
				yield return new WaitUntil(() => cloudConnected.IsCompleted);
				
				if (!cloudConnected.Result)
				{
					UpdateOperationStatus("Cloud connection failed! Cannot load.");
					isOperationInProgress = false;
					UpdateButtonStates(true);
					yield break;
				}
			}

			Logger.Log($"SaveLoadUI: Triggering async load: Slot {loadSlotNumber}, Restore Scene: {loadRestoreLastActiveScene}, Async: {loadAsync}, Allow Activation: {allowSceneActivation}", LogCategory.Other, LogLevel.Info);
			
			// CRITICAL: Use LoadSaveSlotAsync instead of Load to prevent WebGL freezing with cloud backends
			// The synchronous Load() method can cause browser hanging in WebGL builds
			bool loadCompleted = false;
			bool loadFailed = false;
			string loadError = null;
			
			// Start the async load operation
			var loadTask = SaveManager.Instance.LoadSaveSlotAsync(
				loadSlotNumber, 
				loadRestoreLastActiveScene, 
				loadAsync, 
				allowSceneActivation
			);
			
			// Wait for completion in a WebGL-friendly way
			while (!loadTask.IsCompleted)
			{
				yield return null;
			}
			
			// Check the result
			if (loadTask.IsFaulted)
			{
				loadFailed = true;
				loadError = loadTask.Exception?.GetBaseException()?.Message ?? "Unknown load error";
			}
			else
			{
				var result = loadTask.Result;
				if (result.Success)
				{
					loadCompleted = true;
				}
				else
				{
					loadFailed = true;
					loadError = result.ErrorMessage ?? "Load operation failed";
				}
			}
			
			// Handle completion (this might be redundant with events, but ensures UI recovery)
			if (loadCompleted)
			{
				UpdateOperationStatus($"Load completed successfully! Slot: {loadSlotNumber}");
			}
			else if (loadFailed)
			{
				UpdateOperationStatus($"Load failed: {loadError}");
			}
			
			isOperationInProgress = false;
			UpdateButtonStates(true);
		}
		
		private void OnCloudStatusButtonClicked()
		{
			StartCoroutine(CheckCloudConnectionAsync());
		}
		
		private void OnAuthenticateButtonClicked()
		{
			StartCoroutine(AuthenticateAsync());
		}
		
		private IEnumerator CheckCloudConnectionAsync()
		{
			UpdateOperationStatus("Checking cloud connection...");
			
			var settings = SaveManager.Instance?.SaveSettings;
			if (settings == null || !settings.enableCloudSave)
			{
				UpdateOperationStatus("Cloud save is disabled");
				yield break;
			}
			
			var cloudConnected = SaveManager.Instance.WaitForCloudConnectionAsync();
			yield return new WaitUntil(() => cloudConnected.IsCompleted);
			
			bool connected = cloudConnected.Result;
			string status = connected ? "Cloud connection successful!" : "Cloud connection failed!";
			UpdateOperationStatus(status);
			UpdateStatusDisplay();
			
#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log($"SaveLoadUI: WebGL cloud status check - Connected: {connected}, Backend: {settings.backend}", LogCategory.Other, LogLevel.Info);
#endif
		}
		
		private IEnumerator AuthenticateAsync()
		{
			UpdateOperationStatus("Authenticating...");
			
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
			if (!UnityServices.State.Equals(ServicesInitializationState.Initialized))
			{
				UpdateOperationStatus("Initializing Unity Services...");
				var initOp = UnityServices.InitializeAsync();
				yield return new WaitUntil(() => initOp.IsCompleted);
				
				if (initOp.Exception != null)
				{
					UpdateOperationStatus($"Unity Services initialization failed: {initOp.Exception.Message}");
					yield break;
				}
			}
			
			if (!AuthenticationService.Instance.IsSignedIn)
			{
				UpdateOperationStatus("Signing in anonymously...");
				var signInOp = AuthenticationService.Instance.SignInAnonymouslyAsync();
				yield return new WaitUntil(() => signInOp.IsCompleted);
				
				if (signInOp.Exception != null)
				{
					UpdateOperationStatus($"Authentication failed: {signInOp.Exception.Message}");
					yield break;
				}
			}
			
			UpdateOperationStatus($"Authenticated as: {AuthenticationService.Instance.PlayerId}");
#else
			UpdateOperationStatus("Authentication services not available (missing Unity Services packages)");
#endif
			UpdateStatusDisplay();
			yield break; // Ensure coroutine semantics even when Unity Services packages are not present
		}
		
		// Event handlers for SaveManager events
		private void OnSaveCompleted(object sender, SaveLoadEventArgs e)
		{
			UpdateOperationStatus($"Save completed successfully! Slot: {e.Slot?.SlotNumber}");
			isOperationInProgress = false;
			UpdateButtonStates(true);
			UpdateStatusDisplay();
			
#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log($"SaveLoadUI: WebGL save completed for slot {e.Slot?.SlotNumber}", LogCategory.Other, LogLevel.Info);
#endif
		}
		
		private void OnSaveFailed(object sender, OperationFailedEventArgs e)
		{
			UpdateOperationStatus($"Save failed: {e.ErrorMessage}");
			isOperationInProgress = false;
			UpdateButtonStates(true);
			
#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log($"SaveLoadUI: WebGL save failed for slot {e.Slot?.SlotNumber}: {e.ErrorMessage}", LogCategory.Other, LogLevel.Error);
#endif
		}
		
		private void OnLoadCompleted(object sender, SaveLoadEventArgs e)
		{
			UpdateOperationStatus($"Load completed successfully! Slot: {e.Slot?.SlotNumber}");
			isOperationInProgress = false;
			UpdateButtonStates(true);
			UpdateStatusDisplay();
			
#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log($"SaveLoadUI: WebGL load completed for slot {e.Slot?.SlotNumber}", LogCategory.Other, LogLevel.Info);
#endif
		}
		
		private void OnLoadFailed(object sender, OperationFailedEventArgs e)
		{
			UpdateOperationStatus($"Load failed: {e.ErrorMessage}");
			isOperationInProgress = false;
			UpdateButtonStates(true);
			
#if UNITY_WEBGL && !UNITY_EDITOR
			if (verboseLogging)
				Logger.Log($"SaveLoadUI: WebGL load failed for slot {e.Slot?.SlotNumber}: {e.ErrorMessage}", LogCategory.Other, LogLevel.Error);
#endif
		}
		
		// UI Helper methods
		private void UpdateStatusDisplay()
		{
			if (statusText == null) return;
			
			var settings = SaveManager.Instance?.SaveSettings;
			if (settings == null)
			{
				statusText.text = "SaveManager not available";
				return;
			}
			
			string backend = settings.enableCloudSave ? settings.backend.ToString() : "Local";
			string authStatus = "";
			
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
			if (settings.enableCloudSave && UnityServices.State.Equals(ServicesInitializationState.Initialized))
			{
				authStatus = AuthenticationService.Instance.IsSignedIn ? 
					$" (Authenticated: {AuthenticationService.Instance.PlayerId.Substring(0, 8)}...)" : 
					" (Not Authenticated)";
			}
#endif
			
#if UNITY_WEBGL && !UNITY_EDITOR
			statusText.text = $"Platform: WebGL\nBackend: {backend}{authStatus}\nLocal Mirror: {(settings.keepLocalMirror ? "Enabled" : "Disabled")}";
#else
			statusText.text = $"Platform: Desktop\nBackend: {backend}{authStatus}\nLocal Mirror: {(settings.keepLocalMirror ? "Enabled" : "Disabled")}";
#endif
		}
		
		private void UpdateOperationStatus(string message)
		{
			if (operationStatusText != null)
			{
				operationStatusText.text = message;
			}
			
			if (verboseLogging)
			{
				Logger.Log($"SaveLoadUI: {message}", LogCategory.Other, LogLevel.Info);
			}
		}
		
		private void UpdateButtonStates(bool enabled)
		{
			if (saveButton != null) saveButton.interactable = enabled;
			if (loadButton != null) loadButton.interactable = enabled;
			if (cloudStatusButton != null) cloudStatusButton.interactable = enabled;
			if (authenticateButton != null) authenticateButton.interactable = enabled;
		}

		private void OnDestroy()
		{
			// Unsubscribe from SaveManager events
			UnsubscribeFromSaveManagerEvents();
			// Clean up listeners
			if (saveButton != null)
			{
				saveButton.onClick.RemoveListener(OnSaveButtonClicked);
			}

			if (loadButton != null)
			{
				loadButton.onClick.RemoveListener(OnLoadButtonClicked);
			}
		}
	}
}
#endif