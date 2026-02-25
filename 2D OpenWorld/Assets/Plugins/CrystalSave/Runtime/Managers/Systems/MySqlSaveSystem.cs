#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// MySQL backend accessed through a mediating web API.
    /// The client communicates with an HTTP endpoint that performs
    /// all database operations server-side, avoiding direct database
    /// exposure from the game client.
    /// </summary>
    public sealed class MySqlSaveSystem : ISaveSystem
    {
        readonly SaveSettings _cfg;
        readonly string apiUrl;
        readonly string authApiUrl;
        readonly string apiKey;
        readonly string table;
        readonly bool   keepLocalMirror;
        readonly string localFileStem;
        readonly string persistentPath;
        readonly string userIdStatic;
        readonly IUserFolderResolver liveResolver;
        readonly MySqlAuthMode authMode;
    readonly HttpClient httpClient;
    const int DefaultHttpTimeoutSeconds = 15;

        string userIdOverride;
        string UserId => userIdOverride ?? (liveResolver != null ? liveResolver.ResolveUserFolder() : userIdStatic);

    const string LEGACY_META_MASK   = "Slot{0}_Meta.bin";
        const string DEVICE_GUID_PREF = "CrystalSave_DeviceGuid";

        public MySqlSaveSystem(SaveSettings cfg, string persistentPath)
        {
            _cfg = cfg;

            apiUrl        = cfg.mySqlApiUrl?.TrimEnd('/') ?? string.Empty;
            authApiUrl    = string.IsNullOrEmpty(cfg.mySqlAuthApiUrl) ? apiUrl : cfg.mySqlAuthApiUrl.TrimEnd('/');
            apiKey        = cfg.mySqlApiKey;
            table         = cfg.tableName;
            authMode      = cfg.mySqlLoginMode;
            keepLocalMirror = cfg.keepLocalMirror && cfg.saveMethod == SaveMethod.BinaryFileFormat;
            localFileStem   = cfg.saveFileName;
            this.persistentPath  = persistentPath;

            if (cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                cfg.customUserFolderResolver is IUserFolderResolver r)
            {
                liveResolver = r;
            }
            else
            {
                userIdStatic = ResolveUserFolder(cfg);
            }

            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(DefaultHttpTimeoutSeconds);
            if (!string.IsNullOrEmpty(apiKey))
                httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
        }

    // LocalWritesEnabled: local file operations should occur when cloud save is disabled
    // OR when keepLocalMirror is requested while cloud save is enabled.
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
        /* Slot data                                                         */
        /* ================================================================== */

        async Task PostJsonAsync(string endpoint, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = $"{apiUrl}{endpoint}";
            var (ok, code, _, error) = await PostJsonWebGLAsync(url, json);
            if (!ok)
                throw new IOException($"POST {url} → {code} {error}");
#else
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await httpClient.PostAsync($"{apiUrl}{endpoint}", content).ConfigureAwait(false);
#endif
        }

        async Task<(bool ok, long code, string text)> PostJsonForTextAsync(string fullUrl, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, _) = await PostJsonWebGLAsync(fullUrl, json);
            return (ok, code, text);
#else
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await httpClient.PostAsync(fullUrl, content).ConfigureAwait(false);
            string txt = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (resp.IsSuccessStatusCode, (long)resp.StatusCode, txt);
#endif
        }

        async Task<(bool ok, long code, string text)> PostAuthJsonForTextAsync(string endpoint, string json)
        {
            var full = $"{authApiUrl}{endpoint}";
            return await PostJsonForTextAsync(full, json);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        async Task<(bool ok, long code, string text, string error)> PostJsonWebGLAsync(string fullUrl, string json)
        {
            using var req = new UnityWebRequest(fullUrl, UnityWebRequest.kHttpVerbPOST);
            byte[] body = string.IsNullOrEmpty(json) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("X-API-KEY", apiKey);
            req.timeout = DefaultHttpTimeoutSeconds;

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            return (ok, req.responseCode, req.downloadHandler?.text, req.error);
        }

        async Task<(bool ok, long code, string text, string error)> GetTextWebGLAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("X-API-KEY", apiKey);
            req.timeout = DefaultHttpTimeoutSeconds;
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            return (ok, req.responseCode, req.downloadHandler?.text, req.error);
        }

        async Task<(bool ok, long code, byte[] data, string error)> GetBytesWebGLAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("X-API-KEY", apiKey);
            req.timeout = DefaultHttpTimeoutSeconds;
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            return (ok, req.responseCode, req.downloadHandler?.data, req.error);
        }
