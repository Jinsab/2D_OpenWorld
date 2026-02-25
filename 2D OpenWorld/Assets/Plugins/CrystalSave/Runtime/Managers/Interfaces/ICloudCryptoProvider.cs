#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    public interface ICloudCryptoProvider
    {
        ValueTask<byte[]> EncryptForCloudAsync(byte[] plain);
        ValueTask<byte[]> DecryptFromCloudAsync(byte[] blob);
    }
}
#endif
