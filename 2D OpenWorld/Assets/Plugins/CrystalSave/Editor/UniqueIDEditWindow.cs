#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    public class UniqueIDEditWindow : EditorWindow
    {
        private UniqueID _uid;
        private string _newId;

        public static void Open(UniqueID uid)
        {
            if (uid == null) return;
            var w = CreateInstance<UniqueIDEditWindow>();
            w._uid = uid;
            w._newId = uid.ID;
            w.titleContent = new GUIContent("Edit Unique ID");
            const float width = 300f;
            const float height = 90f;
            var center = new Rect(Screen.currentResolution.width / 2f - width / 2f,
                                   Screen.currentResolution.height / 2f - height / 2f,
                                   width, height);
            w.position = center;
            w.minSize = new Vector2(width, height);
            w.ShowUtility();
        }

        private void OnGUI()
        {
            if (_uid == null)
            {
                Close();
                return;
            }

            GUILayout.Label("Edit Unique ID", EditorStyles.boldLabel);
            _newId = EditorGUILayout.TextField("New ID", _newId);

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Generate"))
            {
                _newId = Guid.NewGuid().ToString("N");
            }
            if (GUILayout.Button("Copy ID", GUILayout.Width(70)))
            {
                EditorGUIUtility.systemCopyBuffer = _newId;
                ShowNotification(new GUIContent($"Copied ID: {_newId}"));
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("OK", GUILayout.Width(60)))
            {
                Undo.RecordObject(_uid, "Edit Unique ID");
                if (!string.IsNullOrWhiteSpace(_newId))
                    _uid.ID = _newId;
                EditorUtility.SetDirty(_uid);
                PrefabUtility.RecordPrefabInstancePropertyModifications(_uid);
                if (_uid.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(_uid.gameObject.scene);
                Close();
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
#endif
