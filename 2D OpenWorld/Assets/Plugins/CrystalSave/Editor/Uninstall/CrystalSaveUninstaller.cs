#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;      // reflection for NamedBuildTarget.Editor

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// One-shot, destructive uninstaller for Crystal Save.
    /// Runs ONLY when CrystalSaveUninstallMenu calls <see cref="Run"/>.
    /// </summary>
    internal static class CrystalSaveUninstaller
    {
        /*──────── CONFIG ───────────────────────────────────────────────*/
        private const string kDefineSymbolsFolder = "Assets/Plugins/CrystalSave/Editor/DefineSymbols";
        private const string kCrystalSaveRoot     = "Assets/Plugins/CrystalSave";
        private const string kAsmdefName          = "CrystalSave.Uninstall.Editor.asmdef";

        private static readonly string[] kSymbolsToRemove =
        {
            "REMEMBERME_STANDARD_PRESENT","REMEMBERME_URP_PRESENT","REMEMBERME_HDRP_PRESENT",
            "MEMORYPACK","BOUNCYCASTLE","ARAWN_REMEMBERME","REMEMBERME_GC2CORE_PRESENT","REMEMBERME_GC2DIALOGUE_PRESENT",
            "REMEMBERME_GC2INVENTORY_PRESENT","REMEMBERME_GC2QUESTS_PRESENT","REMEMBERME_GC2STATS_PRESENT",
            "REMEMBERME_GC2MELEE_PRESENT","REMEMBERME_GC2SHOOTER_PRESENT","REMEMBERME_GC2MAILBOX_PRESENT","REMEMBERME_GC2MODULE_PRESENT",
            "REMEMBERME_CLOUDSAVE_PRESENT","REMEMBERME_AUTHENTICATION_PRESENT","REMEMBERME_CORESERVICES_PRESENT",
            "REMEMBERME_LOCALIZATION_PRESENT","REMEMBERME_NVIDIA_DLSS_PRESENT","REMEMBERME_GOOGLEPLAY_PRESENT",
            "REMEMBERME_APPLE_SIGNIN_PRESENT","REMEMBERME_FACEBOOK_SDK_PRESENT","REMEMBERME_STEAMWORKS_PRESENT",
            "REMEMBERME_EDITOR_COROUTINES_PRESENT","REMEMBERME_ADDRESSABLES_PRESENT","REMEMBERME_NGO_PRESENT",
            "NANINOVEL","REMEMBERME_NANINOVEL_PRESENT"
        };

        private static readonly BuildTargetGroup[] kGroups =
        {
            BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS,
            BuildTargetGroup.WebGL,      BuildTargetGroup.PS5,     BuildTargetGroup.XboxOne,
            BuildTargetGroup.Switch
        };

        /*──────── ENTRY ───────────────────────────────────────────────*/
        public static void Run()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                DeleteDefineFolder();      // ① remove helper scripts
                StripComponents();         // ② wipe scenes & prefabs
                DeleteCrystalSaveRoot();   // ③ delete CrystalSave files
                RemoveDefineSymbols();     // ④ clear scripting-define symbols
            }
            catch (Exception ex)
            {
                Debug.LogError($"CrystalSave Uninstaller: {ex}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog(
                "Crystal Save Uninstalled",
                "Crystal Save and all its data have been removed from this project.",
                "OK");

            SelfDestruct();
        }

        /*──────── Phase 1 – Delete DefineSymbols ───────────────────────*/
        private static void DeleteDefineFolder()
        {
            if (AssetDatabase.IsValidFolder(kDefineSymbolsFolder))
                AssetDatabase.DeleteAsset(kDefineSymbolsFolder);
        }

        /*──────── Phase 2 – Strip components ──────────────────────────*/
        private static void StripComponents()
        {
            var crystalTypes = GetCrystalSaveComponentTypes();
            int total = 0;

            /* Prefabs */
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                total += StripFromGameObject(prefab, crystalTypes);
                if (total > 0) EditorUtility.SetDirty(prefab);
            }

            /* Scenes (skip read-only) */
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsOpenForEdit(scenePath, StatusQueryOptions.UseCachedIfPossible))
                    continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    total += StripFromGameObject(root, crystalTypes);

                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"CrystalSave Uninstaller: removed {total} components.");
        }

        private static IEnumerable<Type> GetCrystalSaveComponentTypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(Component).IsAssignableFrom(t) &&
                            t.Namespace?.StartsWith("Arawn.CrystalSave") == true);

        private static int StripFromGameObject(GameObject go, IEnumerable<Type> types)
        {
            int removed = 0;
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                if (types.Contains(comp.GetType()))
                {
                    Undo.DestroyObjectImmediate(comp);
                    removed++;
                }
            }
            return removed;
        }

        /*──────── Phase 3 – Delete root folder ───────────────────────*/
        private static void DeleteCrystalSaveRoot()
        {
            if (AssetDatabase.IsValidFolder(kCrystalSaveRoot))
                DeleteFolderRecursively(kCrystalSaveRoot);

            if (Directory.Exists(kCrystalSaveRoot))
            {
                FileUtil.DeleteFileOrDirectory(kCrystalSaveRoot);
                FileUtil.DeleteFileOrDirectory(kCrystalSaveRoot + ".meta");
            }
        }

        private static void DeleteFolderRecursively(string folderAssetPath)
        {
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { folderAssetPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != folderAssetPath) AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.DeleteAsset(folderAssetPath);
        }

        /*──────── Phase 4 – Remove define symbols ─────────────────────*/
        private static void RemoveDefineSymbols()
        {
            /* Player / platform targets */
            foreach (var group in kGroups)
                ClearSymbolsOnTarget(NamedBuildTarget.FromBuildTargetGroup(group));

            /* Editor target (if supported) */
            if (TryGetEditorNamedBuildTarget(out var editorNBT))
                ClearSymbolsOnTarget(editorNBT);
        }

        private static void ClearSymbolsOnTarget(NamedBuildTarget nbt)
        {
            string current = PlayerSettings.GetScriptingDefineSymbols(nbt);
            var list = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (list.RemoveAll(s => kSymbolsToRemove.Contains(s)) == 0) return;
            PlayerSettings.SetScriptingDefineSymbols(nbt, string.Join(";", list));
        }

        private static bool TryGetEditorNamedBuildTarget(out NamedBuildTarget editorNBT)
        {
            FieldInfo field = typeof(NamedBuildTarget).GetField("Editor",
                               BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(NamedBuildTarget))
            {
                editorNBT = (NamedBuildTarget)field.GetValue(null);
                return true;
            }
            editorNBT = default;
            return false;
        }

        /*──────── Final – Self-destruct ───────────────────────────────*/
        private static void SelfDestruct()
        {
            /* delete this script */
            foreach (string guid in AssetDatabase.FindAssets($"{nameof(CrystalSaveUninstaller)} t:script"))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            /* delete the asmdef that travelled with it */
            foreach (string guid in AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(kAsmdefName)} t:asmdef"))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            AssetDatabase.Refresh();
        }
    }
}
#endif
