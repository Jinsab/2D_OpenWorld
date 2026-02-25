using System;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Provides the root directory used for persistent storage.
    /// </summary>
    public interface IStoragePathProvider
    {
        /// <summary>
        /// Returns the root path for persistent data. Implementation must ensure the
        /// directory exists before returning.
        /// </summary>
        string GetRootPath();
    }
}
