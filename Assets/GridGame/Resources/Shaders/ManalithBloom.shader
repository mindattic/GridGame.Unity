// GG-T679/GG-T680: bloom for the Manalith rig's HDR render (threshold 1.0, docs/MANALITH_VISUAL_SPEC.md). The built-in
// pipeline has no post-processing stack here, so: prefilter, box down/up chain, then combine. The combine turns the
// rig's premultiplied colour into straight alpha (bloom over empty sky becomes its own coverage) for UI Toolkit.
Shader "Hidden/GridGame/ManalithBloom"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex;
    float4 _MainTex_TexelSize;
    half _Threshold, _Intensity;
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
    half3 Box (float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
              + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    half4 Prefilter (v2f i) : SV_Target
    {
        half3 c = Box(i.uv, 1);
        half b = max(c.r, max(c.g, c.b));
        return half4(c * (max(0, b - _Threshold) / max(b, 1e-4)), 1);
    }
    half4 Down (v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
    half4 Up (v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
    half4 Combine (v2f i) : SV_Target
    {
        half4 src = tex2D(_MainTex, i.uv);
        half3 bloom = tex2D(_BloomTex, i.uv).rgb * _Intensity;
        half3 pm = src.rgb + bloom;
        half a = saturate(max(src.a, max(bloom.r, max(bloom.g, bloom.b))));
        return half4(a > 1e-3 ? pm / a : 0, a);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment Prefilter
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment Down
               ENDCG }
        Pass { Blend One One
               CGPROGRAM
               #pragma vertex vert
               #pragma fragment Up
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment Combine
               ENDCG }
    }
}
