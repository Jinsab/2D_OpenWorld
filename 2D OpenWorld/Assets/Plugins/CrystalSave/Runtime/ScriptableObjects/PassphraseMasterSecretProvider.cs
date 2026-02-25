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
        fileName = "PassphraseMasterSecretProvider",
        menuName = "Crystal Save/Settings/Security/User Passphrase Provider",
        order = 802)]
    public sealed class PassphraseMasterSecretProvider : ScriptableObject, IMasterSecretProvider
    {
        [Tooltip("Base64-encoded salt used for deriving the master secret. Changing this invalidates existing saves.")]
        [SerializeField]
        private string saltBase64;

        [Tooltip("PBKDF2 iteration count for deriving the master secret.")]
        [SerializeField]
        private int iterations = 100000;

        [NonSerialized]
        private string runtimePassphrase;

        private static string globalPassphrase;

#if UNITY_EDITOR
        private void OnEnable()
        {
            EnsureHasSalt();
        }

        private void OnValidate()
        {
            EnsureHasSalt();
        }
#endif

        public void SetPassphrase(string passphrase)
        {
            runtimePassphrase = passphrase;
        }

        public static void SetGlobalPassphrase(string passphrase)
        {
            globalPassphrase = passphrase;
        }

        public static void ClearGlobalPassphrase()
        {
            globalPassphrase = null;
        }

        public ValueTask<byte[]> GetMasterSecretAsync()
        {
            string passphrase = !string.IsNullOrEmpty(runtimePassphrase)
                ? runtimePassphrase
                : globalPassphrase;

            if (string.IsNullOrEmpty(passphrase))
            {
                Logger.Log("PassphraseMasterSecretProvider: passphrase not set.", LogCategory.Cryptography, LogLevel.Warning);
                return new ValueTask<byte[]>((byte[])null);
            }

            byte[] salt = GetSaltBytes();
            if (salt == null || salt.Length < 8)
            {
                Logger.Log("PassphraseMasterSecretProvider: invalid salt.", LogCategory.Cryptography, LogLevel.Warning);
                return new ValueTask<byte[]>((byte[])null);
            }

            using var kdf = new Rfc2898DeriveBytes(passphrase, salt, iterations, HashAlgorithmName.SHA256);
            return new ValueTask<byte[]>(kdf.GetBytes(32));
        }

        private byte[] GetSaltBytes()
        {
            try
            {
                return Convert.FromBase64String(saltBase64);
            }
            catch
            {
                return null;
            }
        }

#if UNITY_EDITOR
        private void EnsureHasSalt()
        {
            if (IsValidSalt(saltBase64)) return;

            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            saltBase64 = Convert.ToBase64String(salt);

            EditorUtility.SetDirty(this);
            EditorApplication.delayCall += AssetDatabase.SaveAssets;
        }

        private static bool IsValidSalt(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            try { return Convert.FromBase64String(value).Length >= 8; }
            catch { return false; }
        }
#endif
    }
}
#endif
