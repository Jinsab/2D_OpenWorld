#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Arawn.CrystalSave.Runtime
{
    [AddComponentMenu("Crystal Save/Remember Components/Remember Tilemap")]
    [DisallowMultipleComponent]
    [RememberTarget(typeof(Tilemap))]
    [RememberIcon("Tilemap Icon")]
    public class RememberTilemap : SaveableComponent
    {
        private Tilemap tilemap;
        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged;

        private TilemapSnapshot cachedSnapshot;
        private bool hasCachedSnapshot;
        private byte[] cachedSerializedData;
        private static readonly BindingFlags Binding =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        protected override void Awake()
        {
            base.Awake();
            tilemap = GetComponent<Tilemap>();
            if (tilemap == null)
            {
                Logger.Log($"{nameof(RememberTilemap)} requires a Tilemap component on '{gameObject.name}'.", LogCategory.RememberTilemap, LogLevel.Error);
                enabled = false;
            }

            if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot, false))
            {
                cachedSnapshot = CloneSnapshot(snapshot);
                hasCachedSnapshot = cachedSnapshot != null;
            }
            else
            {
                cachedSnapshot = null;
                hasCachedSnapshot = false;
            }
        }

        protected override byte[] SerializeComponentData()
        {
            if (!TryCaptureCurrentState(out var snapshot, true))
            {
                return null;
            }

            if (skipSavingWhenUnchanged && hasCachedSnapshot && cachedSnapshot != null)
            {
                if (AreEquivalent(cachedSnapshot, snapshot))
                {
                    if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                    {
                        return cachedSerializedData;
                    }
                }
            }

            var serialized = SaveDataSerializer.Instance.Serialize(snapshot);

            if (skipSavingWhenUnchanged)
            {
                cachedSnapshot = CloneSnapshot(snapshot);
                hasCachedSnapshot = cachedSnapshot != null;
                cachedSerializedData = serialized;
            }

            return serialized;
        }

        protected override void DeserializeComponentData(byte[] data)
        {
            if (data == null || data.Length == 0 || tilemap == null) return;

            var snapshot = SaveDataSerializer.Instance.Deserialize<TilemapSnapshot>(data);
            if (snapshot == null || snapshot.Members == null) return;

            Type type = typeof(Tilemap);

            foreach (var member in snapshot.Members)
            {
                object value = member.Value?.GetValue();
                if (member.IsProperty)
                {
                    PropertyInfo prop = type.GetProperty(member.Name, Binding);
                    if (prop == null || !prop.CanWrite) continue;
                    if (value != null || !prop.PropertyType.IsValueType)
                    {
                        value = ConvertValueToMemberType(value, prop.PropertyType);
                        prop.SetValue(tilemap, value);
                    }
                }
                else
                {
                    FieldInfo field = type.GetField(member.Name, Binding);
                    if (field == null) continue;
                    if (value != null || !field.FieldType.IsValueType)
                    {
                        value = ConvertValueToMemberType(value, field.FieldType);
                        field.SetValue(tilemap, value);
                    }
                }
            }

            if (skipSavingWhenUnchanged)
            {
                cachedSnapshot = CloneSnapshot(snapshot);
                hasCachedSnapshot = cachedSnapshot != null;
            }
        }

        private bool TryCaptureCurrentState(out TilemapSnapshot snapshot, bool logWarnings)
        {
            snapshot = null;

            if (tilemap == null)
            {
                if (logWarnings)
                {
                    Logger.Log($"{nameof(RememberTilemap)} could not capture state because the Tilemap component is missing on '{gameObject.name}'.", LogCategory.RememberTilemap, LogLevel.Warning);
                }
                return false;
            }

            var tempSnapshot = new TilemapSnapshot();
            Type type = typeof(Tilemap);

            foreach (var field in type.GetFields(Binding))
            {
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                    continue;

                object value = null;
                try { value = field.GetValue(tilemap); }
                catch { continue; }
                if (value is UnityEngine.Object obj && obj == null) continue;

                var wrapper = TypedObjectFactory.CreateTypedObject(value);
                if (wrapper != null)
                {
                    tempSnapshot.Members.Add(new TilemapMember
                    {
                        Name = field.Name,
                        Value = wrapper,
                        IsProperty = false
                    });
                }
            }

            foreach (var prop in type.GetProperties(Binding))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                object value = null;
                try { value = prop.GetValue(tilemap); }
                catch { continue; }
                if (value is UnityEngine.Object obj && obj == null) continue;

                var wrapper = TypedObjectFactory.CreateTypedObject(value);
                if (wrapper != null)
                {
                    tempSnapshot.Members.Add(new TilemapMember
                    {
                        Name = prop.Name,
                        Value = wrapper,
                        IsProperty = true
                    });
                }
            }

            if (tempSnapshot.Members.Count == 0)
            {
                if (logWarnings)
                {
                    Logger.Log($"{nameof(RememberTilemap)} captured no serializable members on Tilemap '{gameObject.name}'.", LogCategory.RememberTilemap, LogLevel.Warning);
                }
                return false;
            }

            snapshot = tempSnapshot;
            return true;
        }

        private TilemapSnapshot CloneSnapshot(TilemapSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new TilemapSnapshot();
            if (source.Members == null)
            {
                return clone;
            }

            foreach (var member in source.Members)
            {
                clone.Members.Add(new TilemapMember
                {
                    Name = member.Name,
                    IsProperty = member.IsProperty,
                    Value = CloneTypedObject(member.Value)
                });
            }

            return clone;
        }

        private TypedObject CloneTypedObject(TypedObject source)
        {
            if (source == null)
            {
                return null;
            }

            var serialized = SaveDataSerializer.Instance.Serialize(source);
            if (serialized == null || serialized.Length == 0)
            {
                return null;
            }

            var clone = SaveDataSerializer.Instance.Deserialize(source.GetType(), serialized) as TypedObject;
            return clone;
        }

        private bool AreEquivalent(TilemapSnapshot a, TilemapSnapshot b)
        {
            if (a == null || b == null) return false;
            if (a.Members == null || b.Members == null) return false;
            if (a.Members.Count != b.Members.Count) return false;

            for (int i = 0; i < a.Members.Count; i++)
            {
                var memberA = a.Members[i];
                var memberB = b.Members[i];

                if (!string.Equals(memberA?.Name, memberB?.Name, StringComparison.Ordinal))
                    return false;

                if (memberA?.IsProperty != memberB?.IsProperty)
                    return false;

                if (!AreTypedObjectsEquivalent(memberA?.Value, memberB?.Value))
                    return false;
            }

            return true;
        }

        private bool AreTypedObjectsEquivalent(TypedObject a, TypedObject b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.GetType() != b.GetType()) return false;

            var bytesA = SaveDataSerializer.Instance.Serialize(a);
            var bytesB = SaveDataSerializer.Instance.Serialize(b);

            if (bytesA != null && bytesB != null && bytesA.Length == bytesB.Length)
            {
                bool sequenceEqual = true;
                for (int i = 0; i < bytesA.Length; i++)
                {
                    if (bytesA[i] != bytesB[i])
                    {
                        sequenceEqual = false;
                        break;
                    }
                }

                if (sequenceEqual)
                    return true;
            }

            var valueA = a.GetValue();
            var valueB = b.GetValue();

            return AreValuesApproximatelyEqual(valueA, valueB);
        }

        private bool AreValuesApproximatelyEqual(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            if (a is float fa && b is float fb)
                return Mathf.Approximately(fa, fb);

            if (a is double da && b is double db)
                return Math.Abs(da - db) < 1e-6;

            if (a is Vector2 va2 && b is Vector2 vb2)
                return Vector2.SqrMagnitude(va2 - vb2) < 1e-6f;

            if (a is Vector3 va3 && b is Vector3 vb3)
                return Vector3.SqrMagnitude(va3 - vb3) < 1e-6f;

            if (a is Vector4 va4 && b is Vector4 vb4)
                return Vector4.SqrMagnitude(va4 - vb4) < 1e-6f;

            if (a is Color ca && b is Color cb)
                return Vector4.SqrMagnitude((Vector4)ca - (Vector4)cb) < 1e-6f;

            return Equals(a, b);
        }

        private static object ConvertValueToMemberType(object value, Type memberType)
        {
            if (value == null)
                return null;

            if (value is IList list)
            {
                if (memberType.IsArray)
                {
                    Type elementType = memberType.GetElementType();
                    Array arr = Array.CreateInstance(elementType, list.Count);
                    for (int i = 0; i < list.Count; i++)
                        arr.SetValue(ChangeTypeSafe(list[i], elementType), i);
                    return arr;
                }

                if (typeof(IList).IsAssignableFrom(memberType))
                {
                    Type elementType = typeof(object);
                    if (memberType.IsGenericType)
                        elementType = memberType.GetGenericArguments()[0];

                    IList typedList = (IList)Activator.CreateInstance(memberType);
                    foreach (var item in list)
                        typedList.Add(ChangeTypeSafe(item, elementType));
                    return typedList;
                }
            }

            if (value is IDictionary dict && typeof(IDictionary).IsAssignableFrom(memberType))
            {
                Type keyType = typeof(object);
                Type valType = typeof(object);
                if (memberType.IsGenericType)
                {
                    var args = memberType.GetGenericArguments();
                    if (args.Length == 2)
                    {
                        keyType = args[0];
                        valType = args[1];
                    }
                }

                IDictionary typedDict = (IDictionary)Activator.CreateInstance(memberType);
                foreach (DictionaryEntry entry in dict)
                {
                    var k = ChangeTypeSafe(entry.Key, keyType);
                    var v = ChangeTypeSafe(entry.Value, valType);
                    typedDict[k] = v;
                }
                return typedDict;
            }

            return value;
        }

        private static object ChangeTypeSafe(object value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }
    }

    [MemoryPackable]
    public partial class TilemapMember
    {
        public string Name { get; set; }
        public TypedObject Value { get; set; }
        public bool IsProperty { get; set; }
    }

    [MemoryPackable]
    public partial class TilemapSnapshot
    {
        public List<TilemapMember> Members { get; set; }

        [MemoryPackConstructor]
        public TilemapSnapshot()
        {
            Members = new List<TilemapMember>();
        }
    }
}
#endif
