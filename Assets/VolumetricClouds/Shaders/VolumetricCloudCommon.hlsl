#ifndef VOLUMETRIC_CLOUD_COMMON_INCLUDED
#define VOLUMETRIC_CLOUD_COMMON_INCLUDED

#include "../../Atmosphere/Shaders/AtmosphereCommon.hlsl"

bool RaySphereIntersectInterval(float3 rayOrigin, float3 rayDirection, float sphereRadius, out float tNear, out float tFar)
{
    float b = dot(rayOrigin, rayDirection);
    float c = dot(rayOrigin, rayOrigin) - sphereRadius * sphereRadius;
    float discriminant = b * b - c;
    if (discriminant < 0.0)
    {
        tNear = -1.0;
        tFar = -1.0;
        return false;
    }

    float sqrtDiscriminant = sqrt(discriminant);
    tNear = -b - sqrtDiscriminant;
    tFar = -b + sqrtDiscriminant;
    return tFar > 0.0;
}

float3 GetCloudViewDirection(float2 uv, float3 cameraRight, float3 cameraUp, float3 cameraForward, float tanHalfVerticalFov, float aspectRatio)
{
    float2 ndc = uv * 2.0 - 1.0;
    float3 direction = cameraForward
        + cameraRight * (ndc.x * tanHalfVerticalFov * aspectRatio)
        + cameraUp * (ndc.y * tanHalfVerticalFov);
    return normalize(direction);
}

float GetCloudHeight01(float3 samplePositionKm, float groundRadiusKm, float cloudBottomHeightKm, float cloudThicknessKm)
{
    float radiusKm = length(samplePositionKm);
    float heightKm = radiusKm - groundRadiusKm;
    return saturate((heightKm - cloudBottomHeightKm) / max(cloudThicknessKm, 1e-4));
}

float GetCloudVerticalProfile(float height01)
{
    return smoothstep(0.0, 0.15, height01) * (1.0 - smoothstep(0.7, 1.0, height01));
}

float SampleMacroBaseShape(float4 baseNoiseSample)
{
    // The shipped Worley atlas is already authored as "bright = occupied cloud volume".
    // Using 1 - sample inverts the field into wispy cell borders, which is why coverage
    // can look weak even when driven to 1.0.
    float macroLow = baseNoiseSample.r;
    float macroSupport = dot(baseNoiseSample.gba, float3(0.625, 0.25, 0.125));
    return saturate(lerp(macroLow, macroLow * (0.65 + 0.35 * macroSupport), 0.6));
}

float ComputeMacroCoverageThreshold(float coverage01)
{
    // Keep the zero-coverage case above 1 so coverage=0 fully clears the layer.
    return lerp(1.02, 0.28, sqrt(saturate(coverage01)));
}

float ComputeBaseVolumeDensity(float macroShape, float threshold, float densityBias)
{
    return saturate((macroShape + densityBias - threshold) / max(1.0 - threshold, 1e-3));
}

float ComputeLegacyDetailErosion(float detailShape)
{
    return lerp(1.0, saturate(detailShape * 1.2 - 0.2), 0.35);
}

float ComputeLegacyCloudDensity(float baseShape, float detailShape, float cloudCoverage, float densityMultiplier, float height01)
{
    float baseCoverage = saturate((baseShape - (1.0 - cloudCoverage)) / max(cloudCoverage, 1e-3));
    float detailErode = ComputeLegacyDetailErosion(detailShape);
    float verticalProfile = GetCloudVerticalProfile(height01);
    return baseCoverage * detailErode * verticalProfile * densityMultiplier;
}

float4 SampleRuntimeWeatherField(
    Texture2D<float4> weatherFieldTexture,
    SamplerState weatherFieldSampler,
    float3 samplePositionKm,
    float weatherFieldScaleKm,
    float2 weatherFieldOffsetKm)
{
    float safeScaleKm = max(weatherFieldScaleKm, 1e-3);
    float2 uv = frac((samplePositionKm.xz - weatherFieldOffsetKm) / safeScaleKm);
    return saturate(weatherFieldTexture.SampleLevel(weatherFieldSampler, uv, 0));
}

