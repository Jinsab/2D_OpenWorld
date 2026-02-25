#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    public interface IMasterSecretProvider
    {
        ValueTask<byte[]> GetMasterSecretAsync();
    }
}

#endif