// Cel shading for characters (URP): two or three hard tones from a ramp texture, a tinted shadow colour instead of
// black, the main directional light with its shadows, additional lights through the same ramp, ambient from the
// light probes and a rim light on the silhouette. No PBR, no reflections.
// The outline is a separate material (Funseki/ToonOutline) on a second renderer, see RyutaModelSetup.
// Base map alpha: with Alpha Clip on it cuts the shape (face cards); with it off, alpha 0 marks skin, which gets
// Skin Shadow Color instead of Shadow Color (one atlas for clothes and skin).
Shader "Funseki/ToonLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _RampMap ("Ramp (x = light, 0 shadow .. 1 lit)", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.62, 0.6, 0.8, 1)
        _SkinShadowColor ("Skin Shadow Color (base alpha 0)", Color) = (0.93, 0.74, 0.74, 1)
        _ShadowReceive ("Received Shadows", Range(0, 1)) = 1
        _AmbientStrength ("Ambient", Range(0, 1)) = 0.3
        _AdditionalLightsStrength ("Additional Lights", Range(0, 2)) = 1
        _RimColor ("Rim Color", Color) = (0.75, 0.72, 1, 1)
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.7
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.15
        _RimLitOnly ("Rim Only On The Lit Side", Range(0, 1)) = 0.7
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Unlit" }

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 sh : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half ToonRamp(half x)
            {
                return SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(saturate(x), 0.5)).r;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.sh = SampleSH(o.normalWS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(tex.a - _Cutoff);
                half3 shadowTint = _ShadowColor.rgb;
            #else
                half3 shadowTint = lerp(_SkinShadowColor.rgb, _ShadowColor.rgb, tex.a);
            #endif
                half3 albedo = tex.rgb;
                half3 n = normalize(i.normalWS);

            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
            #else
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
            #endif
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowReceive);
                half ramp = ToonRamp((dot(n, mainLight.direction) * 0.5h + 0.5h) * shadow);
                half3 lightColor = mainLight.color * mainLight.distanceAttenuation;
                half3 color = albedo * lerp(shadowTint, 1.0h, ramp) * lightColor;
                color += albedo * i.sh * _AmbientStrength;

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);
                uint lightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    Light l = GetAdditionalLight(dirIndex, i.positionWS, shadowMask);
                    half r = ToonRamp((dot(n, l.direction) * 0.5h + 0.5h) * lerp(1.0h, l.shadowAttenuation, _ShadowReceive));
                    color += albedo * l.color * (l.distanceAttenuation * r * _AdditionalLightsStrength);
                }
            #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    half r = ToonRamp((dot(n, l.direction) * 0.5h + 0.5h) * lerp(1.0h, l.shadowAttenuation, _ShadowReceive));
                    color += albedo * l.color * (l.distanceAttenuation * r * _AdditionalLightsStrength);
                LIGHT_LOOP_END
            #endif

                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = smoothstep(_RimThreshold, _RimThreshold + 0.04h, 1.0h - saturate(dot(n, v)));
                color += _RimColor.rgb * (rim * _RimStrength * lerp(1.0h, ramp, _RimLitOnly));

                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
