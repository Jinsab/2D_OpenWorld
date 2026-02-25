// SpherePoolDemo.cs – updated to use SaveablePrefabFactory (auto‑pooling) and support both legacy and new Input Systems
//
// Demonstrates how to:
//
//   • Optionally pre‑warm via the factory (which auto‑pools when enabled in SaveSettings).
//   • Launch projectiles with velocity.
//   • Auto‑despawn them after N seconds via the factory (returns to pool when pooling is ON).
//   • Despawn projectiles that were ALIVE when the game was saved
//     after they’re restored on load.
//   • Handle input from either the legacy Input Manager or the new Input System.
//
#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// SaveablePrefab / Cache / Events

namespace Arawn.CrystalSave.Demo
{
    public class SpherePoolDemo : MonoBehaviour 
    {
        /* ─── Inspector ──────────────────────────────────────────────────────── */

        [Header("Prefab to pool")]
        [Tooltip("Any prefab that carries a SaveablePrefab component.")]
        [SerializeField] private SaveablePrefab spherePrefab;

    [Header("Pooling parameters")]
    [Tooltip("How many inactive clones to pre‑warm at Start when Use Prefab Pooling is ON.")]
        [SerializeField] private int initialPoolSize = 32;

        [Tooltip("If ON, live bullets are written to the save‑file. If OFF, bullets are treated as transient VFX and are not saved.")]
        [SerializeField] private bool rememberInSave = true;

        [Header("Motion")]
        [Tooltip("Initial velocity (m/s) imparted to each sphere.")]
        [SerializeField] private float launchSpeed = 10f;

        [Tooltip("Time (s) a sphere stays alive before returning to the pool.")]
        [SerializeField] private float despawnDelay = 4f;

    /* ─── Runtime ─────────────────────────────────────────────────────────── */
    private bool usePooling;

        /* ─── Start ───────────────────────────────────────────────────────────── */
        private void Start()
        {
            // Ensure the prefab owns a stable GUID + registry mapping BEFORE we spawn.
            if (!string.IsNullOrEmpty(spherePrefab?.PrefabAssetID))
            {
                AssetProvider.Load<PrefabRegistry>("PrefabRegistry")
                           ?.TryAddPrefab(spherePrefab.PrefabAssetID, spherePrefab.gameObject, out _);
            }
            else
            {
                spherePrefab?.GenerateAndRegisterPrefabAssetIDAtRuntime();
            }

            // ① Read current pooling mode.
            usePooling = SaveManager.Instance?.SaveSettings?.usePrefabPooling ?? false;

            // ② Optional: pre‑warm using the factory path (will return to pool when pooling is ON).
            if (usePooling && initialPoolSize > 0)
                StartCoroutine(Prewarm(initialPoolSize));

            // ③ Listen for restored prefabs so we can start their despawn timer.
            SaveablePrefab.OnAfterRestore += HandleRestoredSphere;

            // ④ UI helper
            CreateInstructionUI();
        }

        /* ─── OnDestroy ───────────────────────────────────────────────────────── */
        private void OnDestroy()
        {
#if UNITY_EDITOR
            // nothing to dispose when using factory path
#endif
            SaveablePrefab.OnAfterRestore -= HandleRestoredSphere;
        }

        /* ─── Application quit (builds) ───────────────────────────────────────── */
    private void OnApplicationQuit() { /* no-op */ }

        /* ─── Update ──────────────────────────────────────────────────────────── */
        private void Update()
        {
            bool clicked;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // New Input System (only)
            clicked = Mouse.current?.leftButton.wasPressedThisFrame ?? false;
#else
            // Legacy Input Manager
            clicked = Input.GetMouseButtonDown(0);
#endif
            if (!clicked) return;

            Vector3 pos = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
            SaveablePrefab sphere = SaveablePrefabFactory.Instantiate(
                spherePrefab.gameObject,
                pos,
                Quaternion.identity,
                parent: null,
                registerWithSaveSystem: rememberInSave);

            if (sphere && sphere.TryGetComponent(out Rigidbody rb))
            {
#if UNITY_6000_0_OR_NEWER
                // Unity 6+: use the new API
                rb.linearVelocity = Camera.main.transform.forward * launchSpeed;
#else
                // Unity 2022.x and earlier: fall back to the old API
                rb.velocity = Camera.main.transform.forward * launchSpeed;
#endif
            }
            StartCoroutine(DespawnLater(sphere, despawnDelay));
        }

