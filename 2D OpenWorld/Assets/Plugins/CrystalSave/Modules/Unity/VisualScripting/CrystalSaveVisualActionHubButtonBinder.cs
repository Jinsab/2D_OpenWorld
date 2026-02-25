#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.VisualScripting
{
    /// <summary>
    /// Helper component that connects a <see cref="UnityEngine.UI.Button"/> with a
    /// <see cref="CrystalSaveVisualActionHub"/>.
    /// Designers can drop this component on a button and configure which action
    /// should be executed without writing any code.
    /// </summary>
    [AddComponentMenu("Crystal Save/Utility/Visual Action Hub Button Binder")]
    [DisallowMultipleComponent]
    public sealed class CrystalSaveVisualActionHubButtonBinder : MonoBehaviour
    {
        /// <summary>
        /// Defines what operation should be triggered when the button is pressed.
        /// </summary>
        public enum TriggerMode
        {
            /// <summary>Execute every configured action in sequence.</summary>
            ExecuteAll,
            /// <summary>Execute a single action using its index.</summary>
            ExecuteAction
        }

        [SerializeField]
        [Tooltip("The Visual Action Hub that will execute the configured actions.")]
        CrystalSaveVisualActionHub hub;

        [SerializeField]
        [Tooltip("Optional explicit button reference. Defaults to the Button on the same GameObject.")]
        Button button;

        [SerializeField]
        [Tooltip("Automatically register to the button's onClick event on enable.")]
        bool autoRegisterOnEnable = true;

        [SerializeField]
        [Tooltip("Defines which action(s) should be executed when triggered.")]
        TriggerMode trigger = TriggerMode.ExecuteAction;

        [SerializeField]
        [Min(0)]
        [Tooltip("Index of the action inside the Visual Action Hub that should run.")]
        int actionIndex;

        /// <summary>The hub that will receive the trigger.</summary>
        public CrystalSaveVisualActionHub Hub
        {
            get => hub;
            set => hub = value;
        }

        /// <summary>The button that will invoke the trigger.</summary>
        public Button Button
        {
            get
            {
                if (button == null)
                {
                    button = GetComponent<Button>();
                }

                return button;
            }
            set => button = value;
        }

        /// <summary>The configured trigger mode.</summary>
        public TriggerMode Mode
        {
            get => trigger;
            set => trigger = value;
        }

        /// <summary>The index of the action that will be executed when <see cref="Mode"/> is <see cref="TriggerMode.ExecuteAction"/>.</summary>
        public int ActionIndex
        {
            get => actionIndex;
            set => actionIndex = Mathf.Max(0, value);
        }

        void Reset()
        {
            button = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (!autoRegisterOnEnable)
                return;

            var targetButton = Button;
            if (targetButton != null)
            {
                targetButton.onClick.AddListener(Trigger);
            }
            else
            {
                Debug.LogWarning(
                    "CrystalSaveVisualActionHubButtonBinder: Unable to auto-register because no Button component was found.",
                    this);
            }
        }

        void OnDisable()
        {
            if (!autoRegisterOnEnable)
                return;

            if (button != null)
            {
                button.onClick.RemoveListener(Trigger);
            }
        }

        /// <summary>
        /// Triggers the configured operation. Can be wired to any UnityEvent.
        /// </summary>
        public void Trigger()
        {
            if (hub == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHubButtonBinder: Hub reference is missing.", this);
                return;
            }

            switch (trigger)
            {
                case TriggerMode.ExecuteAll:
                    hub.ExecuteAll();
                    break;
                case TriggerMode.ExecuteAction:
                    ExecuteActionIndex(actionIndex);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null);
            }
        }

        /// <summary>
        /// Executes an action with the provided index overriding the configured one.
        /// This is useful for wiring the binder to different UI elements that provide
        /// their own index (for example through UnityEvents with integer arguments).
        /// </summary>
        /// <param name="index">The action index to execute.</param>
        public void TriggerWithIndex(int index)
        {
            if (hub == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHubButtonBinder: Hub reference is missing.", this);
                return;
            }

            ExecuteActionIndex(index);
        }

        /// <summary>
        /// Executes the first action that matches the provided name. Name comparison
        /// is case-sensitive and uses ordinal rules.
        /// </summary>
        /// <param name="actionName">The name of the action to execute.</param>
        public void TriggerWithName(string actionName)
        {
            if (hub == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHubButtonBinder: Hub reference is missing.", this);
                return;
            }

            if (string.IsNullOrEmpty(actionName))
            {
                Debug.LogWarning("CrystalSaveVisualActionHubButtonBinder: Cannot trigger with an empty action name.", this);
                return;
            }

            var actions = hub.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                if (string.Equals(actions[i]?.Name, actionName, StringComparison.Ordinal))
                {
                    ExecuteActionIndex(i);
                    return;
                }
            }

            Debug.LogWarning($"CrystalSaveVisualActionHubButtonBinder: No action named '{actionName}' was found on the hub.", this);
        }

        void ExecuteActionIndex(int index)
        {
            if (hub == null)
                return;

            if (index < 0 || index >= hub.Actions.Count)
            {
                Debug.LogWarning($"CrystalSaveVisualActionHubButtonBinder: Action index {index} is out of range.", this);
                return;
            }

            hub.ExecuteAction(index);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }
#endif
    }
}
#endif
