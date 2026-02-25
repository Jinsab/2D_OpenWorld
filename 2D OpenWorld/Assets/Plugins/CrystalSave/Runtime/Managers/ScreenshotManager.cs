#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if REMEMBERME_CLOUDSAVE_PRESENT
using Unity.Services.CloudSave;
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
#endif

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Captures thumbnails, keeps a local copy when needed, and uploads them to
    /// Supabase if that backend is active.  When <i>Keep Local Mirror</i> is
    /// off, a <b>tiny</b> on‑disk copy is still written for Supabase so slot
    /// UIs can load it synchronously.
    /// </summary>
    public class ScreenshotManager
    {
        private readonly SaveSettings saveSettings;
        private readonly string screenshotFolderPath;
        private readonly string rootPath;
        
        // WebGL memory cache for screenshots since file I/O is unreliable
        private static readonly Dictionary<string, byte[]> webglScreenshotCache = new Dictionary<string, byte[]>();
        
        /// <summary>
        /// Optional custom method used to capture a screenshot texture.
        /// When null, <see cref="ScreenCapture.CaptureScreenshotAsTexture"/> is used.
        /// </summary>
        public Func<Texture2D> CustomCapture { get; set; }
        /// <summary>
        /// Optional async custom method used to capture a screenshot texture.
        /// Checked when <see cref="CustomCapture"/> is null.
        /// </summary>
        public Func<Task<Texture2D>> CustomCaptureAsync { get; set; }

        /* ------------------------------------------------------------------ */
        // Helper flags
        /* ------------------------------------------------------------------ */
        private bool UseLocalMirror =>
            !saveSettings.enableCloudSave ||
            (saveSettings.enableCloudSave &&
             saveSettings.keepLocalMirror &&
             saveSettings.cloudCryptoMode != CloudCryptoMode.ServerSide);

        // Store screenshots locally only when using a local save backend or
        // when a local mirror is explicitly enabled.
        private bool SaveLocally =>
            !saveSettings.enableCloudSave ||
            UseLocalMirror;
        /* ------------------------------------------------------------------ */
        public ScreenshotManager(SaveSettings settings, string rootPath)
        {
            saveSettings = settings;
            this.rootPath = rootPath;

#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, prefer memory cache for instant access. When cloud save is disabled
            // (or a local mirror is used), also set up a local file cache under
            // persistentDataPath (IndexedDB) so screenshots can persist across reloads
            // if config.autoSyncPersistentDataPath = true is enabled in createUnityInstance().
            if (saveSettings.enableScreenshots)
            {
                Logger.Log("ScreenshotManager: WebGL mode - using memory cache; local file cache enabled when SaveLocally is true", LogCategory.ScreenshotManager, LogLevel.Info);

                if (SaveLocally)
                {
                    screenshotFolderPath = Path.Combine(rootPath, saveSettings.screenshotFolderName);
                    try
                    {
                        if (!Directory.Exists(screenshotFolderPath))
                            Directory.CreateDirectory(screenshotFolderPath);
                        Logger.Log($"ScreenshotManager: WebGL local screenshot folder: '{screenshotFolderPath}'.", LogCategory.ScreenshotManager, LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"ScreenshotManager: Failed to create WebGL screenshot folder '{screenshotFolderPath}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
                }
            }
#else
            // Create screenshot folder if needed for local saves OR for Supabase cache
            if (saveSettings.enableScreenshots && (SaveLocally || saveSettings.backend == SaveBackend.Supabase))
            {
                screenshotFolderPath = Path.Combine(rootPath,
                                                    saveSettings.screenshotFolderName);
                if (!Directory.Exists(screenshotFolderPath))
                {
                    Directory.CreateDirectory(screenshotFolderPath);
                    string purpose = SaveLocally ? "local storage" : "Supabase cache";
                    Logger.Log($"Created screenshot folder at '{screenshotFolderPath}' for {purpose}.", LogCategory.ScreenshotManager, LogLevel.Info);
                }
            }
#endif
        }

        public bool AreScreenshotsEnabled => saveSettings.enableScreenshots;

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Cache screenshot data in memory for WebGL builds where file I/O is unreliable
        /// </summary>
        public void CacheScreenshotInMemory(string fileName, byte[] data)
        {
            if (!string.IsNullOrEmpty(fileName) && data != null && data.Length > 0)
            {
                webglScreenshotCache[fileName] = data;
                Logger.Log($"ScreenshotManager: Cached screenshot '{fileName}' in WebGL memory ({data.Length} bytes)", LogCategory.ScreenshotManager, LogLevel.Info);
            }
        }
        
        /// <summary>
        /// Static method to cache screenshot data in WebGL memory from external classes
        /// </summary>
        public static void CacheScreenshotInWebGLMemory(string fileName, byte[] data)
        {
            if (!string.IsNullOrEmpty(fileName) && data != null && data.Length > 0)
            {
                webglScreenshotCache[fileName] = data;
                Logger.Log($"ScreenshotManager: Static cached screenshot '{fileName}' in WebGL memory ({data.Length} bytes)", LogCategory.ScreenshotManager, LogLevel.Info);
            }
        }
#endif

        /* ================================================================== */
        #region Capture
        /* ================================================================== */
        /// <summary>
        /// Captures a single end-of-frame screenshot and returns the encoded bytes and file extension.
        /// This does not keep any loop running; it yields until end-of-frame and captures once.
        /// </summary>
        public static IEnumerator CaptureOnce(ScreenshotFormat format, Func<Texture2D> customCapture, Action<byte[], string> onReady)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = customCapture != null ? customCapture() : ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
            {
                onReady?.Invoke(null, null);
                yield break;
            }
            try
            {
                byte[] bytes = format == ScreenshotFormat.PNG ? tex.EncodeToPNG() : tex.EncodeToJPG();
                string ext = format == ScreenshotFormat.PNG ? "png" : "jpg";
                onReady?.Invoke(bytes, ext);
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }
        public IEnumerator CaptureScreenshot(int slotNumber, Action<string> onComplete)
        {
            if (!saveSettings.enableScreenshots)
            {
                onComplete?.Invoke(null);
                yield break;
            }
            
            // In the Editor, WaitForEndOfFrame can hang indefinitely in certain contexts
            // (e.g., when triggered from OnGUI or when Game view isn't active/focused)
            // We'll add our own internal timeout as a safeguard
#if UNITY_EDITOR
            bool completed = false;
            string resultFileName = null;
            
            // Start the actual capture coroutine
            var captureCoroutine = CaptureScreenshotCoroutine(slotNumber, fileName =>
            {
                resultFileName = fileName;
                completed = true;
            });
            
            // Run with timeout
            float timeout = 5f; // 5 second internal timeout for screenshot capture
            float elapsed = 0f;
            
            while (captureCoroutine.MoveNext() && elapsed < timeout)
            {
                yield return captureCoroutine.Current;
                elapsed += Time.unscaledDeltaTime;
            }
            
            if (!completed)
            {
                Logger.Log($"[CrystalSave] Screenshot capture timed out - No screenshot was saved.", LogCategory.SaveManager, LogLevel.Warning);
                Logger.Log($"[CrystalSave] This happens when the Scene view is active instead of the Game view during save.", LogCategory.SaveManager, LogLevel.Warning);
                Logger.Log($"[CrystalSave] Tip: Keep the Game view visible while saving to capture screenshots.", LogCategory.SaveManager, LogLevel.Info);
                Logger.Log($"[CrystalSave] This is an Editor-only issue and will NOT occur in a built game.", LogCategory.SaveManager, LogLevel.Info);
            }
            
            onComplete?.Invoke(resultFileName);
#else
            yield return CaptureScreenshotCoroutine(slotNumber, onComplete);
#endif
        }

        private IEnumerator CaptureScreenshotCoroutine(int slotNumber, Action<string> onComplete)
        {
            /* 1 ─ Render is finished */
            yield return new WaitForEndOfFrame();

            /* 2 ─ Capture + encode on MAIN thread */
            Texture2D tex = null;
            yield return CaptureTextureCoroutine(captured => tex = captured);
            if (tex == null)
            {
                onComplete?.Invoke(null);
                yield break;
            }
            // Normalize to centered 16:9 (landscape) or 9:16 (portrait), then clamp to max 1920px on longer side
            Texture2D normalizedTex = CropAndResize(tex, saveSettings.screenshotFormat);

            string ext = saveSettings.screenshotFormat == ScreenshotFormat.PNG ? "png" : "jpg";
            string fileName;
            if (saveSettings.backend == SaveBackend.MySQL)
            {
                // Align with CrystalSave's remote naming convention so the server
                // can preserve the original timestamped file name.
                fileName = $"{slotNumber}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.{ext}";
            }
            else
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                fileName = $"Slot_{slotNumber}_{ts}.{ext}";
            }
            
            // Generate file path for local storage or Supabase cache
            bool needsLocalFile = SaveLocally || (saveSettings.backend == SaveBackend.Supabase && !SaveLocally);
            string filePath = needsLocalFile && !string.IsNullOrEmpty(screenshotFolderPath)
                ? Path.Combine(screenshotFolderPath, fileName)
                : null;

            byte[] bytes = saveSettings.screenshotFormat == ScreenshotFormat.PNG
                ? normalizedTex.EncodeToPNG()
                : normalizedTex.EncodeToJPG();

            // Optional encryption for screenshots
            try
            {
                if (saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                {
                    var enc = SaveManager.Instance?.EncryptionService;
                    if (enc?.UseEncryption == true)
                    {
                        bytes = enc.MaybeEncrypt(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Screenshot encryption failed; proceeding with plaintext. {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
            }

            UnityEngine.Object.Destroy(tex);          // safe: still on main thread (original)
            UnityEngine.Object.Destroy(normalizedTex); // destroy processed texture

            /* 3 ─ Write locally (if needed) */
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: if using local storage (cloud disabled or local mirror), write to IndexedDB-backed FS
            if (SaveLocally && filePath != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    File.WriteAllBytes(filePath, bytes);
                    Logger.Log($"Saved WebGL local screenshot file: {filePath}", LogCategory.ScreenshotManager, LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to write WebGL local screenshot '{filePath}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                }
            }
            // Always keep an in-memory copy for instant UI display in this session
            if (SaveLocally || saveSettings.backend == SaveBackend.Supabase)
            {
                webglScreenshotCache[fileName] = bytes;
                Logger.Log($"Cached screenshot in WebGL memory: {fileName} ({bytes.Length} bytes)", LogCategory.ScreenshotManager, LogLevel.Info);
            }
#else
            if (SaveLocally && filePath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllBytes(filePath, bytes);
            }
            // For Supabase, save a small local copy even when SaveLocally is false
            // so the UI can load it synchronously without downloading
            else if (saveSettings.backend == SaveBackend.Supabase && 
                     !SaveLocally && 
                     filePath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllBytes(filePath, bytes);
                Logger.Log($"Saved Supabase screenshot cache copy: {filePath}", LogCategory.ScreenshotManager, LogLevel.Info);
            }
#endif

            /* 4 ─ If we're on Supabase, start the async upload (still main thread) */
            if (saveSettings.backend == SaveBackend.Supabase &&
                SaveManager.Instance?.SaveSystem is SupabaseSaveSystem supabase)
            {
                Task upload = supabase.UploadScreenshotAsync(
                    fileName, bytes, saveSettings.screenshotFormat);

                /* wait until the HTTP request is done without leaving the main thread */
                while (!upload.IsCompleted)
                    yield return null;

                if (upload.Exception != null)
                {
                    Logger.Log($"Supabase upload failed: {upload.Exception.GetBaseException().Message}",
                        LogLevel.Error);
                    onComplete?.Invoke(null);
                    yield break;
                }
            }
            else if (saveSettings.backend == SaveBackend.Firebase &&
                     SaveManager.Instance?.SaveSystem is FirebaseSaveSystem firebase)
            {
                Task upload = firebase.UploadScreenshotAsync(
                    fileName, bytes, saveSettings.screenshotFormat);

                while (!upload.IsCompleted)
                    yield return null;

                if (upload.Exception != null)
                {
                    Logger.Log($"Firebase upload failed: {upload.Exception.GetBaseException().Message}",
                        LogLevel.Error);
                    onComplete?.Invoke(null);
                    yield break;
                }
            }
            else if (saveSettings.backend == SaveBackend.MySQL &&
                     SaveManager.Instance?.SaveSystem is MySqlSaveSystem mysql)
            {
                Task upload = mysql.UploadScreenshotAsync(
                    slotNumber,
                    fileName,
                    bytes,
                    saveSettings.screenshotFormat);
                while (!upload.IsCompleted)
                    yield return null;

                if (upload.Exception != null)
                {
                    Logger.Log($"MySQL upload failed: {upload.Exception.GetBaseException().Message}",
                        LogLevel.Error);
                    onComplete?.Invoke(null);
                    yield break;
                }
            }

#if REMEMBERME_CLOUDSAVE_PRESENT
            else if (saveSettings.backend == SaveBackend.UnityCloudSave &&
                     saveSettings.cloudSaveScreenshots &&
                     SaveManager.Instance?.SaveSettings.enableCloudSave == true)
            {
                // Always use Data API with WebGL-friendly compression, even in Editor play
                Logger.Log($"Screenshot original size: {bytes.Length} bytes", LogCategory.ScreenshotManager, LogLevel.Info);
                byte[] plainForCloud = bytes;
                // For Cloud Data API we compress image bytes. If encrypted, compress the decrypted form
                try
                {
                    // If data looks like our encrypted header, try to decrypt for compression
                    var enc = SaveManager.Instance?.EncryptionService;
                    if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                    {
                        var maybePlain = enc.MaybeDecrypt(bytes);
                        if (maybePlain != null) plainForCloud = maybePlain;
                    }
                }
                catch {}
                byte[] compressedBytes = CompressForCloudDataApi(plainForCloud);
                // Re-encrypt after compression if needed before upload
                try
                {
                    var enc = SaveManager.Instance?.EncryptionService;
                    if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                    {
                        compressedBytes = enc.MaybeEncrypt(compressedBytes);
                    }
                }
                catch {}
                string base64Data = Convert.ToBase64String(compressedBytes);

                // Build Data API key: prefix + no extension
                string baseKey = fileName.Contains(".") ? fileName.Substring(0, fileName.LastIndexOf('.')) : fileName;
                string dataApiKey = $"screenshot_{baseKey}";

                // Sanity logs
                Logger.Log($"Screenshot compression details:", LogCategory.ScreenshotManager, LogLevel.Info);
                Logger.Log($"  - Compressed size: {compressedBytes.Length} bytes", LogCategory.ScreenshotManager, LogLevel.Info);
                Logger.Log($"  - Base64 size: {base64Data.Length} characters", LogCategory.ScreenshotManager, LogLevel.Info);
                Logger.Log($"  - Data API key: '{dataApiKey}' (len {dataApiKey.Length})", LogCategory.ScreenshotManager, LogLevel.Info);

                // Validate key for Data API usage
                if (!ValidateCloudSaveKey(dataApiKey))
                {
                    Logger.Log($"Screenshot key '{dataApiKey}' invalid for Unity Cloud Save Data API.", LogCategory.ScreenshotManager, LogLevel.Error);
                    onComplete?.Invoke(null);
                    yield break;
                }

                // Enforce an upper size bound for Data API payloads
                if (!IsValidBase64(base64Data) || base64Data.Length > 800000)
                {
                    Logger.Log($"Screenshot payload too large or invalid for Data API (len={base64Data.Length}). Skipping cloud upload.", LogCategory.ScreenshotManager, LogLevel.Warning);
                }
                else
                {
                    Task uploadData = null;
                    try
                    {
                        var screenshotData = new Dictionary<string, object>
                        {
                            { "data", base64Data },
                            { "filename", fileName },
                            { "format", "jpg" },
                            { "compressed", true },
                            { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                        };
                        var uploadDict = new Dictionary<string, object> { { dataApiKey, screenshotData } };
                        uploadData = UnityCloudSaveService.Instance.Data.Player.SaveAsync(uploadDict);
                    }
                    catch (System.Exception ex)
                    {
                        Logger.Log($"Data API screenshot upload setup failed: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Error);
                        onComplete?.Invoke(null);
                        yield break;
                    }

                    while (uploadData != null && !uploadData.IsCompleted)
                        yield return null;

                    if (uploadData != null && uploadData.Exception != null)
                    {
                        var ex = uploadData.Exception.GetBaseException();
                        Logger.Log($"Data API screenshot upload failed: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Error);
                        onComplete?.Invoke(null);
                        yield break;
                    }
                }

                // Report result – on WebGL we want the dataApiKey back; other platforms keep the file name
#if UNITY_WEBGL && !UNITY_EDITOR
                onComplete?.Invoke(dataApiKey);
                yield break;
#endif
            }
#endif

            // For WebGL Unity Cloud Save, return the actual key used for storage
            string finalFileName = fileName;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (saveSettings.backend == SaveBackend.UnityCloudSave && saveSettings.cloudSaveScreenshots && saveSettings.enableCloudSave)
            {
                string dataApiKey = fileName.Contains(".") ? fileName.Substring(0, fileName.LastIndexOf('.')) : fileName;
                finalFileName = $"screenshot_{dataApiKey}";
            }
#endif

            onComplete?.Invoke(finalFileName);             // success
        }

        private IEnumerator CaptureTextureCoroutine(Action<Texture2D> onCaptured)
        {
            if (CustomCapture != null)
            {
                onCaptured?.Invoke(CustomCapture());
                yield break;
            }

            if (CustomCaptureAsync != null)
            {
                Task<Texture2D> captureTask;
                try
                {
                    captureTask = CustomCaptureAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Custom screenshot capture failed: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Error);
                    onCaptured?.Invoke(null);
                    yield break;
                }

                while (captureTask != null && !captureTask.IsCompleted)
                    yield return null;

                if (captureTask == null)
                {
                    onCaptured?.Invoke(null);
                    yield break;
                }

                if (captureTask.IsCanceled)
                {
                    Logger.Log("Custom screenshot capture was canceled.", LogCategory.ScreenshotManager, LogLevel.Warning);
                    onCaptured?.Invoke(null);
                    yield break;
                }

                if (captureTask.IsFaulted)
                {
                    Logger.Log($"Custom screenshot capture failed: {captureTask.Exception?.GetBaseException().Message}", LogCategory.ScreenshotManager, LogLevel.Error);
                    onCaptured?.Invoke(null);
                    yield break;
                }

                onCaptured?.Invoke(captureTask.Result);
                yield break;
            }

            onCaptured?.Invoke(ScreenCapture.CaptureScreenshotAsTexture());
        }


    public string CaptureScreenshotSync(int slotNumber)
        {
            if (!SaveLocally) return null; // makes sense only for local paths

            string folder = screenshotFolderPath ??
                            Path.Combine(rootPath, saveSettings.screenshotFolderName);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string ts   = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string file = $"Slot{slotNumber}_{ts}.{saveSettings.screenshotFormat.ToString().ToLower()}";
            string path = Path.Combine(folder, file);

            var tex = CustomCapture != null ? CustomCapture() : null;
            if (tex == null && CustomCaptureAsync != null)
                Logger.Log("CaptureScreenshotSync: async custom capture is not supported in sync mode; using ScreenCapture fallback.", LogCategory.ScreenshotManager, LogLevel.Warning);
            tex ??= ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null)
            {
                Texture2D normalizedTex = CropAndResize(tex, saveSettings.screenshotFormat);
                byte[] data = saveSettings.screenshotFormat == ScreenshotFormat.PNG
                    ? normalizedTex.EncodeToPNG()
                    : normalizedTex.EncodeToJPG();
                File.WriteAllBytes(path, data);
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(normalizedTex);
            }
            return file;
        }
        #endregion

        /* ================================================================== */
        #region Load / Cleanup / Delete
        /* ================================================================== */
        public Texture2D LoadScreenshot(SaveSlot slot)
        {
            if (!saveSettings.enableScreenshots) 
            {
                Logger.Log($"LoadScreenshot: Screenshots disabled", LogCategory.ScreenshotManager, LogLevel.Info);
                return null;
            }
            if (slot == null || string.IsNullOrEmpty(slot.ScreenshotFileName)) 
            {
                Logger.Log($"LoadScreenshot: Slot null or no filename - slot null: {slot == null}, filename: '{slot?.ScreenshotFileName}'", LogCategory.ScreenshotManager, LogLevel.Info);
                return null;
            }
            
            // For Supabase backend, allow loading even when SaveLocally is false
            // because we want to check for local cache before downloading
            bool allowLoad = SaveLocally || saveSettings.backend == SaveBackend.Supabase;
            
            Logger.Log($"LoadScreenshot: Backend={saveSettings.backend}, SaveLocally={SaveLocally}, allowLoad={allowLoad}, filename='{slot.ScreenshotFileName}', rootPath='{rootPath}', screenshotFolderPath='{screenshotFolderPath}'", LogCategory.ScreenshotManager, LogLevel.Info);
            
            if (!allowLoad)
            {
                Logger.Log($"LoadScreenshot: Load not allowed for backend {saveSettings.backend}", LogCategory.ScreenshotManager, LogLevel.Info);
                return null;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, try memory cache first
            if (webglScreenshotCache.TryGetValue(slot.ScreenshotFileName, out byte[] cachedData))
            {
                // Decrypt if needed before LoadImage
                try
                {
                    var enc = SaveManager.Instance?.EncryptionService;
                    if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                        cachedData = enc.MaybeDecrypt(cachedData);
                }
                catch {}
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(cachedData))
                {
                    Logger.Log($"LoadScreenshot: Successfully loaded screenshot from WebGL memory cache: {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Info);
                    return tex;
                }
                else
                {
                    Logger.Log($"LoadScreenshot: Failed to load image data from WebGL memory cache: {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    UnityEngine.Object.Destroy(tex);
                }
            }

            // Compatibility: if the slot filename is a Unity Cloud Save Data API key
            // like "screenshot_Slot_1_yyyyMMdd_HHmmssfff" (no extension), try to
            // locate a local file or memory cache entry with the original filename
            // that includes the extension.
            string inferredBase = null;
            if (slot.ScreenshotFileName.StartsWith("screenshot_") && !slot.ScreenshotFileName.Contains('.'))
            {
                inferredBase = slot.ScreenshotFileName.Substring("screenshot_".Length);
                // Try memory cache with common extensions
                string candidatePng = inferredBase + ".png";
                string candidateJpg = inferredBase + ".jpg";
                if (webglScreenshotCache.TryGetValue(candidatePng, out var memPng))
                {
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(memPng))
                    {
                        Logger.Log($"LoadScreenshot: Resolved Data API key via WebGL memory cache: {candidatePng}", LogCategory.ScreenshotManager, LogLevel.Info);
                        return tex;
                    }
                    UnityEngine.Object.Destroy(tex);
                }
                if (webglScreenshotCache.TryGetValue(candidateJpg, out var memJpg))
                {
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(memJpg))
                    {
                        Logger.Log($"LoadScreenshot: Resolved Data API key via WebGL memory cache: {candidateJpg}", LogCategory.ScreenshotManager, LogLevel.Info);
                        return tex;
                    }
                    UnityEngine.Object.Destroy(tex);
                }
            }

            // If using local storage (cloud disabled or local mirror), try IndexedDB-backed FS
            if (SaveLocally && !string.IsNullOrEmpty(screenshotFolderPath))
            {
                string path = Path.Combine(screenshotFolderPath, slot.ScreenshotFileName);
                if (File.Exists(path))
                {
                    try
                    {
                        byte[] data = File.ReadAllBytes(path);
                        try
                        {
                            var enc = SaveManager.Instance?.EncryptionService;
                            if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                                data = enc.MaybeDecrypt(data);
                        }
                        catch {}
                        Texture2D tex = new Texture2D(2, 2);
                        if (tex.LoadImage(data))
                        {
                            // Warm the memory cache for future loads this session
                            webglScreenshotCache[slot.ScreenshotFileName] = data;
                            Logger.Log($"LoadScreenshot: Loaded WebGL local FS screenshot: {path}", LogCategory.ScreenshotManager, LogLevel.Info);
                            return tex;
                        }
                        UnityEngine.Object.Destroy(tex);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"LoadScreenshot: Error reading WebGL local screenshot '{path}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
                }
                else
                {
                    Logger.Log($"LoadScreenshot: WebGL local FS file does not exist at {path}", LogCategory.ScreenshotManager, LogLevel.Info);
                }

                // Try inferred local filenames if we detected a Data API key
                if (!string.IsNullOrEmpty(inferredBase))
                {
                    foreach (var ext in new[] { ".png", ".jpg" })
                    {
                        string inferredPath = Path.Combine(screenshotFolderPath, inferredBase + ext);
                        if (File.Exists(inferredPath))
                        {
                            try
                            {
                                byte[] data = File.ReadAllBytes(inferredPath);
                                try
                                {
                                    var enc = SaveManager.Instance?.EncryptionService;
                                    if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                                        data = enc.MaybeDecrypt(data);
                                }
                                catch {}
                                Texture2D tex = new Texture2D(2, 2);
                                if (tex.LoadImage(data))
                                {
                                    // Cache under both the original key and the inferred filename
                                    webglScreenshotCache[slot.ScreenshotFileName] = data;
                                    webglScreenshotCache[inferredBase + ext] = data;
                                    Logger.Log($"LoadScreenshot: Resolved Data API key via WebGL local FS: {inferredPath}", LogCategory.ScreenshotManager, LogLevel.Info);
                                    return tex;
                                }
                                UnityEngine.Object.Destroy(tex);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"LoadScreenshot: Error reading inferred WebGL local screenshot '{inferredPath}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                            }
                        }
                    }
                }
            }

            Logger.Log($"LoadScreenshot: WebGL cache and local FS miss for {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Info);
            return null;
#else    
            if (string.IsNullOrEmpty(screenshotFolderPath)) 
            {
                Logger.Log($"LoadScreenshot: Screenshot folder path is null or empty", LogCategory.ScreenshotManager, LogLevel.Info);
                return null;
            }

            string path = Path.Combine(screenshotFolderPath, slot.ScreenshotFileName);
            Texture2D tex = null;

            Logger.Log($"LoadScreenshot: Attempting to load from path: {path}", LogCategory.ScreenshotManager, LogLevel.Info);

            // 1️⃣ Try loading the local copy
            if (File.Exists(path))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    try
                    {
                        var enc = SaveManager.Instance?.EncryptionService;
                        if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                            data = enc.MaybeDecrypt(data);
                    }
                    catch {}
                    tex = new Texture2D(2, 2);
                    if (tex.LoadImage(data))
                    {
                        Logger.Log($"LoadScreenshot: Successfully loaded screenshot from {path}", LogCategory.ScreenshotManager, LogLevel.Info);
                        return tex;
                    }
                    else
                    {
                        Logger.Log($"LoadScreenshot: Failed to load image data from {path}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error loading screenshot '{path}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Error);
                    tex = null;
                }
            }
            else
            {
                Logger.Log($"LoadScreenshot: File does not exist at {path}", LogCategory.ScreenshotManager, LogLevel.Info);
            }

            // 2️⃣ Remote fallback removed – use DownloadScreenshotAsync externally

            Logger.Log($"LoadScreenshot: Returning null for {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Info);
            return null;
#endif
        }

        public async Task<Texture2D> DownloadScreenshotAsync(SaveSlot slot, int timeoutSeconds = 10)
        {
            if (slot == null || string.IsNullOrEmpty(slot.ScreenshotFileName))
                return null;

            if (saveSettings.backend == SaveBackend.Supabase)
            {
                if (SaveManager.Instance?.SaveSystem is not SupabaseSaveSystem supabase)
                    return null;

                try
                {
                    var downloadTask = supabase.DownloadScreenshotAsync(slot.ScreenshotFileName);
                    if (await Task.WhenAny(downloadTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) == downloadTask)
                    {
                            byte[] data = await downloadTask;
                        if (data != null && data.Length > 0)
                        {
                                try
                                {
                                    var enc = SaveManager.Instance?.EncryptionService;
                                    if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                                        data = enc.MaybeDecrypt(data);
                                }
                                catch {}
                            string path = Path.Combine(screenshotFolderPath, slot.ScreenshotFileName);
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            File.WriteAllBytes(path, data);
                            var tex = new Texture2D(2, 2);
                            if (tex.LoadImage(data))
                                return tex;
                            UnityEngine.Object.Destroy(tex);
                        }
                    }
                    else
                    {
                        Logger.Log($"Screenshot download timed out: {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to download screenshot '{slot.ScreenshotFileName}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                }

                return null;
            }
#if REMEMBERME_CLOUDSAVE_PRESENT
            if (saveSettings.backend == SaveBackend.UnityCloudSave && saveSettings.cloudSaveScreenshots)
            {
                try
                {
                    // For Unity Cloud Save, we need to check both Data API (new format) and Files API (legacy)
                    // since screenshots might have been uploaded using either method
                    
                    string dataApiKey = slot.ScreenshotFileName.Contains(".") ? 
                        slot.ScreenshotFileName.Substring(0, slot.ScreenshotFileName.LastIndexOf('.')) : 
                        slot.ScreenshotFileName;
                    dataApiKey = $"screenshot_{dataApiKey}";
                    
                    // Try Data API first (new structured format used by WebGL)
                    try
                    {
                        var dataDownloadTask = UnityCloudSaveService.Instance.Data.Player.LoadAsync(
                            new HashSet<string> { dataApiKey });
                        Logger.Log($"Attempting Data API download for screenshot: {dataApiKey}", LogCategory.ScreenshotManager, LogLevel.Info);
                        
                        if (await Task.WhenAny(dataDownloadTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) == dataDownloadTask)
                        {
                            var result = await dataDownloadTask;
                            byte[] data = null;
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
                                            data = Convert.FromBase64String(base64Data);
                                            try
                                            {
                                                var enc = SaveManager.Instance?.EncryptionService;
                                                if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                                                    data = enc.MaybeDecrypt(data);
                                            }
                                            catch {}
                                            Logger.Log($"Screenshot downloaded via Data API with structured format: {data.Length} bytes", LogCategory.ScreenshotManager, LogLevel.Info);
                                        }
                                    }
                                    else
                                    {
                                        // Fallback: try as direct base64 string
                                        string base64Data = item.Value.GetAs<string>();
                                        if (!string.IsNullOrEmpty(base64Data))
                                        {
                                            data = Convert.FromBase64String(base64Data);
                                            try
                                            {
                                                var enc = SaveManager.Instance?.EncryptionService;
                                                if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                                                    data = enc.MaybeDecrypt(data);
                                            }
                                            catch {}
                                            Logger.Log($"Screenshot downloaded via Data API with legacy format: {data.Length} bytes", LogCategory.ScreenshotManager, LogLevel.Info);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log($"Error parsing Data API screenshot data for {dataApiKey}: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                                }
                            }
                            
                            // If we got data from Data API, use it
                            if (data != null && data.Length > 0)
                            {
                                string path = Path.Combine(screenshotFolderPath, slot.ScreenshotFileName);
                                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                                File.WriteAllBytes(path, data);
                                var tex = new Texture2D(2, 2);
                                if (tex.LoadImage(data))
                                    return tex;
                                UnityEngine.Object.Destroy(tex);
                            }
                        }
                        else
                        {
                            Logger.Log($"Data API screenshot download timed out: {dataApiKey}", LogCategory.ScreenshotManager, LogLevel.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Data API download failed for {dataApiKey}, will try Files API fallback: {ex.Message}", LogLevel.Info);
                    }

#if !UNITY_WEBGL || UNITY_EDITOR
                    // Fallback to Files API for non-WebGL platforms or screenshots uploaded via Files API
                    try
                    {
                        Logger.Log($"Attempting Files API download for screenshot: {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Info);
                        var filesDownloadTask = UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(slot.ScreenshotFileName);
                        
                        if (await Task.WhenAny(filesDownloadTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) == filesDownloadTask)
                        {
                            byte[] data = await filesDownloadTask;
                            if (data != null && data.Length > 0)
                            {
                                try
                                {
                                    var enc = SaveManager.Instance?.EncryptionService;
                                    if (enc?.UseEncryption == true && saveSettings.enableEncryption && saveSettings.encryptScreenshots)
                                        data = enc.MaybeDecrypt(data);
                                }
                                catch {}
                                Logger.Log($"Screenshot downloaded via Files API: {data.Length} bytes", LogCategory.ScreenshotManager, LogLevel.Info);
                                string path = Path.Combine(screenshotFolderPath, slot.ScreenshotFileName);
                                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                                File.WriteAllBytes(path, data);
                                var tex = new Texture2D(2, 2);
                                if (tex.LoadImage(data))
                                    return tex;
                                UnityEngine.Object.Destroy(tex);
                            }
                        }
                        else
                        {
                            Logger.Log($"Files API screenshot download timed out: {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Files API download also failed for {slot.ScreenshotFileName}: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
#else
                    Logger.Log($"WebGL platform: skipping Files API fallback for {slot.ScreenshotFileName}", LogCategory.ScreenshotManager, LogLevel.Info);
#endif
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to download screenshot '{slot.ScreenshotFileName}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Error);
                }

                return null;
            }
#endif

            return null;
        }


        public void CleanupUnusedScreenshots(List<SaveSlot> slots)
        {
            if (!SaveLocally || !saveSettings.enableScreenshots || string.IsNullOrEmpty(screenshotFolderPath))
                return;

            try
            {
                string searchPattern = saveSettings.screenshotFormat == ScreenshotFormat.PNG ? "*.png" : "*.jpg";
                var files = new DirectoryInfo(screenshotFolderPath).GetFiles(searchPattern);

                foreach (FileInfo f in files)
                {
                    if (!slots.Any(s => s.ScreenshotFileName == f.Name))
                    {
                        f.Delete();
                        Logger.Log($"Deleted unused screenshot '{f.Name}'.", LogCategory.ScreenshotManager, LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to clean up screenshots: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Error);
            }
        }

        public void DeleteScreenshot(string screenshotFileName)
        {
            if (!saveSettings.enableScreenshots || string.IsNullOrEmpty(screenshotFileName))
                return;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, clear memory cache entry
            if (webglScreenshotCache.Remove(screenshotFileName))
                Logger.Log($"Deleted screenshot from WebGL memory cache: '{screenshotFileName}'.", LogCategory.ScreenshotManager, LogLevel.Info);

            // Also delete local FS copy if we are using local storage
            if (SaveLocally && !string.IsNullOrEmpty(screenshotFolderPath))
            {
                string filePath = Path.Combine(screenshotFolderPath, screenshotFileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Logger.Log($"Deleted WebGL local screenshot at '{filePath}'.", LogCategory.ScreenshotManager, LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to delete WebGL local screenshot '{filePath}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
                }
            }
            
#else
            if (string.IsNullOrEmpty(screenshotFolderPath)) return;

            string filePath = Path.Combine(screenshotFolderPath, screenshotFileName);
            if (!File.Exists(filePath)) return;

            try
            {
                File.Delete(filePath);
                Logger.Log($"Deleted screenshot at '{filePath}'.", LogCategory.ScreenshotManager, LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to delete screenshot '{filePath}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Error);
            }
#endif
        }

        public string DuplicateScreenshot(string existingFileName, int slotNumber)
        {
            if (!SaveLocally || !saveSettings.enableScreenshots || string.IsNullOrEmpty(existingFileName))
                return null;
            if (string.IsNullOrEmpty(screenshotFolderPath))
                return null;

            string srcPath = Path.Combine(screenshotFolderPath, existingFileName);
            if (!File.Exists(srcPath))
                return null;

            string ext = Path.GetExtension(existingFileName);
            string newName = $"Slot_{slotNumber}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}";
            string dstPath = Path.Combine(screenshotFolderPath, newName);
            File.Copy(srcPath, dstPath, true);
            return newName;
        }

        /// <summary>
        /// Compresses screenshot data for Unity Cloud Save Data API uploads.
        /// Cross-platform: clamps longest side to 512px and encodes JPG at ~50% quality.
        /// </summary>
        private byte[] CompressForCloudDataApi(byte[] originalBytes)
        {
            try
            {
                // Load the original texture
                Texture2D originalTex = new Texture2D(2, 2);
                if (!originalTex.LoadImage(originalBytes))
                {
                    UnityEngine.Object.Destroy(originalTex);
                    Logger.Log("Failed to load original screenshot for compression", LogCategory.ScreenshotManager, LogLevel.Warning);
                    return originalBytes;
                }

                // Clamp longest side to 512 (maintain aspect)
                const int MaxDim = 512;
                float scale = Mathf.Min(1f, (float)MaxDim / Mathf.Max(originalTex.width, originalTex.height));
                int newWidth = Mathf.Max(1, Mathf.RoundToInt(originalTex.width * scale));
                int newHeight = Mathf.Max(1, Mathf.RoundToInt(originalTex.height * scale));

                // Downscale via RenderTexture for quality
                RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(originalTex, rt);
                
                RenderTexture.active = rt;
                Texture2D downscaled = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
                downscaled.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                downscaled.Apply();
                
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(originalTex);

                // Encode JPG at ~50% quality
                byte[] compressedBytes = downscaled.EncodeToJPG(50);
                UnityEngine.Object.Destroy(downscaled);

                Logger.Log($"Cloud Data API screenshot compressed: {originalBytes.Length} -> {compressedBytes.Length} bytes", LogCategory.ScreenshotManager, LogLevel.Info);
                return compressedBytes;
            }
            catch (System.Exception ex)
            {
                Logger.Log($"Screenshot compression failed: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                return originalBytes;
            }
        }
        
        /// <summary>
        /// Validates a key for Unity Cloud Save compatibility
        /// </summary>
        private bool ValidateCloudSaveKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Logger.Log("Cloud Save key is null or empty", LogCategory.ScreenshotManager, LogLevel.Error);
                return false;
            }
            
            // Unity Cloud Save key length limit (typically 255 characters)
            if (key.Length > 255)
            {
                Logger.Log($"Cloud Save key too long: {key.Length} characters (max 255)", LogCategory.ScreenshotManager, LogLevel.Error);
                return false;
            }
            
            // Check for invalid characters - Data API is more restrictive than Files API
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    Logger.Log($"Cloud Save key contains invalid character '{c}' at position {i} in key '{key}'. Data API only allows alphanumeric and underscore.", LogCategory.ScreenshotManager, LogLevel.Error);
                    return false;
                }
            }
            
            // Check for leading/trailing underscores or starting with numbers (often not allowed)
            if (key.StartsWith("_") || key.EndsWith("_") || char.IsDigit(key[0]))
            {
                Logger.Log($"Cloud Save key has invalid format: '{key}'. Cannot start with underscore or digit, or end with underscore.", LogLevel.Error);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates if a string is valid base64
        /// </summary>
        private bool IsValidBase64(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return false;
                
            // Base64 length must be multiple of 4
            if (base64String.Length % 4 != 0)
                return false;
                
            try
            {
                Convert.FromBase64String(base64String);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion

        /* ================================================================== */
        #region Crop & Resize Helpers
        /* ================================================================== */
        /// <summary>
        /// Center-crops the source to 16:9 (landscape) or 9:16 (portrait) based on orientation,
        /// then scales down so neither dimension exceeds 1920px. Never scales up.
        /// </summary>
        private Texture2D CropAndResize(Texture2D source, ScreenshotFormat format)
        {
            if (source == null) return null;

            int srcW = source.width;
            int srcH = source.height;
            bool portrait = srcH > srcW;
            float targetAspect = portrait ? (9f / 16f) : (16f / 9f);
            float srcAspect = (float)srcW / srcH;

            // Determine crop rectangle (centered)
            int cropW = srcW;
            int cropH = srcH;
            int cropX = 0;
            int cropY = 0;

            if (srcAspect > targetAspect)
            {
                // Too wide -> crop width
                cropW = Mathf.FloorToInt(srcH * targetAspect);
                cropX = (srcW - cropW) / 2;
            }
            else if (srcAspect < targetAspect)
            {
                // Too tall -> crop height
                cropH = Mathf.FloorToInt(srcW / targetAspect);
                cropY = (srcH - cropH) / 2;
            }

            // Create cropped texture (CPU copy to keep it simple and robust)
            var croppedFormat = format == ScreenshotFormat.PNG ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            Texture2D cropped = new Texture2D(cropW, cropH, croppedFormat, false);
            var pixels = source.GetPixels(cropX, cropY, cropW, cropH);
            cropped.SetPixels(pixels);
            cropped.Apply(false, false);

            // Determine final size (clamp to max 1920 on both axes, no upscaling)
            const int MAX_DIM = 1920;
            float scale = Mathf.Min(1f, Mathf.Min((float)MAX_DIM / cropW, (float)MAX_DIM / cropH));
            int finalW = Mathf.Max(1, Mathf.RoundToInt(cropW * scale));
            int finalH = Mathf.Max(1, Mathf.RoundToInt(cropH * scale));

            if (finalW == cropW && finalH == cropH)
            {
                // Already within limits
                return cropped;
            }

            // Scale with RenderTexture for quality
            RenderTexture rt = RenderTexture.GetTemporary(finalW, finalH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevActive = RenderTexture.active;
            try
            {
                Graphics.Blit(cropped, rt);
                RenderTexture.active = rt;
                Texture2D finalTex = new Texture2D(finalW, finalH, croppedFormat, false);
                finalTex.ReadPixels(new Rect(0, 0, finalW, finalH), 0, 0);
                finalTex.Apply(false, false);

                UnityEngine.Object.Destroy(cropped);
                return finalTex;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
        #endregion
    }
}
#endif
