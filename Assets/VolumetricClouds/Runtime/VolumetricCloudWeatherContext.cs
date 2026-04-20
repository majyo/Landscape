using UnityEngine;

namespace VolumetricClouds.Runtime
{
    internal readonly struct VolumetricCloudWeatherContext
    {
        public readonly bool EnableRuntimeWeatherField;
        public readonly Texture WeatherFieldTexture;
        public readonly Texture2D WeatherSeedTexture;
        public readonly Texture2D CloudHeightDensityLut;
        public readonly int WeatherFieldResolution;
        public readonly float WeatherFieldScaleKm;
        public readonly Vector2 WeatherFieldOffsetKm;
        public readonly float TargetCoverage;
        public readonly float GlobalCoverageGain;
        public readonly float CoverageBias;
        public readonly float CoverageContrast;
        public readonly float CloudType;
        public readonly float Wetness;
        public readonly float DensityBias;
        public readonly float DetailErosionStrength;
        public readonly float CloudTypeRemapMin;
        public readonly float CloudTypeRemapMax;
        public readonly Vector2 WindDirection;
        public readonly float WindSpeedKmPerSecond;
        public readonly float EvolutionSpeed;
        public readonly float WeatherTransition01;
        public readonly float EffectiveTemporalResponse;
        public readonly float DeltaTimeSeconds;
        public readonly bool InitializeFromSeed;
        public readonly int WeatherFieldDiscontinuityVersion;

        public VolumetricCloudWeatherContext(
            bool enableRuntimeWeatherField,
            Texture weatherFieldTexture,
            Texture2D weatherSeedTexture,
            Texture2D cloudHeightDensityLut,
            int weatherFieldResolution,
            float weatherFieldScaleKm,
            Vector2 weatherFieldOffsetKm,
            float targetCoverage,
            float globalCoverageGain,
            float coverageBias,
            float coverageContrast,
            float cloudType,
            float wetness,
            float densityBias,
            float detailErosionStrength,
            float cloudTypeRemapMin,
            float cloudTypeRemapMax,
            Vector2 windDirection,
            float windSpeedKmPerSecond,
            float evolutionSpeed,
            float weatherTransition01,
            float effectiveTemporalResponse,
            float deltaTimeSeconds,
            bool initializeFromSeed,
            int weatherFieldDiscontinuityVersion)
        {
            EnableRuntimeWeatherField = enableRuntimeWeatherField;
            WeatherFieldTexture = weatherFieldTexture;
            WeatherSeedTexture = weatherSeedTexture;
            CloudHeightDensityLut = cloudHeightDensityLut;
            WeatherFieldResolution = weatherFieldResolution;
            WeatherFieldScaleKm = weatherFieldScaleKm;
            WeatherFieldOffsetKm = weatherFieldOffsetKm;
            TargetCoverage = targetCoverage;
            GlobalCoverageGain = globalCoverageGain;
            CoverageBias = coverageBias;
            CoverageContrast = coverageContrast;
            CloudType = cloudType;
            Wetness = wetness;
            DensityBias = densityBias;
            DetailErosionStrength = detailErosionStrength;
            CloudTypeRemapMin = cloudTypeRemapMin;
            CloudTypeRemapMax = cloudTypeRemapMax;
            WindDirection = windDirection;
            WindSpeedKmPerSecond = windSpeedKmPerSecond;
            EvolutionSpeed = evolutionSpeed;
            WeatherTransition01 = weatherTransition01;
            EffectiveTemporalResponse = effectiveTemporalResponse;
            DeltaTimeSeconds = deltaTimeSeconds;
            InitializeFromSeed = initializeFromSeed;
            WeatherFieldDiscontinuityVersion = weatherFieldDiscontinuityVersion;
        }
    }
}
