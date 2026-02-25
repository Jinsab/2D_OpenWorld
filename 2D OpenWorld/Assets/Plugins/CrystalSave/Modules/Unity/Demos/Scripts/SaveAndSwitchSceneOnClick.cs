#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// Saves the current game to <paramref name="slotNumber"/> and then
    /// switches to <paramref name="targetSceneName"/> as soon as the save
    /// finishes.  No intermediate reload is performed.
    /// </summary>
    public sealed class SaveAndSwitchSceneOnClick : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button triggerButton;

        [Header("Scene to load afterwards")]
        [SerializeField] private string targetSceneName = "NextScene";

        CancellationTokenSource cts;
        bool isBusy;

        void Awake()
        {
            if (!triggerButton)
            {
                Logger.Log($"{nameof(SaveAndSwitchSceneOnClick)} button missing", LogLevel.Warning);
                enabled = false;
                return;
            }

            cts = new CancellationTokenSource();
            triggerButton.onClick.AddListener(OnClickAsync);
        }

        void OnDestroy()
        {
            cts.Cancel();
            if (triggerButton)
                triggerButton.onClick.RemoveListener(OnClickAsync);
            // Avoid post-scene-switch callbacks touching a destroyed Button
            triggerButton = null;
        }

        async void OnClickAsync()
        {
            if (isBusy) return;
            isBusy = true;
            triggerButton.interactable = false;

            var mgr = SaveManager.Instance;
            if (mgr == null)
            {
                Logger.Log("SaveManager instance not found", LogLevel.Warning);
                ResetUI();
                return;
            }

            if (!SaveManager.AreSaveSlotsReady)
            {
                Logger.Log("SaveAndSwitch: waiting for SaveManager to initialise...", LogLevel.Info);
                await mgr.WaitForSaveSlotsAsync();
                if (!SaveManager.AreSaveSlotsReady)
                {
                    Logger.Log("SaveAndSwitch: save slots not ready yet", LogLevel.Warning);
                    ResetUI();
                    return;
                }
            }

            try
            {
                await SaveManagerExtensions.LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(mgr, targetSceneName, false, false);
            }
            catch (OperationCanceledException)
            {
                Logger.Log("SaveAndSwitch: operation cancelled", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                Logger.Log($"SaveAndSwitch: save failed – {ex.Message}", LogLevel.Error);
            }
            finally
            {
                // Object or button may be destroyed after scene switch
                if (this)
                    ResetUI();
            }
        }

        void ResetUI()
        {
            // Button might have been destroyed during scene change
            if (triggerButton)
                triggerButton.interactable = true;
            isBusy = false;
        }
    }
}
#endif
