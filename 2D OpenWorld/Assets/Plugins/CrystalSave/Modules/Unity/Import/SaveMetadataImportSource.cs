#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Arawn.CrystalSave.Modules.Unity.Import
{
    [Serializable]
    public class SaveSlotEvent : UnityEvent<Arawn.CrystalSave.Runtime.SaveSlot> {}

    [AddComponentMenu("Crystal Save/Import/Save Metadata Import Source")]
    public class SaveMetadataImportSource : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Optional binary file containing metadata (MemoryPack, optionally CSAV-encrypted, or JSON).")]
        public TextAsset binarySource;

        [Tooltip("Optional string input: base64 payload or raw JSON text.")]
        [TextArea(2, 10)]
        public string base64OrJson;

    [Header("Events")]
    public SaveSlotEvent onImported;
        public UnityEvent onFailed;

        [ContextMenu("Import Now")]
        public void ImportNow()
        {
            var enc = Arawn.CrystalSave.Runtime.SaveManager.Instance?.EncryptionService;

            // 1) Try binary asset first
            if (binarySource != null && binarySource.bytes != null && binarySource.bytes.Length > 0)
            {
                if (Arawn.CrystalSave.Runtime.SaveMetadataImporter.TryParse(binarySource.bytes, out var slot, enc))
                {
                    onImported?.Invoke(slot);
                    return;
                }
            }

            // 2) Try text path
            if (!string.IsNullOrWhiteSpace(base64OrJson))
            {
                if (Arawn.CrystalSave.Runtime.SaveMetadataImporter.TryParseFromBase64OrJson(base64OrJson, out var slot, enc))
                {
                    onImported?.Invoke(slot);
                    return;
                }
            }

            onFailed?.Invoke();
        }
    }
}
#endif
