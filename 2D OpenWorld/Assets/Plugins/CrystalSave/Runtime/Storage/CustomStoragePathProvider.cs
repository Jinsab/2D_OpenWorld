using System;
using System.IO;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Provides a customizable persistent root path. On WebGL builds the root is
    /// fixed to <c>/idbfs</c> and combined with a folder name. On other platforms
    /// the path nests under <see cref="Application.persistentDataPath"/> unless an
    /// override is provided.
    /// </summary>
    public class CustomStoragePathProvider : IStoragePathProvider
    {
        readonly string folderName;
        readonly string overrideRoot;

        public CustomStoragePathProvider(string fixedFolderName, string overrideRoot = "")
        {
            folderName = string.IsNullOrWhiteSpace(fixedFolderName)
                ? "CrystalSave" : fixedFolderName.Trim();
            this.overrideRoot = overrideRoot;
        }

        public string GetRootPath()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string root = "/idbfs";
            string path = Path.Combine(root, folderName);
            Directory.CreateDirectory(path);
            return path.Replace('\\', '/');
#else
            string root = Application.persistentDataPath;

            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                try
                {
                    string expanded = Environment.ExpandEnvironmentVariables(overrideRoot);

                    if (Path.IsPathRooted(expanded))
                    {
                        string normalized = Path.GetFullPath(expanded);
                        string customPath = Path.Combine(normalized, folderName);
                        Directory.CreateDirectory(customPath);
                        return customPath;
                    }

                    Logger.Log($"Custom root path '{overrideRoot}' is not absolute and was ignored.", LogLevel.Warning);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to apply custom root path '{overrideRoot}': {e.Message}", LogLevel.Warning);
                }
            }

            string path = Path.Combine(root, folderName);
            Directory.CreateDirectory(path);
            return path;
#endif
        }
    }

    /// <summary>
    /// Helper for syncing the WebGL file system.
    /// </summary>
    public static class WebGLFS
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_2023_1_OR_NEWER
        [DllImport("__Internal")] private static extern void SyncFS();
#endif
        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_2023_1_OR_NEWER
            SyncFS();
#endif
        }
    }
}
