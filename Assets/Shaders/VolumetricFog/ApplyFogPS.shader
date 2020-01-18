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
			#pragma multi_compile __ FOG_FALLBACK

            #include "UnityCG.cginc"
			#include "FogCommon.cginc"

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
			sampler3D atmoVolume;
			float4 clipPlanes;
			float farPlane;
			float distance;
			matrix viewProjectionInv;
			float fogHeight;
			float fogFalloff;
			float3 scatterColor;
			float scattering;
			float3 ambientColor;
			float3 dirLightColor;
			float3 dirLightDirection;
			float g;
			float noiseIntensity;
			float logfarOverNearInv;
			float4 volumeResolutionWH;

			Texture2D<float4> blueNoiseTex;

            fixed4 frag (v2f i) : SV_Target
            {
                float3 sceneColor = tex2D(_MainTex, i.uv);
				float depth = tex2D(_CameraDepthTexture, i.uv);

				float z = (farPlane - clipPlanes.x) * Linear01Depth(depth) + clipPlanes.x;
				float logZ = log(z);

				float atmoZ = (logZ - clipPlanes.w) * logfarOverNearInv;

				float3 atmoCoord = float3(i.uv, atmoZ);
				

				z = min(z, distance);
				z = (logZ - clipPlanes.w) * clipPlanes.z;
				float3 fogCoord = float3(i.uv, z);

				float3 screenSizeMult = float3(volumeResolutionWH.zw, 1);
				
				// blur to hide dithering
				//TODO: bilateral gaussian blur
				half4 fogSample = half4(0, 0, 0, 0);
				for (int x = -2; x < 2; x++) 
				{
					for (int y = -2; y < 2; y++) 
					{
						fogSample += tex3D(fogVolume, fogCoord + float3(x, -y, 0) * screenSizeMult);
					}
				}
				fogSample /= 16.0f;
				half4 atmoSample = tex3D(atmoVolume, atmoCoord);

				float3 combinedColor = sceneColor * atmoSample.a + atmoSample.rgb;

#ifdef FOG_FALLBACK
				// analytic fallback fog beyond volumetric fog distance
				float3 ndc = float3((i.uv * 2) - 1, depth);
				ndc.y *= -1;
				float4 worldPos = mul(viewProjectionInv, float4(ndc, 1));
				worldPos /= worldPos.w;
				float3 camPos = _WorldSpaceCameraPos;
				float3 viewDir = worldPos.xyz - camPos;
				float3 dist = length(viewDir);
				viewDir = normalize(viewDir);
				float3 fallbackFogStart = viewDir * distance + camPos;
				float fallbackDistance = max(dist - (distance), 0);
				float opticalDepth = exp(-fogFalloff * (fallbackFogStart.y + fallbackDistance * viewDir.y) + fogHeight);
				opticalDepth -= exp(-fogFalloff * fallbackFogStart.y + fogHeight);
				opticalDepth /= -fogFalloff * viewDir.y;
				float3 transmittance = exp(-opticalDepth * scattering * scatterColor * (1 - noiseIntensity));
				float3 scatteredColor = dirLightColor;
				scatteredColor *= henyeyGreensteinPhaseFunction(worldPos, dirLightDirection, camPos, g);
				scatteredColor += ambientColor;
				combinedColor = lerp(scatteredColor, combinedColor, transmittance);
#endif

				combinedColor = combinedColor * fogSample.a + fogSample.rgb;
				return float4(combinedColor, 1);
            }
            ENDHLSL
        }
    }
}
