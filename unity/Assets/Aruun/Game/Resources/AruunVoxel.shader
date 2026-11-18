// Vertex-coloured voxels lit by the sun (with shadows) and the sky. Corner shading (ambient
// occlusion) is baked into the vertex colours by the mesher. Sun, sky and fog come from
// shader globals set by DayNight, so every voxel material follows the time of day.
// Lamps: WorldView hands over the nearest lamps (_AruunLamps) and this pass adds their warm
// light per pixel. Faces with vertex alpha 0 in the opaque pass are lamps themselves and glow.
Shader "Aruun/Voxel"
{
    Properties
    {
        [Enum(Off,0,On,1)] _ZWrite ("Write depth", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _HoleRadius ("Hole radius (far terrain)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "AruunAtmosphere.cginc"

            // Globals from DayNight (sRGB colours); sun, fog and haze are in AruunAtmosphere.
            float4 _AruunAmbient;
            float4 _AruunSkyTop;
            float _HoleRadius;
            // Up to 16 lamps near the camera: xyz in metres, w = reach in metres. Colour from DayNight (a = strength).
            float4 _AruunLamps[16];
            float _AruunLampCount;
            float4 _AruunLampColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                SHADOW_COORDS(2)
            };

            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // The far terrain leaves a hole where the real voxel chunks are.
                if (_HoleRadius > 0 && distance(i.world.xz, _WorldSpaceCameraPos.xz) < _HoleRadius) discard;
                float3 n = normalize(i.normal);
                float3 sunDir = normalize(_AruunSunDir.xyz + float3(0, 1e-4, 0));
                float shadow = SHADOW_ATTENUATION(i);
                float sun = saturate(dot(n, sunDir)) * shadow;
                // Sky light: brightest from straight above, blue-tinted from the sky colour,
                // and a little warm light bounced up from the red ground.
                float up = n.y * 0.5 + 0.5;
                float3 sky = AruunLin(_AruunAmbient.rgb) * lerp(0.7, 1.0, up) + AruunLin(_AruunSkyTop.rgb) * 0.12 * up;
                float3 bounce = float3(0.10, 0.06, 0.04) * (1 - up) * AruunLin(_AruunSunColor.rgb);
                float3 light = AruunLin(_AruunSunColor.rgb) * sun * 1.15 + sky + bounce;

                // Warm lamp light: smooth falloff to zero at the lamp's reach, softly wrapped so
                // faces turned a little away still catch some.
                float lamp = 0;
                int lamps = (int)_AruunLampCount;
                for (int k = 0; k < 16; k++)
                {
                    if (k >= lamps) break;
                    float3 d = _AruunLamps[k].xyz - i.world;
                    float dist2 = dot(d, d);
                    float r = _AruunLamps[k].w;
                    float f = saturate(1 - dist2 / (r * r));
                    float wrap = saturate(dot(n, d * rsqrt(dist2 + 1e-4)) * 0.7 + 0.3);
                    lamp += f * f * wrap / (1 + dist2 * 0.15);
                }
                light += AruunLin(_AruunLampColor.rgb) * _AruunLampColor.a * lamp;

                float3 col = AruunLin(i.color.rgb) * light;
                // A lamp face: lit from within, bright enough at night for the bloom to catch it.
                float glows = 1 - step(0.004, i.color.a);
                col = lerp(col, AruunLin(i.color.rgb) * (0.9 + _AruunLampColor.a * 0.55), glows);
                // Without the post stack, a gentle filmic shoulder so sunlit faces do not clip.
                col = lerp(col / (1 + col * 0.18) * 1.12, col, _AruunPostOn);

                float3 view = normalize(i.world - _WorldSpaceCameraPos);
                col = lerp(col, AruunInscatter(view), AruunHaze(i.world));
                return fixed4(col, max(i.color.a, glows));
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
