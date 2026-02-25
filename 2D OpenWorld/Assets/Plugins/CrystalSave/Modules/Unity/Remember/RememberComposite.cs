// ©2025 Arawn – Crystal Save
// RememberComposite.cs – single-component wrapper that owns multiple hidden SaveableComponent derivatives
// v1.12 – safe queued destruction in Editor (fix duplicate DestroyImmediate warnings)
#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Component = UnityEngine.Component;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;               // for delayCall / Undo / EditorApplication
#endif

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Wrapper that owns a designer-controlled list of <see cref="SaveableComponent"/>s.
    /// Hidden children are created once and then kept for the lifetime of the GameObject
    /// so they **retain their componentID** across domain reloads & scene loads.
    /// </summary>
    [Icon("Assets/Plugins/CrystalSave/Editor/Gizmos/RememberComposite.png")]
    [AddComponentMenu("Crystal Save/Remember Components/Remember Component")]
    //[RequireComponent(typeof(UniqueID))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [DefaultExecutionOrder(-80)]           // ⇦ EARLIER than SaveableComponent (0)
    public sealed class RememberComposite : MonoBehaviour 
    {
#if UNITY_EDITOR
        // Tracks whether the Editor is in the middle of opening another scene
        private static bool _sceneSwitchInProgress;

        // Static ctor runs once per domain-reload
        static RememberComposite()
        {
            // “sceneOpening” fires before anything in the current scene is destroyed
            EditorSceneManager.sceneOpening += (_, __) => _sceneSwitchInProgress = true;
            // Reset as soon as the new scene is fully opened
            EditorSceneManager.sceneOpened  += (_, __) => _sceneSwitchInProgress = false;
        }
#endif

        [SerializeField] private List<string> _rememberTypes = new();             // fq-names
        [SerializeField, HideInInspector] private List<SaveableComponent> _inner = new();
        private bool _cleanupQueued = false;

        /*────────────────────────────────────── LIFECYCLE ─────────────────────────*/
        private void Reset() => ScheduleRefresh();
        private void Awake()
        {
            if (Application.isPlaying) RefreshComponents();
        }

       private void OnDestroy()
{
#if UNITY_EDITOR
    // Avoid running cleanup while Unity is compiling or updating packages (during a build/domain reload)
    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        return;

    // Skip fake destroy during Edit→Play or Play→Edit transitions
    if (!Application.isPlaying &&
        EditorApplication.isPlayingOrWillChangePlaymode)
        return;

    // Skip whole-scene teardown when switching scenes in Edit-mode
    if (!Application.isPlaying &&            // still Edit-mode
        _sceneSwitchInProgress &&            // set by static ctor
        !PrefabUtility.IsPartOfPrefabAsset(gameObject))
        return;
#endif

    if (_cleanupQueued) return;
    _cleanupQueued = true;

    GameObject go = gameObject;

#if UNITY_EDITOR
    /*──────────────── PREFAB-ASSET PATH ─────────────────*/
    if (!Application.isPlaying &&
        PrefabUtility.IsPartOfPrefabAsset(go))
    {
        // Show “helper cleanup” window instead of silent auto-removal
        EditorApplication.delayCall += () =>
        {
            if (this != null) return; // user pressed Undo

            Type winType = FindCleanupWindowType();
            winType?.GetMethod("Open",
                       System.Reflection.BindingFlags.Public |
                       System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { go });
        };
        return; // ← never auto-destroy inside prefab assets
    }
#endif

    /*──────────────── SCENE OBJECT PATH ─────────────────*/

    /* Gather helper types that must be removed */
    string[] helperTypes = _rememberTypes.Count > 0
                           ? _rememberTypes.ToArray()
                           : _inner.Where(c => c != null)
                                   .Select(c => c.GetType().AssemblyQualifiedName)
                                   .ToArray();

#if UNITY_EDITOR
    bool useUndo = !Application.isPlaying;   // keep undo stack in Edit-mode
    RemoveSaveableChildren(go, helperTypes, useUndo);
#else
    RemoveSaveableChildren(go, helperTypes, useUndo: false);
#endif

    /* Remove auto-added UniqueID if appropriate */
#if UNITY_EDITOR
    if (!go.TryGetComponent<SaveablePrefab>(out _))
    {
        var uid = go.GetComponent<UniqueID>();
        if (uid != null)
        {
            if (!Application.isPlaying && useUndo)
                DestroyLater(uid);
            else
                Destroy(uid);
        }
    }
#else
    if (!go.TryGetComponent<SaveablePrefab>(out _))
    {
        var uid = go.GetComponent<UniqueID>();
        if (uid != null) Destroy(uid);
    }
#endif

    /* ── NEW: Remove PersistentVisibilityController if attached ── */
#if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        var pvc = go.GetComponent<PersistentVisibilityController>();
        if (pvc != null)
        {
            if (useUndo)
                DestroyLater(pvc);
            else
                Destroy(pvc);
        }
    }
#endif
}

        /*──────────────────────────────────── PUBLIC API ─────────────────────────*/
        public void ScheduleRefresh()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                RefreshComponents();
            else
                EditorApplication.delayCall += () => { if (this) RefreshComponents(); };
