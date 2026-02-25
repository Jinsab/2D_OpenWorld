#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    internal static class WebGLFileSystem
    {
        [DllImport("__Internal")] private static extern void CrystalSave_WriteFile(string path, byte[] data, int length);
        [DllImport("__Internal")] private static extern void CrystalSave_WriteText(string path, string text);
        [DllImport("__Internal")] private static extern int CrystalSave_ReadFile(string path, out IntPtr buffer);
        [DllImport("__Internal")] private static extern int CrystalSave_ReadText(string path, out IntPtr buffer);
        [DllImport("__Internal")] private static extern int CrystalSave_FileExists(string path);
        [DllImport("__Internal")] private static extern void CrystalSave_DeleteFile(string path);
        [DllImport("__Internal")] private static extern void CrystalSave_InitFs();
#if !UNITY_2023_1_OR_NEWER
        [DllImport("__Internal")] private static extern void CrystalSave_SyncFs(bool populate);
        [DllImport("__Internal")] private static extern int CrystalSave_IsSyncing();
#endif
        [DllImport("__Internal")] private static extern void CrystalSave_Free(IntPtr ptr);

        public static void WriteAllBytes(string path, byte[] data)
            => CrystalSave_WriteFile(path, data, data.Length);

        public static void WriteAllText(string path, string text)
            => CrystalSave_WriteText(path, text);

        public static byte[] ReadAllBytes(string path)
        {
            int len = CrystalSave_ReadFile(path, out var ptr);
            if (len <= 0) return null;
            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, len);
            CrystalSave_Free(ptr);
            return bytes;
        }

        public static string ReadAllText(string path)
        {
            int len = CrystalSave_ReadText(path, out var ptr);
            if (len <= 0) return null;
            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, len);
            CrystalSave_Free(ptr);
            return Encoding.UTF8.GetString(bytes);
        }

        public static bool Exists(string path)
            => CrystalSave_FileExists(path) == 1;

        public static void Delete(string path)
            => CrystalSave_DeleteFile(path);

        public static void Init()
            => CrystalSave_InitFs();

#if !UNITY_2023_1_OR_NEWER
        public static void Sync(bool populate)
            => CrystalSave_SyncFs(populate);

        public static async Task SyncAsync(bool populate)
        {
            CrystalSave_SyncFs(populate);
            while (CrystalSave_IsSyncing() == 1)
                await Task.Yield();
        }
#endif
    }
}
#endif
