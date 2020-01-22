Shader "Custom/AtmosphereSkybox"
{
	Properties 
	{
      [HDR]_Cube ("Environment Map", Cube) = "black" {}
	}

   SubShader 
   {
      Tags { "Queue"="Background"  }

      Pass 
	  {
         ZWrite Off 
         Cull Off

         HLSLPROGRAM
		 #include "UnityCG.cginc"
         #pragma vertex vert
         #pragma fragment frag

         samplerCUBE _Cube;
		 matrix _rotationMatrix;

         struct appdata {
            float4 vertex : POSITION;
            float3 texcoord : TEXCOORD0;
         };

         struct v2f {
            float4 vertex : SV_POSITION;
            float3 texcoord : TEXCOORD0;
         };

		 // source: https://medium.com/@cyrilltoboe/simplest-gradient-skybox-with-moon-8581610c86c0
		float calcSunSpot(float3 sunDirPos, float3 skyDirPos)
		{
			float3 delta = sunDirPos - skyDirPos;
			float dist = length(delta);
			half spot = 1.0 - smoothstep(0.0, 0.025, dist);
			return 1.0 - pow(0.125, spot * 50);
		}

         v2f vert(appdata input)
         {
            v2f output;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.texcoord = input.texcoord;
            return output;
         }

         float4 frag(v2f input) : SV_Target
         {
			float sun = calcSunSpot(_WorldSpaceLightPos0.xyz, input.texcoord.xyz);
			float3 texCoord = mul(input.texcoord.xyz, _rotationMatrix);
			float3 stars = texCUBE(_Cube, texCoord);
			float3 outerSpaceColor = lerp(stars * 0.2f, float3(1, 1, 1) * 500, sun);

			return float4(outerSpaceColor, 1.0);
         }
         ENDHLSL 
      }
   } 	
}
