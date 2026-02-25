#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor.Build;
using System.Collections.Generic;
using System.IO;

namespace Arawn.CrystalSave.DefineSymbols.Editor
{
    [InitializeOnLoad]
    [DefaultExecutionOrder(100)]
    public static class DefineSymbols
    {
        // ───────────────────────────────────────────────────────────────
        //  CONSTANTS
        // ───────────────────────────────────────────────────────────────
        private const string STANDARD_SYMBOL             = "REMEMBERME_STANDARD_PRESENT";
        private const string URP_SYMBOL                  = "REMEMBERME_URP_PRESENT";
        private const string HDRP_SYMBOL                 = "REMEMBERME_HDRP_PRESENT";
        private const string MEMORYPACK_SYMBOL           = "MEMORYPACK";
        private const string BOUNCYCASTLE_SYMBOL         = "BOUNCYCASTLE";
        private const string CUSTOM_SYMBOL               = "ARAWN_REMEMBERME";
        private const string GC2CORE_SYMBOL              = "REMEMBERME_GC2CORE_PRESENT";
        private const string GC2DIALOGUE_SYMBOL          = "REMEMBERME_GC2DIALOGUE_PRESENT";
        private const string GC2INVENTORY_SYMBOL         = "REMEMBERME_GC2INVENTORY_PRESENT";
        private const string GC2QUESTS_SYMBOL            = "REMEMBERME_GC2QUESTS_PRESENT";
        private const string GC2STATS_SYMBOL             = "REMEMBERME_GC2STATS_PRESENT";
        private const string GC2MELEE_SYMBOL             = "REMEMBERME_GC2MELEE_PRESENT";
        private const string GC2SHOOTER_SYMBOL           = "REMEMBERME_GC2SHOOTER_PRESENT";
        private const string GC2MAILBOX_SYMBOL           = "REMEMBERME_GC2MAILBOX_PRESENT";
        private const string GC2MODULE_SYMBOL            = "REMEMBERME_GC2MODULE_PRESENT";
        private const string CLOUD_SAVE_SYMBOL           = "REMEMBERME_CLOUDSAVE_PRESENT";
        private const string AUTHENTICATION_SYMBOL       = "REMEMBERME_AUTHENTICATION_PRESENT";
        private const string CORE_SERVICES_SYMBOL        = "REMEMBERME_CORESERVICES_PRESENT";
        private const string LOCALIZATION_SYMBOL         = "REMEMBERME_LOCALIZATION_PRESENT";
        private const string NVIDIA_DLSS_SYMBOL          = "REMEMBERME_NVIDIA_DLSS_PRESENT";
        private const string GOOGLE_PLAY_SYMBOL          = "REMEMBERME_GOOGLEPLAY_PRESENT";
        private const string APPLE_SIGNIN_SYMBOL         = "REMEMBERME_APPLE_SIGNIN_PRESENT";
        private const string FACEBOOK_SDK_SYMBOL         = "REMEMBERME_FACEBOOK_SDK_PRESENT";
        private const string STEAMWORKS_SYMBOL           = "REMEMBERME_STEAMWORKS_PRESENT";
        private const string EDITOR_COROUTINES_SYMBOL    = "REMEMBERME_EDITOR_COROUTINES_PRESENT";
        private const string ADDRESSABLES_SYMBOL         = "REMEMBERME_ADDRESSABLES_PRESENT";
        private const string NETCODE_SYMBOL             = "REMEMBERME_NGO_PRESENT";
        private const string AC_SYMBOL                   = "CRYSTALSAVE_AC";
        private const string NANINOVEL_SYMBOL            = "NANINOVEL";
        private const string NANINOVEL_MODULE_SYMBOL     = "REMEMBERME_NANINOVEL_PRESENT";
        private const string TIMEMACHINE_SYMBOL          = "CRYSTALSAVE_TIMEMACHINE";
        private const string PLUGIN_DIR = "Assets/Plugins/CrystalSave";
        private const string TIMEMACHINE_MODULE_DIR = "Assets/Plugins/CrystalSave/Modules/TimeCrystal";
        private const string GC2MODULE_FILE = "Assets/Plugins/CrystalSave/Modules/GameCreator2/Remember/Core/RememberGC2DriverLocation.cs";

        // ───────────────────────────────────────────────────────────────
        //  STATE & CACHE
        // ───────────────────────────────────────────────────────────────
        private static bool isUpdating        = false;
        private static bool pendingUpdate     = false;
        private static readonly Dictionary<string,bool> namespaceCache = new Dictionary<string,bool>();

        // ───────────────────────────────────────────────────────────────
        //  STATIC CTOR
        // ───────────────────────────────────────────────────────────────
        static DefineSymbols()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnDomainReload;
            EditorApplication.projectChanged           += QueueUpdate;
            QueueUpdate();
        }

        private static void OnDomainReload()
        {
            namespaceCache.Clear();
            QueueUpdate();
        }

