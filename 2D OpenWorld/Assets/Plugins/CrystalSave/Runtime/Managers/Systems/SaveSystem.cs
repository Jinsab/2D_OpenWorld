#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Collections;
#if REMEMBERME_CLOUDSAVE_PRESENT
using Unity.Services.CloudSave;
#endif
using System.Linq;
#if REMEMBERME_CLOUDSAVE_PRESENT
using PlayerDeleteOptions = Unity.Services.CloudSave.Models.Data.Player.DeleteOptions;
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
#endif
using UnityEngine;
using System.Text.RegularExpressions;

namespace Arawn.CrystalSave.Runtime
{
	public class SaveSystem : ISaveSystem
	{
                private readonly SaveSettings settings;
                private readonly string persistentPath;
#if REMEMBERME_CLOUDSAVE_PRESENT
	private const bool CLOUD_SDK_PRESENT = true;
#else
	private const bool CLOUD_SDK_PRESENT = false;
#endif

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
		static bool SignedIn =>
			Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
#else
	// keep it a property so IntelliSense / LINQ analyzers are happy
		static bool SignedIn => false;       // compile-time fallback
#endif

                public SaveSystem(SaveSettings saveSettings, string rootPath)
                {
                        settings = saveSettings;
                        persistentPath = rootPath;
                        if (settings.enableCloudSave &&
                            settings.cloudCryptoMode == CloudCryptoMode.ServerSide &&
                            settings.keepLocalMirror)
                        {
                                Logger.Log("Server-side cloud crypto enabled; Keep Local Mirror is ignored to avoid local disk writes.",
                                           LogCategory.SaveSystem, LogLevel.Warning);
                        }
#if UNITY_WEBGL && !UNITY_EDITOR
                        // Let Unity handle initial IDBFS population.
                        // ManualSync() is gated by settings.useManualSync and Unity version.
                        ManualSync(true);
#endif
                }

		// Convenience: “are we allowed to touch the local mirror?”
                private bool UseLocalMirror =>
                        !settings.enableCloudSave ||          // classic offline modes
                        (settings.enableCloudSave &&
                         settings.keepLocalMirror &&
                         settings.cloudCryptoMode != CloudCryptoMode.ServerSide);

                private static string Sanitize(string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return key;
        }

        // Local helpers to resolve {n} and {meta:key} placeholders and to build globs
        private static string SanitizeFilePart(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private static string ResolvePattern(string pattern, SaveSlot slot)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;
            if (slot == null) return SanitizeFilePart(pattern);

            string resolved = pattern.Replace("{n}", slot.SlotNumber.ToString());
            resolved = Regex.Replace(resolved, "\\{meta:([^}]+)\\}", m =>
            {
                string key = m.Groups[1].Value;
                string val = null;
                if (slot.CustomMetadata != null && slot.CustomMetadata.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                    val = v;
                return SanitizeFilePart(val ?? string.Empty);
            });
            return SanitizeFilePart(resolved);
        }

        private static string PatternToGlob(string pattern, int slotNumber)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;
            string glob = pattern.Replace("{n}", slotNumber.ToString());
            glob = Regex.Replace(glob, "\\{meta:([^}]+)\\}", "*");
            return glob;
        }

        private string SlotFolder(int slotNumber)
        {
            return Path.Combine(persistentPath, $"slot{slotNumber}");
        }

        private string EnsureSlotFolder(int slotNumber)
        {
            string dir = SlotFolder(slotNumber);
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch {}
            return dir;
        }

        // Resolve save key with optional {n} placeholder; fallback to legacy concatenation
        private string ResolveSaveKey(int slot)
        {
            string k = settings.saveKey ?? string.Empty;
            return k.Contains("{n}") ? k.Replace("{n}", slot.ToString()) : k + slot;
        }

        // Resolve save file stem (without extension) with optional {n} placeholder
        private string ResolveSaveFileStem(int slot)
        {
            string stem = settings.saveFileName ?? string.Empty;
            return stem.Contains("{n}") ? stem.Replace("{n}", slot.ToString()) : stem + slot;
        }

    private string ResolveSaveFileStem(SaveSlot slot)
        {
            string stem = settings.saveFileName ?? string.Empty;
            // Resolve meta placeholders regardless; we handle {n} legacy below
            string resolved = ResolvePattern(stem, slot);
            if (stem.Contains("{n}")) return resolved;
            // Legacy behavior: no {n} provided, append slot number
            return resolved + slot.SlotNumber;
    }

        // Resolve a Cloud Save key from SaveSettings.saveKey allowing {n} and {meta:key}.
        // Then sanitize to allowed characters for Unity Cloud Save Data/Files APIs.
        private string ResolveCloudKey(SaveSlot slot)
        {
            string pattern = settings.saveKey ?? string.Empty;
            string resolved = ResolvePattern(pattern, slot);
            if (!pattern.Contains("{n}"))
            {
                // Legacy behavior: append slot number when {n} is missing
                resolved += slot.SlotNumber.ToString();
            }
            // Cloud Save keys must avoid spaces and special characters.
            // Allow letters, digits, dot, underscore, hyphen; replace others with '_'.
            string sanitized = Regex.Replace(resolved, "[^A-Za-z0-9_.-]", "_");
            // Trim to a reasonable length if needed (defensive; typical limits ~255)
            if (sanitized.Length > 240) sanitized = sanitized.Substring(0, 240);
            return sanitized;
        }

        private static string CloudSanitize(string s)
        {
            s = s ?? string.Empty;
            string sanitized = Regex.Replace(s, "[^A-Za-z0-9_.-]", "_");
            return sanitized.Length > 240 ? sanitized.Substring(0, 240) : sanitized;
        }

#if REMEMBERME_CLOUDSAVE_PRESENT
        static bool IsNotFound(CloudSaveException e)
        {
            var statusCodeProp = e.GetType().GetProperty("StatusCode");
            if (statusCodeProp != null && statusCodeProp.GetValue(e) is int code && code == 404)
                return true;

            var reasonProp = e.GetType().GetProperty("Reason");
            var reasonStr = reasonProp?.GetValue(e)?.ToString();
            if (!string.IsNullOrEmpty(reasonStr) &&
                reasonStr.IndexOf("notfound", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (e.Message != null &&
                (e.Message.Contains("404") || e.Message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            return false;
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        private bool UseFileSystemAccess =>
            settings.saveMethod == SaveMethod.BinaryFileFormat ||
            settings.enableSaveFileVerification ||
            (settings.enableCloudSave && settings.keepLocalMirror);

        private void ManualSync(bool populate)
        {
#if !UNITY_2023_1_OR_NEWER
            if (settings.useManualSync)
            {
                Debug.LogWarning("Manual FS.syncfs is deprecated and will be removed in a future Unity version.");
                WebGLFileSystem.Sync(populate);
            }
#endif
        }

        private async Task ManualSyncAsync(bool populate)
        {
#if !UNITY_2023_1_OR_NEWER
            if (settings.useManualSync)
            {
                Debug.LogWarning("Manual FS.syncfs is deprecated and will be removed in a future Unity version.");
                await WebGLFileSystem.SyncAsync(populate);
            }
#else
            await Task.CompletedTask;
#endif
        }

        private async Task WriteAllBytesAsync(string path, byte[] data)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllBytes(path, data);
                await ManualSyncAsync(false);
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Task.Run is not supported on WebGL builds, perform the
                // file write synchronously instead and yield once so the
                // async signature is preserved.
                File.WriteAllBytes(path, data);
                await Task.Yield();
#else
                await Task.Run(() => File.WriteAllBytes(path, data));
#endif
            }
        }

        private void WriteAllBytes(string path, byte[] data)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllBytes(path, data);
                ManualSync(false);
            }
            else
            {
                File.WriteAllBytes(path, data);
            }
        }

        private async Task WriteAllTextAsync(string path, string text)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllText(path, text);
                await ManualSyncAsync(false);
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Task.Run is not available in WebGL builds so execute
                // the write synchronously and yield control back to the
                // caller to avoid blocking.
                File.WriteAllText(path, text);
                await Task.Yield();
#else
                await Task.Run(() => File.WriteAllText(path, text));
#endif
            }
        }

        private void WriteAllText(string path, string text)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllText(path, text);
                ManualSync(false);
            }
            else
            {
                File.WriteAllText(path, text);
            }
        }

        private async Task<byte[]> ReadAllBytesAsync(string path)
        {
            if (UseFileSystemAccess)
            {
                await ManualSyncAsync(true);
                return WebGLFileSystem.ReadAllBytes(path);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            // Reading via Task.Run is unsupported on WebGL; read
            // synchronously instead.
            return File.ReadAllBytes(path);
#else
            return await Task.Run(() => File.ReadAllBytes(path));
#endif
        }

        private byte[] ReadAllBytes(string path)
        {
            if (UseFileSystemAccess)
            {
                ManualSync(true);
                return WebGLFileSystem.ReadAllBytes(path);
            }
            return File.ReadAllBytes(path);
        }

        private async Task<string> ReadAllTextAsync(string path)
        {
            if (UseFileSystemAccess)
            {
                await ManualSyncAsync(true);
                return WebGLFileSystem.ReadAllText(path);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            // Task.Run is not supported on WebGL; perform a synchronous
            // read and return the result directly.
            return File.ReadAllText(path);
#else
            return await Task.Run(() => File.ReadAllText(path));
#endif
        }
        private string ReadAllText(string path)
        {
            if (UseFileSystemAccess)
            {
                ManualSync(true);
                return WebGLFileSystem.ReadAllText(path);
            }
            return File.ReadAllText(path);
        }

        private async Task<bool> FileExistsAsync(string path)
        {
            if (UseFileSystemAccess)
            {
                await ManualSyncAsync(true);
                return WebGLFileSystem.Exists(path);
            }
            return File.Exists(path);
        }

        private bool FileExists(string path)
        {
            if (UseFileSystemAccess)
            {
                ManualSync(true);
                return WebGLFileSystem.Exists(path);
            }
            return File.Exists(path);
        }

        private void DeleteFile(string path)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.Delete(path);
                ManualSync(false);
            }
            else if (File.Exists(path)) File.Delete(path);
        }

        private void CopyFile(string source, string dest, bool overwrite)
        {
            if (UseFileSystemAccess)
            {
                if (!overwrite && WebGLFileSystem.Exists(dest)) return;
                var bytes = WebGLFileSystem.ReadAllBytes(source);
                if (bytes != null) WebGLFileSystem.WriteAllBytes(dest, bytes);
                ManualSync(false);
            }
            else
            {
                File.Copy(source, dest, overwrite);
            }
        }
