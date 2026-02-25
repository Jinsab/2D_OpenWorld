#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    public interface ISyncSerializer
    {
        byte[] Serialize<T>(T payload);
        T Deserialize<T>(byte[] data);
    }
}
#endif