#endif

        [Serializable]
        class AuthReq
        {
            public string username;
            public string password;
        }

        [Serializable]
        class LoginResp
        {
            public string uid;
        }

    public async Task<bool> SignUpAsync(string username, string password)
    {
        var req = new AuthReq { username = username, password = password };
#if UNITY_WEBGL && !UNITY_EDITOR
    var (ok, _, _) = await PostAuthJsonForTextAsync("/signup", JsonUtility.ToJson(req));
        return ok;
#else
        var (ok, _, _) = await PostAuthJsonForTextAsync("/signup", JsonUtility.ToJson(req));
        return ok;
#endif
    }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var req = new AuthReq { username = username, password = password };
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, _, text) = await PostAuthJsonForTextAsync("/login", JsonUtility.ToJson(req));
            if (!ok) return false;
            var r = JsonUtility.FromJson<LoginResp>(text);
#else
            var (ok, _, text) = await PostAuthJsonForTextAsync("/login", JsonUtility.ToJson(req));
            if (!ok) return false;
            var r = JsonUtility.FromJson<LoginResp>(text);
#endif
            if (r != null && !string.IsNullOrEmpty(r.uid))
            {
                userIdOverride = r.uid;
                return true;
            }
            return false;
        }

        public async Task SaveAsync(byte[] data, SaveSlot slot)
        {
            var req = new SaveReq
            {
                uid  = UserId,
                slot = slot.SlotNumber,
                table = table,
                data = Convert.ToBase64String(data)
            };
            await PostJsonAsync("/save", JsonUtility.ToJson(req));

    #if !UNITY_WEBGL || UNITY_EDITOR
                // Skip local file operations in WebGL builds to prevent freezing
                // WebGL has restricted file system access and sync I/O can cause browser hanging
                if (LocalWritesEnabled())
                {
                    WriteLocalMirror(data, slot);
                }
                else
                {
            UnityEngine.Debug.Log("[MySqlSaveSystem] Local writes disabled by settings; skipping local mirror.");
                }
    #else
                // WebGL: Skipping local file operations to prevent browser freezing
                UnityEngine.Debug.Log("[MySqlSaveSystem] WebGL: Skipped local file operations to prevent freezing");
    #endif

        // Always upload/save metadata (cloud) after blob save so SlotName and fields persist
        await SaveSlotMetadataAsync(slot).ConfigureAwait(false);
        }

        public async Task<byte[]> LoadAsync(SaveSlot slot)
        {
            string url = $"{apiUrl}/load?uid={WebUtility.UrlEncode(UserId)}&slot={slot.SlotNumber}&table={WebUtility.UrlEncode(table)}";
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, err) = await GetTextWebGLAsync(url);
            if (!ok)
                throw new IOException($"GET {url} → {code} {err}");
            string base64 = text;
#else
            string base64 = await httpClient.GetStringAsync(url).ConfigureAwait(false);
