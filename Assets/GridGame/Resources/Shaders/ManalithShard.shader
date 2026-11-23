// GG-T679/GG-T680: a debris shard shed by the Manalith (mesh particles). Lit by the rig's key and rim lights passed in as
// properties, glowing where the particle colour flags it, faded by the particle's alpha, and softened near the frame edge so a falling shard never cuts off hard.
// Writes premultiplied colour and straight alpha coverage; ManalithBloom un-premultiplies for UI Toolkit.
Shader "Gaiden/ManalithShard"
{
    Properties
    {
        _Color ("Albedo", Color) = (0.204, 0.180, 0.259, 1)
        _EmissionColor ("Emission (HDR)", Color) = (0, 0, 0, 0)
        _KeyDir ("Key light direction (to light)", Vector) = (-0.5, 0.7, -0.5, 0)
        _KeyColor ("Key light", Color) = (1, 1, 1, 1)
        _RimDir ("Rim light direction (to light)", Vector) = (0.4, 0.3, 1, 0)
        _RimLightColor ("Rim light", Color) = (0.184, 0.226, 0.314, 1)
        _Ambient ("Ambient", Color) = (0.08, 0.08, 0.1, 1)
        _EdgeFade ("Frame edge fade", Float) = 0.12
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _Color, _EmissionColor, _KeyColor, _RimLightColor, _Ambient;
            float4 _KeyDir, _RimDir;
            float _EdgeFade;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float4 sp : TEXCOORD1; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.sp = ComputeScreenPos(o.pos);
                o.color = v.color;
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                half3 lit = _Ambient.rgb
                    + _KeyColor.rgb * saturate(dot(n, normalize(_KeyDir.xyz)))
                    + _RimLightColor.rgb * saturate(dot(n, normalize(_RimDir.xyz)));
                // The particle colour's red is a flag: 1 for a glowing shard (20%), 0 for dark rock.
                half3 c = _Color.rgb * lit + _EmissionColor.rgb * i.color.r;
                float2 uv = i.sp.xy / i.sp.w;
                float2 e = min(uv, 1 - uv);
                half edge = _EdgeFade > 0 ? saturate(min(e.x, e.y) / _EdgeFade) : 1;
                half a = i.color.a * edge;
                return half4(c * a, a);
            }
            ENDCG
        }
    }
    FallBack Off
}
