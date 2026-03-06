// =============================================================================
// VoxelTriplanar.shader
// URP Triplanar shader for Marching Cubes voxel terrain.
// UV-less: uses world-space position for texture projection on all 3 axes.
// Normal mapping via Ben Golus Whiteout triplanar blend (world-space output).
// =============================================================================

Shader "BioBreach/VoxelTriplanar"
{
    Properties
    {
        _BaseColor      ("Base Color",              Color)          = (1,1,1,1)
        _BaseMap        ("Base Map",                2D)             = "white" {}
        _BumpMap        ("Normal Map",              2D)             = "bump"  {}
        _BumpScale      ("Normal Scale",            Range(0,4))     = 4.0
        _Metallic       ("Metallic",                Range(0,1))     = 0.0
        _Smoothness     ("Smoothness",              Range(0,1))     = 0.5
        _EmissionColor  ("Emission Color",          Color)          = (0,0,0,0)
        _Tiling         ("Tiling",                  Float)          = 0.1
        _BlendSharpness ("Blend Sharpness",         Range(1,16))    = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 300

        // =====================================================================
        // Forward Lit Pass
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _EmissionColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _Tiling;
                float  _BlendSharpness;
            CBUFFER_END

            // -----------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS           : SV_POSITION;
                float3 positionWS           : TEXCOORD0;
                float3 normalWS             : TEXCOORD1;
                half4  fogFactorAndVtxLight : TEXCOORD2; // x=fog, yzw=vertexLight
                float4 shadowCoord          : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // -----------------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs    = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normalInputs.normalWS;

                half  fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                half3 vertexLight = VertexLighting(posInputs.positionWS, normalInputs.normalWS);
                OUT.fogFactorAndVtxLight = half4(fogFactor, vertexLight);

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                OUT.shadowCoord = GetShadowCoord(posInputs);
            #endif

                return OUT;
            }

            // -----------------------------------------------------------------
            // Triplanar albedo: sample BaseMap on YZ, XZ, XY planes and blend
            // -----------------------------------------------------------------
            half4 SampleAlbedo(float3 worldPos, float3 w)
            {
                half4 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPos.yz * _Tiling);
                half4 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPos.xz * _Tiling);
                half4 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPos.xy * _Tiling);
                return cx * w.x + cy * w.y + cz * w.z;
            }

            // -----------------------------------------------------------------
            // Triplanar normal — Ben Golus Whiteout blend → world space output
            // No mesh tangents required.
            // -----------------------------------------------------------------
            float3 SampleNormalWS(float3 worldPos, float3 worldNormal, float3 w)
            {
                float3 tnX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, worldPos.zy * _Tiling), _BumpScale);
                float3 tnY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, worldPos.xz * _Tiling), _BumpScale);
                float3 tnZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, worldPos.xy * _Tiling), _BumpScale);

                // Whiteout blend: 부호 보존 — abs() 사용 시 -X/-Y/-Z 면 노멀이 뒤집혀 조명 불량
                tnX = float3(tnX.xy + worldNormal.zy, worldNormal.x);
                tnY = float3(tnY.xy + worldNormal.xz, worldNormal.y);
                tnZ = float3(tnZ.xy + worldNormal.xy, worldNormal.z);

                return normalize(tnX.zyx * w.x + tnY.xzy * w.y + tnZ.xyz * w.z);
            }

            // -----------------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 worldNormal = normalize(IN.normalWS);
                float3 worldPos    = IN.positionWS;

                // Blend weights: steeper angle = stronger weight
                float3 w = pow(abs(worldNormal), _BlendSharpness);
                w /= dot(w, float3(1, 1, 1));

                half4  albedo   = SampleAlbedo(worldPos, w) * _BaseColor;
                float3 normalWS = SampleNormalWS(worldPos, worldNormal, w);

                // SurfaceData
                SurfaceData sd;
                ZERO_INITIALIZE(SurfaceData, sd);
                sd.albedo     = albedo.rgb;
                sd.alpha      = 1.0;
                sd.metallic   = _Metallic;
                sd.smoothness = _Smoothness;
                sd.normalTS   = float3(0, 0, 1); // not used (world-space normal used directly)
                sd.occlusion  = 1.0;
                sd.emission   = _EmissionColor.rgb;

                // InputData
                InputData id;
                ZERO_INITIALIZE(InputData, id);
                id.positionWS      = worldPos;
                id.normalWS        = normalWS;
                id.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(worldPos));

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                id.shadowCoord = IN.shadowCoord;
            #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                id.shadowCoord = TransformWorldToShadowCoord(worldPos);
            #else
                id.shadowCoord = float4(0, 0, 0, 0);
            #endif

                id.fogCoord                = IN.fogFactorAndVtxLight.x;
                id.vertexLighting          = IN.fogFactorAndVtxLight.yzw;
                id.bakedGI                 = SampleSH(normalWS);
                id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                id.shadowMask              = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(id, sd);
                color.rgb   = MixFog(color.rgb, id.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // =====================================================================
        // Shadow Caster Pass
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _EmissionColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _Tiling;
                float  _BlendSharpness;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttribs
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(ShadowAttribs IN)
            {
                ShadowVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif

                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, lightDir));

            #if UNITY_REVERSED_Z
                OUT.positionCS.z = min(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                OUT.positionCS.z = max(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif

                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // =====================================================================
        // Depth Only Pass
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _EmissionColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _Tiling;
                float  _BlendSharpness;
            CBUFFER_END

            struct DepthAttribs
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVert(DepthAttribs IN)
            {
                DepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag(DepthVaryings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
