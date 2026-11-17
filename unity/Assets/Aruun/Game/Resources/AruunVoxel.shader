// Vertex-coloured voxels lit by the sun (with shadows) and the sky. Corner shading (ambient
// occlusion) is baked into the vertex colours by the mesher. Sun, sky and fog come from
// shader globals set by DayNight, so every voxel material follows the time of day.
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

            // Globals from DayNight (sRGB colours) and Bootstrap (fog distances in metres).
            float4 _AruunSunDir;
            float4 _AruunSunColor;
            float4 _AruunAmbient;
            float4 _AruunSkyTop;
            float4 _AruunFogColor;
            float4 _AruunFog; // x start, y end, z strength 0..1
            float _HoleRadius;

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

            float3 Lin(float3 c)
            {
                #ifndef UNITY_COLORSPACE_GAMMA
                return GammaToLinearSpace(c);
                #else
                return c;
                #endif
            }

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
                float3 sky = Lin(_AruunAmbient.rgb) * lerp(0.55, 1.0, up) + Lin(_AruunSkyTop.rgb) * 0.12 * up;
                float3 bounce = float3(0.10, 0.06, 0.04) * (1 - up) * Lin(_AruunSunColor.rgb);
                float3 light = Lin(_AruunSunColor.rgb) * sun * 1.15 + sky + bounce;
                float3 col = Lin(i.color.rgb) * light;
                // Gentle filmic shoulder so bright sunlit faces do not clip.
                col = col / (1 + col * 0.18) * 1.12;

                float dist = distance(i.world, _WorldSpaceCameraPos);
                float fog = saturate((dist - _AruunFog.x) / max(_AruunFog.y - _AruunFog.x, 0.001));
                fog = fog * fog * (3 - 2 * fog) * _AruunFog.z;
                col = lerp(col, Lin(_AruunFogColor.rgb), fog);
                return fixed4(col, i.color.a);
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
