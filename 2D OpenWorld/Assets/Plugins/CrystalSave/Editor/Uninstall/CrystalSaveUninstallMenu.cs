#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Arawn.CrystalSave.Editor
{
    internal static class CrystalSaveUninstallMenu
    {
        private const string kMenu         = "Tools/Crystal Save/Project/Uninstall Crystal Save";
        private const string kWorkerFile   = "CrystalSaveUninstaller.cs";
        private const string kAsmdefFile   = "CrystalSave.Uninstall.Editor.asmdef";
        private const string kEditorFolder = "Assets/Editor";
        private const string kPendingKey   = "CRYSTALSAVE_UNINSTALL_PENDING";   // SessionState key

        /*────────────────────────────── MENU ITEM ───────────────────────*/
        [MenuItem(kMenu, priority = 2000)]
        private static void UninstallCrystalSave()
        {
            /* ── 1st dialog ─ high-level confirmation */
            if (!EditorUtility.DisplayDialog(
                    "Uninstall Crystal Save",
                    "This will REMOVE Crystal Save, all of its components, "
                  + "define symbols and folders from this project.\n\n"
                  + "Operation is destructive. Continue?",
                    "Yes – uninstall", "Cancel"))
                return;

            /* ── 2nd dialog ─ process-time warning */
            bool acknowledged = EditorUtility.DisplayDialog(
                "Please read before continuing",
                "The uninstall scans every prefab and scene. On large projects "
              + "this can take several minutes.\n\n"
              + "During the process the Unity Editor may appear frozen or "
              + "unresponsive. Do NOT click in the Editor window until the "
              + "uninstallation is complete.",
                "OK – start uninstall", "Cancel");

            if (!acknowledged) return;   // user aborted here

            /* 1 ─ Ensure Assets/Editor exists */
            if (!AssetDatabase.IsValidFolder(kEditorFolder))
                AssetDatabase.CreateFolder("Assets", "Editor");

            /* 2 ─ Locate the worker script */
            string srcScript = AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(kWorkerFile)} t:script")
                                            .Select(AssetDatabase.GUIDToAssetPath)
                                            .FirstOrDefault();
            if (string.IsNullOrEmpty(srcScript))
            {
                EditorUtility.DisplayDialog("File missing",
                    $"Could not locate {kWorkerFile}.", "OK");
                return;
            }

            /* 3 ─ Prepare destination paths */
            string dstScript   = Path.Combine(kEditorFolder, kWorkerFile).Replace("\\", "/");
            string srcAsmdef   = Path.Combine(Path.GetDirectoryName(srcScript), kAsmdefFile).Replace("\\", "/");
            string dstAsmdef   = Path.Combine(kEditorFolder, kAsmdefFile).Replace("\\", "/");

            /* 4 ─ Move script if needed */
            if (srcScript != dstScript)
            {
                string msg = AssetDatabase.MoveAsset(srcScript, dstScript);
                if (!string.IsNullOrEmpty(msg))
                {
                    Debug.LogError("CrystalSave Uninstall (script move): " + msg);
                    return;
                }
            }

            /* 5 ─ Move asmdef if it exists and isn’t already in place */
            if (File.Exists(srcAsmdef) && srcAsmdef != dstAsmdef)
            {
                string msg = AssetDatabase.MoveAsset(srcAsmdef, dstAsmdef);
                if (!string.IsNullOrEmpty(msg))
                {
                    Debug.LogError("CrystalSave Uninstall (asmdef move): " + msg);
                    return;
                }
            }

            /* 6 ─ Flag pending uninstall and trigger recompile */
            SessionState.SetBool(kPendingKey, true);
            AssetDatabase.Refresh();   // compiles → domain reload
        }

        /*────────────────── domain-reload runner ───────────────────────*/
        [InitializeOnLoad]
        private static class UninstallRunner
        {
            static UninstallRunner()
            {
                if (!SessionState.GetBool(kPendingKey, false))
                    return;                           // nothing queued

                SessionState.EraseBool(kPendingKey); // consume flag
                TryInvokeUninstaller();
            }
        }

        /*────────────────── reflection helper ─────────────────────────*/
        private static void TryInvokeUninstaller()
        {
            const string wanted = "Arawn.CrystalSave.Editor.CrystalSaveUninstaller";

            Type worker = AppDomain.CurrentDomain.GetAssemblies()
                                 .Select(a => a.GetType(wanted, false))
                                 .FirstOrDefault(t => t != null);

            if (worker == null)
            {
                Debug.LogError("CrystalSave Uninstall: worker type not found after compile.");
                return;
            }

            MethodInfo run = worker.GetMethod("Run",
                                BindingFlags.Public | BindingFlags.Static);
            if (run == null)
            {
                Debug.LogError("CrystalSave Uninstall: Run() not found.");
                return;
            }

            run.Invoke(null, null);
        }
    }
}
#endif
