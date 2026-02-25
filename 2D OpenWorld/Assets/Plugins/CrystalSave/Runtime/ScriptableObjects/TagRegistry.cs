using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "TagRegistry", menuName = "Crystal Save/Settings/Tag Registry")]
	public class TagRegistry : ScriptableObject
	{
		[Tooltip("List of valid tags in the project.")]
		public List<string> Tags = new List<string>();
	}
}
