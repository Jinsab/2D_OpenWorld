#if MEMORYPACK
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[MemoryPackable]
	public partial class SerializedSaveableData
	{
		public string UniqueIdentifier { get; set; }
		public byte[] BinaryData { get; set; }

		public SerializedSaveableData() { }

		[MemoryPackConstructor]
		public SerializedSaveableData(string uniqueIdentifier, byte[] binaryData)
		{
			UniqueIdentifier = uniqueIdentifier;
			BinaryData = binaryData;
		}
	}
}
#endif