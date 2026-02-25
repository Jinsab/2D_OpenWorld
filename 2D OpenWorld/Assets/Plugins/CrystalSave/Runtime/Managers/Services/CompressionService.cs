#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    public class CompressionService
    {
        readonly SaveSettings settings;
        public bool UseCompression { get; private set; }

        public CompressionService(SaveSettings settings)
        {
            this.settings = settings;
        }

        public Task InitializeAsync()
        {
            UseCompression = settings.enableCompression;
            return Task.CompletedTask;
        }

        public byte[] MaybeCompress(byte[] plain)
        {
            if (!UseCompression || plain == null || plain.Length == 0)
                return plain;

            using var ms = new MemoryStream();
            Span<byte> header = stackalloc byte[5];
            header[0] = (byte)'C';
            header[1] = (byte)'S';
            header[2] = (byte)'C';
            header[3] = (byte)'M';
            header[4] = 1;
            ms.Write(header);
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                gz.Write(plain, 0, plain.Length);
            return ms.ToArray();
        }

        public byte[] MaybeDecompress(byte[] blob)
        {
            bool looksCompressed = blob != null &&
                                    blob.Length > 5 &&
                                    blob[0] == (byte)'C' &&
                                    blob[1] == (byte)'S' &&
                                    blob[2] == (byte)'C' &&
                                    blob[3] == (byte)'M';
            if (!looksCompressed)
                return blob;

            using var input = new MemoryStream(blob, 5, blob.Length - 5);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return output.ToArray();
        }
    }
}
#endif
