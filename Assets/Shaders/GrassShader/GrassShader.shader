Shader "Custom/GrassShader"
{
    Properties
    {
        _Ambient        ("Ambient Light", range(0,1)) = 0.2

        _TessFactor     ("Tesselation Factor", range(1,50)) = 1
        _MaxDistance    ("Maximum Tesselation Distance", range(1, 100)) = 20

        _GrassColor     ("Grass Color", Color) = (0,1,0,1)
        _DryGrassColor  ("Dry Grass Color", Color) = (0,1,0,1)
        _GrassBottomColor("Grass Bottom Color", Color) = (0.5, 1, 0.5, 1)
        _DryBottomColor ("Dry Bottom Color", Color) = (0.5, 1, 0.5, 1)
        _DryGrassTex    ("Dry Grass Distribution Tex", 2D) = "black" {} 
        _GrassNormal    ("Grass Normal Map", 2D) = "black" {}

        _HeightTex      ("Grass Height Texture", 2D) = "white" {}
        _MinGrassHeight ("Min Grass Height", range(0,10)) = 0.02
        _MaxGrassHeight ("Max Grass Height", range(0,10)) = 0.5
        _GrassCutOff    ("Grass Cutoff Point", range (0,1)) = 1
        _GrassColorOffset ("Color Offset", range(0,1)) = 0
        _MaxGrassWidth  ("Grass Width", range(0,1)) = 0.05

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


            fixed4 frag (g2f IN) : SV_Target{
                float3 N = normalize(IN.normal);
                float3 L = normalize(_WorldSpaceLightPos0 - IN.worldPos);

                float intensity1 = dot( N, L);
                float intensity2 = dot(-N, L);
                //float attenuation;//= LIGHT_ATTENUATION(IN);
                UNITY_LIGHT_ATTENUATION(attenuation, IN, IN.worldPos);
                //UnityComputeForwardShadows()

                float intensity = max(clamp(0,1, max(intensity1,intensity2)), _Ambient);
                float fac = tex2Dlod (_DryGrassTex, float4 (IN.uv, 0, 0)).r;

                float dist = distance(IN.worldPos, _WorldSpaceCameraPos);

                float4 GrassColor = lerp (_GrassColor, _DryGrassColor, fac);
                float4 BottomColor = lerp ( lerp (_GrassBottomColor, _DryBottomColor, fac), GrassColor, clamp(0,0.6, dist /(_MaxDistance) ));

                return fixed4(
                (
                _LightColor0 * intensity * attenuation *
                lerp(BottomColor,GrassColor, clamp(0,1,IN.blendFactors.y))).xyz, 

                clamp( 0,1,IN.blendFactors.y > 0 ? 1 : IN.blendFactors.x + _GrassColorOffset)
                );
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