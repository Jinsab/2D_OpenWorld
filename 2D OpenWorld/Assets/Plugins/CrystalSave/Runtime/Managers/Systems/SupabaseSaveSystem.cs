#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// REST-only Supabase backend.
	/// • Writes/reads full metadata as JSON via a pure-fields DTO
	/// • Mirrors .sav *and* MemoryPack header when keepLocalMirror = true
	/// • Keeps remote/local file-name scheme aligned (slot-N.sav / .meta.json)
	/// </summary>
	public sealed class SupabaseSaveSystem : ISaveSystem
	{
	/* ───────────── ctor inputs ───────────── */
	readonly string url;
	readonly string key;
	readonly string bucket;
	readonly string userDirStatic;
	readonly IUserFolderResolver liveResolver;
	readonly bool useJson;
	readonly string dataExt;
	readonly SaveSettings _cfg;

        readonly bool   keepLocalMirror;
        readonly string localFileStem;
        readonly string persistentPath;       // cache path on ctor (main thread)

	const int REQUEST_TIMEOUT_SECONDS = 10;

    const string META_EXT      = ".meta.json";        // cloud
    const string LEGACY_META_MASK = "Slot{0}_Meta.bin";  // local legacy fallback

	/* ───────────── DTO used only for JSON ───────────── */
	[Serializable]
	struct SaveSlotDTO
	{
	    public int    SlotNumber;
	    public string SlotName;
	    public string ScreenshotFileName;
	    public string LastActiveScene;
	    public long   LastSavedTicks;
    }

    // Local metadata filename helpers (configurable pattern with legacy fallback)
    string MetaLocalFileName(int n)
    {
        string pattern = _cfg.metadataFileNamePattern;
        if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains("{n}"))
            pattern = LEGACY_META_MASK.Replace("{0}", "{n}");
        return pattern.Replace("{n}", n.ToString());
    }

    string SlotFolder(int n) => Path.Combine(persistentPath, $"slot{n}");
    string EnsureSlotFolder(int n)
    {
        var dir = SlotFolder(n);
        try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch {}
        return dir;
    }

    string MetaLocalPath(int n) => Path.Combine(SlotFolder(n), MetaLocalFileName(n));
    string LegacyMetaLocalPath(int n) => Path.Combine(persistentPath, string.Format(LEGACY_META_MASK, n));

    void WriteLocalMetadata(SaveSlot s)
    {
        if (!LocalWritesEnabled()) return;

        // Cache metadata locally for faster access on next startup
        try
        {
            string dir = EnsureSlotFolder(s.SlotNumber);
            string path = Path.Combine(dir, MetaLocalFileName(s.SlotNumber));
            byte[] bytes = SaveDataSerializer.Instance.Serialize(s);

            // Respect encryption setting for slot metadata to match runtime readers
            var enc = SaveManager.Instance?.EncryptionService;
            if (enc?.UseEncryption == true && _cfg.encryptSlotMetadata)
            {
                bytes = enc.MaybeEncrypt(bytes);
            }

            File.WriteAllBytes(path, bytes);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SupabaseSaveSystem] Failed to write local metadata cache: {ex.Message}");
        }
    }

    // Paths for remote objects
    string PathFor(SaveSlot s, string ext) => $"{UserDir}/{localFileStem}{s.SlotNumber}{ext}";
    string PathFor(int slot,   string ext) => $"{UserDir}/{localFileStem}{slot}{ext}";

    // Constructor
    public SupabaseSaveSystem(SaveSettings cfg, string persistentPath)
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[SupabaseSaveSystem] WebGL: Constructor started");
#endif
            _cfg = cfg;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: SaveSettings loaded, userFolderStrategy: {cfg.userFolderStrategy}");
#endif

            url    = cfg.supabaseUrl.TrimEnd('/');
            key    = cfg.supabaseAnonKey;
            bucket = cfg.bucket;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Basic config set - URL: {url}, Bucket: {bucket}");
#endif

            /* dynamic folder only for Strategy.Custom */
            if (cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                cfg.customUserFolderResolver is IUserFolderResolver r)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SupabaseSaveSystem] WebGL: Using custom user folder resolver");
