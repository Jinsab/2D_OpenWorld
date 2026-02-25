#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
#if REMEMBERME_CLOUDSAVE_PRESENT
using Unity.Services.CloudSave;
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
using PlayerDeleteOptions = Unity.Services.CloudSave.Models.Data.Player.DeleteOptions;
#endif

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles save operations and related helpers like compression,
    /// encryption and screenshot management.
    /// </summary>
    public class SaveOperationService
    {
        readonly SaveManager      manager;
        readonly ScreenshotManager screenshotManager;
        readonly SaveDataSerializer serializer;
        readonly SaveSlotManager  slotManager;
        readonly LiveConflictResolver conflictResolver;

        public SaveOperationService(SaveManager manager,
                                    ScreenshotManager screenshotManager,
                                    SaveDataSerializer serializer,
                                    SaveSlotManager slotManager,
                                    LiveConflictResolver conflictResolver)
        {
            this.manager          = manager;
            this.screenshotManager = screenshotManager;
            this.serializer       = serializer;
            this.slotManager      = slotManager;
            this.conflictResolver = conflictResolver;
        }

        /* ================================================================== */
        #region Helper Methods
        /* ================================================================== */
        internal byte[] MaybeEncrypt(byte[] plain)
            => manager.EncryptionService?.MaybeEncrypt(plain) ?? plain;

        internal byte[] MaybeDecrypt(byte[] blob)
            => manager.EncryptionService?.MaybeDecrypt(blob) ?? blob;

        internal byte[] MaybeCompress(byte[] plain)
            => manager.CompressionService?.MaybeCompress(plain) ?? plain;

        internal byte[] MaybeDecompress(byte[] blob)
            => manager.CompressionService?.MaybeDecompress(blob) ?? blob;

        private bool UseServerSideCloudCrypto =>
            manager.SaveSettings.enableEncryption &&
            manager.SaveSettings.enableCloudSave &&
            manager.SaveSettings.cloudCryptoMode == CloudCryptoMode.ServerSide;

        private ICloudCryptoProvider CloudCryptoProvider =>
            manager.SaveSettings.cloudCryptoProvider as ICloudCryptoProvider;

        private async Task<byte[]> EncryptForSaveAsync(byte[] plain)
        {
            if (!UseServerSideCloudCrypto)
                return MaybeEncrypt(plain);

            var provider = CloudCryptoProvider;
            if (provider == null)
            {
                Logger.Log("Server-side cloud crypto enabled but no Cloud Crypto Provider assigned.",
                           LogCategory.Cryptography, LogLevel.Error);
                return null;
            }

            return await provider.EncryptForCloudAsync(plain);
        }

        private async Task<byte[]> DecryptFromCloudAsync(byte[] blob)
        {
            if (!UseServerSideCloudCrypto)
                return MaybeDecrypt(blob);

            var provider = CloudCryptoProvider;
            if (provider == null)
            {
                Logger.Log("Server-side cloud crypto enabled but no Cloud Crypto Provider assigned.",
                           LogCategory.Cryptography, LogLevel.Error);
                return null;
            }

            return await provider.DecryptFromCloudAsync(blob);
        }

        internal bool ValidateSavedSlot(SaveSlot slot)
        {
            try
            {
                if (UseServerSideCloudCrypto)
                    return true;
        // WebGL + MySQL: synchronous load is not supported – skip sync verification
#if UNITY_WEBGL && !UNITY_EDITOR
        if (manager.SaveSystem is MySqlSaveSystem || manager.SaveSystem is SupabaseSaveSystem)
            return true;
#endif
        byte[] bytes = manager.SaveSystem.Load(slot);
                if (bytes == null || bytes.Length == 0)
                    return false;

                bytes = MaybeDecrypt(bytes);
                bytes = MaybeDecompress(bytes);
                var data = serializer.Deserialize<SaveData>(bytes);
                return data != null;
            }
            catch
            {
                return false;
            }
        }

        internal async Task<bool> ValidateTempSlotAsync(SaveSlot slot)
        {
            try
            {
                if (UseServerSideCloudCrypto)
                    return true;
                byte[] bytes = await manager.SaveSystem.LoadTempAsync(slot);
                if (bytes == null || bytes.Length == 0)
                    return false;

                bytes = MaybeDecrypt(bytes);
                bytes = MaybeDecompress(bytes);
                var data = serializer.Deserialize<SaveData>(bytes);
                return data != null;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        /* ================================================================== */
        #region Conflict Resolution
        /* ================================================================== */

        internal async Task<SaveData> ResolveSlotDataAsync(SaveSlot slot)
        {
            byte[] localBytes = null;
            // On WebGL, avoid pre-loading via Unity Cloud Save – it can stall. Only load for backends that need a local baseline (Supabase/MySQL).
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!UseServerSideCloudCrypto)
            {
                if (manager.SaveSettings.backend == SaveBackend.Supabase || manager.SaveSystem is MySqlSaveSystem)
                    localBytes = await manager.SaveSystem.LoadAsync(slot).ConfigureAwait(false);
                else
                    localBytes = null; // Skip local load for Unity Cloud Save to prevent hangs
            }
            else
            {
                localBytes = null;
            }
#else
            if (!UseServerSideCloudCrypto)
            {
                if (manager.SaveSettings.backend == SaveBackend.Supabase || manager.SaveSystem is MySqlSaveSystem)
                    localBytes = await manager.SaveSystem.LoadAsync(slot).ConfigureAwait(false);
                else
                    localBytes = manager.SaveSystem.Load(slot);
            }
            else
            {
                localBytes = null;
            }
#endif
            SaveData localData = null;
            if (localBytes != null && localBytes.Length > 0)
            {
                // Correct order: decrypt -> decompress; handle nulls gracefully
                localBytes = MaybeDecrypt(localBytes);
                if (localBytes != null && localBytes.Length > 0)
                    localBytes = MaybeDecompress(localBytes);
                try
                {
                    if (localBytes != null && localBytes.Length > 0)
                        localData = serializer.Deserialize<SaveData>(localBytes);
                }
                catch
                {
                    localData = null;
                }
            }

            SaveData cloudData = null;
            SaveSlot cloudMeta = null;
            if (manager.SaveSettings.enableCloudSave)
            {
                bool connected = await manager.WaitForCloudConnectionAsync();
                if (connected)
                {
                    byte[] cloudBytes = await manager.SaveSystem.LoadAsync(slot);
                    if (cloudBytes != null && cloudBytes.Length > 0)
                    {
                        // Correct order + null-guards
                        cloudBytes = await DecryptFromCloudAsync(cloudBytes);
                        if (cloudBytes != null && cloudBytes.Length > 0)
                            cloudBytes = MaybeDecompress(cloudBytes);
                        try
                        {
                            if (cloudBytes != null && cloudBytes.Length > 0)
                                cloudData = serializer.Deserialize<SaveData>(cloudBytes);
                        }
                        catch
                        {
                            cloudData = null;
                        }
                    }
                    try
                    {
                        cloudMeta = await manager.SaveSystem.LoadSlotMetadataAsync(slot.SlotNumber);
                    }
                    catch { }
                }
            }

            if (localData != null && cloudData != null && localData.LastSaved != cloudData.LastSaved)
            {
                var settings = manager.SaveSettings;
                if (settings.autoResolveConflicts)
                {
                    switch (settings.autoConflictPolicy)
                    {
                        case AutoConflictPolicy.Latest:
                            return localData.LastSaved >= cloudData.LastSaved ? localData : cloudData;
                        case AutoConflictPolicy.Oldest:
                            return localData.LastSaved <= cloudData.LastSaved ? localData : cloudData;
                        case AutoConflictPolicy.LocalWins:
                            return localData;
                        case AutoConflictPolicy.CloudWins:
                            return cloudData;
                        case AutoConflictPolicy.Custom:
                            bool localMatch = MatchesRules(slot.CustomMetadata, settings.metadataRules, localData, cloudData, true);
                            bool cloudMatch = MatchesRules(cloudMeta?.CustomMetadata ?? new Dictionary<string, string>(), settings.metadataRules, localData, cloudData, false);
                            if (localMatch && !cloudMatch) return localData;
                            if (cloudMatch && !localMatch) return cloudData;
                            break;
                    }
                }

                var tcs = new TaskCompletionSource<SaveData>();
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    if (conflictResolver != null)
                        conflictResolver.Resolve(localData, cloudData, r => tcs.SetResult(r));
                    else
                        tcs.SetResult(localData);
                });
                return await tcs.Task;
            }

            return cloudData ?? localData;
        }

        internal void ResolveSlotDataSync(SaveSlot slot)
        {
            // Avoid blocking on platforms/backends that require async
#if UNITY_WEBGL && !UNITY_EDITOR
            Task.Run(() => ResolveSlotDataAsync(slot));
#else
            // Run the async resolver on a background thread to prevent blocking the main thread.
            // Interactive conflict resolution requires the main thread and can deadlock if we
            // synchronously wait here, so we perform resolution asynchronously and return.
            Task.Run(() => ResolveSlotDataAsync(slot));
#endif
        }

        static bool MatchesRules(Dictionary<string, string> meta, MetadataRule[] rules, SaveData local, SaveData cloud, bool isLocal)
        {
            if (rules == null || rules.Length == 0)
                return false;

            int count = Math.Min(rules.Length, 2);
            for (int i = 0; i < count; i++)
            {
                var r = rules[i];
                switch (r.type)
                {
                    case MetadataRuleType.Metadata:
                        if (string.IsNullOrEmpty(r.key))
                            return false;
                        if (!meta.TryGetValue(r.key, out string val))
                            return false;
                        if (!Compare(val, r.value, r.op))
                            return false;
                        break;
                    case MetadataRuleType.Latest:
                        if (local == null || cloud == null)
                            return false;
                        if (isLocal)
                        {
                            if (local.LastSaved < cloud.LastSaved)
                                return false;
                        }
                        else if (cloud.LastSaved < local.LastSaved)
                            return false;
                        break;
                    case MetadataRuleType.Oldest:
                        if (local == null || cloud == null)
                            return false;
                        if (isLocal)
                        {
                            if (local.LastSaved > cloud.LastSaved)
                                return false;
                        }
                        else if (cloud.LastSaved > local.LastSaved)
                            return false;
                        break;
                    case MetadataRuleType.LocalWins:
                        if (!isLocal)
                            return false;
                        break;
                    case MetadataRuleType.CloudWins:
                        if (isLocal)
                            return false;
                        break;
                }
            }
            return true;
        }

        static bool Compare(string lhs, string rhs, ComparisonOp op)
        {
            bool leftNum = double.TryParse(lhs, out double lv);
            bool rightNum = double.TryParse(rhs, out double rv);
            if (leftNum && rightNum)
            {
                return op switch
                {
                    ComparisonOp.Equals => lv == rv,
                    ComparisonOp.Smaller => lv < rv,
                    ComparisonOp.Larger => lv > rv,
                    _ => false
                };
            }

            int cmp = string.CompareOrdinal(lhs, rhs);
            return op switch
            {
                ComparisonOp.Equals => cmp == 0,
                ComparisonOp.Smaller => cmp < 0,
                ComparisonOp.Larger => cmp > 0,
                _ => false
            };
        }

        #endregion

        /* ================================================================== */
        #region Screenshot API
        /* ================================================================== */
        public Texture2D GetScreenshot(SaveSlot slot)
            => screenshotManager.LoadScreenshot(slot);

        public string CaptureScreenshotSync(int slotNumber)
            => screenshotManager.CaptureScreenshotSync(slotNumber);

        internal void DeleteExistingScreenshot(SaveSlot slot)
        {
            if (!manager.SaveSettings.enableScreenshots ||
                string.IsNullOrEmpty(slot?.ScreenshotFileName))
                return;

            screenshotManager.DeleteScreenshot(slot.ScreenshotFileName);

            if (manager.SaveSettings.enableCloudSave)
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (manager.SaveSystem is SupabaseSaveSystem supabase)
                {
                    try { Task.Run(async () => await supabase.DeleteScreenshotAsync(slot.ScreenshotFileName)).GetAwaiter().GetResult(); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"DeleteExistingScreenshot: {ex.Message}");
                    }
                }
                else if (manager.SaveSystem is FirebaseSaveSystem firebase)
                {
                    try { Task.Run(async () => await firebase.DeleteScreenshotAsync(slot.ScreenshotFileName)).GetAwaiter().GetResult(); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"DeleteExistingScreenshot: {ex.Message}");
                    }
                }
