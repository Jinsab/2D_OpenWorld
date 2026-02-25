#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles creation and management of <see cref="SaveSlot"/> instances.
    /// </summary>
    public class SaveSlotManager
    {
        private readonly ISaveSystem     saveSystem;
        private readonly SaveSettings    saveSettings;
        private readonly ScreenshotManager screenshotManager;
        private readonly string rootPath;

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        private static bool SignedIn =>
            Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
#else
        private static bool SignedIn => false;
#endif

        private bool CloudSignedIn => saveSettings.backend == SaveBackend.MySQL || 
                                      saveSettings.backend == SaveBackend.Supabase || 
                                      SignedIn;

        private static string Sanitize(string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return key;
        }

        // Tracks in-flight and completed cloud existence probes
        private readonly Dictionary<int, Task> cloudProbes = new();
        private readonly Dictionary<int, bool> cloudExistence = new();
        
        // Instance-level initialization tracking
        private bool isInitializing = false;

        private async Task<bool> SaveFileExistsAsync(SaveSlot slot, bool hasMetaData)
        {
            _ = hasMetaData; // Parameter retained for API compatibility
            Logger.Log($"SaveFileExistsAsync: Checking slot {slot.SlotNumber}, saveMethod={saveSettings.saveMethod}, enableCloudSave={saveSettings.enableCloudSave}, keepLocalMirror={saveSettings.keepLocalMirror}", LogCategory.SaveSlotManager, LogLevel.Info);

            // First check for a local representation of the save.
            if (saveSettings.saveMethod == SaveMethod.PlayerPrefs)
            {
                if (PlayerPrefs.HasKey(saveSettings.saveKey + slot.SlotNumber))
                    return true;

                if (saveSettings.enableCloudSave)
                {
                    if (cloudExistence.TryGetValue(slot.SlotNumber, out bool exists))
                        return exists;

                    // Wait for cloud probe to complete
                    BeginCloudProbe(slot);
                    Task probeTask;
                    lock (cloudProbes)
                    {
                        cloudProbes.TryGetValue(slot.SlotNumber, out probeTask);
                    }
                    if (probeTask != null)
                    {
                        await probeTask;
                        if (cloudExistence.TryGetValue(slot.SlotNumber, out exists))
                            return exists;
                    }
                }

                return false;
            }

            // Resolve the filename pattern (handles {n} and {meta:key} placeholders)
            string fileName = NamePatternResolver.Resolve(saveSettings.saveFileName, slot);
            // Files are stored in slot subdirectories: {rootPath}/slot{slotNumber}/{fileName}.sav
            string slotFolder = Path.Combine(rootPath, $"slot{slot.SlotNumber}");
            string path = Path.Combine(slotFolder, $"{fileName}.sav");
            Logger.Log($"SaveFileExistsAsync: Checking local file: {path}", LogCategory.SaveSlotManager, LogLevel.Info);
            bool localExists = File.Exists(path);
            Logger.Log($"SaveFileExistsAsync: Local file exists={localExists}", LogCategory.SaveSlotManager, LogLevel.Info);
            if (localExists)
                return true;

            if (saveSettings.enableCloudSave)
            {
                // Check cached cloud existence first
                if (cloudExistence.TryGetValue(slot.SlotNumber, out bool exists))
                {
                    Logger.Log($"SaveSlotManager: Using cached cloud existence for slot {slot.SlotNumber}: {exists}", LogCategory.SaveSlotManager, LogLevel.Info);
                    return exists;
                }

                // No cache hit - start a probe and wait for it
                Logger.Log($"SaveSlotManager: Starting cloud probe for slot {slot.SlotNumber}", LogCategory.SaveSlotManager, LogLevel.Info);
                BeginCloudProbe(slot);
                Task probeTask;
                lock (cloudProbes)
                {
                    cloudProbes.TryGetValue(slot.SlotNumber, out probeTask);
                }
                if (probeTask != null)
                {
                    Logger.Log($"SaveSlotManager: Waiting for cloud probe to complete for slot {slot.SlotNumber}", LogCategory.SaveSlotManager, LogLevel.Info);
                    await probeTask;
                    if (cloudExistence.TryGetValue(slot.SlotNumber, out exists))
                    {
                        Logger.Log($"SaveSlotManager: Cloud probe completed for slot {slot.SlotNumber}: {exists}", LogCategory.SaveSlotManager, LogLevel.Info);
                        return exists;
                    }
                }
            }

            return false;
        }

    private void BeginCloudProbe(SaveSlot slot)
        {
            lock (cloudProbes)
            {
                if (cloudProbes.ContainsKey(slot.SlotNumber)) return;
                var probeTask = ProbeCloudAsync(slot);
                cloudProbes[slot.SlotNumber] = probeTask;
            }
        }

        private async Task ProbeCloudAsync(SaveSlot slot)
        {
            bool exists = false;
            try
            {
                var bytes = await saveSystem.LoadAsync(slot);
                exists = bytes != null && bytes.Length > 0;
                Logger.Log($"SaveSlotManager: cloud probe for slot {slot.SlotNumber}: exists={exists}, bytes={bytes?.Length ?? 0}", LogCategory.SaveSlotManager, LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"SaveSlotManager: cloud probe failed for slot {slot.SlotNumber}: {ex.Message}", LogCategory.SaveSlotManager, LogLevel.Warning);
            }
            finally
            {
                lock (cloudProbes) cloudProbes.Remove(slot.SlotNumber);
                lock (cloudExistence) cloudExistence[slot.SlotNumber] = exists;
            }
        }

        private Task<bool> SlotHasAccessibleDataAsync(SaveSlot slot, bool hasMetaData)
            => SaveFileExistsAsync(slot, hasMetaData);

        // Synchronous wrappers for legacy callers
        private bool SaveFileExists(SaveSlot slot, bool hasMetaData)
            => Task.Run(() => SaveFileExistsAsync(slot, hasMetaData)).GetAwaiter().GetResult();

        private bool SlotHasAccessibleData(SaveSlot slot, bool hasMetaData)
            => SaveFileExists(slot, hasMetaData);

        /// <summary>
        /// The list of managed save slots.
        /// </summary>
        public List<SaveSlot> Slots { get; } = new();

        public SaveSlotManager(ISaveSystem saveSystem,
                               SaveSettings settings,
                               ScreenshotManager screenshotManager,
                               string rootPath)
        {
            this.saveSystem       = saveSystem;
            this.saveSettings     = settings;
            this.screenshotManager = screenshotManager;
            this.rootPath         = rootPath;
        }

        /// <summary>
        /// Loads slot metadata from the underlying save system or creates
        /// default placeholders when none exist.
        /// </summary>
        public async Task InitializeAsync(int numberOfSlots, int startIndex = 1)
        {
            // Instance-level protection - prevent this specific manager from initializing multiple times
            if (isInitializing)
            {
                Logger.Log($"[SaveSlotManager] This instance already initializing, skipping duplicate call", LogCategory.SaveSlotManager, LogLevel.Warning);
                return;
            }
            
            isInitializing = true;
            
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
            Logger.Log($"[SaveSlotManager] WebGL: InitializeAsync called with {numberOfSlots} slots, startIndex={startIndex}. Current Slots.Count before clear: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif
            Logger.Log($"[SaveSlotManager] InitializeAsync: {numberOfSlots} slots, backend={saveSettings.backend}, keepLocalMirror={saveSettings.keepLocalMirror}", LogCategory.SaveSlotManager, LogLevel.Info);
            
            Slots.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
            Logger.Log($"[SaveSlotManager] WebGL: After Slots.Clear(), Slots.Count: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif
            for (int i = 0; i < numberOfSlots; i++)
            {
                int slotNumber = startIndex + i;
                SaveSlot loaded = null;

#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log($"[SaveSlotManager] WebGL: Processing slot {slotNumber} ({i+1}/{numberOfSlots})", LogCategory.SaveSlotManager, LogLevel.Info);
#endif

                try
                {
                    loaded = await saveSystem.LoadSlotMetadataAsync(slotNumber);
                    if (loaded != null)
                    {
                        Logger.Log($"[SaveSlotManager] Loaded slot {slotNumber}: Name='{loaded.SlotName}', LastSaved={loaded.LastSaved}, Screenshot='{loaded.ScreenshotFileName ?? "null"}'", LogCategory.SaveSlotManager, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log($"[SaveSlotManager] No metadata found for slot {slotNumber}", LogCategory.SaveSlotManager, LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to load slot metadata for {slotNumber}: {ex.Message}",
                               LogCategory.SaveSlotManager, LogLevel.Warning);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Logger.Log($"[SaveSlotManager] WebGL: Exception loading slot {slotNumber}: {ex.Message}\n{ex.StackTrace}", LogCategory.SaveSlotManager, LogLevel.Error);
#endif
                }

                // Without a local mirror and no cloud connection there's no
                // accessible data for this slot; drop any cached metadata so
                // it doesn't appear as a valid save.
                bool useLocalMirror = saveSettings.keepLocalMirror &&
                                      saveSettings.cloudCryptoMode != CloudCryptoMode.ServerSide;
                if (loaded != null &&
                    saveSettings.enableCloudSave &&
                    !useLocalMirror &&
                    !CloudSignedIn)
                {
                    Logger.Log($"[SaveSlotManager] WARNING: Dropping loaded metadata for slot {slotNumber} because CloudSignedIn={CloudSignedIn}, backend={saveSettings.backend}", LogCategory.SaveSlotManager, LogLevel.Warning);
                    loaded = null;
                }
                else if (loaded != null)
                {
                    Logger.Log($"[SaveSlotManager] Keeping loaded metadata for slot {slotNumber}: CloudSignedIn={CloudSignedIn}, backend={saveSettings.backend}", LogCategory.SaveSlotManager, LogLevel.Info);
                }

                var slot = loaded ?? new SaveSlot(slotNumber, $"Slot {slotNumber}",
                                                  DateTime.MinValue, string.Empty, string.Empty);
                if (loaded == null)
                {
                    Logger.Log($"[SaveSlotManager] Created empty slot {slotNumber}", LogCategory.SaveSlotManager, LogLevel.Info);
                }
                Slots.Add(slot);

#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log($"[SaveSlotManager] WebGL: Added slot {slotNumber} to Slots list. Current count: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif

                if (saveSettings.enableCloudSave && slot.LastSaved == DateTime.MinValue)
                    BeginCloudProbe(slot);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            Logger.Log($"[SaveSlotManager] WebGL: InitializeAsync completed. Final Slots.Count: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif
            }
            finally
            {
                isInitializing = false;
            }
        }

        public SaveSlot GetByNumber(int slotNumber)
            => Slots.Find(s => s.SlotNumber == slotNumber);

        public SaveSlot GetByName(string slotName)
            => Slots.Find(s => s.SlotName.Equals(slotName, StringComparison.OrdinalIgnoreCase));

        public List<SaveSlot> GetAll() => new List<SaveSlot>(Slots);

        public SaveSlot GetLatest()
        {
            if (Slots.Count == 0) return null;
            SaveSlot latest = Slots[0];
            foreach (var s in Slots)
            {
                if (s.LastSaved > latest.LastSaved)
                    latest = s;
            }
            return latest;
        }

    public async Task<bool> HasSaveAsync(bool hasMetaData = true)
        {
            Logger.Log($"SaveSlotManager: HasSaveAsync called with {Slots.Count} slots", LogCategory.SaveSlotManager, LogLevel.Info);
            foreach (var s in Slots)
            {
        bool exists = await SaveFileExistsAsync(s, hasMetaData);
                Logger.Log($"SaveSlotManager: Slot {s.SlotNumber} exists: {exists}", LogCategory.SaveSlotManager, LogLevel.Info);
                if (exists)
                    return true;
            }
            Logger.Log($"SaveSlotManager: HasSaveAsync returning false", LogCategory.SaveSlotManager, LogLevel.Info);
            return false;
        }

        public bool HasSave(bool hasMetaData = true)
            => Task.Run(() => HasSaveAsync(hasMetaData)).GetAwaiter().GetResult();

        public async Task<bool> HasSaveAtAsync(int slotNumber, bool hasMetaData = true)
        {
            var slot = GetByNumber(slotNumber);
            return slot != null && await SaveFileExistsAsync(slot, hasMetaData);
        }

        public bool HasSaveAt(int slotNumber, bool hasMetaData = true)
            => Task.Run(() => HasSaveAtAsync(slotNumber, hasMetaData)).GetAwaiter().GetResult();

    public async Task<bool> HasSaveAfterAsync(DateTime date, bool hasMetaData = true)
        {
            if (hasMetaData)
            {
                foreach (var s in Slots)
                {
            if (s.LastSaved > date && await SlotHasAccessibleDataAsync(s, true))
                        return true;
                }
                return false;
            }
            else
            {
                foreach (var s in Slots)
                {
            // Check both date and file existence when hasMetaData=false
            if (s.LastSaved > date && await SaveFileExistsAsync(s, false))
                        return true;
                }
                return false;
            }
        }

        public bool HasSaveAfter(DateTime date, bool hasMetaData = true)
            => Task.Run(() => HasSaveAfterAsync(date, hasMetaData)).GetAwaiter().GetResult();

        public async Task<bool> HasSaveInSceneAsync(string sceneName, bool hasMetaData = true)
        {
            Logger.Log($"HasSaveInSceneAsync: Checking for scene '{sceneName}', hasMetaData={hasMetaData}, Slots.Count={Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
            
            if (string.IsNullOrEmpty(sceneName))
            {
                Logger.Log("HasSaveInSceneAsync: sceneName is null or empty, returning false", LogCategory.SaveSlotManager, LogLevel.Warning);
                return false;
            }

            if (hasMetaData)
            {
                foreach (var s in Slots)
                {
                    Logger.Log($"HasSaveInSceneAsync: Checking slot {s.SlotNumber}: LastActiveScene='{s.LastActiveScene}', LastSaved={s.LastSaved}", LogCategory.SaveSlotManager, LogLevel.Info);
                    bool sceneMatches = string.Equals(s.LastActiveScene, sceneName, StringComparison.OrdinalIgnoreCase);
                    bool hasData = await SlotHasAccessibleDataAsync(s, true);
                    Logger.Log($"HasSaveInSceneAsync: Slot {s.SlotNumber} - sceneMatches={sceneMatches}, hasData={hasData}", LogCategory.SaveSlotManager, LogLevel.Info);
                    
                    if (sceneMatches && hasData)
                    {
                        Logger.Log($"HasSaveInSceneAsync: Found match in slot {s.SlotNumber}, returning true", LogCategory.SaveSlotManager, LogLevel.Info);
                        return true;
                    }
                }
                Logger.Log("HasSaveInSceneAsync: No matching slots found, returning false", LogCategory.SaveSlotManager, LogLevel.Info);
                return false;
            }
            else
            {
                // When hasMetaData=false: check scene name matches AND save file exists
                foreach (var s in Slots)
                {
                    Logger.Log($"HasSaveInSceneAsync (no metadata): Checking slot {s.SlotNumber}: LastActiveScene='{s.LastActiveScene}'", LogCategory.SaveSlotManager, LogLevel.Info);
                    bool sceneMatches = string.Equals(s.LastActiveScene, sceneName, StringComparison.OrdinalIgnoreCase);
                    bool fileExists = await SaveFileExistsAsync(s, false);
                    Logger.Log($"HasSaveInSceneAsync (no metadata): Slot {s.SlotNumber} - sceneMatches={sceneMatches}, fileExists={fileExists}", LogCategory.SaveSlotManager, LogLevel.Info);
                    
                    if (sceneMatches && fileExists)
                    {
                        Logger.Log($"HasSaveInSceneAsync (no metadata): Found match in slot {s.SlotNumber}, returning true", LogCategory.SaveSlotManager, LogLevel.Info);
                        return true;
                    }
                }
                Logger.Log("HasSaveInSceneAsync (no metadata): No matching slots found, returning false", LogCategory.SaveSlotManager, LogLevel.Info);
                return false;
            }
        }

        public bool HasSaveInScene(string sceneName, bool hasMetaData = true)
            => Task.Run(() => HasSaveInSceneAsync(sceneName, hasMetaData)).GetAwaiter().GetResult();

        public bool Rename(int slotNumber, string newName)
        {
            if (string.IsNullOrEmpty(newName))
                return false;

            var slot = GetByNumber(slotNumber);
            if (slot == null)
                return false;

            slot.SlotName = newName;
            return true;
        }

        /// <summary>
        /// Clear cloud existence cache for a slot to force re-probing.
        /// Call this after save operations to ensure fresh checks.
        /// </summary>
        public void InvalidateCloudCache(int slotNumber)
        {
            lock (cloudExistence) cloudExistence.Remove(slotNumber);
        }

        public async Task<bool> DeleteAsync(int slotNumber)
        {
            var slot = GetByNumber(slotNumber);
            if (slot == null)
                return false;

            if (saveSettings.enableCloudSave)
                await saveSystem.DeleteAsync(slot);
            else
                saveSystem.Delete(slot);

            if (saveSettings.enableScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
                screenshotManager.DeleteScreenshot(slot.ScreenshotFileName);

            // Clear cloud existence cache for this slot
            lock (cloudExistence) cloudExistence.Remove(slotNumber);

            // Reset basic slot information after deletion
            slot.SlotName = $"Slot {slotNumber}";
            slot.LastSaved = DateTime.MinValue;
            slot.ScreenshotFileName = string.Empty;
            slot.LastActiveScene = string.Empty;

            // Ensure the slot list reflects the deletion immediately.
            try
            {
                if (SaveManager.Instance != null)
                {
                    // Ensure refresh occurs on main thread
                    await SaveManager.Instance.ForceRefreshSlotsAsync();
                }
                else
                {
                    // No manager available; still notify listeners of local change if possible
                    SaveManager.Instance?.NotifySaveSlotsUpdated();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[SaveSlotManager] Post-delete refresh failed: {ex.Message}", LogCategory.SaveSlotManager, LogLevel.Warning);
            }

            return true;
        }
    }
}
#endif
