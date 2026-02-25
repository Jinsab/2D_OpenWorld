#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(
        fileName = "StaticMasterSecret",
        menuName = "Crystal Save/Settings/Security/Static Master Secret",
        order = 800)]
    public sealed class StaticMasterSecret : ScriptableObject, IMasterSecretProvider
    {
        [Tooltip("32-byte secret, Base-64 encoded (auto-generated when asset is created)")]
        [SerializeField/*, HideInInspector*/]          // uncomment HideInInspector if you prefer
        private string base64;

#if UNITY_EDITOR
        private void OnEnable()
        {
            EnsureHasKey();
        }

        private void OnValidate()
        {
            EnsureHasKey();
        }

        private void EnsureHasKey()
        {
            if (IsValidBase64(base64)) return;

            byte[] secret = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(secret);

            base64 = Convert.ToBase64String(secret);

            EditorUtility.SetDirty(this);
            EditorApplication.delayCall += AssetDatabase.SaveAssets;
        }

        private static bool IsValidBase64(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            try { return Convert.FromBase64String(value).Length == 32; }
            catch { return false; }
        }
#endif

        /*───────────────────────────────────────────────────────────────*/

        public ValueTask<byte[]> GetMasterSecretAsync()
            => new ValueTask<byte[]>(Convert.FromBase64String(base64));
    }
}
#endif
