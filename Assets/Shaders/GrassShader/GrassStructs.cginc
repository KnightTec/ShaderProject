#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

#if defined (SHADOWS_DEPTH) && !defined (SPOT)
#       define SHADOW_COORDS(idx1) unityShadowCoord2 _ShadowCoord : TEXCOORD##idx1;
#endif

struct appdata {
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float2 uv       : TEXCOORD0;
};

struct v2g {
    float4 vertex   : SV_POSITION;
    float3 normal   : NORMAL;
    float2 uv       : TEXCOORD0;
};

struct g2f {
    float4 pos          : SV_POSITION;
    float4 vertexWorld  : TEXCOORD0;
    SHADOW_COORDS(1)
    float3 normal       : NORMAL;
    float3 tangent      : TEXCOORD2;
    float2 uv           : TEXCOORD3;
    float2 blendFactors : TEXCOORD4;
};

struct tessFactors {
    float edge[3]   : SV_Tessfactor;
    float inside    : SV_InsideTessFactor;
};