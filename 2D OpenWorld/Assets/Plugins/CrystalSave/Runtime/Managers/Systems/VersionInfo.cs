#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;

namespace Arawn.CrystalSave.Runtime
{
	[System.Serializable]
	[MemoryPackable]
	public partial class VersionData : IComparable<VersionData>
	{
		[MemoryPackInclude]
		public int Major; // Public field for Unity serialization
		[MemoryPackInclude]
		public int Minor; // Public field for Unity serialization
		[MemoryPackInclude]
		public int Patch; // Public field for Unity serialization

		// Properties for MemoryPack serialization
		[MemoryPackIgnore] // Instruct MemoryPack to ignore these properties
		public int MajorProperty { get => Major; set => Major = value; }
		[MemoryPackIgnore]
		public int MinorProperty { get => Minor; set => Minor = value; }
		[MemoryPackIgnore]
		public int PatchProperty { get => Patch; set => Patch = value; }

		public VersionData() { }

		[MemoryPackConstructor]
		public VersionData(int major, int minor, int patch)
		{
			Major = major;
			Minor = minor;
			Patch = patch;
		}



		public int CompareTo(VersionData other)
		{
			if (other == null) return 1;

			int majorComparison = Major.CompareTo(other.Major);
			if (majorComparison != 0) return majorComparison;

			int minorComparison = Minor.CompareTo(other.Minor);
			if (minorComparison != 0) return minorComparison;

			return Patch.CompareTo(other.Patch);
		}

		public override string ToString()
		{
			return $"{Major}.{Minor}.{Patch}";
		}

		public static VersionData Parse(string versionString)
		{
			if (string.IsNullOrEmpty(versionString))
				return null;

			string[] parts = versionString.Split('.');
			if (parts.Length != 3)
				return null;

			if (int.TryParse(parts[0], out int major) &&
				int.TryParse(parts[1], out int minor) &&
				int.TryParse(parts[2], out int patch))
			{
				return new VersionData(major, minor, patch);
			}

			return null;
		}
		// Overload comparison operators
		public static bool operator <(VersionData a, VersionData b)
		{
			if (a is null) return b is not null;
			return a.CompareTo(b) < 0;
		}

		public static bool operator >(VersionData a, VersionData b)
		{
			if (a is null) return false;
			return a.CompareTo(b) > 0;
		}

		public static bool operator ==(VersionData a, VersionData b)
		{
			if (ReferenceEquals(a, b)) return true;
			if (a is null || b is null) return false;
			return a.Major == b.Major && a.Minor == b.Minor && a.Patch == b.Patch;
		}

		public static bool operator !=(VersionData a, VersionData b)
		{
			return !(a == b);
		}

		public override bool Equals(object obj)
		{
			if (obj is VersionData other)
				return this == other;
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Major, Minor, Patch);
		}

		public object Clone()
		{
			return new VersionData(Major, Minor, Patch);
		}
	}
}
#endif