#endif
            if (string.IsNullOrEmpty(base64)) return null;
            return Convert.FromBase64String(base64);
        }

        public async Task DeleteAsync(SaveSlot slot)
        {
            var req = new DeleteReq
            {
                uid  = UserId,
                slot = slot.SlotNumber,
                table = table
            };
            await PostJsonAsync("/delete", JsonUtility.ToJson(req));

    #if !UNITY_WEBGL || UNITY_EDITOR
                // Skip local file operations in WebGL builds to prevent freezing
                if (LocalWritesEnabled())
                {
                    await DeleteLocalSlotAsync(slot).ConfigureAwait(false);
                }
    #endif
        }

    public Task DeleteLocalSlotAsync(SaveSlot slot)
        {
            if (keepLocalMirror)
            {
                string sav = Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav");
                if (File.Exists(sav)) File.Delete(sav);

        // Delete both current-pattern and legacy cached metadata files
        string meta = MetaLocalPath(slot.SlotNumber);
        string legacyMeta = LegacyMetaLocalPath(slot.SlotNumber);
        if (File.Exists(meta)) File.Delete(meta);
        if (legacyMeta != meta && File.Exists(legacyMeta)) File.Delete(legacyMeta);

                if (!string.IsNullOrEmpty(slot.ScreenshotFileName))
                {
                    string localScreenshot = Path.Combine(persistentPath, slot.ScreenshotFileName);
                    if (File.Exists(localScreenshot)) File.Delete(localScreenshot);
                }
            }
            return Task.CompletedTask;
        }

        /* ================================================================== */
        /* Metadata                                                          */
        /* ================================================================== */

        public async Task SaveSlotMetadataAsync(SaveSlot slot)
        {
            if (string.IsNullOrEmpty(slot.SlotName))
                slot.SlotName = $"Slot {slot.SlotNumber}";

            var req = new MetaReq
            {
                uid   = UserId,
                slot  = slot.SlotNumber,
                table = table,
                name  = slot.SlotName,
                ticks = slot.LastSaved.Ticks,
                scene = slot.LastActiveScene,
                shot  = slot.ScreenshotFileName
            };

            string json = JsonUtility.ToJson(req);
            string metaJson = DictToJson(slot.CustomMetadata);
            json = json.TrimEnd('}') + ",\"meta\":" + metaJson + "}";
            await PostJsonAsync("/metadata", json);

#if !UNITY_WEBGL || UNITY_EDITOR
            // Skip local metadata file write in WebGL builds to prevent freezing
            WriteLocalMetadata(slot);
#endif
        }

        [Serializable]
        class MetaPair
        {
            public string key;
            public string value;
        }

        [Serializable]
        class SaveReq
        {
            public string uid;
            public int    slot;
            public string table;
            public string data;
        }

        [Serializable]
        class DeleteReq
        {
            public string uid;
            public int    slot;
            public string table;
        }

        [Serializable]
        class MetaReq
        {
            public string uid;
            public int    slot;
            public string table;
            public string name;
            public long   ticks;
            public string scene;
            public string shot;
        }

        [Serializable]
        class SlotMeta
        {
            public int            slot;
            public string         name;
            public long           ticks;
            public string         scene;
            public string         shot;
            public List<MetaPair> meta;
        }

        [Serializable]
        class SlotMetaList
        {
            public SlotMeta[] items;
        }

        static Dictionary<string, string> PairsToDict(List<MetaPair> list)
        {
            var dict = new Dictionary<string, string>();
            if (list == null) return dict;
            foreach (var kv in list)
                dict[kv.key] = kv.value;
            return dict;
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string DictToJson(Dictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return "{}";
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(',');
                sb.Append('"').Append(Escape(kv.Key)).Append('"').Append(':');
                sb.Append('"').Append(Escape(kv.Value)).Append('"');
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }

        public async Task<SaveSlot> LoadSlotMetadataAsync(int num)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Try local cache first (skip in WebGL to prevent file I/O issues)
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
                    catch { }
                }
                else if (File.Exists(legacyLoc))
                {
                    try
                    {
                        var local = SaveDataSerializer.Instance.Deserialize<SaveSlot>(File.ReadAllBytes(legacyLoc));
                        if (local != null) return local;
                    }
                    catch { }
                }
            }
#endif

            string url = $"{apiUrl}/metadata?uid={WebUtility.UrlEncode(UserId)}&slot={num}";
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, err) = await GetTextWebGLAsync(url);
            if (code == 404) return null;
            if (!ok)
                throw new IOException($"GET {url} → {code} {err}");
            string json = text;
#else
            HttpResponseMessage resp = await httpClient.GetAsync(url).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!resp.IsSuccessStatusCode)
                throw new IOException($"GET {url} → {(int)resp.StatusCode} {resp.ReasonPhrase}");
            string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (string.IsNullOrEmpty(json)) return null;
            var meta = JsonUtility.FromJson<SlotMeta>(json);
            if (meta == null) return null;
            var slot = new SaveSlot(
                num,
                string.IsNullOrEmpty(meta.name) ? $"Slot {num}" : meta.name,
                new DateTime(meta.ticks, DateTimeKind.Utc),
                meta.shot ?? string.Empty,
                meta.scene ?? string.Empty);
            slot.CustomMetadata = PairsToDict(meta.meta);
