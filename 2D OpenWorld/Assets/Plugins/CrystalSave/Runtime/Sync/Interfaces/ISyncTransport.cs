#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    public interface ISyncTransport
    {
        string Name { get; }
        bool IsConnected { get; }
        bool IsServer { get; }

        event Action<SyncEnvelope> OnMessage;

        ValueTask InitializeAsync(SyncTransportConfig config);
        ValueTask ConnectAsync();
        ValueTask DisconnectAsync();
        ValueTask SendAsync(SyncEnvelope message);
    }
}
#endif
