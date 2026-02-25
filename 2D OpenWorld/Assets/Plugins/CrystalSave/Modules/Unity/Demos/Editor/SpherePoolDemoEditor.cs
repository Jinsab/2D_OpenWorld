#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.Demo;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demo.Editor
{
    [CustomEditor(typeof(SpherePoolDemo))]
    public class SpherePoolDemoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Read pooling state from current SaveSettings if available
            var settings = SaveManager.Instance ? SaveManager.Instance.SaveSettings : null;
            bool pooling = settings?.usePrefabPooling ?? false;

            string msg = $"Use Prefab Pooling: {(pooling ? "ENABLED" : "DISABLED")}";
            if (settings == null)
                msg += "\n(SaveManager not present; value will apply at runtime)";

            EditorGUILayout.HelpBox(msg, MessageType.Info);

            if (pooling && settings != null)
            {
                // Surface batching details to help performance tuning
                string batchingInfo = settings.enablePooledPrefabBatching
                    ? $"Batching: ENABLED (Spawn Batch Size {settings.pooledPrefabSpawnBatchSize})"
                    : "Batching: DISABLED";
                EditorGUILayout.LabelField("Pooling Details", batchingInfo);
                EditorGUILayout.Space(4);
            }

            // Draw the rest of the component fields
            base.OnInspectorGUI();
        }
    }
}
#endif
