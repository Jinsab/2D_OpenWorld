using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JeffGrawAssets.FlexibleUI
{
[CustomEditor(typeof(UIBlur))]
[CanEditMultipleObjects]
public class UIBlurEditor : Editor
{
    private static GUIStyle _warningStyle;
    private static GUIStyle WarningStyle => _warningStyle ??= new GUIStyle(EditorStyles.textField) { normal = { textColor = Color.yellow }, alignment = TextAnchor.MiddleCenter };
    private static GUIStyle _errorStyle;
    private static GUIStyle ErrorStyle => _errorStyle ??= new GUIStyle(EditorStyles.textField) { normal = { textColor = Color.red }, alignment = TextAnchor.MiddleCenter };

    public static readonly GUIContent LayerContent = new ("Layer", $"The layer to which this Blur belongs. Lower layers are drawn first. {nameof(FlexibleBlurFeature)}.{FlexibleBlurFeature.UIBlurLayersSeeLowerFieldName}, {nameof(FlexibleBlurFeature)}.{FlexibleBlurFeature.BlurredImagesSeeUIBlursFieldName}, and {nameof(FlexibleBlurFeature)}.{FlexibleBlurFeature.BlurredImageLayersSeeLowerFieldName} control how layers interact with one another.");
    public static readonly GUIContent PriorityContent = new ("Priority", "The order in which Blurs are processed within a layer (lower values are drawn first).");
    public static readonly GUIContent BlurStrengthContent = new ("Blur Strength", "The overall strength of the blur effect. For static blurs (eg. blurs where the strength property never changes), it is *always* preferable to leave this at 1 and modify downscale/blur passes to achieve the desired outcome. Blurs with different blur strengths can never be batched.");
    private static readonly GUIContent CameraReferenceSelfContent = new ("Camera Reference", $"The Camera that supplies the blur effect. In other words the Camera whose output is seen by the component.");
    private static readonly GUIContent StayActiveAtZeroCanvasAlphaContent = new ("Active At 0 Canvas Alpha", $"By default, when Canvas alpha is 0 the {nameof(UIBlur)} will turn off to conserve performance. If you're trying to achieve a \"subtractive blur\" effect, this may be unwanted.");

    private ReorderableList downscaleSectionList;
    private ReorderableList blurSectionList;

    private BlurPresetEditor _blurEditor;
    private BlurPresetEditor BlurEditor => _blurEditor != null
                                         ? _blurEditor
                                         : _blurEditor = target is IBlur blur && blur.Common.blurPreset != null
                                             ? (BlurPresetEditor)CreateEditor(blur.Common.blurPreset) 
                                             : null;

    private SerializedProperty stayActiveAtZeroCanvasAlphaProperty;
    private SerializedProperty cameraReferencePoperty;
    private SerializedProperty blurStrengthProperty;
    private SerializedProperty priorityProperty;
    private SerializedProperty layerProperty;
    private SerializedProperty blurPresetProperty;
    private SerializedProperty blurInstanceSettingsProperty;
    private BlurPreset prevBlurPreset;

    void OnEnable()
    {
        stayActiveAtZeroCanvasAlphaProperty = serializedObject.FindProperty(nameof(UIBlur.zeroCanvasAlphaActive));
        cameraReferencePoperty = serializedObject.FindProperty($"{UIBlur.BlurCommonFieldName}.{nameof(UIBlurCommon.cameraReference)}");
        blurStrengthProperty = serializedObject.FindProperty($"{UIBlur.BlurCommonFieldName}.{nameof(UIBlurCommon.blurStrength)}");
        layerProperty = serializedObject.FindProperty($"{UIBlur.BlurCommonFieldName}.{nameof(UIBlurCommon.unrankedLayer)}");
        priorityProperty = serializedObject.FindProperty($"{UIBlur.BlurCommonFieldName}.{nameof(UIBlurCommon.priority)}");
        blurPresetProperty = serializedObject.FindProperty($"{UIBlur.BlurCommonFieldName}.{nameof(UIBlurCommon.blurPreset)}");
        blurInstanceSettingsProperty = serializedObject.FindProperty($"{UIBlur.BlurCommonFieldName}.{nameof(UIBlurCommon.blurInstanceSettings)}");
    }
    
    void OnDisable()
    {
        if (_blurEditor == null)
            return;

        DestroyImmediate(_blurEditor);
        _blurEditor = null;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(stayActiveAtZeroCanvasAlphaProperty, StayActiveAtZeroCanvasAlphaContent);
        if (!DrawBlurCommonPropertiesOne(cameraReferencePoperty, ((UIBlur)serializedObject.targetObject).gameObject, nameof(UIBlur)))
            return;

        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        EditorGUILayout.PropertyField(blurStrengthProperty, BlurStrengthContent);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.PropertyField(layerProperty, LayerContent);
        EditorGUILayout.PropertyField(priorityProperty, PriorityContent);
        EditorGUILayout.EndVertical();

        _ = BlurEditor;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        DrawBlurCommonPropertiesTwo(serializedObject, blurPresetProperty, blurInstanceSettingsProperty, EditorGUIUtility.labelWidth, ref _blurEditor, ref prevBlurPreset, ref downscaleSectionList, ref blurSectionList);
        serializedObject.ApplyModifiedProperties();
    }

    public static bool DrawBlurCommonPropertiesOne(SerializedProperty cameraReferenceSelf, GameObject go, string componentName)
    {
        var isPreviewScene = EditorSceneManager.IsPreviewScene(go.scene);
        var canvas = go.GetComponentInParent<Canvas>(true);
        if (!isPreviewScene && !canvas)
        {
            EditorGUILayout.LabelField($"No parent canvas detected! {componentName} Only works as part of a UGUI UI.", ErrorStyle);
            return false;
        }

        EditorGUILayout.PropertyField(cameraReferenceSelf, CameraReferenceSelfContent);
        var cameraReference = cameraReferenceSelf.objectReferenceValue as Camera;
        if (!cameraReference)
        {
            if (Camera.main)
                EditorGUILayout.LabelField("No Camera reference supplied. Using Camera.main.", WarningStyle);
            else
                EditorGUILayout.LabelField("No Camera reference supplied.", ErrorStyle);
        }
        return isPreviewScene || DrawFeatureWarning(cameraReference ?? Camera.main);
    }

    public static void DrawBlurCommonPropertiesTwo(SerializedObject serializedObject, SerializedProperty blurPresetProperty, SerializedProperty blurInstanceSettings, float originalLabelWidth, ref BlurPresetEditor presetEditor, ref BlurPreset previousBlurPreset, ref ReorderableList downscaleSections, ref ReorderableList blurSections)
    {
        var blurPreset = blurPresetProperty.objectReferenceValue as BlurPreset;
        if (blurPreset == null)
        {
            var propertyRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 35, EditorGUIUtility.singleLineHeight);
            var buttonRect = propertyRect;
            buttonRect.width = 50;
            buttonRect.x = originalLabelWidth + 20;
            EditorGUIUtility.labelWidth += 50;

            EditorGUI.PropertyField(propertyRect, blurPresetProperty);
            EditorGUIUtility.labelWidth = originalLabelWidth;

            if (GUI.Button(buttonRect, "New"))
            {
                presetEditor = null;
                blurPreset = CreateInstance<BlurPreset>();
                var path = PresetSavePath.GetPresetSavePath("New Blur Preset.asset");
                AssetDatabase.CreateAsset(blurPreset, path);
                AssetDatabase.SaveAssets();
                blurPreset.TryFillSettings();

                var instanceSettings = blurInstanceSettings.boxedValue as BlurSettings;
                foreach (var qualityLevelSettings in blurPreset.Settings)
                    qualityLevelSettings.CopySettings(instanceSettings);

                blurPresetProperty.objectReferenceValue = blurPreset;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("No preset selected—using instance settings.", WarningStyle);
            BlurPresetEditor.DrawBlurProperties(blurInstanceSettings.Copy(), ref downscaleSections, ref blurSections);
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(blurPresetProperty);
            if (EditorGUI.EndChangeCheck())
                blurPreset.TryFillSettings();

            EditorGUILayout.EndVertical();

            if (blurPreset == previousBlurPreset)
                presetEditor.DrawGUI();
            else
                presetEditor = null;
        }
        previousBlurPreset = blurPreset;
    }

    public static bool DrawFeatureWarning(Camera camera)
    {
        if (camera == null)
            return true;

        var data = camera.GetUniversalAdditionalCameraData();

        var rendererIdxField = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        var rendererIdxFieldObj = rendererIdxField?.GetValue(data);
        if (rendererIdxFieldObj is not int rendererIdx)
            return true;

        var currentPipelineAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (currentPipelineAsset == null)
            currentPipelineAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;

        if (currentPipelineAsset == null)
            return true;

        // -1 signifies the default renderer
        if (rendererIdx == -1)
        {
            var defaultRendererIdxField = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            var defaultRendererIdxFieldObj = defaultRendererIdxField?.GetValue(currentPipelineAsset);
            if (defaultRendererIdxFieldObj is not int actualRendererIdx)
                return true;

            rendererIdx = actualRendererIdx;
        }

#if UNITY_2023_2_OR_NEWER
        var rendererData = currentPipelineAsset.rendererDataList[rendererIdx];
#else
        var dataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        var dataListObj = dataListField?.GetValue(currentPipelineAsset);
        if (dataListObj is not ScriptableRendererData[] dataList)
            return true;
        var rendererData = dataList[rendererIdx];
#endif

        if (!rendererData.rendererFeatures.All(x => x is not FlexibleBlurFeature))
            return true;

        EditorGUILayout.LabelField($"{camera.name} is missing {nameof(FlexibleBlurFeature)} renderer feature!", ErrorStyle);
        if (GUILayout.Button("Open Renderer?", GUILayout.Height(28)))
        {
            EditorGUIUtility.PingObject(rendererData);
            Selection.activeObject = rendererData;
        }
        EditorGUILayout.Space(24);

        return true;
    }
}
}