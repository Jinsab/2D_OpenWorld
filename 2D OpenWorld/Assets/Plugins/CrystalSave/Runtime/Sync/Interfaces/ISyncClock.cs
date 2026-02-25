#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    public interface ISyncClock
    {
        long NowTicks { get; }
    }
}
#endif
