// SaveablePrefab.Visibility.cs
#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
	public partial class SaveablePrefab
	{
		public byte[] GetVisibilitySettings()
		{
			bool hasController = visibilityController;
			var data = hasController
				? visibilityController.CaptureAndSerializeSettings()
				: null;
			return data;
		}

		public void ApplyVisibilitySettings(byte[] data)
		{
			if (visibilityController)
				visibilityController.DeserializeAndStoreSettings(data);
		}
	}
}
#endif