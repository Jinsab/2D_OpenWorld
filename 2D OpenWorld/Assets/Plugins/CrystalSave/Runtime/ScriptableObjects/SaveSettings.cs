#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
#if CRYSTALSAVE_TIMEMACHINE
using Arawn.CrystalSave.Runtime.TimeMachine;
#endif

namespace Arawn.CrystalSave.Runtime
{
    public enum PersistentPathMode { Default, Custom }
    public enum MasterSecretSource
    {
        StaticAsset,
        [Obsolete("Deprecated: use Cloud Crypto Mode = ServerSide instead.")]
        Server,
        UserPassphrase
    }
    public enum CloudCryptoMode { ClientSide, ServerSide }

    public enum SceneObjectScanMode
    {
        CurrentOpenedScene,
        ScenesInBuildList,
        AllScenesInProject,
        Custom
    }

    /// <summary>
    /// Controls when Crystal Save scans for SaveableComponents on disabled GameObjects.
    /// </summary>
    public enum DisabledComponentScanMode
    {
        /// <summary>
        /// Scan once during Crystal Save initialization (startup).
        /// </summary>
        OnInitialization,
        
        /// <summary>
        /// Scan each time a scene is loaded. More thorough for additive scenes.
        /// </summary>
        OnSceneLoad
    }

    [Serializable]
    public class PersistentPathOptions
    {
        [Tooltip(
            "How the root folder for save data is resolved.\n" +
            "• Default – uses Application.persistentDataPath (WebGL: /idbfs/<product>).\n" +
            "• Custom  – lets you pick a folder name and optional root path.\n" +
            "           Useful on WebGL when hosting multiple games on the same domain\n" +
            "           or when you need to migrate saves between builds.")]
        public PersistentPathMode mode = PersistentPathMode.Default;

        [Tooltip("Folder name used inside the root. For WebGL idbfs this becomes /idbfs/<folder>.")]
        public string customFolderName = "CrystalSave";

        [Tooltip("Optional absolute override for non WebGL platforms. Leave empty to nest under Application.persistentDataPath.")]
        public string nonWebGLOutputRoot = "";
    }

        [CreateAssetMenu(fileName = "SaveSettings", menuName = "Crystal Save/Settings/Create Save Settings File")]
        public class SaveSettings : ScriptableObject
        {
                // Set true after completing the first-time Settings Wizard onboarding
                [HideInInspector]
                public bool onboardingCompleted = false;
                [Tooltip("Method to use for saving data.")]
                public SaveMethod saveMethod = SaveMethod.BinaryFileFormat;

		[Tooltip("Your Game Version")]
		public VersionData version = new VersionData(1, 0, 0);

                [Tooltip("File name for saving data when using Binary File Format (.sav) method.")]
                public string saveFileName = "Slot_{n}";

                [Tooltip("Key for saving data when using PlayerPrefs method.")]
                public string saveKey = "Slot_{n}";

        public PersistentPathOptions persistentPath = new PersistentPathOptions();

        [Tooltip("Call FS.syncfs() manually on WebGL after file operations. Deprecated; Unity handles this automatically.")]
        public bool useManualSync = false;

        [Header("Save Slots")]
        [Tooltip("Number of save slots available.")]
        public int numberOfSaveSlots = 5;

        [Tooltip(
                "How many quick saves Crystal Save remembers.\n" +
                "By default they start in a separate block of slots so they won't\n" +
                "clash with normal or auto saves unless you choose overlapping\n" +
                "numbers. The newest quick save always goes into the first quick\n" +
                "save slot and pushes the older ones up." )]
        public int numberOfQuickSaveSlots = 1;

        [Tooltip(
                "Starting number for quick save slots.\n" +
                "Example: an offset of 100 means slot 101 holds the latest quick save.\n" +
                "Pick a high offset if you want lots of regular slots below the quick\n" +
                "save range.")]
        public int quickSaveSlotOffset = 1;

