using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricClouds.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VolumetricCloudController : MonoBehaviour
    {
        private const string DefaultProfilePath = "VolumetricClouds/VolumetricCloudProfile_Default";
        private const string RaymarchComputeShaderPath = "VolumetricClouds/VolumetricCloudRaymarch";
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
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            if (instance == this)
                instance = null;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
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

        private void LoadDefaultProfileIfNeeded()
        {
            if (profile != null)
                return;

            profile = UnityEngine.Resources.Load<VolumetricCloudProfile>(DefaultProfilePath);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null)
                return;

            CameraType cameraType = camera.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;

            if (!TryPrepare(camera, out VolumetricCloudParameters parameters))
                return;

            if (!EnsureComputeShader())
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Volumetric Cloud Density Trace");
            cmd.SetComputeTextureParam(raymarchComputeShader, raymarchKernelIndex, VolumetricCloudShaderIDs.VolumetricCloudTexture, TraceTexture);
            cmd.SetComputeTextureParam(raymarchComputeShader, raymarchKernelIndex, VolumetricCloudShaderIDs.CloudBaseShapeNoise, parameters.BaseShapeNoise);
            if (parameters.DetailShapeNoise != null)
                cmd.SetComputeTextureParam(raymarchComputeShader, raymarchKernelIndex, VolumetricCloudShaderIDs.CloudDetailShapeNoise, parameters.DetailShapeNoise);

            cmd.SetComputeVectorParam(
                raymarchComputeShader,
                VolumetricCloudShaderIDs.VolumetricCloudTraceSize,
                new Vector4(parameters.TraceWidth, parameters.TraceHeight, 1.0f / parameters.TraceWidth, 1.0f / parameters.TraceHeight));
            cmd.SetComputeFloatParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudBottomRadiusKm, parameters.CloudBottomRadiusKm);
            cmd.SetComputeFloatParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudTopRadiusKm, parameters.CloudTopRadiusKm);
            cmd.SetComputeFloatParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudThicknessKm, parameters.CloudThicknessKm);
            cmd.SetComputeFloatParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudCoverage, parameters.CloudCoverage);
            cmd.SetComputeFloatParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudDensityMultiplier, parameters.DensityMultiplier);
            cmd.SetComputeFloatParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudMaxRenderDistanceKm, parameters.MaxRenderDistanceKm);
            cmd.SetComputeIntParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudStepCount, parameters.StepCount);
            cmd.SetComputeIntParam(raymarchComputeShader, VolumetricCloudShaderIDs.CloudHasDetailShapeNoise, parameters.DetailShapeNoise != null ? 1 : 0);
            cmd.SetComputeVectorParam(
                raymarchComputeShader,
                VolumetricCloudShaderIDs.CloudShapeScaleData,
                new Vector4(parameters.ShapeBaseScaleKm, parameters.DetailScaleKm, 1.0f / parameters.ShapeBaseScaleKm, 1.0f / parameters.DetailScaleKm));
            cmd.SetComputeVectorParam(
                raymarchComputeShader,
                VolumetricCloudShaderIDs.CloudWindData,
                new Vector4(parameters.WindDirection.x, parameters.WindDirection.y, parameters.WindOffset.x, parameters.WindOffset.y));

            cmd.SetComputeFloatParam(raymarchComputeShader, AtmosphereShaderIDs.GroundRadiusKm, parameters.GroundRadiusKm);
            cmd.SetComputeVectorParam(raymarchComputeShader, AtmosphereShaderIDs.CameraPositionKm, new Vector4(parameters.CameraPositionKm.x, parameters.CameraPositionKm.y, parameters.CameraPositionKm.z, 0.0f));
            cmd.SetComputeVectorParam(raymarchComputeShader, AtmosphereShaderIDs.CameraBasisRight, new Vector4(parameters.CameraBasisRight.x, parameters.CameraBasisRight.y, parameters.CameraBasisRight.z, 0.0f));
            cmd.SetComputeVectorParam(raymarchComputeShader, AtmosphereShaderIDs.CameraBasisUp, new Vector4(parameters.CameraBasisUp.x, parameters.CameraBasisUp.y, parameters.CameraBasisUp.z, 0.0f));
            cmd.SetComputeVectorParam(raymarchComputeShader, AtmosphereShaderIDs.CameraBasisForward, new Vector4(parameters.CameraBasisForward.x, parameters.CameraBasisForward.y, parameters.CameraBasisForward.z, 0.0f));
            cmd.SetComputeFloatParam(raymarchComputeShader, AtmosphereShaderIDs.CameraTanHalfVerticalFov, parameters.TanHalfVerticalFov);
            cmd.SetComputeFloatParam(raymarchComputeShader, AtmosphereShaderIDs.CameraAspectRatio, parameters.AspectRatio);

            cmd.DispatchCompute(
                raymarchComputeShader,
                raymarchKernelIndex,
                Mathf.CeilToInt(parameters.TraceWidth / 8.0f),
                Mathf.CeilToInt(parameters.TraceHeight / 8.0f),
                1);

            BindGlobals(cmd, parameters);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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

            GUI.Label(titleRect, "Volumetric Cloud Density Trace", overlayTitleStyle);
            GUI.DrawTexture(textureRect, TraceTexture, ScaleMode.StretchToFill, false);
            GUI.Label(metadataRect, $"{TraceTexture.width}x{TraceTexture.height} ARGBHalf", overlayLabelStyle);
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
