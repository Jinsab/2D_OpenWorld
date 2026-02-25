using System.IO;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Returns <see cref="Application.persistentDataPath"/> on all platforms.
    /// </summary>
    public class DefaultStoragePathProvider : IStoragePathProvider
    {
        public string GetRootPath()
        {
            string path = Application.persistentDataPath;
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
