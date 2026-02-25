#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    public struct SyncTransportConfig
    {
        public SyncSettings settings;
        public ISyncSerializer serializer;
        public bool isServer;
    }
}
#endif
