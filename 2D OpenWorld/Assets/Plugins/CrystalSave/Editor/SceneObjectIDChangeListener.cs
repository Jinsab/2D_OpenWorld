#if UNITY_EDITOR && ARAWN_REMEMBERME && MEMORYPACK
using UnityEditor;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// Editor script to listen for changes in SceneObjectID.KeepAcrossScenes
    /// and synchronize SaveableComponents accordingly. Uses Undo.postprocessModifications
    /// instead of polling each frame.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneObjectIDChangeListener
    {
        static SceneObjectIDChangeListener()
        {
            // Whenever a property changes via Inspector/Undo, check if it was KeepAcrossScenes
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        /// <summary>
        /// Called whenever one or more properties have been modified (including via Inspector).
        /// We look for SceneObjectID.KeepAcrossScenes changes and sync those instances only.
        /// </summary>
        private static UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            foreach (var mod in modifications)
            {
                // The “currentValue” reflects the new value after the modification.
                var target = mod.currentValue.target as SceneObjectID;
                if (target == null)
                    continue;

                // Only care if the property path ends with "KeepAcrossScenes"
                // (the serialized field name for that public property).
                if (!mod.currentValue.propertyPath.EndsWith("KeepAcrossScenes"))
                    continue;

                bool newKeepAcross = target.KeepAcrossScenes;
                SyncSaveableComponents(target, newKeepAcross);
            }

            return modifications;
        }

        /// <summary>
        /// Synchronizes the KeepAcrossScenes property of all SaveableComponents
        /// on the same GameObject as the given SceneObjectID.
        /// </summary>
        private static void SyncSaveableComponents(SceneObjectID sceneObjectID, bool newKeepAcross)
        {
            var saveables = sceneObjectID.GetComponents<SaveableComponent>();
            if (saveables == null || saveables.Length == 0) return;

            foreach (var saveable in saveables)
            {
                if (saveable.KeepAcrossScenes == newKeepAcross)
                    continue;

                Undo.RecordObject(saveable, "Sync KeepAcrossScenes with SceneObjectID");
                saveable.KeepAcrossScenes = newKeepAcross;
                EditorUtility.SetDirty(saveable);

                Logger.Log(
                    $"SceneObjectIDChangeListener: Synchronized KeepAcrossScenes on '{saveable.gameObject.name}' → {newKeepAcross}.",
                    LogLevel.Info
                );
            }
        }
    }
}
#endif
