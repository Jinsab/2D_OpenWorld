#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Editor
{
    public static class FixDefineSymbolsUtility
    {
        private static readonly string[] SymbolsToRemove = new[]
        {
            "REMEMBERME_STANDARD_PRESENT",
            "REMEMBERME_URP_PRESENT",
            "REMEMBERME_HDRP_PRESENT",
            "MEMORYPACK",
            "ARAWN_REMEMBERME",
            "REMEMBERME_GC2CORE_PRESENT",
            "REMEMBERME_GC2DIALOGUE_PRESENT",
            "REMEMBERME_GC2INVENTORY_PRESENT",
            "REMEMBERME_GC2QUESTS_PRESENT",
            "REMEMBERME_GC2STATS_PRESENT",
            "REMEMBERME_GC2MELEE_PRESENT",
            "REMEMBERME_GC2SHOOTER_PRESENT",
            "REMEMBERME_GC2MODULE_PRESENT",
            "REMEMBERME_CLOUDSAVE_PRESENT",
            "REMEMBERME_AUTHENTICATION_PRESENT",
            "REMEMBERME_CORESERVICES_PRESENT",
            "REMEMBERME_LOCALIZATION_PRESENT",
            "REMEMBERME_NVIDIA_DLSS_PRESENT",
            "REMEMBERME_GOOGLEPLAY_PRESENT",
            "REMEMBERME_APPLE_SIGNIN_PRESENT",
            "REMEMBERME_FACEBOOK_SDK_PRESENT",
            "REMEMBERME_STEAMWORKS_PRESENT",
            "REMEMBERME_EDITOR_COROUTINES_PRESENT",
            "REMEMBERME_NANINOVEL_PRESENT"
        };

        private static readonly BuildTargetGroup[] TargetGroups =
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.WebGL,
            BuildTargetGroup.PS5,
            BuildTargetGroup.XboxOne,
            BuildTargetGroup.Switch
        };

        [MenuItem("Tools/Crystal Save/Project/Reset Scripting Define Symbols")]
        public static void FixDefineSymbols()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Fix Scripting Define Symbols",
                "This will remove all Crystal Save–related scripting define symbols across all platforms.\n\nProceed?",
                "Yes, remove them",
                "Cancel"
            );

            if (!confirm) return;

            foreach (BuildTargetGroup group in TargetGroups)
            {
                NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                List<string> symbolList = new(currentDefines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                bool changed = false;

                foreach (string symbol in SymbolsToRemove)
                {
                    if (symbolList.Remove(symbol))
                        changed = true;
                }

                if (changed)
                {
                    string newDefines = string.Join(";", symbolList);
                    PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
                    Debug.Log($"[Crystal Save] Removed scripting symbols for {group}");
                }
            }

            EditorUtility.DisplayDialog("Done", "All Crystal Save–related symbols have been removed.", "OK");
        }
    }
}
#endif
