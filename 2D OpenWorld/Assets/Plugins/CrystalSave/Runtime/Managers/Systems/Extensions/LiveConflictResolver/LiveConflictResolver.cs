using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Provides a simple runtime conflict resolver that compares two objects
    /// and displays a UI overlay allowing the user to accept, reject or merge
    /// the changes field by field.
    /// </summary>
    public class LiveConflictResolver : MonoBehaviour
    {
        [SerializeField] private Canvas overlayCanvas;

        /// <summary>
        /// Optional canvas used to display the conflict resolution UI. When
        /// assigned, the resolver will reuse this canvas instead of creating a
        /// new one at runtime.
        /// </summary>
        public Canvas OverlayCanvas
        {
            get => overlayCanvas;
            set => overlayCanvas = value;
        }
        private class DiffEntry
        {
            public RuntimeObjectDiffer.FieldDiff Diff;
            public Toggle Toggle;
        }

        /// <summary>
        /// Compares the two objects and presents an overlay to merge the values.
        /// </summary>
        /// <typeparam name="T">Type of the objects being compared.</typeparam>
        /// <param name="local">Current local instance.</param>
        /// <param name="remote">Incoming remote instance.</param>
        /// <param name="onResolved">Callback when the conflict is resolved.</param>
        public void Resolve<T>(T local, T remote, Action<T> onResolved)
        {
            var diffs = RuntimeObjectDiffer.Compare(local, remote);
            if (diffs.Count == 0)
            {
                onResolved?.Invoke(local);
                return;
            }

            // Create or use provided canvas
            Canvas canvas;
            GameObject canvasGO;
            bool createdCanvas = false;
            if (overlayCanvas != null)
            {
                canvas = overlayCanvas;
                canvasGO = canvas.gameObject;
                canvasGO.SetActive(true);
            }
            else
            {
                canvasGO = new GameObject("LiveConflictResolverOverlay");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
                createdCanvas = true;
            }

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvas.transform, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.75f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var entries = new List<DiffEntry>();

            foreach (var diff in diffs)
            {
                var row = new GameObject(diff.FieldName);
                row.transform.SetParent(panel.transform, false);
                var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.childControlWidth = true;
                rowLayout.childForceExpandWidth = false;

                var textGO = new GameObject("Label");
                textGO.transform.SetParent(row.transform, false);
                var text = textGO.AddComponent<Text>();
                text.font = GetBuiltinFont();
                text.text = $"{diff.FieldName}: {diff.LocalValue} -> {diff.RemoteValue}";
                text.color = Color.white;

                var toggleGO = new GameObject("UseRemote");
                toggleGO.transform.SetParent(row.transform, false);
                var toggle = toggleGO.AddComponent<Toggle>();
                toggle.isOn = true; // default to remote value
                entries.Add(new DiffEntry { Diff = diff, Toggle = toggle });
            }

            // Buttons row
            var buttonsRow = new GameObject("Buttons");
            buttonsRow.transform.SetParent(panel.transform, false);
            var buttonsLayout = buttonsRow.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;

            CreateButton("Accept Remote", buttonsRow.transform, () =>
            {
                ApplyAll(remote, local, diffs);
                Cleanup();
                onResolved?.Invoke(local);
            });

            CreateButton("Keep Local", buttonsRow.transform, () =>
            {
                Cleanup();
                onResolved?.Invoke(local);
            });

            CreateButton("Merge", buttonsRow.transform, () =>
            {
                foreach (var entry in entries)
                {
                    if (entry.Toggle.isOn)
                    {
                        entry.Diff.Apply(remote, local);
                    }
                }
                Cleanup();
                onResolved?.Invoke(local);
            });

            void Cleanup()
            {
                if (createdCanvas)
                {
                    Destroy(canvasGO);
                }
                else
                {
                    foreach (Transform child in canvas.transform)
                    {
                        Destroy(child.gameObject);
                    }
                    canvasGO.SetActive(false);
                }
            }
        }

        private static Button CreateButton(string label, Transform parent, UnityAction onClick)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var btnImage = go.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.onClick.AddListener(onClick);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = GetBuiltinFont();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 30f;
            layout.preferredWidth = 150f;

            return button;
        }

        private static Font GetBuiltinFont()
        {
            // Unity 2022+ exposes LegacyRuntime.ttf instead of the old Arial.ttf
            // built-in font. Attempt to load the new font first and fall back to
            // Arial for compatibility with older versions.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private static void ApplyAll<T>(T source, T target, IEnumerable<RuntimeObjectDiffer.FieldDiff> diffs)
        {
            foreach (var diff in diffs)
            {
                diff.Apply(source, target);
            }
        }
    }

    /// <summary>
    /// Reflection based diff utility.
    /// </summary>
    public static class RuntimeObjectDiffer
    {
        public class FieldDiff
        {
            public FieldInfo Field;
            public object LocalValue;
            public object RemoteValue;

            public string FieldName => Field.Name;

            public void Apply(object source, object target)
            {
                var value = Field.GetValue(source);
                Field.SetValue(target, value);
            }
        }

        public static List<FieldDiff> Compare<T>(T a, T b)
        {
            var result = new List<FieldDiff>();
            if (a == null || b == null) return result;

            var type = typeof(T);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var field in type.GetFields(flags))
            {
                var va = field.GetValue(a);
                var vb = field.GetValue(b);
                if (!Equals(va, vb))
                {
                    result.Add(new FieldDiff
                    {
                        Field = field,
                        LocalValue = va,
                        RemoteValue = vb
                    });
                }
            }

            return result;
        }
    }
}

