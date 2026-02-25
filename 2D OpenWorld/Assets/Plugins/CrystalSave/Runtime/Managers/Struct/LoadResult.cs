namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Represents the result of a load operation.
	/// </summary>
	public struct LoadResult
	{
		/// <summary>
		/// Indicates whether the load operation was successful.
		/// </summary>
		public bool Success { get; set; }

		/// <summary>
		/// Contains the error message if the load operation failed.
		/// </summary>
		public string ErrorMessage { get; set; }

		/// <summary>
		/// Initializes a new instance of LoadResult.
		/// </summary>
		/// <param name="success">Load success status.</param>
		/// <param name="errorMessage">Error message if failed.</param>
		public LoadResult(bool success, string errorMessage = null)
		{
			Success = success;
			ErrorMessage = errorMessage;
		}
	}
}
