#ifndef LANDSCAPE_ATMOSPHERE_COMMON_INCLUDED
#define LANDSCAPE_ATMOSPHERE_COMMON_INCLUDED

#ifndef PI
#define PI 3.14159265359
#endif
#ifndef INV_PI
#define INV_PI 0.31830988618
#endif
#ifndef INV_FOUR_PI
#define INV_FOUR_PI 0.07957747154
#endif
static const float kAtmosphereFibonacciGoldenRatio = 1.61803398875;

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

float DistanceToTopAtmosphereBoundary(float radiusKm, float cosTheta, float topRadiusKm)
{
    float discriminant = radiusKm * radiusKm * (cosTheta * cosTheta - 1.0) + topRadiusKm * topRadiusKm;
    return max(0.0, -radiusKm * cosTheta + sqrt(max(discriminant, 0.0)));
}

float GetHorizonCosTheta(float radiusKm, float groundRadiusKm)
{
    float radiusKmSq = max(radiusKm * radiusKm, 1e-4);
    float groundRatioSq = saturate((groundRadiusKm * groundRadiusKm) / radiusKmSq);
    return -sqrt(saturate(1.0 - groundRatioSq));
}

float2 UvToTransmittanceParameters(float2 uv, float groundRadiusKm, float topRadiusKm)
{
    uv = saturate(uv);
    float H = sqrt(max(topRadiusKm * topRadiusKm - groundRadiusKm * groundRadiusKm, 0.0));
    float rho = sqrt(max(uv.y, 0.0)) * H;
    float radiusKm = sqrt(max(rho * rho + groundRadiusKm * groundRadiusKm, 0.0));

    float dMin = topRadiusKm - radiusKm;
    float dMax = rho + H;
    float d = dMin + (dMax - dMin) * uv.x;
    float cosTheta = 1.0;
    if (abs(d) > 1e-4)
    {
        float numerator = topRadiusKm * topRadiusKm - radiusKm * radiusKm - d * d;
        float denominator = max(2.0 * radiusKm * d, 1e-4);
        cosTheta = clamp(numerator / denominator, GetHorizonCosTheta(radiusKm, groundRadiusKm), 1.0);
    }

    return float2(radiusKm, cosTheta);
}

float2 TransmittanceParametersToUv(float radiusKm, float cosTheta, float groundRadiusKm, float topRadiusKm)
{
    float safeRadiusKm = clamp(radiusKm, groundRadiusKm, topRadiusKm);
    float clampedCosTheta = clamp(cosTheta, GetHorizonCosTheta(safeRadiusKm, groundRadiusKm), 1.0);
    float H = sqrt(max(topRadiusKm * topRadiusKm - groundRadiusKm * groundRadiusKm, 0.0));
    float rho = sqrt(max(safeRadiusKm * safeRadiusKm - groundRadiusKm * groundRadiusKm, 0.0));
    float v = H > 0.0 ? saturate((rho / H) * (rho / H)) : 0.0;

    float distanceKm = DistanceToTopAtmosphereBoundary(safeRadiusKm, clampedCosTheta, topRadiusKm);
    float dMin = topRadiusKm - safeRadiusKm;
    float dMax = rho + H;
    float u = saturate((distanceKm - dMin) / max(dMax - dMin, 1e-4));

    return saturate(float2(u, v));
}

float3 SampleTransmittanceLut(
    Texture2D lut,
    SamplerState lutSampler,
    float radiusKm,
    float cosTheta,
    float groundRadiusKm,
    float topRadiusKm)
{
    float2 uv = TransmittanceParametersToUv(radiusKm, cosTheta, groundRadiusKm, topRadiusKm);
    return lut.SampleLevel(lutSampler, uv, 0).rgb;
}

float3 SampleTransmittanceLut(
    Texture2D lut,
    SamplerState lutSampler,
    float3 positionKm,
    float3 direction,
    float groundRadiusKm,
    float topRadiusKm)
{
    float radiusKm = length(positionKm);
    float cosTheta = dot(normalize(positionKm), direction);
    return SampleTransmittanceLut(lut, lutSampler, radiusKm, cosTheta, groundRadiusKm, topRadiusKm);
}