#else
            RefreshComponents();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (TryGetComponent<UniqueID>(out var uid))
                uid.hideFlags |= HideFlags.HideInInspector | HideFlags.NotEditable;
        }
#endif

        public Component GetInnerComponent(string fqName) =>
                        _inner.FirstOrDefault(c => c && c.GetType().AssemblyQualifiedName == fqName);

        public void RefreshComponents()
        {
            EnsureLocalUniqueID();

            // never disturb an active load process ⇒ wait until SaveManager completed
            if (Application.isPlaying && SaveManager.Instance && SaveManager.Instance.IsLoading)
                return;

            /* Normalise designer list */
            _rememberTypes = _rememberTypes.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            /* Collect current SaveableComponents */
            SaveableComponent[] all = gameObject.GetComponents<SaveableComponent>();

            /* Per-type deduplication – **prefer already-tracked instance** */
            foreach (var perType in all.GroupBy(c => c.GetType()))
            {
                bool shouldExist = _rememberTypes.Contains(perType.Key.AssemblyQualifiedName);

                // select candidate to keep
                SaveableComponent keep = perType.First();
                foreach (var c in perType) if (_inner.Contains(c)) { keep = c; break; }

                if (shouldExist)
                {
                    keep.MarkHiddenByComposite();
                    // destroy all *other* duplicates (they will have fresh componentIDs)
                    foreach (var extra in perType) if (extra != keep) DestroySmart(extra);
                    if (!_inner.Contains(keep)) _inner.Add(keep);
                }
                else
                {
                    DestroySmart(keep);
                    _inner.Remove(keep);
                }
            }

            /* Add missing component types */
            foreach (string fqName in _rememberTypes)
            {
                Type t = Type.GetType(fqName);
                if (t == null || !typeof(SaveableComponent).IsAssignableFrom(t)) continue;

                // does an instance already exist?
                if (_inner.Any(c => c && c.GetType() == t)) continue;

                // create + mark hidden
                var comp = (SaveableComponent)gameObject.AddComponent(t);
                comp.MarkHiddenByComposite();

                // Inherit RememberHomeScene from sibling SaveablePrefab at design time
                var siblingPrefab = gameObject.GetComponent<SaveablePrefab>();
                if (siblingPrefab != null && siblingPrefab.RememberHomeScene)
                {
                    comp.RememberHomeScene = true;
                    comp.HomeScene = siblingPrefab.HomeScene;
                }

                _inner.Add(comp);
            }

            /* Cleanup nulls (after domain reload, destroyed comps may leave null slots) */
            _inner.RemoveAll(c => c == null);
        }

        // Helper – queues an Undo-safe destroy on the next editor tick
#if UNITY_EDITOR
        private static void DestroyLater(UnityEngine.Object obj)
        {
            if (obj == null) return;               // already gone
            EditorApplication.delayCall += () =>
            {
                if (obj) Undo.DestroyObjectImmediate(obj);
            };
        }
