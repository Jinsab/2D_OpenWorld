#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
        public partial class SaveManager
        {
                // Internal accessors for refactored components
                internal byte[] CryptoKey { get => cryptoKey; set => cryptoKey = value; }
                internal bool UseEncryptionFlag { get => useEncryption; set => useEncryption = value; }
                internal bool UseCompressionFlag { get => useCompression; set => useCompression = value; }
                internal IMasterSecretProvider SecretProviderInternal { get => secretProvider; set => secretProvider = value; }
                internal byte[] MasterSecretInternal { get => masterSecret; set => masterSecret = value; }

                internal MigrationManager MigrationManagerInternal { get => migrationManager; set => migrationManager = value; }
                internal VersionManager VersionManagerInternal { get => versionManager; set => versionManager = value; }
                internal SaveDataSerializer SerializerInternal { get => serializer; set => serializer = value; }
                internal ISaveSystem SaveSystemInternal { get => saveSystem; set => saveSystem = value; }
                internal ScreenshotManager ScreenshotManagerInternal { get => screenshotManager; set => screenshotManager = value; }
                internal PrefabManager PrefabManagerInternal { get => prefabManager; set => prefabManager = value; }
                internal ComponentManager ComponentManagerInternal { get => componentManager; set => componentManager = value; }
                internal GameObjectTracker GameObjectTrackerInternal { get => gameObjectTracker; set => gameObjectTracker = value; }
                internal SceneObjectRegistry SceneObjectRegistryInternal { get => sceneObjectRegistry; set => sceneObjectRegistry = value; }
                internal List<SaveSlot> SaveSlotsInternal => saveSlots;
                internal List<SaveSlot> QuickSaveSlotsInternal => quickSaveSlots;
                internal List<SaveSlot> AutoSaveSlotsInternal => autoSaveSlots;

                internal EncryptionService EncryptionService { get; set; }
                internal CompressionService CompressionService { get; set; }
                internal SaveSlotManager SlotManager { get; set; }
                internal SaveSlotManager QuickSlotManager { get; set; }
                internal SaveSlotManager AutoSlotManager { get; set; }
                internal InitializationHandler InitializationHandler { get; set; }
                internal CloudSaveService CloudSaveService { get; set; }
                internal PrefabRestoreService PrefabRestoreService { get; set; }
                internal SaveOperationService SaveOperations { get; set; }
                internal VersioningService VersioningService { get; set; }
                internal bool IsSupabaseCustomLoggedInInternal
                {
                        get => isSupabaseCustomLoggedIn;
                        set => isSupabaseCustomLoggedIn = value;
                }

                internal bool IsLoadingInternal
                {
                        get => isLoading;
                        set => isLoading = value;
                }

                internal bool IsInSceneTransitionInternal
                {
                        get => isInSceneTransition;
                        set => isInSceneTransition = value;
                }

                internal SceneLoadManager SceneLoadManager { get; set; }
        }
}
#endif