float2 SampleCurlDistortion(
    Texture2D<float4> curlNoise,
    SamplerState curlNoiseSampler,
    bool hasCurlNoise,
    float3 samplePositionKm,
    float2 windOffsetKm,
    float curlNoiseScaleKm,
    float curlNoiseStrengthKm)
{
    if (!hasCurlNoise || curlNoiseStrengthKm <= 1e-4)
        return 0.0;

    float safeScaleKm = max(curlNoiseScaleKm, 1e-3);
    float2 uv = frac((samplePositionKm.xz + windOffsetKm * 0.35) / safeScaleKm);
    float2 curl = curlNoise.SampleLevel(curlNoiseSampler, uv, 0).rg * 2.0 - 1.0;
    return curl * curlNoiseStrengthKm;
}

float SampleDetailShape(
    Texture3D detailNoise,
    SamplerState detailNoiseSampler,
    bool hasDetailNoise,
    Texture2D<float4> curlNoise,
    SamplerState curlNoiseSampler,
    bool hasCurlNoise,
    float3 samplePositionKm,
    float2 windOffsetKm,
    float detailScaleKm,
    float curlNoiseScaleKm,
    float curlNoiseStrengthKm)
{
    if (!hasDetailNoise)
        return 1.0;

    float2 curlOffsetKm = SampleCurlDistortion(
        curlNoise,
        curlNoiseSampler,
        hasCurlNoise,
        samplePositionKm,
        windOffsetKm,
        curlNoiseScaleKm,
        curlNoiseStrengthKm);
    float3 detailNoiseUv = samplePositionKm / max(detailScaleKm, 1e-3);
    detailNoiseUv += float3(windOffsetKm.x * 1.7 + curlOffsetKm.x, 0.0, windOffsetKm.y * 1.7 + curlOffsetKm.y);
    return detailNoise.SampleLevel(detailNoiseSampler, frac(detailNoiseUv), 0).r;
}

float NormalizeCloudType(float cloudType01, float cloudTypeRemapMin, float cloudTypeRemapMax)
{
    float remapRange = max(cloudTypeRemapMax - cloudTypeRemapMin, 1e-3);
    return saturate((cloudType01 - cloudTypeRemapMin) / remapRange);
}

float SampleCloudTypeDensityProfile(
    Texture2D<float4> cloudHeightDensityLut,
    SamplerState cloudHeightDensityLutSampler,
    float normalizedType,
    float height01,
    bool hasHeightDensityLut)
{
    if (hasHeightDensityLut)
    {
        float2 uv = float2(normalizedType, saturate(1.0 - height01));
        return saturate(cloudHeightDensityLut.SampleLevel(cloudHeightDensityLutSampler, uv, 0).r);
    }

    float stratiform = smoothstep(0.0, 0.05, height01) * (1.0 - smoothstep(0.24, 0.42, height01));
    float cumulusCore = GetCloudVerticalProfile(height01);
    float cumulusBulge = pow(saturate(1.0 - abs(height01 - 0.46) / 0.28), 1.35);
    float cumulus = saturate(cumulusCore * (0.72 + 0.28 * cumulusBulge));
    float toweringBase = smoothstep(0.0, 0.05, height01);
    float toweringBody = pow(saturate(1.0 - abs(height01 - 0.64) / 0.34), 1.1);
    float toweringTop = 1.0 - smoothstep(0.95, 1.0, height01);
    float towering = saturate(toweringBase * toweringBody * toweringTop);

    float stratiformToCumulus = saturate(normalizedType * 2.0);
    float toweringBlend = saturate((normalizedType - 0.5) * 2.0);
    float baseProfile = lerp(stratiform, cumulus, stratiformToCumulus);
    return saturate(lerp(baseProfile, towering, toweringBlend));
}

float ApplyCloudTypeMacroSculpt(float macroShape, float normalizedType, float height01, float macroCoverage)
{
    float stratusBlend = 1.0 - smoothstep(0.22, 0.42, normalizedType);
    float cumulusBlend = saturate(1.0 - abs(normalizedType - 0.50) / 0.28);
    float toweringBlend = smoothstep(0.62, 0.82, normalizedType);

    float bottomMask = 1.0 - smoothstep(0.12, 0.30, height01);
    float midMask = pow(saturate(1.0 - abs(height01 - 0.45) / 0.32), 1.35);
    float topMask = smoothstep(0.52, 0.82, height01) * (1.0 - smoothstep(0.96, 1.0, height01));
    float topCapMask = smoothstep(0.86, 0.97, height01);
    float coverageSupport = smoothstep(0.25, 0.65, macroCoverage);

    float typedMacroShape = macroShape;
    typedMacroShape += stratusBlend * (0.10 * bottomMask - 0.12 * topMask);
    typedMacroShape += cumulusBlend * (0.12 * midMask + 0.06 * topMask);
    typedMacroShape += toweringBlend * coverageSupport * (0.10 * midMask + 0.18 * topMask - 0.08 * topCapMask);
    return saturate(typedMacroShape);
}

