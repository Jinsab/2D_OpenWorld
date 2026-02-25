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
	/// REST-only Firebase backend.
	/// • Writes/reads full metadata as JSON via a pure-fields DTO
	/// • Mirrors .sav *and* MemoryPack header when keepLocalMirror = true
	/// • Keeps remote/local file-name scheme aligned (slot-N.sav / .meta.json)
	/// </summary>
	public sealed class FirebaseSaveSystem : ISaveSystem
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

    const string META_EXT         = ".meta.json";        // cloud
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

	/* ───────────── ctor ───────────── */
	public FirebaseSaveSystem(SaveSettings cfg)
	{
	    _cfg = cfg;

	    bucket = cfg.firebaseBucket;
            url    = "https://firebasestorage.googleapis.com/v0/b/" + bucket;
            key    = cfg.firebaseIdToken;

	    /* dynamic folder only for Strategy.Custom */
            if (cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                cfg.customUserFolderResolver is IUserFolderResolver r)
            {
                liveResolver = r;               // keep reference – its value may change later
            }
	    else
	    {
	        userDirStatic = ResolveUserFolder(cfg);
	    }

	    useJson  = cfg.cloudSaveTransport == CloudSaveTransport.JSON;
	    dataExt  = useJson ? ".json" : ".sav";

            keepLocalMirror = cfg.keepLocalMirror &&
                              cfg.saveMethod == SaveMethod.BinaryFileFormat;
            localFileStem   = cfg.saveFileName;
            persistentPath  = Application.persistentDataPath;
        }

        // LocalWritesEnabled: perform local file operations when cloud save is disabled
        // or when keepLocalMirror is requested.
        bool LocalWritesEnabled() => !_cfg.enableCloudSave || keepLocalMirror;

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

	/* ================================================================== */
	/* Slot data
	/* ================================================================== */

	public async Task SaveAsync(byte[] data, SaveSlot slot)
	{
	    // 1 ─ blob
            if (useJson)
                await UploadJson(PathFor(slot, dataExt), data).ConfigureAwait(false);
            else
                await UploadObject(PathFor(slot, dataExt), data, "application/octet-stream").ConfigureAwait(false);

    // 2 ─ optional local .sav mirror
        if (LocalWritesEnabled())
        {
            WriteLocalMirror(data, slot);
        }

    // 3 ─ Always upload metadata to cloud (and cache locally when supported)
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

    // local mirrors
            if (LocalWritesEnabled())
            {
                string sav = Path.Combine(persistentPath,
                                           $"{localFileStem}{s.SlotNumber}.sav");
                if (File.Exists(sav)) File.Delete(sav);

        // Delete current and legacy cached metadata
        string meta = MetaLocalPath(s.SlotNumber);
        string legacyMeta = LegacyMetaLocalPath(s.SlotNumber);
        if (File.Exists(meta)) File.Delete(meta);
        if (legacyMeta != meta && File.Exists(legacyMeta)) File.Delete(legacyMeta);

                if (!string.IsNullOrEmpty(s.ScreenshotFileName))
                {
                    string localScreenshot = Path.Combine(persistentPath, s.ScreenshotFileName);
                    if (File.Exists(localScreenshot))
                        File.Delete(localScreenshot);
                }
            }
        }

        public Task DeleteLocalSlotAsync(SaveSlot s)
        {
            if (LocalWritesEnabled())
            {
                string sav = Path.Combine(persistentPath,
                                           $"{localFileStem}{s.SlotNumber}.sav");
                if (File.Exists(sav)) File.Delete(sav);

                // Delete current and legacy cached metadata
                string meta = MetaLocalPath(s.SlotNumber);
                string legacyMeta = LegacyMetaLocalPath(s.SlotNumber);
                if (File.Exists(meta)) File.Delete(meta);
                if (legacyMeta != meta && File.Exists(legacyMeta)) File.Delete(legacyMeta);

                if (!string.IsNullOrEmpty(s.ScreenshotFileName))
                {
                    string localScreenshot = Path.Combine(persistentPath, s.ScreenshotFileName);
                    if (File.Exists(localScreenshot)) File.Delete(localScreenshot);
                }
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

	    // cloud JSON – all members survive because DTO uses fields
	    var dto = new SaveSlotDTO {
	        SlotNumber         = slot.SlotNumber,
	        SlotName           = slot.SlotName,
	        ScreenshotFileName = slot.ScreenshotFileName,
	        LastActiveScene    = slot.LastActiveScene,
	        LastSavedTicks     = slot.LastSaved.Ticks
	    };
	    string json = JsonUtility.ToJson(dto);
            await UploadObject(PathFor(slot, META_EXT),
                               Encoding.UTF8.GetBytes(json),
                               "application/json").ConfigureAwait(false);

	    // local MemoryPack mirror (only when keepLocalMirror)
	    WriteLocalMetadata(slot);
	}

	public async Task<SaveSlot> LoadSlotMetadataAsync(int num)
	{
	    /* 1 ─ try local header first */
            if (keepLocalMirror)
            {
                string loc = MetaLocalPath(num);
                string legacyLoc = LegacyMetaLocalPath(num);
                if (File.Exists(loc))
                {
                    try
                    {
                        var local = SaveDataSerializer.Instance.Deserialize<SaveSlot>(File.ReadAllBytes(loc));
                        if (local != null) return local;
                    }
                    catch { /* fall through to cloud */ }
                }
                else if (File.Exists(legacyLoc))
                {
                    try
                    {
                        var local = SaveDataSerializer.Instance.Deserialize<SaveSlot>(File.ReadAllBytes(legacyLoc));
                        if (local != null) return local;
                    }
                    catch { /* fall through to cloud */ }
                }
            }

	    /* 2 ─ cloud JSON */
            byte[] bytes = await DownloadObject(PathFor(num, META_EXT)).ConfigureAwait(false);
	    if (bytes == null || bytes.Length == 0) return null;

	    SaveSlotDTO dto = JsonUtility.FromJson<SaveSlotDTO>(Encoding.UTF8.GetString(bytes));
	    var slot = new SaveSlot(
	        dto.SlotNumber,
	        dto.SlotName,
	        new DateTime(dto.LastSavedTicks, DateTimeKind.Utc),
	        dto.ScreenshotFileName,
	        dto.LastActiveScene);

	    // cache for next start if mirror off
	    WriteLocalMetadata(slot);
	    return slot;
	}

	/* list remote slots – now parses DTOs so every field stays intact */
	public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
	{
	    var list = new List<SaveSlot>();
	    string pfx = $"{UserDir}/";

	    var req = UnityWebRequest.Get($"{url}/o?prefix={Escape(pfx)}");
	    AddAuth(req);
            await Send(req, allow404: true).ConfigureAwait(false);
            bool reqOk = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                req.result == UnityWebRequest.Result.Success);
            if (!reqOk) return list;

            var wrap = JsonUtility.FromJson<ResponseWrap>(req.downloadHandler.text);
	    foreach (var itm in wrap.items)
	    {
	        if (!itm.name.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)) continue;

	        var mReq = UnityWebRequest.Get($"{url}/o/{Escape(itm.name)}?alt=media");
	        AddAuth(mReq);
                await Send(mReq).ConfigureAwait(false);
                bool metaOk = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                    mReq.result == UnityWebRequest.Result.Success);
                if (!metaOk) continue;

	        SaveSlotDTO dto = JsonUtility.FromJson<SaveSlotDTO>(mReq.downloadHandler.text);
	        if (dto.SlotNumber < 1) continue;

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

        void WriteLocalMetadata(SaveSlot s)
        {
            if (!LocalWritesEnabled()) return;
            string path = MetaLocalPath(s.SlotNumber);
            File.WriteAllBytes(path, SaveDataSerializer.Instance.Serialize(s));
        }

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
            await UploadObject($"{UserDir}/{file}", bytes, mime).ConfigureAwait(false);
        }

        public async Task<byte[]> DownloadScreenshotAsync(string file)
        {
            return await DownloadObject($"{UserDir}/{file}").ConfigureAwait(false);
        }

        public async Task DeleteScreenshotAsync(string file)
        {
            await DeleteObject($"{UserDir}/{file}").ConfigureAwait(false);
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
                var r = UnityWebRequest.Put($"{url}/o?name={Escape(path)}&uploadType=media", data);
                r.method = UnityWebRequest.kHttpVerbPOST;
                r.SetRequestHeader("Content-Type", mime);
                AddAuth(r);
                return r;
            });
            await Send(req).ConfigureAwait(false);
        }

        async Task<byte[]> DownloadObject(string path)
        {
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Get($"{url}/o/{Escape(path)}?alt=media");
                AddAuth(r);
                return r;
            });
            await Send(req, allow404: true).ConfigureAwait(false);
            return await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null);
        }

        async Task DeleteObject(string path)
        {
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Delete($"{url}/o/{Escape(path)}");
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
            string bearer = key;
            if (_cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                _cfg.customUserFolderResolver is IUserAuthorizationResolver customAuth &&
                !string.IsNullOrEmpty(customAuth.ResolveAccessKey()))
            {
                bearer = customAuth.ResolveAccessKey();
            }
            if (!string.IsNullOrEmpty(bearer))
                r.SetRequestHeader("Authorization", $"Bearer {bearer}");
        }

