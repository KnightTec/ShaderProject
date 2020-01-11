#include "GrassStructs.cginc"
#include "GrassVars.cginc"
#include "AutoLight.cginc"

v2g vert (appdata IN) {
    v2g OUT;
    OUT.vertex  = IN.vertex;
    OUT.normal  = IN.normal;
    OUT.uv      = IN.uv;
    return OUT;
}

tessFactors patch(InputPatch<v2g, 3> ip) {
    tessFactors t;
    float4 avg = (ip[0].vertex + ip[1].vertex + ip[2].vertex)/3;
    float dist = distance(mul(unity_ObjectToWorld, avg), _WorldSpaceCameraPos);
    float x = lerp(0,1,min( _MaxDistance, dist) / _MaxDistance);
    float fac = lerp(_TessFactor, 1, x );

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

#define RANDOM(fieldname) abs( sin ( dot (fieldname, fixed4 (9520.7254, 5115.2899, 1736.2851, 1683.4103) * 1683.4103) ) )
#define APPEND(index) v = IN[index]; o.vertexWorld = IN[index].vertex + 0.001 * float4(IN[index].normal,0); o.pos = UnityObjectToClipPos(o.vertexWorld); o.tangent = float3(0,0,0); o.normal = IN[index].normal; o.uv = IN[index].uv; o.blendFactors = float2( tex2Dlod(_HeightTex, float4(IN[index].uv, 0, 0)).r, 0); TRANSFER_SHADOW(o); triStream.Append(o)
#define APPEND_ADDITIVE(summand,uvx,uvy) if (sizefac > 0 ) { o.vertexWorld = avg + (summand); o.pos = UnityObjectToClipPos (o.vertexWorld); o.uv = avgUV;o.blendFactors = fixed2(uvx, uvy); o.normal = normal; v.vertex = o.vertexWorld; TRANSFER_SHADOW(o); triStream.Append(o); }
#define APPEND_ADDITIVE_BOTTOM(summand,uvx,uvy) if (sizefac > 0 ) { o.vertexWorld = avg + (summand); o.pos = UnityObjectToClipPos (o.vertexWorld); o.uv = avgUV;o.blendFactors = fixed2(uvx, uvy); o.normal = avgNorm; v.vertex = o.vertexWorld; TRANSFER_SHADOW(o); triStream.Append(o); }
#define AVG(fieldname) (IN[0].fieldname + IN[1].fieldname + IN[2].fieldname) / 3

[maxvertexcount(9)]
void geom (triangle v2g IN[3], inout TriangleStream<g2f> triStream) { 
    g2f o;
    v2g v;

    float4 avg =        AVG(vertex);
    float3 avgNorm =    AVG(normal);
    float2 avgUV =      AVG(uv);

    float2 windUV =     TRANSFORM_TEX(avgUV, _WindTex) + float2(1,0) * _Time.x * _WindSpeed;
    float2 sizeUV =     TRANSFORM_TEX(avgUV, _HeightTex);

    float3 wind =       tex2Dlod (_WindTex, float4 ( windUV, 0, 0)).xyz;
    float size =        tex2Dlod (_HeightTex, float4 (sizeUV, 0, 0)).r;
    
    APPEND(0);
    APPEND(1);
    APPEND(2);
    triStream.RestartStrip();

    float rand0 = RANDOM(IN[0].vertex);
    float rand1 = RANDOM(IN[1].vertex);
    float rand2 = RANDOM(IN[2].vertex);

    avg += float4 ( (rand0 - 0.5) * 0.05, 0, (rand1 - 0.5) * 0.05, 0);

    float sizefac = lerp ( 0.5, 1, rand0) * size;
    sizefac = sizefac >= _MinGrassHeight ? sizefac : 0;

    float4 right =  normalize ( float4( rand1, 0, rand2, 0 ) );
    float4 forward = float4 (right.z, 0, -right.x, 0);
    float4 up =     normalize ( float4( avgNorm, 0 ) + float4( wind, 0 ) * _WindDepth );
    float3 normal = normalize ( float3 (forward.x, ( - forward.x * up.x - forward.z * up.z ) / up.y, forward.z));

    float3 L =  normalize(_WorldSpaceLightPos0 - avg);
    normal = ( dot (L, normal) > 0 ) ? normal : -normal;
    o.tangent =     up.xyz;

    APPEND_ADDITIVE_BOTTOM(right * sizefac * _MaxGrassWidth, 1, 0);
    APPEND_ADDITIVE(sizefac * (up * _MaxGrassHeight * _GrassCutOff + right * _MaxGrassWidth * (1-_GrassCutOff)), 1, 1);
    APPEND_ADDITIVE_BOTTOM(- right * sizefac * _MaxGrassWidth, 1, 0);
    APPEND_ADDITIVE(sizefac * (up * _MaxGrassHeight * _GrassCutOff - right * _MaxGrassWidth * (1-_GrassCutOff)), 1, 1);
    triStream.RestartStrip();
}