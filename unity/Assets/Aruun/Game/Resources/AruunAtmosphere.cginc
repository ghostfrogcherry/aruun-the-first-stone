// Shared by the voxel and sky shaders, so fogged land and the sky meet without a seam.
#ifndef ARUUN_ATMOSPHERE
#define ARUUN_ATMOSPHERE

float4 _AruunSunDir;
float4 _AruunSunColor;
float4 _AruunSkyHorizon;
float4 _AruunFogColor;
float4 _AruunFog;      // x haze start, y haze end (horizon), z strength 0..1, w ground height (m)
float _AruunPostOn;    // 1 while the post-processing stack tone maps the frame

float3 AruunLin(float3 c)
{
    #ifndef UNITY_COLORSPACE_GAMMA
    return GammaToLinearSpace(c);
    #else
    return c;
    #endif
}

// Light scattered toward the eye by the air: warm toward the sun, sky-coloured elsewhere.
float3 AruunInscatter(float3 viewDir)
{
    float3 s = normalize(_AruunSunDir.xyz + float3(0, 1e-4, 0));
    float c = saturate(dot(viewDir, s));
    float3 sun = AruunLin(_AruunSunColor.rgb) * float3(1.0, 0.82, 0.62);
    float low = saturate(1 - s.y * 1.6);   // stronger when the sun is low
    return AruunLin(_AruunFogColor.rgb) + sun * (pow(c, 8) * (0.35 + 0.55 * low) + pow(c, 2) * 0.06);
}

// How much haze lies between the camera and a point: thicker over longer distances and in
// the low air, so valleys fill with mist and peaks stand clear.
float AruunHaze(float3 worldPos)
{
    float dist = distance(worldPos, _WorldSpaceCameraPos);
    float range = max(_AruunFog.y - _AruunFog.x, 1);
    float d = max(dist - _AruunFog.x, 0) / range;
    float h = (worldPos.y + _WorldSpaceCameraPos.y) * 0.5 - _AruunFog.w;
    float thin = lerp(1.0, 0.45, saturate(h / 120));
    float haze = 1 - exp(-d * 2.6 * thin);
    haze = max(haze, smoothstep(0.82, 1.0, dist / _AruunFog.y)); // always gone by the horizon
    return saturate(haze * _AruunFog.z);
}

#endif
