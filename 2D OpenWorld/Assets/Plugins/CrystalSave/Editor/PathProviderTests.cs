#if MEMORYPACK && ARAWN_REMEMBERME
using NUnit.Framework;
using System.IO;
using UnityEngine;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.EditorTests
{
    public class PathProviderTests
    {
        [Test]
        public void DefaultProviderReturnsPersistentDataPath()
        {
            var prov = new DefaultStoragePathProvider();
            Assert.AreEqual(Application.persistentDataPath, prov.GetRootPath());
        }

        [Test]
        public void CustomProviderDefaultsUnderPersistentPath()
        {
            var prov = new CustomStoragePathProvider("TestFolder");
            string expected = Path.Combine(Application.persistentDataPath, "TestFolder");
            Assert.AreEqual(expected, prov.GetRootPath());
        }

        [Test]
        public void CustomProviderUsesOverrideRoot()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cs_override");
            var prov = new CustomStoragePathProvider("Folder", tmp);
            string expected = Path.Combine(tmp, "Folder");
            Assert.AreEqual(expected, prov.GetRootPath());
            Directory.Delete(tmp, true);
        }
    }
}
#endif