        [Tooltip(
                "Which slot number is used for auto saves. Set 0 to turn them off.\n" +
                "Regular or quick saves can still overwrite this slot if they use\n" +
                "the same number.")]
        public int autoSaveSlotNumber = 1;

        [Tooltip(
                "How many auto save slots Crystal Save remembers.\n" +
                "Works like quick saves: the newest auto save goes into the\n" +
                "first slot and pushes older ones up. Set 0 to disable\n" +
                "multi-slot auto saves (falls back to single autoSaveSlotNumber).")]
        public int numberOfAutoSaveSlots = 0;

        [Tooltip(
                "Starting number for auto save slots.\n" +
                "Example: an offset of 200 means slot 201 holds the latest auto save.\n" +
                "Pick an offset that does not overlap with regular or quick save ranges.")]
        public int autoSaveSlotOffset = 200;
			
        [Header("Cryptography Settings")]
                [Tooltip(
                        "Encrypt save blobs with AES-256-GCM before upload/local mirror. " +
                        "Supports all platforms. On WebGL and Linux encryption uses " +
                        "BouncyCastle which may increase CPU load during load/save operations.")]
                public bool enableEncryption = false;

                [Tooltip("Compress serialized save data before encryption using GZip. " +
                         "Reduces file size but increases CPU usage and slows load times " +
                         "when saving or loading.")]
                public bool enableCompression = false;

                // New: granular encryption controls (effective only when enableEncryption is true)
                [Tooltip("When ON and Encryption is enabled, also encrypt per-slot metadata payloads (PlayerPrefs, local files and cloud metadata).")]
                public bool encryptSlotMetadata = false;

                [Tooltip("When ON and Encryption is enabled, also encrypt screenshot bytes (local mirror and cloud uploads). Screenshots will be transparently decrypted at runtime for display.")]
                public bool encryptScreenshots = false;

                [Tooltip(
                        "When ON, derive the encryption key from master secret + user id (per-user isolation). " +
                        "When OFF, all users share the same derived key (saves become portable across installs, " +
                        "but if the master key leaks, all saves can be decrypted).")]
                public bool useUserIdForEncryption = true;
		
		[Tooltip("Where the master secret comes from at runtime (static asset or user passphrase). " +
		         "Server-fetched keys are deprecated; use Cloud Crypto Mode = ServerSide instead.")]
		public MasterSecretSource masterSecretSource = MasterSecretSource.StaticAsset;

		[Tooltip("Provider asset that supplies the 256-bit master secret " +
		         "used to derive per-user encryption keys (based on the selected Key Source).")]
		public ScriptableObject masterSecretProvider;

                [Tooltip("Where encryption/decryption happens for cloud saves. ServerSide uses a server crypto provider and avoids client-side keys.")]
		public CloudCryptoMode cloudCryptoMode = CloudCryptoMode.ClientSide;

		[Tooltip("Provider used when Cloud Crypto Mode is ServerSide (performs encrypt/decrypt on a server endpoint).")]
		public ScriptableObject cloudCryptoProvider;

                [Header("Sync Settings (Preview)")]
                [Tooltip("Enable SyncManager scaffolding (transport-agnostic).")]
                public bool enableSync = false;

                [Tooltip("Optional SyncSettings asset (transports, intervals).")]
                public SyncSettings syncSettings;

                [Header("Path Migration Settings")]
                [Tooltip("Run a one-time migration from the previous persistent path to the current custom path on startup.")]
                public bool runPersistentPathMigrationOnStartup = true;
                [Tooltip(
                        "When Crystal Save migrates a legacy slot, immediately write the upgraded data back " +
                        "over the original save. Prevents the project from repeating the migration on every load, " +
                        "but replaces the legacy file with the new format." )]
                public bool autoSaveMigratedData = false;

