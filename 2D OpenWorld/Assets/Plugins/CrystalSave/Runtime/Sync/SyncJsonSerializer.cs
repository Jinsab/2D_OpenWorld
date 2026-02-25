#if MEMORYPACK && ARAWN_REMEMBERME
using System.Text;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public sealed class SyncJsonSerializer : ISyncSerializer
    {
        public byte[] Serialize<T>(T payload)
        {
            if (payload == null) return null;
            string json = JsonUtility.ToJson(payload);
            return Encoding.UTF8.GetBytes(json);
        }

        public T Deserialize<T>(byte[] data)
        {
            if (data == null || data.Length == 0) return default;
            string json = Encoding.UTF8.GetString(data);
            return JsonUtility.FromJson<T>(json);
        }
    }
}
#endif
