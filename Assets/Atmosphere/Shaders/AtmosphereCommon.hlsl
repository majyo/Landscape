#ifndef LANDSCAPE_ATMOSPHERE_COMMON_INCLUDED
#define LANDSCAPE_ATMOSPHERE_COMMON_INCLUDED

static const float kAtmosphereTransmittanceDistanceScale = 1.05;

float RaySphereIntersectNearest(float3 origin, float3 direction, float radius)
{
    float b = dot(origin, direction);
    float c = dot(origin, origin) - radius * radius;
    float discriminant = b * b - c;

    if (discriminant < 0.0)
        return -1.0;

    float sqrtDiscriminant = sqrt(discriminant);
    float tMin = -b - sqrtDiscriminant;
    float tMax = -b + sqrtDiscriminant;

    if (tMin > 0.0)
        return tMin;

    if (tMax > 0.0)
        return tMax;

    return -1.0;
}

float RaySphereIntersectFar(float3 origin, float3 direction, float radius)
{
    float b = dot(origin, direction);
    float c = dot(origin, origin) - radius * radius;
    float discriminant = b * b - c;

    if (discriminant < 0.0)
        return -1.0;

    return -b + sqrt(discriminant);
}

float GetAtmosphereHeightKm(float3 positionKm, float groundRadiusKm)
{
    return max(0.0, length(positionKm) - groundRadiusKm);
}

float GetRayleighDensity(float heightKm, float scaleHeightKm)
{
    return exp(-heightKm / scaleHeightKm);
}

float GetMieDensity(float heightKm, float scaleHeightKm)
{
    return exp(-heightKm / scaleHeightKm);
}

float GetOzoneDensity(float heightKm, float centerKm, float halfWidthKm)
{
    return max(0.0, 1.0 - abs(heightKm - centerKm) / halfWidthKm);
}

float2 UvToTransmittanceParameters(float2 uv, float groundRadiusKm, float topRadiusKm)
{
    float maxTangentLengthKm = sqrt(max(topRadiusKm * topRadiusKm - groundRadiusKm * groundRadiusKm, 0.0));
    float tangentLengthKm = uv.y * maxTangentLengthKm;
    float radiusKm = sqrt(max(tangentLengthKm * tangentLengthKm + groundRadiusKm * groundRadiusKm, 0.0));

    float minDistanceKm = topRadiusKm - radiusKm;
    float maxDistanceKm = (tangentLengthKm + maxTangentLengthKm) * kAtmosphereTransmittanceDistanceScale;
    float distanceKm = lerp(minDistanceKm, maxDistanceKm, uv.x);

    float cosTheta = 1.0;
    if (distanceKm > 1e-4)
    {
        float denominator = max(2.0 * radiusKm * distanceKm, 1e-4);
        float unclampedCosTheta = (topRadiusKm * topRadiusKm - radiusKm * radiusKm - distanceKm * distanceKm) / denominator;
        cosTheta = clamp(unclampedCosTheta, -1.0, 1.0);
    }

    return float2(radiusKm, cosTheta);
}

float2 TransmittanceParametersToUv(float radiusKm, float cosTheta, float groundRadiusKm, float topRadiusKm)
{
    float maxTangentLengthKm = sqrt(max(topRadiusKm * topRadiusKm - groundRadiusKm * groundRadiusKm, 0.0));
    float tangentLengthKm = sqrt(max(radiusKm * radiusKm - groundRadiusKm * groundRadiusKm, 0.0));
    float v = maxTangentLengthKm > 0.0 ? tangentLengthKm / maxTangentLengthKm : 0.0;

    float minDistanceKm = topRadiusKm - radiusKm;
    float maxDistanceKm = (tangentLengthKm + maxTangentLengthKm) * kAtmosphereTransmittanceDistanceScale;
    float discriminant = radiusKm * radiusKm * (cosTheta * cosTheta - 1.0) + topRadiusKm * topRadiusKm;
    float distanceKm = max(0.0, -radiusKm * cosTheta + sqrt(max(discriminant, 0.0)));
    float u = (distanceKm - minDistanceKm) / max(maxDistanceKm - minDistanceKm, 1e-4);

    return saturate(float2(u, v));
}

float3 GetExtinction(
    float heightKm,
    float3 rayleighScattering,
    float3 mieScattering,
    float3 mieAbsorption,
    float3 ozoneAbsorption,
    float rayleighScaleHeightKm,
    float mieScaleHeightKm,
    float ozoneLayerCenterKm,
    float ozoneLayerHalfWidthKm)
{
    float rhoR = GetRayleighDensity(heightKm, rayleighScaleHeightKm);
    float rhoM = GetMieDensity(heightKm, mieScaleHeightKm);
    float rhoO = GetOzoneDensity(heightKm, ozoneLayerCenterKm, ozoneLayerHalfWidthKm);

    float3 extinctionRayleigh = rayleighScattering * rhoR;
    float3 extinctionMie = (mieScattering + mieAbsorption) * rhoM;
    float3 extinctionOzone = ozoneAbsorption * rhoO;
    return extinctionRayleigh + extinctionMie + extinctionOzone;
}

#endif
