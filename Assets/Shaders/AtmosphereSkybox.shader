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

         v2f vert(appdata input)
         {
            vertexOutput output;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.texcoord = input.texcoord;
            return output;
         }

         float4 frag(v2f input) : SV_Target
         {
            return texCUBE (_Cube, input.texcoord);
         }
         ENDHLSL 
      }
   } 	
}