float ComputeMacroCoverage(float weatherCoverage, float globalCoverageGain, float coverageBias, float coverageContrast)
{
    float macroCoverage = saturate(weatherCoverage * max(globalCoverageGain, 0.0) + coverageBias);
    float contrast = max(coverageContrast, 0.0);
    return saturate((macroCoverage - 0.5) * contrast + 0.5);
}

float ComputeDynamicDetailErosion(
    float detailNoiseValue,
    float baseVolume,
    float height01,
    float wetness,
    float densityBias,
    float detailErosionStrength)
{
    float wetness01 = saturate(wetness);
    float edgeMask = pow(saturate(baseVolume * (1.0 - baseVolume) * 4.0), 0.85);
    float bottomSoftness = 1.0 - smoothstep(0.08, 0.24, height01);
    float shapedDetail = lerp(1.0 - detailNoiseValue, detailNoiseValue, 1.0 - bottomSoftness);
    float threshold = lerp(0.44, 0.18, wetness01) - densityBias * 0.04;
    float erodedDetail = saturate((shapedDetail - threshold) / max(1.0 - threshold, 1e-3));
    float erosionBlend = saturate(detailErosionStrength * edgeMask * lerp(1.0, 0.45, wetness01));
    return lerp(1.0, erodedDetail, erosionBlend);
}

bool UseLegacyCloudDensityPath(int debugMode)
{
    return debugMode == 10;
}

void ResolveCloudShapeState(
    bool useRuntimeWeatherField,
    Texture2D<float4> weatherFieldTexture,
    SamplerState weatherFieldSampler,
    float3 samplePositionKm,
    float weatherFieldScaleKm,
    float2 weatherFieldOffsetKm,
    float cloudCoverage,
    float4 fallbackWeatherStateData,
    float globalCoverageGain,
    float coverageBias,
    float coverageContrast,
    out float macroCoverage,
    out float effectiveCloudType,
    out float wetness,
    out float densityBias,
    out float coverageFade)
{
    if (!useRuntimeWeatherField)
    {
        float fallbackCoverage = saturate(cloudCoverage);
        float fallbackCoverageBias = coverageBias * fallbackCoverage;
        float fallbackCoverageContrast = lerp(1.0, coverageContrast, fallbackCoverage);
        macroCoverage = ComputeMacroCoverage(fallbackCoverage, 1.0, fallbackCoverageBias, fallbackCoverageContrast);
        float softenedCoverage = sqrt(max(macroCoverage, 1e-4));
        effectiveCloudType = saturate(fallbackWeatherStateData.x);
        wetness = saturate(fallbackWeatherStateData.y);
        densityBias = saturate(fallbackWeatherStateData.z) * lerp(0.06, 0.18, softenedCoverage);
        coverageFade = 1.0;
        return;
    }

    float4 weather = SampleRuntimeWeatherField(
        weatherFieldTexture,
        weatherFieldSampler,
        samplePositionKm,
        weatherFieldScaleKm,
        weatherFieldOffsetKm);
    macroCoverage = ComputeMacroCoverage(weather.r, globalCoverageGain, coverageBias, coverageContrast);
    float softenedCoverage = sqrt(max(macroCoverage, 1e-4));
    coverageFade = smoothstep(0.03, 0.18, macroCoverage);
    effectiveCloudType = lerp(min(weather.g, 0.45), weather.g, smoothstep(0.28, 0.68, macroCoverage));
    wetness = saturate(weather.b * coverageFade);
    densityBias = saturate(weather.a) * lerp(0.08, 0.22, softenedCoverage);
}

