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

float ComputeLegacyDetailErosion(float detailShape)
{
    return lerp(1.0, saturate(detailShape * 1.2 - 0.2), 0.35);
}

float ComputeCloudDensity(float baseShape, float detailShape, float cloudCoverage, float densityMultiplier, float height01)
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

float SampleCloudTypeProfile(
    Texture2D<float4> cloudHeightDensityLut,
    SamplerState cloudHeightDensityLutSampler,
    float cloudType01,
    float height01,
    float cloudTypeRemapMin,
    float cloudTypeRemapMax,
    bool hasHeightDensityLut)
{
    float remapRange = max(cloudTypeRemapMax - cloudTypeRemapMin, 1e-3);
    float normalizedType = saturate((cloudType01 - cloudTypeRemapMin) / remapRange);
    if (hasHeightDensityLut)
    {
        float2 uv = float2(normalizedType, saturate(1.0 - height01));
        return saturate(cloudHeightDensityLut.SampleLevel(cloudHeightDensityLutSampler, uv, 0).r);
    }

    float stratiform = smoothstep(0.0, 0.04, height01) * (1.0 - smoothstep(0.32, 0.58, height01));
    float cumulusCore = GetCloudVerticalProfile(height01);
    float cumulusBulge = pow(saturate(1.0 - abs(height01 - 0.35) * 2.4), 1.5);
    float cumulus = saturate(cumulusCore * (0.75 + 0.25 * cumulusBulge));
    float toweringBase = smoothstep(0.0, 0.04, height01);
    float toweringMid = pow(saturate(1.0 - abs(height01 - 0.55) * 1.4), 1.25);
    float toweringTop = 1.0 - smoothstep(0.92, 1.0, height01);
    float towering = saturate(toweringBase * toweringMid * toweringTop);

    float stratiformToCumulus = saturate(normalizedType * 2.0);
    float toweringBlend = saturate((normalizedType - 0.5) * 2.0);
    float baseProfile = lerp(stratiform, cumulus, stratiformToCumulus);
    return saturate(lerp(baseProfile, towering, toweringBlend));
}

float ComputeMacroCoverage(float weatherCoverage, float globalCoverageGain, float coverageBias, float coverageContrast)
{
    float macroCoverage = saturate(weatherCoverage + coverageBias);
    float contrast = max(coverageContrast, 0.0);
    return saturate((macroCoverage - 0.5) * contrast + 0.5);
}

float ComputeDynamicDetailErosion(float detailNoiseValue, float wetness, float densityBias, float detailErosionStrength)
{
    float wetness01 = saturate(wetness);
    float threshold = lerp(0.38, 0.08, wetness01) - densityBias * 0.08;
    float erodedDetail = saturate((detailNoiseValue - threshold) / max(1.0 - threshold, 1e-3));
    float erosionBlend = saturate(detailErosionStrength * lerp(1.1, 0.35, wetness01));
    return lerp(1.0, erodedDetail, erosionBlend);
}

float ComputeCloudDensityFromWeatherField(
    bool useRuntimeWeatherField,
    Texture2D<float4> weatherFieldTexture,
    SamplerState weatherFieldSampler,
    Texture2D<float4> cloudHeightDensityLut,
    SamplerState cloudHeightDensityLutSampler,
    bool hasHeightDensityLut,
    float3 samplePositionKm,
    float height01,
    float baseShape,
    float detailShape,
    float cloudCoverage,
    float densityMultiplier,
    float weatherFieldScaleKm,
    float2 weatherFieldOffsetKm,
    float globalCoverageGain,
    float coverageBias,
    float coverageContrast,
    float cloudTypeRemapMin,
    float cloudTypeRemapMax,
    float detailErosionStrength)
{
    if (!useRuntimeWeatherField)
        return ComputeCloudDensity(baseShape, detailShape, cloudCoverage, densityMultiplier, height01);

    float4 weather = SampleRuntimeWeatherField(
        weatherFieldTexture,
        weatherFieldSampler,
        samplePositionKm,
        weatherFieldScaleKm,
        weatherFieldOffsetKm);
    float macroCoverage = ComputeMacroCoverage(weather.r, globalCoverageGain, coverageBias, coverageContrast);
    float softenedCoverage = sqrt(max(macroCoverage, 1e-4));
    float coverageFade = smoothstep(0.03, 0.18, macroCoverage);
    float densityBias = weather.a * lerp(0.08, 0.22, softenedCoverage);
    float threshold = lerp(0.82, 0.28, softenedCoverage);
    float baseCoverage = saturate((baseShape + densityBias - threshold) / max(1.0 - threshold, 1e-3));
    float effectiveCloudType = lerp(min(weather.g, 0.45), weather.g, smoothstep(0.28, 0.68, macroCoverage));
    float typeProfile = SampleCloudTypeProfile(
        cloudHeightDensityLut,
        cloudHeightDensityLutSampler,
        effectiveCloudType,
        height01,
        cloudTypeRemapMin,
        cloudTypeRemapMax,
        hasHeightDensityLut);
    float erosionStrength = saturate(detailErosionStrength * lerp(1.15, 0.9, softenedCoverage));
    float detailErode = ComputeDynamicDetailErosion(
        detailShape,
        weather.b * coverageFade,
        weather.a * coverageFade,
        erosionStrength);
    return baseCoverage * typeProfile * detailErode * densityMultiplier * coverageFade;
}

float MarchCloudShadow(
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
    float shapeBaseScaleKm,
    float detailScaleKm,
    float lightAbsorption,
    bool useRuntimeWeatherField,
    Texture2D<float4> weatherFieldTexture,
    SamplerState weatherFieldSampler,
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
        float baseShape = baseNoise.SampleLevel(baseNoiseSampler, frac(baseNoiseUv), 0).r;

        float detailShape = 1.0;
        if (hasDetailNoise)
        {
            float3 detailNoiseUv = marchPositionKm / detailScaleKm + windOffsetKm * 1.7;
            detailShape = detailNoise.SampleLevel(detailNoiseSampler, frac(detailNoiseUv), 0).r;
        }

        float density = ComputeCloudDensityFromWeatherField(
            useRuntimeWeatherField,
            weatherFieldTexture,
            weatherFieldSampler,
            cloudHeightDensityLut,
            cloudHeightDensityLutSampler,
            hasHeightDensityLut,
            marchPositionKm,
            height01,
            baseShape,
            detailShape,
            cloudCoverage,
            densityMultiplier,
            weatherFieldScaleKm,
            weatherFieldOffsetKm,
            globalCoverageGain,
            coverageBias,
            coverageContrast,
            cloudTypeRemapMin,
            cloudTypeRemapMax,
            detailErosionStrength);
        opticalDepth += density * shadowStepSizeKm;
    }

    return exp(-lightAbsorption * opticalDepth);
}

#endif
