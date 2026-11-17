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
            #include "AruunAtmosphere.cginc"

            float4 _AruunSkyTop;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                // The horizon is exactly the colour fully hazed land fades to, so they meet cleanly.
                float3 horizon = AruunInscatter(d);
                float3 top = AruunLin(_AruunSkyTop.rgb);
                float t = pow(saturate(d.y), 0.5);
                float3 col = lerp(horizon, top, t);
                col = lerp(col, horizon * 0.8, saturate(-d.y * 4));   // below the horizon
                float3 s = normalize(_AruunSunDir.xyz + float3(0, 1e-4, 0));
                float c = saturate(dot(d, s));
                float3 sun = AruunLin(_AruunSunColor.rgb);
                // A bright disc (bloom turns it into glare) and a soft halo.
                col += sun * (smoothstep(0.99965, 0.99985, c) * 40 + pow(c, 300) * 1.5 + pow(c, 40) * 0.25);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