#endif

        /// <summary>
        /// Guarantees that this GameObject owns a hidden <see cref="UniqueID"/>
        /// whenever other objects may need to refer to it.
        ///
        /// RULES ─────────────────────────────────────────────────────────────
        /// • If this GameObject *is* the root of a <see cref="SaveablePrefab"/>
        ///   → do nothing (the SaveablePrefab component manages its own ID).
        /// • If this GameObject is a *child* of a SaveablePrefab (not the root)
        ///   → do NOT add UniqueID (children use parent's SaveablePrefab ID).
        /// • Otherwise, if the object sits anywhere *inside* a SaveablePrefab
        ///   hierarchy → keep / create a UniqueID so nested prefabs can use it
        ///   as their ParentID.
        /// • Scene-only objects outside any prefab keep the old behaviour:
        ///   add an ID if one is missing, but never duplicate an existing one.
        /// </summary>
        private void EnsureLocalUniqueID()
        {
            /* 1️⃣  Skip the root of a SaveablePrefab – its own component
                    handles the authoritative ID.                               */
            if (TryGetComponent<SaveablePrefab>(out _))
                return;

            /* 2️⃣  CRITICAL FIX: Skip children of SaveablePrefabs - they should NOT
                    have UniqueID components. They use their parent's SaveablePrefab ID. */
            SaveablePrefab parentPrefab = GetComponentInParent<SaveablePrefab>(true);
            bool isChildOfSaveablePrefab = parentPrefab != null && parentPrefab.gameObject != gameObject;
            
            if (isChildOfSaveablePrefab)
            {
                // This is a child of a SaveablePrefab - should NOT have UniqueID
                // If one exists from prefab template, the editor will handle removal
                return;
            }

            /* 3️⃣  If a UID already exists, just hide its inspector entry.     */
            if (TryGetComponent<UniqueID>(out var uid))
            {
        #if UNITY_EDITOR
                uid.hideFlags |= HideFlags.HideInInspector | HideFlags.NotEditable;
        #endif
                return;         // nothing further to do
            }

            /* 4️⃣  Decide whether we *must* add a UID.                         */
            bool insideSaveablePrefab = GetComponentInParent<SaveablePrefab>(true) != null;

            //  – If we’re outside any prefab hierarchy we fall through to the
            //    “old” behaviour (plain scene object gets a UID if it needs one)
            //  – If we *are* inside a prefab hierarchy we *must* add a UID
            //    so deeper nested prefabs can reference us.

            /* 4️⃣  Create the UID (undo-safe in the Editor).                  */
        #if UNITY_EDITOR
            uid = UnityEditor.Undo.AddComponent<UniqueID>(gameObject);
        #else
            uid = gameObject.AddComponent<UniqueID>();
        #endif
        #if UNITY_EDITOR
            uid.hideFlags |= HideFlags.HideInInspector | HideFlags.NotEditable;
        #endif
        }

        /*───────────────────────────────────────────────────────────────────────
         * Removes every SaveableComponent whose type matches the supplied list
         *──────────────────────────────────────────────────────────────────────*/
        private static void RemoveSaveableChildren(GameObject go, string[] types, bool useUndo)
        {
            var comps = go.GetComponents<SaveableComponent>()
                          .Where(c => c && types.Contains(c.GetType().AssemblyQualifiedName))
                          .ToArray();

#if UNITY_EDITOR
            bool editorAndNotPlaying = !Application.isPlaying;

            foreach (var c in comps)
            {
                if (editorAndNotPlaying && useUndo)
                {
                    // Instead of calling Undo.DestroyObjectImmediate directly,
                    // queue it one frame later:
                    DestroyLater(c);
                }
                else
                {
                    DestroySmart(c);          // play-mode (or build) path
                }
            }
#else
            foreach (var c in comps) DestroySmart(c);
#endif
        }

        /* helper – editor or runtime smart destroy */
        private static void DestroySmart(Component c)
        {
            if (c == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Queue for next frame instead of immediate:
                DestroyLater(c);
                return;
            }
#endif
            Destroy(c);                           // play-mode
        }

#if UNITY_EDITOR
        static bool IsPrefabAsset(GameObject go) =>
                PrefabUtility.IsPartOfPrefabAsset(go);
#endif

#if UNITY_EDITOR
        private static Type FindCleanupWindowType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("Arawn.CrystalSave.Editor.RememberCompositeCleanupWindow");
                if (t != null) return t;
            }
            return null;
        }
#endif
    }
}
#endif
