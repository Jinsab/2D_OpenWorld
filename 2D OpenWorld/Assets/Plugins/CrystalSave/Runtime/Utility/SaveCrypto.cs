// ------------------------------------------------
// RFC-5869 HKDF-SHA-256  +  AES-256-GCM
// Desktop => System.Security.Cryptography.AesGcm
// WebGL  =>  Bouncy Castle 2.6.1  (GcmBlockCipher)
// ------------------------------------------------
#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

#if BOUNCYCASTLE && (UNITY_WEBGL || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto;
#endif

namespace Arawn.CrystalSave.Runtime
{
    public static class SaveCrypto
    {
        const int KEY_LEN   = 32;   // 256-bit
        const int NONCE_LEN = 12;   // 96-bit (GCM)
        const int TAG_LEN   = 16;   // 128-bit
        public const string GlobalUserId = "crystalsave-global";

        /*────────────────── HKDF-SHA-256 (RFC 5869) ──────────────────*/
        public static byte[] DeriveKey(string uid, byte[] masterSecret)
        {
            using var hmac = new HMACSHA256(masterSecret);

            // PRK  = HMAC-SHA-256(salt, IKM)  – we use the user-ID as ‘salt’
            Span<byte> prk = stackalloc byte[KEY_LEN];
            if (string.IsNullOrEmpty(uid))
                uid = GlobalUserId;
            hmac.TryComputeHash(Encoding.UTF8.GetBytes(uid), prk, out _);

            // OKM = HMAC(PRK, 0x01)  → 1 block → 32 bytes
            Span<byte> okmInput = stackalloc byte[KEY_LEN + 1];
            prk.CopyTo(okmInput);
            okmInput[^1] = 0x01;

            Span<byte> okm = stackalloc byte[KEY_LEN];
            hmac.Initialize();                              // recycle instance
            hmac.TryComputeHash(okmInput, okm, out _);
            return okm.ToArray();
        }

        /*──────────── Fallback: AES-256-CBC + HMAC-SHA256 ────────────*/
        // Used when AesGcm is not available (older .NET Framework on Windows)
        // This implements Encrypt-then-MAC pattern for authenticated encryption
        
        private static byte[] EncryptFallback(byte[] plain, byte[] key, byte[] nonce)
        {
            // Split key: first 32 bytes for AES, derive HMAC key from nonce
            byte[] aesKey = key;
            byte[] hmacKey = new byte[32];
            using (var sha = SHA256.Create())
            {
                byte[] temp = sha.ComputeHash(nonce.Concat(key).ToArray());
                Buffer.BlockCopy(temp, 0, hmacKey, 0, 32);
            }

            // AES-256-CBC encryption
            byte[] ciphertext;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = aesKey;
                aes.IV = new byte[16]; // Use first 12 bytes of nonce, pad with zeros
                Buffer.BlockCopy(nonce, 0, aes.IV, 0, Math.Min(nonce.Length, 16));
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(plain, 0, plain.Length);
                    cs.FlushFinalBlock();
                    ciphertext = ms.ToArray();
                }
            }

            // Compute HMAC over ciphertext (Encrypt-then-MAC)
            byte[] mac;
            using (var hmac = new HMACSHA256(hmacKey))
            {
                mac = hmac.ComputeHash(ciphertext);
            }

