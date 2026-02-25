using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public static class DestroyHelper
	{
		public static void DestroyWithLogging(GameObject obj, string reason)
		{
			Logger.Log($"Destroying GameObject '{obj.name}' due to: {reason}", LogCategory.Other, LogLevel.Info);
			UnityEngine.Object.Destroy(obj);
		}
	}
}

