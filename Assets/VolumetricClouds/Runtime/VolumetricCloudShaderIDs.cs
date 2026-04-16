using UnityEngine;

namespace VolumetricClouds.Runtime
{
    public static class VolumetricCloudShaderIDs
    {
        public static readonly int VolumetricCloudTexture = Shader.PropertyToID("_VolumetricCloudTexture");
        public static readonly int VolumetricCloudTraceSize = Shader.PropertyToID("_VolumetricCloudTraceSize");
        public static readonly int CloudBottomRadiusKm = Shader.PropertyToID("_CloudBottomRadiusKm");
        public static readonly int CloudTopRadiusKm = Shader.PropertyToID("_CloudTopRadiusKm");
        public static readonly int CloudThicknessKm = Shader.PropertyToID("_CloudThicknessKm");
        public static readonly int CloudCoverage = Shader.PropertyToID("_CloudCoverage");
        public static readonly int CloudDensityMultiplier = Shader.PropertyToID("_CloudDensityMultiplier");
        public static readonly int CloudLightAbsorption = Shader.PropertyToID("_CloudLightAbsorption");
        public static readonly int CloudAmbientStrength = Shader.PropertyToID("_CloudAmbientStrength");
        public static readonly int CloudPhaseG = Shader.PropertyToID("_CloudPhaseG");
        public static readonly int CloudMaxRenderDistanceKm = Shader.PropertyToID("_CloudMaxRenderDistanceKm");
        public static readonly int CloudStepCount = Shader.PropertyToID("_CloudStepCount");
        public static readonly int CloudShadowStepCount = Shader.PropertyToID("_CloudShadowStepCount");
        public static readonly int CloudBaseShapeNoise = Shader.PropertyToID("_CloudBaseShapeNoise");
        public static readonly int CloudDetailShapeNoise = Shader.PropertyToID("_CloudDetailShapeNoise");
        public static readonly int CloudHasDetailShapeNoise = Shader.PropertyToID("_CloudHasDetailShapeNoise");
        public static readonly int CloudShapeScaleData = Shader.PropertyToID("_CloudShapeScaleData");
        public static readonly int CloudWindData = Shader.PropertyToID("_CloudWindData");
    }
}
