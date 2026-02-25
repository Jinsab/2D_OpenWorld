// IPoolableSaveable.cs
// �2025 Arawn Crystal SaveMe
namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Implemented by objects that must refresh their Crystal Save identity
	/// whenever they are taken from / returned to an object-pool.
	/// </summary>
	public interface IPoolableSaveable
	{
		/// Called *immediately before* the instance becomes active.
		void OnBeforeSpawn();

		/// Called *immediately before* the instance goes back to the pool.
		void OnBeforeDespawn();
	}
}