#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Arawn.CrystalSave.Runtime;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// Runtime UI generator that exposes buttons for every Save and Load
    /// method in the <see cref="SaveManager"/> API. The generated Canvas
    /// scales with screen size (reference 1920x1080) and an EventSystem is
    /// automatically created if none exists.
    ///
    /// Designed for testing the save/load pipeline in builds.
    /// </summary>
    public class SaveLoadAPITestUI : MonoBehaviour
    {
        void Start()
        {
            EnsureEventSystem();
            var canvas = CreateCanvas();

            // Display startup warnings about platform compatibility
            if (IsUsingCloudSave())
            {
                Debug.LogWarning("[SaveLoadAPITestUI] Unity Cloud Save detected. Synchronous save operations (SaveSync) are disabled to prevent freezing.");
            }
            if (IsWebGLBuild())
            {
                Debug.LogWarning("[SaveLoadAPITestUI] WebGL build detected. Some operations may have limited functionality or different behavior.");
            }

            var container = new GameObject("ButtonContainer");
            container.transform.SetParent(canvas.transform, false);
            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(320f, 800f); // Set explicit size

            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 10, 10);

            // Add ContentSizeFitter to auto-resize container
            var sizeFitter = container.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var actions = new List<(string label, System.Delegate action)>
            {
                ("Save",        (Action)(() => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("Save() is BLOCKED: Unity Cloud Save detected. Synchronous save operations will freeze the application! Use SaveAsync() instead.");
                        return;
                    }
                    if (IsWebGLWithCloudSave())
                    {
                        Debug.LogWarning("Save() may not work reliably in WebGL with Unity Cloud Save. Consider using SaveAsync() for better compatibility.");
                    }
                    SaveManager.Instance.Save(1);
                })),
                ("SaveWithName",(Action)(() => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("SaveWithName() is BLOCKED: Unity Cloud Save detected. Synchronous save operations will freeze the application! Use SaveAsync() instead.");
                        return;
                    }
                    if (IsWebGLWithCloudSave())
                    {
                        Debug.LogWarning("SaveWithName() may not work reliably in WebGL with Unity Cloud Save. Consider using SaveAsync() for better compatibility.");
                    }
                    SaveManager.Instance.Save(1, null, "Slot 1");
                })),
#if (!UNITY_WEBGL || UNITY_EDITOR) && !REMEMBERME_CLOUDSAVE_PRESENT
                ("SaveSync",    (Action)(() => {
                    // Triple protection against freezing:
                    
                    // 1. Check compile-time cloud save detection
#if REMEMBERME_CLOUDSAVE_PRESENT
                    Debug.LogError("SaveSync is BLOCKED: Cloud Save is enabled via REMEMBERME_CLOUDSAVE_PRESENT. This would freeze the application!");
                    return;
#endif
                    
                    // 2. Check runtime cloud save detection
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("SaveSync is BLOCKED: Unity Cloud Save detected at runtime. This would freeze the application! Use SaveAsync instead.");
                        return;
                    }
                    
                    // 3. Check WebGL platform
                    if (IsWebGLBuild())
                    {
                        Debug.LogError("SaveSync is BLOCKED: WebGL platform detected. This could freeze the browser! Use SaveAsync instead.");
                        return;
                    }
                    
                    // Only execute if all checks pass
                    Debug.Log("SaveSync: All safety checks passed, executing synchronous save...");
                    SaveManager.Instance.SaveSync(1);
                })),
