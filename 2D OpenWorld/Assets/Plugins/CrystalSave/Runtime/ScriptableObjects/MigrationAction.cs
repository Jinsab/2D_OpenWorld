#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public abstract class MigrationAction : ScriptableObject
	{
		/// <summary>
		/// Applies the migration logic to the provided SaveData.
		/// </summary>
		/// <param name="data">The SaveData instance to migrate.</param>
		public abstract void ApplyMigration(SaveData data);
	}
}
#endif