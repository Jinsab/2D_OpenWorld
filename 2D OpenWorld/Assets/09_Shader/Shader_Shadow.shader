Shader "Shader Graphs/ShadowPlayer"
{
    Properties
    {
        _Color("_Color", Color) = (0, 0, 0, 1)
        [NoScaleOffset]_MainTex("_MainTex", 2D) = "white" {}
        _BlurAmount("BlurAmount", Float) = 0.005
        [HideInInspector]White("Color", Color) = (1, 1, 1, 1)
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "UniversalMaterialType" = "Lit"
            "Queue"="Transparent"
            // DisableBatching: <None>
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalSpriteLitSubTarget"
        }
        Pass
        {
            Name "Sprite Lit"
            Tags
            {
                "LightMode" = "Universal2D"
            }
            Stencil
            {
                Ref 1          // 기준값 1
                Comp NotEqual  // 이미 1인 곳(그려진 곳)은 그리지 않음
                Pass Replace   // 처음 그리는 곳은 1로 채움
            }
        
        // Render State
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma exclude_renderers d3d11_9x
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_0
        #pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_1
        #pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_2
        #pragma multi_compile _ USE_SHAPE_LIGHT_TYPE_3
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        #pragma multi_compile_vertex _ SKINNED_SPRITE
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define VARYINGS_NEED_SCREENPOSITION
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SPRITELIT
        #define ALPHA_CLIP_THRESHOLD 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 texCoord0;
             float4 color;
             float4 screenPosition;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float4 color : INTERP1;
             float4 screenPosition : INTERP2;
             float3 positionWS : INTERP3;
             float3 normalWS : INTERP4;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.screenPosition.xyzw = input.screenPosition;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.screenPosition = input.screenPosition.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _MainTex_TexelSize;
        float _BlurAmount;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Negate_float(float In, out float Out)
        {
            Out = -1 * In;
        }
        
        void Unity_Maximum_float(float A, float B, out float Out)
        {
            Out = max(A, B);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Clamp_float(float In, float Min, float Max, out float Out)
        {
            Out = clamp(In, Min, Max);
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float4 SpriteMask;
            float3 NormalTS;
            float Alpha;
            float AlphaClipThreshold;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4 = _Color;
            float _Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[0];
            float _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[1];
            float _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[2];
            float _Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[3];
            float4 _Combine_0924fd2353334adb98d64dcdbad405ee_RGBA_4_Vector4;
            float3 _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3;
            float2 _Combine_0924fd2353334adb98d64dcdbad405ee_RG_6_Vector2;
            Unity_Combine_float(_Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float, _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float, _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float, float(0), _Combine_0924fd2353334adb98d64dcdbad405ee_RGBA_4_Vector4, _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3, _Combine_0924fd2353334adb98d64dcdbad405ee_RG_6_Vector2);
            UnityTexture2D _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float _Property_b807e2969460475dac48546394f43da2_Out_0_Float = _BlurAmount;
            float4 _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4;
            float3 _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3;
            float2 _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3, _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2);
            float4 _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4 = IN.uv0;
            float4 _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4;
            Unity_Add_float4(_Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4);
            float4 _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4.xy)) );
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_R_4_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.r;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_G_5_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.g;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_B_6_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.b;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.a;
            float _Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float = _BlurAmount;
            float _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float;
            Unity_Negate_float(_Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float);
            float4 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4;
            float3 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3;
            float2 _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3, _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2);
            float4 _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4;
            Unity_Add_float4(_Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4);
            float4 _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4.xy)) );
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_R_4_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.r;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_G_5_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.g;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_B_6_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.b;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.a;
            float _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float;
            Unity_Maximum_float(_SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float, _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float, _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float);
            float4 _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4;
            float3 _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3;
            float2 _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3, _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2);
            float4 _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4;
            Unity_Add_float4(_Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4);
            float4 _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4.xy)) );
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_R_4_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.r;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_G_5_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.g;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_B_6_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.b;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.a;
            float _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float;
            Unity_Maximum_float(_Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float, _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float, _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float);
            float4 _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4;
            float3 _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3;
            float2 _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3, _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2);
            float4 _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4;
            Unity_Add_float4(_Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4);
            float4 _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4.xy)) );
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_R_4_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.r;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_G_5_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.g;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_B_6_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.b;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.a;
            float _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float;
            Unity_Maximum_float(_Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float, _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float, _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float);
            float _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float;
            Unity_Multiply_float_float(_Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float, 0.25, _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float);
            float _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float, 10, _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float);
            float _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float;
            Unity_Clamp_float(_Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float, float(0), float(1), _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float);
            float _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float;
            Unity_Saturate_float(_Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float);
            float _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            Unity_Multiply_float_float(_Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float, _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float);
            surface.BaseColor = _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3;
            surface.SpriteMask = IsGammaSpace() ? float4(1, 1, 1, 1) : float4 (SRGBToLinear(float3(1, 1, 1)), 1);
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Alpha = _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            surface.AlphaClipThreshold = float(0.1);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/2D/ShaderGraph/Includes/SpriteLitPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "Sprite Normal"
            Tags
            {
                "LightMode" = "NormalsRendering"
            }
        
        // Render State
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma exclude_renderers d3d11_9x
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_vertex _ SKINNED_SPRITE
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SPRITENORMAL
        #define ALPHA_CLIP_THRESHOLD 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float4 color;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 tangentWS : INTERP0;
             float4 texCoord0 : INTERP1;
             float4 color : INTERP2;
             float3 normalWS : INTERP3;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _MainTex_TexelSize;
        float _BlurAmount;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Negate_float(float In, out float Out)
        {
            Out = -1 * In;
        }
        
        void Unity_Maximum_float(float A, float B, out float Out)
        {
            Out = max(A, B);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Clamp_float(float In, float Min, float Max, out float Out)
        {
            Out = clamp(In, Min, Max);
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float Alpha;
            float AlphaClipThreshold;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4 = _Color;
            float _Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[0];
            float _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[1];
            float _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[2];
            float _Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[3];
            float4 _Combine_0924fd2353334adb98d64dcdbad405ee_RGBA_4_Vector4;
            float3 _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3;
            float2 _Combine_0924fd2353334adb98d64dcdbad405ee_RG_6_Vector2;
            Unity_Combine_float(_Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float, _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float, _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float, float(0), _Combine_0924fd2353334adb98d64dcdbad405ee_RGBA_4_Vector4, _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3, _Combine_0924fd2353334adb98d64dcdbad405ee_RG_6_Vector2);
            UnityTexture2D _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float _Property_b807e2969460475dac48546394f43da2_Out_0_Float = _BlurAmount;
            float4 _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4;
            float3 _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3;
            float2 _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3, _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2);
            float4 _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4 = IN.uv0;
            float4 _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4;
            Unity_Add_float4(_Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4);
            float4 _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4.xy)) );
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_R_4_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.r;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_G_5_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.g;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_B_6_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.b;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.a;
            float _Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float = _BlurAmount;
            float _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float;
            Unity_Negate_float(_Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float);
            float4 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4;
            float3 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3;
            float2 _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3, _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2);
            float4 _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4;
            Unity_Add_float4(_Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4);
            float4 _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4.xy)) );
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_R_4_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.r;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_G_5_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.g;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_B_6_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.b;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.a;
            float _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float;
            Unity_Maximum_float(_SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float, _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float, _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float);
            float4 _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4;
            float3 _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3;
            float2 _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3, _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2);
            float4 _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4;
            Unity_Add_float4(_Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4);
            float4 _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4.xy)) );
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_R_4_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.r;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_G_5_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.g;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_B_6_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.b;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.a;
            float _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float;
            Unity_Maximum_float(_Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float, _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float, _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float);
            float4 _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4;
            float3 _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3;
            float2 _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3, _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2);
            float4 _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4;
            Unity_Add_float4(_Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4);
            float4 _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4.xy)) );
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_R_4_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.r;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_G_5_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.g;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_B_6_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.b;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.a;
            float _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float;
            Unity_Maximum_float(_Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float, _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float, _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float);
            float _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float;
            Unity_Multiply_float_float(_Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float, 0.25, _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float);
            float _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float, 10, _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float);
            float _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float;
            Unity_Clamp_float(_Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float, float(0), float(1), _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float);
            float _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float;
            Unity_Saturate_float(_Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float);
            float _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            Unity_Multiply_float_float(_Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float, _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float);
            surface.BaseColor = _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3;
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Alpha = _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            surface.AlphaClipThreshold = float(0.1);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/2D/ShaderGraph/Includes/SpriteNormalPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma exclude_renderers d3d11_9x
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma shader_feature_local_fragment _ _ALPHATEST_ON
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENESELECTIONPASS 1
        
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _MainTex_TexelSize;
        float _BlurAmount;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Negate_float(float In, out float Out)
        {
            Out = -1 * In;
        }
        
        void Unity_Maximum_float(float A, float B, out float Out)
        {
            Out = max(A, B);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Clamp_float(float In, float Min, float Max, out float Out)
        {
            Out = clamp(In, Min, Max);
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float Alpha;
            float AlphaClipThreshold;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4 = _Color;
            float _Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[0];
            float _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[1];
            float _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[2];
            float _Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[3];
            UnityTexture2D _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float _Property_b807e2969460475dac48546394f43da2_Out_0_Float = _BlurAmount;
            float4 _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4;
            float3 _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3;
            float2 _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3, _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2);
            float4 _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4 = IN.uv0;
            float4 _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4;
            Unity_Add_float4(_Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4);
            float4 _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4.xy)) );
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_R_4_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.r;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_G_5_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.g;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_B_6_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.b;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.a;
            float _Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float = _BlurAmount;
            float _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float;
            Unity_Negate_float(_Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float);
            float4 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4;
            float3 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3;
            float2 _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3, _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2);
            float4 _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4;
            Unity_Add_float4(_Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4);
            float4 _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4.xy)) );
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_R_4_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.r;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_G_5_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.g;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_B_6_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.b;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.a;
            float _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float;
            Unity_Maximum_float(_SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float, _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float, _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float);
            float4 _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4;
            float3 _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3;
            float2 _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3, _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2);
            float4 _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4;
            Unity_Add_float4(_Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4);
            float4 _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4.xy)) );
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_R_4_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.r;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_G_5_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.g;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_B_6_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.b;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.a;
            float _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float;
            Unity_Maximum_float(_Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float, _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float, _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float);
            float4 _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4;
            float3 _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3;
            float2 _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3, _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2);
            float4 _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4;
            Unity_Add_float4(_Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4);
            float4 _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4.xy)) );
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_R_4_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.r;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_G_5_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.g;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_B_6_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.b;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.a;
            float _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float;
            Unity_Maximum_float(_Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float, _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float, _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float);
            float _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float;
            Unity_Multiply_float_float(_Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float, 0.25, _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float);
            float _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float, 10, _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float);
            float _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float;
            Unity_Clamp_float(_Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float, float(0), float(1), _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float);
            float _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float;
            Unity_Saturate_float(_Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float);
            float _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            Unity_Multiply_float_float(_Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float, _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float);
            surface.Alpha = _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            surface.AlphaClipThreshold = float(0.1);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }
        
        // Render State
        Cull [_Cull]
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma exclude_renderers d3d11_9x
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma shader_feature_local_fragment _ _ALPHATEST_ON
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENEPICKINGPASS 1
        
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _MainTex_TexelSize;
        float _BlurAmount;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Negate_float(float In, out float Out)
        {
            Out = -1 * In;
        }
        
        void Unity_Maximum_float(float A, float B, out float Out)
        {
            Out = max(A, B);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Clamp_float(float In, float Min, float Max, out float Out)
        {
            Out = clamp(In, Min, Max);
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float Alpha;
            float AlphaClipThreshold;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4 = _Color;
            float _Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[0];
            float _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[1];
            float _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[2];
            float _Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[3];
            UnityTexture2D _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float _Property_b807e2969460475dac48546394f43da2_Out_0_Float = _BlurAmount;
            float4 _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4;
            float3 _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3;
            float2 _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3, _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2);
            float4 _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4 = IN.uv0;
            float4 _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4;
            Unity_Add_float4(_Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4);
            float4 _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4.xy)) );
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_R_4_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.r;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_G_5_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.g;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_B_6_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.b;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.a;
            float _Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float = _BlurAmount;
            float _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float;
            Unity_Negate_float(_Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float);
            float4 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4;
            float3 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3;
            float2 _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3, _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2);
            float4 _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4;
            Unity_Add_float4(_Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4);
            float4 _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4.xy)) );
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_R_4_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.r;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_G_5_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.g;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_B_6_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.b;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.a;
            float _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float;
            Unity_Maximum_float(_SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float, _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float, _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float);
            float4 _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4;
            float3 _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3;
            float2 _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3, _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2);
            float4 _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4;
            Unity_Add_float4(_Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4);
            float4 _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4.xy)) );
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_R_4_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.r;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_G_5_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.g;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_B_6_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.b;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.a;
            float _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float;
            Unity_Maximum_float(_Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float, _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float, _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float);
            float4 _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4;
            float3 _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3;
            float2 _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3, _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2);
            float4 _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4;
            Unity_Add_float4(_Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4);
            float4 _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4.xy)) );
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_R_4_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.r;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_G_5_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.g;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_B_6_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.b;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.a;
            float _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float;
            Unity_Maximum_float(_Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float, _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float, _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float);
            float _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float;
            Unity_Multiply_float_float(_Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float, 0.25, _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float);
            float _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float, 10, _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float);
            float _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float;
            Unity_Clamp_float(_Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float, float(0), float(1), _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float);
            float _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float;
            Unity_Saturate_float(_Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float);
            float _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            Unity_Multiply_float_float(_Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float, _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float);
            surface.Alpha = _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            surface.AlphaClipThreshold = float(0.1);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "Sprite Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
        
        // Render State
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZTest Less
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        ZWrite Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma exclude_renderers d3d11_9x
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        #pragma multi_compile_vertex _ SKINNED_SPRITE
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
        #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SPRITEFORWARD
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 color : COLOR;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float4 texCoord0;
             float4 color;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float4 color : INTERP1;
             float3 positionWS : INTERP2;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.color.xyzw = input.color;
            output.positionWS.xyz = input.positionWS;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.color = input.color.xyzw;
            output.positionWS = input.positionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _MainTex_TexelSize;
        float _BlurAmount;
        UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Negate_float(float In, out float Out)
        {
            Out = -1 * In;
        }
        
        void Unity_Maximum_float(float A, float B, out float Out)
        {
            Out = max(A, B);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Clamp_float(float In, float Min, float Max, out float Out)
        {
            Out = clamp(In, Min, Max);
        }
        
        void Unity_Saturate_float(float In, out float Out)
        {
            Out = saturate(In);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float Alpha;
            float AlphaClipThreshold;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4 = _Color;
            float _Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[0];
            float _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[1];
            float _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[2];
            float _Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float = _Property_d6cf2efdca5440579a29420c732f904e_Out_0_Vector4[3];
            float4 _Combine_0924fd2353334adb98d64dcdbad405ee_RGBA_4_Vector4;
            float3 _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3;
            float2 _Combine_0924fd2353334adb98d64dcdbad405ee_RG_6_Vector2;
            Unity_Combine_float(_Split_8968ebce846945fdb577eb7c74a4b488_R_1_Float, _Split_8968ebce846945fdb577eb7c74a4b488_G_2_Float, _Split_8968ebce846945fdb577eb7c74a4b488_B_3_Float, float(0), _Combine_0924fd2353334adb98d64dcdbad405ee_RGBA_4_Vector4, _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3, _Combine_0924fd2353334adb98d64dcdbad405ee_RG_6_Vector2);
            UnityTexture2D _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float _Property_b807e2969460475dac48546394f43da2_Out_0_Float = _BlurAmount;
            float4 _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4;
            float3 _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3;
            float2 _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _Combine_e23c77b2353341ada95b9df2b87f2673_RGB_5_Vector3, _Combine_e23c77b2353341ada95b9df2b87f2673_RG_6_Vector2);
            float4 _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4 = IN.uv0;
            float4 _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4;
            Unity_Add_float4(_Combine_e23c77b2353341ada95b9df2b87f2673_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4);
            float4 _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_2b71a6d2a3ce424284399ad231e501ca_Out_2_Vector4.xy)) );
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_R_4_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.r;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_G_5_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.g;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_B_6_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.b;
            float _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float = _SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_RGBA_0_Vector4.a;
            float _Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float = _BlurAmount;
            float _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float;
            Unity_Negate_float(_Property_1134e1f190af4447b8cd3b7881e5eb68_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float);
            float4 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4;
            float3 _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3;
            float2 _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Property_b807e2969460475dac48546394f43da2_Out_0_Float, float(0), float(0), _Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _Combine_002ee209f5e849b0957b9e83c9ca5878_RGB_5_Vector3, _Combine_002ee209f5e849b0957b9e83c9ca5878_RG_6_Vector2);
            float4 _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4;
            Unity_Add_float4(_Combine_002ee209f5e849b0957b9e83c9ca5878_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4);
            float4 _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_d4ad2e6468bb4d0c9ba14d5545f8d7b5_Out_2_Vector4.xy)) );
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_R_4_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.r;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_G_5_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.g;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_B_6_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.b;
            float _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float = _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_RGBA_0_Vector4.a;
            float _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float;
            Unity_Maximum_float(_SampleTexture2D_f592c6daa9274e828c89e80f1b22fe7e_A_7_Float, _SampleTexture2D_9b2dd029dfd44fbda7b845ca81d4aa94_A_7_Float, _Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float);
            float4 _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4;
            float3 _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3;
            float2 _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2;
            Unity_Combine_float(_Property_b807e2969460475dac48546394f43da2_Out_0_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _Combine_00aec7a1203b4f11ae97109514e462e5_RGB_5_Vector3, _Combine_00aec7a1203b4f11ae97109514e462e5_RG_6_Vector2);
            float4 _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4;
            Unity_Add_float4(_Combine_00aec7a1203b4f11ae97109514e462e5_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4);
            float4 _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_f59449d25ca040e1ac1ec567d93a9710_Out_2_Vector4.xy)) );
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_R_4_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.r;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_G_5_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.g;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_B_6_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.b;
            float _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float = _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_RGBA_0_Vector4.a;
            float _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float;
            Unity_Maximum_float(_Maximum_fc906f0ca50f43bc9fdebf563728b71a_Out_2_Float, _SampleTexture2D_19c942fd0f964c50a3f7f4a1d317809a_A_7_Float, _Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float);
            float4 _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4;
            float3 _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3;
            float2 _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2;
            Unity_Combine_float(_Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, _Negate_cf09145985b24e66ac28dd734f3630a5_Out_1_Float, float(0), float(0), _Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _Combine_c5865929a233455d9a3680e105d01ba3_RGB_5_Vector3, _Combine_c5865929a233455d9a3680e105d01ba3_RG_6_Vector2);
            float4 _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4;
            Unity_Add_float4(_Combine_c5865929a233455d9a3680e105d01ba3_RGBA_4_Vector4, _UV_b8527b55fa15470b97b4db7593c63466_Out_0_Vector4, _Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4);
            float4 _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.tex, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.samplerstate, _Property_6fdaa555c4e24071a9766b9727b9588c_Out_0_Texture2D.GetTransformedUV((_Add_5aaeefa9a853462ab287452f7fb7b096_Out_2_Vector4.xy)) );
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_R_4_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.r;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_G_5_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.g;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_B_6_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.b;
            float _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float = _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_RGBA_0_Vector4.a;
            float _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float;
            Unity_Maximum_float(_Maximum_511bb7c5ba6f4b23b2e932249413a1f0_Out_2_Float, _SampleTexture2D_72c392681d0d4914b4ba04e4d96bc0ca_A_7_Float, _Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float);
            float _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float;
            Unity_Multiply_float_float(_Maximum_4d29307dc31b4102a9bc6c4cce64a165_Out_2_Float, 0.25, _Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float);
            float _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_061c1fe94dbc4bf28e714b41d4c9a7e7_Out_2_Float, 10, _Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float);
            float _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float;
            Unity_Clamp_float(_Multiply_2d0f70f7f59d49e187a5f0704491bc98_Out_2_Float, float(0), float(1), _Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float);
            float _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float;
            Unity_Saturate_float(_Clamp_6395c0e562ba4683ab540adbb59a7c00_Out_3_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float);
            float _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            Unity_Multiply_float_float(_Split_8968ebce846945fdb577eb7c74a4b488_A_4_Float, _Saturate_847a8fc7263c4a839b8e9d12f71d6f17_Out_1_Float, _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float);
            surface.BaseColor = _Combine_0924fd2353334adb98d64dcdbad405ee_RGB_5_Vector3;
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Alpha = _Multiply_aa94c3cd6de64d7c9d7b71847f8bfb61_Out_2_Float;
            surface.AlphaClipThreshold = float(0.1);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
            output.uv0 = input.texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
        #else // TODO: XR support for procedural instancing because in this case UNITY_ANY_INSTANCING_ENABLED is not defined and instanceID is incorrect.
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/2D/ShaderGraph/Includes/SpriteForwardPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    CustomEditorForRenderPipeline "UnityEditor.ShaderGraphSpriteGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
    FallBack "Hidden/Shader Graph/FallbackError"
}