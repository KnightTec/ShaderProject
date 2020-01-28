#include "GrassStructs.cginc"
#include "GrassVars.cginc"
#include "Tessellation.cginc"

v2g vert (appdata IN) {
    v2g OUT;
    OUT.vertex  = IN.vertex;
    OUT.normal  = IN.normal;
    OUT.uv      = IN.uv;
    return OUT;
}

#define AVG(fieldname) (IN[0].fieldname + IN[1].fieldname + IN[2].fieldname) / 3

tessFactors patch(InputPatch<v2g, 3> IN) {
    tessFactors t;

    float fac;
    if ( UnityWorldViewFrustumCull 
        (   mul (unity_ObjectToWorld, IN[0].vertex ),
            mul (unity_ObjectToWorld, IN[1].vertex ),
            mul (unity_ObjectToWorld, IN[2].vertex ), 
            0 ) ) 
    {
        fac = 0;
    }
    else {   
        float h = (
            tex2Dlod ( _HeightTex, float4(IN[0].uv, 0, 0) ).r +
            tex2Dlod ( _HeightTex, float4(IN[1].uv, 0, 0)).r +
            tex2Dlod ( _HeightTex, float4(IN[2].uv, 0, 0) ).r ) / 3.;
            
        if ( h <= _MinGrassHeight ) {
            fac = 0;
        }
        else {
            float dist = distance (mul(unity_ObjectToWorld, AVG (vertex) ), _WorldSpaceCameraPos);
            float x = lerp (0, 1, min( _MaxDistance, dist) / _MaxDistance);
            
            fac = lerp(_TessFactor, 1, x );
        }
    }

    t.edge[0] = fac;
    t.edge[1] = fac;
    t.edge[2] = fac;
    t.inside = fac;

    return t;
}

[UNITY_domain("tri")]
[UNITY_outputcontrolpoints(3)]
[UNITY_outputtopology("triangle_cw")]
[UNITY_partitioning("integer")]
[UNITY_patchconstantfunc("patch")]
v2g hull (InputPatch<v2g, 3> patch, uint id : SV_OutputControlPointID) {
    return patch[id];
}

#define INTERPOLATE(fieldname) data.fieldname = op[0].fieldname * dl.x + op[1].fieldname * dl.y + op[2].fieldname * dl.z

[UNITY_domain("tri")]
v2g dom (tessFactors tf, OutputPatch<appdata, 3> op, float3 dl : SV_DomainLocation) {
    appdata data;
    INTERPOLATE(vertex);
    INTERPOLATE(normal);
    INTERPOLATE(uv);

    v2g o;

    o.vertex = data.vertex;
    o.normal = UnityObjectToWorldNormal(data.normal);
    o.uv = data.uv;

    return o;
}

#define RANDOM_ABS(fieldname) abs( sin ( 23.512 * dot ( 13.513323 * fieldname.yzx, fixed4 (4.3754, 5.299, 6.2851, 3.4103) * 93.03) ) )
#define RANDOM(fieldname) sin ( 23.512 * dot ( 123.51323 * fieldname.yzx, fixed4 (423.754, 52.299, 63.22851, 3.24103) * 92.03) ) 

#define APPEND(index)   \
        v = IN[index]; \
        o.worldPos =        mul ( unity_ObjectToWorld, IN[index].vertex ); \
        o.pos =             mul(UNITY_MATRIX_VP, o.worldPos); \
        o.tangent =         float3(0,0,0); \
        o.normal =          IN[index].normal; \
        o.uv =              IN[index].uv; \
        o.blendFactors =    float2( tex2Dlod(_HeightTex, float4(IN[index].uv,0,0)).r, 0); \
        TRANSFER_SHADOW(o); \
        triStream.Append(o);

#define APPEND_ADDITIVE(summand,uvx,uvy) \
        if (sizefac > 0 ) { \
            o.worldPos =    mul ( unity_ObjectToWorld, avg + (summand) ); \
            o.pos =         UnityObjectToClipPos ( avg + (summand) ); \
            o.blendFactors = float2(uvx, uvy); \
            v.vertex =      o.worldPos; \
            TRANSFER_SHADOW(o); \
            triStream.Append(o); \
        }

#define APPEND_ADDITIVE_BOTTOM(summand,uvx,uvy) \
        if (sizefac > 0 ) { \
            o.worldPos =        mul ( unity_ObjectToWorld, avg + (summand) ); \
            o.pos =             UnityObjectToClipPos (avg + (summand)); \
            o.blendFactors =    float2(uvx, uvy); \
            v.vertex =          o.worldPos; \
            TRANSFER_SHADOW(o); \
            triStream.Append(o); \
        }

[maxvertexcount(15)]
void geom (triangle v2g IN[3], inout TriangleStream<g2f> triStream) { 
    g2f o;
    v2g v;

    float4 avgPos =     AVG(vertex);
    float3 avgNorm =    AVG(normal);
    float2 UV =         AVG(uv);

    float2 windUV =     TRANSFORM_TEX(UV, _WindTex) + float2(1,0) * _Time.x * _WindSpeed;
    float2 sizeUV =     TRANSFORM_TEX(UV, _HeightTex);

    float3  wind =      tex2Dlod (_WindTex, float4(windUV, 0, 0)).xyz;
    float   sizefac =   tex2Dlod (_HeightTex, float4(sizeUV, 0, 0)).r;
    
    if ( sizefac < _MinGrassHeight )
        return;

    APPEND(0);
    APPEND(1);
    APPEND(2);
    triStream.RestartStrip();

    if ( distance ( avgPos, _WorldSpaceCameraPos ) >= _MaxDistance )
        return;

    float rand[3];
    rand[0] = RANDOM(IN[0].vertex);
    rand[1] = RANDOM(IN[1].vertex);
    rand[2] = RANDOM(IN[2].vertex);
    float4 right, forward, up, avg;
    float3 V;
    

    for ( int i = 0; i < 3; i++ ) {
        sizefac = tex2Dlod (_HeightTex,
            float4( lerp ( sizeUV, TRANSFORM_TEX( IN[i].uv, _HeightTex), 0.5), 0, 0) 
        ).r;

        if ( sizefac < _MinGrassHeight )
            continue;

        avg      =   lerp ( avgPos, IN[i].vertex, 0.5);
        o.uv    =    lerp ( UV, IN[i].uv, 0.5);

        right    =   normalize ( float4( rand[2-i], 0, rand[i], 1. ) );
        forward  =   right.zyxw;
        up       =   normalize ( float4( avgNorm, 0 ) + float4( wind, 0 ) * _WindDepth );
        o.normal =   normalize ( float3 (forward.x, ( - forward.x * up.x - forward.z * up.z ) / up.y, forward.z));
        V       =  normalize(_WorldSpaceCameraPos - avg);
        o.normal *= ( dot (V, o.normal ) > 0 ) ? 1  : -1 ;

        o.tangent = up.xyz;

        APPEND_ADDITIVE_BOTTOM(right * _MaxGrassWidth, 0, 0);
        APPEND_ADDITIVE(sizefac * (up * _MaxGrassHeight * _GrassCutOff + right * _MaxGrassWidth * (1-_GrassCutOff)), 0, 1);
        APPEND_ADDITIVE_BOTTOM(- right * _MaxGrassWidth, 1, 0);
        APPEND_ADDITIVE(sizefac * (up * _MaxGrassHeight * _GrassCutOff - right * _MaxGrassWidth * (1-_GrassCutOff)), 1, 1);
        
        triStream.RestartStrip();
    }
}