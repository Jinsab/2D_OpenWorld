#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public class InitializationHandler
        {
                private static bool ShouldAbort(SaveManager manager)
                {
                        return manager == null || manager.IsShuttingDown;
                }

                public async Task InitializeAsync(SaveManager manager)
                {
                        try
                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Starting initialization...");
#endif
                                if (ShouldAbort(manager)) return;

                                manager.LoadSaveSettings();
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: SaveSettings loaded");
#endif
                                TextureManager.Initialize();
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: TextureManager initialized");
#endif

#if REMEMBERME_CORESERVICES_PRESENT
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Ensuring Unity Services initialized...");
#endif
                                await SaveManager.EnsureUnityServicesInitializedAsync();
                                if (ShouldAbort(manager)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Unity Services initialization complete");
#endif
#endif

                                if (ShouldAbort(manager)) return;
                                if (manager.SaveSettings == null)
                                        throw new InvalidOperationException("SaveSettings is not assigned. Please complete the Settings Wizard via Tools > Crystal Save > Settings Wizard, or import demo settings via Tools > Crystal Save > Settings > Install Demo Settings.");

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log($"[InitializationHandler] WebGL: SaveSettings validated, backend: {manager.SaveSettings.backend}");
#endif

                                Logger.LogThreshold = manager.SaveSettings.logLevel;

                                if (!manager.SaveSettings.enableCloudSave &&
                                    manager.SaveSettings.backend != SaveBackend.UnityCloudSave)
                                {
#if UNITY_WEBGL && !UNITY_EDITOR
                                        Debug.Log("[InitializationHandler] WebGL: Cloud save is OFF, switching to UnityCloudSave");
#endif
                                        Logger.Log("Cloud-Save is OFF – switching backend to UnityCloudSave.",
                                                   LogCategory.SaveManager, LogLevel.Warning);
                                        manager.SaveSettings.backend = SaveBackend.UnityCloudSave;
#if UNITY_EDITOR
                                        UnityEditor.EditorUtility.SetDirty(manager.SaveSettings);
#endif
                                }

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Initializing encryption service...");
#endif
                                bool serverSideCloudCrypto = manager.SaveSettings.enableCloudSave &&
                                                             manager.SaveSettings.cloudCryptoMode == CloudCryptoMode.ServerSide;
                                if (serverSideCloudCrypto)
                                {
                                        Logger.Log("Server-side cloud crypto enabled; client-side encryption is disabled.",
                                                   LogCategory.Cryptography, LogLevel.Info);
                                        manager.EncryptionService = null;
                                        manager.UseEncryptionFlag = false;
                                }
                                else
                                {
                                        manager.EncryptionService = new EncryptionService(manager.SaveSettings);
                                        string uid = manager.SaveSettings.useUserIdForEncryption
                                                        ? SaveManager.ResolveUserIdentifier()
                                                        : null;
                                        await manager.EncryptionService.InitializeAsync(uid);
                                        if (ShouldAbort(manager)) return;
                                        manager.UseEncryptionFlag = manager.EncryptionService.UseEncryption;
                                }
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Encryption service initialized");
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Initializing compression service...");
#endif
                                manager.CompressionService = new CompressionService(manager.SaveSettings);
                                await manager.CompressionService.InitializeAsync();
                                if (ShouldAbort(manager)) return;
                                manager.UseCompressionFlag = manager.CompressionService.UseCompression;
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Compression service initialized");
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Initializing versioning service...");
#endif
                                manager.VersioningService = new VersioningService(manager);
                                await manager.VersioningService.InitializeAsync();
                                if (ShouldAbort(manager)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Versioning service initialized");
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Creating save system...");
#endif
                                manager.SaveSystemInternal = manager.CreateSaveSystem();
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Save system created successfully");
#endif

                                manager.PrefabManagerInternal = manager.GetComponent<PrefabManager>();
                                manager.GameObjectTrackerInternal = manager.GetComponent<GameObjectTracker>();
                                                                manager.ScreenshotManagerInternal = new ScreenshotManager(manager.SaveSettings, manager.RootPath);
                                manager.ComponentManagerInternal = new ComponentManager();

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Core components initialized");
#endif

                                if (manager.PrefabManagerInternal == null)
                                        throw new InvalidOperationException("PrefabManager instance not found.");

                                if (manager.SceneObjectRegistryInternal == null)
                                        manager.SceneObjectRegistryInternal = AssetProvider.Load<SceneObjectRegistry>("SceneObjectRegistry");

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Creating SlotManager...");
#endif
                                manager.SlotManager = new SaveSlotManager(
                                        manager.SaveSystemInternal,
                                        manager.SaveSettings,
                                        manager.ScreenshotManagerInternal,
                                        manager.RootPath);

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Initializing save slots...");
#endif
                                // Use the public API to initialise the save slots so
                                // AreSaveSlotsReady and the SaveSlotsInitialized event
                                // are properly set.
                                await manager.InitializeSaveSlotsAsync(
                                        manager.SaveSettings.numberOfSaveSlots);
                                if (ShouldAbort(manager)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Save slots initialization completed");
                                Debug.Log("[InitializationHandler] WebGL: Initializing build scene names...");
#endif
                                manager.InitializeBuildSceneNames();

                                // Register SaveableComponents on disabled GameObjects if configured for initialization
                                if (manager.SaveSettings != null && 
                                    manager.SaveSettings.registerDisabledComponents && 
                                    manager.SaveSettings.disabledComponentScanMode == DisabledComponentScanMode.OnInitialization)
                                {
                                    manager.RegisterExistingSaveableComponents();
                                }

#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[InitializationHandler] WebGL: Build scene names initialized");
                                Debug.Log("[InitializationHandler] WebGL: Initialization completed successfully!");
#endif
                        }
                        catch (System.Exception ex)
                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.LogError($"[InitializationHandler] WebGL: Initialization failed with exception: {ex.Message}");
                                Debug.LogError($"[InitializationHandler] WebGL: Exception type: {ex.GetType().Name}");
                                Debug.LogError($"[InitializationHandler] WebGL: Stack trace: {ex.StackTrace}");
#else
                                Debug.LogError($"[InitializationHandler] Initialization failed with exception: {ex.Message}");
#endif
                                throw;
                        }
                }
        }
}
#endif
