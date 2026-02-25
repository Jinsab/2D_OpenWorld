#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
    [MemoryPackable]                 // ← keep, this is the first and only time
    public partial class ComponentBlob
    {
        public string GuidSuffix { get; set; }   // was UniqueID
        public byte[] Payload    { get; set; }

        [MemoryPackConstructor]
        public ComponentBlob(string guidSuffix, byte[] payload)
        {
            GuidSuffix = guidSuffix;
            Payload    = payload;
        }
    }

    // ───────────────────────────────────────────────────────────
    // EXTENSION of the existing RuntimeModificationData class
    // ───────────────────────────────────────────────────────────
    public partial class RuntimeModificationData   // Extension to add blobs
    {
        public List<ComponentBlob> ComponentBlobs { get; set; } = new();
    }
}
#endif