		[Header("Screenshot Settings")]
		[Tooltip("Enable or disable capturing in-game screenshots for each save slot.")]
		public bool enableScreenshots = true;

		[Tooltip("Folder name within persistentDataPath to store screenshots.")]
		public string screenshotFolderName = "SaveSlotScreenshots";

                [Tooltip("Format to use for saving screenshots.")]
                public ScreenshotFormat screenshotFormat = ScreenshotFormat.JPG;

#if NANINOVEL
                [Tooltip("Select which screenshot provider to use when Naninovel is available.")]
                public ScreenshotProvider screenshotProvider = ScreenshotProvider.Naninovel;
#endif

                [Header("Save Slot Metadata")]
                [Tooltip("Optional asset providing additional key/value pairs for each save slot.")]
                public SaveSlotMetadataSO defaultSlotMetadata;

                        [Tooltip(
                                "Local filename pattern for per-slot metadata saved on disk. " +
                                "Must contain {n} which will be replaced with the slot number.\n" +
                                "Default keeps backward compatibility: Slot{n}_Meta.bin")]
                        // Note: the pattern must include {n} to insert the slot number
                        public string metadataFileNamePattern = "Slot{n}_Meta.bin";

                [Header("Backup & Verification Settings")]
                [Tooltip(
                        "Keep a temporary *.bak* copy of the previous .sav next to the save " +
                        "file inside Application.persistentDataPath and verify the new save. " +
                        "The backup is deleted after verification succeeds. Backup files are " +
                        "stored only locally and are never uploaded to the cloud.")]
                public bool enableSaveFileVerification = false;

                [Header("Logging Settings")]
                public LogLevel logLevel = LogLevel.Error;

        [Tooltip("Automatically register tags when the project changes or after assembly reload.")]
        public bool autoRegisterTags = true;

        [Tooltip(
            "Addressables/Resources key used to load the TagRegistry asset at runtime.\n" +
            "Defaults to 'TagRegistry'. Override this if you move the asset and assign a custom Addressables entry.")]
        public string tagRegistryKey = "TagRegistry";

                [Tooltip("Automatically register prefab assets when the project changes or after assembly reload.")]
                public bool autoRegisterPrefabs = false;

        [Header("Performance")]
        [Tooltip("Skip runtime modification capture passes when all related tracking options are disabled.")]
        public bool optimizeRuntimeCapture = true;

        [Tooltip("Cache GameObjects for faster UniqueID lookups. Note: When enabled, objects that are inactive at design time may not restore properly on the first load if the cache is empty. The system now automatically ensures cache consistency during save/load operations to prevent this issue.")]
        public bool enableLookupCache = true;

        [Tooltip("Skip duplicate ID checks in OnValidate for scenes with 10,000+ UniqueID components. GUID collision probability is negligible (≈10⁻²⁸%), but disabling this only skips validation for cloned objects.")]
        public bool skipDuplicateIDCheck = false;

        [Tooltip("Cache saveable components for faster UniqueIdentifier lookups.")]
        public bool enableComponentLookupCache = true;

        [Tooltip("Scan for existing RememberGameObjects on startup. Disable to rely on each component's Awake for registration.")]
        public bool scanForExistingGameObjects = true;

        [Tooltip("Number of existing RememberGameObjects registered per frame during the startup scan. Set 0 or negative to process all in a single frame.")]
        public int existingObjectScanBatchSize = 0;

        [Tooltip("Number of prefabs instantiated per frame during load. Set 0 or negative to instantiate all in a single frame.")]
        public int prefabInstantiationBatchSize = 0;

        [Tooltip("When enabled, prefabs are instantiated grouped by scene to reduce SceneManager.SetActiveScene calls during load.")]
        public bool groupInstantiationByScene = false;

        [Tooltip("Number of components applied per frame during load. Set 0 or negative to apply all in a single frame.")]
        public int componentApplyBatchSize = 0;

