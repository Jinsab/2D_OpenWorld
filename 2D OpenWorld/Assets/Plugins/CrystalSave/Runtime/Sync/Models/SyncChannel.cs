#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    public enum SyncChannel
    {
        Reliable,
        Unreliable,
        Snapshot,
        Diff,
        Control
    }
}
#endif
