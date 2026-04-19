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
            HistoryWeight = 5
        }

        private const string DefaultProfilePath = "VolumetricClouds/VolumetricCloudProfile_Default";
        private const string RaymarchComputeShaderPath = "VolumetricClouds/VolumetricCloudRaymarch";
        private const string TemporalAccumulationComputeShaderPath = "VolumetricClouds/VolumetricCloudTemporalAccumulation";
        private const string CompositeShaderResourceName = "Hidden/Landscape/VolumetricCloudComposite";
        private const string KernelName = "CSMain";
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
        private ComputeShader raymarchComputeShader;
        private ComputeShader temporalAccumulationComputeShader;
        private int raymarchKernelIndex = -1;
        private int temporalAccumulationKernelIndex = -1;
        private int lastParameterHash = int.MinValue;
        private int lastResourceHash = int.MinValue;
        private VolumetricCloudJitterState jitterState = VolumetricCloudJitterState.Legacy;
        private readonly VolumetricCloudTemporalState temporalState = new VolumetricCloudTemporalState();
        private bool loggedMissingAtmosphere;
        private bool loggedMissingProfile;
        private bool loggedMissingBaseNoise;
        private bool loggedMissingDetailNoise;
        private bool loggedMissingComputeShader;
        private GUIStyle overlayLabelStyle;
        private GUIStyle overlayTitleStyle;

        public static VolumetricCloudController Instance => instance;
        public VolumetricCloudProfile Profile => profile;
        public VolumetricCloudResources Resources => resources;
        public RenderTexture TraceTexture => resources != null ? resources.TraceTexture : null;
        public RTHandle TraceHandle => resources != null ? resources.TraceHandle : null;
        public RTHandle StabilizedHandle => resources != null ? resources.StabilizedHandle : null;
        public RTHandle HistoryReadHandle => resources != null ? resources.HistoryReadHandle : null;
        public RTHandle HistoryWriteHandle => resources != null ? resources.HistoryWriteHandle : null;
        public RTHandle HistoryWeightHandle => resources != null ? resources.HistoryWeightHandle : null;
        public RTHandle CompositeHandle => resources != null ? resources.CompositeHandle : null;
        public VolumetricCloudTemporalState TemporalState => temporalState;
        public int LastParameterHash => lastParameterHash;
        public int LastResourceHash => lastResourceHash;

        private void Reset()
        {
            LoadDefaultProfileIfNeeded();
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
            LoadDefaultProfileIfNeeded();
            ResetLogFlags();
        }

        private void OnDisable()
        {
            if (instance == this)
                instance = null;

            resources?.Release();
            temporalState.Reset();
        }

        private void OnValidate()
        {
            LoadDefaultProfileIfNeeded();
        }

        public bool TryPrepare(Camera camera, bool advanceJitter, out VolumetricCloudParameters parameters, out bool resourcesRecreated)
        {
            parameters = default;
            resourcesRecreated = false;

            if (!enabled)
                return false;

            LoadDefaultProfileIfNeeded();
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
            else if (profile != null && !profile.enableJitter)
                jitterState = VolumetricCloudJitterState.Legacy;

            parameters = VolumetricCloudParameters.FromRuntime(profile, atmosphereParameters, viewParameters, camera, Time.time, jitterState);
            if (!parameters.EnableClouds)
                return false;

            if (!resources.EnsureTraceTarget(parameters, out resourcesRecreated))
                return false;

            lastParameterHash = parameters.ParameterHash;
            lastResourceHash = parameters.ResourceHash;
            return true;
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

        private void OnGUI()
        {
            if (!showDebugOverlay || resources == null)
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

            GUI.Label(titleRect, $"Volumetric Cloud Temporal Debug ({GetOverlayModeLabel()})", overlayTitleStyle);
            DrawOverlayTexture(textureRect, overlayTexture);
            GUI.Label(
                metadataRect,
                $"{overlayTexture.width}x{overlayTexture.height} {overlayTexture.format} | HistoryValid={resources.HistoryValid} | Reset={temporalState.LastResetReason}",
                overlayLabelStyle);
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
                _ => "Current",
            };
        }

        private RenderTexture GetOverlayTexture()
        {
            return debugOverlayMode switch
            {
                DebugOverlayMode.History => resources.HistoryReadTexture,
                DebugOverlayMode.Accumulated => resources.StabilizedTexture,
                DebugOverlayMode.HistoryWeight => resources.HistoryWeightTexture,
                _ => resources.TraceTexture,
            };
        }

        private void DrawOverlayTexture(Rect textureRect, RenderTexture sourceTexture)
        {
            if (debugOverlayMode != DebugOverlayMode.CurrentTransmittance && debugOverlayMode != DebugOverlayMode.CurrentOpacity)
            {
                GUI.DrawTexture(textureRect, sourceTexture, ScaleMode.StretchToFill, false);
                return;
            }

            RenderTexture active = RenderTexture.active;
            Texture2D temp = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBAHalf, false, true);
            RenderTexture.active = sourceTexture;
            temp.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0, false);
            temp.Apply(false, false);
            RenderTexture.active = active;

            Color[] pixels = temp.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = debugOverlayMode == DebugOverlayMode.CurrentTransmittance ? pixels[i].a : 1.0f - pixels[i].a;
                pixels[i] = new Color(value, value, value, 1.0f);
            }

            temp.SetPixels(pixels);
            temp.Apply(false, false);
            GUI.DrawTexture(textureRect, temp, ScaleMode.StretchToFill, false);

            if (Application.isPlaying)
                Destroy(temp);
            else
                DestroyImmediate(temp);
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
