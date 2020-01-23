Shader "Hidden/DirectionalLightData"
{
    SubShader
    {
		Pass 
		{
		HLSLPROGRAM
		#pragma target 5.0
        #pragma vertex vert
		#pragma fragment frag
			
		#include "UnityCG.cginc"
		#include "Lighting.cginc"

		struct appdata 
		{
			float4 pos : POSITION;
		};

		struct DirLightData 
		{
			float4x4 worldToShadow[4];
		};
		RWStructuredBuffer<DirLightData> lightData : register(u1);

		float4 vert (appdata vertex) : SV_POSITION
		{
			lightData[0].worldToShadow = unity_WorldToShadow;			
			return float4(0, 0, 0, 1);
		}
		
		fixed4 frag (float4 position : SV_POSITION) : SV_Target
		{
			return fixed4(0, 0, 0, 0);
		}
		ENDHLSL
		}
    }
}
