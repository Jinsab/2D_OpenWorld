#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Text;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Importer for external save-slot metadata. Supports two formats:
    /// - MemoryPack-serialized SaveSlot (optionally encrypted with CSAV header)
    /// - Supabase JSON metadata (.meta.json) with a minimal DTO
    /// </summary>
    public static class SaveMetadataImporter
    {
        /// <summary>
        /// Attempts to parse external metadata bytes into a SaveSlot.
        /// Handles optional CSAV-encrypted payloads when an EncryptionService is provided.
        /// </summary>
        /// <param name="blob">Input bytes (encrypted MemoryPack, plain MemoryPack, or JSON).</param>
        /// <param name="slot">Parsed SaveSlot if successful; null otherwise.</param>
        /// <param name="enc">Optional encryption service (used when blob has CSAV header).</param>
        /// <returns>True on success.</returns>
        public static bool TryParse(byte[] blob, out SaveSlot slot, EncryptionService enc = null)
        {
            slot = null;
            if (blob == null || blob.Length == 0) return false;

            // 1) If looks like JSON, try JSON first.
            if (LooksLikeJson(blob))
            {
                if (TryParseJson(blob, out slot)) return true;
                // fallthrough to MemoryPack probe
            }

            // 2) If looks like CSAV-encrypted, try to decrypt first if possible
            if (LooksEncrypted(blob))
            {
                if (enc == null || !enc.UseEncryption)
                {
                    Logger.Log("SaveMetadataImporter: Payload looks encrypted but encryption is unavailable.", LogLevel.Warning);
                    return false;
                }

                var plain = enc.MaybeDecrypt(blob);
                if (plain == null || plain.Length == 0) return false;

                // After decrypt, it should be MemoryPack bytes (expected format for metadata cache)
                try
                {
                    slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(plain);
                    if (slot != null) EnsureCustomMetadata(slot);
                    return slot != null;
                }
                catch (Exception ex)
                {
                    Logger.Log($"SaveMetadataImporter: Failed to deserialize decrypted metadata: {ex.Message}", LogLevel.Warning);
                    return false;
                }
            }

            // 3) Try MemoryPack directly
            try
            {
                slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(blob);
                if (slot != null) EnsureCustomMetadata(slot);
                if (slot != null) return true;
            }
            catch { /* ignore and try JSON next */ }

            // 4) Finally, try JSON (some providers store plaintext JSON locally)
            return TryParseJson(blob, out slot);
        }

        /// <summary>
        /// Attempts to parse from a base64 string (commonly used by cloud providers or copy/paste flows).
        /// Tries base64 decode first; if that fails, treats the string as JSON text.
        /// </summary>
        public static bool TryParseFromBase64OrJson(string base64OrJson, out SaveSlot slot, EncryptionService enc = null)
        {
            slot = null;
            if (string.IsNullOrWhiteSpace(base64OrJson)) return false;

            // Attempt base64 decode
            try
            {
                byte[] bytes = Convert.FromBase64String(base64OrJson.Trim());
                return TryParse(bytes, out slot, enc);
            }
            catch
            {
                // Not base64 – treat as JSON text
                byte[] bytes = Encoding.UTF8.GetBytes(base64OrJson);
                if (TryParseJson(bytes, out slot)) return true;
                return false;
            }
        }

        private static bool TryParseJson(byte[] bytes, out SaveSlot slot)
        {
            slot = null;
            try
            {
                string json = Encoding.UTF8.GetString(bytes);
                var dto = JsonUtility.FromJson<SaveSlotJsonDTO>(json);
                if (dto == null) return false;

                // Map into SaveSlot
                var s = new SaveSlot
                {
                    SlotNumber = dto.SlotNumber,
                    SlotName = dto.SlotName,
                    ScreenshotFileName = dto.ScreenshotFileName,
                    LastActiveScene = dto.LastActiveScene,
                    LastSaved = dto.LastSavedTicks != 0 ? new DateTime(dto.LastSavedTicks, DateTimeKind.Utc) : default
                };
                EnsureCustomMetadata(s);
                slot = s;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"SaveMetadataImporter: JSON parse failed: {ex.Message}", LogLevel.Info);
                slot = null;
                return false;
            }
        }

        private static bool LooksEncrypted(byte[] bytes)
        {
            return bytes.Length > 8 && bytes[0] == (byte)'C' && bytes[1] == (byte)'S' && bytes[2] == (byte)'A' && bytes[3] == (byte)'V';
        }

        private static bool LooksLikeJson(byte[] bytes)
        {
            // Trim leading whitespace and check starting char
            int i = 0;
            while (i < bytes.Length && (bytes[i] == ' ' || bytes[i] == '\n' || bytes[i] == '\t' || bytes[i] == '\r')) i++;
            if (i >= bytes.Length) return false;
            byte b = bytes[i];
            return b == (byte)'{' || b == (byte)'[';
        }

        private static void EnsureCustomMetadata(SaveSlot s)
        {
            if (s.CustomMetadata == null) s.CustomMetadata = new System.Collections.Generic.Dictionary<string, string>();
        }

        // Matches SupabaseSaveSystem's JSON schema (pure fields, no CustomMetadata)
        [Serializable]
        private class SaveSlotJsonDTO
        {
            public int    SlotNumber;
            public string SlotName;
            public string ScreenshotFileName;
            public string LastActiveScene;
            public long   LastSavedTicks;
        }
    }
}
#endif
