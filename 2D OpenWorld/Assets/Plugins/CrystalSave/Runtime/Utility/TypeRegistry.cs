using System;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Registry mapping current types to their legacy counterparts.
	/// </summary>
	public static class TypeRegistry
	{
		// Dictionary mapping current types to a list of their legacy types
		private static readonly Dictionary<Type, List<Type>> legacyTypeMap = new Dictionary<Type, List<Type>>();

		/// <summary>
		/// Registers a current type and its legacy types.
		/// </summary>
		/// <param name="currentType">The current type.</param>
		/// <param name="legacyTypes">An array of legacy types.</param>
		public static void RegisterLegacyTypes(Type currentType, params Type[] legacyTypes)
		{
			if (!legacyTypeMap.ContainsKey(currentType))
			{
				legacyTypeMap[currentType] = new List<Type>();
			}

			foreach (var legacyType in legacyTypes)
			{
				if (!legacyTypeMap[currentType].Contains(legacyType))
				{
					legacyTypeMap[currentType].Add(legacyType);
				}
			}
		}

		/// <summary>
		/// Retrieves the list of legacy types for a given current type.
		/// </summary>
		/// <param name="currentType">The current type.</param>
		/// <returns>A list of legacy types.</returns>
		public static List<Type> GetLegacyTypes(Type currentType)
		{
			if (legacyTypeMap.TryGetValue(currentType, out var legacyTypes))
			{
				return legacyTypes;
			}

			return new List<Type>();
		}
	}
}
