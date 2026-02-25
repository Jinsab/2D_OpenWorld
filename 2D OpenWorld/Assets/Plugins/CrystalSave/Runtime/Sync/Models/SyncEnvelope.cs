#if MEMORYPACK && ARAWN_REMEMBERME
using System;

namespace Arawn.CrystalSave.Runtime
{
    [Serializable]
    public struct SyncEnvelope
    {
        public SyncMessageType messageType;
        public SyncChannel channel;
        public string slotId;
        public int sequence;
        public long timestampUtcTicks;
        public string payloadHash;
        public string payloadBase64;
    }
}
#endif
