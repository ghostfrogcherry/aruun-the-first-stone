// Aruun's post-processing for the Built-in pipeline: bloom, sun shafts, ACES tone mapping,
// colour grading and a soft vignette. Driven by PostFX.cs. Works on the HDR frame.
Shader "Hidden/Aruun/Post"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _ShaftTex;
    sampler2D _CameraDepthTexture;
    float4 _Threshold;      // x threshold, y knee, z clamp
    float _BloomIntensity;
    float4 _SunScreen;      // xy sun position in uv, z strength, w unused
    float4 _ShaftColor;
    float _Exposure;
    float _Saturation;
    float _Contrast;
    float4 _Lift, _Gain;
    float _Vignette;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half3 Box(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }

    // 0: keep only the bright parts, with a soft knee.
    half4 Prefilter(v2f i) : SV_Target
    {
        half3 c = min(Box(i.uv, 1), _Threshold.z);
        half b = max(c.r, max(c.g, c.b));
        half soft = clamp(b - _Threshold.x + _Threshold.y, 0, 2 * _Threshold.y);
        soft = soft * soft / (4 * _Threshold.y + 1e-4);
        half contrib = max(soft, b - _Threshold.x) / max(b, 1e-4);
        return half4(c * contrib, 1);
    }
    // 1: down, 2: up (added onto the bigger level by the blend state).
    half4 Down(v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
    half4 Up(v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }

    // 3: where the sky shows, near the sun: the source of the shafts.
    half4 ShaftMask(v2f i) : SV_Target
    {
        float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
        half sky = depth > 0.999 ? 1 : 0;
        float2 d = (i.uv - _SunScreen.xy) * float2(_ScreenParams.x / _ScreenParams.y, 1);
        half near = saturate(1 - length(d) * 1.6);
        return half4(sky * near * near, 0, 0, 1);
    }
    // 4: smear the mask toward the sun.
    half4 ShaftBlur(v2f i) : SV_Target
    {
        float2 step = (_SunScreen.xy - i.uv) / 16 * 0.9;
        float2 uv = i.uv;
        half sum = 0, w = 1;
        for (int k = 0; k < 16; k++) { sum += tex2D(_MainTex, uv).r * w; w *= 0.94; uv += step; }
        return half4(sum / 10, 0, 0, 1);
    }

    half3 ACES(half3 x)
    {
        const half a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    // 5: put it together.
    half4 Composite(v2f i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c += tex2D(_ShaftTex, i.uv).r * _ShaftColor.rgb * _SunScreen.z;
        c *= _Exposure;
        c = ACES(c);
        // Grading in display-ish space: lift the shadows toward cool blue, warm the highlights.
        half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(luma.xxx, c, _Saturation);
        c = saturate((c - 0.5) * _Contrast + 0.5);
        c = c * _Gain.rgb + _Lift.rgb * (1 - c);
        float2 v = (i.uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1);
        c *= 1 - _Vignette * dot(v, v);
        // A touch of noise breaks up banding in dark gradients (dusk sky, haze).
        float n = frac(sin(dot(i.uv * _ScreenParams.xy, float2(12.9898, 78.233))) * 43758.5453);
        c += (n - 0.5) / 255.0;
        return half4(c, 1);
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
               #pragma fragment ShaftMask
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment ShaftBlur
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment Composite
               ENDCG }
    }
}