#else
        private Task WriteAllBytesAsync(string path, byte[] data)
            => Task.Run(() => File.WriteAllBytes(path, data));

        private void WriteAllBytes(string path, byte[] data)
            => File.WriteAllBytes(path, data);

        private Task WriteAllTextAsync(string path, string text)
            => Task.Run(() => File.WriteAllText(path, text));

        private void WriteAllText(string path, string text)
            => File.WriteAllText(path, text);

        private Task<byte[]> ReadAllBytesAsync(string path)
            => Task.Run(() => File.ReadAllBytes(path));

        private byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        private Task<string> ReadAllTextAsync(string path)
            => Task.Run(() => File.ReadAllText(path));

        private string ReadAllText(string path) => File.ReadAllText(path);

        private Task<bool> FileExistsAsync(string path)
            => Task.Run(() => File.Exists(path));

        private bool FileExists(string path) => File.Exists(path);

        private void DeleteFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private void CopyFile(string source, string dest, bool overwrite)
            => File.Copy(source, dest, overwrite);
#endif

		private Task<string> GetSaveFilePathAsync(string fileName) =>
			UnityMainThreadDispatcher.Instance().EnqueueAsync(
				() => Path.Combine(persistentPath, fileName));

        // Helper: run an async function on the Unity main thread and await its completion
        private static Task RunOnMainThreadAsync(Func<Task> asyncAction)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityMainThreadDispatcher.Instance().Enqueue(Main());
            IEnumerator Main()
            {
                Task t = null;
                try { t = asyncAction(); }
                catch (Exception ex) { tcs.TrySetException(ex); yield break; }
                while (t != null && !t.IsCompleted) yield return null;
                if (t == null) tcs.TrySetResult(true);
                else if (t.IsFaulted) tcs.TrySetException(t.Exception?.InnerException ?? t.Exception);
                else if (t.IsCanceled) tcs.TrySetCanceled();
                else tcs.TrySetResult(true);
            }
            return tcs.Task;
        }

        // Helper: run an async function returning T on the Unity main thread and await its result
        private static Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> asyncFunc)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityMainThreadDispatcher.Instance().Enqueue(Main());
            IEnumerator Main()
            {
                Task<T> t = null;
                try { t = asyncFunc(); }
                catch (Exception ex) { tcs.TrySetException(ex); yield break; }
                while (t != null && !t.IsCompleted) yield return null;
                if (t == null) tcs.TrySetResult(default);
                else if (t.IsFaulted) tcs.TrySetException(t.Exception?.InnerException ?? t.Exception);
                else if (t.IsCanceled) tcs.TrySetCanceled();
                else tcs.TrySetResult(t.Result);
            }
            return tcs.Task;
        }

        private string GetSlotMetadataFileName(int n)
        {
            string pattern = settings.metadataFileNamePattern;
            if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains("{n}"))
                pattern = "Slot{n}_Meta.bin"; // legacy default
            // No SaveSlot instance here; only replace {n}
            return pattern.Replace("{n}", n.ToString());
        }

        private string GetSlotMetadataFileName(SaveSlot slot)
        {
            string pattern = settings.metadataFileNamePattern;
            if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains("{n}"))
                pattern = "Slot{n}_Meta.bin"; // legacy default
            // Resolve {n} and {meta:key}
            return ResolvePattern(pattern, slot);
        }
    public async Task SaveAsync(byte[] data, SaveSlot slot)
	{
	    try
	    {
            string key     = ResolveCloudKey(slot);
            string safeKey = Sanitize(ResolveSaveKey(slot.SlotNumber));

	        /* 0  EARLY-OUT WHEN NOT SIGNED-IN */
	        bool useRemote = settings.enableCloudSave && SignedIn;
	        if (settings.enableCloudSave && !SignedIn)
	            Logger.Log("Cloud Save enabled but player not signed-in – " +
	                       "skipping remote upload and using local mirror (Ignores disabled Keep Local Mirror).",
	                       LogCategory.SaveSystem,
	                       LogLevel.Warning);

	        /* 1 CLOUD (only when allowed) OR ON-DISK FALLBACK */
                if (useRemote)
               {
                   switch (settings.cloudSaveTransport)
                   {
                       case CloudSaveTransport.Binary:
                       {
#if REMEMBERME_CLOUDSAVE_PRESENT
#if UNITY_WEBGL && !UNITY_EDITOR
                            Logger.Log("WebGL detected – forcing Data.Player.SaveAsync for binary cloud save.", LogCategory.SaveSystem, LogLevel.Info);
                            string b64 = Convert.ToBase64String(data);
                            await RunOnMainThreadAsync(async () =>
                                await UnityCloudSaveService.Instance.Data.Player
                                     .SaveAsync(new Dictionary<string, object> { { key, b64 } }));
#else
                            await RunOnMainThreadAsync(async () =>
                                await UnityCloudSaveService.Instance.Files.Player.SaveAsync(key, data));
#endif
#endif
                            break;
                       }

                       case CloudSaveTransport.JSON:
                       {
#if REMEMBERME_CLOUDSAVE_PRESENT
                            string b64 = Convert.ToBase64String(data);
                            await RunOnMainThreadAsync(async () =>
                                await UnityCloudSaveService.Instance.Data.Player
                                     .SaveAsync(new Dictionary<string, object>{{ key, b64 }}));
#endif
                            break;
                       }
                   }
               }
	        else if (settings.enableCloudSave)      // local fallback blob/json
	        {
	            switch (settings.cloudSaveTransport)
                    {
                        case CloudSaveTransport.Binary:
                            string blobPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".sav");
                            await WriteAllBytesAsync(blobPath, data);
                            break;

                        case CloudSaveTransport.JSON:
                            string jsonPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".json");
                            string b64Local = Convert.ToBase64String(data);   // single conversion
                            await WriteAllTextAsync(jsonPath, $"\"{b64Local}\"");
                            break;
                    }
                }

	        /* 2 LOCAL MIRROR (unchanged) */
	        if (UseLocalMirror)
	        {
                switch (settings.saveMethod)
	            {
	                case SaveMethod.PlayerPrefs:
	                    await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
	                    {
                            PlayerPrefs.SetString(ResolveSaveKey(slot.SlotNumber),
	                                              Convert.ToBase64String(data));
	                        PlayerPrefs.Save();
	                    });
	                    break;

                        case SaveMethod.BinaryFileFormat:
                       string mirrorPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav");
                            await WriteAllBytesAsync(mirrorPath, data);
                            // After successful write, delete older variants produced by metadata changes
                            try
                            {
                                string stemPattern = settings.saveFileName ?? string.Empty;
                                string glob = PatternToGlob(stemPattern, slot.SlotNumber) + ".sav";
                           foreach (var f in Directory.GetFiles(SlotFolder(slot.SlotNumber), glob))
                                {
                                    if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(mirrorPath), StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (FileExists(f)) DeleteFile(f);
                                    }
                                }
                                // Also remove legacy root-level files
                                try
                                {
                                    foreach (var f in Directory.GetFiles(persistentPath, glob))
                                        if (FileExists(f)) DeleteFile(f);
                                }
                                catch { }
                            }
                            catch {}
                            break;
                    }
                }

	        // Always persist metadata locally so screenshot information
	        // survives even when the local mirror is disabled. This mirrors
	        // the previous behaviour before the refactor and ensures
	        // screenshots remain linked after restarting the game.
	        await SaveSlotMetadataAsync(slot);
	    }
	    catch (Exception ex)
	    {
	        Logger.Log($"SaveAsync failed for slot {slot.SlotNumber}: {ex}", LogCategory.SaveSystem, LogLevel.Error);
	        throw;
	    }
	}

		/// <summary>
		/// Asynchronously loads the serialized data from a specific save slot using the configured method.
		/// </summary>
        public async Task<byte[]> LoadAsync(SaveSlot slot)
    {
        try
        {
            string key     = ResolveCloudKey(slot);
            string safeKey = Sanitize(ResolveSaveKey(slot.SlotNumber));

            bool useRemote = settings.enableCloudSave && SignedIn;
            if (settings.enableCloudSave && !SignedIn)
                Logger.Log("Cloud Save load skipped – player not signed-in. Trying local candidates.", LogCategory.SaveSystem, LogLevel.Info);

            // Load both candidates (cloud/local) when auto-conflict is enabled so we can arbitrate.
            byte[] cloudBytes = null;
            byte[] localBytes = null;

            // Kick off independent reads where possible
            Task<byte[]> cloudTask = null;
            if (useRemote)
                cloudTask = LoadCloudBytesAsync(key);

            Task<byte[]> localTask = LoadLocalCandidateBytesAsync(slot, safeKey);

            if (cloudTask != null) cloudBytes = await cloudTask;
            localBytes = await localTask;

            // Apply Auto Conflict Policy when both sides are present
            if (settings.enableCloudSave && settings.autoResolveConflicts && cloudBytes != null && localBytes != null)
            {
                var enc  = SaveManager.Instance?.EncryptionService;
                var comp = SaveManager.Instance?.CompressionService;

                DateTime localTs = default, cloudTs = default;
                SaveData localData = null, cloudData = null;
                try
                {
                    byte[] b = localBytes;
                    if (enc?.UseEncryption == true) b = enc.MaybeDecrypt(b);
                    b = comp?.MaybeDecompress(b) ?? b;
                    localData = SaveDataSerializer.Instance.Deserialize<SaveData>(b);
                    localTs = localData?.LastSaved ?? default;
                }
                catch { }
                try
                {
                    byte[] b = cloudBytes;
                    if (enc?.UseEncryption == true) b = enc.MaybeDecrypt(b);
                    b = comp?.MaybeDecompress(b) ?? b;
                    cloudData = SaveDataSerializer.Instance.Deserialize<SaveData>(b);
                    cloudTs = cloudData?.LastSaved ?? default;
                }
                catch { }

                bool chooseLocal = false;
                switch (settings.autoConflictPolicy)
                {
                    case AutoConflictPolicy.Latest:
                        chooseLocal = localTs >= cloudTs; break;
                    case AutoConflictPolicy.Oldest:
                        chooseLocal = localTs <= cloudTs; break;
                    case AutoConflictPolicy.LocalWins:
                        chooseLocal = true; break;
                    case AutoConflictPolicy.CloudWins:
                        chooseLocal = false; break;
                    case AutoConflictPolicy.Custom:
                        // Minimal custom support: prefer rule evaluation similar to SaveOperationService; if undecided, fall back to Latest
                        try
                        {
                            var localMeta = await LoadLocalSlotMetadataOnlyAsync(slot.SlotNumber) ?? new SaveSlot { SlotNumber = slot.SlotNumber, CustomMetadata = new Dictionary<string,string>() };
                            var cloudMeta = await LoadCloudSlotMetadataOnlyAsync(slot.SlotNumber) ?? new SaveSlot { SlotNumber = slot.SlotNumber, CustomMetadata = new Dictionary<string,string>() };
                            bool localMatch = MatchesRules(localMeta.CustomMetadata ?? new Dictionary<string,string>(), settings.metadataRules, localData, cloudData, true);
                            bool cloudMatch = MatchesRules(cloudMeta.CustomMetadata ?? new Dictionary<string,string>(), settings.metadataRules, localData, cloudData, false);
                            if (localMatch != cloudMatch)
                                chooseLocal = localMatch;
                            else
                                chooseLocal = localTs >= cloudTs; // tie-breaker: Latest
                        }
                        catch { chooseLocal = localTs >= cloudTs; }
                        break;
                }

                var chosen = chooseLocal ? localBytes : cloudBytes;

                // Backfill: keep sources in sync per policy
                if (!chooseLocal && UseLocalMirror)
                {
                    // Chose cloud → refresh local mirror only (no re-upload)
                    await BackfillLocalMirrorAsync(slot, chosen);
                }
                else if (chooseLocal && useRemote)
                {
                    // Chose local while cloud available → schedule upload on main thread (no background thread)
                    UnityMainThreadDispatcher.Instance().Enqueue(async () =>
                    {
                        try { await SaveAsync(localBytes, slot); }
                        catch (Exception ex) { Logger.Log($"Background backfill SaveAsync failed: {ex.Message}", LogCategory.SaveSystem, LogLevel.Warning); }
                    });
                }

                return chosen;
            }

            // If only one side exists, return it (cloud preferred when present and signed-in)
            if (cloudBytes != null)
            {
                if (UseLocalMirror)
                    await BackfillLocalMirrorAsync(slot, cloudBytes);
                return cloudBytes;
            }

            if (localBytes != null)
                return localBytes;

            return null; // nothing found
        }
        catch (Exception ex)
        {
            Logger.Log($"LoadAsync failed for slot {slot.SlotNumber}: {ex}", LogCategory.SaveSystem, LogLevel.Error);
            throw;
        }
    }

    // Load the best available local candidate: mirror (PlayerPrefs/File) or cloud fallbacks (.sav/.json) if present
    private async Task<byte[]> LoadLocalCandidateBytesAsync(SaveSlot slot, string safeKey)
    {
        // 1) Local mirror
        if (UseLocalMirror)
        {
            if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string base64 = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                {
                    string k = ResolveSaveKey(slot.SlotNumber);
                    return PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null;
                });
                if (!string.IsNullOrEmpty(base64))
                    return Convert.FromBase64String(base64);
            }
            else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
            {
                string folder = EnsureSlotFolder(slot.SlotNumber);
                string mirrorPath = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
                if (await FileExistsAsync(mirrorPath))
                    return await ReadAllBytesAsync(mirrorPath);
                // Try latest variant via glob and legacy root-level fallbacks
                try
                {
                    string stemPattern = settings.saveFileName ?? string.Empty;
                    string glob = PatternToGlob(stemPattern, slot.SlotNumber) + ".sav";
                    var matches = Directory.GetFiles(folder, glob);
                    if (matches != null && matches.Length > 0)
                    {
                        string latest = matches.Select(p => new FileInfo(p))
                                               .OrderByDescending(fi => fi.LastWriteTimeUtc)
                                               .First().FullName;
                        return await ReadAllBytesAsync(latest);
                    }
                    string rootExact = Path.Combine(persistentPath, ResolveSaveFileStem(slot) + ".sav");
                    if (await FileExistsAsync(rootExact))
                        return await ReadAllBytesAsync(rootExact);
                    var rootMatches = Directory.GetFiles(persistentPath, glob);
                    if (rootMatches != null && rootMatches.Length > 0)
                    {
                        string latestRoot = rootMatches.Select(p => new FileInfo(p))
                                                       .OrderByDescending(fi => fi.LastWriteTimeUtc)
                                                       .First().FullName;
                        return await ReadAllBytesAsync(latestRoot);
                    }
                }
                catch { }
            }
        }

        // 2) Cloud local fallbacks created when cloud was enabled but not signed-in
        if (settings.enableCloudSave)
        {
            string blobPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".sav");
            if (await FileExistsAsync(blobPath))
                return await ReadAllBytesAsync(blobPath);
            string jsonPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".json");
            if (await FileExistsAsync(jsonPath))
            {
                string txt = await ReadAllTextAsync(jsonPath);
                return Convert.FromBase64String(txt.Trim('"'));
            }
        }

        return null;
    }

    // Cloud load helper honoring current transport
    private async Task<byte[]> LoadCloudBytesAsync(string key)
    {
        switch (settings.cloudSaveTransport)
        {
            case CloudSaveTransport.Binary:
            {
#if REMEMBERME_CLOUDSAVE_PRESENT
#if UNITY_WEBGL && !UNITY_EDITOR
                var res = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key }));
                if (res != null && res.TryGetValue(key, out var e))
                    return Convert.FromBase64String(e.Value.GetAs<string>());
                return null;
