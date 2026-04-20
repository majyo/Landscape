using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace VolumetricClouds.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VolumetricCloudController : MonoBehaviour
    {
        private enum DebugOverlayMode
        {
            Current = 0,
            CurrentTransmittance = 1,
            CurrentOpacity = 2,
            History = 3,
            Accumulated = 4,
            HistoryWeight = 5,
            WeatherFieldCoverage = 6,
            WeatherFieldCloudType = 7,
            WeatherFieldWetness = 8,
            WeatherFieldFront = 9,
            ActivePreset = 10,
            PresetBlend = 11
        }

        private readonly struct WeatherState
        {
            public readonly float Coverage;
            public readonly float CloudType;
            public readonly float Wetness;
            public readonly float DensityBias;
            public readonly Vector2 WindDirection;
            public readonly float WindSpeedKmPerSecond;
            public readonly float EvolutionSpeed;
            public readonly float TransitionDurationSeconds;
            public readonly float CoverageBias;
            public readonly float CoverageContrast;
            public readonly float DetailErosionStrength;

            public WeatherState(
                float coverage,
                float cloudType,
                float wetness,
                float densityBias,
                Vector2 windDirection,
                float windSpeedKmPerSecond,
                float evolutionSpeed,
                float transitionDurationSeconds,
                float coverageBias,
                float coverageContrast,
                float detailErosionStrength)
            {
                Coverage = Mathf.Clamp01(coverage);
                CloudType = Mathf.Clamp01(cloudType);
                Wetness = Mathf.Clamp01(wetness);
                DensityBias = Mathf.Clamp01(densityBias);
                WindDirection = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : new Vector2(1.0f, 0.0f);
                WindSpeedKmPerSecond = Mathf.Max(0.0f, windSpeedKmPerSecond);
                EvolutionSpeed = Mathf.Max(0.0f, evolutionSpeed);
                TransitionDurationSeconds = Mathf.Max(0.0f, transitionDurationSeconds);
                CoverageBias = coverageBias;
                CoverageContrast = Mathf.Max(0.0f, coverageContrast);
                DetailErosionStrength = Mathf.Clamp01(detailErosionStrength);
            }

            public static WeatherState FromPreset(WeatherPreset preset, VolumetricCloudProfile profile)
            {
                if (preset == null)
                    return FromProfile(profile);

                return new WeatherState(
                    preset.targetCoverage,
                    preset.targetCloudType,
                    preset.targetWetness,
                    preset.targetDensityBias,
                    preset.windDirection,
                    preset.windSpeedKmPerSecond,
                    preset.evolutionSpeed,
                    preset.transitionDurationSeconds,
                    preset.coverageBias,
                    preset.coverageContrast,
                    preset.detailErosionStrength);
            }

            public static WeatherState FromProfile(VolumetricCloudProfile profile)
            {
                return new WeatherState(
                    profile != null ? profile.cloudCoverage : 0.45f,
                    0.45f,
                    0.15f,
                    0.1f,
                    profile != null ? profile.windDirection : new Vector2(1.0f, 0.0f),
                    profile != null ? profile.windSpeedKmPerSecond : 0.02f,
                    1.0f,
                    30.0f,
                    0.0f,
                    1.0f,
                    profile != null ? profile.detailErosionStrength : 0.35f);
            }

            public static WeatherState Lerp(in WeatherState a, in WeatherState b, float t)
            {
                float blend = Mathf.Clamp01(t);
                Vector2 wind = Vector2.Lerp(a.WindDirection, b.WindDirection, blend);
                if (wind.sqrMagnitude <= 1e-6f)
                    wind = b.WindDirection.sqrMagnitude > 1e-6f ? b.WindDirection : a.WindDirection;

                return new WeatherState(
                    Mathf.Lerp(a.Coverage, b.Coverage, blend),
                    Mathf.Lerp(a.CloudType, b.CloudType, blend),
                    Mathf.Lerp(a.Wetness, b.Wetness, blend),
                    Mathf.Lerp(a.DensityBias, b.DensityBias, blend),
                    wind,
                    Mathf.Lerp(a.WindSpeedKmPerSecond, b.WindSpeedKmPerSecond, blend),
                    Mathf.Lerp(a.EvolutionSpeed, b.EvolutionSpeed, blend),
                    Mathf.Lerp(a.TransitionDurationSeconds, b.TransitionDurationSeconds, blend),
                    Mathf.Lerp(a.CoverageBias, b.CoverageBias, blend),
                    Mathf.Lerp(a.CoverageContrast, b.CoverageContrast, blend),
                    Mathf.Lerp(a.DetailErosionStrength, b.DetailErosionStrength, blend));
            }
        }

        private const string DefaultProfilePath = "VolumetricClouds/VolumetricCloudProfile_Default";
        private const string DefaultCloudyWeatherPresetPath = "VolumetricClouds/WeatherPreset_Cloudy";
        private const string RaymarchComputeShaderPath = "VolumetricClouds/VolumetricCloudRaymarch";
        private const string TemporalAccumulationComputeShaderPath = "VolumetricClouds/VolumetricCloudTemporalAccumulation";
        private const string WeatherFieldUpdateComputeShaderPath = "VolumetricClouds/VolumetricCloudWeatherFieldUpdate";
        private const string KernelName = "CSMain";
        private const float WeatherTransitionTemporalScale = 0.65f;
        private const float OverlayPaddingLeft = 8.0f;
        private const float OverlayPaddingRight = 8.0f;
        private const float OverlayPaddingTop = 6.0f;
        private const float OverlayPaddingBottom = 8.0f;
        private const float OverlayTitleHeight = 20.0f;
        private const float OverlayMetadataSpacing = 4.0f;
        private const float OverlayMetadataHeight = 18.0f;
        private static VolumetricCloudController instance;

        [SerializeField] private VolumetricCloudProfile profile;
        [SerializeField] private bool showDebugOverlay = true;
        [SerializeField] private Vector2 debugOverlaySize = new Vector2(384.0f, 216.0f);
        [SerializeField] private DebugOverlayMode debugOverlayMode = DebugOverlayMode.Current;

        private VolumetricCloudResources resources;
        private VolumetricCloudWeatherResources weatherResources;
        private ComputeShader raymarchComputeShader;
        private ComputeShader temporalAccumulationComputeShader;
        private ComputeShader weatherFieldUpdateComputeShader;
        private int raymarchKernelIndex = -1;
        private int temporalAccumulationKernelIndex = -1;
        private int weatherFieldUpdateKernelIndex = -1;
        private int lastParameterHash = int.MinValue;
        private int lastResourceHash = int.MinValue;
        private VolumetricCloudJitterState jitterState = VolumetricCloudJitterState.Legacy;
        private readonly VolumetricCloudTemporalState temporalState = new VolumetricCloudTemporalState();
        private WeatherPreset activeWeatherPreset;
        private WeatherPreset targetWeatherPreset;
        private WeatherState startWeatherState;
        private WeatherState currentWeatherState;
        private WeatherState targetWeatherState;
        private bool weatherStateInitialized;
        private float weatherTransition01 = 1.0f;
        private double weatherTransitionStartTimeSeconds;
        private float weatherTransitionDurationSeconds;
        private int lastWeatherFieldUpdateFrame = -1;
        private double lastWeatherFieldUpdateTimeSeconds;
        private bool weatherFieldTimeInitialized;
        private int weatherFieldDiscontinuityVersion;
        private int lastWeatherConfigurationHash = int.MinValue;
        private bool loggedMissingAtmosphere;
        private bool loggedMissingProfile;
        private bool loggedMissingBaseNoise;
        private bool loggedMissingDetailNoise;
        private bool loggedMissingComputeShader;
        private bool loggedMissingWeatherFieldComputeShader;
        private GUIStyle overlayLabelStyle;
        private GUIStyle overlayTitleStyle;

        public static VolumetricCloudController Instance => instance;
        public VolumetricCloudProfile Profile => profile;
        public VolumetricCloudResources Resources => resources;
        public WeatherPreset ActiveWeatherPreset => activeWeatherPreset;
        public WeatherPreset TargetWeatherPreset => targetWeatherPreset;
        public float WeatherTransition01 => weatherTransition01;
        public RenderTexture TraceTexture => resources != null ? resources.TraceTexture : null;
        public RTHandle TraceHandle => resources != null ? resources.TraceHandle : null;
        public RTHandle StabilizedHandle => resources != null ? resources.StabilizedHandle : null;
        public RTHandle HistoryReadHandle => resources != null ? resources.HistoryReadHandle : null;
        public RTHandle HistoryWriteHandle => resources != null ? resources.HistoryWriteHandle : null;
        public RTHandle HistoryWeightHandle => resources != null ? resources.HistoryWeightHandle : null;
        public RTHandle CompositeHandle => resources != null ? resources.CompositeHandle : null;
        public Texture WeatherFieldTexture => weatherResources != null ? weatherResources.WeatherFieldTexture : null;
        public RTHandle WeatherFieldHandle => weatherResources != null ? weatherResources.WeatherFieldHandle : null;
        public RTHandle WeatherFieldScratchHandle => weatherResources != null ? weatherResources.WeatherFieldScratchHandle : null;
        public VolumetricCloudTemporalState TemporalState => temporalState;
        public int WeatherFieldDiscontinuityVersion => weatherFieldDiscontinuityVersion;
        public int LastParameterHash => lastParameterHash;
        public int LastResourceHash => lastResourceHash;

        private void Reset()
        {
            LoadDefaultProfileIfNeeded();
            EnsureDefaultWeatherPresetIfNeeded();
        }

        private void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("VolumetricClouds: multiple VolumetricCloudController instances found. Disabling duplicate.", this);
                enabled = false;
                return;
            }

            instance = this;
            resources ??= new VolumetricCloudResources();
            weatherResources ??= new VolumetricCloudWeatherResources();
            LoadDefaultProfileIfNeeded();
            EnsureDefaultWeatherPresetIfNeeded();
            UpdateWeatherConfigurationState(forceReset: false);
            ResetLogFlags();
            EnsureWeatherStateInitialized(Time.realtimeSinceStartupAsDouble);
        }

        private void OnDisable()
        {
            if (instance == this)
                instance = null;

            resources?.Release();
            weatherResources?.Release();
            temporalState.Reset();
            weatherFieldTimeInitialized = false;
            lastWeatherFieldUpdateFrame = -1;
            lastWeatherConfigurationHash = int.MinValue;
        }

        private void OnValidate()
        {
            LoadDefaultProfileIfNeeded();
            EnsureDefaultWeatherPresetIfNeeded();
            if (!Application.isPlaying)
            {
                weatherStateInitialized = false;
                activeWeatherPreset = null;
                targetWeatherPreset = null;
                weatherTransition01 = 1.0f;
            }
            UpdateWeatherConfigurationState(forceReset: true);
        }

        public bool TryPrepare(Camera camera, bool advanceJitter, out VolumetricCloudParameters parameters, out bool resourcesRecreated)
        {
            parameters = default;
            resourcesRecreated = false;

            if (!enabled)
                return false;

            LoadDefaultProfileIfNeeded();
            EnsureDefaultWeatherPresetIfNeeded();
            if (profile == null)
            {
                LogMissingProfile();
                return false;
            }

            AtmosphereController atmosphereController = AtmosphereController.Instance;
            if (atmosphereController == null)
            {
                LogMissingAtmosphere();
                return false;
            }

            if (camera == null)
                return false;

            if (profile.baseShapeNoise == null)
            {
                LogMissingBaseNoise();
                return false;
            }

            if (profile.detailShapeNoise == null && !loggedMissingDetailNoise)
            {
                Debug.LogWarning("VolumetricClouds: detail shape noise is not assigned. MVP density detail will be limited.", this);
                loggedMissingDetailNoise = true;
            }

            if (!atmosphereController.TryGetRuntimeContext(camera, out AtmosphereParameters atmosphereParameters, out AtmosphereViewParameters viewParameters))
                return false;

            resources ??= new VolumetricCloudResources();
            if (advanceJitter)
                AdvanceJitterState();
            else if (!profile.enableJitter)
                jitterState = VolumetricCloudJitterState.Legacy;

            if (!TryBuildWeatherContext(out VolumetricCloudWeatherContext weatherContext, out _, out _))
                weatherContext = BuildFallbackWeatherContext(0.0f, false);

            float runtimeTimeSeconds = Application.isPlaying
                ? Time.time
                : (float)Time.realtimeSinceStartupAsDouble;
            parameters = VolumetricCloudParameters.FromRuntime(profile, atmosphereParameters, viewParameters, camera, runtimeTimeSeconds, jitterState, weatherContext);
            if (!parameters.EnableClouds)
                return false;

            if (!resources.EnsureTraceTarget(parameters, out resourcesRecreated))
                return false;

            lastParameterHash = parameters.ParameterHash;
            lastResourceHash = parameters.ResourceHash;
            return true;
        }

        internal bool TryPrepareWeatherField(out VolumetricCloudWeatherContext context, out bool resourcesRecreated, out bool shouldUpdate)
        {
            return TryBuildWeatherContext(out context, out resourcesRecreated, out shouldUpdate);
        }

        public void CommitScheduledWeatherFieldUpdate(double updateTimeSeconds)
        {
            if (weatherResources == null)
                return;

            weatherResources.SwapWeatherFieldBuffers();
            weatherResources.MarkInitialized();
            lastWeatherFieldUpdateFrame = Time.frameCount;
            lastWeatherFieldUpdateTimeSeconds = updateTimeSeconds;
            weatherFieldTimeInitialized = true;
        }

        public void SetWeatherPreset(WeatherPreset preset, float transitionDurationSeconds = -1.0f)
        {
            if (preset == null)
                return;

            double nowSeconds = Time.realtimeSinceStartupAsDouble;
            EnsureWeatherStateInitialized(nowSeconds);
            UpdateWeatherTransitionState(nowSeconds);

            WeatherPreset previousPreset = targetWeatherPreset ?? activeWeatherPreset ?? preset;
            startWeatherState = currentWeatherState;
            targetWeatherPreset = preset;
            targetWeatherState = WeatherState.FromPreset(preset, profile);
            activeWeatherPreset = previousPreset;

            float resolvedDurationSeconds = transitionDurationSeconds >= 0.0f
                ? transitionDurationSeconds
                : targetWeatherState.TransitionDurationSeconds;

            if (resolvedDurationSeconds <= 0.0f)
            {
                currentWeatherState = targetWeatherState;
                startWeatherState = targetWeatherState;
                activeWeatherPreset = preset;
                weatherTransition01 = 1.0f;
                weatherTransitionDurationSeconds = 0.0f;
                weatherTransitionStartTimeSeconds = nowSeconds;
                return;
            }

            weatherTransitionStartTimeSeconds = nowSeconds;
            weatherTransitionDurationSeconds = resolvedDurationSeconds;
            weatherTransition01 = 0.0f;
        }

        internal void ResetWeatherField()
        {
            weatherResources?.Invalidate();
            lastWeatherFieldUpdateFrame = -1;
            weatherFieldTimeInitialized = false;
            MarkWeatherFieldDiscontinuity();
            resources?.InvalidateHistory();
            temporalState.Reset();
        }

        public VolumetricCloudTemporalState.HistoryResetReason BeginTemporalFrame(Camera camera, in VolumetricCloudParameters parameters, bool resourcesRecreated)
        {
            EntityId cameraInstanceId = camera != null ? camera.GetEntityId() : default;
            VolumetricCloudTemporalState.HistoryResetReason resetReason = temporalState.BeginFrame(cameraInstanceId, parameters, resourcesRecreated);
            if (resetReason != VolumetricCloudTemporalState.HistoryResetReason.None)
                resources?.InvalidateHistory();

            return resetReason;
        }

        public void CommitTemporalFrame()
        {
            temporalState.CommitFrame();
        }

        public void BindGlobals(CommandBuffer cmd, in VolumetricCloudParameters parameters)
        {
            resources?.BindGlobals(cmd, parameters);
        }

        public bool TryGetRaymarchComputeShader(out ComputeShader computeShader, out int kernelIndex)
        {
            computeShader = null;
            kernelIndex = -1;

            if (!EnsureComputeShader())
                return false;

            computeShader = raymarchComputeShader;
            kernelIndex = raymarchKernelIndex;
            return computeShader != null && kernelIndex >= 0;
        }

        public bool TryGetTemporalAccumulationComputeShader(out ComputeShader computeShader, out int kernelIndex)
        {
            computeShader = null;
            kernelIndex = -1;

            if (!EnsureTemporalAccumulationComputeShader())
                return false;

            computeShader = temporalAccumulationComputeShader;
            kernelIndex = temporalAccumulationKernelIndex;
            return computeShader != null && kernelIndex >= 0;
        }

        public bool TryGetWeatherFieldUpdateComputeShader(out ComputeShader computeShader, out int kernelIndex)
        {
            computeShader = null;
            kernelIndex = -1;

            if (!EnsureWeatherFieldUpdateComputeShader())
                return false;

            computeShader = weatherFieldUpdateComputeShader;
            kernelIndex = weatherFieldUpdateKernelIndex;
            return computeShader != null && kernelIndex >= 0;
        }

        public void SkipTemporalAccumulation()
        {
            resources?.UseCurrentTraceForComposite();
            resources?.InvalidateHistory();
        }

        public void CompleteTemporalAccumulation()
        {
            if (resources == null)
                return;

            resources.SwapHistoryBuffers();
            resources.MarkHistoryValid();
        }

        private bool TryBuildWeatherContext(out VolumetricCloudWeatherContext context, out bool resourcesRecreated, out bool shouldUpdate)
        {
            context = BuildFallbackWeatherContext(0.0f, false);
            resourcesRecreated = false;
            shouldUpdate = false;

            if (!enabled)
                return false;

            LoadDefaultProfileIfNeeded();
            EnsureDefaultWeatherPresetIfNeeded();
            if (profile == null)
                return false;

            UpdateWeatherConfigurationState(forceReset: false);

            double nowSeconds = Time.realtimeSinceStartupAsDouble;
            EnsureWeatherStateInitialized(nowSeconds);
            UpdateWeatherTransitionState(nowSeconds);

            float effectiveTemporalResponse = Mathf.Clamp(
                profile.temporalResponse * (IsWeatherTransitionInProgress() ? WeatherTransitionTemporalScale : 1.0f),
                0.0f,
                0.99f);

            if (!profile.useRuntimeWeatherField)
            {
                context = BuildFallbackWeatherContext(effectiveTemporalResponse, true);
                return true;
            }

            weatherResources ??= new VolumetricCloudWeatherResources();
            if (!weatherResources.EnsureTextures(profile.weatherFieldResolution, out resourcesRecreated))
                return false;

            if (resourcesRecreated)
            {
                weatherResources.Invalidate();
                lastWeatherFieldUpdateFrame = -1;
                weatherFieldTimeInitialized = false;
                MarkWeatherFieldDiscontinuity();
                resources?.InvalidateHistory();
            }

            double minUpdateIntervalSeconds = profile.weatherFieldUpdateRate > 0.0f
                ? 1.0 / profile.weatherFieldUpdateRate
                : 0.0;
            double elapsedSinceLastUpdateSeconds = weatherFieldTimeInitialized
                ? nowSeconds - lastWeatherFieldUpdateTimeSeconds
                : double.MaxValue;
            shouldUpdate =
                lastWeatherFieldUpdateFrame != Time.frameCount
                && (!weatherResources.Initialized || elapsedSinceLastUpdateSeconds + 1e-6 >= minUpdateIntervalSeconds);

            float deltaTimeSeconds = shouldUpdate
                ? Mathf.Clamp((float)(weatherFieldTimeInitialized ? elapsedSinceLastUpdateSeconds : 0.0), 0.0f, 0.1f)
                : 0.0f;

            context = new VolumetricCloudWeatherContext(
                weatherResources.WeatherFieldTexture != null,
                weatherResources.WeatherFieldTexture,
                profile.defaultWeatherSeed,
                profile.cloudHeightDensityLut,
                Mathf.Max(1, profile.weatherFieldResolution),
                Mathf.Max(0.001f, profile.weatherFieldScaleKm),
                Vector2.zero,
                currentWeatherState.Coverage,
                1.0f,
                currentWeatherState.CoverageBias,
                currentWeatherState.CoverageContrast,
                currentWeatherState.CloudType,
                currentWeatherState.Wetness,
                currentWeatherState.DensityBias,
                currentWeatherState.DetailErosionStrength,
                profile.cloudTypeRemapMin,
                profile.cloudTypeRemapMax,
                currentWeatherState.WindDirection,
                currentWeatherState.WindSpeedKmPerSecond,
                currentWeatherState.EvolutionSpeed,
                weatherTransition01,
                effectiveTemporalResponse,
                deltaTimeSeconds,
                !weatherResources.Initialized,
                weatherFieldDiscontinuityVersion);
            return true;
        }

        private VolumetricCloudWeatherContext BuildFallbackWeatherContext(float effectiveTemporalResponse, bool useCurrentState)
        {
            WeatherState state = useCurrentState && weatherStateInitialized
                ? currentWeatherState
                : WeatherState.FromProfile(profile);

            return new VolumetricCloudWeatherContext(
                false,
                null,
                profile != null ? profile.defaultWeatherSeed : null,
                profile != null ? profile.cloudHeightDensityLut : null,
                profile != null ? Mathf.Max(1, profile.weatherFieldResolution) : 256,
                profile != null ? Mathf.Max(0.001f, profile.weatherFieldScaleKm) : 256.0f,
                Vector2.zero,
                state.Coverage,
                1.0f,
                state.CoverageBias,
                state.CoverageContrast,
                state.CloudType,
                state.Wetness,
                state.DensityBias,
                state.DetailErosionStrength,
                profile != null ? profile.cloudTypeRemapMin : 0.0f,
                profile != null ? profile.cloudTypeRemapMax : 1.0f,
                state.WindDirection,
                state.WindSpeedKmPerSecond,
                state.EvolutionSpeed,
                weatherTransition01,
                effectiveTemporalResponse > 0.0f ? effectiveTemporalResponse : (profile != null ? profile.temporalResponse : 0.9f),
                0.0f,
                false,
                weatherFieldDiscontinuityVersion);
        }

        private void EnsureWeatherStateInitialized(double nowSeconds)
        {
            if (weatherStateInitialized)
                return;

            WeatherPreset defaultPreset = ResolveDefaultWeatherPreset();
            WeatherState initialState = WeatherState.FromPreset(defaultPreset, profile);
            startWeatherState = initialState;
            currentWeatherState = initialState;
            targetWeatherState = initialState;
            activeWeatherPreset = defaultPreset;
            targetWeatherPreset = defaultPreset;
            weatherTransition01 = 1.0f;
            weatherTransitionDurationSeconds = 0.0f;
            weatherTransitionStartTimeSeconds = nowSeconds;
            weatherStateInitialized = true;
        }

        private void UpdateWeatherTransitionState(double nowSeconds)
        {
            if (!weatherStateInitialized)
                EnsureWeatherStateInitialized(nowSeconds);

            if (!IsWeatherTransitionInProgress())
            {
                currentWeatherState = targetWeatherState;
                weatherTransition01 = 1.0f;
                return;
            }

            if (weatherTransitionDurationSeconds <= 0.0f)
            {
                currentWeatherState = targetWeatherState;
                startWeatherState = targetWeatherState;
                activeWeatherPreset = targetWeatherPreset;
                weatherTransition01 = 1.0f;
                weatherTransitionDurationSeconds = 0.0f;
                return;
            }

            float progress = Mathf.Clamp01((float)((nowSeconds - weatherTransitionStartTimeSeconds) / weatherTransitionDurationSeconds));
            weatherTransition01 = progress;
            currentWeatherState = WeatherState.Lerp(startWeatherState, targetWeatherState, progress);
            if (progress >= 1.0f)
            {
                startWeatherState = targetWeatherState;
                currentWeatherState = targetWeatherState;
                activeWeatherPreset = targetWeatherPreset;
                weatherTransitionDurationSeconds = 0.0f;
            }
        }

        private bool IsWeatherTransitionInProgress()
        {
            return targetWeatherPreset != null
                && activeWeatherPreset != targetWeatherPreset
                && weatherTransition01 < 1.0f;
        }

        private void MarkWeatherFieldDiscontinuity()
        {
            unchecked
            {
                weatherFieldDiscontinuityVersion++;
                if (weatherFieldDiscontinuityVersion < 0)
                    weatherFieldDiscontinuityVersion = 1;
            }
        }

        private void UpdateWeatherConfigurationState(bool forceReset)
        {
            int configurationHash = ComputeWeatherConfigurationHash();
            if (configurationHash == int.MinValue)
            {
                lastWeatherConfigurationHash = int.MinValue;
                return;
            }

            if (lastWeatherConfigurationHash == int.MinValue)
            {
                lastWeatherConfigurationHash = configurationHash;
                if (!forceReset)
                    return;
            }

            if (!forceReset && lastWeatherConfigurationHash == configurationHash)
                return;

            lastWeatherConfigurationHash = configurationHash;
            weatherResources?.Invalidate();
            lastWeatherFieldUpdateFrame = -1;
            weatherFieldTimeInitialized = false;
            MarkWeatherFieldDiscontinuity();
            resources?.InvalidateHistory();
        }

        private int ComputeWeatherConfigurationHash()
        {
            if (profile == null)
                return int.MinValue;

            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (profile.useRuntimeWeatherField ? 1 : 0);
                hash = (hash * 31) + (profile.defaultWeatherSeed != null ? profile.defaultWeatherSeed.GetHashCode() : 0);
                hash = (hash * 31) + (profile.cloudHeightDensityLut != null ? profile.cloudHeightDensityLut.GetHashCode() : 0);
                hash = (hash * 31) + (profile.defaultWeatherPreset != null ? profile.defaultWeatherPreset.GetHashCode() : 0);
                return hash;
            }
        }

        private void AdvanceJitterState()
        {
            if (profile == null || !profile.enableJitter)
            {
                jitterState = VolumetricCloudJitterState.Legacy;
                return;
            }

            int frameIndex = jitterState.FrameIndex + 1;
            int sequenceLength = Mathf.Max(1, profile.jitterSequenceLength);
            int jitterIndex = PositiveModulo(frameIndex, sequenceLength);
            Vector2 jitterOffset = new Vector2(
                Halton(jitterIndex + 1, 2),
                Halton(jitterIndex + 1, 3));
            jitterState = new VolumetricCloudJitterState(frameIndex, jitterIndex, jitterOffset);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
                return 0;

            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static float Halton(int index, int radix)
        {
            if (index <= 0 || radix <= 1)
                return 0.5f;

            float result = 0.0f;
            float fraction = 1.0f / radix;
            int current = index;
            while (current > 0)
            {
                result += fraction * (current % radix);
                current /= radix;
                fraction /= radix;
            }

            return result;
        }

        private void LoadDefaultProfileIfNeeded()
        {
            if (profile != null)
                return;

            profile = UnityEngine.Resources.Load<VolumetricCloudProfile>(DefaultProfilePath);
        }

        private void EnsureDefaultWeatherPresetIfNeeded()
        {
            if (profile == null || profile.defaultWeatherPreset != null)
                return;

            profile.defaultWeatherPreset = UnityEngine.Resources.Load<WeatherPreset>(DefaultCloudyWeatherPresetPath);
        }

        private WeatherPreset ResolveDefaultWeatherPreset()
        {
            EnsureDefaultWeatherPresetIfNeeded();
            return profile != null ? profile.defaultWeatherPreset : null;
        }

        private bool EnsureComputeShader()
        {
            if (!SystemInfo.supportsComputeShaders)
                return false;

            if (raymarchComputeShader == null)
            {
                raymarchComputeShader = UnityEngine.Resources.Load<ComputeShader>(RaymarchComputeShaderPath);
                if (raymarchComputeShader != null)
                    raymarchKernelIndex = raymarchComputeShader.FindKernel(KernelName);
            }

            if (raymarchComputeShader != null && raymarchKernelIndex >= 0)
                return true;

            if (!loggedMissingComputeShader)
            {
                Debug.LogError("VolumetricClouds: failed to load Resources/VolumetricClouds/VolumetricCloudRaymarch.compute.", this);
                loggedMissingComputeShader = true;
            }

            return false;
        }

        private bool EnsureTemporalAccumulationComputeShader()
        {
            if (!SystemInfo.supportsComputeShaders)
                return false;

            if (temporalAccumulationComputeShader == null)
            {
                temporalAccumulationComputeShader = UnityEngine.Resources.Load<ComputeShader>(TemporalAccumulationComputeShaderPath);
                if (temporalAccumulationComputeShader != null)
                    temporalAccumulationKernelIndex = temporalAccumulationComputeShader.FindKernel(KernelName);
            }

            return temporalAccumulationComputeShader != null && temporalAccumulationKernelIndex >= 0;
        }

        private bool EnsureWeatherFieldUpdateComputeShader()
        {
            if (!SystemInfo.supportsComputeShaders)
                return false;

            if (weatherFieldUpdateComputeShader == null)
            {
                weatherFieldUpdateComputeShader = UnityEngine.Resources.Load<ComputeShader>(WeatherFieldUpdateComputeShaderPath);
                if (weatherFieldUpdateComputeShader != null)
                    weatherFieldUpdateKernelIndex = weatherFieldUpdateComputeShader.FindKernel(KernelName);
            }

            if (weatherFieldUpdateComputeShader != null && weatherFieldUpdateKernelIndex >= 0)
                return true;

            if (!loggedMissingWeatherFieldComputeShader)
            {
                Debug.LogError("VolumetricClouds: failed to load Resources/VolumetricClouds/VolumetricCloudWeatherFieldUpdate.compute.", this);
                loggedMissingWeatherFieldComputeShader = true;
            }

            return false;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
                return;

            RenderTexture overlayTexture = GetOverlayTexture();
            if (overlayTexture == null)
                return;

            EnsureOverlayStyles();

            float maxPanelWidth = Mathf.Max(128.0f, Screen.width - 32.0f - OverlayPaddingLeft - OverlayPaddingRight);
            float width = Mathf.Min(Mathf.Max(128.0f, debugOverlaySize.x), maxPanelWidth);
            float height = Mathf.Max(32.0f, debugOverlaySize.y);
            float panelHeight = OverlayPaddingTop + OverlayTitleHeight + height + OverlayMetadataSpacing + OverlayMetadataHeight + OverlayPaddingBottom;
            Rect panelRect = new Rect(16.0f, 16.0f, width + OverlayPaddingLeft + OverlayPaddingRight, panelHeight);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + OverlayPaddingLeft, panelRect.y + OverlayPaddingTop, width, OverlayTitleHeight);
            Rect textureRect = new Rect(titleRect.x, titleRect.yMax, width, height);
            Rect metadataRect = new Rect(textureRect.x, textureRect.yMax + OverlayMetadataSpacing, textureRect.width, OverlayMetadataHeight);

            GUI.Label(titleRect, $"Volumetric Cloud Debug ({GetOverlayModeLabel()})", overlayTitleStyle);
            DrawOverlayTexture(textureRect, overlayTexture);
            GUI.Label(metadataRect, GetOverlayMetadata(overlayTexture), overlayLabelStyle);
        }

        private string GetOverlayModeLabel()
        {
            return debugOverlayMode switch
            {
                DebugOverlayMode.History => "History",
                DebugOverlayMode.Accumulated => "Accumulated",
                DebugOverlayMode.HistoryWeight => "HistoryWeight",
                DebugOverlayMode.CurrentTransmittance => "Current Transmittance",
                DebugOverlayMode.CurrentOpacity => "Current Opacity",
                DebugOverlayMode.WeatherFieldCoverage => "Weather Coverage",
                DebugOverlayMode.WeatherFieldCloudType => "Weather Cloud Type",
                DebugOverlayMode.WeatherFieldWetness => "Weather Wetness",
                DebugOverlayMode.WeatherFieldFront => "Weather Front",
                DebugOverlayMode.ActivePreset => "Active Preset",
                DebugOverlayMode.PresetBlend => "Preset Blend",
                _ => "Current",
            };
        }

        private RenderTexture GetOverlayTexture()
        {
            return debugOverlayMode switch
            {
                DebugOverlayMode.History => resources != null ? resources.HistoryReadTexture : null,
                DebugOverlayMode.Accumulated => resources != null ? resources.StabilizedTexture : null,
                DebugOverlayMode.HistoryWeight => resources != null ? resources.HistoryWeightTexture : null,
                DebugOverlayMode.WeatherFieldCoverage => weatherResources != null ? weatherResources.WeatherFieldTexture : null,
                DebugOverlayMode.WeatherFieldCloudType => weatherResources != null ? weatherResources.WeatherFieldTexture : null,
                DebugOverlayMode.WeatherFieldWetness => weatherResources != null ? weatherResources.WeatherFieldTexture : null,
                DebugOverlayMode.WeatherFieldFront => weatherResources != null ? weatherResources.WeatherFieldTexture : null,
                DebugOverlayMode.ActivePreset => weatherResources != null ? weatherResources.WeatherFieldTexture : null,
                DebugOverlayMode.PresetBlend => weatherResources != null ? weatherResources.WeatherFieldTexture : null,
                _ => resources != null ? resources.TraceTexture : null,
            };
        }

        private string GetOverlayMetadata(RenderTexture overlayTexture)
        {
            if (debugOverlayMode >= DebugOverlayMode.WeatherFieldCoverage)
            {
                string activePresetName = ActiveWeatherPreset != null ? ActiveWeatherPreset.presetName : "Fallback";
                string targetPresetName = TargetWeatherPreset != null ? TargetWeatherPreset.presetName : activePresetName;
                return $"{overlayTexture.width}x{overlayTexture.height} {overlayTexture.format} | Active={activePresetName} | Target={targetPresetName} | Blend={weatherTransition01:0.00}";
            }

            bool historyValid = resources != null && resources.HistoryValid;
            return $"{overlayTexture.width}x{overlayTexture.height} {overlayTexture.format} | HistoryValid={historyValid} | Reset={temporalState.LastResetReason}";
        }

        private void DrawOverlayTexture(Rect textureRect, RenderTexture sourceTexture)
        {
            if (debugOverlayMode == DebugOverlayMode.CurrentTransmittance || debugOverlayMode == DebugOverlayMode.CurrentOpacity)
            {
                DrawTraceScalarOverlay(textureRect, sourceTexture, debugOverlayMode == DebugOverlayMode.CurrentTransmittance);
                return;
            }

            if (debugOverlayMode >= DebugOverlayMode.WeatherFieldCoverage && debugOverlayMode <= DebugOverlayMode.WeatherFieldFront)
            {
                DrawWeatherScalarOverlay(textureRect, sourceTexture, (int)debugOverlayMode - (int)DebugOverlayMode.WeatherFieldCoverage);
                return;
            }

            GUI.DrawTexture(textureRect, sourceTexture, ScaleMode.StretchToFill, false);
        }

        private void DrawTraceScalarOverlay(Rect textureRect, RenderTexture sourceTexture, bool useTransmittance)
        {
            RenderTexture active = RenderTexture.active;
            Texture2D temp = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBAHalf, false, true);
            RenderTexture.active = sourceTexture;
            temp.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, false);
            temp.Apply(false, false);
            RenderTexture.active = active;

            Color[] pixels = temp.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = useTransmittance ? pixels[i].a : 1.0f - pixels[i].a;
                pixels[i] = new Color(value, value, value, 1.0f);
            }

            temp.SetPixels(pixels);
            temp.Apply(false, false);
            GUI.DrawTexture(textureRect, temp, ScaleMode.StretchToFill, false);
            DestroyTempTexture(temp);
        }

        private void DrawWeatherScalarOverlay(Rect textureRect, RenderTexture sourceTexture, int channelIndex)
        {
            RenderTexture active = RenderTexture.active;
            Texture2D temp = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBAHalf, false, true);
            RenderTexture.active = sourceTexture;
            temp.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, false);
            temp.Apply(false, false);
            RenderTexture.active = active;

            Color[] pixels = temp.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = channelIndex switch
                {
                    1 => pixels[i].g,
                    2 => pixels[i].b,
                    3 => pixels[i].a,
                    _ => pixels[i].r,
                };
                pixels[i] = new Color(value, value, value, 1.0f);
            }

            temp.SetPixels(pixels);
            temp.Apply(false, false);
            GUI.DrawTexture(textureRect, temp, ScaleMode.StretchToFill, false);
            DestroyTempTexture(temp);
        }

        private static void DestroyTempTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
        }

        private void EnsureOverlayStyles()
        {
            if (overlayTitleStyle != null && overlayLabelStyle != null)
                return;

            overlayTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            overlayLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f, 1.0f) }
            };
        }

        private void ResetLogFlags()
        {
            loggedMissingAtmosphere = false;
            loggedMissingProfile = false;
            loggedMissingBaseNoise = false;
            loggedMissingDetailNoise = false;
            loggedMissingComputeShader = false;
            loggedMissingWeatherFieldComputeShader = false;
        }

        private void LogMissingAtmosphere()
        {
            if (loggedMissingAtmosphere)
                return;

            Debug.LogError("VolumetricClouds: AtmosphereController is required before clouds can prepare runtime parameters.", this);
            loggedMissingAtmosphere = true;
        }

        private void LogMissingProfile()
        {
            if (loggedMissingProfile)
                return;

            Debug.LogError("VolumetricClouds: no VolumetricCloudProfile is assigned and the default profile could not be loaded.", this);
            loggedMissingProfile = true;
        }

        private void LogMissingBaseNoise()
        {
            if (loggedMissingBaseNoise)
                return;

            Debug.LogError("VolumetricClouds: base shape noise is required for the MVP cloud runtime.", this);
            loggedMissingBaseNoise = true;
        }
    }
}
