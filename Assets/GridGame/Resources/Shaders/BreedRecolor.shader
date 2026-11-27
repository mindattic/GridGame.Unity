// GG-T1774 (docs/research/sprite-recolor.md §8): a creature breed's recolour, baked once per (art, breed) by BreedArt
// with Graphics.Blit. The region mask picks what changes: R body, G secondary, B keep-as-is (eyes, claws, teeth; B wins
// where it overlaps R or G). A recoloured pixel keeps its own OKLab lightness as the coordinate into the region's ramp,
// so the painted shading survives; alpha is untouched except a breed's own Alpha on the recoloured regions.
Shader "Hidden/GridGame/BreedRecolor"
{
    Properties
    {
        _MainTex ("Art", 2D) = "white" {}
        _MaskTex ("Mask (R body, G secondary, B keep)", 2D) = "black" {}
        _RampBody ("Body ramp", 2D) = "white" {}
        _RampSecondary ("Secondary ramp", 2D) = "white" {}
        _KeepTint ("Keep tint", Color) = (1, 1, 1, 1)
        _KeepMix ("Keep mix", Range(0, 1)) = 0
        _LightLift ("Light lift", Range(-1, 1)) = 0
        _ChromaScale ("Chroma scale", Range(0, 2)) = 1
        _Alpha ("Recoloured alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _MaskTex, _RampBody, _RampSecondary;
            float4 _KeepTint;
            float _KeepMix, _LightLift, _ChromaScale, _Alpha;

            // The project may run in Gamma space (then samples are gamma-encoded) or Linear: OKLab wants linear sRGB.
            float3 ToLinear(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return GammaToLinearSpace(c);
            #else
                return c;
            #endif
            }

            float3 ToOutput(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return LinearToGammaSpace(c);
            #else
                return c;
            #endif
            }

            float3 LinearToOklab(float3 c)
            {
                float l = 0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b;
                float m = 0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b;
                float s = 0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b;
                l = pow(max(l, 0), 1.0 / 3.0); m = pow(max(m, 0), 1.0 / 3.0); s = pow(max(s, 0), 1.0 / 3.0);
                return float3(0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                              1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                              0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
            }

            float3 OklabToLinear(float3 c)
            {
                float l = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
                float m = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
                float s = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;
                l = l * l * l; m = m * m * m; s = s * s * s;
                return saturate(float3(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                                      -1.2684380046 * l + 2.6097574011 * m - 0.3413510813 * s,
                                      -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s));
            }

            // A ramp sample (taken at the pixel's lightness), its chroma scaled.
            float3 Ramp(float3 sampled)
            {
                float3 lab = LinearToOklab(ToLinear(sampled));
                lab.yz *= _ChromaScale;
                return OklabToLinear(lab);
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float4 m = tex2D(_MaskTex, i.uv);
                float keep = saturate(m.b);
                float body = saturate(m.r) * (1 - keep);
                float sec = saturate(m.g) * (1 - keep);

                float3 lin = ToLinear(c.rgb);
                float3 lab = LinearToOklab(lin);
                float t = _LightLift >= 0 ? lerp(lab.x, 1, _LightLift) : lerp(lab.x, 0, -_LightLift);
                t = saturate(t);

                float3 rgb = lin;
                rgb = lerp(rgb, Ramp(tex2D(_RampBody, float2(t, 0.5)).rgb), body);
                rgb = lerp(rgb, Ramp(tex2D(_RampSecondary, float2(t, 0.5)).rgb), sec);

                // Keep region: the tint's hue and chroma, lightness halfway between the pixel's own and the tint's, so an
                // eye's highlight dot and dark pupil still read (research §5).
                float3 tint = LinearToOklab(ToLinear(_KeepTint.rgb));
                float3 kept = OklabToLinear(float3(lerp(lab.x, tint.x, 0.5), tint.yz));
                rgb = lerp(rgb, kept, keep * _KeepMix);

                float a = c.a * lerp(1, _Alpha, saturate(body + sec));
                return fixed4(ToOutput(rgb), a);
            }
            ENDCG
        }
    }
}
