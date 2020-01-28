// This shader is a copy of the shader postet at 
// https://answers.unity.com/questions/877170/render-scene-depth-to-a-texture.html
// and is included for reference only.

Shader "Custom/DepthShader"
{
    Properties
    {
        _MainTex ("Color", Color) = (1,1,1,1)
        _DepthLevel ("Depth Level", Range(1,3)) = 1
    }
    SubShader
    {
        Pass 
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            float _DepthLevel;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD1;
            };

            v2f vert (appdata i) {
                v2f o;

                o.vertex = UnityObjectToClipPos ( i.vertex.xyz );
                o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, i.uv);
                
                return o;
            }

            fixed4 frag ( v2f i ) : SV_TARGET {
                float depth = tex2D (_CameraDepthTexture, float2 (i.uv.x, 1 - i.uv.y) ).r;
            
                return (1 - depth ) * _ProjectionParams.z;
            }

            ENDCG
        }
    }
}
