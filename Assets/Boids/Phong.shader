Shader "Simple/Phong"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "White" {}
		_Color ("Color", Color) = (0.8, 0.8, 0.8, 1.0 )
		_Ambient ("Ambient Lighting", Range( 0.0, 1.0 )) = 0.1
		_SpecularExponent( "Specular Exponent", float ) = 4
		_SpecularFactor( "Specular Factor", Range( 0.0, 1.0 )) = 0.2
	}
	SubShader
	{
		Tags { "LightMode" = "ForwardBase" }

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"

			struct appdata
			{
				float4 vert: POSITION;
				float3 norm: NORMAL;
				float2 uv: TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 pos: SV_POSITION;
				float3 norm: TEXCOORD1;
				float2 uv: TEXCOORD0;
				float4 worldPos: TEXCOORD2;
			};
				
			sampler2D _MainTex;
			float4 _Color;
			float _SpecularExponent;
			float _Ambient;
			float _SpecularFactor;

			struct Boid {
				float4x4 m;
				float3 h;
			};

			StructuredBuffer<Boid> dataBuffer;
			
			v2f vert (appdata v, uint instanceID: SV_InstanceID )
			{
				unity_ObjectToWorld = dataBuffer[instanceID].m;
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				o.pos = UnityObjectToClipPos( v.vert );
				o.norm = UnityObjectToWorldNormal( v.norm );
				o.uv = v.uv;
				o.worldPos = mul( unity_ObjectToWorld, v.vert );
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float3 n = normalize( i.norm );
				float light = dot( n, normalize( _WorldSpaceLightPos0 ));
				float diffuse = tex2D( _MainTex, i.uv ) * max( light, _Ambient ) * _LightColor0;

				float3 v = normalize( UnityWorldSpaceViewDir( i.worldPos ));
				float3 l = normalize( UnityWorldSpaceLightDir( i.worldPos ));

				float r = -l - 2 * n * dot( n, -l );
				float specular = pow( max( dot( v, r ), 0.0f ), _SpecularExponent ) * _LightColor0;

				return diffuse * ( 1.5f - _SpecularFactor ) + specular * _SpecularFactor;
			}

			ENDHLSL
		}
	}
	Fallback "Standard"
}