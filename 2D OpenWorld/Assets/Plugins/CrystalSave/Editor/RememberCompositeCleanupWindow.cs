#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using System;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	internal sealed class RememberCompositeCleanupWindow : EditorWindow
	{
		private GameObject _root;
		private Component[] _leftOvers;

		public static void Open(GameObject prefabRoot)
		{
			var win = GetWindow<RememberCompositeCleanupWindow>(utility: true,
																title: "Remember-Me cleanup");
			win.minSize = new Vector2(150, 120);
			win._root = prefabRoot;
			win._leftOvers =
				prefabRoot.GetComponents<SaveableComponent>()
						  .Cast<Component>()
						  .Concat(prefabRoot.GetComponents<UniqueID>())
						  .Distinct()
						  .ToArray();
			win.Focus();
		}

		private void OnGUI()
		{
			if (_root == null) { Close(); return; }

			EditorGUILayout.LabelField("Removed RememberComposite", EditorStyles.boldLabel);
			EditorGUILayout.LabelField(
				"The prefab still contains the following helper components:",
				EditorStyles.wordWrappedLabel);
			EditorGUILayout.Space(6);

			foreach (var c in _leftOvers)
				if (c) EditorGUILayout.LabelField($"� {c.GetType().Name}");

			EditorGUILayout.Space(10);
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Clean up", GUILayout.Width(100), GUILayout.Height(24)))
					PerformCleanup();
			}
		}

		private void PerformCleanup()
		{
			if (_root == null) { Close(); return; }

			Undo.IncrementCurrentGroup();
			Undo.RecordObjects(new[] { _root }, "Remember-Me cleanup");

			foreach (var c in _leftOvers)
				if (c) Undo.DestroyObjectImmediate(c);

			EditorUtility.SetDirty(_root);
			AssetDatabase.SaveAssets();
			Close();
		}
	}
}
#endif
#endif