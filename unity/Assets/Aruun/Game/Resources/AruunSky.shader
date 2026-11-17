// The sky: a gradient from the horizon colour to the zenith colour, a sun disc with a soft
// glow, and a darker band below the horizon. Colours come from DayNight's globals.
Shader "Aruun/Sky"
{
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _AruunSunDir;
            float4 _AruunSunColor;
            float4 _AruunSkyTop;
            float4 _AruunSkyHorizon;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float3 Lin(float3 c)
            {
                #ifndef UNITY_COLORSPACE_GAMMA
                return GammaToLinearSpace(c);
                #else
                return c;
                #endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 top = Lin(_AruunSkyTop.rgb), horizon = Lin(_AruunSkyHorizon.rgb);
                float t = pow(saturate(d.y), 0.45);
                float3 col = lerp(horizon, top, t);
                col = lerp(col, horizon * 0.75, saturate(-d.y * 4)); // below the horizon
                float3 s = normalize(_AruunSunDir.xyz + float3(0, 1e-4, 0));
                float c = saturate(dot(d, s));
                float3 sun = Lin(_AruunSunColor.rgb);
                col += sun * (pow(c, 1200) * 18 + pow(c, 60) * 0.35 + pow(c, 6) * 0.08 * saturate(1 - s.y * 2));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
