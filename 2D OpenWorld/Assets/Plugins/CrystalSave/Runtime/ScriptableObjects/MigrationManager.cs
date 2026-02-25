#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrationManager", menuName = "Crystal Save/Settings/Create Migration Manager")]
	public class MigrationManager : ScriptableObject
	{
		[SerializeField]
		private List<MigrationStep> migrationSteps = new List<MigrationStep>();

		/// <summary>
		/// Applies all necessary migration steps to the provided SaveData.
		/// </summary>
		/// <param name="data">The SaveData instance to migrate.</param>
		public void Migrate(SaveData data)
		{
			foreach (var step in migrationSteps)
			{
				if (data.Version.CompareTo(step.targetVersion) < 0)
				{
					foreach (var action in step.migrationActions)
					{
						if (action != null)
						{
							action.ApplyMigration(data);
							Debug.Log($"MigrationManager: Applied action '{action.name}' for version {step.targetVersion}.");
						}
						else
						{
							Debug.LogWarning($"MigrationManager: Encountered a null MigrationAction in step targeting version {step.targetVersion}.");
						}
					}
					data.Version = step.targetVersion.Clone() as VersionData;
					Debug.Log($"MigrationManager: Updated SaveData version to {data.Version}.");
				}
			}
		}
	}

	[System.Serializable]
	public class MigrationStep
	{
		public VersionData targetVersion;
		public string description;
		public List<MigrationAction> migrationActions = new List<MigrationAction>();
	}
}
#endif