        /* ─── Coroutine: auto‑despawn ─────────────────────────────────────────── */
        private System.Collections.IEnumerator DespawnLater(SaveablePrefab inst, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (inst) SaveablePrefabFactory.Destroy(inst);
        }

        /* ─── Handle clones restored by the save‑system ──────────────────────── */
        private void HandleRestoredSphere(SaveablePrefab sp)
        {
            if (sp.PrefabAssetID != spherePrefab.PrefabAssetID) return;
            StartCoroutine(DespawnLater(sp, despawnDelay));
        }

        /* ─── Pre‑warm via factory ───────────────────────────────────────────── */
        private System.Collections.IEnumerator Prewarm(int count)
        {
            int created = 0;
            while (created < count)
            {
                // Create a few per frame to avoid hitches; batching inside SaveSettings may also apply
                int batch = Mathf.Min(8, count - created);
                for (int i = 0; i < batch; i++)
                {
                    var sp = SaveablePrefabFactory.Instantiate(
                        spherePrefab.gameObject,
                        Vector3.one * 99999f, // offscreen
                        Quaternion.identity,
                        parent: null,
                        registerWithSaveSystem: rememberInSave);
                    if (sp)
                        SaveablePrefabFactory.Destroy(sp); // returns to pool when pooling is ON
                }
                created += batch;
                yield return null;
            }
        }

        /* ─── UI helper ---------------------------------------------------------------- */
        /// <summary>
        /// Creates a small screen-space canvas with instructions for the demo.
        /// Only runs once; subsequent scenes ignore it.
        /// </summary>
        private void CreateInstructionUI()
        {
            const string uiRootName = "SpherePoolDemo-Instructions";
            if (GameObject.Find(uiRootName)) return;          // already present?

            /* Canvas */
            var canvasGO = new GameObject(uiRootName,
                                          typeof(Canvas),
                                          typeof(CanvasScaler),
                                          typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;                       // on top
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            /* Text */
            var textGO = new GameObject("Text", typeof(Text));
            textGO.transform.SetParent(canvasGO.transform, false);
            var text = textGO.GetComponent<Text>();
            Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 2022+
            if (builtin == null)
                builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");          // 2019–2021
            if (builtin == null)
                builtin = Font.CreateDynamicFontFromOSFont("Arial", 16);            // last-ditch

            text.font = builtin;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontSize = 18;
            text.color = new Color(1f, 1f, 1f, 0.92f);

            text.text =
                $"Sphere Pool Demo\n" +
                $"- Left-click anywhere to spawn a sphere.\n" +
                $"- Each sphere flies at {launchSpeed} m/s and auto-despawns after {despawnDelay} s.\n\n" +
                $"Pooling & persistence:\n" +
                $"  • Spawning uses SaveablePrefabFactory – when 'Use Prefab Pooling' is ON in SaveSettings,\n" +
                $"    instances come from a pool automatically.\n" +
                $"  • This component exposes **Remember In Save**: when ON, spawned spheres register with\n" +
                $"    the save system and are restored on load; when OFF they are treated as transient VFX.\n" +
                $"  • The prefab’s default *Register With Save System* value is ignored; the factory applies\n" +
                $"    the mode at spawn time.\n\n" +
                $"- Use the *Save Slot Manager* window to save / load and watch the behaviour.";

            /* Position in top-left corner */
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(600, 0);      // width, flexible height
        }
    }
}
#endif
