using UnityEngine;

namespace Landscape.Atmosphere
{
    [CreateAssetMenu(fileName = "AtmosphereProfile", menuName = "Rendering/Atmosphere Profile")]
    public sealed class AtmosphereProfile : ScriptableObject
    {
        [Header("Geometry")]
        [Min(1.0f)] public float groundRadiusKm = 6360.0f;
        [Min(1.0f)] public float topRadiusKm = 6460.0f;

        [Header("Rayleigh")]
        public Vector3 rayleighScattering = new Vector3(5.802e-3f, 13.558e-3f, 33.1e-3f);
        [Min(0.001f)] public float rayleighScaleHeightKm = 8.0f;

        [Header("Mie")]
        public Vector3 mieScattering = new Vector3(3.996e-3f, 3.996e-3f, 3.996e-3f);
        public Vector3 mieAbsorption = new Vector3(4.40e-3f, 4.40e-3f, 4.40e-3f);
        [Min(0.001f)] public float mieScaleHeightKm = 1.2f;

        [Header("Ozone")]
        public Vector3 ozoneAbsorption = new Vector3(0.650e-3f, 1.881e-3f, 0.085e-3f);
        [Min(0.0f)] public float ozoneLayerCenterKm = 25.0f;
        [Min(0.001f)] public float ozoneLayerHalfWidthKm = 15.0f;

        [Header("Transmittance LUT")]
        [Min(1)] public int transmittanceWidth = 256;
        [Min(1)] public int transmittanceHeight = 64;
        [Min(1)] public int transmittanceSteps = 40;

        [Header("Multi-scattering LUT")]
        public Vector3 groundAlbedo = new Vector3(0.3f, 0.3f, 0.3f);
        [Min(1)] public int multiScatteringWidth = 32;
        [Min(1)] public int multiScatteringHeight = 32;
        [Min(1)] public int multiScatteringSphereSamples = 64;
        [Min(1)] public int multiScatteringRaySteps = 20;

        private void OnValidate()
        {
            topRadiusKm = Mathf.Max(topRadiusKm, groundRadiusKm + 1.0f);
            rayleighScaleHeightKm = Mathf.Max(0.001f, rayleighScaleHeightKm);
            mieScaleHeightKm = Mathf.Max(0.001f, mieScaleHeightKm);
            ozoneLayerHalfWidthKm = Mathf.Max(0.001f, ozoneLayerHalfWidthKm);
            transmittanceWidth = Mathf.Max(1, transmittanceWidth);
            transmittanceHeight = Mathf.Max(1, transmittanceHeight);
            transmittanceSteps = Mathf.Max(1, transmittanceSteps);
            multiScatteringWidth = Mathf.Max(1, multiScatteringWidth);
            multiScatteringHeight = Mathf.Max(1, multiScatteringHeight);
            multiScatteringSphereSamples = Mathf.Max(1, multiScatteringSphereSamples);
            multiScatteringRaySteps = Mathf.Max(1, multiScatteringRaySteps);
        }
    }
}