float ComputeLayeredCloudDensity(
    bool useRuntimeWeatherField,
    Texture2D<float4> weatherFieldTexture,
    SamplerState weatherFieldSampler,
    Texture2D<float4> cloudHeightDensityLut,
    SamplerState cloudHeightDensityLutSampler,
    bool hasHeightDensityLut,
    float3 samplePositionKm,
    float height01,
    float macroShape,
    float detailShape,
    float cloudCoverage,
    float densityMultiplier,
    float4 fallbackWeatherStateData,
    Texture2D<float4> curlNoise,
    SamplerState curlNoiseSampler,
    bool hasCurlNoise,
    float curlNoiseScaleKm,
    float curlNoiseStrengthKm,
    float weatherFieldScaleKm,
    float2 weatherFieldOffsetKm,
    float globalCoverageGain,
    float coverageBias,
    float coverageContrast,
    float cloudTypeRemapMin,
    float cloudTypeRemapMax,
    float detailErosionStrength,
    out float typeProfilePreview,
    out float typedMacroShapePreview)
{
    float macroCoverage;
    float effectiveCloudType;
    float wetness;
    float densityBias;
    float coverageFade;
    ResolveCloudShapeState(
        useRuntimeWeatherField,
        weatherFieldTexture,
        weatherFieldSampler,
        samplePositionKm,
        weatherFieldScaleKm,
        weatherFieldOffsetKm,
        cloudCoverage,
        fallbackWeatherStateData,
        globalCoverageGain,
        coverageBias,
        coverageContrast,
        macroCoverage,
        effectiveCloudType,
        wetness,
        densityBias,
        coverageFade);

    float normalizedCloudType = NormalizeCloudType(effectiveCloudType, cloudTypeRemapMin, cloudTypeRemapMax);
    float typedMacroShape = ApplyCloudTypeMacroSculpt(macroShape, normalizedCloudType, height01, macroCoverage);
    float threshold = ComputeMacroCoverageThreshold(macroCoverage);
    float baseVolume = ComputeBaseVolumeDensity(typedMacroShape, threshold, densityBias);
    float typeProfile = SampleCloudTypeDensityProfile(
        cloudHeightDensityLut,
        cloudHeightDensityLutSampler,
        normalizedCloudType,
        height01,
        hasHeightDensityLut);
    float softenedCoverage = sqrt(max(macroCoverage, 1e-4));
    float erosionStrength = useRuntimeWeatherField
        ? saturate(detailErosionStrength * lerp(1.15, 0.9, softenedCoverage))
        : saturate(detailErosionStrength);
    float detailErode = ComputeDynamicDetailErosion(detailShape, baseVolume, height01, wetness, densityBias, erosionStrength);
    typeProfilePreview = typeProfile;
    typedMacroShapePreview = typedMacroShape;
    return baseVolume * typeProfile * detailErode * densityMultiplier * coverageFade;
}

float ComputeCloudDensityFromWeatherField(
    int debugMode,
    bool useRuntimeWeatherField,
    Texture2D<float4> weatherFieldTexture,
    SamplerState weatherFieldSampler,
    Texture2D<float4> cloudHeightDensityLut,
    SamplerState cloudHeightDensityLutSampler,
    bool hasHeightDensityLut,
    float3 samplePositionKm,
    float height01,
    float4 baseShapeSample,
    float detailShape,
    float cloudCoverage,
    float densityMultiplier,
    float4 fallbackWeatherStateData,
    Texture2D<float4> curlNoise,
    SamplerState curlNoiseSampler,
    bool hasCurlNoise,
    float curlNoiseScaleKm,
    float curlNoiseStrengthKm,
    float weatherFieldScaleKm,
    float2 weatherFieldOffsetKm,
    float globalCoverageGain,
    float coverageBias,
    float coverageContrast,
    float cloudTypeRemapMin,
    float cloudTypeRemapMax,
    float detailErosionStrength,
    out float typeProfilePreview,
    out float typedMacroShapePreview)
{
    if (UseLegacyCloudDensityPath(debugMode))
    {
        typeProfilePreview = GetCloudVerticalProfile(height01);
        typedMacroShapePreview = baseShapeSample.r;
        return ComputeLegacyCloudDensity(baseShapeSample.r, detailShape, cloudCoverage, densityMultiplier, height01);
    }

    return ComputeLayeredCloudDensity(
        useRuntimeWeatherField,
        weatherFieldTexture,
        weatherFieldSampler,
        cloudHeightDensityLut,
        cloudHeightDensityLutSampler,
        hasHeightDensityLut,
        samplePositionKm,
        height01,
        SampleMacroBaseShape(baseShapeSample),
        detailShape,
        cloudCoverage,
        densityMultiplier,
        fallbackWeatherStateData,
        curlNoise,
        curlNoiseSampler,
        hasCurlNoise,
        curlNoiseScaleKm,
        curlNoiseStrengthKm,
        weatherFieldScaleKm,
        weatherFieldOffsetKm,
        globalCoverageGain,
        coverageBias,
        coverageContrast,
        cloudTypeRemapMin,
        cloudTypeRemapMax,
        detailErosionStrength,
        typeProfilePreview,
        typedMacroShapePreview);
}

