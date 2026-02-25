#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(
		fileName = "MigrateComponentKeyMappings",
		menuName = "Crystal Save/Create Migration Actions/Migrate Component Key Mappings (No-Code)")]
	public class MigrateComponentKeyMappings : MigrationAction
	{
		[System.Serializable]
		public class KeyMappingEntry
		{
			[Tooltip("Old component key from existing saves. Format: GameObjectUniqueID_ComponentID")]
			public string oldKey;

			[Tooltip("New component key in the current build. Leave empty to remove old key data.")]
			public string newKey;

			[Tooltip("Optional note for this mapping.")]
			public string note;
		}

		[Header("Mappings")]
		[Tooltip("List of old-to-new component key mappings.")]
		public List<KeyMappingEntry> mappings = new List<KeyMappingEntry>();

		[Header("Behavior")]
		[Tooltip("When true, existing target keys are overwritten. When false, conflicting targets are skipped.")]
		public bool overwriteExistingTarget;

		[Tooltip("When true, old keys are removed after successful copy/remap.")]
		public bool removeSourceAfterCopy = true;

		[Tooltip("When true, ComponentMetadata is migrated alongside ComponentsData.")]
		public bool syncComponentMetadata = true;

		[Tooltip("When true, entries with empty new key delete old keys (cleanup mode).")]
		public bool removeWhenNewKeyEmpty = true;

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateComponentKeyMappings: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (mappings == null || mappings.Count == 0)
			{
				Logger.Log("MigrateComponentKeyMappings: No mappings provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			if (data.ComponentsData == null)
				data.ComponentsData = new Dictionary<string, byte[]>();

			if (syncComponentMetadata && data.ComponentMetadata == null)
				data.ComponentMetadata = new Dictionary<string, ComponentDataMetadata>();

			int movedDataCount = 0;
			int movedMetadataCount = 0;
			int removedDataCount = 0;
			int removedMetadataCount = 0;
			int skippedMissingCount = 0;
			int skippedInvalidCount = 0;
			int skippedConflictCount = 0;

			for (int i = 0; i < mappings.Count; i++)
			{
				KeyMappingEntry entry = mappings[i];
				if (entry == null)
				{
					Logger.Log($"MigrateComponentKeyMappings: Entry #{i + 1} is null. Skipping.", LogLevel.Warning);
					skippedInvalidCount++;
					continue;
				}

				string oldKey = NormalizeKey(entry.oldKey);
				string newKey = NormalizeKey(entry.newKey);

				if (string.IsNullOrEmpty(oldKey))
				{
					Logger.Log($"MigrateComponentKeyMappings: Entry #{i + 1} has empty old key. Skipping.", LogLevel.Warning);
					skippedInvalidCount++;
					continue;
				}

				bool hasOldData = data.ComponentsData.TryGetValue(oldKey, out byte[] oldData);
				ComponentDataMetadata oldMetadata = null;
				bool hasOldMetadata = syncComponentMetadata &&
				                      data.ComponentMetadata != null &&
				                      data.ComponentMetadata.TryGetValue(oldKey, out oldMetadata);

				if (!hasOldData && !hasOldMetadata)
				{
					Logger.Log($"MigrateComponentKeyMappings: Old key '{oldKey}' not found in save data. Skipping.", LogLevel.Info);
					skippedMissingCount++;
					continue;
				}

				if (string.IsNullOrEmpty(newKey))
				{
					if (!removeWhenNewKeyEmpty)
					{
						Logger.Log($"MigrateComponentKeyMappings: Entry #{i + 1} has empty new key and removeWhenNewKeyEmpty is disabled. Skipping.", LogLevel.Warning);
						skippedInvalidCount++;
						continue;
					}

					if (hasOldData && data.ComponentsData.Remove(oldKey))
					{
						removedDataCount++;
						Logger.Log($"MigrateComponentKeyMappings: Removed component data key '{oldKey}'.", LogLevel.Info);
					}

					if (hasOldMetadata && data.ComponentMetadata.Remove(oldKey))
					{
						removedMetadataCount++;
						Logger.Log($"MigrateComponentKeyMappings: Removed metadata key '{oldKey}'.", LogLevel.Info);
					}

					continue;
				}

				if (string.Equals(oldKey, newKey, System.StringComparison.Ordinal))
				{
					Logger.Log($"MigrateComponentKeyMappings: Entry #{i + 1} maps '{oldKey}' to itself. Skipping.", LogLevel.Info);
					skippedInvalidCount++;
					continue;
				}

				bool movedData = false;
				bool movedMetadata = false;

				if (hasOldData)
				{
					bool targetExists = data.ComponentsData.ContainsKey(newKey);
					if (targetExists && !overwriteExistingTarget)
					{
						Logger.Log($"MigrateComponentKeyMappings: Target data key '{newKey}' already exists. Skipping data move for '{oldKey}'.", LogLevel.Warning);
						skippedConflictCount++;
					}
					else
					{
						data.ComponentsData[newKey] = oldData;
						movedData = true;
						movedDataCount++;
						Logger.Log($"MigrateComponentKeyMappings: Mapped data key '{oldKey}' -> '{newKey}'.", LogLevel.Info);
					}
				}

				if (hasOldMetadata)
				{
					bool targetMetadataExists = data.ComponentMetadata.ContainsKey(newKey);
					if (targetMetadataExists && !overwriteExistingTarget)
					{
						Logger.Log($"MigrateComponentKeyMappings: Target metadata key '{newKey}' already exists. Skipping metadata move for '{oldKey}'.", LogLevel.Warning);
						skippedConflictCount++;
					}
					else
					{
						data.ComponentMetadata[newKey] = oldMetadata;
						movedMetadata = true;
						movedMetadataCount++;
						Logger.Log($"MigrateComponentKeyMappings: Mapped metadata key '{oldKey}' -> '{newKey}'.", LogLevel.Info);
					}
				}

				if (!removeSourceAfterCopy)
					continue;

				if (movedData && data.ComponentsData.Remove(oldKey))
				{
					removedDataCount++;
				}

				if (movedMetadata && data.ComponentMetadata.Remove(oldKey))
				{
					removedMetadataCount++;
				}
			}

			Logger.Log(
				$"MigrateComponentKeyMappings: Done. " +
				$"MovedData={movedDataCount}, MovedMetadata={movedMetadataCount}, " +
				$"RemovedData={removedDataCount}, RemovedMetadata={removedMetadataCount}, " +
				$"SkippedMissing={skippedMissingCount}, SkippedInvalid={skippedInvalidCount}, SkippedConflicts={skippedConflictCount}.",
				LogLevel.Info);
		}

		private static string NormalizeKey(string value)
		{
			return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
		}
	}
}
#endif
