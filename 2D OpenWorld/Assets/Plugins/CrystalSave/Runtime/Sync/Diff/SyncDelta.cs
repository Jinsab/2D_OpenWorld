#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
    [Serializable]
    public class SyncDelta
    {
        public string baseHash;
        public int baseLength;
        public int newLength;
        public List<SyncDeltaChunk> chunks = new();
    }
}
#endif
