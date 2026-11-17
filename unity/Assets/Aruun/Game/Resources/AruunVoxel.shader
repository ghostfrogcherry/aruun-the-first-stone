// Flat-shaded, vertex-coloured voxels with one sun and sky light. One unlit-style pass that
// does its own simple lighting, so it draws the same in the Built-in pipeline and in URP.
// A proper URP lit shader with shadows replaces this once the pipeline is installed.
Shader "Aruun/Voxel"
{
    Properties
    {
        _SunDir ("Sun direction", Vector) = (0.4, 0.8, 0.3, 0)
        _Sun ("Sun colour", Color) = (1.0, 0.96, 0.88, 1)
        _Sky ("Sky light", Color) = (0.42, 0.50, 0.60, 1)
        [Enum(Off,0,On,1)] _ZWrite ("Write depth", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _SunDir;
            fixed4 _Sun;
            fixed4 _Sky;
            // Set globally by the game (Shader.SetGlobal*): distance fog in metres.
            float4 _AruunFogColor;
            float4 _AruunFog; // x start, y end, z strength 0..1

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
                float fog : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float sun = saturate(dot(n, normalize(_SunDir.xyz)));
                // Sides facing away from the sun still get sky light, a little more from above.
                float3 light = _Sun.rgb * sun + _Sky.rgb * (0.75 + 0.25 * n.y);
                float3 albedo = v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                albedo = GammaToLinearSpace(albedo); // palette colours are sRGB
                #endif
                o.color = fixed4(albedo * light, v.color.a);
                float dist = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _WorldSpaceCameraPos);
                o.fog = saturate((dist - _AruunFog.x) / max(_AruunFog.y - _AruunFog.x, 0.001)) * _AruunFog.z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 fogColor = _AruunFogColor.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                fogColor = GammaToLinearSpace(fogColor);
                #endif
                return fixed4(lerp(i.color.rgb, fogColor, i.fog), i.color.a);
            }
            ENDCG
        }
    }
}