#else
                try { return await RunOnMainThreadAsync(async () =>
                            await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(key)); }
                catch (CloudSaveException e) { if (IsNotFound(e)) return null; throw; }
#endif
#else
                return await Task.FromResult<byte[]>(null);
#endif
            }
            case CloudSaveTransport.JSON:
            {
#if REMEMBERME_CLOUDSAVE_PRESENT
                var res = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key }));
                if (res != null && res.TryGetValue(key, out var e))
                    return Convert.FromBase64String(e.Value.GetAs<string>());
#endif
                return await Task.FromResult<byte[]>(null);
            }
        }
        return await Task.FromResult<byte[]>(null);
    }

    private async Task BackfillLocalMirrorAsync(SaveSlot slot, byte[] data)
    {
        if (!UseLocalMirror || data == null || data.Length == 0) return;
        if (settings.saveMethod == SaveMethod.PlayerPrefs)
        {
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                PlayerPrefs.SetString(ResolveSaveKey(slot.SlotNumber), Convert.ToBase64String(data));
                PlayerPrefs.Save();
            });
        }
        else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
        {
            string path = Path.Combine(EnsureSlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav");
            await WriteAllBytesAsync(path, data);
        }
    }

    private async Task<SaveSlot> LoadLocalSlotMetadataOnlyAsync(int slotNumber)
    {
        if (settings.saveMethod == SaveMethod.PlayerPrefs)
        {
            string metaKey = settings.saveKey + "_Metadata_" + slotNumber;
            string base64 = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => PlayerPrefs.GetString(metaKey, null));
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                var enc = SaveManager.Instance?.EncryptionService;
                if (enc?.UseEncryption == true) bytes = enc.MaybeDecrypt(bytes);
                var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                if (slot != null && slot.CustomMetadata == null) slot.CustomMetadata = new Dictionary<string,string>();
                return slot;
            }
            catch { return null; }
        }
        else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
        {
            string folder = SlotFolder(slotNumber);
            string metaFilePath = null;
            try
            {
                string pat = settings.metadataFileNamePattern;
                if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                    pat = "Slot{n}_Meta.bin";
                string glob = PatternToGlob(pat, slotNumber);
                if (Directory.Exists(folder))
                {
                    var candidates = Directory.GetFiles(folder, glob);
                    if (candidates != null && candidates.Length > 0)
                        metaFilePath = candidates.Select(p => new FileInfo(p))
                                                 .OrderByDescending(fi => fi.LastWriteTimeUtc)
                                                 .First().FullName;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(metaFilePath))
            {
                string legacy = Path.Combine(persistentPath, $"Slot{slotNumber}_Meta.bin");
                if (!FileExists(legacy)) return null;
                metaFilePath = legacy;
            }
            try
            {
                byte[] bytes = await ReadAllBytesAsync(metaFilePath);
                var enc = SaveManager.Instance?.EncryptionService;
                if (enc?.UseEncryption == true) bytes = enc.MaybeDecrypt(bytes);
                var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                if (slot != null && slot.CustomMetadata == null) slot.CustomMetadata = new Dictionary<string,string>();
                return slot;
            }
            catch { return null; }
        }
        return null;
    }

    private async Task<SaveSlot> LoadCloudSlotMetadataOnlyAsync(int slotNumber)
    {
        if (!settings.enableCloudSave || !settings.cloudSaveMetadata || !SignedIn)
            return await Task.FromResult<SaveSlot>(null);

#if REMEMBERME_CLOUDSAVE_PRESENT
        string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slotNumber;
        byte[] bytes = null;
        // Data API first
        var res = await RunOnMainThreadAsync(async () =>
            await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>{ metaKey }));
        if (res != null && res.TryGetValue(metaKey, out var e))
            bytes = Convert.FromBase64String(e.Value.GetAs<string>());
