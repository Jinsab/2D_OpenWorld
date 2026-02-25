#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Arawn.CrystalSave.Editor
	{
	public static class CreateSaveMenu
	{
	private const string MenuPath  = "GameObject/Crystal Save/UI/Dynamic Save Slot Menu";
	private const string PrefabPath = "Assets/Plugins/CrystalSave/Modules/Unity/UI/Prefabs/Save Menu.prefab";
        private const string GroupedMenuPath  = "GameObject/Crystal Save/UI/Grouped Dropdown Save Menu";
        private const string GroupedPrefabPath = "Assets/Plugins/CrystalSave/Modules/Unity/UI/Prefabs/GroupedSaveSlotsUI.prefab";

        [MenuItem(MenuPath, false, 10)]
        public static void AddSaveMenu(MenuCommand menuCommand)
        {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
        {
                        Debug.LogError($"Save Menu prefab not found at {PrefabPath}");
                        return;
        }

#pragma warning disable CS0618 // Suppress FindFirstObjectByType deprecation warning for cross-version compatibility
#if UNITY_2023_1_OR_NEWER
                var eventSystem = Object.FindFirstObjectByType<EventSystem>();
#else
                var eventSystem = Object.FindObjectOfType<EventSystem>();
#endif
#pragma warning restore CS0618
                if (eventSystem == null)
                {
                        var go = new GameObject("EventSystem", typeof(EventSystem));
#if USE_NEW_INPUT || (ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER)
                        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                        var module = go.AddComponent<StandaloneInputModule>();
#if !UNITY_2020_1_OR_NEWER
                        module.forceModuleActive = true;
#endif
#endif
                        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                GameObjectUtility.SetParentAndAlign(instance, menuCommand.context as GameObject);
                Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
                Selection.activeObject = instance;
        }

        [MenuItem(GroupedMenuPath, false, 11)]
        public static void AddGroupedSaveMenu(MenuCommand menuCommand)
        {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroupedPrefabPath);
                if (prefab == null)
                {
                        Debug.LogError($"Grouped Save Slots UI prefab not found at {GroupedPrefabPath}");
                        return;
                }

#pragma warning disable CS0618 // Suppress FindFirstObjectByType deprecation warning for cross-version compatibility
#if UNITY_2023_1_OR_NEWER
                var eventSystem = Object.FindFirstObjectByType<EventSystem>();
#else
                var eventSystem = Object.FindObjectOfType<EventSystem>();
#endif
#pragma warning restore CS0618
                if (eventSystem == null)
                {
                        var go = new GameObject("EventSystem", typeof(EventSystem));
#if USE_NEW_INPUT || (ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER)
                        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                        var module = go.AddComponent<StandaloneInputModule>();
#if !UNITY_2020_1_OR_NEWER
                        module.forceModuleActive = true;
#endif
#endif
                        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                GameObjectUtility.SetParentAndAlign(instance, menuCommand.context as GameObject);
                Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
                Selection.activeObject = instance;
        }
	}
	}
#endif
#endif
