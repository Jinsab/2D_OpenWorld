#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public class SaveDataSerializer
        {
                private static readonly Lazy<SaveDataSerializer> LazyInstance =
                        new Lazy<SaveDataSerializer>(() => new SaveDataSerializer());

                public static SaveDataSerializer Instance => LazyInstance.Value;

                private readonly MemoryPackSerializerOptions options;

#if UNITY_WEBGL
                private SaveDataSerializer()
                {
                        options = MemoryPackSerializerOptions.Default;
                }
#else
                private SaveDataSerializer()
                {
                        // MemoryPack v1.21.4 does not support built-in compression options.
                        // Use default serializer options for non-WebGL platforms as well.
                        options = MemoryPackSerializerOptions.Default;
                }
#endif

                public MemoryPackSerializerOptions Options => options;

		/// <summary>
		/// Serializes an object using MemoryPack.
		/// </summary>
		/// <typeparam name="T">Type of the object.</typeparam>
		/// <param name="data">The object to serialize.</param>
		/// <returns>Serialized byte array, or null if serialization fails.</returns>
		public byte[] Serialize<T>(T data) where T : class
		{
			try
			{
                                return MemoryPackSerializer.Serialize(data, options);
			}
			catch (Exception ex)
			{
				Logger.Log($"Serialization failed for type {typeof(T).Name}: {ex.Message}", LogLevel.Error);
				Logger.Log(ex.StackTrace, LogLevel.Error);
				return null;
			}
		}

		/// <summary>
		/// Deserializes a byte array into an object of type T using MemoryPack.
		/// If standard deserialization fails, attempts legacy deserialization paths.
		/// </summary>
		/// <typeparam name="T">Type to deserialize into.</typeparam>
		/// <param name="serializedData">The byte array to deserialize.</param>
		/// <returns>The deserialized object, or null if deserialization fails.</returns>
		public T Deserialize<T>(byte[] serializedData) where T : class
		{
			if (serializedData == null || serializedData.Length == 0)
			{
				Logger.Log($"Deserialize: Serialized data is null or empty for type {typeof(T).Name}.", LogLevel.Warning);
				return null;
			}

			try
			{
                                return MemoryPackSerializer.Deserialize<T>(serializedData, options);
			}
			catch (Exception ex)
			{
				Logger.Log($"Deserialization failed for type {typeof(T).Name}: {ex.Message}", LogLevel.Warning);
				// Attempt legacy deserialization
				T legacyConverted = AttemptLegacyDeserialization<T>(serializedData);
				if (legacyConverted != null)
				{
					Logger.Log($"Successfully deserialized and converted legacy data for type {typeof(T).Name}.", LogLevel.Info);
					return legacyConverted;
				}
				else
				{
					// Note: This can occur during verification/overwrite probes where multiple formats are tried.
					// Demote to Info to avoid misleading error noise in normal flows.
					Logger.Log($"No matching legacy format for type {typeof(T).Name} during deserialization probe.", LogLevel.Info);
					return null;
				}
			}
		}

		/// <summary>
		/// Attempts to deserialize using legacy types and convert to the current type.
		/// </summary>
		/// <typeparam name="T">Current type to convert to.</typeparam>
		/// <param name="serializedData">Serialized byte array.</param>
		/// <returns>Converted object of type T, or null if conversion fails.</returns>
                private T AttemptLegacyDeserialization<T>(byte[] serializedData) where T : class
                {
                        Type currentType = typeof(T);

                        // Only attempt legacy string conversion if the data doesn't look like current MemoryPack format
                        if ((currentType == typeof(StringWrapper) || currentType == typeof(TypedObject)) &&
                            !IsCurrentMemoryPackFormat(serializedData))
                        {
                                if (LegacyStringWrapper.TryConvert(serializedData, options, out var legacyStringWrapper))
                                {
                                        return legacyStringWrapper as T;
                                }
                        }

                        List<Type> legacyTypes = TypeRegistry.GetLegacyTypes(currentType);

			foreach (var legacyType in legacyTypes)
			{
				try
				{
					Logger.Log($"Attempting legacy deserialization for type {legacyType.Name}.", LogLevel.Info);
					// Deserialize as legacy type
                                        object legacyObj = MemoryPackSerializer.Deserialize(legacyType, serializedData.AsSpan(), options);

					if (legacyObj is ILegacyConvertible<T> legacyConvertible)
					{
						Logger.Log($"Legacy type {legacyType.Name} successfully converted to {typeof(T).Name}.", LogLevel.Info);
						return legacyConvertible.ConvertToCurrent();
					}
					else
					{
						Logger.Log($"Legacy type {legacyType.Name} does not implement ILegacyConvertible<{typeof(T).Name}>.", LogLevel.Warning);
					}
				}
				catch (Exception ex)
				{
					Logger.Log($"Legacy deserialization failed for type {legacyType.Name}: {ex.Message}", LogLevel.Warning);
					// Continue to next legacy type
				}
			}

			return null;
		}



		/// <summary>
		/// Generic deserialization method using reflection for types without generic support.
		/// </summary>
		/// <param name="type">The type to deserialize into.</param>
		/// <param name="serializedData">The byte array to deserialize.</param>
		/// <returns>Deserialized object as type object, or null if deserialization fails.</returns>
		public object Deserialize(Type type, byte[] serializedData)
		{
			if (serializedData == null || serializedData.Length == 0)
			{
				Logger.Log($"Deserialize: Serialized data is null or empty for type {type.Name}.", LogLevel.Warning);
				return null;
			}

                        try
                        {
                                var result = MemoryPackSerializer.Deserialize(type, serializedData.AsSpan(), options);
                                return result;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Deserialize: Exception during deserialization for type {type.Name}: {ex.Message}", LogLevel.Warning);
                                // Attempt legacy deserialization
                                object legacyConverted = AttemptLegacyDeserialization(type, serializedData);
                                if (legacyConverted != null)
                                {
                                        Logger.Log($"Successfully deserialized and converted legacy data for type {type.Name}.", LogLevel.Info);
                                        return legacyConverted;
                                }
                                else
                                {
                                        Logger.Log($"Failed to deserialize legacy data for type {type.Name}.", LogLevel.Error);
                                        return null;
                                }
                        }
		}

		/// <summary>
		/// Attempts to deserialize using legacy types and convert to the current type.
		/// </summary>
		/// <param name="currentType">Current type to convert to.</param>
		/// <param name="serializedData">Serialized byte array.</param>
		/// <returns>Converted object, or null if conversion fails.</returns>
                private object AttemptLegacyDeserialization(Type currentType, byte[] serializedData)
                {
                        // Only attempt legacy string conversion if the data doesn't look like current MemoryPack format
                        if ((currentType == typeof(StringWrapper) || currentType == typeof(TypedObject)))
                        {
                                if (!IsCurrentMemoryPackFormat(serializedData))
                                {
                                        if (LegacyStringWrapper.TryConvert(serializedData, options, out var legacyStringWrapper))
                                        {
                                                return legacyStringWrapper;
                                        }
                                }
                        }

                        List<Type> legacyTypes = TypeRegistry.GetLegacyTypes(currentType);

			foreach (var legacyType in legacyTypes)
			{
				try
				{
					// Corrected: Pass Type first, then ReadOnlySpan<byte>
                                        object legacyObj = MemoryPackSerializer.Deserialize(legacyType, serializedData.AsSpan(), options);

					if (legacyObj is ILegacyConvertible<object> legacyConvertible)
					{
						return legacyConvertible.ConvertToCurrent();
					}
				}
				catch (Exception ex)
				{
					Logger.Log($"Legacy deserialization failed for type {legacyType.Name}: {ex.Message}", LogLevel.Warning);
					// Continue to next legacy type
				}
			}

			return null;
		}


		/// <summary>
		/// Serializes an object to a MemoryPack-compatible byte array using generics.
		/// </summary>
		/// <typeparam name="T">Type of the object.</typeparam>
		/// <param name="obj">The object to serialize.</param>
		/// <returns>Serialized byte array, or null if serialization fails.</returns>
		public byte[] SerializeToByteArray<T>(T obj) where T : class
		{
			return Serialize<T>(obj);
		}

		/// <summary>
		/// Deserializes a byte array into an object of the specified generic type using MemoryPack.
		/// </summary>
		/// <typeparam name="T">The type to deserialize into.</typeparam>
		/// <param name="serializedData">The byte array to deserialize.</param>
		/// <returns>Deserialized object, or null if deserialization fails.</returns>
		public T DeserializeFromByteArray<T>(byte[] serializedData) where T : class
		{
			return Deserialize<T>(serializedData);
		}


		/// <summary>
		/// Registers a current type and its legacy types.
		/// </summary>
		/// <param name="currentType">The current type.</param>
		/// <param name="legacyTypes">An array of legacy types.</param>
		public void RegisterLegacyTypes(Type currentType, params Type[] legacyTypes)
		{
			TypeRegistry.RegisterLegacyTypes(currentType, legacyTypes);
		}

		/// <summary>
		/// Checks if the serialized data appears to be in current MemoryPack format
		/// by attempting a quick deserialize check.
		/// </summary>
		/// <param name="serializedData">The serialized data to check.</param>
		/// <returns>True if it appears to be current MemoryPack format, false otherwise.</returns>
		private bool IsCurrentMemoryPackFormat(byte[] serializedData)
		{
			if (serializedData == null || serializedData.Length == 0)
			{
				return false;
			}

			// MemoryPack union types start with a union tag encoded as a varint
			// StringWrapper is union tag 1, so data should start with 0x01 for StringWrapper
			// or other small numbers for other TypedObject unions
			try
			{
				if (serializedData.Length >= 1)
				{
					byte firstByte = serializedData[0];
					
					// Check if it looks like a MemoryPack union tag
					// Union tags should be small positive integers (0-50 is reasonable)
					if (firstByte <= 50)
					{
						// Try TypedObject deserialization as the definitive test
						// (don't try specific types like StringWrapper as they may fail due to union handling)
						try
						{
							var testResult = MemoryPackSerializer.Deserialize<TypedObject>(serializedData.AsSpan(), options);
							return testResult != null;
						}
						catch
						{
							return false;
						}
					}
				}
			}
			catch
			{
				// If any error occurs during the check, assume it's not current format
			}

			return false;
		}
        }
}
#endif
