#if MEMORYPACK
using MemoryPack;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
	[MemoryPackable]
	public partial class SaveDataEntry
	{
		public string UniqueID { get; set; } // GameObject's UniqueID
		public List<SerializedSaveableData> DataEntries { get; set; }
		public string ParentID { get; set; } // Unique identifier of the parent GameObject (if any)
		public bool IsParentSceneObject { get; set; } // Indicates if the parent is a Scene GameObject

		public SaveDataEntry()
		{
			DataEntries = new List<SerializedSaveableData>();
		}

		[MemoryPackConstructor]
		public SaveDataEntry(string uniqueID, List<SerializedSaveableData> dataEntries, string parentID, bool isParentSceneObject)
		{
			UniqueID = uniqueID;
			DataEntries = dataEntries ?? new List<SerializedSaveableData>();
			ParentID = parentID;
			IsParentSceneObject = isParentSceneObject;
		}
	}
}
#endif