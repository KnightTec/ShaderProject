Shader "Custom/GrassShader"
{
    Properties
    {
        _TessFactor ("Tesselation Factor", range(1,50)) = 1
        _MaxDistance("Maximum Tesselation Distance", range(1, 100)) = 20
        _GrassColor ("Grass Color", Color) = (0,1,0,1)
        _GrassBottomColor ("Grass Bottom Color", Color) = (0.5, 1, 0.5, 1)
        _MaxGrassHeight("Grass Height", range(0,1)) = 0.1
        _MaxGrassWidth("Grass Width", range(0,1)) = 0.05
        _WindTex ("Wind Texture", 2D) = "black" {}
        _WindSpeed ("Wind Speed", range(0, 20)) = 1
        _WindDepth ("Wind Depth", range(0, 25)) = 1
    }
    SubShader
    {
        Cull Off

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

            float _TessFactor;
            float _MaxDistance;
            float4 _GrassColor;
            float4 _GrassBottomColor;
            float _MaxGrassHeight;
            float _MaxGrassWidth;

            sampler2D _WindTex;
            float4 _WindTex_ST;
            float _WindSpeed;
            float _WindDepth;
        
            struct appdata {
                float4 vertex   : POSITION;
                float4 tangent  : TANGENT;
                float3 normal   : NORMAL;
                float2 uv       : TEXCOORD0;
            };

            struct v2g {
                float4 vertex   : SV_POSITION;
                float4 tangent  : TANGENT;
                float3 normal   : NORMAL;
                float2 uv       : TEXCOORD0;
            };

            struct g2f {
                float4 vertex   : SV_POSITION;
                float4 vertexWorld : TEXCOORD1;
                float3 normal   : NORMAL;
                float2 uv       : TEXCOORD0;
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
                float fac = lerp(_TessFactor, 0, x );

                t.edge[0] = fac;
                t.edge[1] = fac;
                t.edge[2] = fac;

                t.inside = fac;

                return t;
            }

            v2g vert (appdata IN) {
                v2g OUT;
                OUT.vertex  = IN.vertex;
                OUT.tangent = IN.tangent;
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
                INTERPOLATE(tangent);
                INTERPOLATE(uv);

                v2g o;

                o.vertex = data.vertex;
                o.normal = UnityObjectToWorldNormal(data.normal);
                o.tangent = data.tangent;
                o.uv = data.uv;

                return o;
            }

            #define RANDOM(fieldname) abs( sin ( dot (fieldname, fixed4 (9520.7254, 5115.2899, 1736.2851, 1683.4103) * 1683.4103) ) )

            [maxvertexcount(3)]
            void geom (triangle v2g IN[3], inout TriangleStream<g2f> triStream) { 
                g2f o;

                float4 avg = (IN[0].vertex + IN[1].vertex + IN[2].vertex) / 3;
                float2 uv = (IN[0].uv + IN[1].uv + IN[2].uv) / 3;
                uv = TRANSFORM_TEX(uv, _WindTex);
                uv += float2(1,0) * _Time.x * _WindSpeed;
                float3 wind = tex2Dlod (_WindTex, float4 ( uv, 0, 0)).xyz;
                
                o.normal = IN[0].normal;

                float rand0 = RANDOM(IN[0].vertex);
                float rand1 = RANDOM(IN[1].vertex);
                float rand2 = RANDOM(IN[2].vertex);

                float sizefac = lerp ( 0.5, 1, rand0);

                float4 right = normalize(float4(rand1,0,rand2,0));
                float4 up = normalize ( float4(o.normal,0) + float4(wind,0) * _WindDepth);
                o.normal = right.xyz;

                o.vertexWorld = avg + right * sizefac * _MaxGrassWidth;
                o.vertex = UnityObjectToClipPos(o.vertexWorld);
                o.uv = fixed2 (0,0);
                triStream.Append(o);

                o.vertexWorld = (avg + up * _MaxGrassHeight);
                o.vertex = UnityObjectToClipPos(o.vertexWorld);
                o.uv = fixed2 (0,1);
                triStream.Append(o);

                o.vertexWorld = (avg - right * sizefac * _MaxGrassWidth);\
                o.vertex = UnityObjectToClipPos(o.vertexWorld);
                o.uv = fixed2 (0,0);
                triStream.Append(o);
                triStream.RestartStrip();
                
            }

            fixed4 frag (g2f IN) : SV_Target{
                float3 N = normalize(IN.normal);
                float3 L = normalize(IN.vertexWorld - _WorldSpaceLightPos0);

                float intens1 = dot(N, L);
                float intens2 = dot(-N, L);
                float intensity = max (intens1, intens2);
                return _LightColor0 * intensity * lerp(_GrassBottomColor,_GrassColor, IN.uv.y);
            }

            ENDHLSL
        }
    }
}