        [Tooltip("Number of active states applied per frame during load. Set 0 or negative to apply all in a single frame.")]
        public int activeStateApplyBatchSize = 0;

        [Tooltip(
            "When disabled, Physics.SyncTransforms() runs after each Rigidbody restore.\n" +
            "Enable to call it once after all prefabs finish loading for better performance when many prefabs are restored.")]
        public bool syncTransformsAfterPrefabLoad = true;

        [Tooltip(
            "When a prefab restore has no parent info (ParentID/ParentStableKey/ParentPrefabAssetID), " +
            "still apply the parent during the post-load pass (this detaches to root).\n" +
            "Disable to keep the current parent unchanged.")]
        public bool applyParentWhenParentInfoMissing = true;

                [Tooltip(
                        "Allow asset-based reuse mapping during deferred processing.\n" +
                        "When OFF (default), deferred prefabs will not claim existing scene instances by PrefabAssetID.\n" +
                        "This prevents under-spawning caused by deferred entries reusing scene-baked objects instead of instantiating new ones.")]
                public bool enableAssetBasedReuseDuringDeferred = false;

        [Header("Disabled Component Registration")]
        [Tooltip(
            "When enabled, Crystal Save will scan for SaveableComponents on disabled GameObjects and register them.\n" +
            "This is required when GameObjects with talents, items, or other saveable data start disabled in the scene.\n" +
            "Disable this if all your saveable GameObjects are active at startup.")]
        public bool registerDisabledComponents = true;

        [Tooltip(
            "When to scan for disabled SaveableComponents:\n" +
            "• OnInitialization - Scan once at Crystal Save startup (faster, but misses additively loaded scenes)\n" +
            "• OnSceneLoad - Scan each time a scene loads (more thorough, slight performance cost per scene)")]
        public DisabledComponentScanMode disabledComponentScanMode = DisabledComponentScanMode.OnSceneLoad;

        [Tooltip(
            "When enabled, only scan for disabled components in the newly loaded scene.\n" +
            "When disabled, scan all loaded scenes (heavier but catches cross-scene references).")]
        public bool scanOnlyActiveScene = true;

        [Header("Prefab Pooling")]
        [Tooltip("When enabled, SaveablePrefabs are spawned and recycled via a pool instead of being instantiated and destroyed.")]
        public bool usePrefabPooling = false;

                [Tooltip("Default number of instances each prefab pool keeps warm when pooling is enabled.")]
                public int defaultPrefabPoolSize = 10;

                [Tooltip("Spawn pooled SaveablePrefabs in the active scene instead of DontDestroyOnLoad when pooling is enabled.")]
                public bool spawnPooledPrefabsInScene = false;

                [Tooltip("When > 0, spawning pooled prefabs will be batched per frame to improve performance. 0 = no batching (spawn all at once).")]
                public int pooledPrefabSpawnBatchSize = 0;

                [Tooltip("Enable to yield between batches when spawning pooled prefabs, spreading the work across multiple frames.")]
                public bool enablePooledPrefabBatching = false;

                [Header("Addressables Settings")]
                [Tooltip("Use Unity Addressables instead of Resources for loading assets.")]
                public bool useAddressables = false;

		[Tooltip("If true, the SaveSystem will use Unity Cloud Save instead of local saving methods. Requires Unit Cloud Service Package!")]
		public bool enableCloudSave = false;

        [Tooltip(
            "Where Crystal Save writes slot data:\n" +
            "• LocalFile → persistentDataPath / PlayerPrefs defined above in the dropwdown field Save Method\n" +
            "• Supabase  → uploads raw bytes to Supabase Storage\n" +
            "• Firebase (Beta) → uploads raw bytes to Firebase Cloud Storage")]
        public SaveBackend backend = SaveBackend.UnityCloudSave;

		// Supabase-specific
		[Tooltip("Your project URL, e.g. https://xxxx.supabase.co")]
		public string supabaseUrl;       // e.g. https://…supabase.co
		
