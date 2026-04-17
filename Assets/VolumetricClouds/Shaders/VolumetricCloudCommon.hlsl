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

float ComputeCloudDensity(float baseShape, float detailShape, float cloudCoverage, float densityMultiplier, float height01)
{
    float baseCoverage = saturate((baseShape - (1.0 - cloudCoverage)) / max(cloudCoverage, 1e-3));
    float detailErode = lerp(1.0, saturate(detailShape * 1.2 - 0.2), 0.35);
    float verticalProfile = GetCloudVerticalProfile(height01);
    return baseCoverage * detailErode * verticalProfile * densityMultiplier;
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
    float lightAbsorption)
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

        float density = ComputeCloudDensity(baseShape, detailShape, cloudCoverage, densityMultiplier, height01);
        opticalDepth += density * shadowStepSizeKm;
    }

    return exp(-lightAbsorption * opticalDepth);
}

#endif