            // Return ciphertext || MAC (first 16 bytes of MAC to match TAG_LEN)
            byte[] result = new byte[ciphertext.Length + TAG_LEN];
            Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
            Buffer.BlockCopy(mac, 0, result, ciphertext.Length, TAG_LEN);
            return result;
        }

        private static byte[] DecryptFallback(byte[] body, byte[] key, byte[] nonce)
        {
            if (body.Length < TAG_LEN)
                throw new CryptographicException("Invalid ciphertext length");

            // Split into ciphertext and MAC
            int ctLen = body.Length - TAG_LEN;
            byte[] ciphertext = new byte[ctLen];
            byte[] receivedMac = new byte[TAG_LEN];
            Buffer.BlockCopy(body, 0, ciphertext, 0, ctLen);
            Buffer.BlockCopy(body, ctLen, receivedMac, 0, TAG_LEN);

            // Derive HMAC key
            byte[] hmacKey = new byte[32];
            using (var sha = SHA256.Create())
            {
                byte[] temp = sha.ComputeHash(nonce.Concat(key).ToArray());
                Buffer.BlockCopy(temp, 0, hmacKey, 0, 32);
            }

            // Verify HMAC (constant-time comparison)
            byte[] computedMac;
            using (var hmac = new HMACSHA256(hmacKey))
            {
                computedMac = hmac.ComputeHash(ciphertext);
            }

            bool macValid = true;
            for (int i = 0; i < TAG_LEN; i++)
            {
                macValid &= (receivedMac[i] == computedMac[i]);
            }

            if (!macValid)
                throw new CryptographicException("MAC verification failed");

            // Decrypt with AES-256-CBC
            byte[] plaintext;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = key;
                aes.IV = new byte[16];
                Buffer.BlockCopy(nonce, 0, aes.IV, 0, Math.Min(nonce.Length, 16));
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(ciphertext))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var output = new MemoryStream())
                {
                    cs.CopyTo(output);
                    plaintext = output.ToArray();
                }
            }

            return plaintext;
        }

        /*────────────────────────── Encrypt ──────────────────────────*/
        public static byte[] Encrypt(byte[] plain, byte[] key, Span<byte> header)
        {
            byte[] nonce  = new byte[NONCE_LEN];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(nonce);

#if BOUNCYCASTLE && (UNITY_WEBGL || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
            /* —— Bouncy Castle path —— */
            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(true, new AeadParameters(new KeyParameter(key),
                                                 TAG_LEN * 8, nonce));

            byte[] ciphertag = new byte[cipher.GetOutputSize(plain.Length)];
            int len = cipher.ProcessBytes(plain, 0, plain.Length, ciphertag, 0);
            cipher.DoFinal(ciphertag, len);                 // appends 16-byte tag
#else                
            /* —— Try built-in AesGcm first, fallback to AES-CBC + HMAC —— */
            byte[] ciphertag;
            try
            {
                byte[] cipher   = new byte[plain.Length];
                byte[] tag      = new byte[TAG_LEN];
                using var aes   = new AesGcm(key);
                aes.Encrypt(nonce, plain, cipher, tag);

                ciphertag = new byte[cipher.Length + tag.Length];
                Buffer.BlockCopy(cipher, 0,             ciphertag, 0,           cipher.Length);
                Buffer.BlockCopy(tag,    0,             ciphertag, cipher.Length, tag.Length);
            }
            catch (PlatformNotSupportedException)
            {
                // Fallback: AES-256-CBC + HMAC-SHA256 (Encrypt-then-MAC)
                ciphertag = EncryptFallback(plain, key, nonce);
            }
#endif

            // ─ pack:  [ header | nonce | cipher || tag ]
            using var ms = new MemoryStream(header.Length + NONCE_LEN + ciphertag.Length);
            ms.Write(header);
            ms.Write(nonce);
            ms.Write(ciphertag);
            return ms.ToArray();
        }

        /*────────────────────────── Decrypt ──────────────────────────*/
        public static byte[] Decrypt(ReadOnlySpan<byte> blob, byte[] key, int headerLen)
        {
            ReadOnlySpan<byte> nonce = blob.Slice(headerLen, NONCE_LEN);
            ReadOnlySpan<byte> body  = blob.Slice(headerLen + NONCE_LEN);

#if BOUNCYCASTLE && (UNITY_WEBGL || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(false, new AeadParameters(new KeyParameter(key),
                                                  TAG_LEN * 8, nonce.ToArray()));

            byte[] plain = new byte[cipher.GetOutputSize(body.Length)];
            int len = cipher.ProcessBytes(body.ToArray(), 0, body.Length, plain, 0);
            cipher.DoFinal(plain, len);
            return plain;
#else
            /* —— Try built-in AesGcm first, fallback to AES-CBC + HMAC —— */
            try
            {
                ReadOnlySpan<byte> ct   = body[..^TAG_LEN];
                ReadOnlySpan<byte> tag  = body[^TAG_LEN..];

                byte[] plain = new byte[ct.Length];
                using var aes = new AesGcm(key);
                aes.Decrypt(nonce, ct, tag, plain);
                return plain;
            }
            catch (PlatformNotSupportedException)
            {
                // Fallback: AES-256-CBC + HMAC-SHA256
                return DecryptFallback(body.ToArray(), key, nonce.ToArray());
            }
#endif
        }
        
        /// <summary>
        /// Decrypts <paramref name="blob"/> and returns <c>true</c> on success.
        /// Any MAC failure (wrong key or tampering) is caught and returns <c>false</c>.
        /// </summary>
        public static bool TryDecrypt(ReadOnlySpan<byte> blob,
            byte[]              key,
            int                 headerLen,
            out byte[]          plain)
        {
            /*────────────────  new safety guard  ────────────────*/
            plain = null;                        // default

            // Key must exist and be the expected 32-byte length (AES-256).
            if (key == null || key.Length != 32)
            {
                Logger.Log("Encryption key is null or has invalid length – cannot decrypt.",
                    LogLevel.Error);
                return false;
            }

            /*────────────────  original flow  ───────────────────*/
            try
            {
                plain = Decrypt(blob, key, headerLen);   // existing method
                return true;
            }
#if BOUNCYCASTLE && (UNITY_WEBGL || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
            catch (InvalidCipherTextException)           // Bouncy Castle
#else
    catch (CryptographicException)               // AesGcm on desktop
#endif
            {
                plain = null;
                return false;
            }
        }
    }
}
#endif