        private static void QueueUpdate()
        {
            if (pendingUpdate) return;
            pendingUpdate = true;
            EditorApplication.delayCall += () =>
            {
                pendingUpdate = false;
                UpdateDefineSymbols();
            };
        }

        // ───────────────────────────────────────────────────────────────
        //  MAIN UPDATE
        // ───────────────────────────────────────────────────────────────
        private static void UpdateDefineSymbols()
        {
            if (isUpdating) return;
            isUpdating = true;

            try
            {
                // ── Project-wide checks
                var activePipeline     = GraphicsSettings.currentRenderPipeline;
                string pipelineSymbol  = DetermineActivePipelineSymbol(activePipeline);
                bool   dlssPackageExists = IsDLSSPresent();

                // ── Cache all namespace lookups once
                CacheNamespace("MemoryPack");
                CacheNamespace("Org.BouncyCastle.Crypto");
                CacheNamespace("GameCreator.Runtime.Common");
                CacheNamespace("GameCreator.Runtime.Dialogue");
                CacheNamespace("GameCreator.Runtime.Inventory");
                CacheNamespace("GameCreator.Runtime.Quests");
                CacheNamespace("GameCreator.Runtime.Stats");
                CacheNamespace("GameCreator.Runtime.Melee");
                CacheNamespace("GameCreator.Runtime.Shooter");
                CacheNamespace("Arawn.CrystalSave.GameCreator2.Runtime");
                CacheNamespace("Arawn.CrystalSave.Runtime");
                CacheNamespace("Unity.Services.CloudSave");
                CacheNamespace("Unity.Services.Authentication");
                CacheNamespace("Unity.Services.Core");
                CacheNamespace("UnityEngine.Localization");
                CacheNamespace("Unity.EditorCoroutines.Editor");
                CacheNamespace("GooglePlayGames");
                CacheNamespace("AppleAuth");
                CacheNamespace("Facebook.Unity");
                CacheNamespace("Steamworks");
                CacheNamespace("UnityEngine.AddressableAssets");
                CacheNamespace("Unity.Netcode");
                CacheNamespace("Naninovel");
                CacheNamespace("Arawn.CrystalSave.NaninovelIntegration");
                CacheNamespace("Fullscreen.Mailbox.Runtime");
                CacheNamespace("AC");

                // ── Only update the currently‐selected build target group
                var group = EditorUserBuildSettings.selectedBuildTargetGroup;
                NamedBuildTarget nbt = NamedBuildTarget.FromBuildTargetGroup(group);

                string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(nbt);
                var symbolList = currentSymbols
                                    .Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries)
                                    .ToList();

                // ── Render-pipeline symbol
                symbolList.RemoveAll(s => s == STANDARD_SYMBOL || s == URP_SYMBOL || s == HDRP_SYMBOL);
                if (!string.IsNullOrEmpty(pipelineSymbol))
                    symbolList.Add(pipelineSymbol);

                // ── Namespace/package symbols
                ManageSymbol(symbolList, MEMORYPACK_SYMBOL,       IsNamespaceCached("MemoryPack"));
                ManageSymbol(symbolList, BOUNCYCASTLE_SYMBOL,     IsNamespaceCached("Org.BouncyCastle.Crypto"));
                ManageSymbol(symbolList, GC2CORE_SYMBOL,          IsNamespaceCached("GameCreator.Runtime.Common"));
                ManageSymbol(symbolList, GC2DIALOGUE_SYMBOL,      IsNamespaceCached("GameCreator.Runtime.Dialogue"));
                ManageSymbol(symbolList, GC2INVENTORY_SYMBOL,     IsNamespaceCached("GameCreator.Runtime.Inventory"));
                ManageSymbol(symbolList, GC2QUESTS_SYMBOL,        IsNamespaceCached("GameCreator.Runtime.Quests"));
                ManageSymbol(symbolList, GC2STATS_SYMBOL,         IsNamespaceCached("GameCreator.Runtime.Stats"));
                ManageSymbol(symbolList, GC2MELEE_SYMBOL,         IsNamespaceCached("GameCreator.Runtime.Melee"));
                ManageSymbol(symbolList, GC2SHOOTER_SYMBOL,       IsNamespaceCached("GameCreator.Runtime.Shooter"));
                ManageSymbol(symbolList, GC2MAILBOX_SYMBOL,       IsNamespaceCached("Fullscreen.Mailbox.Runtime"));
                ManageSymbol(symbolList, GC2MODULE_SYMBOL,        IsGC2ModulePresent());
                ManageSymbol(symbolList, CUSTOM_SYMBOL,           IsCrystalSavePresent());
                ManageSymbol(symbolList, CLOUD_SAVE_SYMBOL,       IsNamespaceCached("Unity.Services.CloudSave"));
                ManageSymbol(symbolList, AUTHENTICATION_SYMBOL,   IsNamespaceCached("Unity.Services.Authentication"));
                ManageSymbol(symbolList, CORE_SERVICES_SYMBOL,    IsNamespaceCached("Unity.Services.Core"));
                ManageSymbol(symbolList, LOCALIZATION_SYMBOL,     IsNamespaceCached("UnityEngine.Localization"));
                ManageSymbol(symbolList, EDITOR_COROUTINES_SYMBOL,IsNamespaceCached("Unity.EditorCoroutines.Editor"));
                ManageSymbol(symbolList, GOOGLE_PLAY_SYMBOL,      IsNamespaceCached("GooglePlayGames"));
                ManageSymbol(symbolList, APPLE_SIGNIN_SYMBOL,     IsNamespaceCached("AppleAuth"));
                ManageSymbol(symbolList, FACEBOOK_SDK_SYMBOL,     IsNamespaceCached("Facebook.Unity"));
                ManageSymbol(symbolList, STEAMWORKS_SYMBOL,       IsNamespaceCached("Steamworks"));
                ManageSymbol(symbolList, ADDRESSABLES_SYMBOL,     IsNamespaceCached("UnityEngine.AddressableAssets"));
                ManageSymbol(symbolList, NETCODE_SYMBOL,         IsNamespaceCached("Unity.Netcode"));
                ManageSymbol(symbolList, NANINOVEL_SYMBOL,        IsNamespaceCached("Naninovel"));
                ManageSymbol(symbolList, NANINOVEL_MODULE_SYMBOL, IsNamespaceCached("Arawn.CrystalSave.NaninovelIntegration"));
                ManageSymbol(symbolList, AC_SYMBOL,              IsNamespaceCached("AC"));
                ManageSymbol(symbolList, TIMEMACHINE_SYMBOL,     IsTimeMachineModulePresent());

                // ── DLSS (desktop only)
                bool dlssForGroup = dlssPackageExists && IsDlssSupportedOnGroup(group);
                ManageSymbol(symbolList, NVIDIA_DLSS_SYMBOL, dlssForGroup);

                // ── Commit if anything changed, in one domain‐reload
                string newSymbols = string.Join(";", symbolList);
                if (newSymbols != currentSymbols)
                {
                    EditorApplication.LockReloadAssemblies();
                    try
                    {
                        PlayerSettings.SetScriptingDefineSymbols(nbt, newSymbols);
                    }
                    finally
                    {
                        EditorApplication.UnlockReloadAssemblies();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"DefineSymbols: exception during update → {ex}");
            }
            finally
            {
                isUpdating = false;
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  HELPERS
        // ───────────────────────────────────────────────────────────────
        private static void CacheNamespace(string ns)
        {
            if (!namespaceCache.ContainsKey(ns))
                namespaceCache[ns] = IsNamespacePresent(ns);
        }

        private static bool IsNamespaceCached(string ns)
        {
            return namespaceCache.TryGetValue(ns, out bool present) && present;
        }
        private static bool IsCrystalSavePresent()
        {
            return Directory.Exists(PLUGIN_DIR);
        }

        private static bool IsTimeMachineModulePresent()
        {
            return Directory.Exists(TIMEMACHINE_MODULE_DIR);
        }

        private static bool IsGC2ModulePresent()
        {
            return File.Exists(GC2MODULE_FILE);
        }

        private static void ManageSymbol(List<string> list, string symbol, bool shouldDefine)
        {
            if (shouldDefine)
            {
                if (!list.Contains(symbol)) list.Add(symbol);
            }
            else
            {
                list.RemoveAll(s => s == symbol);
            }
        }

        private static string DetermineActivePipelineSymbol(RenderPipelineAsset pipeline)
        {
            if (pipeline == null)
                return STANDARD_SYMBOL;

            string assetName = pipeline.GetType().Name;
        #if UNITY_6000_0_OR_NEWER
            string runtimeName = pipeline.pipelineType?.Name ?? string.Empty;
        #else
            const string runtimeName = "";
        #endif

            bool isURP  = assetName.Contains("UniversalRenderPipeline") || runtimeName.Contains("UniversalRenderPipeline");
            bool isHDRP = assetName.Contains("HDRenderPipeline")        || runtimeName.Contains("HDRenderPipeline");

            if (isURP)  return URP_SYMBOL;
            if (isHDRP) return HDRP_SYMBOL;
            return string.Empty;
        }

        private static bool IsDLSSPresent()
        {
            const string dlssType =
                "UnityEngine.Rendering.HighDefinition.NVIDIA.DLSSSettings, " +
                "Unity.RenderPipelines.HighDefinition.Runtime";

            return Type.GetType(dlssType, throwOnError: false) != null;
        }

        private static bool IsDlssSupportedOnGroup(BuildTargetGroup group)
        {
            return group == BuildTargetGroup.Standalone;
        }

        private static bool IsNamespacePresent(string namespaceName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic || asm.ReflectionOnly) continue;
                try
                {
                    if (asm.GetTypes().Any(t => t.Namespace == namespaceName))
                        return true;
                }
                catch (ReflectionTypeLoadException) { }
            }
            return false;
        }
    }
}
#endif
