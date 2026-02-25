#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Arawn.CrystalSave.VisualScripting;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.VisualScripting.Editor
{
    [CustomEditor(typeof(CrystalSaveVisualActionHub))]
    public class CrystalSaveVisualActionHubEditor : UnityEditor.Editor
    {
        ReorderableList sharedValuesList;
        ReorderableList actionList;
        readonly Dictionary<string, ReorderableList> conditionLists = new();

        SerializedProperty sharedValueSeedsProp;
        SerializedProperty actionsProp;
        SerializedProperty onActionSucceededProp;
        SerializedProperty onActionFailedProp;
        SerializedProperty onActionFinishedProp;
        SerializedProperty onAllActionsCompletedProp;

        void OnEnable()
        {
            sharedValueSeedsProp = serializedObject.FindProperty("sharedValueSeeds");
            actionsProp = serializedObject.FindProperty("actions");
            onActionSucceededProp = serializedObject.FindProperty("onActionSucceeded");
            onActionFailedProp = serializedObject.FindProperty("onActionFailed");
            onActionFinishedProp = serializedObject.FindProperty("onActionFinished");
            onAllActionsCompletedProp = serializedObject.FindProperty("onAllActionsCompleted");

            sharedValuesList = new ReorderableList(serializedObject, sharedValueSeedsProp, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Shared Values"),
                elementHeightCallback = index =>
                {
                    var element = sharedValueSeedsProp.GetArrayElementAtIndex(index);
                    return DrawSharedValueSeed(element, new Rect(0f, 0f, EditorGUIUtility.currentViewWidth - 60f, 0f), false)
                           + EditorGUIUtility.standardVerticalSpacing;
                },
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = sharedValueSeedsProp.GetArrayElementAtIndex(index);
                    DrawSharedValueSeed(element, rect, true);
                },
                onAddCallback = list =>
                {
                    int newIndex = sharedValueSeedsProp.arraySize;
                    sharedValueSeedsProp.arraySize++;
                    var seed = sharedValueSeedsProp.GetArrayElementAtIndex(newIndex);
                    seed.FindPropertyRelative("key").stringValue = string.Empty;
                    seed.FindPropertyRelative("valueType").enumValueIndex = (int)CrystalSaveVisualActionHub.SharedValueType.Number;
                    seed.FindPropertyRelative("numberValue").doubleValue = 0d;
                    seed.FindPropertyRelative("boolValue").boolValue = false;
                    seed.FindPropertyRelative("stringValue").stringValue = string.Empty;
                }
            };

            actionList = new ReorderableList(serializedObject, actionsProp, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Actions"),
                elementHeightCallback = index =>
                {
                    var element = actionsProp.GetArrayElementAtIndex(index);
                    return DrawAction(element, new Rect(0f, 0f, EditorGUIUtility.currentViewWidth - 60f, 0f), false)
                           + EditorGUIUtility.standardVerticalSpacing;
                },
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = actionsProp.GetArrayElementAtIndex(index);
                    DrawAction(element, rect, true);
                },
                onAddCallback = list =>
                {
                    int newIndex = actionsProp.arraySize;
                    actionsProp.arraySize++;
                    var newElement = actionsProp.GetArrayElementAtIndex(newIndex);
                    newElement.FindPropertyRelative("name").stringValue = $"Action {newIndex + 1}";
                    newElement.FindPropertyRelative("operation").enumValueIndex = (int)CrystalSaveVisualActionHub.OperationType.Load;
                    var slotProp = newElement.FindPropertyRelative("slot");
                    slotProp.FindPropertyRelative("source").enumValueIndex = (int)CrystalSaveVisualActionHub.SlotSource.Latest;
                    slotProp.FindPropertyRelative("explicitSlot").intValue = 1;
                    slotProp.FindPropertyRelative("designTimeSlot").intValue = 1;
                    newElement.FindPropertyRelative("conditions").arraySize = 0;
                }
            };
        }

        void OnDisable()
        {
            conditionLists.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            sharedValuesList.DoLayoutList();
            DrawSharedValueApiHelp();
            EditorGUILayout.Space();

            actionList.DoLayoutList();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(onActionSucceededProp);
            EditorGUILayout.PropertyField(onActionFailedProp);
            EditorGUILayout.PropertyField(onActionFinishedProp);
            EditorGUILayout.PropertyField(onAllActionsCompletedProp);

            EditorGUILayout.Space();
            DrawDiagnostics();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawDiagnostics()
        {
            var hub = (CrystalSaveVisualActionHub)target;

            if (Application.isPlaying)
            {
                var manager = SaveManager.Instance;
                if (manager != null)
                {
                    var latest = manager.GetLatestSaveSlot();
                    if (latest != null && latest.SlotNumber > 0)
                    {
                        EditorGUILayout.HelpBox($"Latest save slot detected: {latest.SlotNumber}", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No save data detected by SaveManager.", MessageType.Warning);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("SaveManager.Instance is not available.", MessageType.Warning);
                }

                if (hub.LatestResolvedSlot > 0)
                {
                    EditorGUILayout.HelpBox($"Hub cached slot: {hub.LatestResolvedSlot}", MessageType.None);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view live slot diagnostics.", MessageType.Info);
            }
        }

        float DrawSharedValueSeed(SerializedProperty seedProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var keyProp = seedProp.FindPropertyRelative("key");
            var typeProp = seedProp.FindPropertyRelative("valueType");
            var numberProp = seedProp.FindPropertyRelative("numberValue");
            var boolProp = seedProp.FindPropertyRelative("boolValue");
            var stringProp = seedProp.FindPropertyRelative("stringValue");

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), keyProp);
            y += EditorGUI.GetPropertyHeight(keyProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), typeProp);
            y += EditorGUI.GetPropertyHeight(typeProp) + spacing;

            var valueType = (CrystalSaveVisualActionHub.SharedValueType)typeProp.enumValueIndex;
            switch (valueType)
            {
                case CrystalSaveVisualActionHub.SharedValueType.Number:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), numberProp);
                    y += EditorGUI.GetPropertyHeight(numberProp) + spacing;
                    break;
                case CrystalSaveVisualActionHub.SharedValueType.Bool:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), boolProp);
                    y += EditorGUI.GetPropertyHeight(boolProp) + spacing;
                    break;
                case CrystalSaveVisualActionHub.SharedValueType.String:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), stringProp);
                    y += EditorGUI.GetPropertyHeight(stringProp) + spacing;
                    break;
            }

            if (string.IsNullOrWhiteSpace(keyProp.stringValue))
            {
                float helpHeight = lineHeight * 1.6f;
                if (render)
                    EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, helpHeight), "Key is required.", MessageType.Warning);
                y += helpHeight + spacing;
            }

            return y - rect.y;
        }

        void DrawSharedValueApiHelp()
        {
            EditorGUILayout.LabelField("Shared Value API", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use the methods below to push runtime data into the hub from UnityEvents or custom scripts.", MessageType.Info);
            DrawSharedValueApiRow("Number", "SetSharedNumber(string key, float value)", "Assigns a numeric shared value that conditions can read. A double overload is also available.");
            DrawSharedValueApiRow("Bool", "SetSharedBool(string key, bool value)", "Assigns a boolean shared value that conditions can read.");
            DrawSharedValueApiRow("String", "SetSharedString(string key, string value)", "Assigns a string shared value that conditions can read.");
        }

        void DrawSharedValueApiRow(string label, string signature, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(label, tooltip), GUILayout.Width(EditorGUIUtility.labelWidth));
                EditorGUILayout.SelectableLabel(signature, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button(new GUIContent("Copy", $"Copy {signature} to the clipboard."), GUILayout.Width(60f)))
                {
                    EditorGUIUtility.systemCopyBuffer = signature;
                }
            }
        }

        float DrawAction(SerializedProperty actionProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var nameProp = actionProp.FindPropertyRelative("name");
            var operationProp = actionProp.FindPropertyRelative("operation");
            var slotProp = actionProp.FindPropertyRelative("slot");
            var saveProp = actionProp.FindPropertyRelative("save");
            var loadProp = actionProp.FindPropertyRelative("load");
            var loadSceneAfterSnapshotProp = actionProp.FindPropertyRelative("loadSceneAfterSnapshotAndPopulate");
            var restoreGoProp = actionProp.FindPropertyRelative("restoreDestroyedGameObject");
            var restorePrefabProp = actionProp.FindPropertyRelative("restoreDestroyedPrefab");
            var restoreSingleProp = actionProp.FindPropertyRelative("restoreSingleGameObject");
            var deleteSlotProp = actionProp.FindPropertyRelative("deleteSlot");
            var renameSlotProp = actionProp.FindPropertyRelative("renameSlot");
            var quickSaveProp = actionProp.FindPropertyRelative("quickSave");
            var quickLoadProp = actionProp.FindPropertyRelative("quickLoad");
            var autoSaveProp = actionProp.FindPropertyRelative("autoSave");
            var loadAutoSaveProp = actionProp.FindPropertyRelative("loadAutoSave");
            var destroyGoProp = actionProp.FindPropertyRelative("destroyGameObject");
            var destroyPrefabAssetProp = actionProp.FindPropertyRelative("destroyPrefabsByAssetId");
            var processDeferredProp = actionProp.FindPropertyRelative("processDeferredPrefabs");
            var processDeferredSceneProp = actionProp.FindPropertyRelative("processDeferredPrefabsForScene");
            var processDeferredAssetProp = actionProp.FindPropertyRelative("processDeferredPrefabsForAsset");
            var processDeferredUniqueProp = actionProp.FindPropertyRelative("processDeferredPrefabByUniqueId");
            var processDeferredInstanceProp = actionProp.FindPropertyRelative("processDeferredPrefabsByInstanceIds");
            var destroyWithSnapshotProp = actionProp.FindPropertyRelative("destroyWithSnapshot");
            var conditionsProp = actionProp.FindPropertyRelative("conditions");
            var onSuccessProp = actionProp.FindPropertyRelative("onSuccess");
            var onFailureProp = actionProp.FindPropertyRelative("onFailure");
            var onFinishedProp = actionProp.FindPropertyRelative("onFinished");

            bool expanded = actionProp.isExpanded;
            string header = string.IsNullOrEmpty(nameProp.stringValue)
                ? ((CrystalSaveVisualActionHub.OperationType)operationProp.enumValueIndex).ToString()
                : nameProp.stringValue;

            Rect foldoutRect = new Rect(rect.x, y, rect.width, lineHeight);
            if (render)
            {
                expanded = EditorGUI.Foldout(foldoutRect, expanded, header, true);
                actionProp.isExpanded = expanded;
            }
            y += lineHeight;

            if (!expanded)
            {
                return lineHeight;
            }

            y += spacing;
            int originalIndent = EditorGUI.indentLevel;
            if (render)
                EditorGUI.indentLevel++;

            Rect fieldRect = new Rect(rect.x, y, rect.width, lineHeight);
            if (render)
                EditorGUI.PropertyField(fieldRect, nameProp);
            y += EditorGUI.GetPropertyHeight(nameProp) + spacing;

            fieldRect.y = y;
            if (render)
                EditorGUI.PropertyField(fieldRect, operationProp);
            y += EditorGUI.GetPropertyHeight(operationProp) + spacing;

            var operation = (CrystalSaveVisualActionHub.OperationType)operationProp.enumValueIndex;
            bool showSlot = ShouldShowSlot(actionProp, operation, conditionsProp);
            if (showSlot)
            {
                float slotHeight = DrawSlotReference(slotProp, new Rect(rect.x, y, rect.width, 0f), render);
                y += slotHeight + spacing;
            }

            switch (operation)
            {
                case CrystalSaveVisualActionHub.OperationType.Save:
                    y += DrawSaveParameters(saveProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.Load:
                    y += DrawLoadParameters(loadProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.DeleteSlot:
                    y += DrawDeleteSlotParameters(deleteSlotProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.RenameSlot:
                    y += DrawRenameSlotParameters(renameSlotProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.QuickSave:
                    y += DrawQuickSaveParameters(quickSaveProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.QuickLoad:
                    y += DrawQuickLoadParameters(quickLoadProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.AutoSave:
                    y += DrawAutoSaveParameters(autoSaveProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.LoadAutoSave:
                    y += DrawLoadAutoSaveParameters(loadAutoSaveProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.LoadSceneAfterSnapshotAndPopulate:
                    y += DrawLoadSceneAfterSnapshotParameters(loadSceneAfterSnapshotProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.RestoreDestroyedGameObject:
                    y += DrawRestoreDestroyedGameObjectParameters(restoreGoProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.RestoreDestroyedPrefabByUniqueID:
                case CrystalSaveVisualActionHub.OperationType.RestoreDestroyedPrefabByAssetID:
                    y += DrawRestoreDestroyedPrefabParameters(restorePrefabProp, new Rect(rect.x, y, rect.width, 0f), render, operation) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.RestoreSingleGameObject:
                    y += DrawRestoreSingleGameObjectParameters(restoreSingleProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.DestroyGameObjectByUniqueID:
                    y += DrawDestroyGameObjectParameters(destroyGoProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.DestroyPrefabsByAssetID:
                    y += DrawDestroyPrefabsByAssetIdParameters(destroyPrefabAssetProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.DestroyWithSnapshot:
                    y += DrawDestroyWithSnapshotParameters(destroyWithSnapshotProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.ProcessDeferredPrefabs:
                    y += DrawProcessDeferredPrefabsParameters(processDeferredProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.ProcessDeferredPrefabsForScene:
                    y += DrawProcessDeferredPrefabsForSceneParameters(processDeferredSceneProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.ProcessDeferredPrefabsForAsset:
                    y += DrawProcessDeferredPrefabsForAssetParameters(processDeferredAssetProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.ProcessDeferredPrefabByUniqueID:
                    y += DrawProcessDeferredPrefabByUniqueIdParameters(processDeferredUniqueProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
                case CrystalSaveVisualActionHub.OperationType.ProcessDeferredPrefabsByInstanceIDs:
                    y += DrawProcessDeferredPrefabsByInstanceIdsParameters(processDeferredInstanceProp, new Rect(rect.x, y, rect.width, 0f), render) + spacing;
                    break;
            }

            var conditionsList = GetConditionList(conditionsProp);
            float conditionHeight = conditionsList.GetHeight();
            if (render)
            {
                Rect listRect = new Rect(rect.x, y, rect.width, conditionHeight);
                conditionsList.DoList(listRect);
            }
            y += conditionHeight + spacing;

            float successHeight = EditorGUI.GetPropertyHeight(onSuccessProp, true);
            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, successHeight), onSuccessProp, true);
            y += successHeight + spacing;

            float failureHeight = EditorGUI.GetPropertyHeight(onFailureProp, true);
            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, failureHeight), onFailureProp, true);
            y += failureHeight + spacing;

            float finishedHeight = EditorGUI.GetPropertyHeight(onFinishedProp, true);
            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, finishedHeight), onFinishedProp, true);
            y += finishedHeight;

            if (render)
                EditorGUI.indentLevel = originalIndent;

            return y - rect.y;
        }

        float DrawSlotReference(SerializedProperty slotProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var sourceProp = slotProp.FindPropertyRelative("source");
            var explicitProp = slotProp.FindPropertyRelative("explicitSlot");
            var designProp = slotProp.FindPropertyRelative("designTimeSlot");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Slot Selection", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sourceProp);
            y += EditorGUI.GetPropertyHeight(sourceProp) + spacing;

            var source = (CrystalSaveVisualActionHub.SlotSource)sourceProp.enumValueIndex;
            if (source == CrystalSaveVisualActionHub.SlotSource.DesignTime)
            {
                if (render)
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), designProp, new GUIContent("Design Time Slot"));
                y += EditorGUI.GetPropertyHeight(designProp) + spacing;
            }
            else
            {
                string label = source == CrystalSaveVisualActionHub.SlotSource.Latest ? "Fallback Slot" : "Slot Number";
                if (render)
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), explicitProp, new GUIContent(label));
                y += EditorGUI.GetPropertyHeight(explicitProp) + spacing;
            }

            return y - rect.y;
        }

        float DrawSaveParameters(SerializedProperty saveProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var sceneProp = saveProp.FindPropertyRelative("lastActiveScene");
            var slotNameProp = saveProp.FindPropertyRelative("slotName");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Save Parameters", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sceneProp);
            y += EditorGUI.GetPropertyHeight(sceneProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), slotNameProp);
            y += EditorGUI.GetPropertyHeight(slotNameProp);

            return y - rect.y;
        }

        float DrawLoadParameters(SerializedProperty loadProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var restoreSceneProp = loadProp.FindPropertyRelative("restoreLastActiveScene");
            var loadAsyncProp = loadProp.FindPropertyRelative("loadAsync");
            var allowActivationProp = loadProp.FindPropertyRelative("allowSceneActivation");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Load Parameters", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), restoreSceneProp);
            y += EditorGUI.GetPropertyHeight(restoreSceneProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), loadAsyncProp);
            y += EditorGUI.GetPropertyHeight(loadAsyncProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), allowActivationProp);
            y += EditorGUI.GetPropertyHeight(allowActivationProp);

            return y - rect.y;
        }

        float DrawLoadSceneAfterSnapshotParameters(SerializedProperty parametersProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var useBuildIndexProp = parametersProp.FindPropertyRelative("useBuildIndex");
            var sceneNameProp = parametersProp.FindPropertyRelative("sceneName");
            var sceneBuildIndexProp = parametersProp.FindPropertyRelative("sceneBuildIndex");
            var loadAdditiveProp = parametersProp.FindPropertyRelative("loadAdditive");
            var loadAsyncProp = parametersProp.FindPropertyRelative("loadAsync");
            var allowDuplicateProp = parametersProp.FindPropertyRelative("allowDuplicateLoad");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Load Scene After Snapshot", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), useBuildIndexProp, new GUIContent("Use Build Index"));
            y += EditorGUI.GetPropertyHeight(useBuildIndexProp) + spacing;

            bool useBuildIndex = useBuildIndexProp.boolValue;
            if (useBuildIndex)
            {
                if (render)
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sceneBuildIndexProp, new GUIContent("Scene Build Index"));
                y += EditorGUI.GetPropertyHeight(sceneBuildIndexProp) + spacing;

                if (sceneBuildIndexProp.intValue < 0)
                {
                    float warnHeight = lineHeight * 1.6f;
                    if (render)
                        EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Build index must be zero or greater.", MessageType.Warning);
                    y += warnHeight + spacing;
                }
            }
            else
            {
                if (render)
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sceneNameProp, new GUIContent("Scene Name"));
                y += EditorGUI.GetPropertyHeight(sceneNameProp) + spacing;

                if (string.IsNullOrWhiteSpace(sceneNameProp.stringValue))
                {
                    float warnHeight = lineHeight * 1.6f;
                    if (render)
                        EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Scene name is required when not using a build index.", MessageType.Warning);
                    y += warnHeight + spacing;
                }
            }

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), loadAdditiveProp, new GUIContent("Load Additive"));
            y += EditorGUI.GetPropertyHeight(loadAdditiveProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), loadAsyncProp, new GUIContent("Load Asynchronously"));
            y += EditorGUI.GetPropertyHeight(loadAsyncProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), allowDuplicateProp, new GUIContent("Allow Duplicate Load"));
            y += EditorGUI.GetPropertyHeight(allowDuplicateProp);

            return y - rect.y;
        }

        float DrawDeleteSlotParameters(SerializedProperty deleteProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var requireProp = deleteProp.FindPropertyRelative("requireExistingData");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Delete Slot", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), requireProp, new GUIContent("Require Saved Data"));
            y += EditorGUI.GetPropertyHeight(requireProp) + spacing;

            if (render)
            {
                float helpHeight = lineHeight * 2f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, helpHeight),
                    "Deletes the resolved slot. Quick and auto slots are defined in SaveSettings.", MessageType.Info);
                y += helpHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawRenameSlotParameters(SerializedProperty renameProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var newNameProp = renameProp.FindPropertyRelative("newName");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Rename Slot", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), newNameProp, new GUIContent("New Name"));
            y += EditorGUI.GetPropertyHeight(newNameProp) + spacing;

            if (render && string.IsNullOrWhiteSpace(newNameProp.stringValue))
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Enter the new slot name.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawQuickSaveParameters(SerializedProperty quickSaveProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var requireProp = quickSaveProp.FindPropertyRelative("requireConfiguredSlots");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Quick Save", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), requireProp, new GUIContent("Require Configured Slots"));
            y += EditorGUI.GetPropertyHeight(requireProp) + spacing;

            if (render)
            {
                float helpHeight = lineHeight * 2f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, helpHeight),
                    "Uses the quick save slot range defined in SaveSettings.", MessageType.Info);
                y += helpHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawQuickLoadParameters(SerializedProperty quickLoadProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var requireProp = quickLoadProp.FindPropertyRelative("requireExistingData");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Quick Load", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), requireProp, new GUIContent("Require Saved Data"));
            y += EditorGUI.GetPropertyHeight(requireProp) + spacing;

            if (render)
            {
                float helpHeight = lineHeight * 2f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, helpHeight),
                    "Loads the most recent quick save slot based on SaveSettings offsets.", MessageType.Info);
                y += helpHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawAutoSaveParameters(SerializedProperty autoSaveProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var requireProp = autoSaveProp.FindPropertyRelative("requireConfiguredSlot");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Auto Save", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), requireProp, new GUIContent("Require Slot Number"));
            y += EditorGUI.GetPropertyHeight(requireProp) + spacing;

            if (render)
            {
                float helpHeight = lineHeight * 2f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, helpHeight),
                    "Saves into the auto save slot configured in SaveSettings.", MessageType.Info);
                y += helpHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawLoadAutoSaveParameters(SerializedProperty loadAutoSaveProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var restoreProp = loadAutoSaveProp.FindPropertyRelative("restoreScene");
            var requireProp = loadAutoSaveProp.FindPropertyRelative("requireExistingData");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Load Auto Save", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), restoreProp, new GUIContent("Restore Scene"));
            y += EditorGUI.GetPropertyHeight(restoreProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), requireProp, new GUIContent("Require Saved Data"));
            y += EditorGUI.GetPropertyHeight(requireProp) + spacing;

            if (render)
            {
                float helpHeight = lineHeight * 2f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, helpHeight),
                    "Loads the auto save slot configured in SaveSettings.", MessageType.Info);
                y += helpHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawRestoreDestroyedGameObjectParameters(SerializedProperty restoreProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var idProp = restoreProp.FindPropertyRelative("uniqueId");
            var dataSourceProp = restoreProp.FindPropertyRelative("dataSource");
            var retryProp = restoreProp.FindPropertyRelative("retry");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Restore Destroyed GameObject", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), idProp, new GUIContent("Unique ID"));
            y += EditorGUI.GetPropertyHeight(idProp) + spacing;

            if (render && string.IsNullOrWhiteSpace(idProp.stringValue))
            {
                Rect warnRect = new Rect(rect.x, y, rect.width, lineHeight * 1.6f);
                EditorGUI.HelpBox(warnRect, "Unique ID is required for this operation.", MessageType.Warning);
                y += warnRect.height + spacing;
            }

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), dataSourceProp);
            y += EditorGUI.GetPropertyHeight(dataSourceProp) + spacing;

            y += DrawRetrySettings(retryProp, new Rect(rect.x, y, rect.width, 0f), render);

            return y - rect.y;
        }

        float DrawRestoreDestroyedPrefabParameters(SerializedProperty restoreProp, Rect rect, bool render, CrystalSaveVisualActionHub.OperationType operation)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var idProp = restoreProp.FindPropertyRelative("identifier");
            var useAssetProp = restoreProp.FindPropertyRelative("useAssetId");
            var dataSourceProp = restoreProp.FindPropertyRelative("dataSource");
            var retryProp = restoreProp.FindPropertyRelative("retry");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Restore Destroyed Prefab", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), idProp, new GUIContent("Identifier"));
            y += EditorGUI.GetPropertyHeight(idProp) + spacing;

            if (render && string.IsNullOrWhiteSpace(idProp.stringValue))
            {
                Rect warnRect = new Rect(rect.x, y, rect.width, lineHeight * 1.6f);
                EditorGUI.HelpBox(warnRect, "Provide a unique ID or prefab asset ID.", MessageType.Warning);
                y += warnRect.height + spacing;
            }

            bool forceAssetId = operation == CrystalSaveVisualActionHub.OperationType.RestoreDestroyedPrefabByAssetID;
            if (forceAssetId && !useAssetProp.boolValue)
                useAssetProp.boolValue = true;

            if (render)
            {
                EditorGUI.BeginDisabledGroup(forceAssetId);
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), useAssetProp, new GUIContent("Interpret As Asset ID"));
                EditorGUI.EndDisabledGroup();
            }
            y += EditorGUI.GetPropertyHeight(useAssetProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), dataSourceProp);
            y += EditorGUI.GetPropertyHeight(dataSourceProp) + spacing;

            y += DrawRetrySettings(retryProp, new Rect(rect.x, y, rect.width, 0f), render);

            return y - rect.y;
        }

        float DrawRestoreSingleGameObjectParameters(SerializedProperty restoreProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var targetProp = restoreProp.FindPropertyRelative("target");
            var dataSourceProp = restoreProp.FindPropertyRelative("dataSource");
            var retryProp = restoreProp.FindPropertyRelative("retry");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Restore Single GameObject", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), targetProp);
            y += EditorGUI.GetPropertyHeight(targetProp) + spacing;

            if (render && targetProp.objectReferenceValue == null)
            {
                Rect warnRect = new Rect(rect.x, y, rect.width, lineHeight * 1.6f);
                EditorGUI.HelpBox(warnRect, "Assign the GameObject to restore.", MessageType.Warning);
                y += warnRect.height + spacing;
            }

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), dataSourceProp);
            y += EditorGUI.GetPropertyHeight(dataSourceProp) + spacing;

            y += DrawRetrySettings(retryProp, new Rect(rect.x, y, rect.width, 0f), render);

            return y - rect.y;
        }

        float DrawDestroyGameObjectParameters(SerializedProperty destroyProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var uniqueProp = destroyProp.FindPropertyRelative("uniqueId");
            var listProp = destroyProp.FindPropertyRelative("uniqueIds");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Destroy GameObjects", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), uniqueProp, new GUIContent("Unique ID"));
            y += EditorGUI.GetPropertyHeight(uniqueProp) + spacing;

            y += DrawStringList(listProp, new Rect(rect.x, y, rect.width, 0f), render, "Unique ID List") + spacing;

            if (render && string.IsNullOrWhiteSpace(uniqueProp.stringValue) && !HasValidStringEntries(listProp))
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Provide at least one Unique ID.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawDestroyPrefabsByAssetIdParameters(SerializedProperty destroyProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var assetProp = destroyProp.FindPropertyRelative("prefabAssetId");
            var listProp = destroyProp.FindPropertyRelative("prefabAssetIds");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Destroy Prefabs", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), assetProp, new GUIContent("Asset ID"));
            y += EditorGUI.GetPropertyHeight(assetProp) + spacing;

            y += DrawStringList(listProp, new Rect(rect.x, y, rect.width, 0f), render, "Asset ID List") + spacing;

            if (render && string.IsNullOrWhiteSpace(assetProp.stringValue) && !HasValidStringEntries(listProp))
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Provide at least one prefab asset ID.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawDestroyWithSnapshotParameters(SerializedProperty destroyProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            if (destroyProp == null)
                return 0f;

            var targetsProp = destroyProp.FindPropertyRelative("targets");
            var destroyFlagProp = destroyProp.FindPropertyRelative("destroy");
            var allowPoolingProp = destroyProp.FindPropertyRelative("allowPooling");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Destroy With Snapshot", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            float targetsHeight = EditorGUI.GetPropertyHeight(targetsProp, true);
            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, targetsHeight), targetsProp, new GUIContent("Targets"), true);
            y += targetsHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), destroyFlagProp, new GUIContent("Destroy GameObjects"));
            y += EditorGUI.GetPropertyHeight(destroyFlagProp) + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), allowPoolingProp, new GUIContent("Allow Pooling"));
            y += EditorGUI.GetPropertyHeight(allowPoolingProp) + spacing;

            bool hasTarget = false;
            if (targetsProp != null)
            {
                for (int i = 0; i < targetsProp.arraySize; i++)
                {
                    if (targetsProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    {
                        hasTarget = true;
                        break;
                    }
                }
            }

            if (render && !hasTarget)
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Add at least one target GameObject.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawProcessDeferredPrefabsParameters(SerializedProperty processProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var destroyedProp = processProp.FindPropertyRelative("destroyedGameObjectIds");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Process Deferred Prefabs", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            y += DrawStringList(destroyedProp, new Rect(rect.x, y, rect.width, 0f), render, "Consumed Unique IDs") + spacing;

            if (render)
            {
                float helpHeight = lineHeight * 2f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, helpHeight),
                    "Spawns any queued prefabs. Optional destroyed IDs clear existing entries before spawning.", MessageType.Info);
                y += helpHeight + spacing;
            }

            return y - rect.y;
        }

        float DrawProcessDeferredPrefabsForSceneParameters(SerializedProperty processProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var sceneProp = processProp.FindPropertyRelative("sceneName");
            var destroyedProp = processProp.FindPropertyRelative("destroyedGameObjectIds");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Process Deferred Prefabs (Scene)", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sceneProp, new GUIContent("Scene Name"));
            y += EditorGUI.GetPropertyHeight(sceneProp) + spacing;

            if (render && string.IsNullOrWhiteSpace(sceneProp.stringValue))
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Specify the scene that owns the deferred prefabs.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            y += DrawStringList(destroyedProp, new Rect(rect.x, y, rect.width, 0f), render, "Consumed Unique IDs") + spacing;

            return y - rect.y;
        }

        float DrawProcessDeferredPrefabsForAssetParameters(SerializedProperty processProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var assetProp = processProp.FindPropertyRelative("prefabAssetId");
            var destroyedProp = processProp.FindPropertyRelative("destroyedGameObjectIds");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Process Deferred Prefabs (Asset)", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), assetProp, new GUIContent("Asset ID"));
            y += EditorGUI.GetPropertyHeight(assetProp) + spacing;

            if (render && string.IsNullOrWhiteSpace(assetProp.stringValue))
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Specify the prefab asset ID to process.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            y += DrawStringList(destroyedProp, new Rect(rect.x, y, rect.width, 0f), render, "Consumed Unique IDs") + spacing;

            return y - rect.y;
        }

        float DrawProcessDeferredPrefabByUniqueIdParameters(SerializedProperty processProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var uniqueProp = processProp.FindPropertyRelative("uniqueId");
            var destroyedProp = processProp.FindPropertyRelative("destroyedGameObjectIds");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Process Deferred Prefab (Unique ID)", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), uniqueProp, new GUIContent("Instance ID"));
            y += EditorGUI.GetPropertyHeight(uniqueProp) + spacing;

            if (render && string.IsNullOrWhiteSpace(uniqueProp.stringValue))
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Enter the queued prefab instance ID.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            y += DrawStringList(destroyedProp, new Rect(rect.x, y, rect.width, 0f), render, "Consumed Unique IDs") + spacing;

            return y - rect.y;
        }

        float DrawProcessDeferredPrefabsByInstanceIdsParameters(SerializedProperty processProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var instanceProp = processProp.FindPropertyRelative("instanceIds");
            var destroyedProp = processProp.FindPropertyRelative("destroyedGameObjectIds");

            if (render)
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), "Process Deferred Prefabs (List)", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            y += DrawStringList(instanceProp, new Rect(rect.x, y, rect.width, 0f), render, "Instance IDs") + spacing;

            if (render && !HasValidStringEntries(instanceProp))
            {
                float warnHeight = lineHeight * 1.6f;
                EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Add one or more instance IDs to spawn.", MessageType.Warning);
                y += warnHeight + spacing;
            }

            y += DrawStringList(destroyedProp, new Rect(rect.x, y, rect.width, 0f), render, "Consumed Unique IDs") + spacing;

            return y - rect.y;
        }

        float DrawStringList(SerializedProperty listProp, Rect rect, bool render, string label)
        {
            float height = EditorGUI.GetPropertyHeight(listProp, true);
            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, height), listProp, new GUIContent(label), true);
            return height;
        }

        float DrawRetrySettings(SerializedProperty retryProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var enabledProp = retryProp.FindPropertyRelative("enabled");
            var attemptsProp = retryProp.FindPropertyRelative("maxAttempts");
            var delayProp = retryProp.FindPropertyRelative("retryDelayMs");

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), enabledProp, new GUIContent("Use Retry"));
            y += EditorGUI.GetPropertyHeight(enabledProp);

            if (enabledProp.boolValue)
            {
                y += spacing;
                if (render)
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), attemptsProp, new GUIContent("Max Attempts"));
                y += EditorGUI.GetPropertyHeight(attemptsProp) + spacing;

                if (render)
                    EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), delayProp, new GUIContent("Delay (ms)"));
                y += EditorGUI.GetPropertyHeight(delayProp);
            }

            return y - rect.y + spacing;
        }

        ReorderableList GetConditionList(SerializedProperty conditionsProp)
        {
            if (!conditionLists.TryGetValue(conditionsProp.propertyPath, out var list))
            {
                list = new ReorderableList(conditionsProp.serializedObject, conditionsProp, true, true, true, true)
                {
                    drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Conditions"),
                    elementHeightCallback = index =>
                    {
                        var element = conditionsProp.GetArrayElementAtIndex(index);
                        return DrawCondition(element, new Rect(0f, 0f, EditorGUIUtility.currentViewWidth - 80f, 0f), false)
                               + EditorGUIUtility.standardVerticalSpacing;
                    },
                    drawElementCallback = (rect, index, active, focused) =>
                    {
                        var element = conditionsProp.GetArrayElementAtIndex(index);
                        DrawCondition(element, rect, true);
                    },
                    onAddCallback = l =>
                    {
                        int newIndex = conditionsProp.arraySize;
                        conditionsProp.arraySize++;
                        var condition = conditionsProp.GetArrayElementAtIndex(newIndex);
                        condition.FindPropertyRelative("type").enumValueIndex = (int)CrystalSaveVisualActionHub.ConditionType.Always;
                        condition.FindPropertyRelative("slotSource").enumValueIndex = (int)CrystalSaveVisualActionHub.ConditionSlotSource.UseActionSlot;
                        condition.FindPropertyRelative("slotNumber").intValue = 1;
                        condition.FindPropertyRelative("sceneName").stringValue = string.Empty;
                        condition.FindPropertyRelative("sharedValueKey").stringValue = string.Empty;
                        condition.FindPropertyRelative("expectedNumber").doubleValue = 0d;
                        condition.FindPropertyRelative("numericComparison").enumValueIndex = (int)CrystalSaveVisualActionHub.NumericComparison.Equal;
                        condition.FindPropertyRelative("useNumericTolerance").boolValue = false;
                        condition.FindPropertyRelative("numericTolerance").doubleValue = 0.001d;
                        condition.FindPropertyRelative("expectedBool").boolValue = true;
                        condition.FindPropertyRelative("expectedString").stringValue = string.Empty;
                        condition.FindPropertyRelative("stringMatchMode").enumValueIndex = (int)CrystalSaveVisualActionHub.StringMatchMode.Exact;
                        condition.FindPropertyRelative("stringCaseSensitive").boolValue = false;
                        condition.FindPropertyRelative("earliestSaveDateIso").stringValue = string.Empty;
                    }
                };

                conditionLists[conditionsProp.propertyPath] = list;
            }

            return list;
        }

        float DrawCondition(SerializedProperty conditionProp, Rect rect, bool render)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = rect.y;

            var typeProp = conditionProp.FindPropertyRelative("type");
            var slotSourceProp = conditionProp.FindPropertyRelative("slotSource");
            var slotNumberProp = conditionProp.FindPropertyRelative("slotNumber");
            var sceneNameProp = conditionProp.FindPropertyRelative("sceneName");
            var sharedKeyProp = conditionProp.FindPropertyRelative("sharedValueKey");
            var expectedNumberProp = conditionProp.FindPropertyRelative("expectedNumber");
            var numericComparisonProp = conditionProp.FindPropertyRelative("numericComparison");
            var useToleranceProp = conditionProp.FindPropertyRelative("useNumericTolerance");
            var numericToleranceProp = conditionProp.FindPropertyRelative("numericTolerance");
            var expectedBoolProp = conditionProp.FindPropertyRelative("expectedBool");
            var expectedStringProp = conditionProp.FindPropertyRelative("expectedString");
            var stringMatchModeProp = conditionProp.FindPropertyRelative("stringMatchMode");
            var stringCaseSensitiveProp = conditionProp.FindPropertyRelative("stringCaseSensitive");
            var earliestDateProp = conditionProp.FindPropertyRelative("earliestSaveDateIso");

            if (render)
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), typeProp);
            y += EditorGUI.GetPropertyHeight(typeProp) + spacing;

            var type = (CrystalSaveVisualActionHub.ConditionType)typeProp.enumValueIndex;
            switch (type)
            {
                case CrystalSaveVisualActionHub.ConditionType.HasSaveInSlot:
                case CrystalSaveVisualActionHub.ConditionType.CurrentSlotEquals:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), slotSourceProp);
                    y += EditorGUI.GetPropertyHeight(slotSourceProp) + spacing;

                    if ((CrystalSaveVisualActionHub.ConditionSlotSource)slotSourceProp.enumValueIndex == CrystalSaveVisualActionHub.ConditionSlotSource.SpecificSlot)
                    {
                        if (render)
                            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), slotNumberProp);
                        y += EditorGUI.GetPropertyHeight(slotNumberProp) + spacing;
                    }
                    break;
                case CrystalSaveVisualActionHub.ConditionType.HasSaveInScene:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sceneNameProp);
                    y += EditorGUI.GetPropertyHeight(sceneNameProp) + spacing;

                    if (render && string.IsNullOrWhiteSpace(sceneNameProp.stringValue))
                    {
                        Rect warnRect = new Rect(rect.x, y, rect.width, lineHeight * 1.6f);
                        EditorGUI.HelpBox(warnRect, "Scene name is required.", MessageType.Warning);
                        y += warnRect.height + spacing;
                    }
                    break;
                case CrystalSaveVisualActionHub.ConditionType.SharedNumber:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sharedKeyProp);
                    y += EditorGUI.GetPropertyHeight(sharedKeyProp) + spacing;

                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), numericComparisonProp);
                    y += EditorGUI.GetPropertyHeight(numericComparisonProp) + spacing;

                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), expectedNumberProp);
                    y += EditorGUI.GetPropertyHeight(expectedNumberProp) + spacing;

                    var numericComparison = (CrystalSaveVisualActionHub.NumericComparison)numericComparisonProp.enumValueIndex;
                    if (numericComparison == CrystalSaveVisualActionHub.NumericComparison.Approximately)
                    {
                        if (render)
                            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), useToleranceProp, new GUIContent("Use Tolerance"));
                        y += EditorGUI.GetPropertyHeight(useToleranceProp) + spacing;

                        if (useToleranceProp.boolValue)
                        {
                            if (render)
                                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), numericToleranceProp, new GUIContent("Tolerance"));
                            y += EditorGUI.GetPropertyHeight(numericToleranceProp) + spacing;
                        }
                    }
                    else if (render && useToleranceProp.boolValue)
                    {
                        useToleranceProp.boolValue = false;
                    }

                    if (string.IsNullOrWhiteSpace(sharedKeyProp.stringValue) && render)
                    {
                        Rect warnRect = new Rect(rect.x, y, rect.width, lineHeight * 1.6f);
                        EditorGUI.HelpBox(warnRect, "Shared key is required.", MessageType.Warning);
                        y += warnRect.height + spacing;
                    }
                    else if (string.IsNullOrWhiteSpace(sharedKeyProp.stringValue))
                    {
                        y += lineHeight * 1.6f + spacing;
                    }
                    break;
                case CrystalSaveVisualActionHub.ConditionType.SharedBool:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sharedKeyProp);
                    y += EditorGUI.GetPropertyHeight(sharedKeyProp) + spacing;

                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), expectedBoolProp, new GUIContent("Expected Value"));
                    y += EditorGUI.GetPropertyHeight(expectedBoolProp) + spacing;

                    if (string.IsNullOrWhiteSpace(sharedKeyProp.stringValue) && render)
                    {
                        Rect warnRect = new Rect(rect.x, y, rect.width, lineHeight * 1.6f);
                        EditorGUI.HelpBox(warnRect, "Shared key is required.", MessageType.Warning);
                        y += warnRect.height + spacing;
                    }
                    else if (string.IsNullOrWhiteSpace(sharedKeyProp.stringValue))
                    {
                        y += lineHeight * 1.6f + spacing;
                    }
                    break;
                case CrystalSaveVisualActionHub.ConditionType.SharedString:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), sharedKeyProp);
                    y += EditorGUI.GetPropertyHeight(sharedKeyProp) + spacing;

                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), stringMatchModeProp, new GUIContent("Match Mode"));
                    y += EditorGUI.GetPropertyHeight(stringMatchModeProp) + spacing;

                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), expectedStringProp, new GUIContent("Expected Value"));
                    y += EditorGUI.GetPropertyHeight(expectedStringProp) + spacing;

                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), stringCaseSensitiveProp, new GUIContent("Case Sensitive"));
                    y += EditorGUI.GetPropertyHeight(stringCaseSensitiveProp) + spacing;

                    if (string.IsNullOrWhiteSpace(sharedKeyProp.stringValue) && render)
                    {
                        Rect warnRect = new Rect(rect.x, y, rect.width, lineHeight * 1.6f);
                        EditorGUI.HelpBox(warnRect, "Shared key is required.", MessageType.Warning);
                        y += warnRect.height + spacing;
                    }
                    else if (string.IsNullOrWhiteSpace(sharedKeyProp.stringValue))
                    {
                        y += lineHeight * 1.6f + spacing;
                    }
                    break;
                case CrystalSaveVisualActionHub.ConditionType.HasSaveAfterDate:
                    if (render)
                        EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), earliestDateProp, new GUIContent("Earliest Save Date"));
                    y += EditorGUI.GetPropertyHeight(earliestDateProp) + spacing;

                    string dateText = earliestDateProp.stringValue;
                    if (string.IsNullOrWhiteSpace(dateText))
                    {
                        float warnHeight = lineHeight * 1.6f;
                        if (render)
                            EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "A valid ISO 8601 date/time is required.", MessageType.Warning);
                        y += warnHeight + spacing;
                    }
                    else if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _) &&
                             !DateTime.TryParse(dateText, out _))
                    {
                        float warnHeight = lineHeight * 1.6f;
                        if (render)
                            EditorGUI.HelpBox(new Rect(rect.x, y, rect.width, warnHeight), "Unable to parse the provided date string.", MessageType.Error);
                        y += warnHeight + spacing;
                    }
                    break;
            }

            return y - rect.y;
        }

        bool ShouldShowSlot(SerializedProperty actionProp, CrystalSaveVisualActionHub.OperationType operation, SerializedProperty conditionsProp)
        {
            if (operation == CrystalSaveVisualActionHub.OperationType.Save ||
                operation == CrystalSaveVisualActionHub.OperationType.Load ||
                operation == CrystalSaveVisualActionHub.OperationType.DeleteSlot ||
                operation == CrystalSaveVisualActionHub.OperationType.RenameSlot)
            {
                return true;
            }

            switch (operation)
            {
                case CrystalSaveVisualActionHub.OperationType.RestoreDestroyedGameObject:
                    return actionProp.FindPropertyRelative("restoreDestroyedGameObject")
                                     .FindPropertyRelative("dataSource").enumValueIndex == (int)CrystalSaveVisualActionHub.RestoreDestroyedDataSource.Slot
                           || ConditionsRequireSlot(conditionsProp);
                case CrystalSaveVisualActionHub.OperationType.RestoreDestroyedPrefabByUniqueID:
                case CrystalSaveVisualActionHub.OperationType.RestoreDestroyedPrefabByAssetID:
                    return actionProp.FindPropertyRelative("restoreDestroyedPrefab")
                                     .FindPropertyRelative("dataSource").enumValueIndex == (int)CrystalSaveVisualActionHub.RestoreDestroyedDataSource.Slot
                           || ConditionsRequireSlot(conditionsProp);
                case CrystalSaveVisualActionHub.OperationType.RestoreSingleGameObject:
                    return actionProp.FindPropertyRelative("restoreSingleGameObject")
                                     .FindPropertyRelative("dataSource").enumValueIndex == (int)CrystalSaveVisualActionHub.RestoreSingleDataSource.Slot
                           || ConditionsRequireSlot(conditionsProp);
                case CrystalSaveVisualActionHub.OperationType.LoadSceneAfterSnapshotAndPopulate:
                    return ConditionsRequireSlot(conditionsProp);
                default:
                    return ConditionsRequireSlot(conditionsProp);
            }
        }

        bool ConditionsRequireSlot(SerializedProperty conditionsProp)
        {
            for (int i = 0; i < conditionsProp.arraySize; i++)
            {
                var conditionProp = conditionsProp.GetArrayElementAtIndex(i);
                var typeProp = conditionProp.FindPropertyRelative("type");
                var slotSourceProp = conditionProp.FindPropertyRelative("slotSource");

                var conditionType = (CrystalSaveVisualActionHub.ConditionType)typeProp.enumValueIndex;
                if ((conditionType == CrystalSaveVisualActionHub.ConditionType.HasSaveInSlot ||
                     conditionType == CrystalSaveVisualActionHub.ConditionType.CurrentSlotEquals) &&
                    (CrystalSaveVisualActionHub.ConditionSlotSource)slotSourceProp.enumValueIndex == CrystalSaveVisualActionHub.ConditionSlotSource.UseActionSlot)
                {
                    return true;
                }
            }

            return false;
        }

        bool HasValidStringEntries(SerializedProperty listProp)
        {
            if (listProp == null)
                return false;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                if (!string.IsNullOrWhiteSpace(element.stringValue))
                    return true;
            }

            return false;
        }
    }
}
#endif
