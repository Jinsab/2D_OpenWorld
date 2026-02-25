#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Text;
using MemoryPack;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        /// <summary>
        /// Handles conversion of string payloads that were serialized before the MemoryPack union wrapper existed.
        /// Supports raw MemoryPack strings, JSON payloads and plain UTF8 text blobs.
        /// </summary>
        [Serializable]
        internal class LegacyStringWrapper : ILegacyConvertible<StringWrapper>, ILegacyConvertible<object>
        {
                [SerializeField] public string Value;
                [SerializeField] public string value;
                [SerializeField] public string RawValue;

                public StringWrapper ConvertToCurrent()
                {
                        return new StringWrapper(ExtractValue() ?? string.Empty);
                }

                object ILegacyConvertible<object>.ConvertToCurrent() => ConvertToCurrent();

                internal string ExtractValue()
                {
                        if (!string.IsNullOrEmpty(Value))
                        {
                                return SanitizeString(Value);
                        }

                        if (!string.IsNullOrEmpty(value))
                        {
                                return SanitizeString(value);
                        }

                        if (!string.IsNullOrEmpty(RawValue))
                        {
                                return SanitizeString(RawValue);
                        }

                        return string.Empty;
                }

                internal static bool TryConvert(byte[] serializedData, MemoryPackSerializerOptions options, out StringWrapper wrapper)
                {
                        wrapper = null;
                        if (serializedData == null || serializedData.Length == 0)
                        {
                                return false;
                        }

                        if (TryDeserializeStringWrapper(serializedData, options, out var directWrapper))
                        {
                                wrapper = directWrapper ?? new StringWrapper(string.Empty);
                                return true;
                        }

                        if (TryDeserializeMemoryPackString(serializedData, options, out var stringValue))
                        {
                                wrapper = new StringWrapper(stringValue ?? string.Empty);
                                return true;
                        }

                        if (TryParseJson(serializedData, out var legacyPayload))
                        {
                                wrapper = legacyPayload.ConvertToCurrent();
                                return true;
                        }

                        if (TryDecodePlainText(serializedData, out stringValue))
                        {
                                wrapper = new StringWrapper(stringValue ?? string.Empty);
                                return true;
                        }

                        return false;
                }

                private static bool TryDeserializeStringWrapper(byte[] serializedData, MemoryPackSerializerOptions options, out StringWrapper wrapper)
                {
                        wrapper = null;

                        if (serializedData == null || serializedData.Length == 0)
                        {
                                return false;
                        }

                        try
                        {
                                var candidate = MemoryPackSerializer.Deserialize<StringWrapper>(serializedData.AsSpan(), options);
                                if (candidate == null)
                                {
                                        return false;
                                }

                                var roundTrip = MemoryPackSerializer.Serialize(candidate, options);
                                if (roundTrip == null || !roundTrip.AsSpan().SequenceEqual(serializedData))
                                {
                                        return false;
                                }

                                if (candidate.Value == null)
                                {
                                        candidate.Value = string.Empty;
                                }

                                wrapper = candidate;
                                return true;
                        }
                        catch
                        {
                                wrapper = null;
                                return false;
                        }
                }

                private static bool TryDeserializeMemoryPackString(byte[] serializedData, MemoryPackSerializerOptions options, out string value)
                {
                        value = null;

                        if (serializedData == null || serializedData.Length == 0)
                        {
                                return false;
                        }

                        try
                        {
                                var candidate = MemoryPackSerializer.Deserialize<string>(serializedData.AsSpan(), options);
                                if (candidate == null)
                                {
                                        return false;
                                }

                                var roundTrip = MemoryPackSerializer.Serialize(candidate, options);
                                if (roundTrip == null || !roundTrip.AsSpan().SequenceEqual(serializedData))
                                {
                                        return false;
                                }

                                value = candidate;
                                return true;
                        }
                        catch
                        {
                                value = null;
                                return false;
                        }
                }

                private static bool TryParseJson(byte[] serializedData, out LegacyStringWrapper legacyWrapper)
                {
                        legacyWrapper = null;
                        if (!TryDecodeString(serializedData, out string json, out _))
                        {
                                return false;
                        }

                        if (string.IsNullOrWhiteSpace(json))
                        {
                                return false;
                        }

                        string trimmed = json.Trim();
                        if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                        {
                                return false;
                        }

                        try
                        {
                                legacyWrapper = JsonUtility.FromJson<LegacyStringWrapper>(trimmed);
                                if (legacyWrapper == null)
                                {
                                        return false;
                                }

                                string extracted = legacyWrapper.ExtractValue();
                                if (extracted == null)
                                {
                                        return false;
                                }

                                return true;
                        }
                        catch
                        {
                                legacyWrapper = null;
                                return false;
                        }
                }

                private static bool TryDecodePlainText(byte[] serializedData, out string value)
                {
                        value = null;
                        if (!TryDecodeString(serializedData, out string decoded, out var usedEncoding))
                        {
                                return false;
                        }

                        if (string.IsNullOrEmpty(decoded))
                        {
                                return false;
                        }

                        decoded = decoded.Replace("\0", string.Empty);

                        if (usedEncoding != null && usedEncoding.CodePage == Encoding.BigEndianUnicode.CodePage && TryRecoverInterleavedAscii(decoded, out var recovered))
                        {
                                decoded = recovered;
                        }

                        decoded = decoded.Trim();
                        decoded = SanitizeString(decoded);
                        if (decoded.Length == 0)
                        {
                                return false;
                        }

                        if (!IsLikelyReadableText(decoded))
                        {
                                return false;
                        }

                        if (decoded.Length >= 2 && decoded[0] == '\"' && decoded[^1] == '\"')
                        {
                                try
                                {
                                        var container = JsonUtility.FromJson<QuotedString>("{\"v\":" + decoded + "}");
                                        if (container != null && container.v != null)
                                        {
                                                decoded = container.v;
                                        }
                                }
                                catch
                                {
                                        // Ignore JSON parsing issues and fall back to the raw decoded value.
                                }
                        }

                        value = decoded;
                        return true;
                }

                private static string SanitizeString(string raw)
                {
                        if (string.IsNullOrEmpty(raw))
                        {
                                return raw;
                        }

                        int start = 0;
                        int end = raw.Length - 1;

                        while (start <= end && IsProblematicPrefixOrSuffix(raw[start]))
                        {
                                start++;
                        }

                        while (end >= start && IsProblematicPrefixOrSuffix(raw[end]))
                        {
                                end--;
                        }

                        if (start > end)
                        {
                                return string.Empty;
                        }

                        string trimmed = (start == 0 && end == raw.Length - 1)
                                ? raw
                                : raw.Substring(start, end - start + 1);

                        return StringWrapper.Cleanse(trimmed);
                }

                private static bool TryDecodeString(byte[] serializedData, out string decoded, out Encoding usedEncoding)
                {
                        decoded = null;
                        usedEncoding = null;

                        if (serializedData == null || serializedData.Length == 0)
                        {
                                return false;
                        }

                        if (HasUtf8Bom(serializedData))
                        {
                                if (TryDecodeWithEncoding(serializedData, Encoding.UTF8, out decoded, 3))
                                {
                                        usedEncoding = Encoding.UTF8;
                                        return true;
                                }

                                return false;
                        }

                        if (HasUtf16LittleEndianBom(serializedData))
                        {
                                if (TryDecodeWithEncoding(serializedData, Encoding.Unicode, out decoded, 2))
                                {
                                        usedEncoding = Encoding.Unicode;
                                        return true;
                                }

                                return false;
                        }

                        if (HasUtf16BigEndianBom(serializedData))
                        {
                                if (TryDecodeWithEncoding(serializedData, Encoding.BigEndianUnicode, out decoded, 2))
                                {
                                        usedEncoding = Encoding.BigEndianUnicode;
                                        return true;
                                }

                                return false;
                        }

                        if (TryDecodeWithEncoding(serializedData, Encoding.UTF8, out decoded))
                        {
                                usedEncoding = Encoding.UTF8;
                                return true;
                        }

                        if (TryDecodeWithEncoding(serializedData, Encoding.Unicode, out decoded))
                        {
                                usedEncoding = Encoding.Unicode;
                                return true;
                        }

                        if (LooksLikeUtf16BigEndian(serializedData) && TryDecodeWithEncoding(serializedData, Encoding.BigEndianUnicode, out decoded))
                        {
                                usedEncoding = Encoding.BigEndianUnicode;
                                return true;
                        }

                        if (TryDecodeWindows1252(serializedData, out decoded))
                        {
                                usedEncoding = GetWindows1252Encoding();
                                return true;
                        }

                        decoded = null;
                        return false;
                }

                private static bool TryDecodeWithEncoding(byte[] serializedData, Encoding encoding, out string decoded, int offset = 0, int count = -1)
                {
                        decoded = null;

                        if (serializedData == null || serializedData.Length == 0)
                        {
                                return false;
                        }

                        if (offset < 0 || offset >= serializedData.Length)
                        {
                                return false;
                        }

                        if (count < 0)
                        {
                                count = serializedData.Length - offset;
                        }

                        if (count <= 0)
                        {
                                return false;
                        }

                        try
                        {
                                Encoding strictEncoding = (Encoding)encoding.Clone();
                                strictEncoding.DecoderFallback = DecoderFallback.ExceptionFallback;
                                strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
                                string candidate = strictEncoding.GetString(serializedData, offset, count);
                                if (string.IsNullOrEmpty(candidate))
                                {
                                        return false;
                                }

                                decoded = candidate;
                                return true;
                        }
                        catch
                        {
                                decoded = null;
                                return false;
                        }
                }

                private static bool HasUtf8Bom(byte[] data)
                {
                        return data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF;
                }

                private static bool HasUtf16LittleEndianBom(byte[] data)
                {
                        return data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE;
                }

                private static bool HasUtf16BigEndianBom(byte[] data)
                {
                        return data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF;
                }

                private static bool LooksLikeUtf16BigEndian(byte[] data)
                {
                        if (data == null || data.Length < 2 || (data.Length & 1) != 0)
                        {
                                return false;
                        }

                        int zeroHighBytes = 0;
                        int pairCount = data.Length / 2;

                        for (int i = 0; i < data.Length; i += 2)
                        {
                                if (data[i] == 0)
                                {
                                        zeroHighBytes++;
                                }
                        }

                        return zeroHighBytes >= Math.Max(1, pairCount / 2);
                }

                private static bool TryRecoverInterleavedAscii(string input, out string recovered)
                {
                        recovered = input;

                        if (string.IsNullOrEmpty(input))
                        {
                                return false;
                        }

                        var builder = new StringBuilder(input.Length * 2);
                        int convertibleCharacters = 0;

                        foreach (char ch in input)
                        {
                                byte high = (byte)(ch >> 8);
                                byte low = (byte)(ch & 0xFF);

                                bool highAscii = IsLikelyAsciiByte(high);
                                bool lowAscii = IsLikelyAsciiByte(low);

                                if (highAscii && lowAscii)
                                {
                                        builder.Append((char)high);
                                        if (low != 0)
                                        {
                                                builder.Append((char)low);
                                        }
                                        convertibleCharacters++;
                                }
                                else if (highAscii && low == 0)
                                {
                                        builder.Append((char)high);
                                        convertibleCharacters++;
                                }
                                else if (lowAscii && high == 0)
                                {
                                        builder.Append((char)low);
                                        convertibleCharacters++;
                                }
                                else
                                {
                                        builder.Append(ch);
                                }
                        }

                        if (convertibleCharacters == 0)
                        {
                                return false;
                        }

                        double ratio = (double)convertibleCharacters / input.Length;
                        if (ratio < 0.6f)
                        {
                                return false;
                        }

                        recovered = builder.ToString();
                        return true;
                }

                private static bool IsLikelyAsciiByte(byte value)
                {
                        return value == 0x09 || value == 0x0A || value == 0x0D || (value >= 0x20 && value <= 0x7E);
                }

                private static Encoding GetWindows1252Encoding()
                {
                        try
                        {
                                return Encoding.GetEncoding(1252);
                        }
                        catch
                        {
                                try
                                {
                                        return Encoding.GetEncoding("iso-8859-1");
                                }
                                catch
                                {
                                        return Encoding.UTF8;
                                }
                        }
                }

                private static bool IsLikelyReadableText(string input)
                {
                        if (string.IsNullOrEmpty(input))
                        {
                                return false;
                        }

                        int printableCharacters = 0;
                        int controlCharacters = 0;

                        foreach (char c in input)
                        {
                                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                                {
                                        controlCharacters++;
                                        continue;
                                }

                                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c))
                                {
                                        printableCharacters++;
                                }
                        }

                        if (controlCharacters > Math.Max(1, input.Length / 10))
                        {
                                return false;
                        }

                        return printableCharacters >= Math.Max(1, (int)(input.Length * 0.6f));
                }

                private static bool TryDecodeWindows1252(byte[] serializedData, out string decoded)
                {
                        decoded = null;

                        if (serializedData == null || serializedData.Length == 0)
                        {
                                return false;
                        }

                        char[] chars = new char[serializedData.Length];

                        for (int i = 0; i < serializedData.Length; i++)
                        {
                                chars[i] = ConvertWindows1252(serializedData[i]);
                        }

                        decoded = new string(chars);
                        return !string.IsNullOrEmpty(decoded);
                }

                private static char ConvertWindows1252(byte value)
                {
                        switch (value)
                        {
                                case 0x80: return '\u20AC';
                                case 0x82: return '\u201A';
                                case 0x83: return '\u0192';
                                case 0x84: return '\u201E';
                                case 0x85: return '\u2026';
                                case 0x86: return '\u2020';
                                case 0x87: return '\u2021';
                                case 0x88: return '\u02C6';
                                case 0x89: return '\u2030';
                                case 0x8A: return '\u0160';
                                case 0x8B: return '\u2039';
                                case 0x8C: return '\u0152';
                                case 0x8E: return '\u017D';
                                case 0x91: return '\u2018';
                                case 0x92: return '\u2019';
                                case 0x93: return '\u201C';
                                case 0x94: return '\u201D';
                                case 0x95: return '\u2022';
                                case 0x96: return '\u2013';
                                case 0x97: return '\u2014';
                                case 0x98: return '\u02DC';
                                case 0x99: return '\u2122';
                                case 0x9A: return '\u0161';
                                case 0x9B: return '\u203A';
                                case 0x9C: return '\u0153';
                                case 0x9E: return '\u017E';
                                case 0x9F: return '\u0178';
                                default: return (char)value;
                        }
                }

                private static bool IsProblematicPrefixOrSuffix(char c)
                {
                        switch (c)
                        {
                                case '\ufeff': // UTF-8 BOM / zero-width no-break space
                                case '\u200b': // zero-width space
                                case '\u200c': // zero-width non-joiner
                                case '\u200d': // zero-width joiner
                                case '\u2060': // word joiner
                                        return true;
                                default:
                                        return false;
                        }
                }

                [Serializable]
                private class QuotedString
                {
                        public string v;
                }
        }
}
#endif
