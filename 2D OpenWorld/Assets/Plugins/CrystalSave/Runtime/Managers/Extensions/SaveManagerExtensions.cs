#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.SceneManagement;
using System.IO;

namespace Arawn.CrystalSave.Runtime
{
/// <summary>
/// Contains extension methods for the SaveManager class.
/// </summary>
public static class SaveManagerExtensions
{
    /// <summary>
    /// Resets all save slots to their default state.
    /// </summary>
    /// <param name="manager">The instance of SaveManager.</param>
    public static void ResetAllSaveSlots(this SaveManager manager)
    {
        if (manager == null)
        {
            Logger.Log("SaveManager instance is null.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        foreach (var slot in manager.GetSaveSlots())
        {
            manager.Delete(slot.SlotNumber);
            manager.RenameSlot(slot.SlotNumber, $"Slot {slot.SlotNumber}");
        }

        Logger.Log("All save slots have been reset.", LogCategory.SaveManagerExtensions, LogLevel.Info);
    }

    /// <summary>
    /// Retrieves all active save slots.
    /// </summary>
    /// <param name="manager">The instance of SaveManager.</param>
    /// <returns>A list of active SaveSlot objects.</returns>
    public static List<SaveSlot> GetActiveSaveSlots(this SaveManager manager)
    {
        if (manager == null)
        {
            Logger.Log("SaveManager instance is null.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return new List<SaveSlot>();
        }

        // Assuming that a slot is active if it has been saved at least once
        List<SaveSlot> activeSlots = new List<SaveSlot>();
        foreach (var slot in manager.GetSaveSlots())
        {
            if (slot.LastSaved > DateTime.MinValue)
            {
                activeSlots.Add(slot);
            }
        }

        return activeSlots;
    }

    /// <summary>
    /// Saves the current game state to all available save slots.
    /// </summary>
    /// <param name="manager">The instance of SaveManager.</param>
    public static void SaveToAllSlots(this SaveManager manager)
    {
        if (manager == null)
        {
            Logger.Log("SaveManager instance is null.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        foreach (var slot in manager.GetSaveSlots())
        {
            manager.Save(slot.SlotNumber);
        }

        Logger.Log("Game state saved to all slots.", LogCategory.SaveManagerExtensions, LogLevel.Info);
    }

    /// <summary>
    /// Loads the most recently saved slot.
    /// </summary>
    /// <param name="manager">The instance of SaveManager.</param>
    /// <param name="restoreLastActiveScene">Whether to restore the scene that was active when the save was created.</param>
    /// <param name="loadAsync">Whether to load the scene asynchronously.</param>
    /// <param name="allowSceneActivation">Whether to allow scene activation after async loading.</param>
    public static void LoadMostRecentSlot(this SaveManager manager, bool restoreLastActiveScene = true, bool loadAsync = false, bool allowSceneActivation = true)
    {
        if (manager == null)
        {
            Logger.Log("SaveManager instance is null.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        var activeSlots = manager.GetActiveSaveSlots();
        if (activeSlots.Count == 0)
        {
            Logger.Log("No active save slots found.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        // Find the slot with the latest LastSaved timestamp
        SaveSlot mostRecentSlot = null;
        DateTime latestSave = DateTime.MinValue;

        foreach (var slot in activeSlots)
        {
            if (slot.LastSaved > latestSave)
            {
                latestSave = slot.LastSaved;
                mostRecentSlot = slot;
            }
        }

        if (mostRecentSlot != null)
        {
            manager.Load(mostRecentSlot.SlotNumber, restoreLastActiveScene: restoreLastActiveScene, loadAsync: loadAsync, allowSceneActivation: allowSceneActivation);
            Logger.Log($"Loaded the most recent save slot: {mostRecentSlot.SlotNumber}", LogCategory.SaveManagerExtensions, LogLevel.Info);
        }
        else
        {
            Logger.Log("No save slots to load.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
        }
    }

    /// <summary>
    /// Restores the given GameObjects from the most‐recent save slot.
    /// If fireEventForEach==false, only the first restored object will fire the event.
    /// </summary>
    public static async Task RestoreSingleGameObjectsFromMostRecentSlotAsync(this SaveManager manager,
                                                                             IReadOnlyList<GameObject> targets,
                                                                             bool fireEventForEach = true)
    {
        if (manager == null)
        {
            Logger.Log("SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        // 1) find latest slot
        var slots = manager.GetActiveSaveSlots();
        if (slots.Count == 0)
        {
            Logger.Log("No active save slots to restore from.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        int slotNumber = slots.OrderByDescending(s => s.LastSaved).First().SlotNumber;

        // 2) load SaveData from cloud/local
        SaveData data = null;
        try
        {
            data = await manager.LoadSaveDataForSlotAsync(slotNumber);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load slot {slotNumber}: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        if (data == null)
        {
            Logger.Log($"No SaveData found in slot {slotNumber}.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        // 3) restore each GameObject
        bool first = true;
        foreach (var go in targets)
        {
            // pass suppressEvent = true on all but the first if fireEventForEach == false
            bool suppress = !fireEventForEach && !first;
            manager.RestoreSingleGameObject(go, data, suppress);
            first = false;
        }
    }

    /// <summary>
    /// Restores the GameObjects identified by these UniqueIDs from the most‐recent save slot.
    /// If fireEventForEach==false, only the first restored object will fire the event.
    /// </summary>
    public static async Task RestoreSingleGameObjectsFromMostRecentSlotAsync(this SaveManager manager,
                                                                             IReadOnlyList<string> uniqueIDs,
                                                                             bool fireEventForEach = true)
    {
        if (manager == null)
        {
            Logger.Log("SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        // 1) find latest slot
        var slots = manager.GetActiveSaveSlots();
        if (slots.Count == 0)
        {
            Logger.Log("No active save slots to restore from.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        int slotNumber = slots.OrderByDescending(s => s.LastSaved).First().SlotNumber;

        // 2) load the SaveData
        SaveData data = null;
        try
        {
            data = await manager.LoadSaveDataForSlotAsync(slotNumber);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load slot {slotNumber}: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        if (data == null)
        {
            Logger.Log($"No SaveData found in slot {slotNumber}.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        // 3) restore by looking up each GameObject
        bool first = true;
        foreach (var uid in uniqueIDs)
        {
            var go = manager.FindGameObjectByUniqueID(uid, SaveManager.IdentifierType.UniqueID);
            if (go == null)
            {
                Logger.Log($"Could not find GameObject for UniqueID '{uid}'. Skipping.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
                continue;
            }

            bool suppress = !fireEventForEach && !first;
            manager.RestoreSingleGameObject(go, data, suppress);
            first = false;
        }
    }

    /// <summary>
    /// Restores destroyed prefab instances identified by these UniqueIDs
    /// from the most‑recent save slot.
    /// </summary>
    public static async Task RestoreSinglePrefabsFromMostRecentSlotAsync(this SaveManager manager,
                                                                         IReadOnlyList<string> uniqueIDs)
    {
        if (manager == null)
        {
            Logger.Log("SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        // 1) find latest slot
        var slots = manager.GetActiveSaveSlots();
        if (slots.Count == 0)
        {
            Logger.Log("No active save slots to restore from.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        int slotNumber = slots.OrderByDescending(s => s.LastSaved).First().SlotNumber;

        // 2) load the SaveData
        SaveData data = null;
        try
        {
            data = await manager.LoadSaveDataForSlotAsync(slotNumber);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load slot {slotNumber}: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        if (data == null)
        {
            Logger.Log($"No SaveData found in slot {slotNumber}.", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        // 3) restore each prefab
        foreach (var uid in uniqueIDs)
        {
            manager.RestoreDestroyedPrefab(uid, data);
        }
    }

    /// <summary>
    /// Restore one GameObject from the most‐recent slot (fires event once by default).
    /// </summary>
    public static Task RestoreSingleGameObjectsFromMostRecentSlotAsync(this SaveManager manager, GameObject target,
                                                                       bool fireEventForEach = true)
    {
        // wrap into a one‐element list
        return manager.RestoreSingleGameObjectsFromMostRecentSlotAsync(new[] { target }, fireEventForEach);
    }

    /// <summary>
    /// Restore one UniqueID from the most‐recent slot (fires event once by default).
    /// </summary>
    public static Task RestoreSingleGameObjectsFromMostRecentSlotAsync(this SaveManager manager, string uniqueID,
                                                                       bool fireEventForEach = true)
    {
        return manager.RestoreSingleGameObjectsFromMostRecentSlotAsync(new[] { uniqueID }, fireEventForEach);
    }

    /// <summary>
    /// Restore one destroyed prefab by UniqueID from the most‑recent slot.
    /// </summary>
    public static Task RestoreSinglePrefabsFromMostRecentSlotAsync(this SaveManager manager, string uniqueID)
    {
        return manager.RestoreSinglePrefabsFromMostRecentSlotAsync(new[] { uniqueID });
    }

    /// <summary>
    /// Loads a new scene after first saving the current game state
    /// to <paramref name="slotNumber"/>.
    /// </summary>
    [Obsolete("Use LoadSceneAfterSaveAndPopulatePendingPrefabsAsync instead for faster scene loading.")]
    public static async Task LoadSceneAfterSaveAsync(this SaveManager manager, int slotNumber, string sceneName)
    {
        await LoadSceneAfterSaveAsync(manager, slotNumber, sceneName, false, false);
    }

    /// <summary>
    /// Loads a new scene after first saving the current game state
    /// to <paramref name="slotNumber"/>.
    /// Allows configuring additive and asynchronous loading modes.
    /// </summary>
    [Obsolete("Use LoadSceneAfterSaveAndPopulatePendingPrefabsAsync instead for faster scene loading.")]
    public static async Task LoadSceneAfterSaveAsync(this SaveManager manager, int slotNumber, string sceneName,
                                                     bool loadAdditive, bool loadAsync) =>
        await LoadSceneAfterSaveAsync(manager, slotNumber, sceneName, loadAdditive, loadAsync, false);

    [Obsolete("Use LoadSceneAfterSaveAndPopulatePendingPrefabsAsync instead for faster scene loading.")]
    public static async Task LoadSceneAfterSaveAsync(this SaveManager manager, int slotNumber, string sceneName,
                                                     bool loadAdditive, bool loadAsync, bool allowDuplicateLoad = false)
    {
        if (manager == null)
        {
            Logger.Log("LoadSceneAfterSaveAsync: SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Logger.Log("LoadSceneAfterSaveAsync: sceneName is null or empty.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        if (loadAdditive && !allowDuplicateLoad &&
            Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(s => s.name == sceneName && s.isLoaded))
        {
            Logger.Log(
                $"LoadSceneAfterSaveAsync: '{sceneName}' already loaded additively; skipping duplicate load.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        using var cts = new CancellationTokenSource();
        Logger.Log("LoadSceneAfterSave: starting save", LogCategory.SaveManagerExtensions, LogLevel.Info);
        await manager.SaveAsync(slotNumber, ct: cts.Token);

        await Task.Yield();
        manager.Load(slotNumber);
        await Task.Yield();

        Logger.Log($"LoadSceneAfterSave: loading scene {sceneName}", LogCategory.SaveManagerExtensions, LogLevel.Info);
        var mode = loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        if (loadAsync)
            await SceneManager.LoadSceneAsync(sceneName, mode);
        else
            SceneManager.LoadScene(sceneName, mode);
    }

    /// <summary>
    /// Overload of <see cref="LoadSceneAfterSaveAsync(Arawn.CrystalSave.Runtime.SaveManager,int,string)"/>
    /// that accepts a build index for the target scene.
    /// </summary>
    [Obsolete("Use LoadSceneAfterSaveAndPopulatePendingPrefabsAsync instead for faster scene loading.")]
    public static async Task LoadSceneAfterSaveAsync(this SaveManager manager, int slotNumber, int sceneBuildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
        if (string.IsNullOrEmpty(path))
        {
            Logger.Log($"LoadSceneAfterSaveAsync: invalid scene index {sceneBuildIndex}.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        await LoadSceneAfterSaveAsync(manager, slotNumber, name);
    }

    /// <summary>
    /// Overload of <see cref="LoadSceneAfterSaveAsync(Arawn.CrystalSave.Runtime.SaveManager,int,string,bool,bool)"/>
    /// that accepts a build index for the target scene and allows specifying loading modes.
    /// </summary>
    [Obsolete("Use LoadSceneAfterSaveAndPopulatePendingPrefabsAsync instead for faster scene loading.")]
    public static async Task LoadSceneAfterSaveAsync(this SaveManager manager, int slotNumber, int sceneBuildIndex,
                                                     bool loadAdditive, bool loadAsync)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
        if (string.IsNullOrEmpty(path))
        {
            Logger.Log($"LoadSceneAfterSaveAsync: invalid scene index {sceneBuildIndex}.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        await LoadSceneAfterSaveAsync(manager, slotNumber, name, loadAdditive, loadAsync);
    }

    /// <summary>
    /// Saves the game to <paramref name="slotNumber"/> then populates pending prefabs
    /// from that slot before loading <paramref name="sceneName"/>.
    /// Allows configuring additive and asynchronous loading modes.
    /// </summary>
    public static async Task LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(this SaveManager manager,
                                                                              int slotNumber, string sceneName,
                                                                              bool loadAdditive, bool loadAsync,
                                                                              bool allowDuplicateLoad = false)
    {
        if (manager == null)
        {
            Logger.Log("LoadSceneAfterSaveAndPopulatePendingPrefabsAsync: SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Logger.Log("LoadSceneAfterSaveAndPopulatePendingPrefabsAsync: sceneName is null or empty.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        if (loadAdditive && !allowDuplicateLoad &&
            Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(s => s.name == sceneName && s.isLoaded))
        {
            Logger.Log(
                $"LoadSceneAfterSaveAndPopulatePendingPrefabsAsync: '{sceneName}' already loaded additively; skipping duplicate load.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        manager.IsInSceneTransitionInternal = true;

        try
        {
            using var cts = new CancellationTokenSource();
            Logger.Log("LoadSceneAfterSaveAndPopulatePendingPrefabs: starting save", LogCategory.SaveManagerExtensions, LogLevel.Info);
            await manager.SaveAsync(slotNumber, ct: cts.Token);

            await Task.Yield();
            await manager.PopulatePendingPrefabsFromSlotAsync(slotNumber);
            await Task.Yield();

            Logger.Log($"LoadSceneAfterSaveAndPopulatePendingPrefabs: loading scene {sceneName}", LogCategory.SaveManagerExtensions, LogLevel.Info);
            var mode = loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            if (loadAsync)
                await SceneManager.LoadSceneAsync(sceneName, mode);
            else
                SceneManager.LoadScene(sceneName, mode);

            await Task.Yield();
            await Task.Yield();

            ReapplyTrackedGameObjectActiveStates(manager,
                "LoadSceneAfterSaveAndPopulatePendingPrefabsAsync");

            ReapplyPersistentVisibility(manager,
                "LoadSceneAfterSaveAndPopulatePendingPrefabsAsync");
        }
        finally
        {
            manager.IsInSceneTransitionInternal = false;
        }
    }

    /// <summary>
    /// Overload of <see cref="LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(Arawn.CrystalSave.Runtime.SaveManager,int,string,bool,bool,bool)"/>
    /// that uses default loading modes.
    /// </summary>
    public static async Task LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(this SaveManager manager,
                                                                              int slotNumber, string sceneName)
    {
        await LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(manager, slotNumber, sceneName, false, false);
    }

    /// <summary>
    /// Overload of <see cref="LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(Arawn.CrystalSave.Runtime.SaveManager,int,string,bool,bool,bool)"/>
    /// allowing specification of loading modes without duplicate load checks.
    /// </summary>
    public static async Task LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(this SaveManager manager,
                                                                              int slotNumber, string sceneName,
                                                                              bool loadAdditive, bool loadAsync) =>
        await LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(manager, slotNumber, sceneName, loadAdditive, loadAsync, false);

    /// <summary>
    /// Overload of <see cref="LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(Arawn.CrystalSave.Runtime.SaveManager,int,string,bool,bool,bool)"/>
    /// that accepts a build index for the target scene.
    /// </summary>
    public static async Task LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(this SaveManager manager,
                                                                              int slotNumber, int sceneBuildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
        if (string.IsNullOrEmpty(path))
        {
            Logger.Log($"LoadSceneAfterSaveAndPopulatePendingPrefabsAsync: invalid scene index {sceneBuildIndex}.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        await LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(manager, slotNumber, name);
    }

    /// <summary>
    /// Overload of <see cref="LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(Arawn.CrystalSave.Runtime.SaveManager,int,string,bool,bool,bool)"/>
    /// that accepts a build index for the target scene and allows specifying loading modes.
    /// </summary>
    public static async Task LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(this SaveManager manager,
                                                                              int slotNumber, int sceneBuildIndex,
                                                                              bool loadAdditive, bool loadAsync)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
        if (string.IsNullOrEmpty(path))
        {
            Logger.Log($"LoadSceneAfterSaveAndPopulatePendingPrefabsAsync: invalid scene index {sceneBuildIndex}.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        await LoadSceneAfterSaveAndPopulatePendingPrefabsAsync(manager, slotNumber, name, loadAdditive, loadAsync);
    }

    /// <summary>
    /// Reads <paramref name="slotNumber"/> and feeds its prefab records into the
    /// <see cref="PrefabManager"/> without performing a full <see cref="SaveManager.Load(int)"/>.
    /// Use this when you want to restore prefabs that remember their home scene
    /// but do not wish to load the entire save.
    /// <para>
    /// Typical usage:
    /// <code><![CDATA[
    /// await saveManager.PopulatePendingPrefabsFromSlotAsync(slot);
    /// // Now load any scenes you need. Prefabs will spawn when their home scene loads.
    /// ]]></code>
    /// </para>
    /// Prefabs whose home scene is not currently loaded are placed into the
    /// pending list and will automatically instantiate once that scene loads.
    /// </summary>
    public static async Task PopulatePendingPrefabsFromSlotAsync(this SaveManager manager, int slotNumber)
    {
        if (manager == null)
        {
            Logger.Log("PopulatePendingPrefabsFromSlotAsync: SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        var prefabManager = manager.GetPrefabManager;
        if (prefabManager == null)
        {
            Logger.Log("PopulatePendingPrefabsFromSlotAsync: PrefabManager not found.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        // GUARD RAIL: Validate timing to catch common mistakes
        ValidatePrefabPopulateTiming(manager, "PopulatePendingPrefabsFromSlotAsync");

        SaveData data = null;
        try
        {
            data = await manager.LoadSaveDataForSlotAsync(slotNumber);
        }
        catch (Exception ex)
        {
            Logger.Log($"PopulatePendingPrefabsFromSlotAsync: failed to load slot {slotNumber}: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        if (data == null)
        {
            Logger.Log($"PopulatePendingPrefabsFromSlotAsync: no SaveData in slot {slotNumber}.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        // Update manager state so pending prefabs are properly tracked when scenes load
        manager.CurrentSaveData = data;
    // Import persisted RememberHome snapshots so components can restore when their home scenes load later
    try { manager.ComponentManager?.ImportHomeSceneSnapshots(data.HomeSceneComponentSnapshots, data.HomeScenePrefabAssetIDs); }
    catch (Exception ex) { Logger.Log($"PopulatePendingPrefabsFromSlotAsync: import snapshots failed: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Warning); }
        manager.GameObjectTracker.DestroyedIDs.Clear();
        if (data.DestroyedGameObjects != null)
        {
            foreach (var id in data.DestroyedGameObjects)
                manager.GameObjectTracker.DestroyedIDs.Add(id);
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.StartCoroutine(InstantiatePrefabsAndApplyCoroutine(manager,
            prefabManager, data, tcs));
        await tcs.Task;
    }

    static IEnumerator InstantiatePrefabsAndApplyCoroutine(
        SaveManager manager,
        PrefabManager prefabManager,
        SaveData data,
        TaskCompletionSource<bool> tcs)
    {
        yield return prefabManager.InstantiatePrefabsCoroutine(
            data.Prefabs,
            manager.GameObjectTracker.GetDestroyedGameObjectIDs(),
            clearExistingPrefabs: false);

        if (manager.ComponentManager != null)
        {
            // Mirror the full load pipeline so components can safely deserialize again
            // when we later revisit their home scenes. Without resetting the deserialized
            // bookkeeping the snapshot-based scene hop thinks the data was already
            // applied and skips reloading, which breaks Remember Home Scene for
            // SaveableComponents.
            SaveableComponent.ResetLoadCallCounts();
            manager.ComponentManager.ResetDeserializedComponents();
            manager.ComponentManager.InstantiateMissingHomeSceneObjectsForLoadedScenes();
            int batchSize = manager.SaveSettings?.componentApplyBatchSize ?? 0;
            if (batchSize > 0)
            {
                var enumerator = manager.ComponentManager.ApplyComponentDataCoroutine(data, batchSize);
                while (true)
                {
                    bool moveNext;
                    try
                    {
                        moveNext = enumerator.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"PopulatePendingPrefabsFromSlotAsync: error applying component data: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
                        break;
                    }
                    if (!moveNext)
                        break;
                    yield return enumerator.Current;
                }
                if (enumerator is System.IDisposable disposable)
                    disposable.Dispose();
            }
            else
            {
                try
                {
                    manager.ComponentManager.ApplyComponentData(data);
                }
                catch (Exception ex)
                {
                    Logger.Log($"PopulatePendingPrefabsFromSlotAsync: error applying component data: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
                }
            }
        }

        if (data.GameObjectStates != null)
        {
            try
            {
                manager.ApplyGameObjectActiveStates(data.GameObjectStates);
            }
            catch (Exception ex)
            {
                Logger.Log($"PopulatePendingPrefabsFromSlotAsync: error applying GameObject active states: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
            }
        }

        tcs.TrySetResult(true);
    }

    /// <summary>
    /// Collects SaveData in-memory (no disk write) and feeds its prefab/component/gameobject
    /// state into the managers so pending prefabs will spawn when their home scenes load.
    /// Use this when you want the "save + populate" behavior without specifying a slot.
    /// Prefer calling <see cref="SaveManager.SnapshotAndPopulateAsync"/> for a one-step
    /// snapshot-and-populate helper.
    /// </summary>
    public static async Task PopulatePendingPrefabsFromSnapshotAsync(this SaveManager manager)
    {
        if (manager == null)
        {
            Logger.Log("PopulatePendingPrefabsFromSnapshotAsync: SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        var prefabManager = manager.GetPrefabManager;
        if (prefabManager == null)
        {
            Logger.Log("PopulatePendingPrefabsFromSnapshotAsync: PrefabManager not found.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        // GUARD RAIL: Validate timing to catch common mistakes
        ValidatePrefabPopulateTiming(manager, "PopulatePendingPrefabsFromSnapshotAsync");

    // Build an in-memory snapshot equivalent to a save operation
        SaveData data = null;
        try
        {
            data = manager.CollectSaveData();
        }
        catch (Exception ex)
        {
            Logger.Log($"PopulatePendingPrefabsFromSnapshotAsync: failed to collect save data: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Error);
            return;
        }

        if (data == null)
        {
            Logger.Log("PopulatePendingPrefabsFromSnapshotAsync: CollectSaveData returned null.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

    // Capture Home Scene snapshot AFTER collecting save data so SaveableComponents capture the correct current state
    // mirroring LoadSceneWithSnapshotAsync behavior.
    try { manager.ComponentManager?.SnapshotCurrentSceneAll(); }
    catch (Exception ex) { Logger.Log($"PopulatePendingPrefabsFromSnapshotAsync: snapshot failed: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Warning); }

        // Update manager state so pending prefabs are properly tracked when scenes load
        manager.CurrentSaveData = data;
        manager.GameObjectTracker.DestroyedIDs.Clear();
        if (data.DestroyedGameObjects != null)
        {
            foreach (var id in data.DestroyedGameObjects)
                manager.GameObjectTracker.DestroyedIDs.Add(id);
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.StartCoroutine(InstantiatePrefabsAndApplyCoroutine(manager,
            prefabManager, data, tcs));
        await tcs.Task;
    }

    /// <summary>
    /// Invoke <paramref name="action"/> if <paramref name="self"/> is non-null.
    /// </summary>
    public static void Let<T>(this T self, Action<T> action)
        where T : class
    {
        if (self != null)
            action(self);
    }

    /// <summary>
    /// Snapshot the active scene for Home Scene memory, then perform a direct scene load.
    /// Use this when you are not using the Save+Populate helper but still want in-memory
    /// Home Scene snapshots to be captured before the switch. This does not save to disk.
    /// </summary>
    public static async Task LoadSceneWithSnapshotAsync(this SaveManager manager,
                                                        string sceneName,
                                                        bool loadAdditive = false,
                                                        bool loadAsync = false)
    {
        if (manager == null)
        {
            Logger.Log("LoadSceneWithSnapshotAsync: SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Logger.Log("LoadSceneWithSnapshotAsync: sceneName is null or empty.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        // Proactively snapshot current active scene so [RememberHome] can restore when we return later
        try { manager.ComponentManager?.SnapshotCurrentSceneAll(); }
        catch (Exception ex) { Logger.Log($"LoadSceneWithSnapshotAsync: snapshot failed: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Warning); }

        var mode = loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        if (loadAsync)
            await SceneManager.LoadSceneAsync(sceneName, mode);
        else
            SceneManager.LoadScene(sceneName, mode);
    }

    /// <summary>
    /// Overload of <see cref="LoadSceneWithSnapshotAsync(Arawn.CrystalSave.Runtime.SaveManager,string,bool,bool)"/>
    /// that accepts a build index for the target scene.
    /// </summary>
    public static async Task LoadSceneWithSnapshotAsync(this SaveManager manager,
                                                        int sceneBuildIndex,
                                                        bool loadAdditive = false,
                                                        bool loadAsync = false)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
        if (string.IsNullOrEmpty(path))
        {
            Logger.Log($"LoadSceneWithSnapshotAsync: invalid scene index {sceneBuildIndex}.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        await LoadSceneWithSnapshotAsync(manager, name, loadAdditive, loadAsync);
    }

    /// <summary>
    /// Helper method to despawn pooled prefabs that are leaving their home scene during snapshot-based transitions.
    /// This ensures pooling works correctly with snapshot-based scene transitions.
    /// </summary>
    public static async Task DespawnPooledPrefabsLeavingHomeScene(SaveManager manager, string targetSceneName)
    {
        if (manager == null || !manager.SaveSettings.usePrefabPooling)
        {
            return;
        }

        var currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid() || currentScene.name == targetSceneName)
        {
            return;
        }

        PrefabRegistry prefabRegistry = null;
        try
        {
            prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
        }
        catch (System.Exception ex)
        {
            Logger.Log($"Failed to load PrefabRegistry: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Warning);
        }


    // Find all active SaveablePrefabs with RememberHomeScene that should be despawned
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
        var saveablePrefabs = UnityEngine.Object.FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#pragma warning restore CS0618
        
        
        
        foreach (var prefab in saveablePrefabs)
        {
            // Only despawn pooled prefabs that have RememberHomeScene and are leaving their home scene.
            // IMPORTANT: Skip scene-backed (design-time) prefabs entirely; only runtime-added prefabs
            // should be returned to the pool on scene transitions. Scene instances must remain in-scene
            // to preserve identity and avoid remapping on return.
            if (!prefab.IsAddedAtRuntime)
                continue; // never despawn scene-backed prefabs

            if (prefab.RememberHomeScene &&
                !string.IsNullOrEmpty(prefab.HomeScene) &&
                prefab.HomeScene == currentScene.name &&
                prefab.HomeScene != targetSceneName &&
                prefab.gameObject.activeInHierarchy)
            {
                if (prefabRegistry != null && prefabRegistry.IsPoolingDisabled(prefab.PrefabAssetID))
                {
                    continue;
                }

                // Get the correct pool size for this specific prefab
                int poolSize = GetPoolSizeForPrefab(manager, prefab, prefabRegistry);

                Logger.Log($"[CrystalSave][HomeScene] Despawn runtime prefab '{prefab.name}' uid='{prefab.UniqueID}' leaving '{currentScene.name}' -> '{targetSceneName}'", LogCategory.SaveManagerExtensions, LogLevel.Info);
                SaveablePrefabPoolCache.TryDespawn(prefab, poolSize, false);
            }
        }
        
        await Task.Yield();
    }

    /// <summary>
    /// Helper method to get the correct pool size for a specific prefab.
    /// Replicates the logic from PrefabManager.GetPoolSizeForPrefab().
    /// </summary>
    private static int GetPoolSizeForPrefab(SaveManager manager, SaveablePrefab prefab, PrefabRegistry prefabRegistry)
    {
        if (manager?.SaveSettings == null || prefab == null)
            return 1; // fallback default

        // Get default pool size from SaveSettings
        int defaultPoolSize = manager.SaveSettings.defaultPrefabPoolSize;

        // Try to load PrefabRegistry to check for individual prefab pool sizes
        if (prefabRegistry != null)
        {
            return prefabRegistry.ResolvePoolSize(prefab.PrefabAssetID, defaultPoolSize);
        }

        return defaultPoolSize;
    }

    /// <summary>
    /// Creates an in-memory snapshot and populates pending prefabs (no slot/disk write),
    /// then loads the specified scene. Mirrors the slot-based helper but works entirely
    /// with transient data.
    /// </summary>
    public static async Task LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(this SaveManager manager,
                                                                                   string sceneName,
                                                                                   bool loadAdditive = false,
                                                                                   bool loadAsync = false,
                                                                                   bool allowDuplicateLoad = false)
    {
        if (manager == null)
        {
            Logger.Log("LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync: SaveManager is null.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Logger.Log("LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync: sceneName is null or empty.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        if (loadAdditive && !allowDuplicateLoad &&
            Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(s => s.name == sceneName && s.isLoaded))
        {
            Logger.Log(
                $"LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync: '{sceneName}' already loaded additively; skipping duplicate load.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

    Logger.Log("LoadSceneAfterSnapshotAndPopulatePendingPrefabs: collecting in-memory snapshot", LogCategory.SaveManagerExtensions, LogLevel.Info);
        
        // Set scene transition flag for the full duration of the snapshot + load workflow
        manager.IsInSceneTransitionInternal = true;

        try
        {
            // First, capture the snapshot while all prefabs are still active
            // await manager.SnapshotAndPopulateAsync();
            // Calling SnapshotAndPopulateAsync makes probably from a User standpoint more sense
            // but in terms of raw performance I believe simply calling PopulatePendingPrefabsFromSnapshotAsync it the better choice.
            await manager.PopulatePendingPrefabsFromSnapshotAsync();
            await Task.Yield();

            // After capturing snapshot, despawn pooled prefabs that are leaving their home scene
            if (manager.SaveSettings?.usePrefabPooling == true)
            {
                await DespawnPooledPrefabsLeavingHomeScene(manager, sceneName);
            }

            Logger.Log($"LoadSceneAfterSnapshotAndPopulatePendingPrefabs: loading scene {sceneName}", LogCategory.SaveManagerExtensions, LogLevel.Info);

            // Suppress pool disposal during snapshot-based scene transition
            SaveablePrefabPoolCache.SuppressPoolDisposal = true;

            try
            {
                var mode = loadAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
                if (loadAsync)
                    await SceneManager.LoadSceneAsync(sceneName, mode);
                else
                    SceneManager.LoadScene(sceneName, mode);

                // Wait for next frame to ensure activeSceneChanged event has been processed
                await Task.Yield();
                await Task.Yield(); // Extra yield to be safe

                ReapplyTrackedGameObjectActiveStates(manager,
                    "LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync");

                // Final PVC visibility sweep — ensures DontDestroyOnLoad prefabs
                // with PersistentVisibilityController have their renderers/colliders
                // toggled for the newly-active scene.  The SceneLoadManager performs
                // an equivalent sweep in HandleDestroyedObjects + FinalizeLoad, but
                // the snapshot-based scene-switch path bypasses that state machine,
                // so we must do it here.
                ReapplyPersistentVisibility(manager,
                    "LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync");
            }
            finally
            {
                // Always clear the flag after scene transition is complete
                SaveablePrefabPoolCache.SuppressPoolDisposal = false;
            }
        }
        finally
        {
            // Clear scene transition flag once the transition has fully completed
            manager.IsInSceneTransitionInternal = false;
        }
    }

    /// <summary>
    /// Overload that accepts a build index for the target scene.
    /// </summary>
    public static async Task LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(this SaveManager manager,
                                                                                   int sceneBuildIndex,
                                                                                   bool loadAdditive = false,
                                                                                   bool loadAsync = false)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
        if (string.IsNullOrEmpty(path))
        {
            Logger.Log($"LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync: invalid scene index {sceneBuildIndex}.", LogCategory.SaveManagerExtensions, LogLevel.Warning);
            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        await LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(manager, name, loadAdditive, loadAsync);
    }

    static void ReapplyTrackedGameObjectActiveStates(SaveManager manager, string operation)
    {
        if (manager == null)
            return;

        var states = manager.CurrentSaveData?.GameObjectStates;
        if (states == null || states.Count == 0)
            return;

        try
        {
            manager.ApplyGameObjectActiveStates(states);

            if (manager.EnforceActiveState)
                manager.ApplyGameObjectActiveStates(states);

            manager.StartActiveStateWatch(states);
        }
        catch (Exception ex)
        {
            Logger.Log($"{operation}: failed to reapply GameObject active states: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Warning);
        }
    }

    /// <summary>
    /// Re-applies <see cref="PersistentVisibilityController.ApplyVisibilityBasedOnScene"/>
    /// on every tracked <see cref="SaveablePrefab"/> and <see cref="ISaveable"/>
    /// (SaveableComponent) that carries a PVC.  This mirrors the sweep the
    /// <see cref="SceneLoadManager"/> performs at the end of
    /// <c>HandleDestroyedObjectsCoroutine</c> and <c>FinalizeLoad</c>.
    /// </summary>
    static void ReapplyPersistentVisibility(SaveManager manager, string operation)
    {
        if (manager == null)
            return;

        try
        {
            string sceneName = SceneManager.GetActiveScene().name;

            int pvcFromComponents = 0;
            // Sweep SaveableComponents (some may be PersistentVisibilityControllers)
            foreach (var component in manager.SaveableComponents)
            {
                if (component is PersistentVisibilityController pvc)
                {
                    pvcFromComponents++;
                    pvc.ApplyVisibilityBasedOnScene(sceneName);
                }
            }

            int pvcFromPrefabs = 0;
            // Sweep SaveablePrefabs
            var prefabManager = manager.GetPrefabManager;
            if (prefabManager != null)
            {
                var allPrefabs = prefabManager.SaveablePrefabs;
                foreach (var prefab in allPrefabs.ToArray())
                {
                    if (prefab == null)
                        continue;

                    if (!prefab.TryGetComponent(out PersistentVisibilityController prefabPvc))
                    {
                        // Safety-net: auto-add PVC if the prefab should have one but doesn't
                        // (mirrors the runtime auto-add in SaveablePrefab.InitializeInstance)
                        bool hasVisibleSceneFilter = prefab.VisibleInScenes != null && prefab.VisibleInScenes.Count > 0;
                        if ((prefab.KeepAcrossScenes || prefab.RememberHomeScene) && hasVisibleSceneFilter)
                        {
                            prefabPvc = prefab.gameObject.AddComponent<PersistentVisibilityController>();
                        }
                        else
                        {
                            continue;
                        }
                    }

                    pvcFromPrefabs++;
                    prefabPvc.ApplyVisibilityBasedOnScene(sceneName);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"{operation}: failed to reapply persistent visibility: {ex.Message}", LogCategory.SaveManagerExtensions, LogLevel.Warning);
        }
    }

    #region Guard Rails - Timing Validation

    /// <summary>
    /// Validates prefab populate timing to detect common mistakes that cause prefabs to spawn in wrong scenes.
    /// Logs warnings when potential timing issues are detected.
    /// </summary>
    /// <param name="manager">SaveManager instance</param>
    /// <param name="operationName">Name of the operation being validated</param>
    private static void ValidatePrefabPopulateTiming(SaveManager manager, string operationName)
    {
        if (manager == null)
            return;

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded || string.IsNullOrEmpty(activeScene.name))
        {
            Logger.Log(
                $"[SCENELOAD][{operationName}] WARNING: No active scene is currently loaded. " +
                "This may cause prefabs to spawn in an unexpected scene or fail to spawn entirely. " +
                "Ensure you call this method BEFORE loading your target scene, not after.", LogCategory.SaveManagerExtensions, LogLevel.Warning
            );
            return;
        }

        // Check if we have multiple scenes loaded (additive loading scenario)
        int loadedSceneCount = SceneManager.sceneCount;
        if (loadedSceneCount > 1)
        {
            // This is expected during additive loading workflows
            // Log informational message about timing best practices
            Logger.Log(
                $"[SCENELOAD][{operationName}] INFO: Multiple scenes loaded ({loadedSceneCount} total). " +
                $"Current active scene: '{activeScene.name}'. " +
                "Prefabs will spawn into the active scene on the next frame. " +
                "If this is not your target scene, call this method BEFORE loading the target scene, " +
                "then set the target scene as active after it finishes loading.", LogCategory.SaveManagerExtensions, LogLevel.Info
            );
        }

        // Check if there's already saved data - this might indicate duplicate populate calls
        if (manager.CurrentSaveData != null && manager.CurrentSaveData.Prefabs != null)
        {
            int existingPrefabCount = manager.CurrentSaveData.Prefabs.Count;
            if (existingPrefabCount > 0)
            {
                Logger.Log(
                    $"[SCENELOAD][{operationName}] INFO: CurrentSaveData already contains {existingPrefabCount} prefabs. " +
                    "This populate operation will replace the existing data. " +
                    "If you're seeing duplicate prefabs, you may be calling populate multiple times.", LogCategory.SaveManagerExtensions, LogLevel.Info
                );
            }
        }

        // Detect if we're being called during an active scene load operation
        // (This is a best-effort check - Unity doesn't provide a direct API for this)
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene != activeScene)
            {
                // Check if this scene was just loaded (heuristic: root object count is very low)
                var rootObjects = scene.GetRootGameObjects();
                if (rootObjects.Length > 0 && scene.name != "DontDestroyOnLoad")
                {
                    Logger.Log(
                        $"[SCENELOAD][{operationName}] DETECTED: Scene '{scene.name}' is loaded but not active. " +
                        "If this is your target scene, remember to call SceneManager.SetActiveScene() " +
                        "AFTER the scene finishes loading and BEFORE prefabs spawn.", LogCategory.SaveManagerExtensions, LogLevel.Info
                    );
                }
            }
        }
    }

    #endregion
}
}
#endif
