#if MEMORYPACK
using MemoryPack;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[MemoryPackable]
	public partial class UniqueMaterialData
	{
		public string MaterialName { get; set; }
		public Color Color { get; set; }

		public UniqueMaterialData() { }
	}
}
#endif