#endif

#if REMEMBERME_CLOUDSAVE_PRESENT
                if (manager.SaveSettings.backend == SaveBackend.UnityCloudSave &&
                    manager.SaveSettings.cloudSaveScreenshots)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    // WebGL requires Data API - normalize to structured key format.
                    // If already a Data API key (starts with "screenshot_"), use as-is; otherwise, strip extension and prefix once.
                    string dataApiKey = slot.ScreenshotFileName;
                    if (!string.IsNullOrEmpty(dataApiKey))
                    {
                        if (dataApiKey.StartsWith("screenshot_"))
                        {
                            // Already normalized; leave unchanged
                        }
                        else
                        {
                            if (dataApiKey.Contains('.'))
                                dataApiKey = dataApiKey.Substring(0, dataApiKey.LastIndexOf('.'));
                            dataApiKey = $"screenshot_{dataApiKey}";
                        }
                    }
                    try { Task.Run(async () => await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.DeleteAsync(dataApiKey, new PlayerDeleteOptions())).GetAwaiter().GetResult(); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"DeleteExistingScreenshot (WebGL): {ex.Message}");
                    }
#else
                    // Non-WebGL platforms can use Files API
                    try { Task.Run(async () => await Unity.Services.CloudSave.CloudSaveService.Instance.Files.Player.DeleteAsync(slot.ScreenshotFileName)).GetAwaiter().GetResult(); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"DeleteExistingScreenshot: {ex.Message}");
                    }
