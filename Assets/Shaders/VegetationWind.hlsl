#ifndef VEGETATION_WIND_INCLUDED
#define VEGETATION_WIND_INCLUDED

// Ветер для деревьев и кустов (HDRP Shader Graph "Nature/VegetationWind").
// Свойства _Veg* объявляет сам граф, здесь они только используются.

// Глобальные параметры от VegetationWind.cs (берутся из WindZone):
// _VegWindDir: xyz - направление, w - сила; _VegWindParams: x - турбулентность, y - сила порывов, z - частота порывов, w - 1 если скрипт работает
float4 _VegWindDir;
float4 _VegWindParams;

float3 VegAbsolutePosition(float3 positionWS)
{
#if defined(SHADERGRAPH_PREVIEW)
    return positionWS;
#else
    return GetAbsolutePositionWS(positionWS);
#endif
}

void VegWind_float(float3 PositionOS, out float3 Out)
{
    float3 posWS = TransformObjectToWorld(PositionOS);
    float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
    float3 absOrigin = VegAbsolutePosition(originWS);
    float3 absPos = VegAbsolutePosition(posWS);

    bool live = _VegWindParams.w > 0.5;
    float strength = live ? _VegWindDir.w : 1.0;
    float turbulence = live ? _VegWindParams.x : 0.3;
    float pulse = live ? _VegWindParams.y : 0.5;
    float pulseFreq = live ? _VegWindParams.z : 0.3;
    float2 dir = live && dot(_VegWindDir.xz, _VegWindDir.xz) > 1e-6 ? normalize(_VegWindDir.xz) : float2(1, 0);

    float t = _TimeParameters.x * _VegSpeed;
    float phase = dot(absOrigin.xz, float2(0.137, 0.173));

    // Насколько вершина далеко от корня: основание стоит, верхушка гнётся сильнее всего
    float rel = max(posWS.y - originWS.y, 0);
    float heightMask = saturate(rel / max(_VegBendHeight, 0.01));
    float bendMask = heightMask * heightMask;

    // Порывы - медленная волна, бегущая по ветру через весь лес
    float travel = dot(absOrigin.xz, dir) * 0.06;
    float gust = 0.5 + 0.5 * sin(_TimeParameters.x * (0.4 + pulseFreq) - travel);
    gust = 1.0 + pulse * (gust * gust * 2.0 - 0.5);

    // Основное раскачивание: наклон по ветру + колебания
    float sway = sin(t + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.25 * (1.0 + turbulence);
    float bend = strength * _VegSway * bendMask * (0.55 * gust + 0.45 * sway);
    float3 windDir = float3(dir.x, 0, dir.y);
    float3 sideDir = float3(-dir.y, 0, dir.x);
    float3 offset = windDir * bend;
    offset += sideDir * strength * _VegSway * bendMask * 0.25 * sin(t * 1.3 + phase * 2.1);
    // Верхушка при наклоне чуть опускается, чтобы ствол не "растягивался"
    offset.y -= abs(bend) * bendMask * 0.15;

    // Дрожание листьев: своя фаза у каждой вершины
    float flutter = sin(t * 6.0 + dot(absPos, float3(3.1, 2.3, 2.7)))
                  + sin(t * 9.7 + dot(absPos, float3(1.7, 3.9, 2.2))) * 0.5;
    offset += float3(0.6, 1.0, 0.5) * flutter * _VegFlutter * strength * gust * (0.5 + turbulence) * heightMask;

    Out = TransformWorldToObject(posWS + offset);
}

// Поверхность в духе HDRP/Lit: base map, normal map, mask map (R - metallic, G - AO, A - smoothness)
void VegSurface_float(float2 UV, out float3 BaseColor, out float3 NormalTS, out float Metallic, out float Smoothness,
    out float Occlusion, out float Alpha, out float Cutoff)
{
    float4 c = SAMPLE_TEXTURE2D(_VegBaseMap, sampler_VegBaseMap, UV) * _VegColor;
    BaseColor = c.rgb;
    Alpha = c.a;
    Cutoff = _VegCutoff;

    NormalTS = float3(0, 0, 1);
    if (_VegHasNormal > 0.5)
        NormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_VegNormalMap, sampler_VegNormalMap, UV), _VegNormalScale);

    Metallic = 0;
    Occlusion = 1;
    Smoothness = _VegSmoothMax;
    if (_VegHasMask > 0.5)
    {
        float4 m = SAMPLE_TEXTURE2D(_VegMaskMap, sampler_VegMaskMap, UV);
        Metallic = m.r;
        Occlusion = m.g;
        Smoothness = lerp(_VegSmoothMin, _VegSmoothMax, m.a);
    }
}

#endif
