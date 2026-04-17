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
            Scattering = 0,
            Transmittance = 1,
            Opacity = 2
        }

        private const string DefaultProfilePath = "VolumetricClouds/VolumetricCloudProfile_Default";
        private const string RaymarchComputeShaderPath = "VolumetricClouds/VolumetricCloudRaymarch";
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
        [SerializeField] private DebugOverlayMode debugOverlayMode = DebugOverlayMode.Scattering;

        private VolumetricCloudResources resources;
        private ComputeShader raymarchComputeShader;
        private int raymarchKernelIndex = -1;
        private int lastParameterHash = int.MinValue;
        private int lastResourceHash = int.MinValue;
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
        }

        private void OnValidate()
        {
            LoadDefaultProfileIfNeeded();
        }

        public bool TryPrepare(Camera camera, out VolumetricCloudParameters parameters)
        {
            parameters = default;

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
            parameters = VolumetricCloudParameters.FromRuntime(profile, atmosphereParameters, viewParameters, camera, Time.time);
            if (!parameters.EnableClouds)
                return false;

            if (!resources.EnsureTraceTarget(parameters))
                return false;

            lastParameterHash = parameters.ParameterHash;
            lastResourceHash = parameters.ResourceHash;
            return true;
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

        private void OnGUI()
        {
            if (!showDebugOverlay || TraceTexture == null)
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

            GUI.Label(titleRect, $"Volumetric Cloud Lighting Trace ({GetOverlayModeLabel()})", overlayTitleStyle);
            DrawOverlayTexture(textureRect);
            GUI.Label(metadataRect, $"{TraceTexture.width}x{TraceTexture.height} ARGBHalf", overlayLabelStyle);
        }

        private string GetOverlayModeLabel()
        {
            return debugOverlayMode switch
            {
                DebugOverlayMode.Transmittance => "Transmittance",
                DebugOverlayMode.Opacity => "Opacity",
                _ => "Scattering",
            };
        }

        private void DrawOverlayTexture(Rect textureRect)
        {
            if (debugOverlayMode == DebugOverlayMode.Scattering)
            {
                GUI.DrawTexture(textureRect, TraceTexture, ScaleMode.StretchToFill, false);
                return;
            }

            RenderTexture active = RenderTexture.active;
            Texture2D temp = new Texture2D(TraceTexture.width, TraceTexture.height, TextureFormat.RGBAHalf, false, true);
            RenderTexture.active = TraceTexture;
            temp.ReadPixels(new Rect(0, 0, TraceTexture.width, TraceTexture.height), 0, 0, false);
            temp.Apply(false, false);
            RenderTexture.active = active;

            Color[] pixels = temp.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = debugOverlayMode == DebugOverlayMode.Transmittance ? pixels[i].a : 1.0f - pixels[i].a;
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