#endif
                ("SaveAsync",   (Func<Task>)(async () => await SaveManager.Instance.SaveAsync(1))),
                ("SaveAsyncDebug", (Func<Task>)(async () => await SaveManager.Instance.SaveAsyncDebug(1))),
                ("SaveImmediateAsync", (Func<Task>)(async () => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogWarning("SaveImmediateAsync may have different behavior with Unity Cloud Save - cloud operations are inherently async and may not be truly 'immediate'.");
                    }
                    await SaveManager.Instance.SaveImmediateAsync(1);
                })),
                ("QuickSave",   (Action)(() => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("QuickSave() is BLOCKED: Unity Cloud Save detected. Synchronous save operations will freeze the application! Use QuickSaveAsync() instead.");
                        return;
                    }
                    if (IsWebGLWithCloudSave())
                    {
                        Debug.LogWarning("QuickSave() may not work reliably in WebGL with Unity Cloud Save. Consider using QuickSaveAsync() for better compatibility.");
                    }
                    SaveManager.Instance.QuickSave();
                })),
                ("QuickSaveAsync", (Func<Task>)(async () => await SaveManager.Instance.QuickSaveAsync())),
                ("AutoSave",    (Action)(() => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("AutoSave() is BLOCKED: Unity Cloud Save detected. Synchronous save operations will freeze the application! Use AutoSaveAsync() instead.");
                        return;
                    }
                    if (IsWebGLWithCloudSave())
                    {
                        Debug.LogWarning("AutoSave() may not work reliably in WebGL with Unity Cloud Save. Consider using AutoSaveAsync() for better compatibility.");
                    }
                    SaveManager.Instance.AutoSave();
                })),
                ("AutoSaveAsync",(Func<Task>)(async () => await SaveManager.Instance.AutoSaveAsync())),
                ("Load",        (Action)(() => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("Load() is BLOCKED: Unity Cloud Save detected. Synchronous load operations will freeze the application! Use QuickLoadAsync() instead.");
                        return;
                    }
                    if (IsWebGLWithCloudSave())
                    {
                        Debug.LogWarning("Load() may not work reliably in WebGL with Unity Cloud Save. Consider using QuickLoadAsync() for better compatibility.");
                    }
                    SaveManager.Instance.Load(1);
                })),
#if !UNITY_WEBGL || UNITY_EDITOR
                ("LoadSaveDataForSlotAsync", (Func<Task>)(async () => {
                    if (IsWebGLBuild())
                    {
                        Debug.LogError("LoadSaveDataForSlotAsync is not available in WebGL builds due to platform limitations with direct file access.");
                        return;
                    }
                    await SaveManager.Instance.LoadSaveDataForSlotAsync(1);
                })),
                ("LoadSaveSlotAsync", (Func<Task>)(async () => {
                    if (IsWebGLBuild())
                    {
                        Debug.LogError("LoadSaveSlotAsync is not available in WebGL builds due to platform limitations with direct file access.");
                        return;
                    }
                    await SaveManager.Instance.LoadSaveSlotAsync(1, false, false, true);
                })),
#endif
                ("QuickLoad",   (Action)(() => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("QuickLoad() is BLOCKED: Unity Cloud Save detected. Synchronous load operations will freeze the application! Use QuickLoadAsync() instead.");
                        return;
                    }
                    if (IsWebGLWithCloudSave())
                    {
                        Debug.LogWarning("QuickLoad() may not work reliably in WebGL with Unity Cloud Save. Consider using QuickLoadAsync() for better compatibility.");
                    }
                    SaveManager.Instance.QuickLoad();
                })),
                ("QuickLoadAsync", (Func<Task>)(async () => await SaveManager.Instance.QuickLoadAsync())),
                ("LoadAutoSave",(Action)(() => {
                    if (IsUsingCloudSave())
                    {
                        Debug.LogError("LoadAutoSave() is BLOCKED: Unity Cloud Save detected. Synchronous load operations will freeze the application! Use async load methods instead.");
                        return;
                    }
                    if (IsWebGLWithCloudSave())
                    {
                        Debug.LogWarning("LoadAutoSave() may not work reliably in WebGL with Unity Cloud Save. Consider using async load methods for better compatibility.");
                    }
                    SaveManager.Instance.LoadAutoSave();
                })),
            };

            foreach (var (label, action) in actions)
            {
                CreateButton(label, action, container.transform);
            }
        }

        void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindAnyObjectByType<EventSystem>() != null) return;
#else
            if (FindObjectOfType<EventSystem>() != null) return;
#endif
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            
            // Use the appropriate input module based on the active input system
#if ENABLE_INPUT_SYSTEM
            eventSystemGO.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGO.AddComponent<StandaloneInputModule>();
