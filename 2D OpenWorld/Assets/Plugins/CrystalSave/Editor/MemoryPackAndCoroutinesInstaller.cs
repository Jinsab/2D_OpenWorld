#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    [InitializeOnLoad]
    [DefaultExecutionOrder(-250)]
    sealed partial class MemoryPackAndCoroutinesInstaller
    {
        /*─────────────────────────────  folders / consts  ───────────────────────────*/
        const string MemPkgRoot       = "Packages/com.memorypack.runtime";
        const string MemRuntimeDir    = MemPkgRoot + "/Runtime";
        const string MemEditorDir     = MemPkgRoot + "/Editor";

        // New plugin folder for BouncyCastle
        const string BcPluginDir      = "Assets/Plugins/BouncyCastle";

        const string CoroutinesPackage = "com.unity.editorcoroutines";

        /*─────────────────────────────  NuGet maps  ────────────────────────────────*/
        static readonly (string id,string ver,string zip,string file)[] RuntimeDlls =
        {
            ("MemoryPack.Core",                        "1.21.4", "lib/netstandard2.1/MemoryPack.Core.dll",                         "MemoryPack.Core.dll"),
#if !UNITY_6000_5_OR_NEWER
            // These assemblies are shipped with Unity 6.5+, so we only need them for older versions
            ("System.Collections.Immutable",           "6.0.0",  "lib/netstandard2.0/System.Collections.Immutable.dll",            "System.Collections.Immutable.dll"),
            ("System.Runtime.CompilerServices.Unsafe", "6.0.0",  "lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll",  "System.Runtime.CompilerServices.Unsafe.dll"),
#endif
        };

        static readonly (string id,string ver,string zip,string file,bool roslyn)[] EditorDlls =
        {
            ("MemoryPack.Generator",          "1.21.4", "analyzers/dotnet/cs/MemoryPack.Generator.dll",         "MemoryPack.Generator.dll",          true),
            ("Microsoft.CodeAnalysis.Common", "4.3.1",  "lib/netstandard2.0/Microsoft.CodeAnalysis.dll",       "Microsoft.CodeAnalysis.dll",        false),
            ("Microsoft.CodeAnalysis.CSharp", "4.3.1",  "lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll","Microsoft.CodeAnalysis.CSharp.dll", false),
#if !UNITY_6000_5_OR_NEWER
            // This assembly is shipped with Unity 6.5+, so we only need it for older versions
            ("System.Reflection.Metadata",    "6.0.0",  "lib/netstandard2.0/System.Reflection.Metadata.dll",   "System.Reflection.Metadata.dll",    false),
#endif
        };

        // BouncyCastle metadata
        static readonly (string id,string ver,string zip,string file) BouncyCastleDll =
            ("BouncyCastle.Cryptography", "2.6.1",
                "lib/net461/BouncyCastle.Cryptography.dll",
             "BouncyCastle.Cryptography.dll");  // must match assembly name

        static AddRequest? coroutinesAddRequest;

        static MemoryPackAndCoroutinesInstaller()
        {
            EditorApplication.delayCall += PromptIfNeeded;
#if UNITY_2021_3_OR_NEWER
            // modern hook lives in BuildTargetChangeHook below
#else
            EditorUserBuildSettings.activeBuildTargetChanged += OnBuildTargetChanged;
#endif
        }

        [DidReloadScripts]
        static void OnScriptsReloaded() => PromptIfNeeded();

        [MenuItem("Tools/Crystal Save/Project/Install Dependencies", false, 2000)]
        static void MenuInstallDependencies() => ShowConsentDialog(forcePrompt: true);

        [MenuItem("Tools/Crystal Save/Project/Install Dependencies", true)]
        static bool ValidateMenuInstallDependencies() => DependenciesMissing;

        const string PromptFlag = "REMEMBERME_DEP_PROMPTED";

        static bool DependenciesMissing
        {
            get
            {
                bool memMissing  = !File.Exists(Path.Combine(MemPkgRoot, "package.json"));
                bool coroMissing = !File.Exists($"Packages/{CoroutinesPackage}/package.json");
                bool needsBc     = NeedsBouncyCastle;
                bool hasPlugin   = File.Exists(Path.Combine(BcPluginDir, BouncyCastleDll.file));
                bool bcMissing   = needsBc && !hasPlugin;
                return memMissing || coroMissing || bcMissing;
            }
        }

        static bool NeedsBouncyCastle =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL
         || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneLinux64;

        static void PromptIfNeeded()
        {
            if (SessionState.GetBool(PromptFlag, false)) return;
            if (!DependenciesMissing)                     return;
            SessionState.SetBool(PromptFlag, true);
            ShowConsentDialog(forcePrompt: false);
        }

        static void ShowConsentDialog(bool forcePrompt)
        {
            bool memMissing  = !File.Exists(Path.Combine(MemPkgRoot, "package.json"));
            bool coroMissing = !File.Exists($"Packages/{CoroutinesPackage}/package.json");
            bool needsBc     = NeedsBouncyCastle;
            bool hasPlugin   = File.Exists(Path.Combine(BcPluginDir, BouncyCastleDll.file));
            bool bcMissing   = needsBc && !hasPlugin;

            if (!memMissing && !coroMissing && !bcMissing) return;

            string msg = "Crystal Save requires the following free dependencies:\n\n";
            if (memMissing)
                msg += "• MemoryPack 1.21.4 (nuget.org – MIT)\n" +
                       "    – MemoryPack.Core & Generator\n" +
                       "    – System.Collections.Immutable / Unsafe\n";
            if (coroMissing)
                msg += "• Unity Editor Coroutines 1.x (Unity Registry)\n";
            if (bcMissing)
                msg += "• BouncyCastle 2.6.1 (nuget.org – MIT) – will install as a plugin under Assets/Plugins/BouncyCastle for AES-GCM on WebGL & Linux\n";
            msg += "\nInstall now?";

            if (EditorUtility.DisplayDialog("Install required dependencies?", msg, "Install", "Cancel"))
            {
                if (memMissing)  InstallMemoryPack();
                if (coroMissing) InstallEditorCoroutines();
                if (bcMissing)   InstallBouncyCastle();
            }
            else if (!forcePrompt)
            {
                Debug.LogWarning("[Crystal Save] Dependencies not installed. " +
                                 "Run Tools ▸ Crystal Save ▸ Install Dependencies to add them.");
            }
        }

        static void InstallMemoryPack()
        {
            try
            {
                Directory.CreateDirectory(MemRuntimeDir);
                Directory.CreateDirectory(MemEditorDir);

                foreach (var (id, v, p, f)    in RuntimeDlls)            ExtractNuGet(id, v, p, Path.Combine(MemRuntimeDir,  f), false);
                foreach (var (id, v, p, f, r) in EditorDlls)             ExtractNuGet(id, v, p, Path.Combine(MemEditorDir, f), r);

                File.WriteAllText(Path.Combine(MemPkgRoot, "package.json"),
@"{
  ""name"": ""com.memorypack.runtime"",
  ""version"": ""1.21.4"",
  ""displayName"": ""MemoryPack Runtime"",
  ""description"": ""MemoryPack runtime & Roslyn generator (embedded)"",
  ""unity"": ""2022.3"",
  ""author"": { ""name"": ""Cysharp"" }
}");
                Debug.Log("<color=green>[Dependency]</color> MemoryPack 1.21.4 installed.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dependency] MemoryPack install failed:\n" + ex);
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        static void InstallEditorCoroutines()
        {
            coroutinesAddRequest = Client.Add(CoroutinesPackage);
            EditorApplication.update += OnCoroutinesAddProgress;
        }

        static void OnCoroutinesAddProgress()
        {
            if (coroutinesAddRequest is null || !coroutinesAddRequest.IsCompleted) return;
            EditorApplication.update -= OnCoroutinesAddProgress;
            if (coroutinesAddRequest.Status == StatusCode.Success)
                Debug.Log("<color=green>[Dependency]</color> Unity Editor Coroutines installed.");
            else
                Debug.LogError($"[Dependency] Failed to add {CoroutinesPackage}: {coroutinesAddRequest.Error.message}");
        }

        static void InstallBouncyCastle()
        {
            try
            {
                // ── Remove any old embedded package ────────────────────
                const string oldPkg = "Packages/com.bouncycastle.runtime";
                if (Directory.Exists(oldPkg))
                {
                    Directory.Delete(oldPkg, true);
                    var meta = oldPkg + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                    Debug.Log("<color=yellow>[Dependency]</color> Removed old BouncyCastle package from Packages/");
                }

                // ── Create plugin folder ──────────────────────────────
                Directory.CreateDirectory(BcPluginDir);

                // 1) Extract the correctly-named DLL
                string dst = Path.Combine(BcPluginDir, BouncyCastleDll.file);
                ExtractNuGet(BouncyCastleDll.id, BouncyCastleDll.ver, BouncyCastleDll.zip, dst, false);

                // 2) Write link.xml to preserve it for IL2CPP/WebGL
                File.WriteAllText(Path.Combine(BcPluginDir, "link.xml"),
@"<linker>
  <assembly fullname=""BouncyCastle.Cryptography"">
    <type fullname=""Org.BouncyCastle.Crypto.*"" preserve=""all"" />
  </assembly>
</linker>");

                Debug.Log("<color=green>[Dependency]</color> BouncyCastle 2.6.1 installed as plugin under Assets/Plugins/BouncyCastle.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dependency] BouncyCastle install failed:\n" + ex);
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        static void ExtractNuGet(string id, string ver, string zipPath, string dst, bool roslynLabel)
        {
            string nupkg = DownloadNuGet(id, ver);
            using var zip = ZipFile.OpenRead(nupkg);
            var entry = zip.GetEntry(zipPath.Replace('\\','/'))
                        ?? throw new Exception($"Path {zipPath} not found in {id} {ver}");
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            entry.ExtractToFile(dst, true);

            string meta = dst + ".meta";
            if (!File.Exists(meta)) WriteMeta(meta, roslynLabel);
            else if (roslynLabel)   EnsureRoslynLabel(meta);
        }

        static string DownloadNuGet(string id, string ver)
        {
            string tmp = Path.Combine(Path.GetTempPath(), $"{id}.{ver}.nupkg");
            if (File.Exists(tmp)) return tmp;

            string url = $"https://www.nuget.org/api/v2/package/{id}/{ver}";
            try
            {
                // Run the blocking download on a worker so the editor thread can keep repainting progress.
                Task downloadTask = Task.Run(() =>
                {
                    using var wc = new WebClient();
                    wc.DownloadFile(url, tmp);
                });

                double start = EditorApplication.timeSinceStartup;
                while (!downloadTask.Wait(100))
                {
                    float pulse = 0.1f + Mathf.PingPong((float)(EditorApplication.timeSinceStartup - start) * 0.35f, 0.8f);
                    EditorUtility.DisplayProgressBar("Installing Dependencies", $"Downloading {id} v{ver}\u2026", pulse);
                }

                downloadTask.GetAwaiter().GetResult(); // propagate any exception
            }
            catch
            {
                if (File.Exists(tmp)) try { File.Delete(tmp); } catch { }
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return tmp;
        }

        static void WriteMeta(string path, bool roslyn)
        {
            File.WriteAllText(path,
$"fileFormatVersion: 2\nguid: {Guid.NewGuid():N}\nlabels:{(roslyn ? "\n- RoslynAnalyzer" : "")}\nDefaultImporter:\n  externalObjects: {{ }}\n  userData: \n  assetBundleName: \n  assetBundleVariant: ");
        }

        static void EnsureRoslynLabel(string metaPath)
        {
            string txt = File.ReadAllText(metaPath);
            if (!txt.Contains("RoslynAnalyzer"))
            {
                txt = txt.Replace("labels:", "labels:\n- RoslynAnalyzer");
                File.WriteAllText(metaPath, txt);
            }
        }

        internal static void OnBuildTargetChanged()
        {
            SessionState.SetBool(PromptFlag, false); // allow re-prompt
            PromptIfNeeded();
        }
    }
}

#if UNITY_2021_3_OR_NEWER
namespace Arawn.CrystalSave.Editor
{
    sealed class BuildTargetChangeHook : UnityEditor.Build.IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;
        public void OnActiveBuildTargetChanged(BuildTarget prev, BuildTarget next) =>
            MemoryPackAndCoroutinesInstaller.OnBuildTargetChanged();
    }
}
#endif