		[Tooltip("Public *anon* API key.  NEVER paste the service (admin) key here; " +
			 "the anon key is safe to ship in builds.")]
		[TextArea(2, 5)]
		public string supabaseAnonKey;   // *never* store the service key here

		[Tooltip("Storage bucket that will hold the *.sav* files.  " +
             "Must already exist in Supabase → Storage.\n" +
             "Default: \"game-saves\"")]
		public string bucket = "game-saves";
		
                [Tooltip(
                        "How the path  users/{uid}/…  is chosen when using cloud backends.\n" +
                        "» Shared              all players write to users/guest\n" +
                        "» UnityAuthentication uses AuthenticationService.PlayerId\n" +
                        "» GuidPerDevice       one GUID per installation, stored in PlayerPrefs\n" +
                        "» PublicPerBuild      same as Shared, but you deploy each build\n" +
                        "                      to a separate bucket")]
                public UserFolderStrategy userFolderStrategy =
#if REMEMBERME_AUTHENTICATION_PRESENT
                        UserFolderStrategy.UnityAuthentication;
#else
    UserFolderStrategy.GuidPerDevice;
#endif

                [Tooltip(
            "ScriptableObject that returns the folder for UserFolderStrategy.Custom.\n" +
            "Used by SupabaseSaveSystem, FirebaseSaveSystem, and future cloud save systems.\n" +
            "Must implement IUserFolderResolver. Implement IUserAuthorizationResolver\n" +
            "as well if you want to inject a per-user JWT or other access key.")]
        [SerializeField]
        public ScriptableObject customUserFolderResolver;

                [Header("Firebase Settings")]
                [Tooltip("Firebase storage bucket name, e.g. my-app.appspot.com")]
                public string firebaseBucket = "game-saves";

                [Tooltip("Firebase ID token used for authenticated storage requests.")]
                public string firebaseIdToken;

                [Header("MySQL Settings")]
                [Tooltip("Base URL of the web API that mediates MySQL access for save data.")]
                public string mySqlApiUrl = "http://localhost/crystal-save-api.php";

                [Tooltip("Base URL of the web API used for MySQL authentication. Leave empty to reuse the MySQL API URL above.")]
                public string mySqlAuthApiUrl;

                [Tooltip("Optional API key sent as X-API-KEY header when calling the web API.")]
                public string mySqlApiKey;

                [Tooltip("Table used to store save data records on the server.")]
                public string tableName = "CrystalSaveData";

[Tooltip("Authentication method used when connecting to the MySQL backend.\n" +
"Choose 'UsernamePassword' to require explicit credentials. Call\n" +
"SaveManager.Instance.MySqlSignUpAsync(username, password) to create an account,\n" +
"SaveManager.Instance.MySqlSignInAsync(username, password) to log in, and\n" +
"SaveManager.Instance.MySqlSignOut() to log out.")]
public MySqlAuthMode mySqlLoginMode = MySqlAuthMode.Anonymous;

                [Tooltip("Binary uses CloudSaveService.Files; JsonBase64 uses Data.Player with Base64.")]
                public CloudSaveTransport cloudSaveTransport = CloudSaveTransport.Binary;

		[Tooltip("Which Unity Authentication sign-in flow should be used "
	   + "before Cloud Save becomes available.")]
		public AuthProvider defaultAuthProvider = AuthProvider.Anonymous;

                [Tooltip("Keep a local copy of every slot on disk even when Cloud Save is on. "
                + "Turn this OFF if you want 100 % cloud-only saves.")]
                public bool keepLocalMirror = true;

                [Tooltip("Store screenshots in Unity Cloud Save in addition to the save file.")]
                public bool cloudSaveScreenshots = true;

                [Tooltip("Store slot metadata in Unity Cloud Save in addition to the save file.")]
                public bool cloudSaveMetadata = true;