#if !UNITY_WEBGL || UNITY_EDITOR
            // Cache locally for faster access (skip in WebGL to prevent file I/O issues)
            WriteLocalMetadata(slot);
#endif
            return slot;
        }

        public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
        {
            string url = $"{apiUrl}/list?uid={WebUtility.UrlEncode(UserId)}";
            var list = new List<SaveSlot>();
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, err) = await GetTextWebGLAsync(url);
            if (code == 404) return list;
            if (!ok)
                throw new IOException($"GET {url} → {code} {err}");
            string json = text;
#else
            HttpResponseMessage resp = await httpClient.GetAsync(url).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return list;
            if (!resp.IsSuccessStatusCode)
                throw new IOException($"GET {url} → {(int)resp.StatusCode} {resp.ReasonPhrase}");
            string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (string.IsNullOrEmpty(json)) return list;
            var wrapper = JsonUtility.FromJson<SlotMetaList>("{\"items\":" + json + "}");
            if (wrapper?.items == null) return list;
            foreach (var m in wrapper.items)
            {
                var slot = new SaveSlot(
                    m.slot,
                    string.IsNullOrEmpty(m.name) ? $"Slot {m.slot}" : m.name,
                    new DateTime(m.ticks, DateTimeKind.Utc),
                    m.shot ?? string.Empty,
                    m.scene ?? string.Empty);
                slot.CustomMetadata = PairsToDict(m.meta);
                list.Add(slot);
            }
            return list;
        }

        /* ================================================================== */
        /* Metadata helpers                                                  */
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
            string path   = Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav");
            string backup = path + ".bak";
            if (File.Exists(path))
                File.Copy(path, backup, true);
        }

        public void RestoreBackup(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            string path   = Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav");
            string backup = path + ".bak";
            if (File.Exists(backup))
                File.Copy(backup, path, true);
        }

        public void DeleteBackup(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            string backup = Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav.bak");
            if (File.Exists(backup)) File.Delete(backup);
        }

        /* ================================================================== */
        /*  Temporary files (verification without mirror)                     */
        /* ================================================================== */

        string TempPathFor(SaveSlot slot) => Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.tmp");

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
        /*  Sync wrappers                                                     */
        /* ================================================================== */

#if UNITY_WEBGL && !UNITY_EDITOR
    public void   Save  (byte[] d, SaveSlot s) => throw new NotSupportedException("Synchronous Save is not supported on WebGL. Use SaveAsync.");
    public byte[] Load  (SaveSlot s)           => throw new NotSupportedException("Synchronous Load is not supported on WebGL. Use LoadAsync.");
    public void   Delete(SaveSlot s)           => throw new NotSupportedException("Synchronous Delete is not supported on WebGL. Use DeleteAsync.");
#else
    public void   Save  (byte[] d, SaveSlot s) => Task.Run(() => SaveAsync  (d, s)).GetAwaiter().GetResult();
    public byte[] Load  (SaveSlot s)           => Task.Run(() => LoadAsync  (s)).GetAwaiter().GetResult();
    public void   Delete(SaveSlot s)           => Task.Run(() => DeleteAsync(s)).GetAwaiter().GetResult();
