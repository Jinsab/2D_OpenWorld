#if MEMORYPACK
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[MemoryPackable]
	public partial class GameObjectState
	{
		public string UniqueID { get; set; }
		public bool? IsActive { get; set; }

		public GameObjectState() { }

		[MemoryPackConstructor]
		public GameObjectState(string uniqueID, bool? isActive)
		{
			UniqueID = uniqueID;
			IsActive = isActive;
		}
	}
}
#endif