#endif
                }
#endif
            }

            slot.ScreenshotFileName = string.Empty;
        }

        internal IEnumerator DeleteExistingScreenshotCoroutine(SaveSlot slot)
        {
            if (!manager.SaveSettings.enableScreenshots ||
                string.IsNullOrEmpty(slot?.ScreenshotFileName))
                yield break;

            screenshotManager.DeleteScreenshot(slot.ScreenshotFileName);

            if (manager.SaveSettings.enableCloudSave)
            {
                const float timeout = 10f;
                if (manager.SaveSystem is SupabaseSaveSystem supabase)
                {
                    var waitTask = supabase.DeleteScreenshotAsync(slot.ScreenshotFileName);
                    float elapsed = 0f;
                    while (!waitTask.IsCompleted && elapsed < timeout)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (!waitTask.IsCompleted)
                    {
                        Debug.LogWarning("DeleteExistingScreenshotCoroutine: Supabase screenshot delete timed out.");
                    }
                    else if (waitTask.IsFaulted)
                    {
                        var msg = waitTask.Exception?.ToString();
                        if (string.IsNullOrEmpty(msg) || (msg.IndexOf("404", StringComparison.OrdinalIgnoreCase) < 0 && msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) < 0))
                            Debug.LogWarning($"DeleteExistingScreenshotCoroutine: {waitTask.Exception?.GetBaseException().Message}");
                    }
                }
                else if (manager.SaveSystem is FirebaseSaveSystem firebase)
                {
                    var waitTask = firebase.DeleteScreenshotAsync(slot.ScreenshotFileName);
                    float elapsed = 0f;
                    while (!waitTask.IsCompleted && elapsed < timeout)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (!waitTask.IsCompleted)
                    {
                        Debug.LogWarning("DeleteExistingScreenshotCoroutine: Firebase screenshot delete timed out.");
                    }
                    else if (waitTask.IsFaulted)
                    {
                        var msg = waitTask.Exception?.ToString();
                        if (string.IsNullOrEmpty(msg) || (msg.IndexOf("404", StringComparison.OrdinalIgnoreCase) < 0 && msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) < 0))
                            Debug.LogWarning($"DeleteExistingScreenshotCoroutine: {waitTask.Exception?.GetBaseException().Message}");
                    }
                }
