using System;
using System.IO;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Moves existing data from the previous persistent path to the new one.
    /// </summary>
    public static class PersistentPathMigration
    {
        const string MarkerFile = ".migrated";

        public static void TryMigrate(string oldPath, string newPath)
        {
            try
            {
                if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                    return;
                if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                    return;
                if (!Directory.Exists(oldPath))
                    return;
                string marker = Path.Combine(newPath, MarkerFile);
                if (File.Exists(marker))
                    return;

                // Ensure the parent directory of newPath exists before migration
                string parentDir = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                Directory.CreateDirectory(newPath);
                string temp = newPath + "_tmp";
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
                
                // Ensure temp directory's parent exists as well
                string tempParent = Path.GetDirectoryName(temp);
                if (!string.IsNullOrEmpty(tempParent) && !Directory.Exists(tempParent))
                {
                    Directory.CreateDirectory(tempParent);
                }
                
                CopyRecursive(oldPath, temp);
                if (Directory.Exists(newPath)) Directory.Delete(newPath, true);
                Directory.Move(temp, newPath);
                File.WriteAllText(marker, DateTime.UtcNow.ToString("o"));
                try
                {
                    Directory.Delete(oldPath, true);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Logger.Log($"PersistentPathMigration failed: {ex.Message}", LogLevel.Warning);
            }
        }

        static void CopyRecursive(string sourceDir, string destDir)
        {
            // Ensure destination directory exists
            Directory.CreateDirectory(destDir);
            
            foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(sourceDir, destDir));
            }
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string target = file.Replace(sourceDir, destDir);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }
    }
}
