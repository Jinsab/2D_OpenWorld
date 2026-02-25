#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.VisualScripting
{
    /// <summary>
    /// Configurable hub that exposes Crystal Save operations to UnityEvents.
    /// Designers can author a list of actions with optional conditions and
    /// trigger them from UI buttons without writing glue code.
    /// </summary>
    [AddComponentMenu("Crystal Save/Utility/Visual Action Hub")]
    public sealed class CrystalSaveVisualActionHub : MonoBehaviour
    {
        /// <summary>
        /// Defines the supported shared value types for seed configuration.
        /// </summary>
        public enum SharedValueType
        {
            Number,
            Bool,
            String
        }

        /// <summary>
        /// Comparison modes used when evaluating numeric shared values.
        /// </summary>
        public enum NumericComparison
        {
            Equal,
            NotEqual,
            GreaterThan,
            GreaterOrEqual,
            LessThan,
            LessOrEqual,
            Approximately
        }

        /// <summary>
        /// Defines how string values are compared when evaluating conditions.
        /// </summary>
        public enum StringMatchMode
        {
            Exact,
            Contains,
            StartsWith,
            EndsWith
        }

        /// <summary>
        /// Raised when an action succeeds. The integer parameter is the action index.
        /// </summary>
        [Serializable]
        public class ActionIndexEvent : UnityEvent<int> { }

        /// <summary>
        /// Raised when an action finishes. Provides the action index and whether it succeeded.
        /// </summary>
        [Serializable]
        public class ActionIndexResultEvent : UnityEvent<int, bool> { }

        /// <summary>
        /// UnityEvent that exposes a boolean flag to indicate success.
        /// </summary>
        [Serializable]
        public class BoolEvent : UnityEvent<bool> { }

        /// <summary>
        /// Operations that the hub can execute.
        /// </summary>
        public enum OperationType
        {
            /// <summary>Saves the resolved slot.</summary>
            Save,
            /// <summary>Loads the resolved slot.</summary>
            Load,
            /// <summary>Deletes the resolved slot.</summary>
            DeleteSlot,
            /// <summary>Renames the resolved slot.</summary>
            RenameSlot,
            /// <summary>Performs a quick save using the configuration in <see cref="SaveSettings"/>.</summary>
            QuickSave,
            /// <summary>Loads the most recent quick save slot.</summary>
            QuickLoad,
            /// <summary>Saves into the auto-save slot defined in <see cref="SaveSettings"/>.</summary>
            AutoSave,
            /// <summary>Loads the auto-save slot defined in <see cref="SaveSettings"/>.</summary>
            LoadAutoSave,
            /// <summary>Loads a scene after capturing a snapshot and populating pending prefabs.</summary>
            LoadSceneAfterSnapshotAndPopulate,
            /// <summary>Restores a destroyed GameObject by unique identifier.</summary>
            RestoreDestroyedGameObject,
            /// <summary>Restores a destroyed prefab using its instance identifier.</summary>
            RestoreDestroyedPrefabByUniqueID,
            /// <summary>Restores a destroyed prefab by prefab asset identifier.</summary>
            RestoreDestroyedPrefabByAssetID,
            /// <summary>Destroys GameObjects by their saved unique identifiers.</summary>
            DestroyGameObjectByUniqueID,
            /// <summary>Destroys spawned prefabs that match the provided asset identifiers.</summary>
            DestroyPrefabsByAssetID,
            /// <summary>Processes the global deferred prefab queue.</summary>
            ProcessDeferredPrefabs,
            /// <summary>Processes deferred prefabs that belong to a specific scene.</summary>
            ProcessDeferredPrefabsForScene,
            /// <summary>Processes deferred prefabs registered for a prefab asset.</summary>
            ProcessDeferredPrefabsForAsset,
            /// <summary>Processes a single deferred prefab instance.</summary>
            ProcessDeferredPrefabByUniqueID,
            /// <summary>Processes a curated list of deferred prefab instances.</summary>
            ProcessDeferredPrefabsByInstanceIDs,
            /// <summary>Restores a single GameObject from stored component data.</summary>
            RestoreSingleGameObject,
            /// <summary>Destroys live GameObjects while saving restoration data for each target.</summary>
            DestroyWithSnapshot
        }

        /// <summary>
        /// Defines how an action resolves the save slot it operates on.
        /// </summary>
        public enum SlotSource
        {
            Latest,
            Explicit,
            DesignTime
        }

        /// <summary>
        /// Data source for restore operations that can work on live data or by loading a slot.
        /// </summary>
        public enum RestoreDestroyedDataSource
        {
            CurrentSaveData,
            Slot
        }

        /// <summary>
        /// Data source for targeted GameObject restore operations.
        /// </summary>
        public enum RestoreSingleDataSource
        {
            CurrentSaveData,
            Slot
        }

        /// <summary>
        /// Simple retry settings used by operations that support async retries.
        /// </summary>
        [Serializable]
        public class RetrySettings
        {
            [SerializeField]
            bool enabled;

            [SerializeField, Min(1)]
            int maxAttempts = 3;

            [SerializeField, Min(1)]
            int retryDelayMs = 500;

            /// <summary>Whether retry logic is enabled.</summary>
            public bool Enabled
            {
                get => enabled;
                set => enabled = value;
            }

            /// <summary>Maximum number of attempts when retrying.</summary>
            public int MaxAttempts
            {
                get => Mathf.Max(1, maxAttempts);
                set => maxAttempts = Mathf.Max(1, value);
            }

            /// <summary>Delay between attempts in milliseconds.</summary>
            public int RetryDelayMs
            {
                get => Mathf.Max(1, retryDelayMs);
                set => retryDelayMs = Mathf.Max(1, value);
            }
        }

        /// <summary>
        /// Describes how a slot number is chosen when executing an action.
        /// </summary>
        [Serializable]
        public class SlotReference
        {
            [SerializeField]
            SlotSource source = SlotSource.Latest;

            [SerializeField, Min(1)]
            int explicitSlot = 1;

            [SerializeField, Min(1)]
            int designTimeSlot = 1;

            /// <summary>The configured slot source.</summary>
            public SlotSource Source
            {
                get => source;
                set => source = value;
            }

            /// <summary>Slot number used when <see cref="SlotSource.Explicit"/> is selected.</summary>
            public int ExplicitSlot
            {
                get => Mathf.Max(1, explicitSlot);
                set => explicitSlot = Mathf.Max(1, value);
            }

            /// <summary>
            /// Slot number chosen via design-time dropdowns.
            /// </summary>
            public int DesignTimeSlot
            {
                get => Mathf.Max(1, designTimeSlot);
                set => designTimeSlot = Mathf.Max(1, value);
            }

            /// <summary>
            /// Resolves the slot based on the supplied latest slot value.
            /// </summary>
            public int Resolve(int latestSlot, out bool usedLatest)
            {
                switch (source)
                {
                    case SlotSource.Latest:
                        if (latestSlot > 0)
                        {
                            usedLatest = true;
                            return latestSlot;
                        }
                        usedLatest = false;
                        return ExplicitSlot;
                    case SlotSource.DesignTime:
                        usedLatest = false;
                        return DesignTimeSlot;
                    default:
                        usedLatest = false;
                        return ExplicitSlot;
                }
            }
        }

        /// <summary>
        /// Initial seed for a shared value entry that can be configured in the inspector.
        /// </summary>
        [Serializable]
        public class SharedValueSeed
        {
            [SerializeField]
            string key;

            [SerializeField]
            SharedValueType valueType = SharedValueType.Number;

            [SerializeField]
            double numberValue;

            [SerializeField]
            bool boolValue;

            [SerializeField]
            string stringValue;

            /// <summary>The identifier used to resolve the shared value at runtime.</summary>
            public string Key => key;

            /// <summary>The configured type of the shared value.</summary>
            public SharedValueType ValueType => valueType;

            /// <summary>Numeric value used when <see cref="ValueType"/> is <see cref="SharedValueType.Number"/>.</summary>
            public double NumberValue => numberValue;

            /// <summary>Boolean value used when <see cref="ValueType"/> is <see cref="SharedValueType.Bool"/>.</summary>
            public bool BoolValue => boolValue;

            /// <summary>String value used when <see cref="ValueType"/> is <see cref="SharedValueType.String"/>.</summary>
            public string StringValue => stringValue;
        }

        /// <summary>Supported condition types for gating an action.</summary>
        public enum ConditionType
        {
            Always,
            HasAnySave,
            HasSaveInSlot,
            HasSaveInScene,
            SharedNumber,
            SharedBool,
            SharedString,
            SaveManagerIsLoading,
            SaveManagerHasSnapshot,
            SaveSlotsReady,
            QuickSlotsReady,
            CurrentSlotEquals,
            HasSaveAfterDate
        }

        /// <summary>Determines which slot a condition should inspect.</summary>
        public enum ConditionSlotSource
        {
            UseActionSlot,
            SpecificSlot
        }

        /// <summary>
        /// Describes a conditional check that must pass before an action is executed.
        /// </summary>
        [Serializable]
        public class ActionCondition
        {
            [SerializeField]
            ConditionType type = ConditionType.Always;

            [SerializeField]
            ConditionSlotSource slotSource = ConditionSlotSource.UseActionSlot;

            [SerializeField, Min(1)]
            int slotNumber = 1;

            [SerializeField]
            string sceneName;

            [SerializeField]
            string sharedValueKey;

            [SerializeField]
            double expectedNumber;

            [SerializeField]
            NumericComparison numericComparison = NumericComparison.Equal;

            [SerializeField]
            bool useNumericTolerance;

            [SerializeField]
            double numericTolerance = 0.001;

            [SerializeField]
            bool expectedBool = true;

            [SerializeField]
            string expectedString;

            [SerializeField]
            StringMatchMode stringMatchMode = StringMatchMode.Exact;

            [SerializeField]
            bool stringCaseSensitive;

            [SerializeField]
            string earliestSaveDateIso;

            internal ConditionType ConditionKind => type;
            internal ConditionSlotSource SlotUsage => slotSource;

            /// <summary>Evaluates this condition against the provided SaveManager.</summary>
            public bool Evaluate(CrystalSaveVisualActionHub hub, SaveManager manager, int? actionSlot, out string message)
            {
                message = null;

                if (hub == null)
                {
                    message = "CrystalSaveVisualActionHub reference is missing.";
                    return false;
                }

                if (manager == null)
                {
                    message = "SaveManager.Instance is not available.";
                    return false;
                }

                try
                {
                    switch (type)
                    {
                        case ConditionType.Always:
                            return true;
                        case ConditionType.HasAnySave:
                            if (manager.HasSave())
                                return true;
                            message = "No save data has been created yet.";
                            return false;
                        case ConditionType.HasSaveInSlot:
                        {
                            int slot;
                            if (slotSource == ConditionSlotSource.UseActionSlot)
                            {
                                if (!actionSlot.HasValue)
                                {
                                    message = "Action slot was not resolved but the condition requires it.";
                                    return false;
                                }
                                slot = actionSlot.Value;
                            }
                            else
                            {
                                slot = Mathf.Max(1, slotNumber);
                            }

                            if (manager.HasSaveAt(slot))
                                return true;

                            message = $"Save slot {slot} does not contain data.";
                            return false;
                        }
                        case ConditionType.HasSaveInScene:
                            if (string.IsNullOrWhiteSpace(sceneName))
                            {
                                message = "Scene name is empty.";
                                return false;
                            }

                            if (manager.HasSaveInScene(sceneName))
                                return true;

                            message = $"No save data referencing scene '{sceneName}' was found.";
                            return false;
                        case ConditionType.SharedNumber:
                            return EvaluateSharedNumber(hub, out message);
                        case ConditionType.SharedBool:
                            return EvaluateSharedBool(hub, out message);
                        case ConditionType.SharedString:
                            return EvaluateSharedString(hub, out message);
                        case ConditionType.SaveManagerIsLoading:
                            if (manager.IsLoading)
                                return true;
                            message = "SaveManager is not currently loading.";
                            return false;
                        case ConditionType.SaveManagerHasSnapshot:
                            if (manager.CurrentSaveData != null)
                                return true;
                            message = "SaveManager does not hold an in-memory snapshot.";
                            return false;
                        case ConditionType.SaveSlotsReady:
                            if (SaveManager.AreSaveSlotsReady)
                                return true;
                            message = "Save slots are not ready yet.";
                            return false;
                        case ConditionType.QuickSlotsReady:
                            if (SaveManager.AreQuickSlotsReady)
                                return true;
                            message = "Quick save slots are not ready yet.";
                            return false;
                        case ConditionType.CurrentSlotEquals:
                        {
                            int expectedSlot;
                            if (slotSource == ConditionSlotSource.UseActionSlot)
                            {
                                if (!actionSlot.HasValue)
                                {
                                    message = "Action slot was not resolved but the condition requires it.";
                                    return false;
                                }
                                expectedSlot = actionSlot.Value;
                            }
                            else
                            {
                                expectedSlot = Mathf.Max(1, slotNumber);
                            }

                            int? currentSlot = manager.CurrentSaveSlot != null ? manager.CurrentSaveSlot.SlotNumber : (int?)null;
                            if (currentSlot.HasValue && currentSlot.Value == expectedSlot)
                                return true;

                            message = currentSlot.HasValue
                                ? $"Current save slot ({currentSlot.Value}) does not match expected slot {expectedSlot}."
                                : "SaveManager.CurrentSaveSlot is not assigned.";
                            return false;
                        }
                        case ConditionType.HasSaveAfterDate:
                            return EvaluateHasSaveAfterDate(manager, out message);
                        default:
                            message = $"Unsupported condition {type}.";
                            return false;
                    }
                }
                catch (Exception ex)
                {
                    message = $"Condition {type} raised an exception: {ex.Message}";
                    return false;
                }
            }

            bool EvaluateSharedNumber(CrystalSaveVisualActionHub hub, out string message)
            {
                message = null;
                string key = sharedValueKey?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    message = "Shared value key is empty.";
                    return false;
                }

                if (!hub.TryGetSharedNumber(key, out double actual))
                {
                    message = $"Shared number '{key}' is not available.";
                    return false;
                }

                double tolerance = useNumericTolerance ? Math.Max(0d, numericTolerance) : 0d;
                if (hub.CompareNumber(actual, expectedNumber, numericComparison, tolerance))
                    return true;

                message = $"Shared number '{key}' comparison failed.";
                return false;
            }

            bool EvaluateSharedBool(CrystalSaveVisualActionHub hub, out string message)
            {
                message = null;
                string key = sharedValueKey?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    message = "Shared value key is empty.";
                    return false;
                }

                if (!hub.TryGetSharedBool(key, out bool actual))
                {
                    message = $"Shared bool '{key}' is not available.";
                    return false;
                }

                if (actual == expectedBool)
                    return true;

                message = $"Shared bool '{key}' was {actual} but expected {expectedBool}.";
                return false;
            }

            bool EvaluateSharedString(CrystalSaveVisualActionHub hub, out string message)
            {
                message = null;
                string key = sharedValueKey?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    message = "Shared value key is empty.";
                    return false;
                }

                if (!hub.TryGetSharedString(key, out string actual))
                {
                    message = $"Shared string '{key}' is not available.";
                    return false;
                }

                if (hub.CompareString(actual, expectedString ?? string.Empty, stringMatchMode, stringCaseSensitive))
                    return true;

                message = $"Shared string '{key}' comparison failed.";
                return false;
            }

            bool EvaluateHasSaveAfterDate(SaveManager manager, out string message)
            {
                message = null;
                if (string.IsNullOrWhiteSpace(earliestSaveDateIso))
                {
                    message = "Earliest save date is not configured.";
                    return false;
                }

                if (!DateTime.TryParse(earliestSaveDateIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                {
                    if (!DateTime.TryParse(earliestSaveDateIso, out parsed))
                    {
                        message = $"Unable to parse '{earliestSaveDateIso}' as a valid date.";
                        return false;
                    }
                }

                if (manager.HasSaveAfterDate(parsed))
                    return true;

                message = $"No save data was created after {parsed:u}.";
                return false;
            }
        }

        [Serializable]
        public class SaveParameters
        {
            [SerializeField]
            string lastActiveScene;

            [SerializeField]
            string slotName;

            public string LastActiveScene => lastActiveScene;
            public string SlotName => slotName;
        }

        [Serializable]
        public class LoadParameters
        {
            [SerializeField]
            bool restoreLastActiveScene;

            [SerializeField]
            bool loadAsync;

            [SerializeField]
            bool allowSceneActivation = true;

            public bool RestoreLastActiveScene => restoreLastActiveScene;
            public bool LoadAsync => loadAsync;
            public bool AllowSceneActivation => allowSceneActivation;
        }

        [Serializable]
        public class RestoreDestroyedGameObjectParameters
        {
            [SerializeField]
            string uniqueId;

            [SerializeField]
            RestoreDestroyedDataSource dataSource = RestoreDestroyedDataSource.CurrentSaveData;

            [SerializeField]
            RetrySettings retry = new();

            public string UniqueId => uniqueId;
            public RestoreDestroyedDataSource DataSource => dataSource;
            public RetrySettings Retry => retry;
        }

        [Serializable]
        public class RestoreDestroyedPrefabParameters
        {
            [SerializeField]
            string identifier;

            [SerializeField]
            bool useAssetId;

            [SerializeField]
            RestoreDestroyedDataSource dataSource = RestoreDestroyedDataSource.CurrentSaveData;

            [SerializeField]
            RetrySettings retry = new();

            public string Identifier => identifier;
            public bool UseAssetId
            {
                get => useAssetId;
                set => useAssetId = value;
            }
            public RestoreDestroyedDataSource DataSource => dataSource;
            public RetrySettings Retry => retry;
        }

        [Serializable]
        public class RestoreSingleGameObjectParameters
        {
            [SerializeField]
            GameObject target;

            [SerializeField]
            RestoreSingleDataSource dataSource = RestoreSingleDataSource.CurrentSaveData;

            [SerializeField]
            RetrySettings retry = new();

            public GameObject Target => target;
            public RestoreSingleDataSource DataSource => dataSource;
            public RetrySettings Retry => retry;
        }

        /// <summary>Configures delete slot behavior.</summary>
        [Serializable]
        public class DeleteSlotParameters
        {
            [SerializeField]
            bool requireExistingData = true;

            /// <summary>
            /// When true the hub checks that the slot currently contains save data before calling
            /// <see cref="SaveManager.Delete(int)"/>.
            /// </summary>
            public bool RequireExistingData
            {
                get => requireExistingData;
                set => requireExistingData = value;
            }
        }

        /// <summary>Configures rename slot behavior.</summary>
        [Serializable]
        public class RenameSlotParameters
        {
            [SerializeField]
            string newName;

            /// <summary>Desired slot name.</summary>
            public string NewName
            {
                get => newName;
                set => newName = value;
            }
        }

        /// <summary>Controls validation when triggering a quick save.</summary>
        [Serializable]
        public class QuickSaveParameters
        {
            [SerializeField]
            bool requireConfiguredSlots = true;

            /// <summary>
            /// When true the hub validates that quick save slots are configured before invoking
            /// <see cref="SaveManager.QuickSave"/>.
            /// </summary>
            public bool RequireConfiguredSlots
            {
                get => requireConfiguredSlots;
                set => requireConfiguredSlots = value;
            }
        }

        /// <summary>Controls validation when loading a quick save.</summary>
        [Serializable]
        public class QuickLoadParameters
        {
            [SerializeField]
            bool requireExistingData = true;

            /// <summary>
            /// When true the hub ensures that the target quick save slot currently has data before
            /// calling <see cref="SaveManager.QuickLoad"/>.
            /// </summary>
            public bool RequireExistingData
            {
                get => requireExistingData;
                set => requireExistingData = value;
            }
        }

        /// <summary>Controls validation when triggering an auto save.</summary>
        [Serializable]
        public class AutoSaveParameters
        {
            [SerializeField]
            bool requireConfiguredSlot = true;

            /// <summary>
            /// When true the hub validates that an auto save slot number is configured before calling
            /// <see cref="SaveManager.AutoSave"/>.
            /// </summary>
            public bool RequireConfiguredSlot
            {
                get => requireConfiguredSlot;
                set => requireConfiguredSlot = value;
            }
        }

        /// <summary>Parameters for the LoadAutoSave operation.</summary>
        [Serializable]
        public class LoadAutoSaveParameters
        {
            [SerializeField]
            bool restoreScene = true;

            [SerializeField]
            bool requireExistingData = true;

            /// <summary>Whether to restore the scene specified in the auto-save metadata.</summary>
            public bool RestoreScene
            {
                get => restoreScene;
                set => restoreScene = value;
            }

            /// <summary>
            /// When true the hub ensures the auto save slot currently stores data before attempting
            /// to load it.
            /// </summary>
            public bool RequireExistingData
            {
                get => requireExistingData;
                set => requireExistingData = value;
            }
        }

        /// <summary>Parameters for destroying GameObjects by saved unique identifier.</summary>
        [Serializable]
        public class DestroyGameObjectParameters
        {
            [SerializeField]
            string uniqueId;

            [SerializeField]
            List<string> uniqueIds = new();

            /// <summary>Single unique identifier to destroy.</summary>
            public string UniqueId
            {
                get => uniqueId;
                set => uniqueId = value;
            }

            /// <summary>Optional list of unique identifiers to destroy.</summary>
            public List<string> UniqueIds => uniqueIds;
        }

        /// <summary>Parameters for processing the global deferred prefab queue.</summary>
        [Serializable]
        public class ProcessDeferredPrefabsParameters
        {
            [SerializeField]
            List<string> destroyedGameObjectIds = new();

            /// <summary>
            /// Optional list of destroyed GameObject unique IDs to remove from the queue while processing.
            /// </summary>
            public List<string> DestroyedGameObjectIds => destroyedGameObjectIds;
        }

        /// <summary>Parameters for processing deferred prefabs tied to a scene.</summary>
        [Serializable]
        public class ProcessDeferredPrefabsForSceneParameters
        {
            [SerializeField]
            string sceneName;

            [SerializeField]
            List<string> destroyedGameObjectIds = new();

            /// <summary>Scene whose deferred prefabs should be processed.</summary>
            public string SceneName
            {
                get => sceneName;
                set => sceneName = value;
            }

            /// <summary>Optional destroyed GameObject IDs to consume from the queue.</summary>
            public List<string> DestroyedGameObjectIds => destroyedGameObjectIds;
        }

        /// <summary>Parameters for processing deferred prefabs tied to an asset identifier.</summary>
        [Serializable]
        public class ProcessDeferredPrefabsForAssetParameters
        {
            [SerializeField]
            string prefabAssetId;

            [SerializeField]
            List<string> destroyedGameObjectIds = new();

            /// <summary>Prefab asset identifier whose deferred queue should be processed.</summary>
            public string PrefabAssetId
            {
                get => prefabAssetId;
                set => prefabAssetId = value;
            }

            /// <summary>Optional destroyed GameObject IDs to consume from the queue.</summary>
            public List<string> DestroyedGameObjectIds => destroyedGameObjectIds;
        }

        /// <summary>Parameters for processing a deferred prefab by unique instance ID.</summary>
        [Serializable]
        public class ProcessDeferredPrefabByUniqueIDParameters
        {
            [SerializeField]
            string uniqueId;

            [SerializeField]
            List<string> destroyedGameObjectIds = new();

            /// <summary>Unique instance identifier queued in the prefab manager.</summary>
            public string UniqueId
            {
                get => uniqueId;
                set => uniqueId = value;
            }

            /// <summary>Optional destroyed GameObject IDs to consume from the queue.</summary>
            public List<string> DestroyedGameObjectIds => destroyedGameObjectIds;
        }

        /// <summary>Parameters for processing a curated list of deferred prefab instance IDs.</summary>
        [Serializable]
        public class ProcessDeferredPrefabsByInstanceIDsParameters
        {
            [SerializeField]
            List<string> instanceIds = new();

            [SerializeField]
            List<string> destroyedGameObjectIds = new();

            /// <summary>Collection of instance identifiers to spawn.</summary>
            public List<string> InstanceIds => instanceIds;

            /// <summary>Optional destroyed GameObject IDs to consume from the queue.</summary>
            public List<string> DestroyedGameObjectIds => destroyedGameObjectIds;
        }

        /// <summary>Parameters for destroying prefab instances by asset identifier.</summary>
        [Serializable]
        public class DestroyPrefabsByAssetIDParameters
        {
            [SerializeField]
            string prefabAssetId;

            [SerializeField]
            List<string> prefabAssetIds = new();

            /// <summary>Single prefab asset identifier to destroy.</summary>
            public string PrefabAssetId
            {
                get => prefabAssetId;
                set => prefabAssetId = value;
            }

            /// <summary>Optional list of prefab asset identifiers to destroy.</summary>
            public List<string> PrefabAssetIds => prefabAssetIds;
        }

        /// <summary>
        /// Parameters for destroying live GameObjects while capturing snapshots so they can be restored later.
        /// Multiple targets are supported and each target is processed individually.
        /// </summary>
        [Serializable]
        public class DestroyWithSnapshotParameters
        {
            [SerializeField]
            List<GameObject> targets = new();

            [SerializeField]
            bool destroy = true;

            [SerializeField]
            bool allowPooling = true;

            /// <summary>List of GameObjects to process. Each entry is handled separately.</summary>
            public List<GameObject> Targets => targets;

            /// <summary>If true each GameObject is destroyed, otherwise it is deactivated after the snapshot is captured.</summary>
            public bool Destroy
            {
                get => destroy;
                set => destroy = value;
            }

            /// <summary>When true pooled prefabs are returned to their pool instead of being destroyed.</summary>
            public bool AllowPooling
            {
                get => allowPooling;
                set => allowPooling = value;
            }
        }

        /// <summary>Parameters for loading a scene after capturing a snapshot and populating pending prefabs.</summary>
        [Serializable]
        public class LoadSceneAfterSnapshotAndPopulateParameters
        {
            [SerializeField]
            bool useBuildIndex;

            [SerializeField]
            string sceneName = string.Empty;

            [SerializeField]
            int sceneBuildIndex = -1;

            [SerializeField]
            bool loadAdditive;

            [SerializeField]
            bool loadAsync;

            [SerializeField]
            bool allowDuplicateLoad;

            /// <summary>When true the build index is used instead of the scene name.</summary>
            public bool UseBuildIndex
            {
                get => useBuildIndex;
                set => useBuildIndex = value;
            }

            /// <summary>Scene name used when <see cref="UseBuildIndex"/> is false.</summary>
            public string SceneName
            {
                get => sceneName;
                set => sceneName = value;
            }

            /// <summary>Scene build index used when <see cref="UseBuildIndex"/> is true.</summary>
            public int SceneBuildIndex
            {
                get => sceneBuildIndex;
                set => sceneBuildIndex = value;
            }

            /// <summary>Loads the scene additively when true.</summary>
            public bool LoadAdditive
            {
                get => loadAdditive;
                set => loadAdditive = value;
            }

            /// <summary>Loads the scene asynchronously when true.</summary>
            public bool LoadAsync
            {
                get => loadAsync;
                set => loadAsync = value;
            }

            /// <summary>Allows loading an additive scene even if it is already loaded.</summary>
            public bool AllowDuplicateLoad
            {
                get => allowDuplicateLoad;
                set => allowDuplicateLoad = value;
            }
        }

        /// <summary>
        /// Represents a single action entry in the hub.
        /// </summary>
        [Serializable]
        public class VisualAction
        {
            [SerializeField]
            string name;

            [SerializeField]
            OperationType operation = OperationType.Load;

            [SerializeField]
            SlotReference slot = new();

            [SerializeField]
            SaveParameters save = new();

            [SerializeField]
            LoadParameters load = new();

            [SerializeField]
            RestoreDestroyedGameObjectParameters restoreDestroyedGameObject = new();

            [SerializeField]
            RestoreDestroyedPrefabParameters restoreDestroyedPrefab = new();

            [SerializeField]
            RestoreSingleGameObjectParameters restoreSingleGameObject = new();

            [SerializeField]
            DeleteSlotParameters deleteSlot = new();

            [SerializeField]
            RenameSlotParameters renameSlot = new();

            [SerializeField]
            QuickSaveParameters quickSave = new();

            [SerializeField]
            QuickLoadParameters quickLoad = new();

            [SerializeField]
            AutoSaveParameters autoSave = new();

            [SerializeField]
            LoadAutoSaveParameters loadAutoSave = new();

            [SerializeField]
            DestroyGameObjectParameters destroyGameObject = new();

            [SerializeField]
            DestroyPrefabsByAssetIDParameters destroyPrefabsByAssetId = new();

            [SerializeField]
            ProcessDeferredPrefabsParameters processDeferredPrefabs = new();

            [SerializeField]
            ProcessDeferredPrefabsForSceneParameters processDeferredPrefabsForScene = new();

            [SerializeField]
            ProcessDeferredPrefabsForAssetParameters processDeferredPrefabsForAsset = new();

            [SerializeField]
            ProcessDeferredPrefabByUniqueIDParameters processDeferredPrefabByUniqueId = new();

            [SerializeField]
            ProcessDeferredPrefabsByInstanceIDsParameters processDeferredPrefabsByInstanceIds = new();

            [SerializeField]
            DestroyWithSnapshotParameters destroyWithSnapshot = new();

            [SerializeField]
            LoadSceneAfterSnapshotAndPopulateParameters loadSceneAfterSnapshotAndPopulate = new();

            [SerializeField]
            List<ActionCondition> conditions = new();

            [SerializeField]
            UnityEvent onSuccess = new();

            [SerializeField]
            UnityEvent onFailure = new();

            [SerializeField]
            BoolEvent onFinished = new();

            public string Name
            {
                get => name;
                set => name = value;
            }

            public OperationType Operation => operation;
            public SlotReference Slot => slot;
            public SaveParameters Save => save;
            public LoadParameters Load => load;
            public RestoreDestroyedGameObjectParameters RestoreDestroyedGameObject => restoreDestroyedGameObject;
            public RestoreDestroyedPrefabParameters RestoreDestroyedPrefab => restoreDestroyedPrefab;
            public RestoreSingleGameObjectParameters RestoreSingleGameObject => restoreSingleGameObject;
            public DeleteSlotParameters DeleteSlot => deleteSlot;
            public RenameSlotParameters RenameSlot => renameSlot;
            public QuickSaveParameters QuickSave => quickSave;
            public QuickLoadParameters QuickLoad => quickLoad;
            public AutoSaveParameters AutoSave => autoSave;
            public LoadAutoSaveParameters LoadAutoSave => loadAutoSave;
            public DestroyGameObjectParameters DestroyGameObject => destroyGameObject;
            public DestroyPrefabsByAssetIDParameters DestroyPrefabsByAssetId => destroyPrefabsByAssetId;
            public ProcessDeferredPrefabsParameters ProcessDeferredPrefabs => processDeferredPrefabs;
            public ProcessDeferredPrefabsForSceneParameters ProcessDeferredPrefabsForScene => processDeferredPrefabsForScene;
            public ProcessDeferredPrefabsForAssetParameters ProcessDeferredPrefabsForAsset => processDeferredPrefabsForAsset;
            public ProcessDeferredPrefabByUniqueIDParameters ProcessDeferredPrefabByUniqueId => processDeferredPrefabByUniqueId;
            public ProcessDeferredPrefabsByInstanceIDsParameters ProcessDeferredPrefabsByInstanceIds => processDeferredPrefabsByInstanceIds;
            /// <summary>Configuration for destroying live GameObjects while capturing snapshots for restoration.</summary>
            public DestroyWithSnapshotParameters DestroyWithSnapshot => destroyWithSnapshot;
            public LoadSceneAfterSnapshotAndPopulateParameters LoadSceneAfterSnapshotAndPopulate => loadSceneAfterSnapshotAndPopulate;
            public List<ActionCondition> Conditions => conditions;
            public UnityEvent OnSuccess => onSuccess;
            public UnityEvent OnFailure => onFailure;
            public BoolEvent OnFinished => onFinished;
        }

        [SerializeField]
        List<SharedValueSeed> sharedValueSeeds = new();

        [SerializeField]
        List<VisualAction> actions = new();

        [SerializeField]
        ActionIndexEvent onActionSucceeded = new();

        [SerializeField]
        ActionIndexEvent onActionFailed = new();

        [SerializeField]
        ActionIndexResultEvent onActionFinished = new();

        [SerializeField]
        UnityEvent onAllActionsCompleted = new();

        [SerializeField, HideInInspector]
        int latestResolvedSlot = -1;

        readonly Dictionary<string, double> sharedNumbers = new(StringComparer.Ordinal);
        readonly Dictionary<string, bool> sharedBools = new(StringComparer.Ordinal);
        readonly Dictionary<string, string> sharedStrings = new(StringComparer.Ordinal);

        /// <summary>The configured actions.</summary>
        public IReadOnlyList<VisualAction> Actions => actions;

        /// <summary>The default shared value seeds configured in the inspector.</summary>
        public IReadOnlyList<SharedValueSeed> SharedValueSeeds => sharedValueSeeds;

        /// <summary>The last slot resolved by the hub.</summary>
        public int LatestResolvedSlot => latestResolvedSlot;

        void Awake()
        {
            SeedSharedValues(overwriteExisting: true);
        }

        void OnEnable()
        {
            SeedSharedValues(overwriteExisting: !Application.isPlaying);
        }

        /// <summary>
        /// Executes all configured actions sequentially.
        /// </summary>
        public void ExecuteAll()
        {
            _ = ExecuteAllAsync();
        }

        /// <summary>
        /// Executes the action at the provided index.
        /// </summary>
        /// <param name="index">Action index within <see cref="Actions"/>.</param>
        public void ExecuteAction(int index)
        {
            _ = ExecuteActionAsync(index);
        }

        async Task ExecuteAllAsync()
        {
            for (int i = 0; i < actions.Count; i++)
            {
                await ExecuteActionAsync(i);
            }

            onAllActionsCompleted?.Invoke();
        }

        async Task<bool> ExecuteActionAsync(int index)
        {
            if (index < 0 || index >= actions.Count)
            {
                Debug.LogWarning($"CrystalSaveVisualActionHub: Requested action index {index} is out of range.");
                return false;
            }

            var action = actions[index];
            var manager = SaveManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: SaveManager.Instance is not available in the scene.");
                action.OnFailure?.Invoke();
                action.OnFinished?.Invoke(false);
                onActionFailed?.Invoke(index);
                onActionFinished?.Invoke(index, false);
                return false;
            }

            int resolvedSlot = -1;
            bool needsSlot = OperationUsesSlot(action) || ConditionsRequireSlot(action);
            if (needsSlot)
            {
                if (!TryResolveSlot(action, manager, out resolvedSlot, out string resolutionMessage))
                {
                    Debug.LogWarning($"CrystalSaveVisualActionHub: {resolutionMessage}");
                    action.OnFailure?.Invoke();
                    action.OnFinished?.Invoke(false);
                    onActionFailed?.Invoke(index);
                    onActionFinished?.Invoke(index, false);
                    return false;
                }
            }

            if (!EvaluateConditions(action, manager, resolvedSlot, out string conditionMessage))
            {
                if (!string.IsNullOrEmpty(conditionMessage))
                {
                    Debug.Log($"CrystalSaveVisualActionHub: Conditions for action '{action.Name}' prevented execution ({conditionMessage}).");
                }

                action.OnFinished?.Invoke(false);
                onActionFinished?.Invoke(index, false);
                return false;
            }

            bool success = await PerformOperationAsync(action, manager, resolvedSlot);
            if (success)
            {
                action.OnSuccess?.Invoke();
                onActionSucceeded?.Invoke(index);
            }
            else
            {
                action.OnFailure?.Invoke();
                onActionFailed?.Invoke(index);
            }

            action.OnFinished?.Invoke(success);
            onActionFinished?.Invoke(index, success);
            return success;
        }

        static bool ConditionsRequireSlot(VisualAction action)
        {
            if (action == null)
                return false;

            foreach (var condition in action.Conditions)
            {
                if (condition == null)
                    continue;

                if ((condition.ConditionKind == ConditionType.HasSaveInSlot ||
                     condition.ConditionKind == ConditionType.CurrentSlotEquals) &&
                    condition.SlotUsage == ConditionSlotSource.UseActionSlot)
                    return true;
            }

            return false;
        }

        void SeedSharedValues(bool overwriteExisting)
        {
            if (sharedValueSeeds == null || sharedValueSeeds.Count == 0)
                return;

            foreach (var seed in sharedValueSeeds)
            {
                if (seed == null)
                    continue;

                string key = NormalizeKey(seed.Key);
                if (string.IsNullOrEmpty(key))
                    continue;

                bool exists = sharedNumbers.ContainsKey(key) || sharedBools.ContainsKey(key) || sharedStrings.ContainsKey(key);
                if (!overwriteExisting && exists)
                    continue;

                switch (seed.ValueType)
                {
                    case SharedValueType.Number:
                        SetSharedNumber(key, seed.NumberValue);
                        break;
                    case SharedValueType.Bool:
                        SetSharedBool(key, seed.BoolValue);
                        break;
                    case SharedValueType.String:
                        SetSharedString(key, seed.StringValue);
                        break;
                }
            }
        }

        static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }

        /// <summary>
        /// Stores or updates a shared numeric value at runtime.
        /// </summary>
        /// <param name="key">Lookup key for the shared value.</param>
        /// <param name="value">Numeric payload stored in the registry.</param>
        public void SetSharedNumber(string key, double value)
        {
            string normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub.SetSharedNumber called with an empty key.");
                return;
            }

            sharedNumbers[normalized] = value;
            sharedBools.Remove(normalized);
            sharedStrings.Remove(normalized);
        }

        /// <summary>
        /// Stores or updates a shared numeric value using a float payload.
        /// </summary>
        /// <param name="key">Lookup key for the shared value.</param>
        /// <param name="value">Numeric payload stored in the registry.</param>
        public void SetSharedNumber(string key, float value)
        {
            SetSharedNumber(key, (double)value);
        }

        /// <summary>
        /// Stores or updates a shared boolean value at runtime.
        /// </summary>
        /// <param name="key">Lookup key for the shared value.</param>
        /// <param name="value">Boolean payload stored in the registry.</param>
        public void SetSharedBool(string key, bool value)
        {
            string normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub.SetSharedBool called with an empty key.");
                return;
            }

            sharedBools[normalized] = value;
            sharedNumbers.Remove(normalized);
            sharedStrings.Remove(normalized);
        }

        /// <summary>
        /// Stores or updates a shared string value at runtime.
        /// </summary>
        /// <param name="key">Lookup key for the shared value.</param>
        /// <param name="value">String payload stored in the registry.</param>
        public void SetSharedString(string key, string value)
        {
            string normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub.SetSharedString called with an empty key.");
                return;
            }

            sharedStrings[normalized] = value ?? string.Empty;
            sharedNumbers.Remove(normalized);
            sharedBools.Remove(normalized);
        }

        /// <summary>
        /// Attempts to resolve a shared numeric value.
        /// </summary>
        /// <param name="key">Lookup key for the shared value.</param>
        /// <param name="value">The resolved value when available.</param>
        /// <returns>True when a numeric value exists for the provided key.</returns>
        public bool TryGetSharedNumber(string key, out double value)
        {
            string normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized))
            {
                value = default;
                return false;
            }

            return sharedNumbers.TryGetValue(normalized, out value);
        }

        /// <summary>
        /// Attempts to resolve a shared boolean value.
        /// </summary>
        /// <param name="key">Lookup key for the shared value.</param>
        /// <param name="value">The resolved value when available.</param>
        /// <returns>True when a boolean value exists for the provided key.</returns>
        public bool TryGetSharedBool(string key, out bool value)
        {
            string normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized))
            {
                value = default;
                return false;
            }

            return sharedBools.TryGetValue(normalized, out value);
        }

        /// <summary>
        /// Attempts to resolve a shared string value.
        /// </summary>
        /// <param name="key">Lookup key for the shared value.</param>
        /// <param name="value">The resolved value when available.</param>
        /// <returns>True when a string value exists for the provided key.</returns>
        public bool TryGetSharedString(string key, out string value)
        {
            string normalized = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalized))
            {
                value = null;
                return false;
            }

            return sharedStrings.TryGetValue(normalized, out value);
        }

        internal bool CompareNumber(double actual, double expected, NumericComparison comparison, double tolerance)
        {
            switch (comparison)
            {
                case NumericComparison.Equal:
                    return actual.Equals(expected);
                case NumericComparison.NotEqual:
                    return !actual.Equals(expected);
                case NumericComparison.GreaterThan:
                    return actual > expected;
                case NumericComparison.GreaterOrEqual:
                    return actual >= expected;
                case NumericComparison.LessThan:
                    return actual < expected;
                case NumericComparison.LessOrEqual:
                    return actual <= expected;
                case NumericComparison.Approximately:
                    double limit = tolerance > 0d ? tolerance : double.Epsilon;
                    return Math.Abs(actual - expected) <= limit;
                default:
                    return actual.Equals(expected);
            }
        }

        internal bool CompareString(string actual, string expected, StringMatchMode matchMode, bool caseSensitive)
        {
            actual ??= string.Empty;
            expected ??= string.Empty;

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            switch (matchMode)
            {
                case StringMatchMode.Contains:
                    return actual.IndexOf(expected, comparison) >= 0;
                case StringMatchMode.StartsWith:
                    return actual.StartsWith(expected, comparison);
                case StringMatchMode.EndsWith:
                    return actual.EndsWith(expected, comparison);
                default:
                    return string.Equals(actual, expected, comparison);
            }
        }

        static bool OperationUsesSlot(VisualAction action)
        {
            if (action == null)
                return false;

            switch (action.Operation)
            {
                case OperationType.Save:
                case OperationType.Load:
                case OperationType.DeleteSlot:
                case OperationType.RenameSlot:
                    return true;
                case OperationType.RestoreDestroyedGameObject:
                    return action.RestoreDestroyedGameObject.DataSource == RestoreDestroyedDataSource.Slot;
                case OperationType.RestoreDestroyedPrefabByUniqueID:
                case OperationType.RestoreDestroyedPrefabByAssetID:
                    return action.RestoreDestroyedPrefab.DataSource == RestoreDestroyedDataSource.Slot;
                case OperationType.RestoreSingleGameObject:
                    return action.RestoreSingleGameObject.DataSource == RestoreSingleDataSource.Slot;
                case OperationType.DestroyWithSnapshot:
                    return false;
                case OperationType.LoadSceneAfterSnapshotAndPopulate:
                    return false;
                default:
                    return false;
            }
        }

        bool EvaluateConditions(VisualAction action, SaveManager manager, int resolvedSlot, out string message)
        {
            message = null;

            if (action == null)
                return true;

            int? slotForCondition = resolvedSlot > 0 ? resolvedSlot : (int?)null;

            foreach (var condition in action.Conditions)
            {
                if (condition == null)
                    continue;

                if (!condition.Evaluate(this, manager, slotForCondition, out message))
                    return false;
            }

            return true;
        }

        bool TryResolveSlot(VisualAction action, SaveManager manager, out int resolvedSlot, out string message)
        {
            resolvedSlot = -1;
            message = null;

            var latestSlot = manager.GetLatestSaveSlot();
            if (latestSlot != null && latestSlot.SlotNumber > 0)
            {
                latestResolvedSlot = latestSlot.SlotNumber;
            }

            int latest = latestResolvedSlot;
            resolvedSlot = action.Slot.Resolve(latest, out _);

            if (action.Slot.Source == SlotSource.Latest && (latestSlot == null || latestSlot.SlotNumber <= 0))
            {
                message = $"Action '{action.Name}' requested the latest save slot but no saved data was found.";
                return false;
            }

            if (resolvedSlot <= 0)
            {
                message = $"Action '{action.Name}' produced an invalid slot index ({resolvedSlot}).";
                return false;
            }

            latestResolvedSlot = resolvedSlot;
            return true;
        }

        async Task<bool> PerformOperationAsync(VisualAction action, SaveManager manager, int resolvedSlot)
        {
            try
            {
                switch (action.Operation)
                {
                    case OperationType.Save:
                    {
                        string scene = string.IsNullOrWhiteSpace(action.Save.LastActiveScene)
                            ? null
                            : action.Save.LastActiveScene;
                        string slotName = string.IsNullOrWhiteSpace(action.Save.SlotName)
                            ? null
                            : action.Save.SlotName;

                        if (!string.IsNullOrEmpty(slotName))
                        {
                            manager.Save(resolvedSlot, scene, slotName);
                        }
                        else
                        {
                            manager.Save(resolvedSlot, scene);
                        }

                        return true;
                    }
                    case OperationType.Load:
                        manager.Load(
                            resolvedSlot,
                            action.Load.RestoreLastActiveScene,
                            action.Load.LoadAsync,
                            action.Load.AllowSceneActivation);
                        return true;
                    case OperationType.DeleteSlot:
                        return await ExecuteDeleteSlot(action, manager, resolvedSlot);
                    case OperationType.RenameSlot:
                        return await ExecuteRenameSlot(action, manager, resolvedSlot);
                    case OperationType.QuickSave:
                        return await ExecuteQuickSave(action, manager);
                    case OperationType.QuickLoad:
                        return await ExecuteQuickLoad(action, manager);
                case OperationType.AutoSave:
                    return await ExecuteAutoSave(action, manager);
                case OperationType.LoadAutoSave:
                    return await ExecuteLoadAutoSave(action, manager);
                case OperationType.LoadSceneAfterSnapshotAndPopulate:
                    return await ExecuteLoadSceneAfterSnapshotAsync(action, manager);
                case OperationType.RestoreDestroyedGameObject:
                    return await ExecuteRestoreDestroyedGameObject(action, manager, resolvedSlot);
                    case OperationType.RestoreDestroyedPrefabByUniqueID:
                        return await ExecuteRestoreDestroyedPrefab(action, manager, resolvedSlot);
                    case OperationType.RestoreDestroyedPrefabByAssetID:
                        return await ExecuteRestoreDestroyedPrefab(action, manager, resolvedSlot);
                    case OperationType.DestroyGameObjectByUniqueID:
                        return await ExecuteDestroyGameObject(action, manager);
                    case OperationType.DestroyPrefabsByAssetID:
                        return await ExecuteDestroyPrefabsByAssetId(action, manager);
                    case OperationType.ProcessDeferredPrefabs:
                        return await ExecuteProcessDeferredPrefabs(action, manager);
                    case OperationType.ProcessDeferredPrefabsForScene:
                        return await ExecuteProcessDeferredPrefabsForScene(action, manager);
                    case OperationType.ProcessDeferredPrefabsForAsset:
                        return await ExecuteProcessDeferredPrefabsForAsset(action, manager);
                    case OperationType.ProcessDeferredPrefabByUniqueID:
                        return await ExecuteProcessDeferredPrefabByUniqueId(action, manager);
                    case OperationType.ProcessDeferredPrefabsByInstanceIDs:
                        return await ExecuteProcessDeferredPrefabsByInstanceIds(action, manager);
                    case OperationType.RestoreSingleGameObject:
                        return await ExecuteRestoreSingleGameObject(action, manager, resolvedSlot);
                    case OperationType.DestroyWithSnapshot:
                        return ExecuteDestroyWithSnapshot(action, manager);
                    default:
                        Debug.LogWarning($"CrystalSaveVisualActionHub: Operation '{action.Operation}' is not implemented.");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"CrystalSaveVisualActionHub: Operation '{action.Operation}' failed with exception: {ex.Message}");
                return false;
            }
        }

        async Task<bool> ExecuteLoadSceneAfterSnapshotAsync(VisualAction action, SaveManager manager)
        {
            var parameters = action.LoadSceneAfterSnapshotAndPopulate;
            if (parameters == null)
                return false;

            string sceneName;
            if (parameters.UseBuildIndex)
            {
                int buildIndex = parameters.SceneBuildIndex;
                if (buildIndex < 0)
                {
                    Debug.LogWarning("CrystalSaveVisualActionHub: Build index must be non-negative for LoadSceneAfterSnapshotAndPopulate.");
                    return false;
                }

                string scenePath;
                try
                {
                    scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
                }
                catch (ArgumentException ex)
                {
                    Debug.LogWarning($"CrystalSaveVisualActionHub: Invalid build index {buildIndex} ({ex.Message}).");
                    return false;
                }

                if (string.IsNullOrEmpty(scenePath))
                {
                    Debug.LogWarning($"CrystalSaveVisualActionHub: Could not resolve scene path for build index {buildIndex}.");
                    return false;
                }

                sceneName = Path.GetFileNameWithoutExtension(scenePath);
                if (string.IsNullOrEmpty(sceneName))
                {
                    Debug.LogWarning($"CrystalSaveVisualActionHub: Unable to determine scene name from path '{scenePath}'.");
                    return false;
                }
            }
            else
            {
                sceneName = parameters.SceneName;
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    Debug.LogWarning("CrystalSaveVisualActionHub: Scene name is required when not using a build index for LoadSceneAfterSnapshotAndPopulate.");
                    return false;
                }

                sceneName = sceneName.Trim();
            }

            await manager.LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(
                sceneName,
                parameters.LoadAdditive,
                parameters.LoadAsync,
                parameters.AllowDuplicateLoad);

            return true;
        }

        async Task<bool> ExecuteRestoreDestroyedGameObject(VisualAction action, SaveManager manager, int slot)
        {
            var parameters = action.RestoreDestroyedGameObject;
            if (string.IsNullOrWhiteSpace(parameters.UniqueId))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: RestoreDestroyedGameObject requires a Unique ID.");
                return false;
            }

            switch (parameters.DataSource)
            {
                case RestoreDestroyedDataSource.CurrentSaveData:
                    if (parameters.Retry.Enabled)
                    {
                        return await manager.RestoreDestroyedGameObjectFromCurrentDataAsync(
                            parameters.UniqueId,
                            parameters.Retry.MaxAttempts,
                            parameters.Retry.RetryDelayMs);
                    }

                    manager.RestoreDestroyedGameObject(parameters.UniqueId);
                    return true;
                case RestoreDestroyedDataSource.Slot:
                {
                    int attempts = parameters.Retry.Enabled ? parameters.Retry.MaxAttempts : 1;
                    int delay = parameters.Retry.Enabled ? parameters.Retry.RetryDelayMs : 1;
                    return await manager.RestoreDestroyedGameObjectWithRetryAsync(
                        parameters.UniqueId,
                        slot,
                        attempts,
                        delay);
                }
                default:
                    return false;
            }
        }

        async Task<bool> ExecuteRestoreDestroyedPrefab(VisualAction action, SaveManager manager, int slot)
        {
            var parameters = action.RestoreDestroyedPrefab;
            if (string.IsNullOrWhiteSpace(parameters.Identifier))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: RestoreDestroyedPrefab requires an identifier.");
                return false;
            }

            bool interpretAsAssetId = action.Operation == OperationType.RestoreDestroyedPrefabByAssetID || parameters.UseAssetId;

            switch (parameters.DataSource)
            {
                case RestoreDestroyedDataSource.CurrentSaveData:
                    if (interpretAsAssetId)
                    {
                        manager.RestoreDestroyedPrefabByAssetID(parameters.Identifier);
                    }
                    else
                    {
                        manager.RestoreDestroyedPrefab(parameters.Identifier);
                    }

                    return true;
                case RestoreDestroyedDataSource.Slot:
                {
                    int attempts = parameters.Retry.Enabled ? parameters.Retry.MaxAttempts : 1;
                    int delay = parameters.Retry.Enabled ? parameters.Retry.RetryDelayMs : 1;

                    if (interpretAsAssetId)
                    {
                        return await manager.RestoreDestroyedPrefabByAssetIDWithRetryAsync(
                            parameters.Identifier,
                            slot,
                            attempts,
                            delay);
                    }

                    return await manager.RestoreDestroyedPrefabWithRetryAsync(
                        parameters.Identifier,
                        slot,
                        attempts,
                        delay);
                }
                default:
                    return false;
            }
        }

        async Task<bool> ExecuteRestoreSingleGameObject(VisualAction action, SaveManager manager, int slot)
        {
            var parameters = action.RestoreSingleGameObject;
            if (parameters.Target == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: RestoreSingleGameObject requires a target GameObject.");
                return false;
            }

            switch (parameters.DataSource)
            {
                case RestoreSingleDataSource.CurrentSaveData:
                    if (parameters.Retry.Enabled)
                    {
                        return await manager.RestoreSingleGameObjectFromCurrentDataAsync(
                            parameters.Target,
                            parameters.Retry.MaxAttempts,
                            parameters.Retry.RetryDelayMs);
                    }

                    manager.RestoreSingleGameObject(parameters.Target, null, suppressEvent: false);
                    return true;
                case RestoreSingleDataSource.Slot:
                {
                    int attempts = parameters.Retry.Enabled ? parameters.Retry.MaxAttempts : 1;
                    int delay = parameters.Retry.Enabled ? parameters.Retry.RetryDelayMs : 1;
                    return await manager.RestoreSingleGameObjectWithRetryAsync(
                        parameters.Target,
                        slot,
                        attempts,
                        delay);
                }
                default:
                    return false;
            }
        }

        Task<bool> ExecuteDeleteSlot(VisualAction action, SaveManager manager, int slot)
        {
            var parameters = action.DeleteSlot;
            if (parameters.RequireExistingData && !manager.HasSaveAt(slot))
            {
                Debug.LogWarning($"CrystalSaveVisualActionHub: Save slot {slot} has no data to delete.");
                return Task.FromResult(false);
            }

            manager.Delete(slot);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteRenameSlot(VisualAction action, SaveManager manager, int slot)
        {
            var parameters = action.RenameSlot;
            string newName = parameters.NewName?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: RenameSlot requires a non-empty target name.");
                return Task.FromResult(false);
            }

            var slotData = manager.GetSaveSlotByNumber(slot);
            if (slotData == null)
            {
                Debug.LogWarning($"CrystalSaveVisualActionHub: Save slot {slot} does not exist and cannot be renamed.");
                return Task.FromResult(false);
            }

            manager.RenameSlot(slot, newName);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteQuickSave(VisualAction action, SaveManager manager)
        {
            var parameters = action.QuickSave;
            var settings = manager.SaveSettings;
            if (settings == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: SaveSettings asset could not be loaded for QuickSave.");
                return Task.FromResult(false);
            }

            bool configured = settings.numberOfQuickSaveSlots > 0 && settings.quickSaveSlotOffset > 0;
            if (!configured)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: Quick save slots are not configured in SaveSettings.");
                if (parameters.RequireConfiguredSlots)
                    return Task.FromResult(false);
            }

            manager.QuickSave();
            return Task.FromResult(true);
        }

        Task<bool> ExecuteQuickLoad(VisualAction action, SaveManager manager)
        {
            var parameters = action.QuickLoad;
            var settings = manager.SaveSettings;
            if (settings == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: SaveSettings asset could not be loaded for QuickLoad.");
                return Task.FromResult(false);
            }

            if (settings.numberOfQuickSaveSlots <= 0 || settings.quickSaveSlotOffset <= 0)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: Quick save slots are not configured in SaveSettings.");
                return Task.FromResult(false);
            }

            int slot = settings.quickSaveSlotOffset + 1;
            if (parameters.RequireExistingData && !manager.HasSaveAt(slot))
            {
                Debug.LogWarning($"CrystalSaveVisualActionHub: Quick save slot {slot} does not contain data to load.");
                return Task.FromResult(false);
            }

            manager.QuickLoad();
            return Task.FromResult(true);
        }

        Task<bool> ExecuteAutoSave(VisualAction action, SaveManager manager)
        {
            var parameters = action.AutoSave;
            var settings = manager.SaveSettings;
            if (settings == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: SaveSettings asset could not be loaded for AutoSave.");
                return Task.FromResult(false);
            }

            bool hasLegacyAutoSlot = settings.autoSaveSlotNumber > 0;
            bool hasMultiAutoSlots = settings.numberOfAutoSaveSlots > 0;
            if (!hasLegacyAutoSlot && !hasMultiAutoSlots)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: Auto save is not configured in SaveSettings.");
                if (parameters.RequireConfiguredSlot)
                    return Task.FromResult(false);
            }

            manager.AutoSave();
            return Task.FromResult(true);
        }

        Task<bool> ExecuteLoadAutoSave(VisualAction action, SaveManager manager)
        {
            var parameters = action.LoadAutoSave;
            var settings = manager.SaveSettings;
            if (settings == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: SaveSettings asset could not be loaded for LoadAutoSave.");
                return Task.FromResult(false);
            }

            int slot = settings.numberOfAutoSaveSlots > 0
                ? settings.autoSaveSlotOffset + 1
                : settings.autoSaveSlotNumber;
            if (slot <= 0)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: Auto save is not configured in SaveSettings.");
                return Task.FromResult(false);
            }

            if (parameters.RequireExistingData && !manager.HasSaveAt(slot))
            {
                Debug.LogWarning($"CrystalSaveVisualActionHub: Auto save slot {slot} does not contain data to load.");
                return Task.FromResult(false);
            }

            manager.LoadAutoSave(parameters.RestoreScene);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteDestroyGameObject(VisualAction action, SaveManager manager)
        {
            var componentManager = manager.ComponentManager;
            if (componentManager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: ComponentManager is not available for destroy operation.");
                return Task.FromResult(false);
            }

            var parameters = action.DestroyGameObject;
            var batch = SanitizeStringList(parameters.UniqueIds);
            if (batch.Count > 0)
            {
                componentManager.DestroyGameObjectByUniqueID(batch);
                return Task.FromResult(true);
            }

            string singleId = parameters.UniqueId?.Trim();
            if (string.IsNullOrEmpty(singleId))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: DestroyGameObject requires at least one Unique ID.");
                return Task.FromResult(false);
            }

            componentManager.DestroyGameObjectByUniqueID(singleId);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteDestroyPrefabsByAssetId(VisualAction action, SaveManager manager)
        {
            var prefabManager = manager.GetPrefabManager;
            if (prefabManager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: PrefabManager is not available for destroy prefab operation.");
                return Task.FromResult(false);
            }

            var parameters = action.DestroyPrefabsByAssetId;
            var assetIds = SanitizeStringList(parameters.PrefabAssetIds);
            if (assetIds.Count > 0)
            {
                prefabManager.DestroyPrefabsByAssetID(assetIds);
                return Task.FromResult(true);
            }

            string assetId = parameters.PrefabAssetId?.Trim();
            if (string.IsNullOrEmpty(assetId))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: DestroyPrefabsByAssetID requires at least one prefab asset ID.");
                return Task.FromResult(false);
            }

            prefabManager.DestroyPrefabsByAssetID(assetId);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteProcessDeferredPrefabs(VisualAction action, SaveManager manager)
        {
            var prefabManager = manager.GetPrefabManager;
            if (prefabManager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: PrefabManager is not available for processing deferred prefabs.");
                return Task.FromResult(false);
            }

            var parameters = action.ProcessDeferredPrefabs;
            var destroyedIds = SanitizeStringList(parameters.DestroyedGameObjectIds);
            prefabManager.ProcessDeferredPrefabs(destroyedIds.Count > 0 ? destroyedIds : null);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteProcessDeferredPrefabsForScene(VisualAction action, SaveManager manager)
        {
            var prefabManager = manager.GetPrefabManager;
            if (prefabManager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: PrefabManager is not available for scene deferred prefab processing.");
                return Task.FromResult(false);
            }

            var parameters = action.ProcessDeferredPrefabsForScene;
            string scene = parameters.SceneName?.Trim();
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: ProcessDeferredPrefabsForScene requires a scene name.");
                return Task.FromResult(false);
            }

            var destroyedIds = SanitizeStringList(parameters.DestroyedGameObjectIds);
            prefabManager.ProcessDeferredPrefabsForScene(scene, destroyedIds.Count > 0 ? destroyedIds : null);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteProcessDeferredPrefabsForAsset(VisualAction action, SaveManager manager)
        {
            var prefabManager = manager.GetPrefabManager;
            if (prefabManager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: PrefabManager is not available for asset deferred prefab processing.");
                return Task.FromResult(false);
            }

            var parameters = action.ProcessDeferredPrefabsForAsset;
            string assetId = parameters.PrefabAssetId?.Trim();
            if (string.IsNullOrEmpty(assetId))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: ProcessDeferredPrefabsForAsset requires a prefab asset ID.");
                return Task.FromResult(false);
            }

            var destroyedIds = SanitizeStringList(parameters.DestroyedGameObjectIds);
            prefabManager.ProcessDeferredPrefabsForAsset(assetId, destroyedIds.Count > 0 ? destroyedIds : null);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteProcessDeferredPrefabByUniqueId(VisualAction action, SaveManager manager)
        {
            var prefabManager = manager.GetPrefabManager;
            if (prefabManager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: PrefabManager is not available for processing by unique ID.");
                return Task.FromResult(false);
            }

            var parameters = action.ProcessDeferredPrefabByUniqueId;
            string uniqueId = parameters.UniqueId?.Trim();
            if (string.IsNullOrEmpty(uniqueId))
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: ProcessDeferredPrefabByUniqueID requires an instance ID.");
                return Task.FromResult(false);
            }

            var destroyedIds = SanitizeStringList(parameters.DestroyedGameObjectIds);
            prefabManager.ProcessDeferredPrefabByUniqueID(uniqueId, destroyedIds.Count > 0 ? destroyedIds : null);
            return Task.FromResult(true);
        }

        Task<bool> ExecuteProcessDeferredPrefabsByInstanceIds(VisualAction action, SaveManager manager)
        {
            var prefabManager = manager.GetPrefabManager;
            if (prefabManager == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: PrefabManager is not available for processing by instance IDs.");
                return Task.FromResult(false);
            }

            var parameters = action.ProcessDeferredPrefabsByInstanceIds;
            var instanceIds = SanitizeStringList(parameters.InstanceIds);
            if (instanceIds.Count == 0)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: Provide at least one instance ID to process deferred prefabs.");
                return Task.FromResult(false);
            }

            var destroyedIds = SanitizeStringList(parameters.DestroyedGameObjectIds);
            prefabManager.ProcessDeferredPrefabsByInstanceIDs(instanceIds, destroyedIds.Count > 0 ? destroyedIds : null);
            return Task.FromResult(true);
        }

        bool ExecuteDestroyWithSnapshot(VisualAction action, SaveManager manager)
        {
            var parameters = action.DestroyWithSnapshot;
            if (parameters == null)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: DestroyWithSnapshot parameters are not configured.");
                return false;
            }

            var targets = parameters.Targets;
            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: DestroyWithSnapshot requires at least one target GameObject.");
                return false;
            }

            bool executed = false;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    Debug.LogWarning($"CrystalSaveVisualActionHub: DestroyWithSnapshot target at index {i} is null and will be skipped.");
                    continue;
                }

                manager.DestroyWithSnapshot(target, parameters.Destroy, parameters.AllowPooling);
                executed = true;
            }

            if (!executed)
            {
                Debug.LogWarning("CrystalSaveVisualActionHub: DestroyWithSnapshot did not process any targets.");
            }

            return executed;
        }

        static List<string> SanitizeStringList(List<string> source)
        {
            var result = new List<string>();
            if (source == null)
                return result;

            foreach (var entry in source)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                result.Add(entry.Trim());
            }

            return result;
        }
    }
}
#endif