#if REMEMBERME_CLOUDSAVE_PRESENT
                else if (manager.SaveSettings.backend == SaveBackend.UnityCloudSave &&
                         manager.SaveSettings.cloudSaveScreenshots)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    // WebGL requires Data API - normalize to structured key format.
                    string dataApiKey = slot.ScreenshotFileName;
                    if (!string.IsNullOrEmpty(dataApiKey))
                    {
                        if (dataApiKey.StartsWith("screenshot_"))
                        {
                            // Already normalized
                        }
                        else
                        {
                            if (dataApiKey.Contains('.'))
                                dataApiKey = dataApiKey.Substring(0, dataApiKey.LastIndexOf('.'));
                            dataApiKey = $"screenshot_{dataApiKey}";
                        }
                    }
                    var waitTask = Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.DeleteAsync(dataApiKey, new PlayerDeleteOptions());
#else
                    // Non-WebGL platforms can use Files API
                    var waitTask = Unity.Services.CloudSave.CloudSaveService.Instance.Files.Player.DeleteAsync(slot.ScreenshotFileName);
#endif
                    float elapsed = 0f;
                    while (!waitTask.IsCompleted && elapsed < timeout)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (!waitTask.IsCompleted)
                    {
                        Debug.LogWarning("DeleteExistingScreenshotCoroutine: Unity Cloud Save screenshot delete timed out.");
                    }
                    else if (waitTask.IsFaulted)
                    {
                        var msg = waitTask.Exception?.ToString();
                        if (string.IsNullOrEmpty(msg) || (msg.IndexOf("404", StringComparison.OrdinalIgnoreCase) < 0 && msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) < 0))
                            Debug.LogWarning($"DeleteExistingScreenshotCoroutine: {waitTask.Exception?.GetBaseException().Message}");
                    }
                }