                [Tooltip("If true the SaveManager signs-in automatically on startup " +
                 "using the provider set in defaultAuthProvider.\n" +
                 "Turn it OFF when you want to trigger the sign-in yourself " +
                 "via code or a login UI.")]
                public bool autoCloudSignIn = false;

                [Header("Conflict Resolution")]
                [Tooltip("Automatically resolve save conflicts without showing a UI.")]
                public bool autoResolveConflicts = true;

                [Tooltip(
                        "Policy used when automatically resolving save conflicts.\n" +
                        "Latest: keep the newer file and discard the other.\n" +
                        "Oldest: keep the older file, losing newer progress.\n" +
                        "LocalWins: always keep the local copy and overwrite the cloud.\n" +
                        "CloudWins: always keep the cloud copy and overwrite local.\n" +
                        "Custom: evaluate Metadata Rules; unresolved conflicts fall back to the UI.")]
                public AutoConflictPolicy autoConflictPolicy = AutoConflictPolicy.Latest;

                [Tooltip("Rules evaluated when AutoConflictPolicy.Custom is selected. Supports up to two rules.")]
                public MetadataRule[] metadataRules = new MetadataRule[1];

[Tooltip("Canvas prefab used by LiveConflictResolver when displaying the conflict resolution UI. Optional; if left unassigned, a default overlay canvas will be created at runtime.")]
public Canvas conflictOverlayCanvas;

                [Header("Scene Object Registry")]
                [Tooltip("Controls which scenes are scanned when auto-populating the SceneObjectRegistry.")]
                public SceneObjectScanMode sceneObjectScanMode = SceneObjectScanMode.CurrentOpenedScene;

                [Tooltip("Used only when Scene Object Scan Mode is Custom. Leave empty to scan the currently opened scene.")]
                [SerializeField]
                public List<string> scenesToScan = new List<string>();

#if CRYSTALSAVE_TIMEMACHINE
        [Header("TimeMachine Settings")]
        [Tooltip("Maximum duration (in seconds) of TimeMachine recordings that will be saved to disk.\n" +
                 "Example: 30.0 = save last 30 seconds of recorded history.\n" +
                 "Set to 0 to disable TimeMachine persistence, or -1 to save entire timeline (warning: large save files!).\n" +
                 "Only snapshots within this duration from the latest snapshot will be persisted.")]
        public float timeMachineMaxSaveDuration = 30f;

        [Header("TimeMachine Recording Defaults")]
        [Tooltip("Default: Record snapshots at regular intervals instead of every frame.\n" +
                 "SaveableComponents can override this per-object.")]
        public bool timeMachineUseInterval = true;

        [Tooltip("Default: Interval between snapshots (seconds) when using interval-based recording.\n" +
                 "SaveableComponents can override this per-object.")]
        public float timeMachineSnapshotInterval = 0.1f;

        [Tooltip("Default: Maximum number of snapshots to keep per GameObject.\n" +
                 "SaveableComponents can override this per-object.")]
        public int timeMachineMaxSnapshots = 500;

        [Tooltip("Maximum memory budget (MB) for all TimeMachine snapshots across all objects.\n" +
                 "When exceeded, oldest snapshots are pruned automatically.\n" +
                 "Set to 0 for unlimited memory (warning: can cause performance issues!).\n" +
                 "Recommended: 100-500 MB for typical games, 1000+ MB for complex simulations.")]
        [Range(0f, 10000f)]
        public float timeMachineMaxMemoryBudgetMB = 100f;

        [Header("TimeMachine Configuration Preset")]
        [Tooltip("Pre-configured settings for common game mechanics.\n" +
                 "• Ghost Racing - Race against previous attempts (Mario Kart style)\n" +
                 "• Speedrunning - Compare multiple speedrun attempts\n" +
                 "• Puzzle Solver - Review attempts while trying new solutions (Braid style)\n" +
                 "• Branching Story - Butterfly Effect, Alternative story paths (Life is Strange style)\n" +
                 "• Linear Replay - Simple record/playback system\n" +
                 "• Training Mode - Show expert playback while practicing\n" +
                 "• Loop Debugger - Fast iteration for development\n" +
                 "• Custom - Manually configure all settings below")]
        public TimeMachinePresetType timeMachinePreset = TimeMachinePresetType.LinearReplay;

