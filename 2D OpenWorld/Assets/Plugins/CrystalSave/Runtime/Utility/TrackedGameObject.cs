// TrackedGameObject.cs
#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Represents a GameObject being tracked by SaveManager.
	/// </summary>
	public class TrackedGameObject
	{
		public string UniqueID { get; private set; }
		public GameObject GameObject { get; private set; }
		public GameObjectPropertySettings Settings { get; private set; }
		private List<SaveableComponent> components = new List<SaveableComponent>();


		public TrackedGameObject(GameObject gameObject, GameObjectPropertySettings settings)
		{
			GameObject = gameObject;
			Settings = settings;
			UniqueID = gameObject.GetComponent<UniqueID>()?.ID;
		}

		public void AddComponent(SaveableComponent component)
		{
			if (!components.Contains(component))
			{
				components.Add(component);
			}
		}

		public void RemoveComponent(SaveableComponent component)
		{
			if (components.Contains(component))
			{
				components.Remove(component);
			}
		}

		public bool IsEmpty()
		{
			return components.Count == 0;
		}

		public IReadOnlyList<SaveableComponent> GetComponentsList()
		{
			return components.AsReadOnly();
		}
	}
}
#endif