#endif

        public string GetSaveGamesPath() => persistentPath;

        /* ───────────── user id helper (same as Supabase) ───────────── */
        static string ResolveUserFolder(SaveSettings cfg)
        {
            switch (cfg.userFolderStrategy)
            {
                case UserFolderStrategy.Shared:
                    return "users/guest";
                case UserFolderStrategy.PublicPerBuild:
#if UNITY_2022_1_OR_NEWER
                    return $"build/{Application.buildGUID}";
#else
                    return "build/unknown";
#endif
                case UserFolderStrategy.GuidPerDevice:
                    string guid = PlayerPrefs.GetString(DEVICE_GUID_PREF, string.Empty);
                    if (string.IsNullOrEmpty(guid))
                    {
                        guid = Guid.NewGuid().ToString("N");
                        PlayerPrefs.SetString(DEVICE_GUID_PREF, guid);
                    }
                    return $"users/{guid}";
                case UserFolderStrategy.UnityAuthentication:
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                    return $"users/{Unity.Services.Authentication.AuthenticationService.Instance.PlayerId}";
#else
                    return "users/guest";
#endif
                case UserFolderStrategy.Custom:
                default:
                    return "users/guest";
            }
        }
        
        public (string apiUrl, string apiKey) GetUploadInfo()
        {
            return (apiUrl, apiKey);
        }

        /// <summary>
        /// Uploads a screenshot for the given slot to the configured MySQL API.
        /// </summary>
        /// <param name="slot">Numeric slot index the image belongs to.</param>
        /// <param name="fileName">Original name of the image file.</param>
        /// <param name="bytes">Encoded image data.</param>
        /// <param name="fmt">Image encoding format.</param>
        public async Task UploadScreenshotAsync(int slot, string fileName, byte[] bytes, ScreenshotFormat fmt)
        {
            string url = $"{apiUrl.TrimEnd('/')}/uploadImage";

            var form = new WWWForm();
            form.AddField("uid",  UserId);
            form.AddField("slot", slot);
            form.AddField("name", fileName);
            form.AddBinaryData("shot", bytes, fileName,
                fmt == ScreenshotFormat.PNG ? "image/png" : "image/jpeg");

            using var req = UnityWebRequest.Post(url, form);
            if (!string.IsNullOrEmpty(apiKey))
                req.SetRequestHeader("X-API-KEY", apiKey);

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new IOException($"POST {url} → {req.responseCode} {req.error}");
        }

        /// <summary>
        /// Downloads a screenshot for the given file name directly from the API server.
        /// </summary>
        /// <param name="fileName">Name of the screenshot file to fetch.</param>
        public async Task<byte[]> DownloadScreenshotAsync(string fileName)
        {
            // Ensure the API URL is treated as a directory so that any path
            // segments (e.g. `/saves`) are preserved when combining with the
            // screenshot location. Without the trailing slash `new Uri(apiUrl)`
            // would consider the last segment a file and strip it, producing
            // URLs like `http://host/img/...` which results in HTTP 404s.
            var baseUri = new Uri(apiUrl.EndsWith("/") ? apiUrl : apiUrl + "/");
            // Screenshots live under {apiUrl}/img/<scope>/<uid>/<filename>
            // where <scope> is either "users" or "build" depending on the
            // configured user-folder strategy.
            // Normalize the UserId so we don't end up with invalid paths like
            // "users/build/<guid>". If the id already starts with "users/" or
            // "build/", use it as-is; otherwise default to "users/<id>".
            string uid = (UserId ?? string.Empty).TrimStart('/');
            bool hasUsers = uid.StartsWith("users/", StringComparison.OrdinalIgnoreCase);
            bool hasBuild = uid.StartsWith("build/", StringComparison.OrdinalIgnoreCase);
            if (!hasUsers && !hasBuild)
                uid = $"users/{uid}";

            var shotUri = new Uri(baseUri, $"img/{uid}/{fileName}");
            Logger.Log($"MySqlSaveSystem: Normalized uid='{uid}', requesting screenshot from '{shotUri}'.", LogCategory.CloudSave, LogLevel.Info);

            
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, bytes, err) = await GetBytesWebGLAsync(shotUri.ToString());
            Logger.Log($"MySqlSaveSystem: Received HTTP {code} for screenshot '{fileName}'.", LogCategory.CloudSave, ok ? LogLevel.Info : LogLevel.Warning);
            if (!ok) return null;
            Logger.Log($"MySqlSaveSystem: Downloaded {bytes?.Length ?? 0} bytes for screenshot '{fileName}'.", LogCategory.CloudSave, LogLevel.Info);
            return bytes;
#else
            HttpResponseMessage resp = await httpClient.GetAsync(shotUri).ConfigureAwait(false);
            Logger.Log($"MySqlSaveSystem: Received HTTP {(int)resp.StatusCode} for screenshot '{fileName}'.", LogCategory.CloudSave, resp.IsSuccessStatusCode ? LogLevel.Info : LogLevel.Warning);
            if (!resp.IsSuccessStatusCode)
                return null;

            byte[] bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            Logger.Log($"MySqlSaveSystem: Downloaded {bytes?.Length ?? 0} bytes for screenshot '{fileName}'.", LogCategory.CloudSave, LogLevel.Info);
            return bytes;
#endif
        }
    }
}
#endif

