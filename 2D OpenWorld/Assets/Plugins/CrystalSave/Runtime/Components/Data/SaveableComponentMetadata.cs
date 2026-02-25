#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
    [MemoryPackable]
    public partial class ComponentDataMetadata
    {
        public int LoadPriority { get; set; }
        public bool DeferLowPriorityUntilRequested { get; set; }
        public string HomeScene { get; set; }

        public ComponentDataMetadata()
        {
            LoadPriority = 50;
            DeferLowPriorityUntilRequested = false;
            HomeScene = null;
        }

        [MemoryPackConstructor]
        public ComponentDataMetadata(int loadPriority, bool deferLowPriorityUntilRequested, string homeScene)
        {
            LoadPriority = loadPriority;
            DeferLowPriorityUntilRequested = deferLowPriorityUntilRequested;
            HomeScene = homeScene;
        }
    }
}
#endif
