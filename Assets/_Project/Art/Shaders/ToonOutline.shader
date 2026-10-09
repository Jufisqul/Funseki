// Inverted-hull outline for characters (URP): the mesh is pushed out along its normals and only its back faces
// are drawn. Vertex colour R scales the width per vertex (0 = no line: face cards, places hidden inside clothes).
// Put it on a second renderer that shares the character's mesh and bones, see RyutaModelSetup.
Shader "Funseki/ToonOutline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (0.06, 0.04, 0.07, 1)
        _OutlineWidth ("Width (m, at 1 m from the camera)", Range(0, 0.02)) = 0.0028
        _DistanceScale ("Keep Width On Screen (0 = world size, 1 = screen size)", Range(0, 1)) = 0.6
        _MaxDistanceScale ("Max Distance Scale", Range(1, 30)) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _DistanceScale;
                float _MaxDistanceScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half width : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float distanceScale = unity_OrthoParams.w > 0.5
                    ? 1.0
                    : clamp(distance(GetCameraPositionWS(), positionWS), 1.0, _MaxDistanceScale);
                float width = _OutlineWidth * input.color.r * lerp(1.0, distanceScale, _DistanceScale);
                o.positionCS = TransformWorldToHClip(positionWS + normalWS * width);
                o.width = input.color.r;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(i.width - 0.001h);
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1.0h);
            }
            ENDHLSL
        }
    }
}
