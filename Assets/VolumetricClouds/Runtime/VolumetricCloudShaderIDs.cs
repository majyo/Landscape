using UnityEngine;

namespace VolumetricClouds.Runtime
{
    public static class VolumetricCloudShaderIDs
    {
        public static readonly int VolumetricCloudTexture = Shader.PropertyToID("_VolumetricCloudTexture");
        public static readonly int VolumetricCloudCurrentTexture = Shader.PropertyToID("_VolumetricCloudCurrentTexture");
        public static readonly int VolumetricCloudHistoryTexture = Shader.PropertyToID("_VolumetricCloudHistoryTexture");
        public static readonly int VolumetricCloudHistoryOutputTexture = Shader.PropertyToID("_VolumetricCloudHistoryOutputTexture");
        public static readonly int VolumetricCloudStabilizedTexture = Shader.PropertyToID("_VolumetricCloudStabilizedTexture");
        public static readonly int VolumetricCloudHistoryWeightTexture = Shader.PropertyToID("_VolumetricCloudHistoryWeightTexture");
        public static readonly int CloudWeatherFieldTexture = Shader.PropertyToID("_CloudWeatherFieldTexture");
        public static readonly int CloudWeatherFieldOutputTexture = Shader.PropertyToID("_CloudWeatherFieldOutputTexture");
        public static readonly int CloudWeatherSeedTexture = Shader.PropertyToID("_CloudWeatherSeedTexture");
        public static readonly int CloudHeightDensityLut = Shader.PropertyToID("_CloudHeightDensityLut");
        public static readonly int CloudCurlNoise = Shader.PropertyToID("_CloudCurlNoise");
        public static readonly int VolumetricCloudTraceSize = Shader.PropertyToID("_VolumetricCloudTraceSize");
        public static readonly int CloudViewBasisRight = Shader.PropertyToID("_CloudViewBasisRight");
        public static readonly int CloudViewBasisUp = Shader.PropertyToID("_CloudViewBasisUp");
        public static readonly int CloudViewBasisForward = Shader.PropertyToID("_CloudViewBasisForward");
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
        public static readonly int CloudJitterData = Shader.PropertyToID("_CloudJitterData");
        public static readonly int CloudEnableJitter = Shader.PropertyToID("_CloudEnableJitter");
        public static readonly int CloudDebugMode = Shader.PropertyToID("_CloudDebugMode");
        public static readonly int CloudSceneDepthTexture = Shader.PropertyToID("_CloudSceneDepthTexture");
        public static readonly int CloudScreenSize = Shader.PropertyToID("_CloudScreenSize");
        public static readonly int CloudTemporalData = Shader.PropertyToID("_CloudTemporalData");
        public static readonly int CloudWeatherFieldSize = Shader.PropertyToID("_CloudWeatherFieldSize");
        public static readonly int CloudWeatherFieldData = Shader.PropertyToID("_CloudWeatherFieldData");
        public static readonly int CloudWeatherPresetData = Shader.PropertyToID("_CloudWeatherPresetData");
        public static readonly int CloudWeatherRemapData = Shader.PropertyToID("_CloudWeatherRemapData");
        public static readonly int CloudWeatherUpdateData = Shader.PropertyToID("_CloudWeatherUpdateData");
        public static readonly int CloudWeatherWindFieldData = Shader.PropertyToID("_CloudWeatherWindFieldData");
        public static readonly int CloudWeatherSeedScaleData = Shader.PropertyToID("_CloudWeatherSeedScaleData");
        public static readonly int CloudFallbackWeatherStateData = Shader.PropertyToID("_CloudFallbackWeatherStateData");
        public static readonly int CloudTypeRemapData = Shader.PropertyToID("_CloudTypeRemapData");
        public static readonly int CloudCurlNoiseData = Shader.PropertyToID("_CloudCurlNoiseData");
        public static readonly int CloudDetailErosionData = Shader.PropertyToID("_CloudDetailErosionData");
        public static readonly int CloudUseRuntimeWeatherField = Shader.PropertyToID("_CloudUseRuntimeWeatherField");
        public static readonly int CloudHasWeatherSeed = Shader.PropertyToID("_CloudHasWeatherSeed");
        public static readonly int CloudHasHeightDensityLut = Shader.PropertyToID("_CloudHasHeightDensityLut");
        public static readonly int CloudHasCurlNoise = Shader.PropertyToID("_CloudHasCurlNoise");
        public static readonly int CloudPreviousCameraPositionKm = Shader.PropertyToID("_CloudPreviousCameraPositionKm");
        public static readonly int CloudPreviousViewBasisRight = Shader.PropertyToID("_CloudPreviousViewBasisRight");
        public static readonly int CloudPreviousViewBasisUp = Shader.PropertyToID("_CloudPreviousViewBasisUp");
        public static readonly int CloudPreviousViewBasisForward = Shader.PropertyToID("_CloudPreviousViewBasisForward");
        public static readonly int CloudPreviousCameraData = Shader.PropertyToID("_CloudPreviousCameraData");
    }
}
