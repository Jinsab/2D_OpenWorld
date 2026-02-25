#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public sealed class SyncManager
    {
        private readonly SaveManager saveManager;
        private readonly SyncSettings settings;
        private readonly ISyncSerializer serializer;
        private readonly List<ISyncTransport> transports = new();
        private readonly Dictionary<string, byte[]> lastSnapshots = new();
        private readonly Dictionary<string, int> sequenceBySlot = new();
        private readonly SyncReconciler reconciler;

        public bool IsInitialized { get; private set; }

        public event Action<SyncEnvelope> OnMessage;
        public SyncReconciler Reconciler => reconciler;

        public SyncManager(SaveManager manager, SyncSettings syncSettings)
        {
            saveManager = manager;
            settings = syncSettings;
            serializer = new SyncJsonSerializer();
            reconciler = new SyncReconciler(serializer);
            RegisterConfiguredTransports();
        }

        private void RegisterConfiguredTransports()
        {
            if (settings == null || settings.transports == null) return;
            foreach (var transport in settings.transports)
            {
                if (transport == null) continue;
                RegisterTransport(transport);
            }
        }

        public void RegisterTransport(ISyncTransport transport)
        {
            if (transport == null || transports.Contains(transport)) return;
            transports.Add(transport);
            transport.OnMessage += HandleIncoming;
        }

        public void UnregisterTransport(ISyncTransport transport)
        {
            if (transport == null) return;
            transport.OnMessage -= HandleIncoming;
            transports.Remove(transport);
        }

        public async Task InitializeAsync()
        {
            if (IsInitialized || settings == null) return;

            var config = new SyncTransportConfig
            {
                settings = settings,
                serializer = serializer,
                isServer = Application.isBatchMode
            };

            foreach (var transport in transports)
            {
                await transport.InitializeAsync(config);
            }

            if (settings.autoConnectTransports)
            {
                foreach (var transport in transports)
                {
                    await transport.ConnectAsync();
                }
            }

            IsInitialized = true;
        }

        public async ValueTask SendSnapshotAsync(byte[] payload, string slotId = null)
        {
            if (payload == null || settings == null || !settings.enableSnapshots) return;
            CacheSnapshot(slotId, payload);
            var envelope = CreateEnvelope(SyncMessageType.Snapshot, SyncChannel.Snapshot, payload, slotId);
            await BroadcastAsync(envelope);
        }

        public async ValueTask SendDiffFromSnapshotAsync(byte[] newSnapshot, string slotId = null)
        {
            if (newSnapshot == null || settings == null || !settings.enableDiffs)
            {
                await SendSnapshotAsync(newSnapshot, slotId);
                return;
            }

            string slotKey = NormalizeSlot(slotId);
            if (!lastSnapshots.TryGetValue(slotKey, out var baseSnapshot) || baseSnapshot == null)
            {
                await SendSnapshotAsync(newSnapshot, slotId);
                return;
            }

            if (!SyncDeltaCodec.TryCreateDelta(baseSnapshot, newSnapshot, out var delta))
            {
                await SendSnapshotAsync(newSnapshot, slotId);
                return;
            }

            CacheSnapshot(slotId, newSnapshot);

            byte[] deltaBytes = serializer.Serialize(delta);
            var envelope = CreateEnvelope(SyncMessageType.Diff, SyncChannel.Diff, deltaBytes, slotId);
            await BroadcastAsync(envelope);
        }

        public async ValueTask SendDiffAsync(byte[] payload, string slotId = null)
        {
            if (payload == null || settings == null || !settings.enableDiffs) return;
            var envelope = CreateEnvelope(SyncMessageType.Diff, SyncChannel.Diff, payload, slotId);
            await BroadcastAsync(envelope);
        }

        private async ValueTask BroadcastAsync(SyncEnvelope envelope)
        {
            foreach (var transport in transports)
            {
                await transport.SendAsync(envelope);
            }
        }

        private SyncEnvelope CreateEnvelope(SyncMessageType type, SyncChannel channel, byte[] payload, string slotId)
        {
            return new SyncEnvelope
            {
                messageType = type,
                channel = channel,
                slotId = slotId,
                sequence = NextSequence(slotId),
                timestampUtcTicks = DateTime.UtcNow.Ticks,
                payloadHash = SyncDeltaCodec.ComputeHash(payload),
                payloadBase64 = Convert.ToBase64String(payload)
            };
        }

        private int NextSequence(string slotId)
        {
            string key = NormalizeSlot(slotId);
            if (!sequenceBySlot.TryGetValue(key, out int seq))
                seq = 0;
            seq++;
            sequenceBySlot[key] = seq;
            return seq;
        }

        private static string NormalizeSlot(string slotId)
        {
            return string.IsNullOrEmpty(slotId) ? "default" : slotId;
        }

        private void CacheSnapshot(string slotId, byte[] snapshot)
        {
            if (snapshot == null) return;
            lastSnapshots[NormalizeSlot(slotId)] = snapshot;
        }

        private void HandleIncoming(SyncEnvelope envelope)
        {
            reconciler.TryApplyEnvelope(envelope, out _);
            OnMessage?.Invoke(envelope);
        }
    }
}
#endif
