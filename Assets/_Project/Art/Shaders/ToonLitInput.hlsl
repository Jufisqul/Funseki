#ifndef FUNSEKI_TOON_LIT_INPUT_INCLUDED
#define FUNSEKI_TOON_LIT_INPUT_INCLUDED

// Shared by every pass of Funseki/ToonLit. URP's ShadowCaster / DepthOnly / DepthNormals passes
// read _BaseMap, _BaseColor and _Cutoff from here, so the same material casts the same alpha-clipped shape.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _ShadowColor;
    half4 _SkinShadowColor;
    half4 _RimColor;
    half _RimThreshold;
    half _RimStrength;
    half _RimLitOnly;
    half _ShadowReceive;
    half _AmbientStrength;
    half _AdditionalLightsStrength;
    half _Cutoff;
CBUFFER_END

TEXTURE2D(_RampMap);
SAMPLER(sampler_RampMap);

#endif
