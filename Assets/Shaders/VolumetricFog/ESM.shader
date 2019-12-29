Shader "Hidden/ESM"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
			#pragma target 5.0
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

            Texture2D _MainTex;
			SamplerState sampler_MainTex;

            float4 frag (v2f i) : SV_Target
            {
                //float shadow = tex2D(_MainTex, i.uv).r;
				// convert to exponential shadow map
				// http://jankautz.com/publications/esm_gi08.pdf
				float4 accum = 0;
				accum += exp(_MainTex.GatherRed(sampler_MainTex, i.uv, int2(0, 0)) * -80);
				//accum += exp(_MainTex.GatherRed(sampler_MainTex, i.uv, int2(0, 1)) * -30);
				//accum += exp(_MainTex.GatherRed(sampler_MainTex, i.uv, int2(1, 0)) * -30);
				//accum += exp(_MainTex.GatherRed(sampler_MainTex, i.uv, int2(1, 1)) * -30);
				//float output = exp(shadow * -30);
                return dot(accum, 1/4.0f);
            }
            ENDHLSL
        }
		GrabPass {}
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

			sampler2D _GrabTexture;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				float3 screenTex = tex2D(_GrabTexture, i.uv);
				float stepX = 1 / 2048.0f;

				for (int x = 0; x < 10; x++) 
				{
					float2 offsetX = float2(stepX * x, 0);
					screenTex += tex2D(_GrabTexture, i.uv + offsetX);
					screenTex += tex2D(_GrabTexture, i.uv - offsetX);
				}
				screenTex /= 10 * 2 + 1;
				return fixed4(screenTex, 1);
            }

            ENDHLSL
        }
		GrabPass {}
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

			sampler2D _GrabTexture;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				float3 screenTex = tex2D(_GrabTexture, i.uv);
				float stepY = 1 / 2048.0f;

				for (int y = 0; y < 5; y++) 
				{
					float2 offsetY = float2(0, stepY * y);
					screenTex += tex2D(_GrabTexture, i.uv + offsetY);
					screenTex += tex2D(_GrabTexture, i.uv - offsetY);
				}
				screenTex /= 5 * 2 + 1;
				return fixed4(screenTex, 1);
            }

            ENDHLSL
        }
    }
}