#endif
            }

            slot.ScreenshotFileName = string.Empty;
        }

        public void CleanupScreenshots()
            => screenshotManager.CleanupUnusedScreenshots(slotManager.Slots);
        #endregion

        /* ================================================================== */
        #region Save API
        /* ================================================================== */
        public void Save(int slotNumber = 1, string lastActiveScene = null)
        {
            var slot = slotManager.GetByNumber(slotNumber);
            if (slot == null)
            {
                Logger.Log($"Save failed: Save slot {slotNumber} does not exist.", LogCategory.SaveManager, LogLevel.Error);
                return;
            }

            manager.StartCoroutine(SaveCoroutine(slot, lastActiveScene));
        }

        public Task SaveAsync(int slotNumber = 1,
                              string lastActiveScene = null,
                              CancellationToken ct = default)
        {
            return SaveAsync(slotNumber, lastActiveScene, null, ct);
        }

        public Task SaveAsync(int slotNumber,
                              string lastActiveScene,
                              string customSlotName,
                              CancellationToken ct = default)
        {
            var slot = slotManager.GetByNumber(slotNumber);
            if (slot == null)
            {
                return Task.FromException(new ArgumentException($"Slot {slotNumber} does not exist."));
            }

            // Preserve the original slot name so we can roll back on failure
            string originalSlotName = slot.SlotName;

            // Apply the custom slot name immediately so any listeners of
            // OnSaveCompleted receive the updated name.
            if (!string.IsNullOrEmpty(customSlotName))
                slot.SlotName = customSlotName;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Coroutine running = null;
            CancellationTokenRegistration? ctr = null;

            void Unsub()
            {
                manager.OnSaveCompleted -= Completed;
                manager.OnSaveFailed    -= Failed;
                if (ctr.HasValue) ctr.Value.Dispose();
                if (running != null) manager.StopCoroutine(running);
            }

            void Completed(object _, SaveLoadEventArgs e)
            {
                if (e.Slot != slot) return;
                Unsub();
                
                tcs.TrySetResult(true);
            }

            void Failed(object _, OperationFailedEventArgs e)
            {
                if (e.Slot != slot) return;

                // Restore the original name if the save failed
                if (!string.IsNullOrEmpty(customSlotName))
                    slot.SlotName = originalSlotName;

                Unsub();
                tcs.TrySetException(new Exception($"SaveAsync failed: {e.ErrorMessage}"));
            }

            if (ct.CanBeCanceled)
                ctr = ct.Register(() =>
                {
                    // Restore the original name if the operation was cancelled
                    if (!string.IsNullOrEmpty(customSlotName))
                        slot.SlotName = originalSlotName;

                    Unsub();
                    tcs.TrySetCanceled(ct);
                });

            manager.OnSaveCompleted += Completed;
            manager.OnSaveFailed    += Failed;

            running = manager.StartCoroutine(SaveCoroutine(slot, lastActiveScene));

            return tcs.Task;
        }

        public async Task SaveImmediateAsync(int slotNumber = 1,
                                             string lastActiveScene = null)
        {
            var slot = slotManager.GetByNumber(slotNumber);
            if (slot == null)
            {
                manager.HandleSaveCompletion(null, false,
                    $"SaveImmediateAsync: slot {slotNumber} does not exist.");
                return;
            }

            // Ensure lookup cache consistency before saving
            // This is critical when enableLookupCache is true to prevent first-load issues
            manager.EnsureCacheConsistency();

#if !REMEMBERME_AUTHENTICATION_PRESENT || !REMEMBERME_CORESERVICES_PRESENT
            if (manager.SaveSettings.enableCloudSave &&
                manager.SaveSettings.backend == SaveBackend.UnityCloudSave)
            {
                SaveManagerDefineSymbolsWarning.WarnOnSaveAttempt();
                manager.HandleSaveCompletion(slot, false,
                    SaveManagerDefineSymbolsWarning.MissingDefineSymbolsMessage);
                return;
            }
#endif

            if (manager.SaveSettings.enableCloudSave)
            {
                bool connected = await manager.WaitForCloudConnectionAsync();
                if (!connected)
                {
                    manager.HandleSaveCompletion(slot, false, "Unable to save: connection to cloud provider not established.");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(lastActiveScene) && !manager.IsSceneInBuild(lastActiveScene))
            {
                manager.HandleSaveCompletion(slot, false,
                    $"Scene \"{lastActiveScene}\" is not in build settings.");
                return;
            }

            await ResolveSlotDataAsync(slot);

            try
            {
                SaveableComponent.ResetSaveCallCounts();
                SaveData data = manager.CollectSaveData(lastActiveScene);
                lock (manager.TrackedGameObjectsLock)
                    data.TrackedUniqueIDs = manager.TrackedGameObjects.Keys.ToList();

                var dupSaves = SaveableComponent.SaveCallCounts.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
                if (dupSaves.Count > 0)
                    Debug.LogWarning($"SaveImmediateAsync: Duplicate SaveData calls for {string.Join(", ", dupSaves)}");
                else
                    Debug.Log($"SaveImmediateAsync: Serialized {SaveableComponent.SaveCallCounts.Count} components.");

                slot.LastSaved = DateTime.Now;
                slot.LastActiveScene = data.LastActiveScene;
                manager.ApplyDefaultSlotMetadata(slot);

                byte[] plain = serializer.Serialize(data);
                if (manager.SyncManager != null && manager.SaveSettings.enableSync)
                {
                    try
                    {
                        if (manager.SaveSettings.syncSettings != null &&
                            manager.SaveSettings.syncSettings.enableDiffs)
                        {
                            await manager.SyncManager.SendDiffFromSnapshotAsync(plain, slot.SlotNumber.ToString());
                        }
                        else
                        {
                            await manager.SyncManager.SendSnapshotAsync(plain, slot.SlotNumber.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"SyncManager send failed: {ex.Message}", LogCategory.SaveManager, LogLevel.Warning);
                    }
                }
                // Standardize order: compress first, then encrypt
                byte[] compressed = MaybeCompress(plain);
                byte[] blob  = await EncryptForSaveAsync(compressed);
                if (blob == null || blob.Length == 0)
                {
                    manager.HandleSaveCompletion(slot, false, "Save failed: encryption step failed.");
                    return;
                }

                bool localMirror = !manager.SaveSettings.enableCloudSave ||
                                   (manager.SaveSettings.enableCloudSave &&
                                    manager.SaveSettings.keepLocalMirror);

                if (UseServerSideCloudCrypto)
                    localMirror = false;

                bool tempVerify = manager.SaveSettings.enableSaveFileVerification &&
                                   !localMirror &&
                                   !UseServerSideCloudCrypto;

                if (tempVerify)
                {
                    await manager.SaveSystem.WriteTempAsync(blob, slot).ConfigureAwait(false);
                    bool okTemp = await ValidateTempSlotAsync(slot).ConfigureAwait(false);
                    if (!okTemp)
                    {
                        manager.SaveSystem.DeleteTemp(slot);
                        manager.InvokeVerificationFailed(slot, "Verification", "Save file corrupted after write.");
                        manager.HandleSaveCompletion(slot, false, "Save file corrupted.");
                        return;
                    }
                }

                if (manager.SaveSettings.enableSaveFileVerification)
                {
                    try
                    {
                        manager.SaveSystem.BackupSlot(slot);
                    }
                    catch (Exception ex)
                    {
                        manager.InvokeBackupFailed(slot, "Backup", ex.Message);
                    }
                }

                if (manager.SaveSettings.enableCloudSave)
                    await manager.SaveSystem.SaveAsync(blob, slot).ConfigureAwait(false);
                else
                    manager.SaveSystem.Save(blob, slot);

                if (tempVerify)
                    manager.SaveSystem.DeleteTemp(slot);

                if (manager.SaveSettings.enableSaveFileVerification &&
                    localMirror && !ValidateSavedSlot(slot))
                {
                    manager.SaveSystem.RestoreBackup(slot);
                    manager.InvokeVerificationFailed(slot, "Verification", "Save file corrupted after write.");
                    manager.HandleSaveCompletion(slot, false,
                        "Save file corrupted. Restored backup.");
                    return;
                }

                if (manager.SaveSettings.enableSaveFileVerification)
                    manager.SaveSystem.DeleteBackup(slot);

                if (manager.UseLocalMirror)
                    await manager.SaveSystem.SaveSlotMetadataAsync(slot).ConfigureAwait(false);

                manager.HandleSaveCompletion(slot, true);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                manager.HandleSaveCompletion(slot, false, ex.Message);
            }
        }

        internal IEnumerator SaveCoroutine(SaveSlot slot, string lastActiveScene = null)
        {
            Logger.Log($"[SaveCoroutine] Starting save for slot {slot?.SlotNumber}", LogCategory.SaveManager, LogLevel.Info);
            
            // Ensure lookup cache consistency before saving
            // This is critical when enableLookupCache is true to prevent first-load issues
            manager.EnsureCacheConsistency();

#if !REMEMBERME_AUTHENTICATION_PRESENT || !REMEMBERME_CORESERVICES_PRESENT
            if (manager.SaveSettings.enableCloudSave &&
                manager.SaveSettings.backend == SaveBackend.UnityCloudSave)
            {
                SaveManagerDefineSymbolsWarning.WarnOnSaveAttempt();
                manager.HandleSaveCompletion(slot, false,
                    SaveManagerDefineSymbolsWarning.MissingDefineSymbolsMessage);
                yield break;
            }
#endif
            if (manager.SaveSettings.enableCloudSave)
            {
                // For Supabase Custom with no local mirror, avoid indefinite waits when not logged in
                if (manager.SaveSettings.backend == SaveBackend.Supabase &&
                    manager.SaveSettings.userFolderStrategy == UserFolderStrategy.Custom &&
                    !manager.IsSupabaseCustomLoggedIn &&
                    !manager.UseLocalMirror)
                {
                    manager.HandleSaveCompletion(slot, false, "Not logged in to Supabase.");
                    yield break;
                }

                var waitTask = manager.WaitForCloudConnectionAsync();
                while (!waitTask.IsCompleted) yield return null;
                if (waitTask.IsFaulted || !waitTask.Result)
                {
                    string err = waitTask.IsFaulted ? (waitTask.Exception?.GetBaseException().Message ?? "Cloud connection failed") : "Unable to save: connection to cloud provider not established.";
                    manager.HandleSaveCompletion(slot, false, err);
                    yield break;
                }
            }

            if (!string.IsNullOrEmpty(lastActiveScene) && !manager.IsSceneInBuild(lastActiveScene))
            {
                Logger.Log($"SaveManager: Provided LastActiveScene '{lastActiveScene}' does not exist in build settings.", LogCategory.SaveManager, LogLevel.Error);
                manager.HandleSaveCompletion(slot, false,
                    $"LastActiveScene '{lastActiveScene}' does not exist.");
                yield break;
            }

            bool skipResolve = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (manager.SaveSystem is MySqlSaveSystem || manager.SaveSystem is SupabaseSaveSystem)
            {
                skipResolve = true;
                Debug.Log("[SaveOperationService] WebGL+(MySQL/Supabase): Skipping ResolveSlotDataAsync to avoid preloads");
            }
#endif
            if (!skipResolve)
            {
                var resolveTask = ResolveSlotDataAsync(slot);
                while (!resolveTask.IsCompleted) yield return null;
                if (resolveTask.IsFaulted)
                {
                    string err = resolveTask.Exception?.GetBaseException().Message ?? "Conflict resolution failed";
                    manager.HandleSaveCompletion(slot, false, err);
                    yield break;
                }
            }

            SaveableComponent.ResetSaveCallCounts();
            string screenshotFileName = null;
            var saveSettings = manager.SaveSettings;
            var saveSystem = manager.SaveSystem;

            Logger.Log($"[SaveCoroutine] Screenshot enabled: {saveSettings.enableScreenshots}", LogCategory.SaveManager, LogLevel.Info);

            if (saveSettings.enableScreenshots)
            {
                if (!string.IsNullOrEmpty(slot.ScreenshotFileName))
                {
                    Logger.Log($"[SaveCoroutine] Deleting existing screenshot: {slot.ScreenshotFileName}", LogCategory.SaveManager, LogLevel.Info);
                    yield return DeleteExistingScreenshotCoroutine(slot);
                }

                manager.RaiseScreenshotCaptureStarted();

                bool shotDone = false;
                Logger.Log($"[SaveCoroutine] Starting screenshot capture...", LogCategory.SaveManager, LogLevel.Info);
                
                // Start the screenshot coroutine WITHOUT yielding on it directly
                // This allows us to implement a proper timeout that actually works
                Coroutine screenshotCoroutine = null;
                try
                {
                    screenshotCoroutine = manager.StartCoroutine(screenshotManager.CaptureScreenshot(slot.SlotNumber, fileName =>
                    {
                        Logger.Log($"[SaveCoroutine] Screenshot callback received: {fileName ?? "null"}", LogCategory.SaveManager, LogLevel.Info);
                        screenshotFileName = fileName;
                        shotDone = true;
                    }));
                }
                catch (System.Exception ex)
                {
                    Logger.Log($"[SaveCoroutine] Failed to start screenshot coroutine: {ex.Message}", LogCategory.SaveManager, LogLevel.Error);
                    shotDone = true; // Mark as done to skip waiting
                }
                
                // Wait for screenshot with timeout
                float timeout = 5f; // 5 seconds timeout
                float elapsed = 0f;
                while (!shotDone && elapsed < timeout)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    
                    // Log progress every 5 seconds to show we're still running
                    if (elapsed > 0 && ((int)elapsed % 5 == 0) && ((int)(elapsed - Time.unscaledDeltaTime) % 5 != 0))
                    {
                        Logger.Log($"[SaveCoroutine] Waiting for screenshot... {elapsed:F1}s elapsed", LogCategory.SaveManager, LogLevel.Info);
                    }
                }
                
                if (!shotDone)
                {
#if UNITY_EDITOR
                    Logger.Log($"[CrystalSave] Screenshot capture timed out - No screenshot was saved.", LogCategory.SaveManager, LogLevel.Warning);
                    Logger.Log($"[CrystalSave] This happens when the Scene view is active instead of the Game view during save.", LogCategory.SaveManager, LogLevel.Warning);
                    Logger.Log($"[CrystalSave] This is an Editor-only issue and will NOT occur in a built game.", LogCategory.SaveManager, LogLevel.Info);
#else
                    Logger.Log($"[SaveCoroutine] Screenshot capture timed out after {timeout}s - continuing without screenshot", LogCategory.SaveManager, LogLevel.Warning);
#endif
                    // Try to stop the hanging coroutine
                    if (screenshotCoroutine != null)
                    {
                        try
                        {
                            manager.StopCoroutine(screenshotCoroutine);
                        }
                        catch (System.Exception ex)
                        {
                            Logger.Log($"[SaveCoroutine] Failed to stop screenshot coroutine: {ex.Message}", LogCategory.SaveManager, LogLevel.Warning);
                        }
                    }
                    // Continue without screenshot rather than hang forever
                    screenshotFileName = null;
                }

                Logger.Log($"[SaveCoroutine] Screenshot capture complete", LogCategory.SaveManager, LogLevel.Info);
                manager.RaiseScreenshotCaptureFinished();
            }

            Logger.Log($"[SaveCoroutine] Starting data collection...", LogCategory.SaveManager, LogLevel.Info);
            SaveData data;
            try
            {
                data = manager.CollectSaveData(lastActiveScene);
                Logger.Log($"[SaveCoroutine] Data collection complete. Prefabs: {data?.Prefabs?.Count ?? 0}", LogCategory.SaveManager, LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                Logger.Log($"SaveCoroutine: Failed to collect save data: {ex.Message}", LogCategory.SaveManager, LogLevel.Error);
                UnityEngine.Debug.LogException(ex);
                manager.HandleSaveCompletion(slot, false, $"Failed to collect save data: {ex.Message}");
                yield break;
            }
            
            lock (manager.TrackedGameObjectsLock)
                data.TrackedUniqueIDs = manager.TrackedGameObjects.Keys.ToList();

            var dupSaves = SaveableComponent.SaveCallCounts.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
            if (dupSaves.Count > 0)
                Debug.LogWarning($"SaveCoroutine: Duplicate SaveData calls for {string.Join(", ", dupSaves)}");

            slot.LastSaved = DateTime.Now;
            slot.ScreenshotFileName = screenshotFileName;
            slot.LastActiveScene = data.LastActiveScene;
            manager.ApplyDefaultSlotMetadata(slot);

            Logger.Log($"[SaveCoroutine] Starting serialization...", LogCategory.SaveManager, LogLevel.Info);
            byte[] serialized = null;
            Exception serEx = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            try { serialized = serializer.Serialize(data); }
            catch (Exception ex) { serEx = ex; }
            yield return null; // preserve async behavior
#else
            bool done = false;
            Task.Run(() =>
            {
                try { serialized = serializer.Serialize(data); }
                catch (Exception ex) { serEx = ex; }
                finally { done = true; }
            });
            yield return new WaitUntil(() => done);
#endif

            if (serEx != null)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    manager.HandleSaveCompletion(slot, false, serEx.Message);
                });
                yield break;
            }

            serialized = MaybeCompress(serialized);
            var encTask = EncryptForSaveAsync(serialized);
            yield return manager.AwaitTask(encTask);
            serialized = encTask.Result;
            if (serialized == null || serialized.Length == 0)
            {
                manager.HandleSaveCompletion(slot, false, "Save failed: encryption step failed.");
                yield break;
            }

            bool localMirror = !manager.SaveSettings.enableCloudSave ||
                               (manager.SaveSettings.enableCloudSave &&
                                manager.SaveSettings.keepLocalMirror);

            if (UseServerSideCloudCrypto)
                localMirror = false;

            bool tempVerify = manager.SaveSettings.enableSaveFileVerification &&
                               !localMirror &&
                               !UseServerSideCloudCrypto;
