Shader "Hidden/ApplyFogPS"
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
				float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
			sampler2D _CameraDepthTexture;
			sampler3D fogVolume;
			float4 clipPlanes;
			float farPlane;
			float distance;
			matrix viewProjectionInv;

            fixed4 frag (v2f i) : SV_Target
            {
                float3 sceneColor = tex2D(_MainTex, i.uv);
				float depth = tex2D(_CameraDepthTexture, i.uv);

				float3 ndc = float3(i.uv * float2(2, -2) + float2(-1, 1), depth);
				float4 worldPos = mul(viewProjectionInv, float4(ndc, 1));
				worldPos /= worldPos.w;

				//TODO: fallback after max distance

				
				float z = (farPlane - clipPlanes.x) * Linear01Depth(depth) + clipPlanes.x;
				z = min(z, distance);
				z = (log(z) - clipPlanes.w) * clipPlanes.z;
				float3 fogCoord = float3(i.uv, z);
				half4 fogSample = tex3D(fogVolume, fogCoord);

				float3 combinedColor = sceneColor * fogSample.a + fogSample.rgb;
				return float4(combinedColor, 1);
            }
            ENDHLSL
        }
    }
}