#endif
        }

        Canvas CreateCanvas()
        {
            var canvasGO = new GameObject("SaveLoadAPITestCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Ensure it renders on top
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        void CreateButton(string label, System.Delegate action, Transform parent)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 60f);

            var img = go.AddComponent<Image>();
            // Try to get Unity's built-in UI sprite, with fallbacks for different Unity versions
            img.sprite = GetDefaultUISprite();
            img.type = Image.Type.Sliced;
            
            // Color buttons differently based on their compatibility
            if (label.Contains("Sync") && (IsUsingCloudSave() || IsWebGLBuild()))
            {
                // Red tint for incompatible sync operations
                img.color = new Color(1f, 0.6f, 0.6f, 1f); // Light red
            }
            else if (IsWebGLWithCloudSave() && !label.Contains("Async"))
            {
                // Yellow tint for potentially problematic operations
                img.color = new Color(1f, 1f, 0.6f, 1f); // Light yellow
            }
            else
            {
                // Normal gray for safe operations
                img.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Light gray
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = img; // Set the image as the target graphic
            
            // Set button colors for better visibility
            var colors = button.colors;
            colors.normalColor = img.color;
            colors.highlightedColor = new Color(img.color.r + 0.1f, img.color.g + 0.1f, img.color.b + 0.1f, 1f);
            colors.pressedColor = new Color(img.color.r - 0.1f, img.color.g - 0.1f, img.color.b - 0.1f, 1f);
            colors.selectedColor = new Color(img.color.r + 0.05f, img.color.g + 0.05f, img.color.b + 0.05f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            
            button.onClick.AddListener(() => {
                try 
                {
                    if (action is Func<Task> asyncAction)
                    {
                        // Handle async operations
                        _ = ExecuteAsyncAction(asyncAction, label);
                    }
                    else if (action is Action syncAction)
                    {
                        // Handle synchronous operations
#if UNITY_WEBGL && !UNITY_EDITOR
                        // In WebGL, even sync operations should be careful about blocking
                        if (label.Contains("Sync"))
                        {
                            Debug.LogWarning($"Skipping potentially blocking operation in WebGL: {label}");
                            return;
                        }
#endif
                        syncAction();
                        Debug.Log($"Executed: {label}");
                    }
                    else
                    {
                        Debug.LogError($"Unknown action type for {label}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error executing {label}: {e.Message}");
                }
            });

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var txtRect = textGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            var txt = textGO.AddComponent<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 14; // Set explicit font size

            // Unity 2022+ replaced the built-in Arial font with LegacyRuntime.ttf.
            // Try to load LegacyRuntime first and fall back to Arial for older
            // editor versions where it's still available.
            var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null)
                builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (builtinFont == null)
                builtinFont = Resources.FindObjectsOfTypeAll<Font>()[0]; // Fallback to any available font
            txt.font = builtinFont;
            txt.color = Color.black;
            
            // Add LayoutElement for proper sizing in VerticalLayoutGroup
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.minHeight = 60f;
            layoutElement.preferredHeight = 60f;
            layoutElement.minWidth = 300f;
            layoutElement.preferredWidth = 300f;
        }

        private async Task ExecuteAsyncAction(Func<Task> asyncAction, string label)
        {
            try
            {
                await asyncAction();
                Debug.Log($"Executed async: {label}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error executing async {label}: {e.Message}");
            }
        }

        private bool IsUsingCloudSave()
        {
            // Use CrystalSave's actual define symbols to detect cloud save
#if REMEMBERME_CLOUDSAVE_PRESENT
            // CrystalSave has Unity Cloud Save integration enabled
            Debug.Log("[SaveLoadAPITestUI] Cloud Save detected via REMEMBERME_CLOUDSAVE_PRESENT define symbol");
            return true;
#else
            // Check platform - Unity Cloud Save is commonly used on WebGL
#if UNITY_WEBGL && !UNITY_EDITOR
            return true; // Assume cloud save on WebGL builds
#else
            return false;
#endif
#endif
        }        private bool IsWebGLBuild()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private bool IsWebGLWithCloudSave()
        {
            return IsWebGLBuild() || IsUsingCloudSave();
        }

        private Sprite GetDefaultUISprite()
        {
            // Skip trying built-in resources to avoid error spam
            // Just create a reliable custom sprite
            return CreateDefaultSprite();
        }

        private Sprite CreateDefaultSprite()
        {
            // Create a simple 4x4 white texture with border for better UI appearance
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            
            // Fill the texture with white, with a slight border effect
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    // Create a simple border effect
                    if (x == 0 || x == 3 || y == 0 || y == 3)
                    {
                        texture.SetPixel(x, y, new Color(0.8f, 0.8f, 0.8f, 1f)); // Light gray border
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.white); // White center
                    }
                }
            }
            
            texture.Apply();
            
            // Create sprite with proper border settings for UI slicing
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        }
    }
}
#endif
