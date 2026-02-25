#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    public struct SyncResult
    {
        public bool success;
        public string error;

        public static SyncResult Ok() => new SyncResult { success = true };
        public static SyncResult Fail(string message) => new SyncResult { success = false, error = message };
    }
}
#endif
