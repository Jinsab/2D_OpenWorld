#if UNITY_EDITOR && ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(StaticMasterSecret))]
    public class StaticMasterSecretEditor : UnityEditor.Editor
    {
        SerializedProperty base64Prop;

        void OnEnable()
        {
            base64Prop = serializedObject.FindProperty("base64");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(base64Prop);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Static Master Secret is embedded in the build and used for local encryption. " +
                "If you want the key to stay off the client, use Cloud Crypto Mode = ServerSide " +
                "and the one-click export in Crystal Save Settings.",
                MessageType.Info);

            if (!IsValidBase64(base64Prop.stringValue))
            {
                if (GUILayout.Button("Generate Random Key"))
                {
                    byte[] secret = new byte[32];
                    using (var rng = RandomNumberGenerator.Create())
                        rng.GetBytes(secret);

                    base64Prop.stringValue = Convert.ToBase64String(secret);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private static bool IsValidBase64(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            try { return Convert.FromBase64String(value).Length == 32; }
            catch { return false; }
        }
    }
}
#endif
