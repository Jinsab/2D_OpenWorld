#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demo
{
    public class SaveSlotsDemoUI : MonoBehaviour
    {
        [Header("Assign these in the Inspector")]
        public Image slot1Image, slot2Image, slot3Image;
        public Text  slot1MetadataText, slot2MetadataText, slot3MetadataText;

        void OnEnable()
        {
            // Update UI when slots list changes or when a save completes
            SaveManager.Instance.OnSaveSlotsUpdated += OnSlotsUpdated;
            SaveManager.Instance.OnSaveCompleted    += OnSaveCompleted;

            // do a full load once we actually log in
            SupabaseAuthRelay.LoggedIn       += OnSupabaseLoggedIn;
        }

        void OnDisable()
        {
            SaveManager.Instance.OnSaveSlotsUpdated -= OnSlotsUpdated;
            SaveManager.Instance.OnSaveCompleted    -= OnSaveCompleted;

            SupabaseAuthRelay.LoggedIn       -= OnSupabaseLoggedIn;
        }

        async void Start()
        {
            // initial full load (if already logged in or using guest)
            await FullLoadAndPaint();
        }

        // 1) Full load: init slots + refresh remote, then paint
        private async Task FullLoadAndPaint()
        {
            await SaveManager.Instance
                .InitializeSaveSlotsAsync(SaveManager.Instance.SaveSettings.numberOfSaveSlots);
            await SaveManager.Instance
                .InitializeQuickSaveSlotsAsync(SaveManager.Instance.SaveSettings.numberOfQuickSaveSlots);

            await SaveManager.Instance.RefreshRemoteSlotsAsync();
            PaintUI();
        }

        // 2) Called when user logs in: do full load again
        async void OnSupabaseLoggedIn(string uid)
        {
            await FullLoadAndPaint();
        }

        // 3) Called when SaveManager has new data: only repaint UI
        void OnSlotsUpdated()
        {
            PaintUI();
        }

        // Called when a save operation finishes
        void OnSaveCompleted(object sender, SaveLoadEventArgs e)
        {
            // For Supabase, just repaint UI directly without triggering full refresh
            // to avoid race conditions with remote synchronization
            PaintUI();
        }

        // 4) Repaint current state (no async calls here!)
        private void PaintUI()
        {
            StartCoroutine(PaintSlotCoroutine(slot1Image, slot1MetadataText, 1));
            StartCoroutine(PaintSlotCoroutine(slot2Image, slot2MetadataText, 2));
            StartCoroutine(PaintSlotCoroutine(slot3Image, slot3MetadataText, 3));
        }

        // Helper to paint one slot (coroutine so you can await the download if needed)
        private IEnumerator PaintSlotCoroutine(Image img, Text txt, int slotNumber)
        {
            var slot = SaveManager.Instance.GetSaveSlotByNumber(slotNumber);
            if (slot == null)
            {
                txt.text = $"Slot {slotNumber}: <empty>";
                yield break;
            }

            // metadata
            if (slot.LastSaved > DateTime.MinValue)
                txt.text = $"Slot {slotNumber}: “{slot.SlotName}” @ {slot.LastSaved.ToLocalTime():g}";
            else
                txt.text = $"Slot {slotNumber}: <empty>";

            string meta = FormatCustomMetadata(slot);
            if (!string.IsNullOrEmpty(meta))
                txt.text += "\n" + meta;

            // screenshot
            Texture2D tex = SaveManager.Instance.GetScreenshot(slot);
            if (tex == null 
                && SaveManager.Instance.SaveSettings.backend == SaveBackend.Supabase
                && SaveManager.Instance.SaveSystem is SupabaseSaveSystem supabase
                && !string.IsNullOrEmpty(slot.ScreenshotFileName))
            {
                var dl = supabase.DownloadScreenshotAsync(slot.ScreenshotFileName);
                yield return new WaitUntil(() => dl.IsCompleted);
                try
                {
                    var data = dl.Result;
                    if (data?.Length > 0)
                    {
                        tex = new Texture2D(2,2);
                        tex.LoadImage(data);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Screenshot download failed: {e.Message}");
                }
            }

            if (tex != null)
                img.sprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height),
                                          new Vector2(0.5f,0.5f));
        }

        string FormatCustomMetadata(SaveSlot s)
        {
            if (s == null) return string.Empty;
            if (s.CustomMetadata != null && s.CustomMetadata.Count > 0)
                return string.Join("\n", s.CustomMetadata.Select(kvp => $"{kvp.Key}: {kvp.Value}"));

            var settings = SaveManager.Instance?.SaveSettings;
            if (settings != null && settings.defaultSlotMetadata != null)
            {
                var dict = settings.defaultSlotMetadata.ToDictionary();
                if (dict.Count > 0)
                    return string.Join("\n", dict.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            }
            return string.Empty;
        }
    }
}
#endif