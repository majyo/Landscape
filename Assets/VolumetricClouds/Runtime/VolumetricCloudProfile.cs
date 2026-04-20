using UnityEngine;

namespace VolumetricClouds.Runtime
{
    [CreateAssetMenu(fileName = "VolumetricCloudProfile", menuName = "Rendering/Volumetric Cloud Profile")]
    public sealed class VolumetricCloudProfile : ScriptableObject
    {
        [Header("General")]
        public bool enableClouds = true;
        public bool useRuntimeWeatherField = true;

        [Header("Layer")]
        [Min(0.001f)] public float cloudBottomHeightKm = 1.5f;
        [Min(0.001f)] public float cloudTopHeightKm = 4.0f;
        [Range(0.0f, 1.0f)] public float cloudCoverage = 0.45f;
        [Min(0.0f)] public float densityMultiplier = 1.2f;

        [Header("Lighting")]
        [Min(0.0f)] public float lightAbsorption = 1.0f;
        [Min(0.0f)] public float ambientStrength = 0.35f;
        [Range(0.0f, 0.99f)] public float forwardScatteringG = 0.55f;

        [Header("Tracing")]
        [Min(1)] public int stepCount = 48;
        [Min(1)] public int shadowStepCount = 8;
        [Min(0.001f)] public float maxRenderDistanceKm = 64.0f;
        [Min(1)] public int traceWidth = 960;
        [Min(1)] public int traceHeight = 540;
        public bool enableJitter = false;
        [Range(0.0f, 1.0f)] public float jitterStrength = 1.0f;
        [Min(1)] public int jitterSequenceLength = 8;

        [Header("Temporal")]
        public bool enableTemporalAccumulation = true;
        [Range(0.0f, 0.99f)] public float temporalResponse = 0.9f;
        [Range(0.0f, 1.0f)] public float temporalTransmittanceRejectThreshold = 0.2f;
        [Min(0.0f)] public float temporalCameraResetDistanceKm = 0.25f;
        [Range(0.0f, 180.0f)] public float temporalCameraResetAngleDegrees = 20.0f;
        [Min(0.0f)] public float temporalFovResetDegrees = 2.0f;

        [Header("Noise")]
        [Min(0.001f)] public float shapeBaseScaleKm = 32.0f;
        [Min(0.001f)] public float detailScaleKm = 8.0f;
        public Texture3D baseShapeNoise;
        public Texture3D detailShapeNoise;
        public Texture2D defaultWeatherSeed;
        public Texture2D cloudHeightDensityLut;

        [Header("Weather Field")]
        public WeatherPreset defaultWeatherPreset;
        [Min(1)] public int weatherFieldResolution = 256;
        [Min(0.001f)] public float weatherFieldScaleKm = 256.0f;
        [Min(0.0f)] public float weatherFieldUpdateRate = 60.0f;
        [Range(0.0f, 1.0f)] public float detailErosionStrength = 0.35f;
        [Range(0.0f, 1.0f)] public float cloudTypeRemapMin = 0.0f;
        [Range(0.0f, 1.0f)] public float cloudTypeRemapMax = 1.0f;

        [Header("Wind")]
        public Vector2 windDirection = new Vector2(1.0f, 0.0f);
        [Min(0.0f)] public float windSpeedKmPerSecond = 0.02f;

        private void OnValidate()
        {
            cloudBottomHeightKm = Mathf.Max(0.001f, cloudBottomHeightKm);
            cloudTopHeightKm = Mathf.Max(cloudBottomHeightKm + 0.001f, cloudTopHeightKm);
            cloudCoverage = Mathf.Clamp01(cloudCoverage);
            densityMultiplier = Mathf.Max(0.0f, densityMultiplier);
            lightAbsorption = Mathf.Max(0.0f, lightAbsorption);
            ambientStrength = Mathf.Max(0.0f, ambientStrength);
            forwardScatteringG = Mathf.Clamp(forwardScatteringG, 0.0f, 0.99f);
            stepCount = Mathf.Max(1, stepCount);
            shadowStepCount = Mathf.Max(1, shadowStepCount);
            maxRenderDistanceKm = Mathf.Max(0.001f, maxRenderDistanceKm);
            traceWidth = Mathf.Max(1, traceWidth);
            traceHeight = Mathf.Max(1, traceHeight);
            jitterStrength = Mathf.Clamp01(jitterStrength);
            jitterSequenceLength = Mathf.Max(1, jitterSequenceLength);
            temporalResponse = Mathf.Clamp(temporalResponse, 0.0f, 0.99f);
            temporalTransmittanceRejectThreshold = Mathf.Clamp01(temporalTransmittanceRejectThreshold);
            temporalCameraResetDistanceKm = Mathf.Max(0.0f, temporalCameraResetDistanceKm);
            temporalCameraResetAngleDegrees = Mathf.Clamp(temporalCameraResetAngleDegrees, 0.0f, 180.0f);
            temporalFovResetDegrees = Mathf.Max(0.0f, temporalFovResetDegrees);
            shapeBaseScaleKm = Mathf.Max(0.001f, shapeBaseScaleKm);
            detailScaleKm = Mathf.Max(0.001f, detailScaleKm);
            weatherFieldResolution = Mathf.Max(1, weatherFieldResolution);
            weatherFieldScaleKm = Mathf.Max(0.001f, weatherFieldScaleKm);
            weatherFieldUpdateRate = Mathf.Max(0.0f, weatherFieldUpdateRate);
            detailErosionStrength = Mathf.Clamp01(detailErosionStrength);
            cloudTypeRemapMin = Mathf.Clamp01(cloudTypeRemapMin);
            cloudTypeRemapMax = Mathf.Clamp01(cloudTypeRemapMax);
            if (cloudTypeRemapMax < cloudTypeRemapMin)
                cloudTypeRemapMax = cloudTypeRemapMin;
            windSpeedKmPerSecond = Mathf.Max(0.0f, windSpeedKmPerSecond);
        }
    }
}