#endif
                liveResolver = r;               // keep reference – its value may change later
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SupabaseSaveSystem] WebGL: Resolving static user folder...");
#endif
                userDirStatic = ResolveUserFolder(cfg);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SupabaseSaveSystem] WebGL: User folder resolved: {userDirStatic}");
#endif
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Setting transport and file options...");
#endif
            useJson  = cfg.cloudSaveTransport == CloudSaveTransport.JSON;
            dataExt  = useJson ? ".json" : ".sav";

            keepLocalMirror = cfg.keepLocalMirror &&
                              cfg.saveMethod == SaveMethod.BinaryFileFormat;
            localFileStem   = cfg.saveFileName;
            this.persistentPath  = persistentPath;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Constructor completed successfully! KeepLocalMirror: {keepLocalMirror}, UseJson: {useJson}");
#endif
        }
        catch (System.Exception ex)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogError($"[SupabaseSaveSystem] WebGL: Constructor failed with exception: {ex.Message}");
            Debug.LogError($"[SupabaseSaveSystem] WebGL: Stack trace: {ex.StackTrace}");
#else
            Debug.LogError($"[SupabaseSaveSystem] Constructor failed with exception: {ex.Message}");
#endif
            throw;
        }
    }

    // LocalWritesEnabled: perform local file writes when cloud save is disabled
    // or when keepLocalMirror is requested.
    bool LocalWritesEnabled() => !_cfg.enableCloudSave || keepLocalMirror;

	/* ================================================================== */
	/* Slot data
	/* ================================================================== */

	public async Task SaveAsync(byte[] data, SaveSlot slot)
	{
        // 1 ─ blob
        // Ensure network upload does not block the Unity main thread; keep continuations
        // off the main thread and only use the dispatcher for brief UnityWebRequest creation.
        if (useJson)
            await UploadJson(PathFor(slot, dataExt), data).ConfigureAwait(false);
        else
            await UploadObject(PathFor(slot, dataExt), data, "application/octet-stream").ConfigureAwait(false);

#if !UNITY_WEBGL || UNITY_EDITOR
    // Skip local file operations in WebGL builds to prevent freezing
    // WebGL has restricted file system access and sync I/O can cause browser hanging
    if (LocalWritesEnabled())
    {
        // 2 ─ optional local .sav mirror
        WriteLocalMirror(data, slot);
    }
    else
    {
        UnityEngine.Debug.Log("[SupabaseSaveSystem] Local writes disabled by settings; skipping local mirror and metadata write.");
    }
#else
    // WebGL: Skipping local file operations to prevent browser freezing
    UnityEngine.Debug.Log("[SupabaseSaveSystem] WebGL: Skipped local file operations to prevent freezing");
#endif

    // 3 ─ Always upload metadata to cloud (and cache locally when supported)
    // This ensures SlotName and other fields persist even in cloud-only mode.
    await SaveSlotMetadataAsync(slot).ConfigureAwait(false);
	}

	public Task<byte[]> LoadAsync(SaveSlot s) =>
	    useJson ? DownloadJson(PathFor(s, dataExt))
	            : DownloadObject(PathFor(s, dataExt));

        public async Task DeleteAsync(SaveSlot s)
        {
            // cloud
            await DeleteObject(PathFor(s, dataExt)).ConfigureAwait(false);
            await DeleteObject(PathFor(s, META_EXT)).ConfigureAwait(false);

	    if (!string.IsNullOrEmpty(s.ScreenshotFileName))
                await DeleteScreenshotAsync(s.ScreenshotFileName).ConfigureAwait(false);

#if !UNITY_WEBGL || UNITY_EDITOR
    // Skip local file operations in WebGL builds to prevent freezing
    // 1) Always delete cached metadata (both current and legacy) regardless of LocalWritesEnabled
    string meta = MetaLocalPath(s.SlotNumber);
    string legacyMeta = LegacyMetaLocalPath(s.SlotNumber);
    if (File.Exists(meta)) File.Delete(meta);
    if (legacyMeta != meta && File.Exists(legacyMeta)) File.Delete(legacyMeta);

    // 2) Delete local mirrors only when enabled
    if (LocalWritesEnabled())
    {
        string sav = Path.Combine(persistentPath, $"{localFileStem}{s.SlotNumber}.sav");
        if (File.Exists(sav)) File.Delete(sav);

        if (!string.IsNullOrEmpty(s.ScreenshotFileName))
        {
            string localScreenshot = Path.Combine(persistentPath, s.ScreenshotFileName);
            if (File.Exists(localScreenshot)) File.Delete(localScreenshot);
        }
    }
#endif
        }

        public Task DeleteLocalSlotAsync(SaveSlot s)
        {
            // Always delete cached metadata, even when local mirror is disabled
            string meta = MetaLocalPath(s.SlotNumber);
            string legacyMeta = LegacyMetaLocalPath(s.SlotNumber);
        
            // Always delete cached metadata (both current and legacy)
            if (File.Exists(meta)) File.Delete(meta);
            if (legacyMeta != meta && File.Exists(legacyMeta)) File.Delete(legacyMeta);
        
            // Delete save file when local writes enabled
            if (LocalWritesEnabled())
            {
                string sav = Path.Combine(persistentPath, $"{localFileStem}{s.SlotNumber}.sav");
                if (File.Exists(sav)) File.Delete(sav);
            }

            if (LocalWritesEnabled() && !string.IsNullOrEmpty(s.ScreenshotFileName))
            {
                string localScreenshot = Path.Combine(persistentPath, s.ScreenshotFileName);
                if (File.Exists(localScreenshot)) File.Delete(localScreenshot);
            }

            return Task.CompletedTask;
        }

	/* ================================================================== */
	/* Metadata
	/* ================================================================== */

	public async Task SaveSlotMetadataAsync(SaveSlot slot)
	{
	    if (string.IsNullOrEmpty(slot.SlotName))
	        slot.SlotName = $"Slot {slot.SlotNumber}";

	    UnityEngine.Debug.Log($"[SupabaseSaveSystem] SaveSlotMetadataAsync for slot {slot.SlotNumber}: Name='{slot.SlotName}', LastSaved={slot.LastSaved}, Screenshot='{slot.ScreenshotFileName ?? "null"}'");

	    // cloud JSON – all members survive because DTO uses fields
	    var dto = new SaveSlotDTO {
	        SlotNumber         = slot.SlotNumber,
	        SlotName           = slot.SlotName,
	        ScreenshotFileName = slot.ScreenshotFileName,
	        LastActiveScene    = slot.LastActiveScene,
	        LastSavedTicks     = slot.LastSaved.Ticks
	    };

        // Upload metadata JSON to cloud
        string metaJson = JsonUtility.ToJson(dto);
        await UploadObject(PathFor(slot, META_EXT), Encoding.UTF8.GetBytes(metaJson), "application/json").ConfigureAwait(false);
        UnityEngine.Debug.Log($"[SupabaseSaveSystem] Metadata uploaded to cloud for slot {slot.SlotNumber}");

#if !UNITY_WEBGL || UNITY_EDITOR
            // Skip local metadata file write in WebGL builds to prevent freezing
            // Cache metadata locally when local writes are enabled
            WriteLocalMetadata(slot);
            UnityEngine.Debug.Log($"[SupabaseSaveSystem] Metadata cached locally for slot {slot.SlotNumber}");
#endif
	}

	public async Task<SaveSlot> LoadSlotMetadataAsync(int num)
	{
	    UnityEngine.Debug.Log($"[SupabaseSaveSystem] LoadSlotMetadataAsync for slot {num}");
	    
#if !UNITY_WEBGL || UNITY_EDITOR
	    /* 1 ─ try local cached metadata first (skip in WebGL to prevent file I/O issues) */
            string loc = MetaLocalPath(num);
            string legacyLoc = LegacyMetaLocalPath(num);
            SaveSlot cachedSlot = null;
            if (File.Exists(loc))
            {
                try
                {
                    cachedSlot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(File.ReadAllBytes(loc));
                    if (cachedSlot != null) 
                    {
                        UnityEngine.Debug.Log($"[SupabaseSaveSystem] Found cached slot {num}: Name='{cachedSlot.SlotName}', LastSaved={cachedSlot.LastSaved}, Screenshot='{cachedSlot.ScreenshotFileName ?? "null"}'");
                    }
                }
                catch { /* fall through to cloud */ }
            }
            else if (File.Exists(legacyLoc))
            {
                try
                {
                    cachedSlot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(File.ReadAllBytes(legacyLoc));
                    if (cachedSlot != null)
                    {
                        UnityEngine.Debug.Log($"[SupabaseSaveSystem] Found legacy cached slot {num}: Name='{cachedSlot.SlotName}', LastSaved={cachedSlot.LastSaved}, Screenshot='{cachedSlot.ScreenshotFileName ?? "null"}'");
                    }
                }
                catch { /* fall through to cloud */ }
            }
#endif

	    UnityEngine.Debug.Log($"[SupabaseSaveSystem] Fetching slot {num} from cloud to validate cache");
	    
	    /* 2 ─ always check cloud to validate cached data */
	    byte[] bytes = null;
	    try
	    {
#if UNITY_WEBGL && !UNITY_EDITOR
	        UnityEngine.Debug.Log($"[SupabaseSaveSystem] WebGL: Starting cloud fetch for slot {num}");
#endif
            // WebGL requires resuming on the main thread for Unity APIs (logging, JSON, etc.)
            // Removing ConfigureAwait(false) here ensures the continuation runs on Unity's main thread.
            bytes = await DownloadObject(PathFor(num, META_EXT));
#if UNITY_WEBGL && !UNITY_EDITOR
	        UnityEngine.Debug.Log($"[SupabaseSaveSystem] WebGL: Cloud fetch completed for slot {num}, bytes: {bytes?.Length ?? 0}");
#endif
	    }
        catch (System.Exception ex)
        {
    #if UNITY_WEBGL && !UNITY_EDITOR
        UnityEngine.Debug.LogError($"[SupabaseSaveSystem] WebGL: Cloud fetch failed for slot {num}: {ex.Message}");
    #else
        UnityEngine.Debug.LogError($"[SupabaseSaveSystem] Cloud fetch failed for slot {num}: {ex.Message}");
    #endif
        bytes = null;

    }

	    if (bytes == null || bytes.Length == 0) 
	    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UnityEngine.Debug.Log($"[SupabaseSaveSystem] WebGL: No cloud metadata found for slot {num}, returning null");
#endif
        UnityEngine.Debug.Log($"[SupabaseSaveSystem] No cloud metadata found for slot {num}");
	        
#if !UNITY_WEBGL || UNITY_EDITOR
        // If no cloud data but we have cached data, it's stale - remove it (skip in WebGL)
        if (cachedSlot != null)
        {
            UnityEngine.Debug.Log($"[SupabaseSaveSystem] Removing stale cached metadata for slot {num}");
            if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) File.Delete(loc);
            if (!string.IsNullOrEmpty(legacyLoc) && File.Exists(legacyLoc)) File.Delete(legacyLoc);
        }
