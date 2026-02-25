#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	[CustomPropertyDrawer(typeof(VersionData))]
	public class VersionInfoPropertyDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// Begin property
			EditorGUI.BeginProperty(position, label, property);

			// Calculate rects
			float lineHeight = EditorGUIUtility.singleLineHeight;
			float spacing = 4f;
			float labelWidth = 110f; // Width for the 'Current Version' label

			// Draw main label
			Rect mainLabelRect = new Rect(position.x, position.y, labelWidth, lineHeight);
			EditorGUI.LabelField(mainLabelRect, label);

			// Move to next line
			float y = position.y + lineHeight + spacing;

			// Define width for sub-labels and fields
			float subLabelWidth = 60f;
			float fieldWidth = 60f;
			float spacingBetweenFields = 20f;

			// Total width for all fields and labels
			float totalWidth = (subLabelWidth + fieldWidth + spacingBetweenFields) * 3 - spacingBetweenFields;

			// Start x position to center the fields
			float startX = position.x + (position.width - totalWidth) / 2;

			// Major
			Rect majorLabelRect = new Rect(startX, y, subLabelWidth, lineHeight);
			EditorGUI.LabelField(majorLabelRect, "Major:");
			Rect majorFieldRect = new Rect(startX + subLabelWidth + 5, y, fieldWidth, lineHeight);
			EditorGUI.PropertyField(majorFieldRect, property.FindPropertyRelative("Major"), GUIContent.none);

			// Minor
			float minorStartX = majorFieldRect.x + fieldWidth + spacingBetweenFields;
			Rect minorLabelRect = new Rect(minorStartX, y, subLabelWidth, lineHeight);
			EditorGUI.LabelField(minorLabelRect, "Minor:");
			Rect minorFieldRect = new Rect(minorLabelRect.x + subLabelWidth + 5, y, fieldWidth, lineHeight);
			EditorGUI.PropertyField(minorFieldRect, property.FindPropertyRelative("Minor"), GUIContent.none);

			// Patch
			float patchStartX = minorFieldRect.x + fieldWidth + spacingBetweenFields;
			Rect patchLabelRect = new Rect(patchStartX, y, subLabelWidth, lineHeight);
			EditorGUI.LabelField(patchLabelRect, "Patch:");
			Rect patchFieldRect = new Rect(patchLabelRect.x + subLabelWidth + 5, y, fieldWidth, lineHeight);
			EditorGUI.PropertyField(patchFieldRect, property.FindPropertyRelative("Patch"), GUIContent.none);

			// End property
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// Two lines: label and fields
			return (EditorGUIUtility.singleLineHeight * 2) + 8f;
		}
	}
}
#endif
#endif