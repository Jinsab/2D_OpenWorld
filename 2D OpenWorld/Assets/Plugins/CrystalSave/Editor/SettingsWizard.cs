#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    public class SettingsWizard : EditorWindow
    {
    enum Page { ProjectInfo, SaveMethod, Encryption, Compression, Verification, Screenshots, Addressables, CloudBackend, Conflicts, Complete }

        Page currentPage = Page.ProjectInfo;

        string productName;
        SaveMethod saveMethod = SaveMethod.BinaryFileFormat;
        int slots = 5;
        bool enableEncryption;
        bool useUserIdForEncryption = true;
        bool enableCloud;
        SaveBackend backend = SaveBackend.UnityCloudSave;
    bool useAddressables;

    // Encryption key source and provider
    MasterSecretSource masterSecretSource = MasterSecretSource.StaticAsset;
    ScriptableObject masterSecretProvider;
    CloudCryptoMode cloudCryptoMode = CloudCryptoMode.ClientSide;
    ScriptableObject cloudCryptoProvider;

    // Unity Cloud Save
    AuthProvider defaultAuthProvider = AuthProvider.Anonymous;
    CloudSaveTransport cloudTransport = CloudSaveTransport.Binary;
    bool autoCloudSignIn = false;
    bool keepLocalMirror = true;
    bool cloudSaveScreenshots;
    bool cloudSaveMetadata;

    // Supabase
    string supabaseUrl = string.Empty;
    string supabaseAnonKey = string.Empty;
    string bucket = "game-saves";
    UserFolderStrategy userFolderStrategy = UserFolderStrategy.GuidPerDevice;

    // MySQL
    string mySqlApiUrl = "http://localhost/crystal-save-api.php";
    string mySqlAuthApiUrl = string.Empty;
    string mySqlApiKey = string.Empty;
    string tableName = "CrystalSaveData";
    MySqlAuthMode mySqlLoginMode = MySqlAuthMode.Anonymous;

    // Firebase (Beta)
    string firebaseBucket = "game-saves";
    string firebaseIdToken = string.Empty;

    // UI state
    bool showResources = true;
    Vector2 scrollPosition = Vector2.zero;
    Texture2D coverImage;
    const string coverImagePath = "Assets/Plugins/CrystalSave/Editor/Logo/CrystalSaveCoverWizard.png";
    const string slideshowFolderPath = "Assets/Plugins/CrystalSave/Editor/Wizard";
    readonly Page[] slideshowPages = new[]
    {
        Page.SaveMethod,
        Page.Encryption,
        Page.Compression,
        Page.Verification,
        Page.Screenshots,
        Page.Addressables,
        Page.CloudBackend,
        Page.Conflicts
    };
    Dictionary<Page, Texture2D> pageBanner = new Dictionary<Page, Texture2D>();

        [MenuItem("Tools/Crystal Save/Settings/Settings Wizard")]
        public static void OpenWizard()
        {
            var window = GetWindow<SettingsWizard>(true, "Crystal Save Wizard");
            // Fix the window to a size similar to the design in the screenshot
            // Slightly taller so non-Unity cloud backend pages are fully visible
            var fixedSize = new Vector2(974f, 860f);
            window.minSize = fixedSize;
            window.maxSize = new Vector2(fixedSize.x, 2000f);
            // Center on the main editor window
            window.position = GetCenteredRectOnMainEditor(fixedSize);
        }

        static Rect GetCenteredRectOnMainEditor(Vector2 size)
        {
            Rect main = GetMainEditorWindowPosition();
            float x = main.x + (main.width - size.x) / 2f;
            float y = main.y + (main.height - size.y) / 2f;
            return new Rect(x, y, size.x, size.y);
        }

        // Uses internal UnityEditor.ContainerWindow to locate the main editor window bounds
        static Rect GetMainEditorWindowPosition()
        {
            var containerWinType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ContainerWindow");
            if (containerWinType == null)
                return new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);
            var showModeField = containerWinType.GetField("m_ShowMode", BindingFlags.NonPublic | BindingFlags.Instance);
            var positionProperty = containerWinType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
            if (showModeField == null || positionProperty == null)
                return new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);
            var windows = Resources.FindObjectsOfTypeAll(containerWinType);
            foreach (var win in windows)
            {
                var showModeObj = showModeField.GetValue(win);
                if (showModeObj == null) continue;
                // 4 == main editor window
                if ((int)showModeObj == 4)
                {
                    var pos = (Rect)positionProperty.GetValue(win, null);
                    return pos;
                }
            }
            return new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);
        }

        void OnEnable()
        {
            productName = PlayerSettings.productName;
            // Load cover image for the wizard welcome page
            coverImage = AssetDatabase.LoadAssetAtPath<Texture2D>(coverImagePath);
            // Ensure texture import settings preserve aspect (avoid NPOT upscaling to square) and suit GUI usage
            if (coverImage == null)
                AssetDatabase.ImportAsset(coverImagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(coverImagePath) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.GUI) { importer.textureType = TextureImporterType.GUI; changed = true; }
                if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
                // Keep sRGB on for UI colors; no heavy compression for crisp UI
                if (!importer.sRGBTexture) { importer.sRGBTexture = true; changed = true; }
                if (changed)
                {
                    importer.SaveAndReimport();
                    coverImage = AssetDatabase.LoadAssetAtPath<Texture2D>(coverImagePath);
                }
            }

            // Try to preload existing SaveSettings to initialize fields
            const string saveSettingsPath = "Assets/Plugins/CrystalSave/Resources/SaveSettings.asset";
            var saveSettings = AssetDatabase.LoadAssetAtPath<SaveSettings>(saveSettingsPath);
            if (saveSettings != null)
            {
                saveMethod = saveSettings.saveMethod;
                slots = saveSettings.numberOfSaveSlots;
                enableEncryption = saveSettings.enableEncryption;
                useUserIdForEncryption = saveSettings.useUserIdForEncryption;
                masterSecretSource = saveSettings.masterSecretSource;
                masterSecretProvider = saveSettings.masterSecretProvider;
                cloudCryptoMode = saveSettings.cloudCryptoMode;
                cloudCryptoProvider = saveSettings.cloudCryptoProvider;
                // New pages
                enableCompression = saveSettings.enableCompression;
                enableVerification = saveSettings.enableSaveFileVerification;
                screenshotsEnabled = saveSettings.enableScreenshots;
                autoResolveConflicts = saveSettings.autoResolveConflicts;
                screenshotFormat = saveSettings.screenshotFormat;
                conflictPolicy = saveSettings.autoConflictPolicy;
                enableCloud = saveSettings.enableCloudSave;
                backend = saveSettings.backend;
                useAddressables = saveSettings.useAddressables;

                if (!IsAddressablesAvailable())
                    useAddressables = false;

                // Cloud specifics
                defaultAuthProvider = saveSettings.defaultAuthProvider;
                cloudTransport = saveSettings.cloudSaveTransport;
                autoCloudSignIn = saveSettings.autoCloudSignIn;
                keepLocalMirror = saveSettings.keepLocalMirror;
                cloudSaveScreenshots = saveSettings.cloudSaveScreenshots;
                cloudSaveMetadata = saveSettings.cloudSaveMetadata;
                
                // Supabase
                supabaseUrl = saveSettings.supabaseUrl;
                supabaseAnonKey = saveSettings.supabaseAnonKey;
                bucket = saveSettings.bucket;
                userFolderStrategy = saveSettings.userFolderStrategy;

                // MySQL
                mySqlApiUrl = saveSettings.mySqlApiUrl;
                mySqlAuthApiUrl = saveSettings.mySqlAuthApiUrl;
                mySqlApiKey = saveSettings.mySqlApiKey;
                tableName = saveSettings.tableName;
                mySqlLoginMode = saveSettings.mySqlLoginMode;

                // Firebase
                firebaseBucket = saveSettings.firebaseBucket;
                firebaseIdToken = saveSettings.firebaseIdToken;
            }

            // Prepare slideshow images: load, shuffle, and assign distinct textures per eligible page
            pageBanner.Clear();
            if (AssetDatabase.IsValidFolder(slideshowFolderPath))
            {
                // Find PNG textures in the folder
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { slideshowFolderPath });
                var textures = new List<Texture2D>();
                foreach (var guid in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    bool changed = false;
                    if (ti != null)
                    {
                        if (ti.textureType != TextureImporterType.GUI) { ti.textureType = TextureImporterType.GUI; changed = true; }
                        if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; changed = true; }
                        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
                        if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                        if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
                        if (!ti.sRGBTexture) { ti.sRGBTexture = true; changed = true; }
                        if (changed) ti.SaveAndReimport();
                    }
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (tex != null) textures.Add(tex);
                }
                // Shuffle
                var rng = new System.Random(Environment.TickCount);
                for (int i = textures.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (textures[i], textures[j]) = (textures[j], textures[i]);
                }
                // Assign unique textures to pages without repeats
                int assignCount = Mathf.Min(textures.Count, slideshowPages.Length);
                for (int i = 0; i < assignCount; i++)
                {
                    pageBanner[slideshowPages[i]] = textures[i];
                }
            }
        }

    void OnGUI()
        {
            EditorGUILayout.Space();
            DrawStepHeader();
            // Draw slideshow banner for current page (except ProjectInfo & Complete where we have special content)
            DrawSlideshowBannerForCurrentPage();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            switch (currentPage)
            {
                case Page.ProjectInfo:
                    DrawProjectInfo();
                    break;
                case Page.SaveMethod:
                    DrawSaveMethod();
                    break;
                case Page.Encryption:
                    DrawEncryption();
                    break;
                case Page.Compression:
                    DrawCompression();
                    break;
                case Page.Verification:
                    DrawVerification();
                    break;
                case Page.Screenshots:
                    DrawScreenshots();
                    break;
                case Page.Addressables:
                    DrawAddressables();
                    break;
                case Page.CloudBackend:
                    DrawCloudBackend();
                    break;
                case Page.Conflicts:
                    DrawConflicts();
                    break;
                case Page.Complete:
                    DrawComplete();
                    break;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
            DrawNavigation();
        }

        int GetStepIndex(Page p)
        {
            switch (p)
            {
                case Page.ProjectInfo: return 1;
                case Page.SaveMethod:  return 2;
                case Page.Encryption:  return 3;
                case Page.Compression: return 4;
                case Page.Verification:return 5;
                case Page.Screenshots: return 6;
                case Page.Addressables:return 7;
                case Page.CloudBackend:return 8;
                case Page.Conflicts:   return 9;
                case Page.Complete:    return 9; // final step is 9 of 9
                default: return 1;
            }
        }

        void DrawStepHeader()
        {
            const int totalSteps = 9;
            int step = GetStepIndex(currentPage);
            var rect = GUILayoutUtility.GetRect(10, 6);
            if (currentPage == Page.Complete)
            {
                // Show completion state with a full bar
                EditorGUILayout.LabelField("Setup Complete", EditorStyles.miniBoldLabel);
                EditorGUI.ProgressBar(new Rect(rect.x, rect.y, EditorGUIUtility.currentViewWidth - 24, 6), 1f, string.Empty);
            }
            else
            {
                EditorGUILayout.LabelField($"Step {step} of {totalSteps}", EditorStyles.miniBoldLabel);
                float progress = Mathf.Clamp01(step / (float)totalSteps);
                EditorGUI.ProgressBar(new Rect(rect.x, rect.y, EditorGUIUtility.currentViewWidth - 24, 6), progress, string.Empty);
            }
            GUILayout.Space(6);
        }
        // ─────────────────────────────────────────────────────────────
        // New Steps
        // ─────────────────────────────────────────────────────────────
        bool enableCompression;
        bool enableVerification;
        bool screenshotsEnabled;
        bool autoResolveConflicts;
    ScreenshotFormat screenshotFormat = ScreenshotFormat.JPG;
    AutoConflictPolicy conflictPolicy = AutoConflictPolicy.Latest;

        void DrawCompression()
        {
            EditorGUILayout.LabelField("Compression", EditorStyles.boldLabel);
            enableCompression = EditorGUILayout.Toggle(
                new GUIContent("Enable Compression", "Compress serialized data (GZip) before encryption to reduce file size."),
                enableCompression);
            EditorGUILayout.HelpBox(
                "Purpose: Shrinks save data (GZip) before encryption.\n" +
                "Upside: Smaller files → faster uploads and lower storage costs.\n" +
                "Downside: Extra CPU when saving/loading; negligible for tiny saves, noticeable for very large ones.",
                MessageType.Info);
        }

        void DrawVerification()
        {
            EditorGUILayout.LabelField("Save File Verification", EditorStyles.boldLabel);
            enableVerification = EditorGUILayout.Toggle(
                new GUIContent("Enable Save File Verification", "Keep a .bak of the previous save and verify new saves before replacing."),
                enableVerification);
            EditorGUILayout.HelpBox(
                "Purpose: Validate new saves and keep a .bak for safety.\n" +
                "Upside: Prevents corruption from partial writes or crashes.\n" +
                "Downside: Slightly more disk I/O (extra write/read per save).",
                MessageType.Info);
        }

        void DrawScreenshots()
        {
            EditorGUILayout.LabelField("Screenshots", EditorStyles.boldLabel);
            screenshotsEnabled = EditorGUILayout.Toggle(
                new GUIContent("Enable Screenshots", "Capture a screenshot per slot for UI previews."),
                screenshotsEnabled);
            using (new EditorGUI.DisabledScope(!screenshotsEnabled))
            {
                screenshotFormat = (ScreenshotFormat)EditorGUILayout.EnumPopup(
                    new GUIContent("Screenshot Format", "Image format used when storing screenshots. PNG is lossless and larger; JPG is smaller with compression artifacts."),
                    screenshotFormat);
            }
            EditorGUILayout.HelpBox(
                "Purpose: Show visual previews in your save UI.\n" +
                "Upside: Better UX; easy slot recognition.\n" +
                "Downside: Extra storage and upload size (PNG > JPG). Consider scaling resolution to improve performance.",
                MessageType.Info);
        }

        void DrawConflicts()
        {
            EditorGUILayout.LabelField("Conflict Resolution", EditorStyles.boldLabel);
            autoResolveConflicts = EditorGUILayout.Toggle(
                new GUIContent("Auto Resolve Conflicts", "Resolve cloud/local conflicts automatically using the configured policy."),
                autoResolveConflicts);
            using (new EditorGUI.DisabledScope(!autoResolveConflicts))
            {
                conflictPolicy = (AutoConflictPolicy)EditorGUILayout.EnumPopup(
                    new GUIContent("Conflict Policy", "Policy used when auto-resolving conflicts: Latest, Oldest, LocalWins, CloudWins, or Custom (uses metadata rules)."),
                    conflictPolicy);
            }
            EditorGUILayout.HelpBox(
                "Purpose: Decide which save wins without prompting the player.\n" +
                "Tip: 'Latest' fits most cases; 'Custom' enables metadata rules.\n" +
                "Note: Live Conflict Resolver only makes sense when Cloud Save is ON and Local Mirror is ON—otherwise there is no local/cloud divergence to resolve.",
                MessageType.Info);
        }

        void DrawProjectInfo()
        {
            EditorGUILayout.LabelField("Welcome to Crystal Save Setup Wizard", EditorStyles.boldLabel);
            // Draw cover image if available, scaled to fit the view width while preserving aspect ratio
            if (coverImage != null)
            {
                // Preserve the image aspect ratio and center horizontally.
                // Never upscale beyond the source width; scale down to fit the view.
                float viewWidth = Mathf.Max(10f, EditorGUIUtility.currentViewWidth - 24f); // account for padding
                float maxWidth = Mathf.Max(1f, coverImage.width);
                float targetWidth = Mathf.Min(viewWidth, maxWidth);
                float aspect = (float)coverImage.width / Mathf.Max(1, coverImage.height); // w/h
                float targetHeight = targetWidth / Mathf.Max(0.001f, aspect);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    Rect r = GUILayoutUtility.GetRect(targetWidth, targetHeight,
                        GUILayout.Width(targetWidth), GUILayout.Height(targetHeight),
                        GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                    // Draw maintaining aspect inside the exact rect (rect is already computed for correct aspect)
                    GUI.DrawTexture(r, coverImage, ScaleMode.ScaleToFit);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(6);
            }
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Product Name", productName);
            }
            EditorGUILayout.HelpBox(
                "This wizard helps you set up a working Crystal Save configuration for your project quickly.\n" +
                "You can fine‑tune advanced options later in the Crystal Save Settings window.",
                MessageType.Info);

            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Start Setup", GUILayout.Height(28), GUILayout.MaxWidth(200)))
                {
                    currentPage = Page.SaveMethod;
                }
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);
            showResources = EditorGUILayout.Foldout(showResources, "Resources & Support", true);
            if (showResources)
            {
                EditorGUILayout.HelpBox(
                    "Documentation: https://arawn-software-publishing.gitbook.io/arawn/basics/editor\n" +
                    "Discord: https://discord.gg/MPhMKtSMUZ\n" +
                    "Email: mail@arawn.digital",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Documentation", EditorStyles.miniButton))
                        Application.OpenURL("https://arawn-software-publishing.gitbook.io/arawn/basics/editor");
                    if (GUILayout.Button("Join Discord", EditorStyles.miniButton))
                        Application.OpenURL("https://discord.gg/MPhMKtSMUZ");
                    if (GUILayout.Button("Email Support", EditorStyles.miniButton))
                        Application.OpenURL("mailto:mail@arawn.digital");
                }
            }
        }

        void DrawSlideshowBannerForCurrentPage()
        {
            if (currentPage == Page.ProjectInfo || currentPage == Page.Complete) return;
            if (!pageBanner.TryGetValue(currentPage, out var tex) || tex == null) return;
            // Clarify that the banner is a feature showcase unrelated to the current form fields
            EditorGUILayout.LabelField("Feature Spotlight", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("This image showcases one of the many features of Crystal Save.", EditorStyles.miniLabel);
            float viewWidth = Mathf.Max(10f, EditorGUIUtility.currentViewWidth - 24f);
            float maxWidth = Mathf.Max(1f, tex.width);
            float targetWidth = Mathf.Min(viewWidth, maxWidth);
            float aspect = (float)tex.width / Mathf.Max(1, tex.height);
            float targetHeight = targetWidth / Mathf.Max(0.001f, aspect);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                Rect r = GUILayoutUtility.GetRect(targetWidth, targetHeight,
                    GUILayout.Width(targetWidth), GUILayout.Height(targetHeight),
                    GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(6);
        }

        void DrawSaveMethod()
        {
            EditorGUILayout.LabelField("Save Method & Slots", EditorStyles.boldLabel);
            var gcSaveMethod = new GUIContent(
                "Save Method",
                "Choose where local saves are written: PlayerPrefs (good for WebGL idbfs) or Binary File (*.sav). " +
                "Crystal Save supports Player Prefs and Binary for local storage. JSON is available as a Cloud Transport option only.");
            saveMethod = (SaveMethod)EditorGUILayout.EnumPopup(gcSaveMethod, saveMethod);
            slots = EditorGUILayout.IntField("Number of Save Slots", slots);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Purpose: Choose how local saves are written.\n" +
                "PlayerPrefs → simple and robust (best for WebGL idbfs).\n" +
                "Binary File → fastest for large saves on desktop/console.\n" +
                "Note: JSON is only a Cloud transport option, not a local method.",
                MessageType.Info);
        }

        void DrawEncryption()
        {
            const int LegacyServerIndex = 1; // MasterSecretSource.Server (deprecated)
            EditorGUILayout.LabelField("Encryption", EditorStyles.boldLabel);
            var gcEnableEnc = new GUIContent(
                "Enable Encryption",
                "Encrypt save blobs with AES-256-GCM. Works on all platforms, including WebGL (uses Bouncy Castle).");
            enableEncryption = EditorGUILayout.Toggle(gcEnableEnc, enableEncryption);

            EditorGUILayout.HelpBox(
                "Purpose: Protect saves against tampering (AES‑256‑GCM).\n" +
                "Upside: Strong security for local and cloud saves.\n" +
                "Downside: Small CPU overhead (more noticeable on WebGL). Keep the master key safe.",
                MessageType.Info);

            if (enableEncryption)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Cloud Crypto", EditorStyles.boldLabel);
                cloudCryptoMode = (CloudCryptoMode)EditorGUILayout.EnumPopup(
                    new GUIContent(
                        "Cloud Crypto Mode",
                        "ClientSide encrypts/decrypts on the client. ServerSide uses a server crypto provider and keeps the key off the client."),
                    cloudCryptoMode);

                if (cloudCryptoMode == CloudCryptoMode.ServerSide)
                {
                    cloudCryptoProvider = (ScriptableObject)EditorGUILayout.ObjectField(
                        new GUIContent("Cloud Crypto Provider"),
                        cloudCryptoProvider,
                        typeof(ServerSideCryptoProvider),
                        false);

                    if (cloudCryptoProvider == null)
                    {
                        EditorGUILayout.HelpBox(
                            "Server-side crypto provider missing.\n\nCreate one via  Create ▸ Crystal Save ▸ Settings ▸ Security ▸ " +
                            "Server-Side Crypto Provider  and drag it here.",
                            MessageType.Warning);
                    }

                    EditorGUILayout.HelpBox(
                        "Server-side crypto requires a custom backend that performs encrypt/decrypt. " +
                        "The client will never receive the master key. This works for both local and cloud saves.",
                        MessageType.Info);
                }

                EditorGUILayout.Space(4);
                bool serverSideCloudCrypto = cloudCryptoMode == CloudCryptoMode.ServerSide;

                if (!serverSideCloudCrypto)
                {
                    if ((int)masterSecretSource == LegacyServerIndex)
                    {
                        EditorGUILayout.HelpBox(
                            "Legacy key source detected: Server-fetched client-side keys are deprecated. " +
                            "Use Cloud Crypto Mode = ServerSide if you want the key off the client.",
                            MessageType.Warning);
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Switch to Static Asset"))
                            masterSecretSource = MasterSecretSource.StaticAsset;
                        if (GUILayout.Button("Switch to Passphrase"))
                            masterSecretSource = MasterSecretSource.UserPassphrase;
                        EditorGUILayout.EndHorizontal();
                    }

                    if ((int)masterSecretSource != LegacyServerIndex)
                    {
                        string[] keyOptions = { "Static Asset", "User Passphrase" };
                        int currentIndex = masterSecretSource == MasterSecretSource.UserPassphrase ? 1 : 0;
                        int newIndex = EditorGUILayout.Popup(
                            new GUIContent("Key Source", "Where the master secret comes from at runtime (Static Asset or User Passphrase)."),
                            currentIndex,
                            keyOptions);
                        if (newIndex != currentIndex)
                            masterSecretSource = newIndex == 1 ? MasterSecretSource.UserPassphrase : MasterSecretSource.StaticAsset;

                        GUIContent providerLabel;
                        Type providerType;
                        if (masterSecretSource == MasterSecretSource.UserPassphrase)
                        {
                            providerLabel = new GUIContent("Passphrase Provider");
                            providerType = typeof(PassphraseMasterSecretProvider);
                        }
                        else
                        {
                            providerLabel = new GUIContent("Static Master Secret");
                            providerType = typeof(StaticMasterSecret);
                        }

                        masterSecretProvider = (ScriptableObject)EditorGUILayout.ObjectField(
                            providerLabel,
                            masterSecretProvider,
                            providerType,
                            false);

                        if (masterSecretProvider == null)
                        {
                            string helpText = masterSecretSource == MasterSecretSource.UserPassphrase
                                ? "Passphrase provider missing.\n\nCreate one via  Create ▸ Crystal Save ▸ Settings ▸ Security ▸ User Passphrase Provider  and drag it here."
                                : "Encryption key missing.\n\nCreate one via  Create ▸ Crystal Save ▸ Settings ▸ Security ▸ Static Master Secret  and drag it here.";

                            EditorGUILayout.HelpBox(
                                helpText + " If the key changes or is lost, players can’t open existing saves.",
                                MessageType.Warning);
                        }

                        if (masterSecretSource == MasterSecretSource.UserPassphrase)
                        {
                            EditorGUILayout.HelpBox(
                                "Passphrase mode: the master secret is derived from a user-provided passphrase at runtime. " +
                                "Prompt for the passphrase before Crystal Save initializes. Losing the passphrase means saves are lost.",
                                MessageType.Info);
                            EditorGUILayout.HelpBox(
                                "Note: the Salt Base64 field is not the passphrase. The passphrase must be provided at runtime (e.g., login/lock screen).",
                                MessageType.Info);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(
                                "Static key mode: the master secret asset must be included in the build to decrypt local saves. " +
                                "Keep it private and avoid changing it after release.",
                                MessageType.Info);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Cloud Crypto Mode is set to ServerSide. Key Source and local master secret providers are ignored.",
                        MessageType.Info);
                }

                useUserIdForEncryption = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Use User ID For Encryption",
                        "When ON, derive a per-user key (master secret + user id). When OFF, all users share the same derived key."),
                    useUserIdForEncryption);

                if (useUserIdForEncryption)
                {
                    EditorGUILayout.HelpBox(
                        "Encryption derives a per-user key from master secret + user id. If Unity Authentication is installed and the player signs in, " +
                        "the user id is their PlayerId; otherwise a per-install GUID (PlayerPrefs) is used. Encrypted saves will not transfer across " +
                        "different user ids (e.g., Editor vs build or after reinstall) unless you keep the same user id. " +
                        "Note: the per-install GUID is not stable and can change if you rename your project/company/product, delete PlayerPrefs, " +
                        "or change Persistent Path Mode (save location changes can make saves appear missing).",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "User ID is disabled for encryption: all users share the same derived key. " +
                        "This makes saves portable across installs and Editor/build, but it reduces isolation. " +
                        "If the master key is leaked, all saves can be decrypted. Not recommended for shared cloud environments.",
                        MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
        }

        void DrawCloudBackend()
        {
            EditorGUILayout.LabelField("Cloud Save Backends", EditorStyles.boldLabel);
            var gcEnableCloud = new GUIContent(
                "Enable Cloud Save",
                "Enable uploading and fetching save slots from a cloud backend. Local mirror can still be kept.");
            enableCloud = EditorGUILayout.Toggle(gcEnableCloud, enableCloud);

            var gcKeepLocalMirror = new GUIContent(
                "Keep Local Mirror",
                "Stores the .sav file, metadata, and screenshots locally even when using a cloud backend.");
            keepLocalMirror = EditorGUILayout.Toggle(gcKeepLocalMirror, keepLocalMirror);
            if (!keepLocalMirror)
            {
                EditorGUILayout.HelpBox(
                    "We strongly recommend keeping the local mirror enabled. It allows instant slot refreshes, offline saving, " +
                    "and drastically cuts the amount of data you have to upload/download.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Tip: With the local mirror enabled you can keep saving without a cloud connection and still sync later. " +
                    "It also reduces bandwidth because only the save file needs to be uploaded.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!enableCloud))
            {
                var gcBackend = new GUIContent(
                    "Backend",
                    "Choose a cloud save provider. Firebase is currently in Beta.");
                backend = (SaveBackend)EditorGUILayout.EnumPopup(gcBackend, backend);

                bool disableCloudScreenshots = !screenshotsEnabled;
                if (disableCloudScreenshots)
                    cloudSaveScreenshots = false;

                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(disableCloudScreenshots);
                cloudSaveScreenshots = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Upload Screenshots",
                        "Uploads captured screenshots to the cloud. Only enable for workflows that require them remotely."),
                    cloudSaveScreenshots);
                EditorGUI.EndDisabledGroup();
                cloudSaveMetadata = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Upload Slot Metadata",
                        "Uploads slot metadata JSON to the cloud. Recommended to keep disabled unless absolutely necessary."),
                    cloudSaveMetadata);
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    "Leave metadata and screenshot uploads OFF unless you have a specific remote UI that needs them. They " +
                    "increase storage usage and slow down uploads.",
                    MessageType.Info);

                EditorGUILayout.Space(6);
                switch (backend)
                {
                    case SaveBackend.UnityCloudSave:
                        EditorGUILayout.LabelField("Unity Cloud Save", EditorStyles.boldLabel);
                        defaultAuthProvider = (AuthProvider)EditorGUILayout.EnumPopup(
                            new GUIContent("Default Auth Provider", "Sign-in flow to use prior to Cloud Save."),
                            defaultAuthProvider);
                        cloudTransport = (CloudSaveTransport)EditorGUILayout.EnumPopup(
                            new GUIContent("Cloud Save Transport", "Binary (Files API) or JSON (Data.Player Base64)."),
                            cloudTransport);
                        autoCloudSignIn = EditorGUILayout.Toggle(
                            new GUIContent("Auto-Login Unity Cloud Save", "Automatically sign-in on startup using the selected provider."),
                            autoCloudSignIn);
                        EditorGUILayout.HelpBox(
                            "Purpose: Store saves in Unity Cloud Save.\n" +
                            "Binary → smaller/faster; JSON → easier inspection via Data.Player (Base64).\n" +
                            "Tip: Auto‑login is convenient for single‑player. Disable if you have a custom login UI.",
                            MessageType.Info);
                        break;

                    case SaveBackend.Supabase:
                        EditorGUILayout.LabelField("Supabase", EditorStyles.boldLabel);
                        supabaseUrl = EditorGUILayout.TextField(
                            new GUIContent("Supabase URL", "Project URL, e.g. https://xxxx.supabase.co"),
                            supabaseUrl);
                        supabaseAnonKey = EditorGUILayout.TextField(
                            new GUIContent("Anon Key", "Public anon key (do not use service/admin key)."),
                            supabaseAnonKey);
                        bucket = EditorGUILayout.TextField(
                            new GUIContent("Bucket Name", "Storage bucket to store saves."),
                            bucket);
                        userFolderStrategy = (UserFolderStrategy)EditorGUILayout.EnumPopup(
                            new GUIContent("User-Folder Strategy", "How user folder is determined (UID/GUID/etc.)."),
                            userFolderStrategy);
                        EditorGUILayout.HelpBox("HTTPS is strongly recommended for production builds, especially on WebGL. HTTP may fail or be blocked.", MessageType.Warning);
                        EditorGUILayout.HelpBox(
                            "Purpose: Save to Supabase Storage.\n" +
                            "Pros: Simple object storage with CDN. Cons: You manage auth and buckets.\n" +
                            "Tip: Use per‑user folders; never ship the service (admin) key.",
                            MessageType.Info);
                        break;

                    case SaveBackend.MySQL:
                        EditorGUILayout.LabelField("MySQL", EditorStyles.boldLabel);
                        mySqlApiUrl = EditorGUILayout.TextField(
                            new GUIContent("Web API URL", "Base URL of your MySQL mediator API."),
                            mySqlApiUrl);
                        mySqlApiKey = EditorGUILayout.TextField(
                            new GUIContent("API Key", "Optional API key sent as X-API-KEY header."),
                            mySqlApiKey);
                        tableName = EditorGUILayout.TextField(
                            new GUIContent("Table Name", "Table where saves are stored."),
                            tableName);
                        mySqlLoginMode = (MySqlAuthMode)EditorGUILayout.EnumPopup(
                            new GUIContent("Login Mode", "Anonymous or Username/Password."),
                            mySqlLoginMode);
                        if (mySqlLoginMode == MySqlAuthMode.UsernamePassword)
                        {
                            mySqlAuthApiUrl = EditorGUILayout.TextField(
                                new GUIContent("Login API URL", "Auth endpoint for username/password."),
                                mySqlAuthApiUrl);
                        }
                        EditorGUILayout.HelpBox("HTTPS is required for secure and reliable communication in builds. HTTP is not recommended and may fail on WebGL.", MessageType.Warning);
                        EditorGUILayout.HelpBox(
                            "Purpose: Save through your own web API to MySQL.\n" +
                            "Pros: Full control. Cons: You host, scale, and secure it.\n" +
                            "Tip: Validate payload sizes and throttle requests to protect the DB.",
                            MessageType.Info);
                        break;

                    case SaveBackend.Firebase:
                        EditorGUILayout.LabelField("Firebase (Beta)", EditorStyles.boldLabel);
                        EditorGUILayout.HelpBox("Firebase integration is in Beta.", MessageType.Info);
                        firebaseBucket = EditorGUILayout.TextField(
                            new GUIContent("Storage Bucket", "e.g., my-app.appspot.com"),
                            firebaseBucket);
                        firebaseIdToken = EditorGUILayout.TextField(
                            new GUIContent("ID Token", "Firebase ID token for authenticated requests."),
                            firebaseIdToken);
                        userFolderStrategy = (UserFolderStrategy)EditorGUILayout.EnumPopup(
                            new GUIContent("User-Folder Strategy", "How user folder is determined."),
                            userFolderStrategy);
                        EditorGUILayout.HelpBox("Use HTTPS endpoints for stability and security, especially on WebGL.", MessageType.Warning);
                        EditorGUILayout.HelpBox(
                            "Purpose: Save to Firebase Cloud Storage (via GCS).\n" +
                            "Pros: Scales automatically. Cons: Handle token refresh; watch upload sizes.\n" +
                            "Tip: Keep screenshots modest to reduce latency and costs.",
                            MessageType.Info);
                        break;
                }
            }
        }

        static bool IsAddressablesAvailable()
        {
            return Type.GetType("UnityEngine.AddressableAssets.Addressables, Unity.Addressables") != null;
        }

        void DrawAddressables()
        {
            EditorGUILayout.LabelField("Unity Addressables", EditorStyles.boldLabel);

            bool addressablesPresent = IsAddressablesAvailable();

            var toggleContent = new GUIContent(
                "Use Addressables",
                "Switches Crystal Save to load assets via Unity Addressables instead of the Resources folder. Addressables support remote content catalogs, dependency management, and asynchronous loading.");

            using (new EditorGUI.DisabledScope(!addressablesPresent))
            {
                useAddressables = EditorGUILayout.Toggle(toggleContent, useAddressables);
            }

            if (!addressablesPresent)
            {
                if (useAddressables)
                    useAddressables = false;

                EditorGUILayout.HelpBox(
                    "Install the Unity Addressables package to enable this option. The toggle stays disabled until the editor detects the Addressables assemblies.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Why Addressables?\n" +
                "• Decouple content from the Resources folder so large projects stay organized.\n" +
                "• Stream and update content via catalogs without shipping new builds.\n" +
                "• Async loading reduces frame spikes when prefabs or textures are fetched.\n\n" +
                "Trade-offs:\n" +
                "• Requires setting up Addressable groups and building catalogs.\n" +
                "• Slightly higher initial setup complexity compared to dropping assets into Resources.",
                MessageType.Info);
        }

        void DrawComplete()
        {
            EditorGUILayout.LabelField("Setup Complete", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assets were created or updated.", MessageType.Info);

            if (GUILayout.Button("Open Crystal Save Settings"))
            {
                Type windowType = Type.GetType("Arawn.CrystalSave.Editor.RememberMeSettingsWindow");
                if (windowType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        windowType = asm.GetType("Arawn.CrystalSave.Editor.RememberMeSettingsWindow");
                        if (windowType != null) break;
                    }
                }
                if (windowType != null)
                {
                    MethodInfo showMethod = windowType.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static);
                    showMethod?.Invoke(null, null);
                }
                else
                {
                    Debug.LogWarning("RememberMeSettingsWindow not found.");
                }
                Close();
            }
        }

        void DrawNavigation()
        {
            EditorGUILayout.BeginHorizontal();
            float navButtonHeight = 32f;
            float navButtonMinWidth = 110f;
            if (currentPage != Page.ProjectInfo && currentPage != Page.Complete)
            {
                if (GUILayout.Button("Back", GUILayout.Height(navButtonHeight), GUILayout.MinWidth(navButtonMinWidth)))
                    currentPage--;
            }
            GUILayout.FlexibleSpace();
            if (currentPage == Page.Conflicts)
            {
                if (GUILayout.Button("Finish", GUILayout.Height(navButtonHeight), GUILayout.MinWidth(navButtonMinWidth)))
                    CreateOrUpdateAssets();
            }
            else if (currentPage != Page.Complete && currentPage != Page.ProjectInfo)
            {
                if (GUILayout.Button("Next", GUILayout.Height(navButtonHeight), GUILayout.MinWidth(navButtonMinWidth)))
                    currentPage++;
            }
            else
            {
                if (GUILayout.Button("Close & Setup Manually", GUILayout.Height(navButtonHeight), GUILayout.MinWidth(navButtonMinWidth)))
                {
                    MarkOnboardingCompleted();
                    Close();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // Marks onboarding as completed without forcing users to run through the wizard.
        // Creates SaveSettings.asset if it doesn’t exist yet and flips the flag.
        static void MarkOnboardingCompleted()
        {
            const string resourceDir = "Assets/Plugins/CrystalSave/Resources";
            if (!Directory.Exists(resourceDir))
                Directory.CreateDirectory(resourceDir);

            string saveSettingsPath = Path.Combine(resourceDir, "SaveSettings.asset");
            var saveSettings = AssetDatabase.LoadAssetAtPath<Arawn.CrystalSave.Runtime.SaveSettings>(saveSettingsPath);
            if (saveSettings == null)
            {
                saveSettings = ScriptableObject.CreateInstance<Arawn.CrystalSave.Runtime.SaveSettings>();
                AssetDatabase.CreateAsset(saveSettings, saveSettingsPath);
            }
            saveSettings.onboardingCompleted = true;
            EditorUtility.SetDirty(saveSettings);
            AssetDatabase.SaveAssets();
        }

        void CreateOrUpdateAssets()
        {
            const string resourceDir = "Assets/Plugins/CrystalSave/Resources";
            if (!Directory.Exists(resourceDir))
                Directory.CreateDirectory(resourceDir);

            string saveSettingsPath = Path.Combine(resourceDir, "SaveSettings.asset");
            string prefabRegistryPath = Path.Combine(resourceDir, "PrefabRegistry.asset");
            string tagRegistryPath = Path.Combine(resourceDir, "TagRegistry.asset");

            var saveSettings = AssetDatabase.LoadAssetAtPath<SaveSettings>(saveSettingsPath);
            if (saveSettings == null)
            {
                saveSettings = ScriptableObject.CreateInstance<SaveSettings>();
                AssetDatabase.CreateAsset(saveSettings, saveSettingsPath);
            }

            saveSettings.saveMethod = saveMethod;
            saveSettings.numberOfSaveSlots = slots;
            saveSettings.enableEncryption = enableEncryption;
            saveSettings.useUserIdForEncryption = useUserIdForEncryption;
            saveSettings.masterSecretSource = masterSecretSource;
            saveSettings.masterSecretProvider = masterSecretProvider;
            saveSettings.cloudCryptoMode = cloudCryptoMode;
            saveSettings.cloudCryptoProvider = cloudCryptoProvider;
            saveSettings.enableCloudSave = enableCloud;
            saveSettings.backend = backend;
            if (!IsAddressablesAvailable())
                useAddressables = false;
            saveSettings.useAddressables = useAddressables;
            // New steps
            saveSettings.enableCompression = enableCompression;
            saveSettings.enableSaveFileVerification = enableVerification;
            saveSettings.enableScreenshots = screenshotsEnabled;
            saveSettings.autoResolveConflicts = autoResolveConflicts;
            saveSettings.screenshotFormat = screenshotFormat;
            saveSettings.autoConflictPolicy = conflictPolicy;
            // Cloud storage hygiene
            if (!screenshotsEnabled)
                cloudSaveScreenshots = false;
            saveSettings.keepLocalMirror = keepLocalMirror;
            saveSettings.cloudSaveScreenshots = cloudSaveScreenshots;
            saveSettings.cloudSaveMetadata = cloudSaveMetadata;

            // Cloud-specific assignments
            saveSettings.defaultAuthProvider = defaultAuthProvider;
            saveSettings.cloudSaveTransport = cloudTransport;
            saveSettings.autoCloudSignIn = autoCloudSignIn;

            // Supabase
            saveSettings.supabaseUrl = supabaseUrl;
            saveSettings.supabaseAnonKey = supabaseAnonKey;
            saveSettings.bucket = bucket;
            saveSettings.userFolderStrategy = userFolderStrategy;

            // MySQL
            saveSettings.mySqlApiUrl = mySqlApiUrl;
            saveSettings.mySqlAuthApiUrl = mySqlAuthApiUrl;
            saveSettings.mySqlApiKey = mySqlApiKey;
            saveSettings.tableName = tableName;
            saveSettings.mySqlLoginMode = mySqlLoginMode;

            // Firebase
            saveSettings.firebaseBucket = firebaseBucket;
            saveSettings.firebaseIdToken = firebaseIdToken;
            // Mark onboarding complete to prevent auto-launching this wizard again
            saveSettings.onboardingCompleted = true;
            EditorUtility.SetDirty(saveSettings);

            var prefabRegistry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(prefabRegistryPath);
            if (prefabRegistry == null)
            {
                prefabRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
                AssetDatabase.CreateAsset(prefabRegistry, prefabRegistryPath);
            }
            EditorUtility.SetDirty(prefabRegistry);

            var tagRegistry = AssetDatabase.LoadAssetAtPath<TagRegistry>(tagRegistryPath);
            if (tagRegistry == null)
            {
                tagRegistry = ScriptableObject.CreateInstance<TagRegistry>();
                AssetDatabase.CreateAsset(tagRegistry, tagRegistryPath);
            }
            EditorUtility.SetDirty(tagRegistry);

            AssetDatabase.SaveAssets();
            currentPage = Page.Complete;

            // After finishing the wizard, open the main settings window
            EditorApplication.delayCall += () =>
            {
                var windowType = Type.GetType("Arawn.CrystalSave.Editor.RememberMeSettingsWindow");
                if (windowType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        windowType = asm.GetType("Arawn.CrystalSave.Editor.RememberMeSettingsWindow");
                        if (windowType != null) break;
                    }
                }
                var show = windowType?.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static);
                show?.Invoke(null, null);
            };
        }
    }
}
#endif
