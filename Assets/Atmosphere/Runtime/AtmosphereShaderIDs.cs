using UnityEngine;

namespace Landscape.Atmosphere
{
    public static class AtmosphereShaderIDs
    {
        public static readonly int TransmittanceLut = Shader.PropertyToID("_AtmosphereTransmittanceLut");
        public static readonly int GroundRadiusKm = Shader.PropertyToID("_AtmosphereGroundRadiusKm");
        public static readonly int TopRadiusKm = Shader.PropertyToID("_AtmosphereTopRadiusKm");
        public static readonly int RayleighScattering = Shader.PropertyToID("_AtmosphereRayleighScattering");
        public static readonly int MieScattering = Shader.PropertyToID("_AtmosphereMieScattering");
        public static readonly int MieAbsorption = Shader.PropertyToID("_AtmosphereMieAbsorption");
        public static readonly int OzoneAbsorption = Shader.PropertyToID("_AtmosphereOzoneAbsorption");
        public static readonly int ScaleHeights = Shader.PropertyToID("_AtmosphereScaleHeights");
        public static readonly int OzoneLayer = Shader.PropertyToID("_AtmosphereOzoneLayer");
        public static readonly int TransmittanceSize = Shader.PropertyToID("_AtmosphereTransmittanceSize");
        public static readonly int TransmittanceSteps = Shader.PropertyToID("_AtmosphereTransmittanceSteps");
    }
}
