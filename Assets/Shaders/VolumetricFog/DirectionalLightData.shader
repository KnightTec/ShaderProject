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
			float4 lightSplitsNear;
			float4 lightSplitsFar;
		};
		RWStructuredBuffer<DirLightData> lightData : register(u1);

		float4 vert (appdata vertex) : SV_POSITION
		{
			for (int i = 0; i < 4; i++) 
			{
				lightData[0].worldToShadow[i] = unity_WorldToShadow[i];
			}
			lightData[0].lightSplitsNear = _LightSplitsNear;
			lightData[0].lightSplitsFar = _LightSplitsFar;
			
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
