Shader "Custom/GrassShader"
{
    Properties
    {
        _Ambient        ("Ambient Light", range(0,1)) = 0.2

        _TessFactor     ("Tesselation Factor", range(1,50)) = 1
        _MaxDistance    ("Maximum Tesselation Distance", range(1, 100)) = 20

        _GrassColor     ("Grass Color", Color) = (0,1,0,1)
        _DryGrassColor  ("Dry Grass Color", Color) = (0,1,0,1)
        _GrassBottomColor("Grass Bottom Color", Color) = (0.5, 1, 0.5, 1)
        _DryGrassTex    ("Dry Grass Distribution Tex", 2D) = "black" {} 
        _GrassNormal    ("Grass Normal Map", 2D) = "black" {}

        _HeightTex      ("Grass Height Texture", 2D) = "white" {}
        _MinGrassHeight ("Min Grass Height", range(0,10)) = 0.02
        _MaxGrassHeight ("Max Grass Height", range(0,10)) = 0.5
        _GrassColorOffset ("Color Offset", range(0,1)) = 0
        _MaxGrassWidth  ("Grass Width", range(0,1)) = 0.05

        _WindTex        ("Wind Texture", 2D) = "black" {}
        _WindSpeed      ("Wind Speed", range(0, 20)) = 1
        _WindDepth      ("Wind Depth", range(0, 100)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Grass PAss
        Pass 
        {
            HLSLPROGRAM
            
            #pragma target 4.6

            #include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
            #include "Lighting.cginc"


            #pragma vertex vert
            #pragma hull hull
            #pragma domain dom
            #pragma geometry geom
            #pragma fragment frag

            // Tessellation Options
            float _TessFactor;
            float _MaxDistance;

            // Grass Appearance
            float  _Ambient;
            float4 _GrassColor;
            float4 _DryGrassColor;
            float4 _GrassBottomColor;
            sampler2D _DryGrassTex;
            float4 _DryGrassTex_ST;

            // Grass Geometry
            sampler2D _HeightTex;
            float4 _HeightTex_ST;
            float _GrassColorOffset;
            float _MinGrassHeight;
            float _MaxGrassHeight;
            float _MaxGrassWidth;

            // Wind
            sampler2D _WindTex;
            float4 _WindTex_ST;
            float _WindSpeed;
            float _WindDepth;
        
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
                float4 vertex       : SV_POSITION;
                float4 vertexWorld  : TEXCOORD0;
                float3 normal       : NORMAL;
                //float3 tangent      : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float2 blendFactors : TEXCOORD3;
            };

            struct tessFactors {
                float edge[3]   : SV_Tessfactor;
                float inside    : SV_InsideTessFactor;
            };

            tessFactors patch(InputPatch<v2g, 3> ip) {
                tessFactors t;
                float4 avg = (ip[0].vertex + ip[1].vertex + ip[2].vertex)/3;
                float dist = distance(mul(unity_ObjectToWorld, avg), _WorldSpaceCameraPos);
                float x = lerp(0,1,min( _MaxDistance, dist) / _MaxDistance);
                float fac = lerp(_TessFactor, 2, x );

                t.edge[0] = fac;
                t.edge[1] = fac;
                t.edge[2] = fac;

                t.inside = fac;

                return t;
            }

            v2g vert (appdata IN) {
                v2g OUT;
                OUT.vertex  = IN.vertex;
                OUT.normal  = IN.normal;
                OUT.uv      = IN.uv;
                return OUT;
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
            #define APPEND(index) o.vertexWorld = IN[index].vertex + 0.001 * float4(IN[index].normal,0); o.vertex = UnityObjectToClipPos(o.vertexWorld); o.normal = IN[index].normal; o.uv = IN[index].uv; o.blendFactors = float2( tex2Dlod(_HeightTex, float4(IN[index].uv, 0, 0)).r, 0); triStream.Append(o)
            #define APPEND_ADDITIVE(summand,uvx,uvy) if (sizefac > 0 ) { o.vertexWorld = avg + (summand); o.vertex = UnityObjectToClipPos (o.vertexWorld); o.uv = avgUV; o.blendFactors = fixed2(uvx, uvy); triStream.Append(o); }
            #define AVG(fieldname) (IN[0].fieldname + IN[1].fieldname + IN[2].fieldname) / 3

            [maxvertexcount(6)]
            void geom (triangle v2g IN[3], inout TriangleStream<g2f> triStream) { 
                g2f o;

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

                float sizefac = lerp ( 0.5, 1, rand0) * size;
                sizefac = sizefac >= _MinGrassHeight ? sizefac : 0;

                float4 right =  normalize ( float4( rand1, 0, rand2, 0 ) );
                float4 up =     normalize ( float4( avgNorm, 0 ) + float4( wind, 0 ) * _WindDepth );
                o.normal =      normalize ( float3 (right.x, (- right.x * up.x - right.z * up.z) / up.y, right.z));

                APPEND_ADDITIVE(right * sizefac * _MaxGrassWidth, 1, 0);
                APPEND_ADDITIVE(up * sizefac * _MaxGrassHeight, 1, 1);
                APPEND_ADDITIVE(- right * sizefac * _MaxGrassWidth, 1, 0);
                triStream.RestartStrip();
            }

            fixed4 frag (g2f IN) : SV_Target{
                float3 N = normalize(IN.normal);
                float3 L = normalize(IN.vertexWorld - _WorldSpaceLightPos0);

                float intensity1 = clamp(0,1,dot(N, L));
                float intensity2 = clamp(0,1,dot(-N, L));

                float intensity = intensity1 + intensity2 + _Ambient;
                float4 GrassColor = lerp (_GrassColor, _DryGrassColor, tex2Dlod (_DryGrassTex, float4 (IN.uv, 0, 0)).r);

                return fixed4((_LightColor0 * intensity * lerp(_GrassBottomColor,GrassColor, IN.blendFactors.y)).xyz, clamp(0,1,IN.blendFactors.x > 0 ? IN.blendFactors.x + _GrassColorOffset : 0))  ;
            }

            ENDHLSL
        }
    }
}
