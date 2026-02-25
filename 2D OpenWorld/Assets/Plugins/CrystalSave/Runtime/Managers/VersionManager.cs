#if MEMORYPACK && ARAWN_REMEMBERME
using System;

namespace Arawn.CrystalSave.Runtime
{
	public enum VersionComparisonResult
	{
		Newer,
		Older,
		Equal,
		Incompatible
	}

	public class VersionManager
	{
		public VersionData CurrentVersion { get; }

		public VersionManager(int major, int minor, int patch)
		{
			CurrentVersion = new VersionData(major, minor, patch);
		}

		public VersionManager(SaveSettings settings)
		{
			if (settings == null || settings.version == null)
				throw new ArgumentNullException(nameof(settings), "SaveSettings or CurrentVersion cannot be null.");

			CurrentVersion = settings.version;
		}

		public VersionComparisonResult CompareVersion(VersionData dataVersion)
		{
			if (dataVersion == null)
				return VersionComparisonResult.Incompatible;

			int comparison = dataVersion.CompareTo(CurrentVersion);

			if (comparison > 0)
				return VersionComparisonResult.Newer;
			if (comparison < 0)
				return VersionComparisonResult.Older;
			return VersionComparisonResult.Equal;
		}

	}
}
#endif