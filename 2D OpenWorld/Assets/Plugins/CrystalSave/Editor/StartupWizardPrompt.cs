#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    static class StartupWizardPrompt
    {
        const string KeyPrefix = "CrystalSave_WizardShown_";

        [InitializeOnLoadMethod]
        static void CheckForSaveSettings()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            EditorApplication.delayCall -= Run;
            string key = KeyPrefix + Application.dataPath.Replace('/', '_').Replace('\\', '_');

            if (HasSaveSettings())
            {
                EditorPrefs.SetBool(key, true);
                return;
            }

            if (EditorPrefs.GetBool(key, false)) return;

            ShowWizard();
        }

        static bool HasSaveSettings()
        {
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:SaveSettings");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<SaveSettings>(path);
                    if (asset != null) return true;
                }
            }
            catch { }
            return false;
        }

        static void ShowWizard()
        {
            var wizType = Type.GetType("Arawn.CrystalSave.Editor.SettingsWizard");
            if (wizType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    wizType = asm.GetType("Arawn.CrystalSave.Editor.SettingsWizard");
                    if (wizType != null) break;
                }
            }
            var open = wizType?.GetMethod("OpenWizard", BindingFlags.Public | BindingFlags.Static);
            open?.Invoke(null, null);
        }
    }
}
#endif
