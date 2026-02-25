#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    public class ModulesInstallerWindow : EditorWindow
    {
        private const string PackagesFolder = "Assets/Plugins/CrystalSave/Packages/";
        private const string ModulePrefix = "CrystalSave-";
        
        private List<ModuleInfo> availableModules = new List<ModuleInfo>();
        private Vector2 scrollPosition;
        private GUIStyle headerStyle;
        private GUIStyle moduleBoxStyle;
        private GUIStyle titleStyle;
        private GUIStyle versionStyle;
        private GUIStyle installButtonStyle;
        private bool stylesInitialized = false;
        
        private Texture2D headerBackground;
        private Texture2D moduleBackground;
        
        [MenuItem("Tools/Crystal Save/Install Modules...", false, 1500)]
        public static void ShowWindow()
        {
            var window = GetWindow<ModulesInstallerWindow>("Crystal Save Modules Installer");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            ScanForModules();
        }
        
        private void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            // Header style
            headerStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.9f, 1f) : new Color(0.1f, 0.2f, 0.3f) },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 15, 15)
            };
            
            // Module box style
            moduleBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 15, 15),
                margin = new RectOffset(10, 10, 5, 5),
                normal = { background = CreateColorTexture(EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.28f) : new Color(0.85f, 0.85f, 0.88f)) }
            };
            
            // Title style
            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 1f) : new Color(0.1f, 0.1f, 0.2f) }
            };
            
            // Version style
            versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.7f, 0.8f) : new Color(0.4f, 0.4f, 0.5f) },
                fontStyle = FontStyle.Italic
            };
            
            // Install button style
            installButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(20, 20, 8, 8),
                normal = { textColor = Color.white, background = CreateColorTexture(new Color(0.58f, 0.44f, 0.86f)) },      // Purple-violet
                hover = { textColor = Color.white, background = CreateColorTexture(new Color(0.68f, 0.56f, 0.92f)) },       // Lighter lilac-blue
                active = { textColor = Color.white, background = CreateColorTexture(new Color(0.48f, 0.36f, 0.76f)) }       // Deeper purple
            };
            
            // Create textures
            headerBackground = CreateColorTexture(EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.25f) : new Color(0.75f, 0.75f, 0.8f));
            moduleBackground = CreateColorTexture(EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.24f) : new Color(0.88f, 0.88f, 0.9f));
            
            stylesInitialized = true;
        }
        
        private Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
        
        private void OnGUI()
        {
            InitializeStyles();
            
            // Draw header
            DrawHeader();
            
            // Draw refresh button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(25)))
            {
                ScanForModules();
            }
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Draw modules list
            if (availableModules.Count == 0)
            {
                DrawNoModulesMessage();
            }
            else
            {
                DrawModulesList();
            }
        }
        
        private void DrawHeader()
        {
            var headerRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(headerRect, headerBackground);
            
            GUILayout.BeginArea(headerRect);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Crystal Save Modules Installer", headerStyle);
            GUILayout.Label($"Found {availableModules.Count} module(s)", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }
        
        private void DrawNoModulesMessage()
        {
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            
            var messageStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = Color.gray }
            };
            
            GUILayout.Label("No modules found", messageStyle);
            GUILayout.Space(10);
            GUILayout.Label($"Place .unitypackage files starting with '{ModulePrefix}'\nin the folder: {PackagesFolder}", messageStyle);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
        }
        
        private void DrawModulesList()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var module in availableModules)
            {
                DrawModuleItem(module);
            }
            
            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawModuleItem(ModuleInfo module)
        {
            EditorGUILayout.BeginVertical(moduleBoxStyle);
            
            EditorGUILayout.BeginHorizontal();
            
            // Left side: Module info
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            EditorGUILayout.LabelField(module.Title, titleStyle);
            EditorGUILayout.LabelField($"Version: {module.Version}", versionStyle);
            
            GUILayout.Space(5);
            
            var pathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.gray },
                wordWrap = true
            };
            EditorGUILayout.LabelField($"Package: {module.FileName}", pathStyle);
            
            EditorGUILayout.EndVertical();
            
            // Right side: Install button
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Install", installButtonStyle, GUILayout.Height(35)))
            {
                InstallModule(module);
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void ScanForModules()
        {
            availableModules.Clear();
            
            if (!Directory.Exists(PackagesFolder))
            {
                Debug.LogWarning($"CrystalSave Modules folder not found: {PackagesFolder}");
                return;
            }
            
            string[] packageFiles = Directory.GetFiles(PackagesFolder, "*.unitypackage", SearchOption.TopDirectoryOnly);
            
            foreach (string filePath in packageFiles)
            {
                string fileName = Path.GetFileName(filePath);
                
                // Skip meta files and non-CrystalSave packages
                if (fileName.EndsWith(".meta") || !fileName.StartsWith(ModulePrefix))
                    continue;
                
                var moduleInfo = ParseModuleInfo(fileName, filePath);
                if (moduleInfo != null)
                {
                    availableModules.Add(moduleInfo);
                }
            }
            
            // Sort modules by title
            availableModules = availableModules.OrderBy(m => m.Title).ToList();
            
            Repaint();
        }
        
        private ModuleInfo ParseModuleInfo(string fileName, string filePath)
        {
            try
            {
                // Remove .unitypackage extension
                string nameWithoutExt = fileName.Replace(".unitypackage", "");
                
                // Remove CrystalSave- prefix
                if (!nameWithoutExt.StartsWith(ModulePrefix))
                    return null;
                
                string nameAfterPrefix = nameWithoutExt.Substring(ModulePrefix.Length);
                
                // Pattern: ModuleName-V1.0.0
                // We need to extract the module name and version
                var versionMatch = Regex.Match(nameAfterPrefix, @"-V(\d+\.\d+\.\d+)$", RegexOptions.IgnoreCase);
                
                string moduleName;
                string version;
                
                if (versionMatch.Success)
                {
                    version = versionMatch.Groups[1].Value;
                    moduleName = nameAfterPrefix.Substring(0, versionMatch.Index);
                }
                else
                {
                    // No version found, use entire name
                    moduleName = nameAfterPrefix;
                    version = "Unknown";
                }
                
                // Format the title by adding spaces before capital letters
                string title = FormatModuleName(moduleName);
                
                return new ModuleInfo
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Title = title,
                    Version = version,
                    RawName = moduleName
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse module info from {fileName}: {ex.Message}");
                return null;
            }
        }
        
        private string FormatModuleName(string rawName)
        {
            // Add spaces before capital letters and numbers
            string formatted = Regex.Replace(rawName, @"(\d+)", " $1");
            formatted = Regex.Replace(formatted, @"([A-Z])", " $1");
            formatted = formatted.Trim();
            
            // Clean up multiple spaces
            formatted = Regex.Replace(formatted, @"\s+", " ");
            
            return formatted;
        }
        
        private void InstallModule(ModuleInfo module)
        {
            if (!File.Exists(module.FilePath))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"Package file not found:\n{module.FilePath}", 
                    "OK");
                return;
            }
            
            bool proceed = EditorUtility.DisplayDialog(
                "Install Module",
                $"Install {module.Title} (v{module.Version})?\n\n" +
                $"This will import the Unity package:\n{module.FileName}\n\n" +
                $"You can choose which assets to import in the next step.",
                "Install",
                "Cancel");
            
            if (proceed)
            {
                AssetDatabase.ImportPackage(module.FilePath, true);
                Debug.Log($"Installing CrystalSave module: {module.Title} v{module.Version}");
            }
        }
        
        private void OnDestroy()
        {
            if (headerBackground != null)
                DestroyImmediate(headerBackground);
            if (moduleBackground != null)
                DestroyImmediate(moduleBackground);
        }
        
        [Serializable]
        private class ModuleInfo
        {
            public string FileName;
            public string FilePath;
            public string Title;
            public string Version;
            public string RawName;
        }
    }
}
#endif
