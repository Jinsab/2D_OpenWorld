#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    public enum SyncMessageType
    {
        Snapshot,
        Diff,
        Ack,
        Nack,
        Ping,
        Pong,
        Hello,
        Custom
    }
}
#endif
