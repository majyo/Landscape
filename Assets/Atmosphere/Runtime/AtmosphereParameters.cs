using UnityEngine;

namespace Atmosphere.Runtime
{
    public struct AtmosphereParameters
    {
        public float GroundRadiusKm;
        public float TopRadiusKm;
        public Vector3 RayleighScattering;
        public float RayleighScaleHeightKm;
        public Vector3 MieScattering;
        public Vector3 MieAbsorption;
        public float MieScaleHeightKm;
        public Vector3 OzoneAbsorption;
        public float OzoneLayerCenterKm;
        public float OzoneLayerHalfWidthKm;
        public int TransmittanceWidth;
        public int TransmittanceHeight;
        public int TransmittanceSteps;
        public Vector3 GroundAlbedo;
        public bool RenderGroundInSkyView;
        public int MultiScatteringWidth;
        public int MultiScatteringHeight;
        public int MultiScatteringSphereSamples;
        public int MultiScatteringRaySteps;
        public int SkyViewWidth;
        public int SkyViewHeight;
        public int SkyViewRaySteps;
        public int AerialPerspectiveWidth;
        public int AerialPerspectiveHeight;
        public int AerialPerspectiveDepth;
        public float AerialPerspectiveMaxDistanceKm;
        public Vector3 SunIlluminance;
        public bool UseDirectionalLightColor;
        public float SunIntensityMultiplier;
        public float MiePhaseG;
        public float SunAngularRadiusRadians;
        public float SunDiskEdgeSoftnessRadians;
        public float SunDiskIntensityMultiplier;
        public float SkyExposure;
        public float AerialPerspectiveExposure;
        public int TransmittanceHash;
        public int MultiScatteringHash;
        public int SkyViewHash;
        public int AerialPerspectiveHash;

        public static AtmosphereParameters FromProfile(AtmosphereProfile profile)
        {
            AtmosphereParameters parameters = new AtmosphereParameters
            {
                GroundRadiusKm = Mathf.Max(1.0f, profile.groundRadiusKm),
                TopRadiusKm = Mathf.Max(profile.topRadiusKm, profile.groundRadiusKm + 1.0f),
                RayleighScattering = profile.rayleighScattering,
                RayleighScaleHeightKm = Mathf.Max(0.001f, profile.rayleighScaleHeightKm),
                MieScattering = profile.mieScattering,
                MieAbsorption = profile.mieAbsorption,
                MieScaleHeightKm = Mathf.Max(0.001f, profile.mieScaleHeightKm),
                OzoneAbsorption = profile.ozoneAbsorption,
                OzoneLayerCenterKm = Mathf.Max(0.0f, profile.ozoneLayerCenterKm),
                OzoneLayerHalfWidthKm = Mathf.Max(0.001f, profile.ozoneLayerHalfWidthKm),
                TransmittanceWidth = Mathf.Max(1, profile.transmittanceWidth),
                TransmittanceHeight = Mathf.Max(1, profile.transmittanceHeight),
                TransmittanceSteps = Mathf.Max(1, profile.transmittanceSteps),
                GroundAlbedo = new Vector3(
                    Mathf.Clamp01(profile.groundAlbedo.x),
                    Mathf.Clamp01(profile.groundAlbedo.y),
                    Mathf.Clamp01(profile.groundAlbedo.z)),
                RenderGroundInSkyView = profile.renderGroundInSkyView,
                MultiScatteringWidth = Mathf.Max(1, profile.multiScatteringWidth),
                MultiScatteringHeight = Mathf.Max(1, profile.multiScatteringHeight),
                MultiScatteringSphereSamples = Mathf.Max(1, profile.multiScatteringSphereSamples),
                MultiScatteringRaySteps = Mathf.Max(1, profile.multiScatteringRaySteps),
                SkyViewWidth = Mathf.Max(1, profile.skyViewWidth),
                SkyViewHeight = Mathf.Max(1, profile.skyViewHeight),
                SkyViewRaySteps = Mathf.Max(1, profile.skyViewRaySteps),
                AerialPerspectiveWidth = Mathf.Max(1, profile.aerialPerspectiveWidth),
                AerialPerspectiveHeight = Mathf.Max(1, profile.aerialPerspectiveHeight),
                AerialPerspectiveDepth = Mathf.Max(1, profile.aerialPerspectiveDepth),
                AerialPerspectiveMaxDistanceKm = Mathf.Max(0.001f, profile.aerialPerspectiveMaxDistanceKm),
                SunIlluminance = profile.sunIlluminance,
                UseDirectionalLightColor = profile.useDirectionalLightColor,
                SunIntensityMultiplier = Mathf.Max(0.0f, profile.sunIntensityMultiplier),
                MiePhaseG = Mathf.Clamp(profile.miePhaseG, 0.0f, 0.99f),
                SunAngularRadiusRadians = Mathf.Deg2Rad * Mathf.Clamp(profile.sunAngularRadiusDegrees, 0.0f, 45.0f),
                SunDiskEdgeSoftnessRadians = Mathf.Deg2Rad * Mathf.Max(0.0f, profile.sunDiskEdgeSoftnessDegrees),
                SunDiskIntensityMultiplier = Mathf.Max(0.0f, profile.sunDiskIntensityMultiplier),
                SkyExposure = Mathf.Max(0.001f, profile.skyExposure),
                AerialPerspectiveExposure = Mathf.Max(0.001f, profile.aerialPerspectiveExposure),
            };

            parameters.TransmittanceHash = ComputeTransmittanceHash(parameters);
            parameters.MultiScatteringHash = ComputeMultiScatteringHash(parameters);
            parameters.SkyViewHash = ComputeSkyViewHash(parameters);
            parameters.AerialPerspectiveHash = ComputeAerialPerspectiveHash(parameters);
            return parameters;
        }