#if !UNITY_WEBGL || UNITY_EDITOR
        if (bytes == null || bytes.Length == 0)
        {
            try { bytes = await RunOnMainThreadAsync(async () =>
                        await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(metaKey)); } catch { }
        }
#endif
        if (bytes == null || bytes.Length == 0) return await Task.FromResult<SaveSlot>(null);
        try
        {
            var enc = SaveManager.Instance?.EncryptionService;
            if (enc?.UseEncryption == true) bytes = enc.MaybeDecrypt(bytes);
            var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
            if (slot != null && slot.CustomMetadata == null) slot.CustomMetadata = new Dictionary<string,string>();
            return slot;
        }
        catch { return await Task.FromResult<SaveSlot>(null); }
#else
        // SDK not present – nothing to load
        return await Task.FromResult<SaveSlot>(null);
#endif
    }

    // Simplified copy of SaveOperationService rule matcher for Custom policy
    private static bool MatchesRules(Dictionary<string,string> meta, MetadataRule[] rules, SaveData local, SaveData cloud, bool isLocal)
    {
        if (rules == null || rules.Length == 0) return false;
        int count = Math.Min(rules.Length, 2);
        for (int i = 0; i < count; i++)
        {
            var r = rules[i];
            switch (r.type)
            {
                case MetadataRuleType.Metadata:
                    if (string.IsNullOrEmpty(r.key)) return false;
                    if (!meta.TryGetValue(r.key, out string val)) return false;
                    if (!CompareMeta(val, r.value, r.op)) return false;
                    break;
                case MetadataRuleType.Latest:
                    if (local == null || cloud == null) return false;
                    if (isLocal) { if (local.LastSaved < cloud.LastSaved) return false; }
                    else { if (cloud.LastSaved < local.LastSaved) return false; }
                    break;
                case MetadataRuleType.Oldest:
                    if (local == null || cloud == null) return false;
                    if (isLocal) { if (local.LastSaved > cloud.LastSaved) return false; }
                    else { if (cloud.LastSaved > local.LastSaved) return false; }
                    break;
                case MetadataRuleType.LocalWins:
                    if (!isLocal) return false; break;
                case MetadataRuleType.CloudWins:
                    if (isLocal) return false; break;
            }
        }
        return true;
    }

    private static bool CompareMeta(string lhs, string rhs, ComparisonOp op)
    {
        bool leftNum = double.TryParse(lhs, out double lv);
        bool rightNum = double.TryParse(rhs, out double rv);
        if (leftNum && rightNum)
        {
            switch (op)
            {
                case ComparisonOp.Equals:  return lv == rv;
                case ComparisonOp.Smaller: return lv < rv;
                case ComparisonOp.Larger:  return lv > rv;
            }
            return false;
        }
        int cmp = string.CompareOrdinal(lhs, rhs);
        switch (op)
        {
            case ComparisonOp.Equals:  return cmp == 0;
            case ComparisonOp.Smaller: return cmp < 0;
            case ComparisonOp.Larger:  return cmp > 0;
        }
        return false;
    }

        private async Task DeleteLocalDataAsync(SaveSlot slot, string safeKey)
        {
            // Always purge PlayerPrefs keys regardless of current saveMethod to avoid stale entries
            try
            {
                await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                {
                    string mainKey = ResolveSaveKey(slot.SlotNumber);
                    string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
                    if (PlayerPrefs.HasKey(mainKey)) PlayerPrefs.DeleteKey(mainKey);
                    if (PlayerPrefs.HasKey(metaKey)) PlayerPrefs.DeleteKey(metaKey);
                    if (PlayerPrefs.HasKey(mainKey + "_bak")) PlayerPrefs.DeleteKey(mainKey + "_bak");
                    PlayerPrefs.Save();
                });
            }
            catch { }

            // File-based cleanup (runs irrespective of saveMethod)
            {
                // mirror .sav exists only when using a local mirror
                if (UseLocalMirror)
                {
                        string mirrorSav = Path.Combine(EnsureSlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav");
                    if (FileExists(mirrorSav)) DeleteFile(mirrorSav);
                    // Also sweep any other .sav variants created by metadata changes
                    try
                    {
                        string stemPattern = settings.saveFileName ?? string.Empty;
                        string glob = PatternToGlob(stemPattern, slot.SlotNumber) + ".sav";
                            foreach (var f in Directory.GetFiles(EnsureSlotFolder(slot.SlotNumber), glob))
                            if (FileExists(f)) DeleteFile(f);
                        // Also delete root-level legacy files
                        foreach (var f in Directory.GetFiles(persistentPath, glob))
                            if (FileExists(f)) DeleteFile(f);
                    }
                    catch {}
                }

                // cloud‑fallbacks (created when not signed in)
                    string blobSav = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".sav");
                if (FileExists(blobSav)) DeleteFile(blobSav);

                    string jsonFall = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".json");
                if (FileExists(jsonFall)) DeleteFile(jsonFall);
                // Also root-level fallbacks
                string legacyBlob = Path.Combine(persistentPath, safeKey + ".sav");
                if (FileExists(legacyBlob)) DeleteFile(legacyBlob);
                string legacyJson = Path.Combine(persistentPath, safeKey + ".json");
                if (FileExists(legacyJson)) DeleteFile(legacyJson);

                // metadata is always stored locally (may include meta placeholders)
                    string meta = Path.Combine(EnsureSlotFolder(slot.SlotNumber), GetSlotMetadataFileName(slot));
                if (FileExists(meta)) DeleteFile(meta);
                // Also sweep any other matches produced by changed values
                try
                {
                    string pat = settings.metadataFileNamePattern;
                    if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                        pat = "Slot{n}_Meta.bin";
                    string glob = PatternToGlob(pat, slot.SlotNumber);
                        foreach (var f in Directory.GetFiles(EnsureSlotFolder(slot.SlotNumber), glob))
                        if (FileExists(f)) DeleteFile(f);
                    // Delete legacy root-level metadata
                    foreach (var f in Directory.GetFiles(persistentPath, glob))
                        if (FileExists(f)) DeleteFile(f);
                    string legacyMeta = Path.Combine(persistentPath, $"Slot{slot.SlotNumber}_Meta.bin");
                    if (FileExists(legacyMeta)) DeleteFile(legacyMeta);
                }
                catch {}
            }
        }

        public async Task DeleteLocalSlotAsync(SaveSlot slot)
        {
            string safeKey = Sanitize(ResolveSaveKey(slot.SlotNumber));
            await DeleteLocalDataAsync(slot, safeKey);

            if (settings.enableScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
            {
                string shot = Path.Combine(persistentPath,
                                            settings.screenshotFolderName,
                                            slot.ScreenshotFileName);
                if (FileExists(shot)) DeleteFile(shot);
            }
        }

		/// <summary>
		/// Asynchronously deletes the saved data from a specific save slot using the configured method.
		/// Also deletes the associated screenshot if enabled.
		/// </summary>
        public async Task DeleteAsync(SaveSlot slot)
	{
	    try
	    {
            // Use cloud-safe resolved key for remote deletes
            string key     = ResolveCloudKey(slot);
            string safeKey = Sanitize(ResolveSaveKey(slot.SlotNumber));

            // If the slot looks empty (no known last save and no screenshot), skip remote deletes to avoid 404 noise.
            // Local cleanup will still run to be safe.
            bool looksEmpty = (slot == null) || (slot.LastSaved == default && string.IsNullOrEmpty(slot.ScreenshotFileName));
            if (looksEmpty)
            {
                Logger.Log($"DeleteAsync: Slot {slot?.SlotNumber} appears empty; skipping remote delete calls.", LogCategory.SaveSystem, LogLevel.Info);
                await DeleteLocalDataAsync(slot, safeKey);
                // Also try to remove local screenshot if any lingering file name was set
                if (settings.enableScreenshots && !string.IsNullOrEmpty(slot?.ScreenshotFileName))
                {
                    string shot = Path.Combine(persistentPath, settings.screenshotFolderName, slot.ScreenshotFileName);
                    if (FileExists(shot)) DeleteFile(shot);
                }
                return;
            }

	        /* 1  Cloud OR fallback */
	        if (settings.enableCloudSave)
	        {
	            switch (settings.cloudSaveTransport)
	            {
            case CloudSaveTransport.Binary:
                if (CLOUD_SDK_PRESENT)
                {
#if REMEMBERME_CLOUDSAVE_PRESENT
                    try
                    {
                        await RunOnMainThreadAsync(async () =>
                            await UnityCloudSaveService.Instance.Files.Player.DeleteAsync(key));
                    }
                    catch (CloudSaveException e)
                    {
                        if (!IsNotFound(e)) throw;
                        Logger.Log($"Cloud (Files) delete skipped, not found: {key}", LogCategory.SaveSystem, LogLevel.Info);
                    }
#endif
                }
                else
                {
#pragma warning disable CS0162 // Unreachable code detected
                                string blobPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".sav");
#pragma warning restore CS0162 // Unreachable code detected
                                if (FileExists(blobPath)) DeleteFile(blobPath);
                }
	                    break;

                        case CloudSaveTransport.JSON:
                            if (CLOUD_SDK_PRESENT)
                            {
#if REMEMBERME_CLOUDSAVE_PRESENT
                            try
                            {
                                await RunOnMainThreadAsync(async () =>
                                    await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(key, new PlayerDeleteOptions()));
                            }
                            catch (CloudSaveException e)
                            {
                                if (!IsNotFound(e)) throw;
                                Logger.Log($"Cloud (Data) delete skipped, not found: {key}", LogCategory.SaveSystem, LogLevel.Info);
                            }
#endif
                            }
                            else
                            {
#pragma warning disable CS0162 // Unreachable code detected
                                string jsonPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".json");
#pragma warning restore CS0162 // Unreachable code detected
                                if (FileExists(jsonPath)) DeleteFile(jsonPath);
	                    }
                            break;
                    }

                    if (CLOUD_SDK_PRESENT)
                    {
#if REMEMBERME_CLOUDSAVE_PRESENT
                        if (settings.cloudSaveScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                            // WebGL: slot.ScreenshotFileName already contains the correct key format
                            string deleteKey = slot.ScreenshotFileName;
                            // If the key doesn't start with screenshot_, add the prefix for backward compatibility
                            if (!deleteKey.StartsWith("screenshot_"))
                            {
                                deleteKey = deleteKey.Contains(".") ? 
                                    deleteKey.Substring(0, deleteKey.LastIndexOf('.')) : deleteKey;
                                deleteKey = $"screenshot_{deleteKey}";
                            }
                            try
                            {
                                await RunOnMainThreadAsync(async () =>
                                    await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(deleteKey, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions()));
                                Logger.Log($"WebGL screenshot deleted via Data API: {deleteKey}", LogCategory.SaveSystem, LogLevel.Info);
                            }
                            catch (CloudSaveException e)
                            {
                                if (!IsNotFound(e)) throw;
                                Logger.Log($"Screenshot not found in cloud (ignored): {deleteKey}", LogCategory.SaveSystem, LogLevel.Info);
                            }
#else
                            // Non-WebGL platforms: try both APIs for maximum compatibility
                            try
                            {
                                string dataApiKey = slot.ScreenshotFileName;
                                if (!dataApiKey.StartsWith("screenshot_"))
                                {
                                    dataApiKey = dataApiKey.Contains(".") ? 
                                        dataApiKey.Substring(0, dataApiKey.LastIndexOf('.')) : dataApiKey;
                                    dataApiKey = $"screenshot_{dataApiKey}";
                                }
                                try
                                {
                                    await RunOnMainThreadAsync(async () =>
                                        await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(dataApiKey, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions()));
                                    Logger.Log($"Screenshot deleted via Data API: {dataApiKey}", LogCategory.SaveSystem, LogLevel.Info);
                                }
                                catch (CloudSaveException e)
                                {
                                    if (!IsNotFound(e)) throw;
                                    Logger.Log($"Screenshot not found in cloud (ignored): {dataApiKey}", LogCategory.SaveSystem, LogLevel.Info);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Data API screenshot deletion failed: {ex.Message}", LogCategory.SaveSystem, LogLevel.Warning);
                            }
#endif
                        }

                        if (settings.cloudSaveMetadata)
                        {
                            // Cloud metadata now always uses Data API keys; delete from both Data and Files for safety
                            string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slot.SlotNumber;
                            switch (settings.cloudSaveTransport)
                            {
                                case CloudSaveTransport.Binary:
#if REMEMBERME_CLOUDSAVE_PRESENT
                                    // Primary: Data API only on WebGL to avoid CORS
                                    try
                                    {
                                        await RunOnMainThreadAsync(async () =>
                                            await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(metaKey, new PlayerDeleteOptions()));
                                    }
                                    catch (CloudSaveException e)
                                    {
                                        if (!IsNotFound(e)) throw;
                                        Logger.Log($"Metadata key not found in Data API (ignored): {metaKey}", LogCategory.SaveSystem, LogLevel.Info);
                                    }
                                    #if !UNITY_WEBGL || UNITY_EDITOR
                    try { await RunOnMainThreadAsync(async () =>
                        await UnityCloudSaveService.Instance.Files.Player.DeleteAsync(metaKey)); } catch {}
                                    #endif
#endif
                                    break;
                                case CloudSaveTransport.JSON:
#if REMEMBERME_CLOUDSAVE_PRESENT
                                    try
                                    {
                                        await RunOnMainThreadAsync(async () =>
                                            await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(metaKey, new PlayerDeleteOptions()));
                                    }
                                    catch (CloudSaveException e)
                                    {
                                        if (!IsNotFound(e)) throw;
                                        Logger.Log($"Metadata key not found in Data API (ignored): {metaKey}", LogCategory.SaveSystem, LogLevel.Info);
                                    }
                                    #if !UNITY_WEBGL || UNITY_EDITOR
                    try { await RunOnMainThreadAsync(async () =>
                        await UnityCloudSaveService.Instance.Files.Player.DeleteAsync(metaKey)); } catch {}
                                    #endif
#endif
                                    break;
                            }
                        }
#endif
                    }
                }

	        /* 2 Local mirror delete */
	        await DeleteLocalDataAsync(slot, safeKey);

                /* 3 Screenshot */
                if (settings.enableScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
                {
                    string shot = Path.Combine(persistentPath, settings.screenshotFolderName, slot.ScreenshotFileName);
                    if (FileExists(shot)) DeleteFile(shot);
                }
            }
            catch (Exception ex)
            {
	        Logger.Log($"Failed to delete slot {slot.SlotNumber}: {ex.Message}", LogCategory.SaveSystem, LogLevel.Error);
	        throw;
	    }
	}

		/// <summary>
		/// Saves the serialized data to a specific save slot using the configured method.
		/// </summary>
		/// <param name="data">Serialized byte array.</param>
		/// <param name="slot">The save slot to save to.</param>
                public void Save(byte[] data, SaveSlot slot)
                {
                        if (!UseLocalMirror)
                        {
                                // When the local mirror is disabled we still need to
                                // persist the data using the cloud backend.  Fallback
                                // to the async API and block until it completes so
                                // callers remain synchronous.
                if (settings.enableCloudSave)
                {
                    // Schedule async save on the main thread to satisfy Unity Cloud Save threading
                    UnityMainThreadDispatcher.Instance().Enqueue(async () =>
                    {
                        try { await SaveAsync(data, slot); }
                        catch (Exception ex) { Logger.Log($"Save() fallback async path failed: {ex.Message}", LogCategory.SaveSystem, LogLevel.Error); }
                    });
                }
                                else
                                {
                                        Logger.Log("Save() skipped – local mirror disabled.", LogCategory.SaveSystem, LogLevel.Warning);
                                }
                                return;
                        }

                        try
                        {
                                if (settings.saveMethod == SaveMethod.PlayerPrefs)
                                {
                                        // Convert byte array to Base64 string for PlayerPrefs
                                        string base64Data = Convert.ToBase64String(data);
                                        string fullKey = settings.saveKey + slot.SlotNumber;
                                        PlayerPrefs.SetString(fullKey, base64Data);
                                        PlayerPrefs.Save();
                                        Logger.Log($"Saved data to PlayerPrefs slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
                                }
                                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                                {
                                        string folder = EnsureSlotFolder(slot.SlotNumber);
                                        string saveFilePath = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
                                        WriteAllBytes(saveFilePath, data);
                                        Logger.Log($"Saved data to file at {saveFilePath}.", LogCategory.SaveSystem, LogLevel.Info);

                                        // Sweep stale variants created by metadata changes
                                        try
                                        {
                                                string stemPattern = settings.saveFileName ?? string.Empty;
                                                string glob = PatternToGlob(stemPattern, slot.SlotNumber) + ".sav";
                                                foreach (var f in Directory.GetFiles(folder, glob))
                                                {
                                                    if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(saveFilePath), StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        if (FileExists(f)) DeleteFile(f);
                                                    }
                                                }
                                                // Also remove legacy root-level files
                                                foreach (var f in Directory.GetFiles(persistentPath, glob))
                                                    if (FileExists(f)) DeleteFile(f);
                                                string legacyExact = Path.Combine(persistentPath, ResolveSaveFileStem(slot) + ".sav");
                                                if (FileExists(legacyExact)) DeleteFile(legacyExact);
                                        }
                                        catch { }

                                        // Keep metadata in sync in the same slot folder
                                        SaveSlotMetadata(slot);
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Failed to save data to slot {slot.SlotNumber}. Exception: {ex.Message}", LogCategory.SaveSystem, LogLevel.Error);
                                throw;
                        }
                }

		/// <summary>
		/// Loads the serialized data from a specific save slot using the configured method.
		/// </summary>
		/// <param name="slot">The save slot to load from.</param>
		/// <returns>Serialized byte array.</returns>
		public byte[] Load(SaveSlot slot)
		{
                        if (!UseLocalMirror)
                        {
                                Logger.Log("Load() skipped – local mirror disabled.", LogCategory.SaveSystem, LogLevel.Info);
                                return null;
                        }

			try
			{
				if (settings.saveMethod == SaveMethod.PlayerPrefs)
				{
					string fullKey = settings.saveKey + slot.SlotNumber;
					if (PlayerPrefs.HasKey(fullKey))
					{
						string base64Data = PlayerPrefs.GetString(fullKey);
						byte[] data = Convert.FromBase64String(base64Data);
						Logger.Log($"Loaded data from PlayerPrefs slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
						return data;
					}
					else
					{
						Logger.Log($"No saved data found in PlayerPrefs slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
						return null;
					}
				}
                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                {
                    string folder = EnsureSlotFolder(slot.SlotNumber);
                    string saveFilePath = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
                    if (FileExists(saveFilePath))
                    {
                        byte[] data = ReadAllBytes(saveFilePath);
                        Logger.Log($"Loaded data from file at {saveFilePath}.", LogCategory.SaveSystem, LogLevel.Info);
                        return data;
                    }

                    // Fallback to latest variant when metadata-based names changed
                    try
                    {
                        string stemPattern = settings.saveFileName ?? string.Empty;
                        string glob = PatternToGlob(stemPattern, slot.SlotNumber) + ".sav";
                        var matches = Directory.GetFiles(folder, glob);
                        if (matches != null && matches.Length > 0)
                        {
                            string latest = matches
                                .Select(p => new FileInfo(p))
                                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                                .First().FullName;
                            byte[] data = ReadAllBytes(latest);
                            Logger.Log($"Loaded data from variant file at {latest}.", LogCategory.SaveSystem, LogLevel.Info);
                            return data;
                        }
                    }
                    catch { }

                    // Legacy root-level exact and glob fallback
                    try
                    {
                        string stemPattern = settings.saveFileName ?? string.Empty;
                        string glob = PatternToGlob(stemPattern, slot.SlotNumber) + ".sav";
                        string rootExact = Path.Combine(persistentPath, ResolveSaveFileStem(slot) + ".sav");
                        if (FileExists(rootExact))
                        {
                            byte[] data = ReadAllBytes(rootExact);
                            Logger.Log($"Loaded data from legacy root file at {rootExact}.", LogCategory.SaveSystem, LogLevel.Info);
                            return data;
                        }
                        var rootMatches = Directory.GetFiles(persistentPath, glob);
                        if (rootMatches != null && rootMatches.Length > 0)
                        {
                            string latestRoot = rootMatches
                                .Select(p => new FileInfo(p))
                                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                                .First().FullName;
                            byte[] data = ReadAllBytes(latestRoot);
                            Logger.Log($"Loaded data from legacy variant at {latestRoot}.", LogCategory.SaveSystem, LogLevel.Info);
                            return data;
                        }
                    }
                    catch { }

                    Logger.Log($"No saved data file found for slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Off);
                    return null;
                }
				return null;
			}
			catch (Exception ex)
			{
				Logger.Log($"Failed to load data from slot {slot.SlotNumber}. Exception: {ex.Message}", LogCategory.SaveSystem, LogLevel.Error);
				throw;
			}
		}

		/// <summary>
		/// Deletes the saved data from a specific save slot using the configured method.
		/// Also deletes the associated screenshot (if enabled) and the slot's metadata.
		/// </summary>
		/// <param name="slot">The save slot to delete.</param>
		public void Delete(SaveSlot slot)
		{
			string safeKey = Sanitize(settings.saveKey) + slot.SlotNumber;

	    /* Always clean fallback artefacts */
        DeleteFile(Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".sav"));
        DeleteFile(Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".json"));

			if (!UseLocalMirror)
			{
				// For Unity Cloud Save without local mirror, use async delete and block
                if (settings.enableCloudSave)
                {
                    Logger.Log("Delete() calling DeleteAsync for cloud-only delete.", LogCategory.SaveSystem, LogLevel.Info);
                    try
                    {
                        Task.Run(() => DeleteAsync(slot)).GetAwaiter().GetResult();
                        Logger.Log($"Successfully deleted cloud slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to delete cloud slot {slot.SlotNumber}: {ex.Message}", LogCategory.SaveSystem, LogLevel.Error);
                        throw;
                    }
                }
				else
				{
					Logger.Log("Delete() skipped – local mirror disabled and cloud save disabled.", LogCategory.SaveSystem, LogLevel.Warning);
					return;
				}
			}

			try
			{
				// 1) Delete the main save data
				if (settings.saveMethod == SaveMethod.PlayerPrefs)
				{
					// Main data key
					string fullKey = settings.saveKey + slot.SlotNumber;
					if (PlayerPrefs.HasKey(fullKey))
					{
						PlayerPrefs.DeleteKey(fullKey);
						PlayerPrefs.Save();
						Logger.Log($"Deleted saved data from PlayerPrefs slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
					}
					else
					{
						Logger.Log($"No saved data found in PlayerPrefs slot {slot.SlotNumber} to delete.", LogCategory.SaveSystem, LogLevel.Warning);
					}

					string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
					if (PlayerPrefs.HasKey(metaKey))
					{
						PlayerPrefs.DeleteKey(metaKey);
						PlayerPrefs.Save();
						Logger.Log($"Deleted metadata for PlayerPrefs slot {slot.SlotNumber} (key: {metaKey}).", LogCategory.SaveSystem, LogLevel.Info);
					}
					else
					{
						Logger.Log($"No metadata found in PlayerPrefs for slot {slot.SlotNumber} to delete.", LogCategory.SaveSystem, LogLevel.Warning);
					}
				}
                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                {
                    // Main .sav in slot folder
                    string folder = EnsureSlotFolder(slot.SlotNumber);
                    string saveFilePath = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
                    if (FileExists(saveFilePath))
                    {
                        DeleteFile(saveFilePath);
                        Logger.Log($"Deleted saved data file at {saveFilePath}.", LogCategory.SaveSystem, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log($"No saved data file found at {saveFilePath} to delete.", LogCategory.SaveSystem, LogLevel.Warning);
                    }

                    // Sweep any variant .sav files created by metadata changes
                    try
                    {
                        string stemPattern = settings.saveFileName ?? string.Empty;
                        string glob = PatternToGlob(stemPattern, slot.SlotNumber) + ".sav";
                        foreach (var f in Directory.GetFiles(folder, glob))
                            if (FileExists(f)) DeleteFile(f);
                    }
                    catch { }

                    // Metadata in slot folder, using resolved pattern
                    string metaFileName = GetSlotMetadataFileName(slot);
                    string metaFilePath = Path.Combine(folder, metaFileName);
                    if (FileExists(metaFilePath))
                    {
                        DeleteFile(metaFilePath);
                        Logger.Log($"Deleted metadata file at {metaFilePath}.", LogCategory.SaveSystem, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log($"No metadata file found at {metaFilePath} to delete.", LogCategory.SaveSystem, LogLevel.Warning);
                    }

                    // Sweep metadata pattern variants
                    try
                    {
                        string pat = settings.metadataFileNamePattern;
                        if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                            pat = "Slot{n}_Meta.bin";
                        string glob = PatternToGlob(pat, slot.SlotNumber);
                        foreach (var f in Directory.GetFiles(folder, glob))
                            if (FileExists(f)) DeleteFile(f);
                    }
                    catch { }
                }

				// 2) Delete screenshot if enabled and exists
				if (settings.enableScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
				{
					string screenshotPath = Path.Combine(
						persistentPath,
						settings.screenshotFolderName,
						slot.ScreenshotFileName
					);
                                        if (FileExists(screenshotPath))
                                        {
                                                DeleteFile(screenshotPath);
                                                Logger.Log($"Deleted screenshot '{screenshotPath}' for slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
                                        }
					else
					{
						Logger.Log($"No screenshot file '{screenshotPath}' found to delete for slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Warning);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Log($"Failed to delete saved data from slot {slot.SlotNumber}. Exception: {ex.Message}", LogCategory.SaveSystem, LogLevel.Error);
				throw;
			}
		}

		#region Metadata
		public string GetSaveGamesPath()
		{
			return persistentPath;
		}

		/// <summary>
		/// Asynchronously saves the entire SaveSlot object as "metadata"
		/// (i.e. just the fields in SaveSlot, not the entire game state)
		/// using MemoryPack.
		/// </summary>
        public async Task SaveSlotMetadataAsync(SaveSlot slot)
        {
            byte[] metaBytes = SaveDataSerializer.Instance.Serialize(slot);
            // Encrypt metadata if enabled and configured
            try
            {
                var enc = SaveManager.Instance?.EncryptionService;
                if (settings.enableEncryption && settings.encryptSlotMetadata && (enc?.UseEncryption ?? false))
                {
                    metaBytes = enc.MaybeEncrypt(metaBytes);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[SaveSlotMetadataAsync] Encryption step failed, storing plaintext metadata. {ex.Message}", LogCategory.SaveSystem, LogLevel.Warning);
            }

                        if (!settings.enableCloudSave || UseLocalMirror)
                        {
                                if (settings.saveMethod == SaveMethod.PlayerPrefs)
                                {
                                        string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;

                                        // MAIN THREAD write
                                        await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                                        {
                                                string base64 = Convert.ToBase64String(metaBytes);
                                                PlayerPrefs.SetString(metaKey, base64);
                                                PlayerPrefs.Save();
                                                Logger.Log($"[SaveSlotMetadataAsync] Stored slot metadata in PlayerPrefs key: {metaKey}",
                                                                       LogCategory.SaveSystem,
                                                                       LogLevel.Info);
                                        });
                                }
                                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                                {
                                        string metaFileName = GetSlotMetadataFileName(slot);
                                        string metaFilePath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), metaFileName);

                                        await WriteAllBytesAsync(metaFilePath, metaBytes);
                                        Logger.Log($"[SaveSlotMetadataAsync] Stored slot metadata file at: {metaFilePath}",
                                                                       LogCategory.SaveSystem,
                                                                       LogLevel.Info);
                                        // Sweep previous metadata variants for this slot (pattern-based)
                                        try
                                        {
                                            string pat = settings.metadataFileNamePattern;
                                            if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                                                pat = "Slot{n}_Meta.bin";
                                            string glob = PatternToGlob(pat, slot.SlotNumber);
                                            foreach (var f in Directory.GetFiles(SlotFolder(slot.SlotNumber), glob))
                                            {
                                                if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(metaFilePath), StringComparison.OrdinalIgnoreCase))
                                                {
                                                    if (FileExists(f)) DeleteFile(f);
                                                }
                                            }
                                        }
                                        catch {}
                                }
                        }
            else
            {
                // Cloud-only mode: skip local writes but keep SlotManager in sync immediately
                Logger.Log("[SaveSlotMetadataAsync] Local metadata skipped – cloud only mode.",
                       LogCategory.SaveSystem,
                       LogLevel.Info);
                                try
                                {
                                    var sm = SaveManager.Instance?.SlotManager;
                                    if (sm != null)
                                    {
                                        var existing = sm.GetByNumber(slot.SlotNumber);
                                        if (existing != null)
                                        {
                                            existing.SlotName = slot.SlotName;
                                            existing.LastSaved = slot.LastSaved;
                                            existing.ScreenshotFileName = slot.ScreenshotFileName;
                                            existing.LastActiveScene = slot.LastActiveScene;
                                            existing.CustomMetadata = slot.CustomMetadata;
                                        }
                                    }
                                }
                                catch {}
            }

            if (settings.enableCloudSave && settings.cloudSaveMetadata && SignedIn)
            {
                // Always store metadata via Data API as base64 string for consistency
                string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slot.SlotNumber;
                string b64 = Convert.ToBase64String(metaBytes);
#if REMEMBERME_CLOUDSAVE_PRESENT
                await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>{{metaKey, b64}}));
                // Optional: also save to Files API for backward compatibility with older clients
                #if !UNITY_WEBGL || UNITY_EDITOR
                try { await RunOnMainThreadAsync(async () =>
                        await UnityCloudSaveService.Instance.Files.Player.SaveAsync(metaKey, metaBytes)); } catch {}
                #endif
#endif
            }
                }

		/// <summary>
		/// Asynchronously loads the entire SaveSlot object from disk or PlayerPrefs
		/// using MemoryPack. Returns null if it doesn't exist.
		/// </summary>
                public async Task<SaveSlot> LoadSlotMetadataAsync(int slotNumber)
                {
                        if (settings.saveMethod == SaveMethod.PlayerPrefs)
                        {
				string metaKey = settings.saveKey + "_Metadata_" + slotNumber;

				// MAIN THREAD read
				string base64 = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
					PlayerPrefs.GetString(metaKey, null));

				if (string.IsNullOrEmpty(base64))
				{
                    // No metadata present for this slot in PlayerPrefs – treat as empty without logging
                    return null;
				}

	                        try
	                        {
	                                byte[] bytes = Convert.FromBase64String(base64);
                                    // Decrypt if needed
                                    try
                                    {
                                        var enc = SaveManager.Instance?.EncryptionService;
                                        if (enc?.UseEncryption == true)
                                            bytes = enc.MaybeDecrypt(bytes);
                                    }
                                    catch { }
	                                var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
	                                if (slot != null && slot.CustomMetadata == null)
	                                        slot.CustomMetadata = new Dictionary<string, string>();
	                                return slot;
	                        }
	                        catch (Exception ex)
	                        {
	                                Logger.Log($"[LoadSlotMetadataAsync] Failed to deserialize slot {slotNumber}: {ex.Message}",
	                                                   LogCategory.SaveSystem,
	                                                   LogLevel.Error);
	                                return null;
	                        }
                        }
                        else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                        {
                                // First look inside the per-slot folder using a glob based on the pattern
                                string folder = SlotFolder(slotNumber);
                                string metaFilePath = null;
                                try
                                {
                                    string pat = settings.metadataFileNamePattern;
                                    if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                                        pat = "Slot{n}_Meta.bin";
                                    string glob = PatternToGlob(pat, slotNumber);
                                    if (Directory.Exists(folder))
                                    {
                                        var candidates = Directory.GetFiles(folder, glob);
                                        if (candidates != null && candidates.Length > 0)
                                        {
                                            metaFilePath = candidates
                                                .Select(p => new FileInfo(p))
                                                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                                                .First().FullName;
                                        }
                                    }
                                }
                                catch { }

                                if (string.IsNullOrEmpty(metaFilePath))
                                {
                                    // Try legacy root-level name for backward compatibility
                                    string legacy = Path.Combine(persistentPath, $"Slot{slotNumber}_Meta.bin");
                                    if (!FileExists(legacy))
                                    {
                                        // No metadata file for this slot – treat as empty without logging
                                        return null;
                                    }
                                    metaFilePath = legacy;
                                }

                                try
                                {
                                        byte[] bytes = await ReadAllBytesAsync(metaFilePath);
                                        // Decrypt if needed
                                        try
                                        {
                                            var enc = SaveManager.Instance?.EncryptionService;
                                            if (enc?.UseEncryption == true)
                                                bytes = enc.MaybeDecrypt(bytes);
                                        }
                                        catch { }
                                        var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
	                                if (slot != null && slot.CustomMetadata == null)
	                                        slot.CustomMetadata = new Dictionary<string, string>();
	                                return slot;
	                        }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[LoadSlotMetadataAsync] Failed to deserialize {metaFilePath}: {ex.Message}",
                                                           LogLevel.Error);
                                        return null;
                                }
                        }

            if (settings.enableCloudSave && settings.cloudSaveMetadata && SignedIn)
            {
                string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slotNumber;
                byte[] bytes = null;
#if REMEMBERME_CLOUDSAVE_PRESENT
                // Primary: Data API
                var res = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>{metaKey}));
                if (res != null && res.TryGetValue(metaKey, out var e))
                    bytes = Convert.FromBase64String(e.Value.GetAs<string>());
                #if !UNITY_WEBGL || UNITY_EDITOR
                if (bytes == null || bytes.Length == 0)
                {
                    // Fallback: Files API (legacy)
            try { bytes = await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(metaKey)); } catch {}
                }
                #endif
#endif
                if (bytes != null && bytes.Length > 0)
                {
                    // Decrypt if needed
                    try
                    {
                        var enc = SaveManager.Instance?.EncryptionService;
                        if (enc?.UseEncryption == true)
                            bytes = enc.MaybeDecrypt(bytes);
                    }
                    catch { }
                    var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                    if (slot != null && slot.CustomMetadata == null)
                        slot.CustomMetadata = new Dictionary<string, string>();
                    return slot;
                }
            }

                        return null;
                }

        public void BackupSlot(SaveSlot slot)
        {
            if (!UseLocalMirror || !settings.enableSaveFileVerification) return;

            if (settings.saveMethod == SaveMethod.BinaryFileFormat)
            {
                string path = Path.Combine(EnsureSlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav");
                string backup = path + ".bak";
                if (FileExists(path))
                {
                    long size = new FileInfo(path).Length;
                    string root = Path.GetPathRoot(path);
                    if (!string.IsNullOrEmpty(root))
                    {
                        var drive = new DriveInfo(root);
                        if (drive.AvailableFreeSpace < size)
                            throw new IOException($"Not enough disk space to create backup '{backup}'.");
                    }
                    CopyFile(path, backup, true);
                }
            }
            else if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string key = ResolveSaveKey(slot.SlotNumber);
                if (PlayerPrefs.HasKey(key))
                {
                    string val = PlayerPrefs.GetString(key);
                    PlayerPrefs.SetString(key + "_bak", val);
                    PlayerPrefs.Save();
                }
            }
        }

        public void SaveSlotMetadata(SaveSlot slot)
        {
            byte[] metaBytes = SaveDataSerializer.Instance.Serialize(slot);
            // Encrypt metadata if enabled and configured
            try
            {
                var enc = SaveManager.Instance?.EncryptionService;
                if (settings.enableEncryption && settings.encryptSlotMetadata && (enc?.UseEncryption ?? false))
                {
                    metaBytes = enc.MaybeEncrypt(metaBytes);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[SaveSlotMetadata] Encryption step failed, storing plaintext metadata. {ex.Message}", LogLevel.Warning);
            }

            if (!settings.enableCloudSave || UseLocalMirror)
            {
                if (settings.saveMethod == SaveMethod.PlayerPrefs)
                {
                    string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
                    string base64 = Convert.ToBase64String(metaBytes);
                    PlayerPrefs.SetString(metaKey, base64);
                    PlayerPrefs.Save();
                    Logger.Log($"[SaveSlotMetadata] Stored slot metadata in PlayerPrefs key: {metaKey}", LogCategory.SaveSystem, LogLevel.Info);
                }
                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                {
                    string metaFileName = GetSlotMetadataFileName(slot);
                    string folder = EnsureSlotFolder(slot.SlotNumber);
                    string metaFilePath = Path.Combine(folder, metaFileName);
                    WriteAllBytes(metaFilePath, metaBytes);
                    Logger.Log($"[SaveSlotMetadata] Stored slot metadata file at: {metaFilePath}", LogCategory.SaveSystem, LogLevel.Info);

                    // Sweep previous metadata variants for this slot (pattern-based)
                    try
                    {
                        string pat = settings.metadataFileNamePattern;
                        if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                            pat = "Slot{n}_Meta.bin";
                        string glob = PatternToGlob(pat, slot.SlotNumber);
                        foreach (var f in Directory.GetFiles(folder, glob))
                        {
                            if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(metaFilePath), StringComparison.OrdinalIgnoreCase))
                            {
                                if (FileExists(f)) DeleteFile(f);
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                Logger.Log("[SaveSlotMetadata] Local metadata skipped – cloud only mode.", LogCategory.SaveSystem, LogLevel.Info);
            }

            if (settings.enableCloudSave && settings.cloudSaveMetadata && SignedIn)
            {
                // Always prefer Data API with base64, keep Files API as best-effort fallback
                string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slot.SlotNumber;
                string b64 = Convert.ToBase64String(metaBytes);
#if REMEMBERME_CLOUDSAVE_PRESENT
        try { _ = RunOnMainThreadAsync(async () =>
            await UnityCloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>{{metaKey, b64}})); }
        catch (Exception ex) { Logger.Log($"[SaveSlotMetadata] UnityCloudSave Data save failed: {ex.Message}", LogCategory.SaveSystem, LogLevel.Warning); }
        try { _ = RunOnMainThreadAsync(async () =>
            await UnityCloudSaveService.Instance.Files.Player.SaveAsync(metaKey, metaBytes)); } catch {}
#endif
            }
        }

        public void RestoreBackup(SaveSlot slot)
        {
            if (!UseLocalMirror || !settings.enableSaveFileVerification) return;

            if (settings.saveMethod == SaveMethod.BinaryFileFormat)
            {
                string path = Path.Combine(EnsureSlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav");
                string backup = path + ".bak";
                if (FileExists(backup))
                    CopyFile(backup, path, true);
            }
            else if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string key = settings.saveKey + slot.SlotNumber;
                string bak = key + "_bak";
                if (PlayerPrefs.HasKey(bak))
                {
                    string val = PlayerPrefs.GetString(bak);
                    PlayerPrefs.SetString(key, val);
                    PlayerPrefs.Save();
                }
            }
        }

        public void DeleteBackup(SaveSlot slot)
        {
            if (!UseLocalMirror || !settings.enableSaveFileVerification) return;

            if (settings.saveMethod == SaveMethod.BinaryFileFormat)
            {
                string path = Path.Combine(EnsureSlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav.bak");
                if (FileExists(path))
                    DeleteFile(path);
            }
            else if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string bak = settings.saveKey + slot.SlotNumber + "_bak";
                if (PlayerPrefs.HasKey(bak))
                {
                    PlayerPrefs.DeleteKey(bak);
                    PlayerPrefs.Save();
                }
            }
        }

        /* ================================================================== */
        /*  Temporary files (verification without mirror)                     */
        /* ================================================================== */

        string TempPathFor(SaveSlot slot)
            => Path.Combine(EnsureSlotFolder(slot.SlotNumber), $"{ResolveSaveFileStem(slot)}.tmp");

        public async Task WriteTempAsync(byte[] data, SaveSlot slot)
        {
            string path = TempPathFor(slot);
            await WriteAllBytesAsync(path, data);
        }

        public async Task<byte[]> LoadTempAsync(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            if (!FileExists(path)) return null;
            return await ReadAllBytesAsync(path);
        }

        public void DeleteTemp(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            if (FileExists(path)) DeleteFile(path);
        }

        /* ------------------------------------------------------------------ */
        /*  Remote slot listing (Unity Cloud Save)                            */
        /* ------------------------------------------------------------------ */

        public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
        {
            var list = new List<SaveSlot>();
            
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: ListRemoteSlotsAsync started - enableCloudSave: {settings.enableCloudSave}, SignedIn: {SignedIn}, cloudSaveMetadata: {settings.cloudSaveMetadata}");
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            Debug.Log($"[SaveSystem] WebGL: AuthenticationService.Instance.IsSignedIn: {Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn}");
            Debug.Log($"[SaveSystem] WebGL: AuthenticationService.Instance.PlayerId: {Unity.Services.Authentication.AuthenticationService.Instance.PlayerId}");
#else
            Debug.Log("[SaveSystem] WebGL: REMEMBERME_AUTHENTICATION_PRESENT and/or REMEMBERME_CORESERVICES_PRESENT not defined!");
#endif
#endif
            
            if (!settings.enableCloudSave || !SignedIn || !settings.cloudSaveMetadata)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSystem] WebGL: Returning empty list - enableCloudSave: {settings.enableCloudSave}, SignedIn: {SignedIn}, cloudSaveMetadata: {settings.cloudSaveMetadata}");
#endif
                return list;
            }

#if REMEMBERME_CLOUDSAVE_PRESENT
            string prefix = CloudSanitize(settings.saveKey) + "_Meta_";
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: Using prefix: {prefix}, transport: {settings.cloudSaveTransport}");
#endif
            
            // First pass: collect all keys to identify screenshots (WebGL/Data API path)
            var allKeys = new List<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                var keysResult = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.ListAllKeysAsync());
                allKeys = keysResult.Select(k => k.Key).ToList();
                Debug.Log($"[SaveSystem] WebGL: Found {allKeys.Count} total keys in cloud save");
            }
            catch (Exception ex)
            {
                Debug.Log($"[SaveSystem] WebGL: Failed to list all keys for screenshot matching: {ex.Message}");
                allKeys = new List<string>();
            }
