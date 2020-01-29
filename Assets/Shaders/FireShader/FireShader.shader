Shader "Custom/FireShader"
{
    Properties
    {
        _Peak ("Peak Color", Color) = (1,1,1,1)
        _Top ("Top Color", Color) = (1,0,0,0)
        _Bottom ("Bottom Color", Color) = ( 1,1,1,1)
        _Noise ("NoiseTexture", 2D) = "white" {}
        _Flutter ("Flutter speed", range(0.1,10)) = 1
        _Depth ("Flutter depth", range(0.0001,0.1)) = 0.0001
        _AlphaPow ("Alpha pow", Int) = 1
        _NoiseSpeed ("Noise Speed", range(-10,10) ) = 1

        _Cutoff ("Cutoff val", range (1,0)) = 0.15
        _Seed ("Random Seed", range(1,2) )= 1
    }
    SubShader
    {
        Tags {"RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Pass {
            CGPROGRAM
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            #pragma vertex vert
            #pragma fragment frag

            float4 _Top;
            float4 _Bottom;
            float4 _Peak;

            sampler2D _Noise;
            float4 _Noise_ST;

            float _Flutter;
            float _Depth;
            float _Cutoff;
            float _Seed;
            float _NoiseSpeed;
            int _AlphaPow;

            struct appdata {
                float3 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD1;
            };

            #define random(IN) \
                sin ( 1.231 * _Seed * dot ( IN, float3(12.241 * _Seed ,47.462,66.42 )))

            v2f vert (appdata i) {
                v2f o;

                float3 position =
                    i.vertex +
                        (_Depth ) * float3 ( sin ( i.vertex.x + _Flutter * _Time.y), 0, sin ( i.vertex.z + _Flutter * _Time.y));

                o.vertex = UnityObjectToClipPos (float4 ( position, 1 ));
                o.uv = TRANSFORM_TEX(i.uv, _Noise);
 
                return o;
            }

            fixed4 frag ( v2f i ) : SV_TARGET {
                float noise = tex2D (_Noise, i.uv - _NoiseSpeed * _Time.y * float2 (0,1)).r;

                if ( noise < _Cutoff * i.uv.y)
                    discard;

                int step1 = step ( noise, i.uv.y );
                int step2 = step ( noise, i.uv.y - 0.4);
                int step3 = step ( noise, i.uv.y - 0.5);

                float4 col = lerp ( 
                    _Peak,
                    _Bottom,
                    step1 - step2
                );

                float4 final = lerp (
                    col,
                    _Top,
                    step2 - step3
                );

                return float4 (final.rgb, 1. - pow(i.uv.y, _AlphaPow));
            }

            ENDCG
        }
    }
}
