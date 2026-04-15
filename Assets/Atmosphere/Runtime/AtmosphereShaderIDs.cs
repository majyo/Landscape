using UnityEngine;

namespace Atmosphere.Runtime
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
        public static readonly int MultiScatteringLut = Shader.PropertyToID("_AtmosphereMultiScatteringLut");
        public static readonly int MultiScatteringSize = Shader.PropertyToID("_AtmosphereMultiScatteringSize");
        public static readonly int GroundAlbedo = Shader.PropertyToID("_AtmosphereGroundAlbedo");
        public static readonly int MultiScatteringSphereSamples = Shader.PropertyToID("_AtmosphereMultiScatteringSphereSamples");
        public static readonly int MultiScatteringRaySteps = Shader.PropertyToID("_AtmosphereMultiScatteringRaySteps");
        public static readonly int SkyViewLut = Shader.PropertyToID("_AtmosphereSkyViewLut");
        public static readonly int SkyViewSize = Shader.PropertyToID("_AtmosphereSkyViewSize");
        public static readonly int SkyViewRaySteps = Shader.PropertyToID("_AtmosphereSkyViewRaySteps");
        public static readonly int SunDirection = Shader.PropertyToID("_AtmosphereSunDirection");
        public static readonly int SunIlluminance = Shader.PropertyToID("_AtmosphereSunIlluminance");
        public static readonly int MiePhaseG = Shader.PropertyToID("_AtmosphereMiePhaseG");
        public static readonly int CameraPositionKm = Shader.PropertyToID("_AtmosphereCameraPositionKm");
        public static readonly int CameraBasisRight = Shader.PropertyToID("_AtmosphereCameraBasisRight");
        public static readonly int CameraBasisUp = Shader.PropertyToID("_AtmosphereCameraBasisUp");
        public static readonly int CameraBasisForward = Shader.PropertyToID("_AtmosphereCameraBasisForward");
        public static readonly int CameraSkyViewMatrix = Shader.PropertyToID("_AtmosphereCameraSkyViewMatrix");
    }
}