float MarchCloudShadow(
    int debugMode,
    float3 samplePositionKm,
    float3 sunDirection,
    int shadowStepCount,
    float shadowStepSizeKm,
    float cloudBottomRadiusKm,
    float cloudTopRadiusKm,
    float groundRadiusKm,
    float cloudCoverage,
    float densityMultiplier,
    float3 windOffsetKm,
    Texture3D baseNoise,
    SamplerState baseNoiseSampler,
    Texture3D detailNoise,
    SamplerState detailNoiseSampler,
    bool hasDetailNoise,
    Texture2D<float4> curlNoise,
    SamplerState curlNoiseSampler,
    bool hasCurlNoise,
    float shapeBaseScaleKm,
    float detailScaleKm,
    float curlNoiseScaleKm,
    float curlNoiseStrengthKm,
    float lightAbsorption,
    bool useRuntimeWeatherField,
    Texture2D<float4> weatherFieldTexture,
    SamplerState weatherFieldSampler,
    float4 fallbackWeatherStateData,
    float weatherFieldScaleKm,
    float2 weatherFieldOffsetKm,
    float globalCoverageGain,
    float coverageBias,
    float coverageContrast,
    Texture2D<float4> cloudHeightDensityLut,
    SamplerState cloudHeightDensityLutSampler,
    bool hasHeightDensityLut,
    float cloudTypeRemapMin,
    float cloudTypeRemapMax,
    float detailErosionStrength)
{
    float opticalDepth = 0.0;

    [loop]
    for (int stepIndex = 0; stepIndex < shadowStepCount; ++stepIndex)
    {
        float t = ((float)stepIndex + 0.5) * shadowStepSizeKm;
        float3 marchPositionKm = samplePositionKm + sunDirection * t;
        float radiusKm = length(marchPositionKm);
        if (radiusKm < cloudBottomRadiusKm || radiusKm > cloudTopRadiusKm)
            break;

        float height01 = GetCloudHeight01(marchPositionKm, groundRadiusKm, cloudBottomRadiusKm - groundRadiusKm, cloudTopRadiusKm - cloudBottomRadiusKm);
        float3 baseNoiseUv = marchPositionKm / shapeBaseScaleKm + windOffsetKm;
        float4 baseShapeSample = baseNoise.SampleLevel(baseNoiseSampler, frac(baseNoiseUv), 0);

        float detailShape = SampleDetailShape(
            detailNoise,
            detailNoiseSampler,
            hasDetailNoise,
            curlNoise,
            curlNoiseSampler,
            hasCurlNoise,
            marchPositionKm,
            windOffsetKm.xz,
            detailScaleKm,
            curlNoiseScaleKm,
            curlNoiseStrengthKm);

        float typeProfile = 0.0;
        float typedMacroShape = 0.0;
        float density = ComputeCloudDensityFromWeatherField(
            debugMode,
            useRuntimeWeatherField,
            weatherFieldTexture,
            weatherFieldSampler,
            cloudHeightDensityLut,
            cloudHeightDensityLutSampler,
            hasHeightDensityLut,
            marchPositionKm,
            height01,
            baseShapeSample,
            detailShape,
            cloudCoverage,
            densityMultiplier,
            fallbackWeatherStateData,
            curlNoise,
            curlNoiseSampler,
            hasCurlNoise,
            curlNoiseScaleKm,
            curlNoiseStrengthKm,
            weatherFieldScaleKm,
            weatherFieldOffsetKm,
            globalCoverageGain,
            coverageBias,
            coverageContrast,
            cloudTypeRemapMin,
            cloudTypeRemapMax,
            detailErosionStrength,
            typeProfile,
            typedMacroShape);
        opticalDepth += density * shadowStepSizeKm;
    }

    return exp(-lightAbsorption * opticalDepth);
}

#endif
