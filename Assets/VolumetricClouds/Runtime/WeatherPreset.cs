using UnityEngine;

namespace VolumetricClouds.Runtime
{
    [CreateAssetMenu(fileName = "WeatherPreset", menuName = "Rendering/Volumetric Cloud Weather Preset")]
    public sealed class WeatherPreset : ScriptableObject
    {
        [Header("Identity")]
        public string presetName = "Cloudy";

        [Header("Targets")]
        [Range(0.0f, 1.0f)] public float targetCoverage = 0.45f;
        [Range(0.0f, 1.0f)] public float targetCloudType = 0.45f;
        [Range(0.0f, 1.0f)] public float targetWetness = 0.2f;
        [Range(0.0f, 1.0f)] public float targetDensityBias = 0.15f;

        [Header("Wind")]
        public Vector2 windDirection = new Vector2(1.0f, 0.0f);
        [Min(0.0f)] public float windSpeedKmPerSecond = 0.02f;

        [Header("Evolution")]
        [Min(0.0f)] public float evolutionSpeed = 1.0f;
        [Min(0.0f)] public float transitionDurationSeconds = 30.0f;

        [Header("Density Remap")]
        public float coverageBias = 0.0f;
        [Min(0.0f)] public float coverageContrast = 1.0f;
        [Range(0.0f, 1.0f)] public float detailErosionStrength = 0.35f;

        private void OnValidate()
        {
            targetCoverage = Mathf.Clamp01(targetCoverage);
            targetCloudType = Mathf.Clamp01(targetCloudType);
            targetWetness = Mathf.Clamp01(targetWetness);
            targetDensityBias = Mathf.Clamp01(targetDensityBias);
            windSpeedKmPerSecond = Mathf.Max(0.0f, windSpeedKmPerSecond);
            evolutionSpeed = Mathf.Max(0.0f, evolutionSpeed);
            transitionDurationSeconds = Mathf.Max(0.0f, transitionDurationSeconds);
            coverageContrast = Mathf.Max(0.0f, coverageContrast);
            detailErosionStrength = Mathf.Clamp01(detailErosionStrength);
        }
    }
}
