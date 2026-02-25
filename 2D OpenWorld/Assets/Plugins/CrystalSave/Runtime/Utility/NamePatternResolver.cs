#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Arawn.CrystalSave.Runtime
{
    public static class NamePatternResolver
    {
        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }

        // Replace {n} and {meta:key} placeholders using values from the given SaveSlot
        public static string Resolve(string pattern, SaveSlot slot)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;
            if (slot == null) return Sanitize(pattern);

            string resolved = pattern.Replace("{n}", slot.SlotNumber.ToString());

            // {meta:key}
            resolved = Regex.Replace(resolved, "\\{meta:([^}]+)\\}", m =>
            {
                string key = m.Groups[1].Value;
                string val = null;
                if (slot.CustomMetadata != null && slot.CustomMetadata.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                    val = v;
                return Sanitize(val ?? string.Empty);
            });

            return Sanitize(resolved);
        }

        // Build a glob (Directory.GetFiles) for a slot number. Replaces meta placeholders with *
        public static string ToGlob(string pattern, int slotNumber)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;
            string glob = pattern.Replace("{n}", slotNumber.ToString());
            glob = Regex.Replace(glob, "\\{meta:([^}]+)\\}", "*");
            return glob;
        }
    }
}
#endif
