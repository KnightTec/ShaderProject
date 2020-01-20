Shader "Custom/GrassShader"
{
    Properties
    {
        _MainTexture    ("Main Texture", 2D) = "white" {}
        _Ambient        ("Ambient Light", range(0,1)) = 0.2
        _SpecularExp    ("Specular Exponent", float) = 40
        _DiffSpec       ("Diff/Spec ratio", range(0,1)) = 0.5

        _TessFactor     ("Tesselation Factor", range(1,50)) = 1
        _MaxDistance    ("Maximum Tesselation Distance", range(1, 100)) = 20

        _GrassColor     ("Grass Color", Color) = (0,1,0,1)
        _DryGrassColor  ("Dry Grass Color", Color) = (0,1,0,1)
        _GrassBottomColor("Grass Bottom Color", Color) = (0.5, 1, 0.5, 1)
        _DryBottomColor ("Dry Bottom Color", Color) = (0.5, 1, 0.5, 1)
        _DryGrassTex    ("Dry Grass Distribution Tex", 2D) = "black" {}

        _HeightTex      ("Grass Height Texture", 2D) = "white" {}
        _MinGrassHeight ("Min Grass Height", range(0,10)) = 0.02
        _MaxGrassHeight ("Max Grass Height", range(0,10)) = 0.5
        _GrassCutOff    ("Grass Cutoff Point", range (0,1)) = 1
        _GrassColorOffset ("Color Offset", range(0,1)) = 0
        _MaxGrassWidth  ("Grass Width", range(0,0.1)) = 0.05

        _WindTex        ("Wind Texture", 2D) = "black" {}
        _WindSpeed      ("Wind Speed", range(0, 20)) = 1
        _WindDepth      ("Wind Depth", range(0, 100)) = 1
    }

    CGINCLUDE
    #include "Tesselation.cginc"
    ENDCG

    SubShader
    {
        // Grass Pass
        Pass 
        {
            Tags {"LightMode" = "ForwardBase"}
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            Lighting On

            CGPROGRAM
            
            #pragma target 4.6
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            #pragma vertex vert
            #pragma hull hull
            #pragma domain dom
            #pragma geometry geom
            #pragma fragment frag

            sampler2D _MainTexture;

            float _SpecularExp;
            float _DiffSpec;

            const float pi = 3.14159265f;


            fixed4 frag (g2f IN) : SV_Target{
               
                float3 N = normalize(IN.normal);
                float3 L = normalize(_WorldSpaceLightPos0 - IN.worldPos);
                float3 V = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float3 H = normalize( L + V );

                if ( dot (L, N) < 0)
                    N = -N;

                float energy = (8. + _SpecularExp) / (8. * 3.1415); 
                float specular = energy * pow ( max ( dot ( N, H), 0.), _SpecularExp);
                float3 specularTerm = _LightColor0 * specular;

                UNITY_LIGHT_ATTENUATION(attenuation, IN, IN.worldPos);

                float fac = tex2Dlod (_DryGrassTex, float4 (IN.uv, 0, 0)).r;
                float dist = distance (_WorldSpaceCameraPos, IN.worldPos);

                float4 GrassColor = lerp (_GrassColor, _DryGrassColor, fac);
                float4 BottomColor = lerp (_GrassBottomColor, _DryBottomColor, fac);

                float3 FragColor = lerp( BottomColor, GrassColor, clamp(0,1,IN.blendFactors.y) ).rgb;
                float alpha = 1.0;
                float diffuse = max ( dot (L, N), 0. ) / 3.1415;
                float3 diffuseTerm = FragColor * diffuse * _LightColor0;

                float3 tex = tex2D ( _MainTexture, IN.blendFactors );

                FragColor = FragColor - tex.r;
                float3 ambientTerm = FragColor * _Ambient;

                return fixed4( ( attenuation < 2. ? attenuation : 1. ) * (lerp ( diffuseTerm, specularTerm, _DiffSpec ) + ambientTerm ), 1 * tex.b);
            }

            ENDCG
        }

        Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
            CGPROGRAM

            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma hull hull
            #pragma domain dom
            #pragma target 4.6
            #pragma multi_compile_shadowcaster

            float4 frag(g2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        } 
    }
}