#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Generic component that saves and restores custom MonoBehaviour fields.
    /// </summary>
    [AddComponentMenu("Crystal Save/Remember Components/Remember Custom Components")]
    [DisallowMultipleComponent]
    public class RememberCustomComponents : SaveableComponent
    {
        private const string PropertyPrefix = "#PROP#:";

        private const float FloatTolerance = 0.0001f;
        private const float VectorToleranceSqr = 0.0001f;

        public enum SerializationMode
        {
            AllCustomComponents,
            SelectedComponents
        }

        [SerializeField, HideInInspector]
        private SerializationMode mode = SerializationMode.AllCustomComponents;

        [SerializeField, HideInInspector]
        private List<MonoBehaviour> components = new();

        [SerializeField, Tooltip("Wait this many seconds before applying restored values to the target components.\nUse this when other scripts overwrite values in Awake/Start/OnEnable (e.g., health reset).")]
        private float restoreDelaySeconds = 0f;

        [SerializeField, Tooltip("Also serialize public properties with both getter and setter. This is useful for third-party components that expose data via properties (e.g., CurrentHealth). Use with care: only simple, side-effect-free properties are recommended.")]
        private bool includePublicProperties = false;

        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged = false;

        private List<ComponentSnapshot> cachedSnapshot;
        private bool hasCachedSnapshot;
        private byte[] cachedSerializedData;

        protected override void Awake()
        {
            base.Awake();

            if (skipSavingWhenUnchanged)
            {
                if (TryCaptureCurrentState(out var snapshot, logWarnings: false))
                {
                    CacheSnapshot(snapshot);
                }
                else
                {
                    hasCachedSnapshot = false;
                    cachedSnapshot = null;
                }
            }
            else
            {
                hasCachedSnapshot = false;
                cachedSnapshot = null;
            }
        }

        protected override byte[] SerializeComponentData()
        {
            if (!TryCaptureCurrentState(out var snapshots, logWarnings: true))
                return null;

            if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshots))
            {
                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                {
                    return cachedSerializedData;
                }
            }

            var serialized = Serializer.Serialize(snapshots);

            if (skipSavingWhenUnchanged)
            {
                CacheSnapshot(snapshots);
                cachedSerializedData = serialized;
            }

            return serialized;
        }

        protected override void DeserializeComponentData(byte[] data)
        {
            var snapshots = Serializer.Deserialize<List<ComponentSnapshot>>(data);
            if (snapshots == null) return;

            if (restoreDelaySeconds > 0f)
            {
                StartCoroutine(ApplySnapshotsAfterDelay(snapshots, restoreDelaySeconds));
            }
            else
            {
                ApplySnapshotsNow(snapshots);
            }
        }

        private IEnumerator ApplySnapshotsAfterDelay(List<ComponentSnapshot> snapshots, float delaySeconds)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSecondsRealtime(delaySeconds);
            ApplySnapshotsNow(snapshots);
        }

        private void ApplySnapshotsNow(List<ComponentSnapshot> snapshots)
        {
            foreach (var snap in snapshots)
            {
                var type = Type.GetType(snap.ComponentType);
                if (type == null) continue;
                var comp = GetComponent(type) as MonoBehaviour;
                if (comp == null)
                    comp = gameObject.AddComponent(type) as MonoBehaviour;
                if (comp == null) continue;

                foreach (var fieldSnap in snap.Fields)
                {
                    // Detect property snapshots encoded with prefix
                    if (!string.IsNullOrEmpty(fieldSnap.FieldName) && fieldSnap.FieldName.StartsWith(PropertyPrefix, StringComparison.Ordinal))
                    {
                        string propName = fieldSnap.FieldName.Substring(PropertyPrefix.Length);
                        var p = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                        if (p == null || !p.CanWrite) continue;
                        object value = fieldSnap.Value?.GetValue();
                        if (value != null || !p.PropertyType.IsValueType)
                        {
                            value = ConvertValueToFieldType(value, p.PropertyType);
                            try
                            {
                                p.SetValue(comp, value);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"RememberCustomComponents: Could not set property '{type.Name}.{propName}': {ex.Message}", LogLevel.Warning);
                            }
                        }
                        continue;
                    }

                    FieldInfo f = type.GetField(fieldSnap.FieldName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f == null) continue;
                    object valueField = fieldSnap.Value?.GetValue();
                    if (valueField != null || !f.FieldType.IsValueType)
                    {
                        valueField = ConvertValueToFieldType(valueField, f.FieldType);
                        f.SetValue(comp, valueField);
                    }
                }
            }

            if (skipSavingWhenUnchanged)
            {
                RefreshCacheFromCurrentState();
            }
        }

        private static object ConvertValueToFieldType(object value, Type fieldType)
        {
            if (value == null)
                return null;

            // Convert IList → array or generic list
            if (value is IList list)
            {
                if (fieldType.IsArray)
                {
                    Type elementType = fieldType.GetElementType();
                    Array arr = Array.CreateInstance(elementType, list.Count);
                    for (int i = 0; i < list.Count; i++)
                    {
                        arr.SetValue(ChangeTypeSafe(list[i], elementType), i);
                    }
                    return arr;
                }

                if (typeof(IList).IsAssignableFrom(fieldType))
                {
                    Type elementType = typeof(object);
                    if (fieldType.IsGenericType)
                        elementType = fieldType.GetGenericArguments()[0];

                    IList typedList = (IList)Activator.CreateInstance(fieldType);
                    foreach (var item in list)
                        typedList.Add(ChangeTypeSafe(item, elementType));
                    return typedList;
                }
            }

            // Convert IDictionary → generic dictionary
            if (value is IDictionary dict && typeof(IDictionary).IsAssignableFrom(fieldType))
            {
                Type keyType = typeof(object);
                Type valType = typeof(object);
                if (fieldType.IsGenericType)
                {
                    var args = fieldType.GetGenericArguments();
                    if (args.Length == 2)
                    {
                        keyType = args[0];
                        valType = args[1];
                    }
                }

                IDictionary typedDict = (IDictionary)Activator.CreateInstance(fieldType);
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

        private IEnumerable<MonoBehaviour> GetTargets()
        {
            if (mode == SerializationMode.SelectedComponents)
                return components.Where(c => c != null);
            else
                return GetComponents<MonoBehaviour>().Where(IsCustomComponent);
        }

        public static bool IsCustomComponent(MonoBehaviour comp)
        {
            if (comp == null) return false;

            Type type = comp.GetType();

            string asmName = type.Assembly.GetName().Name;
            if (asmName.StartsWith("UnityEngine"))
                return false;

            if (typeof(SaveableComponent).IsAssignableFrom(type))
                return false;

            if (typeof(SaveablePrefab).IsAssignableFrom(type))
                return false;

            if (type == typeof(RememberComposite))
                return false;

            if (type.FullName.StartsWith("Arawn.CrystalSave") &&
                (type.Name == nameof(UniqueID) ||
                 type.Name == nameof(TrackedGameObjectProxy) ||
                 type.Name == nameof(PersistentVisibilityController)))
                return false;

            return true;
        }

        private bool TryCaptureCurrentState(out List<ComponentSnapshot> snapshot, bool logWarnings)
        {
            snapshot = null;

            var targets = GetTargets()?.Where(t => t != null).ToList();
            if (targets == null || targets.Count == 0)
            {
                return false;
            }

            List<ComponentSnapshot> snapshots = new();
            bool hasAnyField = false;

            foreach (var comp in targets)
            {
                var componentSnapshot = SerializeComponent(comp, logWarnings);
                snapshots.Add(componentSnapshot);

                if (componentSnapshot.Fields != null && componentSnapshot.Fields.Count > 0)
                {
                    hasAnyField = true;
                }
            }

            if (!hasAnyField)
            {
                return false;
            }

            snapshot = snapshots;
            return true;
        }

        private ComponentSnapshot SerializeComponent(MonoBehaviour comp, bool logWarnings)
        {
            var snap = new ComponentSnapshot
            {
                ComponentType = comp.GetType().AssemblyQualifiedName,
                Fields = new List<FieldSnapshot>()
            };

            var fields = comp.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                    continue;

                object value = null;
                try
                {
                    value = field.GetValue(comp);
                }
                catch (Exception ex)
                {
                    if (logWarnings)
                        Logger.Log($"RememberCustomComponents: Could not read field '{field.Name}' on component '{comp.GetType().Name}': {ex.Message}", LogLevel.Warning);
                    continue;
                }

                // Skip unassigned UnityEngine.Object fields
                if (value is UnityEngine.Object obj && obj == null)
                {
                    if (logWarnings)
                        Logger.Log($"RememberCustomComponents: Field '{field.Name}' on '{comp.GetType().Name}' is not assigned and will be skipped.", LogLevel.Info);
                    continue;
                }

                TypedObject wrapper = TypedObjectFactory.CreateTypedObject(value);
                if (wrapper != null)
                {
                    snap.Fields.Add(new FieldSnapshot
                    {
                        FieldName = field.Name,
                        Value = wrapper
                    });
                }
                else
                {
                    if (logWarnings)
                        Logger.Log($"RememberCustomComponents: Unsupported field type '{field.FieldType}' for '{comp.GetType().Name}.{field.Name}'. This won't break your save; it just won't be stored.", LogLevel.Warning);
                }
            }

            // Properties (opt-in): serialize public get/set, non-indexer
            if (includePublicProperties)
            {
                var props = comp.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);
                foreach (var prop in props)
                {
                    object value = null;
                    try
                    {
                        value = prop.GetValue(comp);
                    }
                    catch (Exception ex)
                    {
                        if (logWarnings)
                            Logger.Log($"RememberCustomComponents: Could not read property '{prop.Name}' on component '{comp.GetType().Name}': {ex.Message}", LogLevel.Warning);
                        continue;
                    }

                    // Skip unassigned UnityEngine.Object
                    if (value is UnityEngine.Object pobj && pobj == null)
                        continue;

                    var wrapper = TypedObjectFactory.CreateTypedObject(value);
                    if (wrapper != null)
                    {
                        snap.Fields.Add(new FieldSnapshot
                        {
                            FieldName = PropertyPrefix + prop.Name,
                            Value = wrapper
                        });
                    }
                    else
                    {
                        if (logWarnings)
                            Logger.Log($"RememberCustomComponents: Unsupported property type '{prop.PropertyType}' for '{comp.GetType().Name}.{prop.Name}'. Skipping.", LogLevel.Info);
                    }
                }
            }

            return snap;
        }

        private bool AreEquivalent(List<ComponentSnapshot> a, List<ComponentSnapshot> b)
        {
            if (a == null || b == null)
                return false;

            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                var compA = a[i];
                var compB = b[i];

                if (!string.Equals(compA?.ComponentType, compB?.ComponentType, StringComparison.Ordinal))
                    return false;

                var fieldsA = compA?.Fields ?? new List<FieldSnapshot>();
                var fieldsB = compB?.Fields ?? new List<FieldSnapshot>();

                if (fieldsA.Count != fieldsB.Count)
                    return false;

                for (int j = 0; j < fieldsA.Count; j++)
                {
                    var fieldA = fieldsA[j];
                    var fieldB = fieldsB[j];

                    if (!string.Equals(fieldA?.FieldName, fieldB?.FieldName, StringComparison.Ordinal))
                        return false;

                    if (!AreTypedObjectsEquivalent(fieldA?.Value, fieldB?.Value))
                        return false;
                }
            }

            return true;
        }

        private bool AreTypedObjectsEquivalent(TypedObject a, TypedObject b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null)
                return false;

            if (a.Equals(b))
                return true;

            if (a.GetType() != b.GetType())
                return false;

            object aValue = a.GetValue();
            object bValue = b.GetValue();

            return AreValuesEquivalent(aValue, bValue);
        }

        private bool AreValuesEquivalent(object a, object b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null)
                return false;

            if (a is float fa && b is float fb)
                return AreFloatsApproximatelyEqual(fa, fb);

            if (a is double da && b is double db)
                return Math.Abs(da - db) <= FloatTolerance;

            if (a is Vector2 v2a && b is Vector2 v2b)
                return (v2a - v2b).sqrMagnitude <= VectorToleranceSqr;

            if (a is Vector3 v3a && b is Vector3 v3b)
                return (v3a - v3b).sqrMagnitude <= VectorToleranceSqr;

            if (a is Vector4 v4a && b is Vector4 v4b)
                return (v4a - v4b).sqrMagnitude <= VectorToleranceSqr;

            if (a is Quaternion qa && b is Quaternion qb)
                return 1f - Math.Abs(Quaternion.Dot(qa, qb)) <= FloatTolerance;

            if (a is Color ca && b is Color cb)
            {
                Vector4 diff = (Vector4)(ca - cb);
                return diff.sqrMagnitude <= VectorToleranceSqr;
            }

            if (a is IList listA && b is IList listB)
                return AreListsEquivalent(listA, listB);

            if (a is IDictionary dictA && b is IDictionary dictB)
                return AreDictionariesEquivalent(dictA, dictB);

            return a.Equals(b);
        }

        private bool AreListsEquivalent(IList a, IList b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!AreValuesEquivalent(a[i], b[i]))
                    return false;
            }

            return true;
        }

        private bool AreDictionariesEquivalent(IDictionary a, IDictionary b)
        {
            if (a.Count != b.Count)
                return false;

            foreach (DictionaryEntry entry in a)
            {
                if (!b.Contains(entry.Key))
                    return false;

                if (!AreValuesEquivalent(entry.Value, b[entry.Key]))
                    return false;
            }

            return true;
        }

        private bool AreFloatsApproximatelyEqual(float a, float b)
        {
            if (float.IsNaN(a) || float.IsNaN(b))
                return float.IsNaN(a) && float.IsNaN(b);

            if (float.IsInfinity(a) || float.IsInfinity(b))
                return float.IsInfinity(a) == float.IsInfinity(b) && Mathf.Sign(a) == Mathf.Sign(b);

            return Mathf.Abs(a - b) <= FloatTolerance;
        }

        private void CacheSnapshot(List<ComponentSnapshot> snapshot)
        {
            if (snapshot == null)
            {
                cachedSnapshot = null;
                hasCachedSnapshot = false;
                return;
            }

            cachedSnapshot = CloneSnapshot(snapshot);
            hasCachedSnapshot = cachedSnapshot != null;
        }

        private List<ComponentSnapshot> CloneSnapshot(List<ComponentSnapshot> snapshot)
        {
            if (snapshot == null)
                return null;

            try
            {
                var clone = Serializer.Deserialize<List<ComponentSnapshot>>(Serializer.Serialize(snapshot));
                return clone ?? new List<ComponentSnapshot>();
            }
            catch
            {
                var fallback = new List<ComponentSnapshot>(snapshot.Count);
                foreach (var comp in snapshot)
                {
                    var clonedFields = new List<FieldSnapshot>();
                    if (comp.Fields != null)
                    {
                        foreach (var field in comp.Fields)
                        {
                            clonedFields.Add(new FieldSnapshot
                            {
                                FieldName = field.FieldName,
                                Value = field.Value != null ? TypedObjectFactory.CreateTypedObject(field.Value.GetValue()) : null
                            });
                        }
                    }

                    fallback.Add(new ComponentSnapshot
                    {
                        ComponentType = comp.ComponentType,
                        Fields = clonedFields
                    });
                }

                return fallback;
            }
        }

        private void RefreshCacheFromCurrentState()
        {
            if (!skipSavingWhenUnchanged)
            {
                hasCachedSnapshot = false;
                cachedSnapshot = null;
                return;
            }

            if (TryCaptureCurrentState(out var refreshed, logWarnings: false))
            {
                CacheSnapshot(refreshed);
            }
            else
            {
                hasCachedSnapshot = false;
                cachedSnapshot = null;
            }
        }

    }

    [MemoryPackable]
    public partial class ComponentSnapshot
    {
        public string ComponentType { get; set; }
        public List<FieldSnapshot> Fields { get; set; }
    }

    [MemoryPackable]
    public partial class FieldSnapshot
    {
        public string FieldName { get; set; }
        public TypedObject Value { get; set; }
    }
}
#endif