#endif
	        
	        return null;
	    }

	    SaveSlotDTO dto = JsonUtility.FromJson<SaveSlotDTO>(Encoding.UTF8.GetString(bytes));
	    var cloudSlot = new SaveSlot(
	        dto.SlotNumber,
	        dto.SlotName,
	        new DateTime(dto.LastSavedTicks, DateTimeKind.Utc),
	        dto.ScreenshotFileName,
	        dto.LastActiveScene);

	    UnityEngine.Debug.Log($"[SupabaseSaveSystem] Loaded slot {num} from cloud: Name='{cloudSlot.SlotName}', LastSaved={cloudSlot.LastSaved}, Screenshot='{cloudSlot.ScreenshotFileName ?? "null"}'");

#if !UNITY_WEBGL || UNITY_EDITOR
	    // Use cloud data (most authoritative) and update cache (skip in WebGL to prevent file I/O issues)
	    WriteLocalMetadata(cloudSlot);
#endif
	    return cloudSlot;
	}

	/* list remote slots – now parses DTOs so every field stays intact */
	public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
	{
	    var list = new List<SaveSlot>();
	    string pfx = $"{UserDir}/";

        var req = UnityWebRequest.Get(
            $"{url}/storage/v1/object/list/{bucket}?prefix={Escape(pfx)}&limit=1000");
	    AddAuth(req);
            await Send(req, allow404: true).ConfigureAwait(false);
            bool reqOk = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                req.result == UnityWebRequest.Result.Success);
            if (!reqOk) return list;

        var wrap = JsonUtility.FromJson<ResponseWrap>($"{{\"items\":{req.downloadHandler.text}}}");

        // Build a set of all object names under this prefix so we can verify that
        // each metadata file has a corresponding data blob. This avoids treating
        // metadata-only objects as valid saves which leads to false positives.
        var allNames = new HashSet<string>(wrap.items.Select(i => i.name), StringComparer.Ordinal);
	    foreach (var itm in wrap.items)
	    {
	        if (!itm.name.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)) continue;

            var mReq = UnityWebRequest.Get($"{url}/storage/v1/object/{bucket}/{itm.name}");
	        AddAuth(mReq);
                await Send(mReq).ConfigureAwait(false);
                bool metaOk = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                    mReq.result == UnityWebRequest.Result.Success);
                if (!metaOk) continue;

            SaveSlotDTO dto = JsonUtility.FromJson<SaveSlotDTO>(mReq.downloadHandler.text);
	        if (dto.SlotNumber < 1) continue;

            // Verify data blob exists alongside metadata before adding.
            string expectedDataKey = $"{UserDir}/{localFileStem}{dto.SlotNumber}{dataExt}";
            if (!allNames.Contains(expectedDataKey))
                continue;

            list.Add(new SaveSlot(
	            dto.SlotNumber,
	            dto.SlotName,
	            new DateTime(dto.LastSavedTicks, DateTimeKind.Utc),
	            dto.ScreenshotFileName,
	            dto.LastActiveScene));
	    }

	    return list
	        .GroupBy(s => s.SlotNumber)
	        .Select(g => g.OrderByDescending(x => x.LastSaved).First())
	        .OrderBy(s => s.SlotNumber)
	        .ToList();
	}

	/* ================================================================== */
	/* Metadata helpers
	/* ================================================================== */

    void WriteLocalMirror(byte[] data, SaveSlot s)
    {
        if (!LocalWritesEnabled()) return;
            string file = $"{localFileStem}{s.SlotNumber}.sav";
            File.WriteAllBytes(Path.Combine(persistentPath, file), data);
    }

    // removed duplicate WriteLocalMetadata; the version near the top is used

        public void BackupSlot(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            string path   = Path.Combine(persistentPath,
                                        $"{localFileStem}{slot.SlotNumber}.sav");
            string backup = path + ".bak";
            if (File.Exists(path))
                File.Copy(path, backup, true);
        }

        public void RestoreBackup(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            string path   = Path.Combine(persistentPath,
                                        $"{localFileStem}{slot.SlotNumber}.sav");
            string backup = path + ".bak";
            if (File.Exists(backup))
                File.Copy(backup, path, true);
        }

        public void DeleteBackup(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            string backup = Path.Combine(persistentPath,
                                        $"{localFileStem}{slot.SlotNumber}.sav.bak");
            if (File.Exists(backup))
                File.Delete(backup);
        }

        /* ================================================================== */
        /*  Temporary files (verification without mirror)                     */
        /* ================================================================== */

        string TempPathFor(SaveSlot slot) =>
            Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.tmp");

        public async Task WriteTempAsync(byte[] data, SaveSlot slot)
        {
            string path = TempPathFor(slot);
            await Task.Run(() => File.WriteAllBytes(path, data));
        }

        public async Task<byte[]> LoadTempAsync(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            if (!File.Exists(path)) return null;
            return await Task.Run(() => File.ReadAllBytes(path));
        }

        public void DeleteTemp(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }

	/* ================================================================== */
	/*  Screenshot API
	/* ================================================================== */

        public string GetSaveGamesPath() => persistentPath;

        public async Task UploadScreenshotAsync(string file, byte[] bytes, ScreenshotFormat fmt)
        {
            string mime = fmt == ScreenshotFormat.PNG ? "image/png" : "image/jpeg";
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Put(
                    $"{url}/storage/v1/object/{bucket}/{UserDir}/{Escape(file)}", bytes);
                r.downloadHandler = new DownloadHandlerBuffer();
                r.SetRequestHeader("Content-Type", mime);
                r.SetRequestHeader("x-upsert", "true");
                AddAuth(r);
                return r;
            });
            await Send(req).ConfigureAwait(false);
        }

        public async Task<byte[]> DownloadScreenshotAsync(string file)
        {
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Get(
                    $"{url}/storage/v1/object/{bucket}/{UserDir}/{Escape(file)}");
                AddAuth(r);
                return r;
            });
            await Send(req, allow404: true).ConfigureAwait(false);
            return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null;
        }

        public async Task DeleteScreenshotAsync(string file)
        {
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Delete(
                    $"{url}/storage/v1/object/{bucket}/{UserDir}/{Escape(file)}");
                AddAuth(r);
                return r;
            });
            await Send(req, allow404: true).ConfigureAwait(false);
        }

	/* ================================================================== */
	/*  Screenshot
	/* ================================================================== */

	/* ───────────── REST helpers ───────────── */

	[Serializable] struct ResponseWrap { public FileEntry[] items; }
	[Serializable] struct FileEntry    { public string name; }

        async Task UploadObject(string path, byte[] data, string mime)
        {
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Put(
                    $"{url}/storage/v1/object/{bucket}/{Escape(path)}", data);
                r.downloadHandler = new DownloadHandlerBuffer();
                r.SetRequestHeader("Content-Type", mime);
                r.SetRequestHeader("x-upsert", "true");
                AddAuth(r);
                return r;
            });
            await Send(req).ConfigureAwait(false);
        }

        async Task<byte[]> DownloadObject(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: DownloadObject starting for path: {path}");
#endif
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Get(
                    $"{url}/storage/v1/object/{bucket}/{Escape(path)}");
                AddAuth(r);
                return r;
            });
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: DownloadObject request created, calling Send...");
#endif
            await Send(req, allow404: true).ConfigureAwait(false);
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Send completed, processing response...");
#endif
            return await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SupabaseSaveSystem] WebGL: Processing response - Result: {req.result}, ResponseCode: {req.responseCode}");