#if UNITY_WEBGL && !UNITY_EDITOR
            if ((manager.SaveSystem is MySqlSaveSystem || manager.SaveSystem is SupabaseSaveSystem) && tempVerify)
            {
                // Avoid temp verification roundtrip on WebGL for MySQL backend
                tempVerify = false;
                Debug.Log("[SaveOperationService] WebGL+(MySQL/Supabase): Disabling temp verify to prevent stalls");
            }
#endif

            if (tempVerify)
            {
                var wTask = saveSystem.WriteTempAsync(serialized, slot);
                yield return manager.AwaitTask(wTask);
                var vTask = ValidateTempSlotAsync(slot);
                yield return manager.AwaitTask(vTask);
                if (!vTask.Result)
                {
                    saveSystem.DeleteTemp(slot);
                    manager.InvokeVerificationFailed(slot, "Verification", "Save file corrupted after write.");
                    manager.HandleSaveCompletion(slot, false, "Save file corrupted.");
                    yield break;
                }
            }

            if (manager.SaveSettings.enableSaveFileVerification)
            {
                try
                {
                    manager.SaveSystem.BackupSlot(slot);
                }
                catch (Exception ex)
                {
                    manager.InvokeBackupFailed(slot, "Backup", ex.Message);
                }
            }

            if (saveSettings.enableCloudSave)
            {
                Task saveTask = null;
                try
                {
                    saveTask = saveSystem.SaveAsync(serialized, slot);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Cloud save failed for slot {slot.SlotNumber}: {ex.Message}", LogCategory.SaveManager, LogLevel.Error);
                    manager.HandleSaveCompletion(slot, false, ex.Message);
                    yield break;
                }

                while (!saveTask.IsCompleted) yield return null;
                if (saveTask.IsFaulted)
                {
                    string err = saveTask.Exception?.InnerException?.Message ?? saveTask.Exception?.Message;
                    Logger.Log($"Cloud save failed for slot {slot.SlotNumber}: {err}", LogCategory.SaveManager, LogLevel.Error);
                    manager.HandleSaveCompletion(slot, false, err);
                    yield break;
                }

                Logger.Log($"Cloud save successful for slot {slot.SlotNumber}.", LogCategory.SaveManager, LogLevel.Info);
            }
            else
            {
                bool ok = false; string err = null;
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    try { saveSystem.Save(serialized, slot); ok = true; }
                    catch (Exception ex) { err = ex.Message; }
                });
                yield return null;
                if (!ok)
                {
                    if (manager.SaveSettings.enableSaveFileVerification)
                        manager.SaveSystem.RestoreBackup(slot);
                    manager.HandleSaveCompletion(slot, false, err);
                    yield break;
                }
            }

            if (tempVerify)
                saveSystem.DeleteTemp(slot);

            if (manager.SaveSettings.enableSaveFileVerification &&
                localMirror && !ValidateSavedSlot(slot))
            {
                manager.SaveSystem.RestoreBackup(slot);
                manager.InvokeVerificationFailed(slot, "Verification", "Save file corrupted after write.");
                manager.HandleSaveCompletion(slot, false, "Save file corrupted. Restored backup.");
                yield break;
            }

            if (manager.SaveSettings.enableSaveFileVerification)
                manager.SaveSystem.DeleteBackup(slot);

            Task metaTask = manager.UseLocalMirror && !saveSettings.enableCloudSave
                ? saveSystem.SaveSlotMetadataAsync(slot)
                : Task.CompletedTask;

            while (!metaTask.IsCompleted) yield return null;

            // Cloud-only: ensure remote metadata (incl. SlotName) is visible to the app before we signal completion
            if (saveSettings.enableCloudSave && !manager.UseLocalMirror)
            {
                // Best-effort: refresh remote slots once; if the name hasn't propagated yet, retry a few times quickly
                const int maxAttempts = 5;
                const float backoffSeconds = 0.2f;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var refreshTask = manager.RefreshRemoteSlotsAsync();
                    yield return manager.AwaitTask(refreshTask);

                    var updated = manager.GetSaveSlotByNumber(slot.SlotNumber);
                    if (updated != null && string.Equals(updated.SlotName, slot.SlotName, StringComparison.Ordinal))
                        break; // propagated

                    yield return new WaitForSecondsRealtime(backoffSeconds);
                }
            }

            if (metaTask.IsFaulted)
            {
                string err = metaTask.Exception?.Flatten()?.InnerException?.Message;
                manager.HandleSaveCompletion(slot, false, err);
            }
            else
            {
                manager.HandleSaveCompletion(slot, true);
            }
        }
        #endregion
    }
}
#endif