        private static int ComputeTransmittanceHash(AtmosphereParameters parameters)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Quantize(parameters.GroundRadiusKm);
                hash = (hash * 31) + Quantize(parameters.TopRadiusKm);
                hash = (hash * 31) + Quantize(parameters.RayleighScattering.x);
                hash = (hash * 31) + Quantize(parameters.RayleighScattering.y);
                hash = (hash * 31) + Quantize(parameters.RayleighScattering.z);
                hash = (hash * 31) + Quantize(parameters.RayleighScaleHeightKm);
                hash = (hash * 31) + Quantize(parameters.MieScattering.x);
                hash = (hash * 31) + Quantize(parameters.MieScattering.y);
                hash = (hash * 31) + Quantize(parameters.MieScattering.z);
                hash = (hash * 31) + Quantize(parameters.MieAbsorption.x);
                hash = (hash * 31) + Quantize(parameters.MieAbsorption.y);
                hash = (hash * 31) + Quantize(parameters.MieAbsorption.z);
                hash = (hash * 31) + Quantize(parameters.MieScaleHeightKm);
                hash = (hash * 31) + Quantize(parameters.OzoneAbsorption.x);
                hash = (hash * 31) + Quantize(parameters.OzoneAbsorption.y);
                hash = (hash * 31) + Quantize(parameters.OzoneAbsorption.z);
                hash = (hash * 31) + Quantize(parameters.OzoneLayerCenterKm);
                hash = (hash * 31) + Quantize(parameters.OzoneLayerHalfWidthKm);
                hash = (hash * 31) + parameters.TransmittanceWidth;
                hash = (hash * 31) + parameters.TransmittanceHeight;
                hash = (hash * 31) + parameters.TransmittanceSteps;
                return hash;
            }
        }

        private static int ComputeMultiScatteringHash(AtmosphereParameters parameters)
        {
            unchecked
            {
                int hash = parameters.TransmittanceHash;
                hash = (hash * 31) + Quantize(parameters.GroundAlbedo.x);
                hash = (hash * 31) + Quantize(parameters.GroundAlbedo.y);
                hash = (hash * 31) + Quantize(parameters.GroundAlbedo.z);
                hash = (hash * 31) + parameters.MultiScatteringWidth;
                hash = (hash * 31) + parameters.MultiScatteringHeight;
                hash = (hash * 31) + parameters.MultiScatteringSphereSamples;
                hash = (hash * 31) + parameters.MultiScatteringRaySteps;
                return hash;
            }
        }

        private static int ComputeSkyViewHash(AtmosphereParameters parameters)
        {
            unchecked
            {
                int hash = parameters.MultiScatteringHash;
                hash = (hash * 31) + parameters.SkyViewWidth;
                hash = (hash * 31) + parameters.SkyViewHeight;
                hash = (hash * 31) + parameters.SkyViewRaySteps;
                hash = (hash * 31) + Quantize(parameters.SunIlluminance.x);
                hash = (hash * 31) + Quantize(parameters.SunIlluminance.y);
                hash = (hash * 31) + Quantize(parameters.SunIlluminance.z);
                hash = (hash * 31) + (parameters.UseDirectionalLightColor ? 1 : 0);
                hash = (hash * 31) + Quantize(parameters.SunIntensityMultiplier);
                hash = (hash * 31) + Quantize(parameters.MiePhaseG);
                hash = (hash * 31) + Quantize(parameters.SkyExposure);
                hash = (hash * 31) + (parameters.RenderGroundInSkyView ? 1 : 0);
                return hash;
            }
        }

        private static int ComputeAerialPerspectiveHash(AtmosphereParameters parameters)
        {
            unchecked
            {
                int hash = parameters.MultiScatteringHash;
                hash = (hash * 31) + parameters.AerialPerspectiveWidth;
                hash = (hash * 31) + parameters.AerialPerspectiveHeight;
                hash = (hash * 31) + parameters.AerialPerspectiveDepth;
                hash = (hash * 31) + Quantize(parameters.AerialPerspectiveMaxDistanceKm);
                hash = (hash * 31) + Quantize(parameters.SunIlluminance.x);
                hash = (hash * 31) + Quantize(parameters.SunIlluminance.y);
                hash = (hash * 31) + Quantize(parameters.SunIlluminance.z);
                hash = (hash * 31) + (parameters.UseDirectionalLightColor ? 1 : 0);
                hash = (hash * 31) + Quantize(parameters.SunIntensityMultiplier);
                hash = (hash * 31) + Quantize(parameters.MiePhaseG);
                hash = (hash * 31) + Quantize(parameters.AerialPerspectiveExposure);
                return hash;
            }
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 100000.0f);
        }
    }
}
