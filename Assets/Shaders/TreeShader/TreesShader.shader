Shader "Custom/TreesShader"
{
    Properties
    {
        _MainTex ("TreeTex", 2D) = "white" {}
        _DistributionTex ("Tree Distibution tex", 2D) = "white" {}
        _WindTex ("Wind Tex", 2D) = "white" {}
        _Scale ("Tree Scale", Range(0,10)) = 1 
        _Random("Tree Density", Range (1,0.0)) = 0.5
        _WindDepth("WindDepth", Range (0,5)) = 3
        _WindSpeed("WindSpeed", Range (0,1)) = 0.02
        _VerticalSize ("Vetical Size", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="Transparent" }
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        
        Pass {
            CGPROGRAM
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdbase

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2g {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct g2f {
                float4 pos : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float2 uv : TEXCOORD0;
                SHADOW_COORDS(3)
            };

            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            sampler2D _MainTex;
            sampler2D _DistributionTex;
            sampler2D _WindTex;
            float _Scale;
            float _Random;
            float _WindDepth;
            float _WindSpeed;
            float _VerticalSize;

            v2g vert ( appdata IN ) {
                v2g OUT;
                OUT.vertex = IN.vertex;
                OUT.normal = IN.normal;
                OUT.uv = IN.uv;
                return OUT;
            }

            #define AVG(fieldname) ( ( IN[0].fieldname + IN[1].fieldname + IN[2].fieldname) / 3. )
            #define randomTree4 ( floor ( 4 * abs ( 0.99 * sin ( dot ( 1931. * IN[1].vertex.xz, float2 (12412.224, 2441.2123) ) ) ) ) )
            #define randomTree1 ( frac ( abs ( sin ( 15.412 * dot ( 12.22 * IN[2].vertex.yzx, float3(35.5132,12.211, 42.223)) ) ) ) )

            [maxvertexcount(4)]
            void geom ( triangle v2g IN[3], inout TriangleStream<g2f> tristream ) {
                if ( tex2Dlod ( _DistributionTex , float4( IN[1].uv,0,0 ) ).r > .75 && randomTree1 > _Random) {
                    g2f o;
                    o.worldPos = mul ( UNITY_MATRIX_M, AVG( vertex ) );
                    int random = randomTree4;
                    float2 uvStart = 
                        random == 0 ? float2 (0,0) :
                        random == 1 ? float2 (0,.5) :
                        random == 2 ? float2 (.5, 0) :
                        random >= 3 ? float2 (.5, .5) : float2 (.5, .5);

                    float wind = _WindDepth * tex2Dlod ( _WindTex, float4(IN[1].uv, 0, 0) + _WindSpeed * float4 (_Time.y, _Time.y,0,0) );
                    float4 middlePos = mul ( UNITY_MATRIX_V, o.worldPos );

                    o.normal = _WorldSpaceCameraPos - o.worldPos;
                    o.uv = uvStart + float2 (0,0);
                    o.pos = mul ( UNITY_MATRIX_P,
                        middlePos - _Scale * float4 (1,0,0,0)
                    );
                    TRANSFER_VERTEX_TO_FRAGMENT(o);
                    tristream.Append(o);

                    o.normal = float3(0,1,0);
                    o.uv = uvStart + float2 (0,.5);
                    o.pos = mul ( UNITY_MATRIX_P,
                        middlePos + _Scale * float4 (-1 + wind,1 * _VerticalSize,0,0) 
                    );
                    TRANSFER_VERTEX_TO_FRAGMENT(o);
                    tristream.Append(o);

                    o.normal = _WorldSpaceCameraPos - o.worldPos;
                    o.uv = uvStart + float2 (.5,0);
                    o.pos = mul ( UNITY_MATRIX_P,
                        middlePos + _Scale * float4 (1,0,0,0)
                    );
                    TRANSFER_VERTEX_TO_FRAGMENT(o);
                    tristream.Append(o);

                    o.normal = float3(0,1,0);
                    o.uv = uvStart + float2 (.5, .5);
                    o.pos = mul ( UNITY_MATRIX_P,
                        middlePos + _Scale * float4 (1 + wind,1 * _VerticalSize,0,0)
                    );
                    tristream.Append(o);
                    TRANSFER_VERTEX_TO_FRAGMENT(o);
                    tristream.RestartStrip();
                }
            }

            fixed4 frag ( g2f IN ) : SV_TARGET {
                float4 col = tex2D (
                    _MainTex,
                    IN.uv
                );
                if ( col.a < 0.65f) 
                    discard;

                float3 L = normalize ( _WorldSpaceLightPos0 - IN.worldPos );
                float3 N = normalize ( IN.normal );

                UNITY_LIGHT_ATTENUATION(attenuation, IN, IN.worldPos);
                float diffuse = max ( dot (L, N), 0. ) / 3.1415;

                float3 diffuseTerm = col.rgb * diffuse * _LightColor0;

                return float4(diffuseTerm * attenuation, col.a);
            }

            ENDCG
        }
    }
}
