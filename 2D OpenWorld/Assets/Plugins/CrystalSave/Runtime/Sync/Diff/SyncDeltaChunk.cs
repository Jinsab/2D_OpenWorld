#if MEMORYPACK && ARAWN_REMEMBERME
using System;

namespace Arawn.CrystalSave.Runtime
{
    [Serializable]
    public class SyncDeltaChunk
    {
        public int offset;
        public byte[] data;
    }
}
#endif
