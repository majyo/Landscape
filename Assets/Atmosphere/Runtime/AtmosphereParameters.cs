using UnityEngine;

namespace Landscape.Atmosphere
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
        public int MultiScatteringWidth;
        public int MultiScatteringHeight;
        public int MultiScatteringSphereSamples;
        public int MultiScatteringRaySteps;
        public int TransmittanceHash;
        public int MultiScatteringHash;

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
                GroundAlbedo = profile.groundAlbedo,
                MultiScatteringWidth = Mathf.Max(1, profile.multiScatteringWidth),
                MultiScatteringHeight = Mathf.Max(1, profile.multiScatteringHeight),
                MultiScatteringSphereSamples = Mathf.Max(1, profile.multiScatteringSphereSamples),
                MultiScatteringRaySteps = Mathf.Max(1, profile.multiScatteringRaySteps),
            };

            parameters.TransmittanceHash = ComputeTransmittanceHash(parameters);
            parameters.MultiScatteringHash = ComputeMultiScatteringHash(parameters);
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

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * 100000.0f);
        }
    }
}
