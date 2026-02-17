using UnityEditor;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace JeffGrawAssets.FlexibleUI
{
    [CustomEditor(typeof(FlexibleBlurFeature))]
    public class FlexibleBlurFeatureEditor : Editor
    {
        private static readonly GUIContent GlobalBlurPauseContent = new ("Pause (global)", "Ceases blur processing altogether (existing blurred areas will remain. When a blur layer is added, blurs will process again during that frame)");
        private static readonly GUIContent BlurTypesHandledContent = new ("Blur Types Handled", $"Which types of blur the feature handles. If you want different Render Pass Events for {nameof(UIBlur)}s and blurred Images, add separate FlexibleBlurFeatures.");
        private static readonly GUIContent RenderPassEventContent = new ("Render Pass Event", "What stage of the rendering pipeline blurs are drawn in. Incorrect settings can cause blowout.");
        private static readonly GUIContent UIBlurLayersSeeLowerContent = new ("UIBlur Layers See Lower", $"Higher {nameof(UIBlur)} layers blur results of lower layers. Costs one blit per layer.");
        private static readonly GUIContent blurredImagesSeeUIBlursContent = new ("Blurred Images See UIBlurs", $"Blurred Images will blur the results of {nameof(UIBlur)}s.");
        private static readonly GUIContent blurredImageLayersSeeLowerContent = new ("Blurred Images See Lower", "Higher blurred Image layers blur results of lower layers. Costs one blit and texture per layer.");
        private static readonly GUIContent UseComputeShadersContent = new ("Use Compute Shaders", "Use compute instead of fragment shaders for blur computation. Compute shaders save draw calls and are likely faster on most platforms, but it is recommended to test. Does not affect blits.");
        private static readonly GUIContent ResultFormatContent = new ("Result Format", "Color format of blur textures. Generally don't change unless needed.");
        private static readonly GUIContent BlurFormatContent = new ("Blur Format", "Format for blur computation. Lower precision can be significantly faster, but may introduce banding, especially when differences in the source texture are subtle.");
        private static readonly GUIContent Format8BpcContent = new ("8bpc", "Set formats to 8bpc. Faster, but more prone to banding.");
        private static readonly GUIContent FormatHybridBpcContent = new ("8/16bpc", "Sets blur format to 16bpc and result to 8bpc. Compromise between the other two options.");
        private static readonly GUIContent Format16BpcContent = new ("16bpc", "Set formats to 16bpc. Slower, but less prone to banding.");
        private static readonly GUIContent LayerResolutionRatioContent = new ("Layer Resolution Ratio", "The size of blur layer result textures relative to camera resolution. For example, at 2160p camera resolution and 0.5 ratio, the layer resolution will be 1080p. ");
        private static readonly GUIContent MaxLayerResolutionContent = new ("Max Layer Resolution", "The maximum resolution of blur layer result textures. Regardless of ratio, layer textures will not exceed this value.");
        private static readonly GUIContent OverlayCanvasCompatibilityFixContent = new ("Overlay Compatibility Fix", "Uses a less optimized blit method for overlay canvases. Leave this disabled unless you notice issues.");

        private static readonly string ApplyToAllButtonText = "Apply To All";
        private static readonly string PlatformSettingsText = "Platform Settings";

        private SerializedProperty blurTypesHandledProperty;
        private SerializedProperty renderPassEventProperty;
        private SerializedProperty destinationFilterModeProperty;
        private SerializedProperty uiBlurLayersSeeLowerProperty;
        private SerializedProperty blurredImagesSeeUIBlursProperty;
        private SerializedProperty blurredImageLayersSeeLowerProperty;
        private SerializedProperty useComputeShadersProperty;
        private SerializedProperty resultFormatProperty;
        private SerializedProperty blurFormatProperty;
        private SerializedProperty layerResolutionRatioProperty;
        private SerializedProperty maxLayerResolutionProperty;
        private SerializedProperty platformDataProperty;
        private SerializedProperty overlayCompatibilityFixProperty;

        private Dictionary<string, (bool useComputeShaders, GraphicsFormat resultFormat, GraphicsFormat blurFormat, float layerResolutionRatio, int maxLayerResolution)> platformDataDict;
        private string[] platformNames;
        private int platformIdx;

        private void OnEnable()
        {
            blurTypesHandledProperty = serializedObject.FindProperty(FlexibleBlurFeature.BlursHandledFieldName);
            renderPassEventProperty = serializedObject.FindProperty(FlexibleBlurFeature.RenderPassEventFieldName);
            destinationFilterModeProperty = serializedObject.FindProperty(FlexibleBlurFeature.DestinationFilterModeFieldName);
            uiBlurLayersSeeLowerProperty = serializedObject.FindProperty(FlexibleBlurFeature.UIBlurLayersSeeLowerFieldName);
            blurredImagesSeeUIBlursProperty = serializedObject.FindProperty(FlexibleBlurFeature.BlurredImagesSeeUIBlursFieldName);
            blurredImageLayersSeeLowerProperty = serializedObject.FindProperty(FlexibleBlurFeature.BlurredImageLayersSeeLowerFieldName);
            useComputeShadersProperty = serializedObject.FindProperty(FlexibleBlurFeature.UseComputeShadersFieldName);
            resultFormatProperty = serializedObject.FindProperty(FlexibleBlurFeature.ResultFormatFieldName);
            blurFormatProperty = serializedObject.FindProperty(FlexibleBlurFeature.BlurFormatFieldName);
            layerResolutionRatioProperty = serializedObject.FindProperty(FlexibleBlurFeature.LayerResolutionRatioFieldName);
            maxLayerResolutionProperty = serializedObject.FindProperty(FlexibleBlurFeature.MaxLayerResolutionFieldName);
            platformDataProperty = serializedObject.FindProperty(FlexibleBlurFeature.PlatformDataFieldName);
            overlayCompatibilityFixProperty = serializedObject.FindProperty(FlexibleBlurFeature.OverlayCompatibilityFixFieldName);
            platformDataDict = FlexibleBlurFeature.DecodePlatformData(platformDataProperty.stringValue);

            var platformNamesList = new List<string>();
            foreach (BuildTarget buildTarget in Enum.GetValues(typeof(BuildTarget)))
            {
                var group = BuildPipeline.GetBuildTargetGroup(buildTarget);
                if (group != BuildTargetGroup.Unknown && BuildPipeline.IsBuildTargetSupported(group, buildTarget))
                    platformNamesList.Add(BuildPipeline.GetBuildTargetName(buildTarget));
            }

            platformNames = new string[platformNamesList.Count];
            platformNamesList.CopyTo(platformNames);
            var selectedBuildTarget = BuildPipeline.GetBuildTargetName(GetEditorBuildTarget());
            platformIdx = Array.IndexOf(platformNames, selectedBuildTarget);

            foreach (var key in platformNames)
            {
                if (!platformDataDict.ContainsKey(key))
                    platformDataDict[key] = (false, GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.R16G16B16A16_SFloat, 1f, 1080);
            }

            var currentPlatformData = platformDataDict[selectedBuildTarget];
            useComputeShadersProperty.boolValue = currentPlatformData.useComputeShaders;
            resultFormatProperty.enumValueIndex = (int)currentPlatformData.resultFormat;
            blurFormatProperty.enumValueIndex = (int)currentPlatformData.blurFormat;
            layerResolutionRatioProperty.floatValue = currentPlatformData.layerResolutionRatio;
            maxLayerResolutionProperty.intValue = currentPlatformData.maxLayerResolution;
            platformDataProperty.stringValue = FlexibleBlurFeature.EncodePlatformData(platformDataDict);
            SceneView.RepaintAll();
        }

        public void OnDisable()
        {
            var feature = (FlexibleBlurFeature)target;
            if (feature?.platformData == null || feature.platformData.Length == 0)
                return;

            feature.UsePlatformSettings(GetEditorBuildTarget());
            feature.Dispose();
            feature.Create();
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FlexibleBlurFeature.GloballyPaused = EditorGUILayout.Toggle(GlobalBlurPauseContent, FlexibleBlurFeature.GloballyPaused);

            var originalLabelWidth = EditorGUIUtility.labelWidth;
            var modifiedLabelWidth = originalLabelWidth;
            if (originalLabelWidth < 160)
                modifiedLabelWidth = Mathf.Min(160, Mathf.Max(120, EditorGUIUtility.currentViewWidth - 55));

            var blurTypesHandledValue = (FlexibleBlurFeature.BlurTypesHandled)blurTypesHandledProperty.enumValueFlag;
            EditorGUILayout.PropertyField(renderPassEventProperty, RenderPassEventContent);
            EditorGUILayout.PropertyField(blurTypesHandledProperty, BlurTypesHandledContent);
            EditorGUILayout.PropertyField(destinationFilterModeProperty);

            var handlesUIBlur = (blurTypesHandledValue & FlexibleBlurFeature.BlurTypesHandled.UIBlur) != 0;
            var handlesBlurredImage = (blurTypesHandledValue & FlexibleBlurFeature.BlurTypesHandled.BlurredImage) != 0;

            EditorGUIUtility.labelWidth = modifiedLabelWidth;
            EditorGUILayout.PropertyField(overlayCompatibilityFixProperty, OverlayCanvasCompatibilityFixContent);
            if (handlesUIBlur)
                EditorGUILayout.PropertyField(uiBlurLayersSeeLowerProperty, UIBlurLayersSeeLowerContent);
            if (handlesUIBlur && handlesBlurredImage)
                EditorGUILayout.PropertyField(blurredImagesSeeUIBlursProperty, blurredImagesSeeUIBlursContent);
            if (handlesBlurredImage)
                EditorGUILayout.PropertyField(blurredImageLayersSeeLowerProperty, blurredImageLayersSeeLowerContent);
            EditorGUIUtility.labelWidth = originalLabelWidth;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(PlatformSettingsText);

            var prevPlatformIdx = platformIdx;

            EditorGUILayout.BeginHorizontal();
            platformIdx = EditorGUILayout.Popup(platformIdx, platformNames);

            var selectedBuildTarget = platformNames[platformIdx];
            var currentPlatformData = platformDataDict[selectedBuildTarget];

            if (GUILayout.Button(ApplyToAllButtonText))
            {
                foreach (var key in platformDataDict.Keys)
                    platformDataDict[key] = currentPlatformData;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginVertical(GUI.skin.box);

            modifiedLabelWidth = originalLabelWidth;
            if (originalLabelWidth < 135)
                modifiedLabelWidth = Mathf.Min(135, Mathf.Max(120, EditorGUIUtility.currentViewWidth - 55));

            EditorGUIUtility.labelWidth = modifiedLabelWidth;
            useComputeShadersProperty.boolValue = currentPlatformData.useComputeShaders;
            EditorGUILayout.PropertyField(useComputeShadersProperty, UseComputeShadersContent);
            var useComputeShadersValue = useComputeShadersProperty.boolValue;
            EditorGUIUtility.labelWidth = originalLabelWidth;

            layerResolutionRatioProperty.floatValue = currentPlatformData.layerResolutionRatio;
            EditorGUILayout.PropertyField(layerResolutionRatioProperty, LayerResolutionRatioContent);
            layerResolutionRatioProperty.floatValue = Mathf.Clamp(layerResolutionRatioProperty.floatValue, 0.1f, 1.0f);
            var layerResolutionRatioValue = layerResolutionRatioProperty.floatValue;

            maxLayerResolutionProperty.intValue = currentPlatformData.maxLayerResolution;
            EditorGUILayout.PropertyField(maxLayerResolutionProperty, MaxLayerResolutionContent);
            maxLayerResolutionProperty.intValue = Mathf.Max(maxLayerResolutionProperty.intValue, 64);
            var maxLayerResolutionValue = maxLayerResolutionProperty.intValue;

            var resultFormatValue = (GraphicsFormat)EditorGUILayout.EnumPopup(ResultFormatContent, currentPlatformData.resultFormat);
            var blurFormatValue = (GraphicsFormat)EditorGUILayout.EnumPopup(BlurFormatContent, currentPlatformData.blurFormat);

            var isSetTo8Bpc = resultFormatValue == GraphicsFormat.R8G8B8A8_UNorm && blurFormatValue == GraphicsFormat.R8G8B8A8_UNorm;
            var isSetToHybridBpc = resultFormatValue == GraphicsFormat.R8G8B8A8_UNorm && blurFormatValue == GraphicsFormat.R16G16B16A16_SFloat;
            var isSetTo16Bpc = resultFormatValue == GraphicsFormat.R16G16B16A16_SFloat && blurFormatValue == GraphicsFormat.R16G16B16A16_SFloat;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(0, true);

            EditorGUI.BeginDisabledGroup(isSetTo8Bpc);
            if (GUILayout.Button(Format8BpcContent))
                resultFormatValue = blurFormatValue = GraphicsFormat.R8G8B8A8_UNorm;
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(isSetToHybridBpc);
            if (GUILayout.Button(FormatHybridBpcContent))
            {
                resultFormatValue = GraphicsFormat.R8G8B8A8_UNorm;
                blurFormatValue = GraphicsFormat.R16G16B16A16_SFloat;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(isSetTo16Bpc);
            if (GUILayout.Button(Format16BpcContent))
                resultFormatValue = blurFormatValue = GraphicsFormat.R16G16B16A16_SFloat;
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(0, true);
            EditorGUILayout.EndHorizontal();

            var formatTuple = (useComputeShadersValue, resultFormatValue, blurFormatValue, layerResolutionRatioValue, maxLayerResolutionValue);
            if (currentPlatformData != formatTuple)
            {
                platformDataDict[selectedBuildTarget] = formatTuple;
                platformDataProperty.stringValue = FlexibleBlurFeature.EncodePlatformData(platformDataDict);
            }
            if (currentPlatformData != formatTuple || prevPlatformIdx != platformIdx)
            {
                useComputeShadersProperty.boolValue = useComputeShadersValue;
                resultFormatProperty.enumValueIndex = (int)resultFormatValue;
                blurFormatProperty.enumValueIndex = (int)blurFormatValue;
                layerResolutionRatioProperty.floatValue = layerResolutionRatioValue;
                maxLayerResolutionProperty.intValue = maxLayerResolutionValue;
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private BuildTarget GetEditorBuildTarget()
        {
            const BuildTarget ret =
#if UNITY_EDITOR_WIN
                BuildTarget.StandaloneWindows64;
#elif UNITY_EDITOR_OSX
                BuildTarget.StandaloneOSX;
#elif UNITY_EDITOR_LINUX
                BuildTarget.StandaloneLinux64;
#endif
            var retName = BuildPipeline.GetBuildTargetName(ret);
            return Array.IndexOf(platformNames, retName) != -1 ? ret : EditorUserBuildSettings.activeBuildTarget;
        }
    }
}