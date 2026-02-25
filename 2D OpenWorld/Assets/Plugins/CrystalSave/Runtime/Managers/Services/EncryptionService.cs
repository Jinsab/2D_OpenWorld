#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public class EncryptionService
        {
                private readonly SaveSettings settings;
                private IMasterSecretProvider secretProvider;
                private byte[] masterSecret;
                private byte[] cryptoKey;

                public bool UseEncryption { get; private set; }

                public EncryptionService(SaveSettings settings)
                {
                        this.settings = settings;
                }

                public async Task InitializeAsync(string userId)
                {
                        UseEncryption = settings.enableEncryption;
                        if (UseEncryption && settings.masterSecretProvider == null)
                        {
                                Logger.Log(
                                        "Encryption enabled but no Master-Secret Provider assigned – disabling encryption.",
                                        LogLevel.Warning);
                                UseEncryption = false;
                                return;
                        }

                        if (UseEncryption)
                        {
                                secretProvider = settings.masterSecretProvider as IMasterSecretProvider;
                                if (secretProvider == null)
                                {
                                        Logger.Log(
                                                "Master-Secret Provider does not implement IMasterSecretProvider – disabling encryption.",
                                                LogLevel.Warning);
                                        UseEncryption = false;
                                        return;
                                }
                                masterSecret   = await secretProvider.GetMasterSecretAsync();
                                if (masterSecret?.Length != 32)
                                {
                                        Logger.Log(
                                                "Master-secret must be exactly 32 bytes – disabling encryption.",
                                                LogLevel.Warning);
                                        UseEncryption  = false;
                                        secretProvider = null;
                                        masterSecret   = null;
                                        cryptoKey      = null;
                                }
                                else
                                {
                                        string effectiveUserId = settings.useUserIdForEncryption
                                                ? userId
                                                : SaveCrypto.GlobalUserId;
                                        cryptoKey = SaveCrypto.DeriveKey(effectiveUserId, masterSecret);
                                }
                        }
                }

                public byte[] MaybeEncrypt(byte[] plain)
                {
                        if (!UseEncryption) return plain;

                        Span<byte> header = stackalloc byte[5];
                        header[0] = (byte)'C';
                        header[1] = (byte)'S';
                        header[2] = (byte)'A';
                        header[3] = (byte)'V';
                        header[4] = 1;
                        return SaveCrypto.Encrypt(plain, cryptoKey, header);
                }

                public byte[] MaybeDecrypt(byte[] blob)
                {
                        bool looksEncrypted = blob != null &&
                                        blob.Length > 33 &&
                                        blob[0] == (byte)'C' &&
                                        blob[1] == (byte)'S' &&
                                        blob[2] == (byte)'A' &&
                                        blob[3] == (byte)'V';

                        // If the payload isn't tagged as encrypted, pass through unchanged
                        if (!looksEncrypted) return blob;

                        // If encryption is disabled, don't attempt to decrypt old encrypted data
                        if (!UseEncryption)
                        {
                                Logger.Log("Encountered encrypted save data while encryption is disabled; treating as unavailable.", LogCategory.Cryptography, LogLevel.Info);
                                return null;
                        }

                        // Guard against missing/invalid key to avoid noisy errors
                        if (cryptoKey == null || cryptoKey.Length != 32)
                        {
                                Logger.Log("Encryption key unavailable; cannot decrypt encrypted save data.", LogCategory.Cryptography, LogLevel.Warning);
                                return null;
                        }

                        if (SaveCrypto.TryDecrypt(blob, cryptoKey, 5, out var plain))
                                return plain;

                        Logger.Log("Load failed: save file was encrypted with a different master secret.",
                                LogLevel.Error);
                        return null;
                }
        }
}
#endif
