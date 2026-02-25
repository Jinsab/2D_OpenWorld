#if MEMORYPACK && ARAWN_REMEMBERME
using System;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Interface for converting legacy data types to current data types.
	/// </summary>
	public interface ILegacyConvertible<T>
	{
		/// <summary>
		/// Converts the legacy data to the current data type.
		/// </summary>
		/// <returns>An instance of the current data type.</returns>
		T ConvertToCurrent();
	}
}
#endif