float2 MultiScatteringParametersToUv(float radiusKm, float cosSunZenith, float groundRadiusKm, float topRadiusKm)
{
    float u = cosSunZenith * 0.5 + 0.5;
    float v = (radiusKm - groundRadiusKm) / max(topRadiusKm - groundRadiusKm, 1e-4);
    return saturate(float2(u, v));
}

float3 SampleMultiScatteringLut(
    Texture2D lut,
    SamplerState lutSampler,
    float radiusKm,
    float cosSunZenith,
    float groundRadiusKm,
    float topRadiusKm)
{
    float2 uv = MultiScatteringParametersToUv(radiusKm, cosSunZenith, groundRadiusKm, topRadiusKm);
    return lut.SampleLevel(lutSampler, uv, 0).rgb;
}

float PhaseRayleigh(float cosTheta)
{
    return (3.0 / (16.0 * PI)) * (1.0 + cosTheta * cosTheta);
}

float PhaseMieCornetteShanks(float cosTheta, float g)
{
    float g2 = g * g;
    float denominator = max(pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-4), 1.5), 1e-4);
    return (3.0 * (1.0 - g2) * (1.0 + cosTheta * cosTheta)) / (8.0 * PI * (2.0 + g2) * denominator);
}

float SkyViewElevationToV(float elevation)
{
    float normalizedElevation = clamp(elevation / (PI * 0.5), -1.0, 1.0);
    return 0.5 + 0.5 * sign(normalizedElevation) * sqrt(abs(normalizedElevation));
}

float SkyViewVToElevation(float v)
{
    float adjustedV = v - 0.5;
    float signV = adjustedV < 0.0 ? -1.0 : 1.0;
    float vSquared = adjustedV * adjustedV * 4.0;
    return signV * vSquared * (PI * 0.5);
}

float2 DirectionToSkyViewUv(float3 direction, float3 sunDirection, float3 basisRight, float3 basisUp, float3 basisForward)
{
    float x = dot(direction, basisRight);
    float y = dot(direction, basisUp);
    float z = dot(direction, basisForward);
    float elevation = asin(clamp(y, -1.0, 1.0));
    float azimuth = atan2(x, z);

    float sunX = dot(sunDirection, basisRight);
    float sunZ = dot(sunDirection, basisForward);
    float sunAzimuth = atan2(sunX, sunZ);
    float relativeAzimuth = atan2(sin(azimuth - sunAzimuth), cos(azimuth - sunAzimuth));
    float u = relativeAzimuth / (2.0 * PI) + 0.5;
    float v = SkyViewElevationToV(elevation);
    return saturate(float2(frac(u), v));
}

float3 GetCameraRayDirection(float2 uv, float tanHalfVerticalFov, float aspectRatio, float3 basisRight, float3 basisUp, float3 basisForward)
{
    float2 ndc = uv * 2.0 - 1.0;
    float3 ray = basisForward
        + basisRight * (ndc.x * tanHalfVerticalFov * aspectRatio)
        + basisUp * (ndc.y * tanHalfVerticalFov);
    return normalize(ray);
}

float SliceToDistanceKm(float normalizedZ, float maxDistanceKm)
{
    float clampedZ = saturate(normalizedZ);
    return clampedZ * clampedZ * maxDistanceKm;
}

float DistanceToSliceNormalized(float distanceKm, float maxDistanceKm)
{
    if (maxDistanceKm <= 1e-4)
        return 0.0;

    return saturate(sqrt(max(distanceKm, 0.0) / maxDistanceKm));
}

float3 GetFibonacciSphereDirection(uint sampleIndex, uint sampleCount)
{
    float phi = 2.0 * PI * frac((float)sampleIndex / kAtmosphereFibonacciGoldenRatio);
    float cosTheta = 1.0 - (2.0 * (float)sampleIndex + 1.0) / (float)sampleCount;
    float sinTheta = sqrt(saturate(1.0 - cosTheta * cosTheta));
    return float3(cos(phi) * sinTheta, cosTheta, sin(phi) * sinTheta);
}

float GetPlanetShadow(float3 samplePositionKm, float3 sunDirection, float groundRadiusKm)
{
    float groundHitDistance = RaySphereIntersectNearest(samplePositionKm, sunDirection, groundRadiusKm);
    return groundHitDistance > 0.0 ? 0.0 : 1.0;
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
