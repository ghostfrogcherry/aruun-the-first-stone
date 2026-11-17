// Glowing voxels (magick, lamps, embers): vertex colour, unlit and brightened, with the same
// distance fog as the terrain. A bloom pass can pick these up later.
Shader "Aruun/VoxelGlow"
{
    Properties
    {
        _Glow ("Brightness", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Glow;
            float4 _AruunFogColor;
            float4 _AruunFog;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float fog : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 c = v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
                #endif
                o.color = fixed4(c * _Glow, 1);
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
                return fixed4(lerp(i.color.rgb, fogColor, i.fog), 1);
            }
            ENDCG
        }
    }
}
