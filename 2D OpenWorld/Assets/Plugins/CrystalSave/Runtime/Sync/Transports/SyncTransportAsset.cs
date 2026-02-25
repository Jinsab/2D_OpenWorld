#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public abstract class SyncTransportAsset : ScriptableObject, ISyncTransport
    {
        [SerializeField] private string transportName = "SyncTransport";

        public string Name => string.IsNullOrWhiteSpace(transportName) ? name : transportName;
        public virtual bool IsConnected { get; protected set; }
        public virtual bool IsServer { get; protected set; }

        public event Action<SyncEnvelope> OnMessage;

        protected void RaiseMessage(SyncEnvelope envelope)
        {
            OnMessage?.Invoke(envelope);
        }

        public abstract ValueTask InitializeAsync(SyncTransportConfig config);
        public abstract ValueTask ConnectAsync();
        public abstract ValueTask DisconnectAsync();
        public abstract ValueTask SendAsync(SyncEnvelope message);
    }
}
#endif