        [Header("Advanced Settings (Overridden by Preset)")]
        [Tooltip("Timeline Mode: How timestamps are recorded.\n" +
                 "• Continuous - Timeline continues from last point with no gaps when pausing/resuming recording (default).\n" +
                 "• Absolute - Timeline uses real time, pausing creates gaps.")]
        public bool useContinuousTimeByDefault = true;

        [Tooltip("Resume Mode: What happens when you resume recording after stopping playback mid-timeline.\n" +
                 "• Overwrite - Replaces future snapshots from the resume point (destructive).\n" +
                 "• Auto-Branch - Creates alternative timelines automatically (non-destructive).\n" +
                 "• Continue - Jumps to timeline end, continues recording from there.")]
        public PlaybackResumeMode defaultResumeMode = PlaybackResumeMode.ContinueFromEnd;

        [Tooltip("Auto-Branch Behavior: Controls what happens to Alternative branches when Auto-Branch mode is active.\n" +
                 "• Overwrite - Replaces Alternative branch from stop point (2 timelines max).\n" +
                 "• Continue - Jumps to Alternative's end, continues recording.\n" +
                 "• Unlimited - Creates numbered branches (Alt1, Alt2, Alt3...) infinitely.")]
        public AutoBranchBehavior defaultAutoBranchBehavior = AutoBranchBehavior.OverwriteCurrentBranch;

        [Tooltip("Branch Copy Mode: Controls what snapshot history gets copied when creating new Alt# branches.\n" +
                 "• Clean - Copy only source snapshots (from Original or parent branch).\n" +
                 "• Accumulative - Copy all ancestor snapshots (enables ghost racing).\n" +
                 "• Empty - Start with empty timeline (manual re-record required).")]
        public BranchCopyMode defaultBranchCopyMode = BranchCopyMode.FromOriginal;
        
        [Tooltip("Ghost Mode: Allow recording during playback (simultaneous record + playback).\n\n" +
                 "WHEN TO ENABLE (Useful For):\n" +
                 "✓ Ghost Racing - Record new attempt while racing against previous ghost (Mario Kart)\n" +
                 "✓ Speedrunning - Compare current run against best time in real-time\n" +
                 "✓ Training Mode - Record player while showing expert demonstration\n" +
                 "✓ Puzzle Solver - Record new solution while replaying previous attempt\n" +
                 "✓ Combo Training - Practice combos while watching tutorial playback\n\n" +
                 "WHEN TO DISABLE (Not Recommended):\n" +
                 "✗ Linear Replay - Simple record/playback without simultaneous operations\n" +
                 "✗ Branching Story - Exclusive choices (can't replay and record at same time)\n" +
                 "✗ Loop Debugger - Fast iteration without concurrent operations\n" +
                 "✗ Memory-Constrained Platforms - Doubles memory usage (two timelines active)\n\n" +
                 "CONFIGURATION PRESET COMPATIBILITY:\n" +
                 "• Ghost Racing ✓ REQUIRED - Core mechanic\n" +
                 "• Speedrunning ✓ REQUIRED - Compare runs in real-time\n" +
                 "• Training Mode ✓ REQUIRED - Learn while watching\n" +
                 "• Puzzle Solver ✓ OPTIONAL - Helpful for comparison\n" +
                 "• Branching Story ✗ NOT NEEDED - Mutually exclusive paths\n" +
                 "• Linear Replay ✗ NOT NEEDED - Simple playback only\n" +
                 "• Loop Debugger ✗ NOT NEEDED - Fast iteration focus\n\n" +
                 "TECHNICAL IMPACT:\n" +
                 "• Memory: ~2x usage (active recording + playback timelines)\n" +
                 "• CPU: Minimal impact (both operations are lightweight)\n" +
                 "• Complexity: Requires careful branch management\n\n" +
                 "WARNING: Only enable if your game mechanic specifically requires simultaneous record + playback!")]
        public bool allowRecordingDuringPlayback = false;
        
