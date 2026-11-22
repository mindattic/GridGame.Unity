// GG-T679/GG-T680: the Manalith satellite. Standard PBR from the Meshy maps plus a Fresnel rim in the Manalith's blue
// (docs/MANALITH_VISUAL_SPEC.md). _MetallicGlossMap packs metallic in R and smoothness (1 - roughness) in A.
Shader "Gaiden/ManalithSatellite"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "black" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _RimColor ("Rim", Color) = (0.459, 0.565, 0.784, 1)
        _RimPower ("Rim exponent", Float) = 3
        _RimIntensity ("Rim intensity", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard noshadow nolightmap nodynlightmap nodirlightmap nofog
        #pragma target 3.0
        sampler2D _MainTex, _MetallicGlossMap, _BumpMap;
        half _BumpScale, _RimPower, _RimIntensity;
        half4 _RimColor;
        struct Input { float2 uv_MainTex; float3 viewDir; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
            half4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = mg.r;
            o.Smoothness = mg.a;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            // viewDir is tangent-space here because o.Normal is written, so the rim follows the normal map.
            half rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _RimColor.rgb * _RimIntensity * pow(rim, _RimPower);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack Off
}