#endif
                if (req.result == UnityWebRequest.Result.Success)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SupabaseSaveSystem] WebGL: Success - returning downloadHandler.data with {req.downloadHandler.data?.Length ?? 0} bytes");
#endif
                    return req.downloadHandler.data;
                }
                else
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log($"[SupabaseSaveSystem] WebGL: Not success - returning null");
#endif
                    return null;
                }
            });
        }

        async Task DeleteObject(string path)
        {
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Delete(
                    $"{url}/storage/v1/object/{bucket}/{Escape(path)}");
                AddAuth(r);
                return r;
            });
            await Send(req, allow404: true).ConfigureAwait(false);
        }

	async Task UploadJson(string path, byte[] bytes)
	{
	    string json = $"\"{Convert.ToBase64String(bytes)}\"";
            await UploadObject(path, Encoding.UTF8.GetBytes(json), "application/json").ConfigureAwait(false);
	}

	async Task<byte[]> DownloadJson(string path)
	{
            byte[] blob = await DownloadObject(path).ConfigureAwait(false);
	    if (blob == null || blob.Length == 0) return null;
	    string txt = Encoding.UTF8.GetString(blob).Trim().Trim('"');
	    return Convert.FromBase64String(txt);
	}

	static string Escape(string p) =>
	    string.Join("/", p.Split('/').Select(Uri.EscapeDataString));

	/// <summary>Current user folder – re-evaluates the resolver each call.</summary>
	string UserDir => liveResolver != null
	    ? liveResolver.ResolveUserFolder()     // e.g. users/<uid>
	    : userDirStatic;                      // Shared / GuidPerDevice / etc.

	void AddAuth(UnityWebRequest r)
	{
	    // 1) apikey is always the project’s public anon key
	    r.SetRequestHeader("apikey", key);

	    // 2) always set Authorization: Bearer <token>
	    //    • if you’ve got a custom JWT resolver, use that
	    //    • otherwise fall back to the anon key
	    string bearer = key;
            if (_cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                _cfg.customUserFolderResolver is IUserAuthorizationResolver customAuth
                && !string.IsNullOrEmpty(customAuth.ResolveAccessKey()))
            {
                bearer = customAuth.ResolveAccessKey();
            }
            r.SetRequestHeader("Authorization", $"Bearer {bearer}");
        }

        static async Task Send(UnityWebRequest r, bool allow404 = false)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Send request starting: {r.method} {r.url}");
