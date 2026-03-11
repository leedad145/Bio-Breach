// =============================================================================
// VoxelTriplanar.shader
// URP Triplanar shader for Marching Cubes voxel terrain.
// UV-less: uses world-space position for texture projection on all 3 axes.
// Normal mapping via Ben Golus Whiteout triplanar blend (world-space output).
// Maps: Albedo, Normal (n), Roughness (r), Height (ht), Emission (em),
//       Clear Coat (c), Ambient Occlusion (ao)
// =============================================================================

Shader "BioBreach/VoxelTriplanar"
{
    Properties
    {
        _BaseColor           ("Base Color",            Color)         = (1,1,1,1)
        _BaseMap             ("Base Map",              2D)            = "white" {}

        [Normal]
        _BumpMap             ("Normal Map (n)",        2D)            = "bump"  {}
        _BumpScale           ("Normal Scale",          Range(0,4))    = 4.0

        _RoughnessMap        ("Roughness Map (r)",     2D)            = "black" {}
        // R 채널 = roughness (0=smooth, 1=rough). black 기본 → _Smoothness 그대로 사용.
        _Metallic            ("Metallic",              Range(0,1))    = 0.0
        _Smoothness          ("Smoothness (max)",      Range(0,1))    = 0.5

        _HeightMap           ("Height Map (ht)",       2D)            = "black" {}
        // R 채널로 블렌드 경계 샤프닝. black → 기존 normal-power 블렌드와 동일.
        _HeightScale         ("Height Blend Scale",    Range(0,1))    = 0.3

        _EmissionMap         ("Emission Map (em)",     2D)            = "black" {}
        _EmissionColor       ("Emission Color",        Color)         = (0,0,0,0)
        _EmissionIntensity   ("Emission Intensity",    Range(0,8))    = 1.0

        _ClearCoatMap        ("Clear Coat Map (c)",    2D)            = "black" {}
        // R 채널 = 클리어코트 마스크. GGX 하이라이트 레이어 추가.
        _ClearCoatStrength   ("Clear Coat Strength",   Range(0,1))    = 0.0
        _ClearCoatRoughness  ("Clear Coat Roughness",  Range(0,1))    = 0.1

        _OcclusionMap        ("Occlusion Map (ao)",    2D)            = "white" {}
        // R 채널 = AO (1=밝음, 0=가려짐). white 기본 → AO 없음.
        _OcclusionStrength   ("AO Strength",           Range(0,1))    = 1.0

        _Tiling              ("Tiling",                Float)         = 0.1
        _BlendSharpness      ("Blend Sharpness",       Range(1,16))   = 4.0
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

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughnessMap);   SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_HeightMap);      SAMPLER(sampler_HeightMap);
            TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_ClearCoatMap);   SAMPLER(sampler_ClearCoatMap);
            TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _EmissionColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _HeightScale;
                float  _EmissionIntensity;
                float  _ClearCoatStrength;
                float  _ClearCoatRoughness;
                float  _OcclusionStrength;
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
            // Blend weights: normal-power + height-based sharpening
            // Height map R채널을 w에 더해 면 경계를 자연스럽게 샤프닝
            // -----------------------------------------------------------------
            float3 ComputeWeights(float3 worldNormal, float3 worldPos)
            {
                float3 w = pow(abs(worldNormal), _BlendSharpness);

                float hx = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, worldPos.yz * _Tiling).r;
                float hy = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, worldPos.xz * _Tiling).r;
                float hz = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, worldPos.xy * _Tiling).r;
                w += float3(hx, hy, hz) * _HeightScale;

                w /= dot(w, float3(1, 1, 1));
                return w;
            }

            // -----------------------------------------------------------------
            // Albedo
            // -----------------------------------------------------------------
            half4 SampleAlbedo(float3 worldPos, float3 w)
            {
                half4 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPos.yz * _Tiling);
                half4 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPos.xz * _Tiling);
                half4 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldPos.xy * _Tiling);
                return cx * w.x + cy * w.y + cz * w.z;
            }

            // -----------------------------------------------------------------
            // Normal (n) — Ben Golus Whiteout blend → world space
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
            // Roughness (r) — R채널 = roughness, smoothness = _Smoothness * (1-r)
            // black 기본값이면 roughness=0 → smoothness = _Smoothness 그대로 유지
            // -----------------------------------------------------------------
            float SampleSmoothness(float3 worldPos, float3 w)
            {
                float rx = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, worldPos.yz * _Tiling).r;
                float ry = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, worldPos.xz * _Tiling).r;
                float rz = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, worldPos.xy * _Tiling).r;
                float roughness = rx * w.x + ry * w.y + rz * w.z;
                return _Smoothness * (1.0 - roughness);
            }

            // -----------------------------------------------------------------
            // Emission (em)
            // -----------------------------------------------------------------
            half3 SampleEmission(float3 worldPos, float3 w)
            {
                half3 ex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, worldPos.yz * _Tiling).rgb;
                half3 ey = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, worldPos.xz * _Tiling).rgb;
                half3 ez = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, worldPos.xy * _Tiling).rgb;
                return (ex * w.x + ey * w.y + ez * w.z) * _EmissionColor.rgb * _EmissionIntensity;
            }

            // -----------------------------------------------------------------
            // Ambient Occlusion (ao) — R채널, _OcclusionStrength로 세기 조절
            // white 기본값 → occlusion=1 (영향 없음)
            // -----------------------------------------------------------------
            float SampleOcclusion(float3 worldPos, float3 w)
            {
                float ox = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, worldPos.yz * _Tiling).r;
                float oy = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, worldPos.xz * _Tiling).r;
                float oz = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, worldPos.xy * _Tiling).r;
                float ao = ox * w.x + oy * w.y + oz * w.z;
                return lerp(1.0, ao, _OcclusionStrength);
            }

            // -----------------------------------------------------------------
            // Clear Coat (c) — R채널 마스크 × GGX 하이라이트 (IOR≈1.5, F0=0.04)
            // black 기본값 + ClearCoatStrength=0 → 비활성
            // -----------------------------------------------------------------
            float SampleClearCoatMask(float3 worldPos, float3 w)
            {
                float cx = SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, worldPos.yz * _Tiling).r;
                float cy = SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, worldPos.xz * _Tiling).r;
                float cz = SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, worldPos.xy * _Tiling).r;
                return cx * w.x + cy * w.y + cz * w.z;
            }

            half3 ComputeClearCoat(float3 normalWS, float3 viewDirWS, float4 shadowCoord, float ccMask)
            {
                if (ccMask < 0.001 || _ClearCoatStrength < 0.001)
                    return (half3)0;

                Light mainLight = GetMainLight(shadowCoord);
                float3 H   = SafeNormalize(mainLight.direction + viewDirWS);
                float  NoH = saturate(dot(normalWS, H));
                float  VoH = saturate(dot(viewDirWS, H));

                float a  = max(_ClearCoatRoughness * _ClearCoatRoughness, 0.002);
                float a2 = a * a;
                float d  = (NoH * a2 - NoH) * NoH + 1.0;
                float D  = a2 / (PI * d * d + 1e-7);

                // Schlick Fresnel (F0=0.04)
                float F = 0.04 + 0.96 * pow(1.0 - VoH, 5.0);

                return (half3)(ccMask * _ClearCoatStrength * F * D
                    * mainLight.color * mainLight.distanceAttenuation
                    * mainLight.shadowAttenuation);
            }

            // -----------------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 worldNormal = normalize(IN.normalWS);
                float3 worldPos    = IN.positionWS;

                // Height-adjusted blend weights
                float3 w = ComputeWeights(worldNormal, worldPos);

                half4  albedo     = SampleAlbedo(worldPos, w) * _BaseColor;
                float3 normalWS   = SampleNormalWS(worldPos, worldNormal, w);
                float  smoothness = SampleSmoothness(worldPos, w);
                float  occlusion  = SampleOcclusion(worldPos, w);
                half3  emission   = SampleEmission(worldPos, w);

                SurfaceData sd;
                ZERO_INITIALIZE(SurfaceData, sd);
                sd.albedo     = albedo.rgb;
                sd.alpha      = 1.0;
                sd.metallic   = _Metallic;
                sd.smoothness = smoothness;
                sd.normalTS   = float3(0, 0, 1); // world-space normal used directly
                sd.occlusion  = occlusion;
                sd.emission   = emission;

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

                // Clear Coat 레이어 — 기본 PBR 위에 추가 하이라이트
                float ccMask = SampleClearCoatMask(worldPos, w);
                color.rgb += ComputeClearCoat(normalWS, id.viewDirectionWS, id.shadowCoord, ccMask);

                color.rgb = MixFog(color.rgb, id.fogCoord);
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
                float  _HeightScale;
                float  _EmissionIntensity;
                float  _ClearCoatStrength;
                float  _ClearCoatRoughness;
                float  _OcclusionStrength;
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
                float  _HeightScale;
                float  _EmissionIntensity;
                float  _ClearCoatStrength;
                float  _ClearCoatRoughness;
                float  _OcclusionStrength;
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