        [Header("Ghost Animation Settings")]
        [Tooltip("Configure animator parameters and behavior for ghost/clone animations.\n" +
                 "Only applies to presets that use Ghost Mode (Ghost Racing, Time Travel, Puzzle Solver, etc.)\n" +
                 "These settings are automatically applied to all ghosts/clones created at runtime.")]
        public GhostAnimationSettings ghostAnimationSettings = GhostAnimationSettings.CreateDefault();
        
        /// <summary>
        /// Applies the current preset configuration to the individual settings.
        /// Call this when preset changes to synchronize the advanced settings.
        /// </summary>
        public void ApplyPresetToSettings()
        {
            if (timeMachinePreset == TimeMachinePresetType.Custom)
                return; // Don't override custom settings
            
            var config = TimeMachinePresets.GetPresetConfig(timeMachinePreset);
            if (config != null)
            {
                useContinuousTimeByDefault = config.useContinuousTime;
                defaultResumeMode = config.resumeMode;
                defaultAutoBranchBehavior = config.autoBranchBehavior;
                defaultBranchCopyMode = config.branchCopyMode;
                allowRecordingDuringPlayback = config.allowRecordingDuringPlayback;
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
        }
#endif

        public IStoragePathProvider CreatePathProvider()
        {
            if (persistentPath.mode == PersistentPathMode.Custom)
                return new CustomStoragePathProvider(persistentPath.customFolderName, persistentPath.nonWebGLOutputRoot);
            return new DefaultStoragePathProvider();
        }
        }

        public enum ScreenshotFormat
        {
                PNG,
                JPG
        }

#if NANINOVEL
        public enum ScreenshotProvider
        {
                Naninovel,
                CrystalSave
        }
#endif

	public enum AuthProvider
	{
		Anonymous,
		Unity,
		UsernamePassword,
		GooglePlay,
		Apple,
		Facebook,
		Steam
	}
	public enum CloudSaveTransport
	{
		Binary,       // Uses Unity Cloud Save Files API (binary blobs)
		JSON, // Uses Data.Player API (Base64‐in‐JSON)
	}
	
        public enum SaveBackend
        {
                UnityCloudSave,   // Local disk or Unity Cloud Save when enabled
                Supabase,
                MySQL,
                [InspectorName("Firebase (Beta)")]
                Firebase
        }

        public enum MySqlAuthMode
        {
                Anonymous,
                UsernamePassword
        }
	
        public enum UserFolderStrategy
        {
                /// Every device gets the same hard-coded path “users/guest”
                Shared,

                /// Uses Unity Authentication → AuthenticationService.Instance.PlayerId
                UnityAuthentication,

                /// Generates a GUID on the first launch and persists it in PlayerPrefs
                GuidPerDevice,

                /// Same as Shared, but *you* promise to deploy every build
                /// to its own bucket so players can’t collide.
                PublicPerBuild,

                // Custom Solution
                Custom
        }

        public enum AutoConflictPolicy
        {
                Latest,
                Oldest,
                LocalWins,
                CloudWins,
                Custom
        }

        public enum ComparisonOp
        {
                Equals,
                Smaller,
                Larger
        }

        public enum MetadataRuleType
        {
                Metadata,
                Latest,
                Oldest,
                LocalWins,
                CloudWins
        }

        [Serializable]
        public struct MetadataRule
        {
                public MetadataRuleType type;
                public string key;
                public ComparisonOp op;
                public string value;
        }
}
#endif
