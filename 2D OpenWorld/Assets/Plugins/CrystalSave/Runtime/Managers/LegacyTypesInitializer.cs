#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public static class LegacyTypesInitializer
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void InitializeSerializer()
		{
			RegisterLegacyTypes();
		}

		private static void RegisterLegacyTypes()
		{
			var serializer = SaveDataSerializer.Instance;

			// Register legacy types for GameObjectData
			serializer.RegisterLegacyTypes(
				typeof(GameObjectData),
				typeof(LegacyGameObjectData)
			// Add more legacy types as needed
			);

			// Register legacy types for TransformData
			serializer.RegisterLegacyTypes(
				typeof(TransformData),
				typeof(LegacyTransformData)
			);

			// Register legacy types for RememberParent
			serializer.RegisterLegacyTypes(
				typeof(RememberParent),
				typeof(LegacyRememberParentData)
			);

			// Register legacy type for SaveablePrefabData
			serializer.RegisterLegacyTypes(
				typeof(SaveablePrefabData),
				typeof(LegacySaveablePrefabData)
			);

			// Register legacy types for other data classes similarly
			// serializer.RegisterLegacyTypes(typeof(OtherData), typeof(OtherDataLegacyV1), ...);

			Logger.Log("SerializerInitializer: Registered all legacy types with SaveDataSerializer.", LogCategory.Serialization, LogLevel.Off);
		}
	}
	partial class LegacyRememberParentData
	{ }
	partial class LegacyTransformData
	{ }
}



#endif