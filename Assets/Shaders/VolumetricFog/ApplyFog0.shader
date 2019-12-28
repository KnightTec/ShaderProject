Shader "Hidden/NewImageEffectShader"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D mainTex;
			sampler3D froxelVolume;
			sampler2D _CameraDepthTexture;

            fixed4 frag (v2f i) : SV_Target
            {

                fixed4 sceneColor = tex2D(mainTex, i.uv);
				float depth = tex2D(_CameraDepthTexture, i.uv).r;
                float3 fogCoord = float3(float2(i.uv.x, 1 - i.uv.y), Linear01Depth(depth));
				float4 fogSample = tex3D(froxelVolume, fogCoord);
				float3 combinedColor = sceneColor * fogSample.a + fogSample.rgb;
                return fixed4(combinedColor, 1);
            }
            ENDHLSL
        }
    }
}