#endif
            // Use Data API listing for metadata keys regardless of transport
            var keys = await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Data.Player.ListAllKeysAsync());
            foreach (var k in keys)
            {
                if (!k.Key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var res = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { k.Key }));
                if (res != null && res.TryGetValue(k.Key, out var e))
                {
                    string b64 = e.Value.GetAs<string>();
                    if (!string.IsNullOrEmpty(b64))
                    {
                        byte[] bytes = Convert.FromBase64String(b64);
                        try
                        {
                            var enc = SaveManager.Instance?.EncryptionService;
                            if (enc?.UseEncryption == true)
                                bytes = enc.MaybeDecrypt(bytes);
                        }
                        catch { }
                        var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                        if (slot != null)
                            list.Add(slot);
                    }
                }
            }
#endif
#if !REMEMBERME_CLOUDSAVE_PRESENT
            await Task.CompletedTask;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: ListRemoteSlotsAsync completed, returning {list.Count} slots before grouping");
            
            // Match screenshot keys to slots
            #if REMEMBERME_CLOUDSAVE_PRESENT
            // Always populate the latest screenshot key from cloud for Unity Cloud Save so the UI can fetch it on reload.
            foreach (var slot in list)
            {
                Debug.Log($"[SaveSystem] WebGL: Checking slot {slot.SlotNumber} for screenshot matches");
                
                // Look for screenshot keys that match this slot
                var screenshotKeys = allKeys.Where(k => k.StartsWith("screenshot_Slot_" + slot.SlotNumber + "_")).ToList();
                
                if (screenshotKeys.Any())
                {
                    // Use the most recent screenshot key (assuming timestamp in filename)
                    var latestScreenshotKey = screenshotKeys.OrderByDescending(k => k).First();
                    Debug.Log($"[SaveSystem] WebGL: Found screenshot key for slot {slot.SlotNumber}: {latestScreenshotKey}");
                    slot.ScreenshotFileName = latestScreenshotKey;
                }
                else
                {
                    Debug.Log($"[SaveSystem] WebGL: No screenshot key found for slot {slot.SlotNumber}");
                }
            }
            #endif
#endif

            // Legacy fallback: if some slots weren't found via Data API, try Files API for metadata
#if REMEMBERME_CLOUDSAVE_PRESENT
            #if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                var present = new HashSet<int>(list.Select(s => s.SlotNumber));
                for (int n = 1; n <= settings.numberOfSaveSlots; n++)
                {
                    if (present.Contains(n)) continue;
                    string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + n;
                    byte[] bytes = null;
            try { bytes = await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(metaKey)); }
                    catch { bytes = null; }
                    if (bytes != null && bytes.Length > 0)
                    {
                        try
                        {
                            var enc = SaveManager.Instance?.EncryptionService;
                            if (enc?.UseEncryption == true)
                                bytes = enc.MaybeDecrypt(bytes);
                        }
                        catch { }
                        var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                        if (slot != null) list.Add(slot);
                    }
                }
            }
            catch { }
            #endif
#endif

            var result = list
                .GroupBy(s => s.SlotNumber)
                .Select(g => g.OrderByDescending(x => x.LastSaved).First())
                .OrderBy(s => s.SlotNumber)
                .ToList();

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: ListRemoteSlotsAsync final result: {result.Count} slots after grouping");
#endif

            return result;
        }

                #endregion
        }
}
#endif
