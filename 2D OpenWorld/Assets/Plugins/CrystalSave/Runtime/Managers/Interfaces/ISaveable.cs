#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
	public interface ISaveable
	{
		string UniqueIdentifier { get; } // Composite ID: GameObjectUniqueID_ComponentID
		byte[] SaveData();
		void LoadData(byte[] data);
	}
}
#endif