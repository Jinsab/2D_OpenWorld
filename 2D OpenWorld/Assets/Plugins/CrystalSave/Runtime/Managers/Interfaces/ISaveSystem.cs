#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    public interface ISaveSystem
    {   
        Task SaveAsync(byte[] data, SaveSlot slot);
        Task<byte[]> LoadAsync(SaveSlot slot);
        Task DeleteAsync(SaveSlot slot);
        Task<SaveSlot> LoadSlotMetadataAsync(int slotNumber);
        Task SaveSlotMetadataAsync(SaveSlot slot);
        void Save (byte[] data, SaveSlot slot);
        byte[] Load (SaveSlot slot);
        void Delete (SaveSlot slot);
        string GetSaveGamesPath();
        void BackupSlot   (SaveSlot slot);
        void RestoreBackup(SaveSlot slot);
        void DeleteBackup (SaveSlot slot);

        // Temporary file helpers used when keepLocalMirror is disabled but
        // enableSaveFileVerification is on. Implementations should write the
        // already encrypted/compressed blob to a transient file so verification
        // can read it back before uploading to the cloud.
        Task WriteTempAsync(byte[] data, SaveSlot slot);
        Task<byte[]> LoadTempAsync(SaveSlot slot);
        void DeleteTemp(SaveSlot slot);
    }
}

#endif