using Atmosphere.Runtime;
using UnityEngine;

namespace VolumetricClouds.Runtime
{
    public readonly struct VolumetricCloudParameters
    {
        public readonly bool EnableClouds;
        public readonly float CloudBottomHeightKm;
        public readonly float CloudTopHeightKm;
        public readonly float CloudCoverage;
        public readonly float DensityMultiplier;
        public readonly float LightAbsorption;
        public readonly float AmbientStrength;
        public readonly float ForwardScatteringG;
        public readonly int StepCount;
        public readonly int ShadowStepCount;
        public readonly float MaxRenderDistanceKm;
        public readonly int TraceWidth;
        public readonly int TraceHeight;
        public readonly bool EnableJitter;
        public readonly float JitterStrength;
        public readonly int JitterSequenceLength;
        public readonly int JitterFrameIndex;
        public readonly int JitterIndex;
        public readonly Vector2 JitterOffset;
        public readonly bool EnableTemporalAccumulation;
        public readonly float TemporalResponse;
        public readonly float TemporalTransmittanceRejectThreshold;
        public readonly float TemporalCameraResetDistanceKm;
        public readonly float TemporalCameraResetAngleDegrees;
        public readonly float TemporalFovResetDegrees;
        public readonly float ShapeBaseScaleKm;
        public readonly float DetailScaleKm;
        public readonly Vector2 WindDirection;
        public readonly float WindSpeedKmPerSecond;
        public readonly Texture3D BaseShapeNoise;
        public readonly Texture3D DetailShapeNoise;
        public readonly bool EnableRuntimeWeatherField;
        public readonly Texture WeatherFieldTexture;
        public readonly Texture2D CloudHeightDensityLut;
        public readonly float WeatherFieldScaleKm;
        public readonly Vector2 WeatherFieldOffsetKm;
        public readonly float GlobalCoverageGain;
        public readonly float CoverageBias;
        public readonly float CoverageContrast;
        public readonly float DetailErosionStrength;
        public readonly float CloudTypeRemapMin;
        public readonly float CloudTypeRemapMax;
        public readonly int WeatherFieldDiscontinuityVersion;
        public readonly float GroundRadiusKm;
        public readonly float TopRadiusKm;
        public readonly Vector3 SunDirection;
        public readonly Vector3 SunIlluminance;
        public readonly Vector3 CameraPositionKm;
        public readonly Vector3 ViewBasisRight;
        public readonly Vector3 ViewBasisUp;
        public readonly Vector3 ViewBasisForward;
        public readonly Vector3 CameraBasisRight;
        public readonly Vector3 CameraBasisUp;
        public readonly Vector3 CameraBasisForward;
        public readonly float TanHalfVerticalFov;
        public readonly float AspectRatio;
        public readonly float CloudBottomRadiusKm;
        public readonly float CloudTopRadiusKm;
        public readonly float CloudThicknessKm;
        public readonly Vector2 WindOffset;
        public readonly int ResourceHash;
        public readonly int ParameterHash;
        public readonly int HistoryResetHash;

        public VolumetricCloudParameters(
            bool enableClouds,
            float cloudBottomHeightKm,
            float cloudTopHeightKm,
            float cloudCoverage,
            float densityMultiplier,
            float lightAbsorption,
            float ambientStrength,
            float forwardScatteringG,
            int stepCount,
            int shadowStepCount,
            float maxRenderDistanceKm,
            int traceWidth,
            int traceHeight,
            bool enableJitter,
            float jitterStrength,
            int jitterSequenceLength,
            int jitterFrameIndex,
            int jitterIndex,
            Vector2 jitterOffset,
            bool enableTemporalAccumulation,
            float temporalResponse,
            float temporalTransmittanceRejectThreshold,
            float temporalCameraResetDistanceKm,
            float temporalCameraResetAngleDegrees,
            float temporalFovResetDegrees,
            float shapeBaseScaleKm,
            float detailScaleKm,
            Vector2 windDirection,
            float windSpeedKmPerSecond,
            Texture3D baseShapeNoise,
            Texture3D detailShapeNoise,
            bool enableRuntimeWeatherField,
            Texture weatherFieldTexture,
            Texture2D cloudHeightDensityLut,
            float weatherFieldScaleKm,
            Vector2 weatherFieldOffsetKm,
            float globalCoverageGain,
            float coverageBias,
            float coverageContrast,
            float detailErosionStrength,
            float cloudTypeRemapMin,
            float cloudTypeRemapMax,
            int weatherFieldDiscontinuityVersion,
            float groundRadiusKm,
            float topRadiusKm,
            Vector3 sunDirection,
            Vector3 sunIlluminance,
            Vector3 cameraPositionKm,
            Vector3 viewBasisRight,
            Vector3 viewBasisUp,
            Vector3 viewBasisForward,
            Vector3 cameraBasisRight,
            Vector3 cameraBasisUp,
            Vector3 cameraBasisForward,
            float tanHalfVerticalFov,
            float aspectRatio,
            float cloudBottomRadiusKm,
            float cloudTopRadiusKm,
            float cloudThicknessKm,
            Vector2 windOffset)
        {
            EnableClouds = enableClouds;
            CloudBottomHeightKm = cloudBottomHeightKm;
            CloudTopHeightKm = cloudTopHeightKm;
            CloudCoverage = cloudCoverage;
            DensityMultiplier = densityMultiplier;
            LightAbsorption = lightAbsorption;
            AmbientStrength = ambientStrength;
            ForwardScatteringG = forwardScatteringG;
            StepCount = stepCount;
            ShadowStepCount = shadowStepCount;
            MaxRenderDistanceKm = maxRenderDistanceKm;
            TraceWidth = traceWidth;
            TraceHeight = traceHeight;
            EnableJitter = enableJitter;
            JitterStrength = jitterStrength;
            JitterSequenceLength = jitterSequenceLength;
            JitterFrameIndex = jitterFrameIndex;
            JitterIndex = jitterIndex;
            JitterOffset = jitterOffset;
            EnableTemporalAccumulation = enableTemporalAccumulation;
            TemporalResponse = temporalResponse;
            TemporalTransmittanceRejectThreshold = temporalTransmittanceRejectThreshold;
            TemporalCameraResetDistanceKm = temporalCameraResetDistanceKm;
            TemporalCameraResetAngleDegrees = temporalCameraResetAngleDegrees;
            TemporalFovResetDegrees = temporalFovResetDegrees;
            ShapeBaseScaleKm = shapeBaseScaleKm;
            DetailScaleKm = detailScaleKm;
            WindDirection = windDirection;
            WindSpeedKmPerSecond = windSpeedKmPerSecond;
            BaseShapeNoise = baseShapeNoise;
            DetailShapeNoise = detailShapeNoise;
            EnableRuntimeWeatherField = enableRuntimeWeatherField;
            WeatherFieldTexture = weatherFieldTexture;
            CloudHeightDensityLut = cloudHeightDensityLut;
            WeatherFieldScaleKm = weatherFieldScaleKm;
            WeatherFieldOffsetKm = weatherFieldOffsetKm;
            GlobalCoverageGain = globalCoverageGain;
            CoverageBias = coverageBias;
            CoverageContrast = coverageContrast;
            DetailErosionStrength = detailErosionStrength;
            CloudTypeRemapMin = cloudTypeRemapMin;
            CloudTypeRemapMax = cloudTypeRemapMax;
            WeatherFieldDiscontinuityVersion = weatherFieldDiscontinuityVersion;
            GroundRadiusKm = groundRadiusKm;
            TopRadiusKm = topRadiusKm;
            SunDirection = sunDirection;
            SunIlluminance = sunIlluminance;
            CameraPositionKm = cameraPositionKm;
            ViewBasisRight = viewBasisRight;
            ViewBasisUp = viewBasisUp;
            ViewBasisForward = viewBasisForward;
            CameraBasisRight = cameraBasisRight;
            CameraBasisUp = cameraBasisUp;
            CameraBasisForward = cameraBasisForward;
            TanHalfVerticalFov = tanHalfVerticalFov;
            AspectRatio = aspectRatio;
            CloudBottomRadiusKm = cloudBottomRadiusKm;
            CloudTopRadiusKm = cloudTopRadiusKm;
            CloudThicknessKm = cloudThicknessKm;
            WindOffset = windOffset;
            ResourceHash = ComputeResourceHash(traceWidth, traceHeight);
            HistoryResetHash = ComputeHistoryResetHash(
                enableClouds,
                cloudBottomHeightKm,
                cloudTopHeightKm,
                cloudCoverage,
                densityMultiplier,
                lightAbsorption,
                ambientStrength,
                forwardScatteringG,
                stepCount,
                shadowStepCount,
                maxRenderDistanceKm,
                enableJitter,
                jitterStrength,
                jitterSequenceLength,
                enableTemporalAccumulation,
                shapeBaseScaleKm,
                detailScaleKm,
                windDirection,
                windSpeedKmPerSecond,
                baseShapeNoise,
                detailShapeNoise,
                enableRuntimeWeatherField,
                weatherFieldTexture,
                cloudHeightDensityLut,
                weatherFieldScaleKm,
                weatherFieldOffsetKm,
                globalCoverageGain,
                coverageBias,
                coverageContrast,
                detailErosionStrength,
                cloudTypeRemapMin,
                cloudTypeRemapMax,
                weatherFieldDiscontinuityVersion,
                groundRadiusKm,
                topRadiusKm,
                sunDirection,
                sunIlluminance,
                cloudBottomRadiusKm,
                cloudTopRadiusKm,
                cloudThicknessKm,
                ResourceHash);
            ParameterHash = ComputeParameterHash(
                enableClouds,
                cloudBottomHeightKm,
                cloudTopHeightKm,
                cloudCoverage,
                densityMultiplier,
                lightAbsorption,
                ambientStrength,
                forwardScatteringG,
                stepCount,
                shadowStepCount,
                maxRenderDistanceKm,
                enableJitter,
                jitterStrength,
                jitterSequenceLength,
                jitterFrameIndex,
                jitterIndex,
                jitterOffset,
                enableTemporalAccumulation,
                temporalResponse,
                temporalTransmittanceRejectThreshold,
                temporalCameraResetDistanceKm,
                temporalCameraResetAngleDegrees,
                temporalFovResetDegrees,
                shapeBaseScaleKm,
                detailScaleKm,
                windDirection,
                windSpeedKmPerSecond,
                baseShapeNoise,
                detailShapeNoise,
                enableRuntimeWeatherField,
                weatherFieldTexture,
                cloudHeightDensityLut,
                weatherFieldScaleKm,
                weatherFieldOffsetKm,
                globalCoverageGain,
                coverageBias,
                coverageContrast,
                detailErosionStrength,
                cloudTypeRemapMin,
                cloudTypeRemapMax,
                weatherFieldDiscontinuityVersion,
                groundRadiusKm,
                topRadiusKm,
                sunDirection,
                sunIlluminance,
                cameraPositionKm,
                viewBasisRight,
                viewBasisUp,
                viewBasisForward,
                cameraBasisRight,
                cameraBasisUp,
                cameraBasisForward,
                tanHalfVerticalFov,
                aspectRatio,
                cloudBottomRadiusKm,
                cloudTopRadiusKm,
                cloudThicknessKm,
                windOffset,
                ResourceHash);
        }

        internal static VolumetricCloudParameters FromRuntime(
            VolumetricCloudProfile profile,
            in AtmosphereParameters atmosphereParameters,
            in AtmosphereViewParameters viewParameters,
            Camera camera,
            float timeSeconds,
            in VolumetricCloudJitterState jitterState,
            in VolumetricCloudWeatherContext weatherContext)
        {
            Vector2 sourceWindDirection = weatherContext.EnableRuntimeWeatherField
                ? weatherContext.WindDirection
                : profile.windDirection;
            float sourceWindSpeedKmPerSecond = weatherContext.EnableRuntimeWeatherField
                ? weatherContext.WindSpeedKmPerSecond
                : profile.windSpeedKmPerSecond;

            Vector2 normalizedWind = sourceWindDirection.sqrMagnitude > 1e-6f
                ? sourceWindDirection.normalized
                : new Vector2(1.0f, 0.0f);
            float cloudBottomHeightKm = Mathf.Max(0.001f, profile.cloudBottomHeightKm);
            float cloudTopHeightKm = Mathf.Max(cloudBottomHeightKm + 0.001f, profile.cloudTopHeightKm);
            float cloudThicknessKm = cloudTopHeightKm - cloudBottomHeightKm;
            float windDistanceKm = Mathf.Max(0.0f, sourceWindSpeedKmPerSecond) * Mathf.Max(0.0f, timeSeconds);
            Vector2 windOffset = normalizedWind * windDistanceKm;
            Transform cameraTransform = camera.transform;
            Vector3 viewBasisRight = cameraTransform.right.normalized;
            Vector3 viewBasisUp = cameraTransform.up.normalized;
            Vector3 viewBasisForward = cameraTransform.forward.normalized;
            Vector3 cameraBasisRight = new Vector3(viewParameters.CameraBasisRight.x, viewParameters.CameraBasisRight.y, viewParameters.CameraBasisRight.z);
            Vector3 cameraBasisUp = new Vector3(viewParameters.CameraBasisUp.x, viewParameters.CameraBasisUp.y, viewParameters.CameraBasisUp.z);
            Vector3 cameraBasisForward = new Vector3(viewParameters.CameraBasisForward.x, viewParameters.CameraBasisForward.y, viewParameters.CameraBasisForward.z);
            float verticalFovRadians = Mathf.Deg2Rad * Mathf.Max(1.0f, camera.fieldOfView);
            float tanHalfVerticalFov = Mathf.Tan(verticalFovRadians * 0.5f);
            float aspectRatio = camera.aspect > 0.0f ? camera.aspect : 1.0f;

            return new VolumetricCloudParameters(
                profile.enableClouds,
                cloudBottomHeightKm,
                cloudTopHeightKm,
                Mathf.Clamp01(profile.cloudCoverage),
                Mathf.Max(0.0f, profile.densityMultiplier),
                Mathf.Max(0.0f, profile.lightAbsorption),
                Mathf.Max(0.0f, profile.ambientStrength),
                Mathf.Clamp(profile.forwardScatteringG, 0.0f, 0.99f),
                Mathf.Max(1, profile.stepCount),
                Mathf.Max(1, profile.shadowStepCount),
                Mathf.Max(0.001f, profile.maxRenderDistanceKm),
                Mathf.Max(1, profile.traceWidth),
                Mathf.Max(1, profile.traceHeight),
                profile.enableJitter,
                Mathf.Clamp01(profile.jitterStrength),
                Mathf.Max(1, profile.jitterSequenceLength),
                jitterState.FrameIndex,
                jitterState.JitterIndex,
                jitterState.JitterOffset,
                profile.enableTemporalAccumulation,
                Mathf.Clamp(weatherContext.EffectiveTemporalResponse, 0.0f, 0.99f),
                Mathf.Clamp01(profile.temporalTransmittanceRejectThreshold),
                Mathf.Max(0.0f, profile.temporalCameraResetDistanceKm),
                Mathf.Clamp(profile.temporalCameraResetAngleDegrees, 0.0f, 180.0f),
                Mathf.Max(0.0f, profile.temporalFovResetDegrees),
                Mathf.Max(0.001f, profile.shapeBaseScaleKm),
                Mathf.Max(0.001f, profile.detailScaleKm),
                normalizedWind,
                Mathf.Max(0.0f, sourceWindSpeedKmPerSecond),
                profile.baseShapeNoise,
                profile.detailShapeNoise,
                weatherContext.EnableRuntimeWeatherField && weatherContext.WeatherFieldTexture != null,
                weatherContext.WeatherFieldTexture,
                weatherContext.CloudHeightDensityLut,
                Mathf.Max(0.001f, weatherContext.WeatherFieldScaleKm),
                weatherContext.WeatherFieldOffsetKm,
                Mathf.Clamp01(weatherContext.GlobalCoverageGain),
                weatherContext.CoverageBias,
                Mathf.Max(0.0f, weatherContext.CoverageContrast),
                Mathf.Clamp01(weatherContext.DetailErosionStrength),
                Mathf.Clamp01(weatherContext.CloudTypeRemapMin),
                Mathf.Clamp01(weatherContext.CloudTypeRemapMax),
                Mathf.Max(0, weatherContext.WeatherFieldDiscontinuityVersion),
                atmosphereParameters.GroundRadiusKm,
                atmosphereParameters.TopRadiusKm,
                viewParameters.SunDirection,
                viewParameters.SunIlluminance,
                viewParameters.CameraPositionKm,
                viewBasisRight,
                viewBasisUp,
                viewBasisForward,
                cameraBasisRight,
                cameraBasisUp,
                cameraBasisForward,
                tanHalfVerticalFov,
                aspectRatio,
                atmosphereParameters.GroundRadiusKm + cloudBottomHeightKm,
                atmosphereParameters.GroundRadiusKm + cloudTopHeightKm,
                cloudThicknessKm,
                windOffset);
        }

        private static int ComputeResourceHash(int traceWidth, int traceHeight)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + traceWidth;
                hash = (hash * 31) + traceHeight;
                return hash;
            }
        }

        private static int ComputeParameterHash(
            bool enableClouds,
            float cloudBottomHeightKm,
            float cloudTopHeightKm,
            float cloudCoverage,
            float densityMultiplier,
            float lightAbsorption,
            float ambientStrength,
            float forwardScatteringG,
            int stepCount,
            int shadowStepCount,
            float maxRenderDistanceKm,
            bool enableJitter,
            float jitterStrength,
            int jitterSequenceLength,
            int jitterFrameIndex,
            int jitterIndex,
            Vector2 jitterOffset,
            bool enableTemporalAccumulation,
            float temporalResponse,
            float temporalTransmittanceRejectThreshold,
            float temporalCameraResetDistanceKm,
            float temporalCameraResetAngleDegrees,
            float temporalFovResetDegrees,
            float shapeBaseScaleKm,
            float detailScaleKm,
            Vector2 windDirection,
            float windSpeedKmPerSecond,
            Texture3D baseShapeNoise,
            Texture3D detailShapeNoise,
            bool enableRuntimeWeatherField,
            Texture weatherFieldTexture,
            Texture2D cloudHeightDensityLut,
            float weatherFieldScaleKm,
            Vector2 weatherFieldOffsetKm,
            float globalCoverageGain,
            float coverageBias,
            float coverageContrast,
            float detailErosionStrength,
            float cloudTypeRemapMin,
            float cloudTypeRemapMax,
            int weatherFieldDiscontinuityVersion,
            float groundRadiusKm,
            float topRadiusKm,
            Vector3 sunDirection,
            Vector3 sunIlluminance,
            Vector3 cameraPositionKm,
            Vector3 viewBasisRight,
            Vector3 viewBasisUp,
            Vector3 viewBasisForward,
            Vector3 cameraBasisRight,
            Vector3 cameraBasisUp,
            Vector3 cameraBasisForward,
            float tanHalfVerticalFov,
            float aspectRatio,
            float cloudBottomRadiusKm,
            float cloudTopRadiusKm,
            float cloudThicknessKm,
            Vector2 windOffset,
            int resourceHash)
        {
            unchecked
            {
                int hash = resourceHash;
                hash = AppendFloat(hash, cloudBottomHeightKm);
                hash = AppendFloat(hash, cloudTopHeightKm);
                hash = AppendFloat(hash, cloudCoverage);
                hash = AppendFloat(hash, densityMultiplier);
                hash = AppendFloat(hash, lightAbsorption);
                hash = AppendFloat(hash, ambientStrength);
                hash = AppendFloat(hash, forwardScatteringG);
                hash = (hash * 31) + stepCount;
                hash = (hash * 31) + shadowStepCount;
                hash = AppendFloat(hash, maxRenderDistanceKm);
                hash = (hash * 31) + (enableJitter ? 1 : 0);
                hash = AppendFloat(hash, jitterStrength);
                hash = (hash * 31) + jitterSequenceLength;
                hash = (hash * 31) + jitterFrameIndex;
                hash = (hash * 31) + jitterIndex;
                hash = AppendVector2(hash, jitterOffset);
                hash = (hash * 31) + (enableTemporalAccumulation ? 1 : 0);
                hash = AppendFloat(hash, temporalResponse);
                hash = AppendFloat(hash, temporalTransmittanceRejectThreshold);
                hash = AppendFloat(hash, temporalCameraResetDistanceKm);
                hash = AppendFloat(hash, temporalCameraResetAngleDegrees);
                hash = AppendFloat(hash, temporalFovResetDegrees);
                hash = AppendFloat(hash, shapeBaseScaleKm);
                hash = AppendFloat(hash, detailScaleKm);
                hash = AppendVector2(hash, windDirection);
                hash = AppendFloat(hash, windSpeedKmPerSecond);
                hash = (hash * 31) + (enableRuntimeWeatherField ? 1 : 0);
                hash = AppendFloat(hash, weatherFieldScaleKm);
                hash = AppendVector2(hash, weatherFieldOffsetKm);
                hash = AppendFloat(hash, globalCoverageGain);
                hash = AppendFloat(hash, coverageBias);
                hash = AppendFloat(hash, coverageContrast);
                hash = AppendFloat(hash, detailErosionStrength);
                hash = AppendFloat(hash, cloudTypeRemapMin);
                hash = AppendFloat(hash, cloudTypeRemapMax);
                hash = (hash * 31) + weatherFieldDiscontinuityVersion;
                hash = AppendFloat(hash, groundRadiusKm);
                hash = AppendFloat(hash, topRadiusKm);
                hash = AppendVector3(hash, sunDirection);
                hash = AppendVector3(hash, sunIlluminance);
                hash = AppendVector3(hash, cameraPositionKm);
                hash = AppendVector3(hash, viewBasisRight);
                hash = AppendVector3(hash, viewBasisUp);
                hash = AppendVector3(hash, viewBasisForward);
                hash = AppendVector3(hash, cameraBasisRight);
                hash = AppendVector3(hash, cameraBasisUp);
                hash = AppendVector3(hash, cameraBasisForward);
                hash = AppendFloat(hash, tanHalfVerticalFov);
                hash = AppendFloat(hash, aspectRatio);
                hash = AppendFloat(hash, cloudBottomRadiusKm);
                hash = AppendFloat(hash, cloudTopRadiusKm);
                hash = AppendFloat(hash, cloudThicknessKm);
                hash = AppendVector2(hash, windOffset);
                hash = (hash * 31) + (enableClouds ? 1 : 0);
                hash = (hash * 31) + (baseShapeNoise != null ? baseShapeNoise.GetHashCode() : 0);
                hash = (hash * 31) + (detailShapeNoise != null ? detailShapeNoise.GetHashCode() : 0);
                hash = (hash * 31) + (cloudHeightDensityLut != null ? cloudHeightDensityLut.GetHashCode() : 0);
                return hash;
            }
        }

        private static int ComputeHistoryResetHash(
            bool enableClouds,
            float cloudBottomHeightKm,
            float cloudTopHeightKm,
            float cloudCoverage,
            float densityMultiplier,
            float lightAbsorption,
            float ambientStrength,
            float forwardScatteringG,
            int stepCount,
            int shadowStepCount,
            float maxRenderDistanceKm,
            bool enableJitter,
            float jitterStrength,
            int jitterSequenceLength,
            bool enableTemporalAccumulation,
            float shapeBaseScaleKm,
            float detailScaleKm,
            Vector2 windDirection,
            float windSpeedKmPerSecond,
            Texture3D baseShapeNoise,
            Texture3D detailShapeNoise,
            bool enableRuntimeWeatherField,
            Texture weatherFieldTexture,
            Texture2D cloudHeightDensityLut,
            float weatherFieldScaleKm,
            Vector2 weatherFieldOffsetKm,
            float globalCoverageGain,
            float coverageBias,
            float coverageContrast,
            float detailErosionStrength,
            float cloudTypeRemapMin,
            float cloudTypeRemapMax,
            int weatherFieldDiscontinuityVersion,
            float groundRadiusKm,
            float topRadiusKm,
            Vector3 sunDirection,
            Vector3 sunIlluminance,
            float cloudBottomRadiusKm,
            float cloudTopRadiusKm,
            float cloudThicknessKm,
            int resourceHash)
        {
            unchecked
            {
                int hash = resourceHash;
                hash = (hash * 31) + (enableClouds ? 1 : 0);
                hash = AppendFloat(hash, cloudBottomHeightKm);
                hash = AppendFloat(hash, cloudTopHeightKm);
                hash = AppendFloat(hash, cloudCoverage);
                hash = AppendFloat(hash, densityMultiplier);
                hash = AppendFloat(hash, lightAbsorption);
                hash = AppendFloat(hash, ambientStrength);
                hash = AppendFloat(hash, forwardScatteringG);
                hash = (hash * 31) + stepCount;
                hash = (hash * 31) + shadowStepCount;
                hash = AppendFloat(hash, maxRenderDistanceKm);
                hash = (hash * 31) + (enableJitter ? 1 : 0);
                hash = AppendFloat(hash, jitterStrength);
                hash = (hash * 31) + jitterSequenceLength;
                hash = (hash * 31) + (enableTemporalAccumulation ? 1 : 0);
                hash = AppendFloat(hash, shapeBaseScaleKm);
                hash = AppendFloat(hash, detailScaleKm);
                hash = AppendVector2(hash, windDirection);
                hash = AppendFloat(hash, windSpeedKmPerSecond);
                hash = (hash * 31) + (enableRuntimeWeatherField ? 1 : 0);
                hash = AppendFloat(hash, weatherFieldScaleKm);
                hash = AppendVector2(hash, weatherFieldOffsetKm);
                hash = (hash * 31) + weatherFieldDiscontinuityVersion;
                hash = (hash * 31) + (baseShapeNoise != null ? baseShapeNoise.GetHashCode() : 0);
                hash = (hash * 31) + (detailShapeNoise != null ? detailShapeNoise.GetHashCode() : 0);
                hash = (hash * 31) + (cloudHeightDensityLut != null ? cloudHeightDensityLut.GetHashCode() : 0);
                hash = AppendFloat(hash, groundRadiusKm);
                hash = AppendFloat(hash, topRadiusKm);
                hash = AppendVector3(hash, sunDirection);
                hash = AppendVector3(hash, sunIlluminance);
                hash = AppendFloat(hash, cloudBottomRadiusKm);
                hash = AppendFloat(hash, cloudTopRadiusKm);
                hash = AppendFloat(hash, cloudThicknessKm);
                return hash;
            }
        }

        private static int AppendFloat(int hash, float value)
        {
            return (hash * 31) + Mathf.RoundToInt(value * 100000.0f);
        }

        private static int AppendVector2(int hash, Vector2 value)
        {
            hash = AppendFloat(hash, value.x);
            hash = AppendFloat(hash, value.y);
            return hash;
        }

        private static int AppendVector3(int hash, Vector3 value)
        {
            hash = AppendFloat(hash, value.x);
            hash = AppendFloat(hash, value.y);
            hash = AppendFloat(hash, value.z);
            return hash;
        }
    }
}
