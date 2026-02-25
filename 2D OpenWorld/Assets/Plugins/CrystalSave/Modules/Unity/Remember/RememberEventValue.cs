#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Prototype bridge component for event-driven custom value save/load workflows.
    /// Intended as an adapter point for project-specific logic.
    /// </summary>
    [AddComponentMenu("Crystal Save/Remember Components/Remember Event Value (Prototype)")]
    [DisallowMultipleComponent]
    public class RememberEventValue : SaveableComponent
    {
        public enum StoredValueType
        {
            Bool,
            Float,
            Int,
            String
        }

        [Serializable] public class BoolEvent : UnityEvent<bool> { }
        [Serializable] public class FloatEvent : UnityEvent<float> { }
        [Serializable] public class IntEvent : UnityEvent<int> { }
        [Serializable] public class StringEvent : UnityEvent<string> { }

        [Header("Source")]
        [SerializeField, Tooltip("Optional reference to the object this key/value belongs to (prototype helper).")]
        private UnityEngine.Object persistentObject;

        [SerializeField, Tooltip("Logical key (for example: hp, burnt, unlocked).")]
        private string key = "value";

        [Header("Type Configuration")]
        [SerializeField] private StoredValueType type = StoredValueType.Float;
        [SerializeField] private bool defaultBool;
        [SerializeField] private float defaultFloat;
        [SerializeField] private int defaultInt;
        [SerializeField] private string defaultString = string.Empty;

        [Header("Events")]
        [SerializeField, Tooltip("Invoked right before save serialization. Use this to push a runtime value into SetBool/SetFloat/SetInt/SetString.")]
        private UnityEvent onSaving = new();

        [SerializeField, Tooltip("Invoked once after this component restored a value.")]
        private UnityEvent onLoadedSingle = new();

        [SerializeField] private BoolEvent onLoadedBool = new();
        [SerializeField] private FloatEvent onLoadedFloat = new();
        [SerializeField] private IntEvent onLoadedInt = new();
        [SerializeField] private StringEvent onLoadedString = new();

        private bool currentBool;
        private float currentFloat;
        private int currentInt;
        private string currentString = string.Empty;

        public UnityEngine.Object PersistentObject => persistentObject;
        public string Key => key;
        public StoredValueType Type => type;
        public bool CurrentBool => currentBool;
        public float CurrentFloat => currentFloat;
        public int CurrentInt => currentInt;
        public string CurrentString => currentString;

        protected override void Awake()
        {
            base.Awake();
            ResetToDefault();
        }

        protected override byte[] SerializeComponentData()
        {
            // Start from default every save, then allow user callbacks to override.
            ResetToDefault();
            onSaving?.Invoke();

            var data = new RememberEventValueData
            {
                Key = key,
                Type = type,
                BoolValue = currentBool,
                FloatValue = currentFloat,
                IntValue = currentInt,
                StringValue = currentString ?? string.Empty
            };

            return Serializer.Serialize(data);
        }

        protected override void DeserializeComponentData(byte[] data)
        {
            RememberEventValueData restored = null;
            try
            {
                restored = Serializer.Deserialize<RememberEventValueData>(data);
            }
            catch (Exception ex)
            {
                Logger.Log(
                    $"RememberEventValue: Failed to deserialize key '{key}' on '{gameObject.name}': {ex.Message}",
                    LogCategory.SaveableComponent,
                    LogLevel.Warning);
            }

            if (restored == null)
            {
                ResetToDefault();
                RaiseLoadedEvents(type);
                return;
            }

            currentBool = restored.BoolValue;
            currentFloat = restored.FloatValue;
            currentInt = restored.IntValue;
            currentString = restored.StringValue ?? string.Empty;

            if (restored.Type != type)
            {
                Logger.Log(
                    $"RememberEventValue: Saved type '{restored.Type}' differs from configured type '{type}' for key '{key}'. Using saved payload.",
                    LogCategory.SaveableComponent,
                    LogLevel.Info);
            }

            RaiseLoadedEvents(restored.Type);
        }

        public void SetBool(bool value)
        {
            currentBool = value;
        }

        public void SetFloat(float value)
        {
            currentFloat = value;
        }

        public void SetInt(int value)
        {
            currentInt = value;
        }

        public void SetString(string value)
        {
            currentString = value ?? string.Empty;
        }

        public void ResetToDefault()
        {
            currentBool = defaultBool;
            currentFloat = defaultFloat;
            currentInt = defaultInt;
            currentString = defaultString ?? string.Empty;
        }

        private void RaiseLoadedEvents(StoredValueType restoredType)
        {
            switch (restoredType)
            {
                case StoredValueType.Bool:
                    onLoadedBool?.Invoke(currentBool);
                    break;
                case StoredValueType.Float:
                    onLoadedFloat?.Invoke(currentFloat);
                    break;
                case StoredValueType.Int:
                    onLoadedInt?.Invoke(currentInt);
                    break;
                case StoredValueType.String:
                    onLoadedString?.Invoke(currentString);
                    break;
            }

            onLoadedSingle?.Invoke();
        }
    }

    [MemoryPackable]
    public partial class RememberEventValueData
    {
        public string Key;
        public RememberEventValue.StoredValueType Type;
        public bool BoolValue;
        public float FloatValue;
        public int IntValue;
        public string StringValue;
    }
}
#endif
