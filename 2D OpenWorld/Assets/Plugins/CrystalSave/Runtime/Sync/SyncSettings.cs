#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(fileName = "SyncSettings", menuName = "Crystal Save/Settings/Sync Settings", order = 820)]
    public class SyncSettings : ScriptableObject
    {
        [Tooltip("Enable snapshot syncing through registered transports.")]
        public bool enableSnapshots = true;

        [Tooltip("Enable diff syncing through registered transports.")]
        public bool enableDiffs = false;

        [Tooltip("Seconds between snapshot syncs.")]
        public float snapshotIntervalSeconds = 300f;

        [Tooltip("Seconds between diff syncs (for high-frequency transport).")]
        public float diffIntervalSeconds = 0.25f;

        [Tooltip("Auto-connect transports on startup.")]
        public bool autoConnectTransports = true;

        [Tooltip("Registered sync transports (network-agnostic).")]
        public List<SyncTransportAsset> transports = new();
    }
}
#endif