#endif
            var op = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                r.timeout = REQUEST_TIMEOUT_SECONDS;
                return r.SendWebRequest();
            });
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Request sent, waiting for completion...");
#endif
            while (!await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => op.isDone))
                await Task.Yield();

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Request completed, checking result...");
#endif
            
            // Get the result values first
            var result = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => r.result);
            var responseCode = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => r.responseCode);
            var error = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => r.error);
            var method = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => r.method);
            var url = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => r.url);

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Response - Result: {result}, Code: {responseCode}, Error: {error}");
            Debug.Log($"[SupabaseSaveSystem] WebGL: Allow404: {allow404}");
#endif

            bool ok = result == UnityWebRequest.Result.Success || responseCode is 201 or 206;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Initial OK: {ok}");
#endif

            if (allow404 && (responseCode == 400 || responseCode == 404))
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SupabaseSaveSystem] WebGL: 400/404 error allowed, setting OK to true");
#endif
                ok = true;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Final OK status: {ok}");
#endif

            if (!ok)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogError($"[SupabaseSaveSystem] WebGL: Throwing IOException: {method} {url} → {responseCode} {error}");
#endif
                throw new IOException($"{method} {url} → {responseCode} {error}");
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Send completed successfully");
#endif
        }

	/* ================================================================== */
	/*  Sync wrappers
	/* ================================================================== */

        public void Save(byte[] d, SaveSlot s)
        {
            _ = Task.Run(() => SaveAsync(d, s));
        }

        public byte[] Load(SaveSlot s)
        {
            return Task.Run(() => LoadAsync(s)).GetAwaiter().GetResult();
        }

        public void Delete(SaveSlot s)
        {
            _ = Task.Run(() => DeleteAsync(s));
        }

    /* ───────────── user-folder strategy (unchanged) ───────────── */

	const string DEVICE_GUID_PREF = "CrystalSave_DeviceGuid";

	static string ResolveUserFolder(SaveSettings cfg)
	{
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: ResolveUserFolder called with strategy: {cfg.userFolderStrategy}");
#endif
            switch (cfg.userFolderStrategy)
            {
                case UserFolderStrategy.Shared:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using Shared strategy, returning 'users/guest'");
#endif
                    return "users/guest";

                case UserFolderStrategy.PublicPerBuild:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using PublicPerBuild strategy");
#endif
#if UNITY_2022_1_OR_NEWER
                    return $"build-{Application.buildGUID}";
#else
                    return $"build-{Application.version}";
#endif
                case UserFolderStrategy.GuidPerDevice:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using GuidPerDevice strategy - accessing PlayerPrefs");
#endif
                    if (!PlayerPrefs.HasKey(DEVICE_GUID_PREF))
                        PlayerPrefs.SetString(DEVICE_GUID_PREF, Guid.NewGuid().ToString("N"));
                    return $"users/{PlayerPrefs.GetString(DEVICE_GUID_PREF)}";

                case UserFolderStrategy.UnityAuthentication:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using UnityAuthentication strategy");
#endif
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                    var auth = Unity.Services.Authentication.AuthenticationService.Instance;
                    return auth.IsSignedIn ? $"users/{auth.PlayerId}" : "users/guest";
#else
                    Debug.LogWarning("[SupabaseSaveSystem] Unity Authentication not present – using guest.");
                    return "users/guest";
#endif
                case UserFolderStrategy.Custom:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using Custom strategy");
#endif
                    if (cfg.customUserFolderResolver is IUserFolderResolver r)
                    {
                        string folder = r.ResolveUserFolder();
                        if (!string.IsNullOrEmpty(folder)) return folder;
                    }
                    Debug.LogWarning("[SupabaseSaveSystem] Custom resolver failed – using guest.");
                    return "users/guest";

                default:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using default strategy, returning 'users/guest'");
#endif
                    return "users/guest";
            }
        }
	}
}
#endif
