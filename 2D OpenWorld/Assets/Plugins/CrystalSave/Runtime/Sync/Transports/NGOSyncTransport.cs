#if MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_NGO_PRESENT
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(fileName = "NGOSyncTransport", menuName = "Crystal Save/Sync/NGO Sync Transport", order = 821)]
    public sealed class NGOSyncTransport : SyncTransportAsset
    {
        [Tooltip("Named message key used for sync payloads.")]
        public string messageName = "CrystalSaveSync";

        private ISyncSerializer serializer;
        private CustomMessagingManager messaging;

        public override ValueTask InitializeAsync(SyncTransportConfig config)
        {
            serializer = config.serializer ?? new SyncJsonSerializer();
            IsServer = config.isServer;

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Logger.Log("NGOSyncTransport: NetworkManager.Singleton not found.", LogCategory.SaveManager, LogLevel.Warning);
                return ValueTask.CompletedTask;
            }

            messaging = networkManager.CustomMessagingManager;
            if (messaging != null)
            {
                messaging.RegisterNamedMessageHandler(messageName, HandleMessage);
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask ConnectAsync()
        {
            IsConnected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            return ValueTask.CompletedTask;
        }

        public override ValueTask DisconnectAsync()
        {
            if (messaging != null)
            {
                messaging.UnregisterNamedMessageHandler(messageName);
            }
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public override ValueTask SendAsync(SyncEnvelope message)
        {
            if (messaging == null)
                return ValueTask.CompletedTask;

            byte[] payload = serializer.Serialize(message);
            if (payload == null)
                return ValueTask.CompletedTask;

            using var writer = new FastBufferWriter(sizeof(int) + payload.Length, Allocator.Temp);
            writer.WriteValueSafe(payload.Length);
            writer.WriteBytesSafe(payload);

            var net = NetworkManager.Singleton;
            if (net == null) return ValueTask.CompletedTask;

            if (net.IsServer)
            {
                messaging.SendNamedMessage(messageName, net.ConnectedClientsIds, writer);
            }
            else
            {
                messaging.SendNamedMessage(messageName, NetworkManager.ServerClientId, writer);
            }

            return ValueTask.CompletedTask;
        }

        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!reader.TryBeginRead(sizeof(int)))
                return;

            reader.ReadValueSafe(out int length);
            if (length <= 0 || !reader.TryBeginRead(length))
                return;

            byte[] data = new byte[length];
            reader.ReadBytesSafe(ref data, length);

            SyncEnvelope envelope = serializer.Deserialize<SyncEnvelope>(data);
            RaiseMessage(envelope);
        }
    }
}
#endif