static async Task Send(UnityWebRequest r, bool allow404 = false)
        {
            var op = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                r.timeout = REQUEST_TIMEOUT_SECONDS;
                return r.SendWebRequest();
            });
            while (!await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => op.isDone))
                await Task.Yield();

            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                bool ok = r.result == UnityWebRequest.Result.Success ||
                          r.responseCode is 201 or 206;

                if (allow404 && (r.responseCode == 400 || r.responseCode == 404))
                    ok = true;

                if (!ok)
                    throw new IOException($"{r.method} {r.url} → {r.responseCode} {r.error}");
            });
        }

	/* ================================================================== */
	/*  Sync wrappers
	/* ================================================================== */

    public void   Save  (byte[] d, SaveSlot s) => Task.Run(() => SaveAsync  (d, s)).GetAwaiter().GetResult();
    public byte[] Load  (SaveSlot s)           => Task.Run(() => LoadAsync  (s)).GetAwaiter().GetResult();
    public void   Delete(SaveSlot s)           => Task.Run(() => DeleteAsync(s)).GetAwaiter().GetResult();

	/* ================================================================== */
	/*
	/* ================================================================== */

	string PathFor(SaveSlot s, string ext) => $"{UserDir}/{localFileStem}{s.SlotNumber}{ext}";
	string PathFor(int slot,   string ext) => $"{UserDir}/{localFileStem}{slot}{ext}";

	/* ───────────── user-folder strategy (unchanged) ───────────── */

	const string DEVICE_GUID_PREF = "CrystalSave_DeviceGuid";

	static string ResolveUserFolder(SaveSettings cfg)
	{
            switch (cfg.userFolderStrategy)
            {
                case UserFolderStrategy.Shared:
                    return "users/guest";

                case UserFolderStrategy.PublicPerBuild:
#if UNITY_2022_1_OR_NEWER
                    return $"build-{Application.buildGUID}";
#else
                    return $"build-{Application.version}";
#endif
                case UserFolderStrategy.GuidPerDevice:
                    if (!PlayerPrefs.HasKey(DEVICE_GUID_PREF))
                        PlayerPrefs.SetString(DEVICE_GUID_PREF, Guid.NewGuid().ToString("N"));
                    return $"users/{PlayerPrefs.GetString(DEVICE_GUID_PREF)}";

                case UserFolderStrategy.UnityAuthentication:
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                    var auth = Unity.Services.Authentication.AuthenticationService.Instance;
                    return auth.IsSignedIn ? $"users/{auth.PlayerId}" : "users/guest";
#else
                    Debug.LogWarning("[FirebaseSaveSystem] Unity Authentication not present – using guest.");
                    return "users/guest";
#endif
                case UserFolderStrategy.Custom:
                    if (cfg.customUserFolderResolver is IUserFolderResolver r)
                    {
                        string folder = r.ResolveUserFolder();
                        if (!string.IsNullOrEmpty(folder)) return folder;
                    }
                    Debug.LogWarning("[FirebaseSaveSystem] Custom resolver failed – using guest.");
                    return "users/guest";

                default:
                    return "users/guest";
            }
        }
	}
}
#endif
