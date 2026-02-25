#if MEMORYPACK
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace Arawn.CrystalSave.Runtime
{
        public class ResourceMissingScriptChecker : EditorWindow
        {
                private readonly List<string> _problemAssets = new List<string>();
                private readonly List<string> _allResourceAssets = new List<string>();
                private readonly Dictionary<string, Object> _cachedAssets = new Dictionary<string, Object>();
                private Vector2 _problemScroll;
                private Vector2 _resourceScroll;
                private long _totalResourceMemory;

                [MenuItem("Tools/Crystal Save/Project/Check Resource Folders for Missing Scripts")]
                public static void ShowWindow()
                {
                        var window = GetWindow<ResourceMissingScriptChecker>("Missing Scripts Checker");
                        window.ScanForMissingScripts();
                }

                private void OnGUI()
                {
                        if (GUILayout.Button("Scan"))
                        {
                                ScanForMissingScripts();
                        }

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Resource Browser", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"Estimated RAM usage: {(_totalResourceMemory / (1024f * 1024f)):F2} MB");
                        // Expand the resource list to fill the window height instead of a fixed percentage.
                        // Reserve some space for the surrounding controls.
                        float resourceHeight = Mathf.Max(position.height - 150f, 100f);
                        _resourceScroll = EditorGUILayout.BeginScrollView(_resourceScroll, GUILayout.Height(resourceHeight));
                        foreach (string path in _allResourceAssets)
                        {
                                EditorGUILayout.BeginHorizontal();
                                _cachedAssets.TryGetValue(path, out var obj);
                                EditorGUILayout.ObjectField(obj, typeof(Object), false);
                                if (GUILayout.Button("Select", GUILayout.Width(60)))
                                {
                                        Selection.activeObject = obj;
                                        EditorGUIUtility.PingObject(obj);
                                        EditorUtility.FocusProjectWindow();
                                }
                                EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.EndScrollView();

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField($"Found {_problemAssets.Count} assets with missing scripts.", EditorStyles.boldLabel);

                        _problemScroll = EditorGUILayout.BeginScrollView(_problemScroll);
                        foreach (string path in _problemAssets)
                        {
                                EditorGUILayout.BeginHorizontal();
                                _cachedAssets.TryGetValue(path, out var obj);
                                EditorGUILayout.ObjectField(obj, typeof(Object), false);
                                if (GUILayout.Button("Select", GUILayout.Width(60)))
                                {
                                        Selection.activeObject = obj;
                                        EditorGUIUtility.PingObject(obj);
                                        EditorUtility.FocusProjectWindow();
                                }
                                EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.EndScrollView();
                }

                private void ScanForMissingScripts()
                {
                        _problemAssets.Clear();
                        _allResourceAssets.Clear();
                        _cachedAssets.Clear();
                        _totalResourceMemory = 0;
                        string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
                        foreach (string guid in guids)
                        {
                                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                                if (!assetPath.ToLower().Contains("/resources/"))
                                        continue;

                                if (AssetDatabase.IsValidFolder(assetPath))
                                        continue;

                                _allResourceAssets.Add(assetPath);

                                Object obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
                                _cachedAssets[assetPath] = obj;
                                if (obj != null)
                                {
                                        _totalResourceMemory += Profiler.GetRuntimeMemorySizeLong(obj);
                                }

                                GameObject go = obj as GameObject;
                                if (go == null)
                                        continue;

                                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                                {
                                        _problemAssets.Add(assetPath);
                                }
                        }
                }
        }
}
#endif
