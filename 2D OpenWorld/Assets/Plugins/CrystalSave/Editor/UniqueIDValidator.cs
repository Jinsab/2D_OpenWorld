#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Editor
{
	public static class UniqueIDValidator
	{
		public static List<T> FindDuplicateOrMissingUniqueIDs<T>(IEnumerable<T> items, Func<T, string> getID)
		{
			List<T> problematicItems = new List<T>();
			HashSet<string> uniqueIDs = new HashSet<string>();

			foreach (var item in items)
			{
				string id = getID(item);

				if (string.IsNullOrEmpty(id))
				{
					Runtime.Logger.Log($"Item of type '{typeof(T).Name}' has an empty UniqueID.", Runtime.LogLevel.Warning);
					problematicItems.Add(item);
				}
				else if (!uniqueIDs.Add(id))
				{
					Runtime.Logger.Log($"Duplicate UniqueID '{id}' found in item of type '{typeof(T).Name}'.", Runtime.LogLevel.Warning);
					problematicItems.Add(item);
				}
			}

			return problematicItems;
		}

		public static int FixDuplicateOrMissingUniqueIDs<T>(List<T> itemsToFix, Action<T, string> setID)
		{
			int fixCount = 0;

			foreach (var item in itemsToFix)
			{
				if (item == null)
				{
					// This can occur if an item was destroyed during the process
					continue;
				}

				string newID = Guid.NewGuid().ToString();
				setID(item, newID);
				fixCount++;
				Runtime.Logger.Log($"Assigned new UniqueID '{newID}' to item of type '{typeof(T).Name}'.");
			}

			return fixCount;
		}

		/// <summary>
		/// Finds duplicate or missing UniqueIDs in components of type T.
		/// </summary>
		/// <typeparam name="T">The type of component to search for UniqueIDs.</typeparam>
		/// <returns>A list of components with duplicate or missing UniqueIDs.</returns>
		public static List<T> FindDuplicateOrMissingUniqueIDsInComponents<T>() where T : Component
		{
			List<T> problematicComponents = new List<T>();
			HashSet<string> uniqueIDs = new HashSet<string>();

			// Find all components of type T in the scene
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
			T[] allComponents = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618

                        foreach (var component in allComponents)
                        {
                                if (IsInPool(component))
                                        continue;

                                string id = GetID(component);

                                if (string.IsNullOrEmpty(id))
                                {
                                        Runtime.Logger.Log($"Component '{component.gameObject.name}' of type '{typeof(T).Name}' has an empty UniqueID.", Runtime.LogLevel.Warning);
                                        problematicComponents.Add(component);
                                }
                                else if (!uniqueIDs.Add(id))
                                {
                                        Runtime.Logger.Log($"Duplicate UniqueID '{id}' found on Component '{component.gameObject.name}' of type '{typeof(T).Name}'.", Runtime.LogLevel.Warning);
                                        problematicComponents.Add(component);
                                }
                        }

			return problematicComponents;
		}

		/// <summary>
		/// Fixes duplicate and missing UniqueIDs in components of type T by assigning new GUIDs.
		/// </summary>
		/// <typeparam name="T">The type of component to fix UniqueIDs.</typeparam>
		/// <param name="componentsToFix">List of components to fix.</param>
		/// <returns>The number of UniqueIDs fixed.</returns>
                public static int FixDuplicateOrMissingUniqueIDsInComponents<T>(List<T> componentsToFix) where T : Component
                {
                        int fixCount = 0;

			foreach (var component in componentsToFix)
			{
				if (component == null)
				{
					// This can occur if a component was destroyed during the process
					continue;
				}

				string oldID = GetID(component);
				string newID = Guid.NewGuid().ToString();

				SerializedObject serializedObject = new SerializedObject(component);
				SerializedProperty idProperty = FindIDProperty(serializedObject);
				if (idProperty != null)
				{
					idProperty.stringValue = newID;
					serializedObject.ApplyModifiedProperties();

					Runtime.Logger.Log($"Assigned new UniqueID '{newID}' to '{component.gameObject.name}' ({component.GetType().Name}).");
					fixCount++;
				}
				else
				{
					Runtime.Logger.Log($"Component '{component.gameObject.name}' of type '{typeof(T).Name}' does not have an 'ID' property or field.", Runtime.LogLevel.Warning);
				}
			}

			// Save all modified assets
			if (fixCount > 0)
			{
				// Assuming all components are part of the same scene or asset
				EditorUtility.SetDirty(componentsToFix[0]); // Mark at least one as dirty to ensure saving
				AssetDatabase.SaveAssets();
			}

                        return fixCount;
                }

                private static bool IsInPool(Component component)
                {
                        Transform current = component.transform;
                        while (current != null)
                        {
                                if (current.gameObject.name == "RememberMe_Pools")
                                        return true;
                                if ((current.gameObject.hideFlags & HideFlags.DontSave) != 0)
                                        return true;
                                current = current.parent;
                        }
                        return false;
                }

                /// <summary>
                /// Retrieves the ID from a component.
                /// Assumes the component has a public string property or field named 'ID'.
                /// </summary>
		/// <typeparam name="T">The type of component.</typeparam>
		/// <param name="component">The component instance.</param>
		/// <returns>The UniqueID string.</returns>
		private static string GetID<T>(T component) where T : Component
		{
			var type = typeof(T);
			var idProperty = type.GetProperty("ID");
			if (idProperty != null)
			{
				return idProperty.GetValue(component) as string;
			}
			else
			{
				// Attempt to get a field named 'ID'
				var idField = type.GetField("ID");
				if (idField != null)
				{
					return idField.GetValue(component) as string;
				}
			}

			Runtime.Logger.Log($"Component of type '{type.Name}' does not have an 'ID' property or field.");
			return string.Empty;
		}

		/// <summary>
		/// Finds the SerializedProperty for 'ID' in the given SerializedObject.
		/// </summary>
		/// <param name="serializedObject">The SerializedObject to search.</param>
		/// <returns>The SerializedProperty for 'ID' if found; otherwise, null.</returns>
		private static SerializedProperty FindIDProperty(SerializedObject serializedObject)
		{
			SerializedProperty idProp = serializedObject.FindProperty("ID");
			if (idProp != null)
				return idProp;

			// Attempt to find a field named 'ID' if property not found
			idProp = serializedObject.FindProperty("id");
			return idProp;
		}
	}
}
#endif
