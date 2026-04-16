using UnityEngine;
using UnityEngine.Rendering;

namespace Atmosphere.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AtmosphereController : MonoBehaviour
    {
        private const string DefaultProfilePath = "Atmosphere/AtmosphereProfile_Earth";
        private const string DebugSliceShaderPath = "Hidden/Landscape/AtmosphereDebugSlice";
        private const float OverlayPaddingLeft = 8.0f;
        private const float OverlayPaddingRight = 8.0f;
        private const float OverlayPaddingTop = 6.0f;
        private const float OverlayPaddingBottom = 8.0f;
        private const float OverlayTitleHeight = 20.0f;
        private const float OverlayMetadataSpacing = 4.0f;
        private const float OverlayMetadataHeight = 18.0f;
        private const float OverlaySectionSpacing = 6.0f;
        private static AtmosphereController instance;

        [SerializeField] private AtmosphereProfile profile;
        [SerializeField] private Light mainLight;
        [SerializeField] private bool showDebugOverlay = true;
        [SerializeField] private Vector2 debugOverlaySize = new Vector2(384.0f, 96.0f);
        [SerializeField] private bool showAerialPerspectiveDebug = true;
        [SerializeField] [Range(0.0f, 1.0f)] private float aerialDebugSlice = 0.5f;

        private AtmosphereLutManager lutManager;
        private bool forceRebuild = true;
        private bool forceMultiScatteringRebuild = true;
        private bool forceAerialPerspectiveRebuild = true;
        private GUIStyle overlayLabelStyle;
        private GUIStyle overlayTitleStyle;
        private Material debugSliceMaterial;
        private RenderTexture aerialScatteringDebugTexture;
        private RenderTexture aerialTransmittanceDebugTexture;

        public static AtmosphereController Instance => instance;
        public RTHandle TransmittanceHandle => lutManager != null ? lutManager.TransmittanceHandle : null;
        public RTHandle MultiScatteringHandle => lutManager != null ? lutManager.MultiScatteringHandle : null;
        public RTHandle SkyViewHandle => lutManager != null ? lutManager.SkyViewHandle : null;
        public RTHandle AerialScatteringHandle => lutManager != null ? lutManager.AerialScatteringHandle : null;
        public RTHandle AerialTransmittanceHandle => lutManager != null ? lutManager.AerialTransmittanceHandle : null;
        public RenderTexture TransmittanceTexture => lutManager != null ? lutManager.TransmittanceTexture : null;
        public RenderTexture MultiScatteringTexture => lutManager != null ? lutManager.MultiScatteringTexture : null;
        public RenderTexture SkyViewTexture => lutManager != null ? lutManager.SkyViewTexture : null;
        public RenderTexture AerialScatteringTexture => lutManager != null ? lutManager.AerialScatteringTexture : null;
        public RenderTexture AerialTransmittanceTexture => lutManager != null ? lutManager.AerialTransmittanceTexture : null;
        public ComputeShader TransmittanceComputeShader => lutManager != null ? lutManager.TransmittanceComputeShader : null;
        public ComputeShader MultiScatteringComputeShader => lutManager != null ? lutManager.MultiScatteringComputeShader : null;
        public ComputeShader SkyViewComputeShader => lutManager != null ? lutManager.SkyViewComputeShader : null;
        public ComputeShader AerialPerspectiveComputeShader => lutManager != null ? lutManager.AerialPerspectiveComputeShader : null;
        public int TransmittanceKernelIndex => lutManager != null ? lutManager.TransmittanceKernelIndex : -1;
        public int MultiScatteringKernelIndex => lutManager != null ? lutManager.MultiScatteringKernelIndex : -1;
        public int SkyViewKernelIndex => lutManager != null ? lutManager.SkyViewKernelIndex : -1;
        public int AerialPerspectiveKernelIndex => lutManager != null ? lutManager.AerialPerspectiveKernelIndex : -1;

        private void Reset()
        {
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
        }

        private void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Atmosphere: multiple AtmosphereController instances found. Disabling duplicate.", this);
                enabled = false;
                return;
            }

            instance = this;
            lutManager ??= new AtmosphereLutManager();
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
            forceRebuild = true;
            forceMultiScatteringRebuild = true;
            forceAerialPerspectiveRebuild = true;
        }

        private void OnDisable()
        {
            if (instance == this)
                instance = null;

            if (lutManager != null)
                lutManager.Release();

            ReleaseDebugResources();
        }

        private void OnValidate()
        {
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
            forceRebuild = true;
            forceMultiScatteringRebuild = true;
            forceAerialPerspectiveRebuild = true;
        }

        public bool TryPrepareForRender(out AtmosphereParameters parameters)
        {
            parameters = default;

            if (!enabled)
                return false;

            LoadDefaultProfileIfNeeded();
            if (profile == null)
                return false;

            lutManager ??= new AtmosphereLutManager();
            parameters = AtmosphereParameters.FromProfile(profile);
            return lutManager.EnsureTransmittanceResources(parameters);
        }

        public bool TryPrepareForMultiScattering(out AtmosphereParameters parameters)
        {
            parameters = default;
            if (!TryPrepareForRender(out parameters))
                return false;

            return lutManager.EnsureMultiScatteringResources(parameters);
        }

        public bool TryPrepareForSkyView(Camera camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters)
        {
            viewParameters = default;
            if (!TryPrepareForMultiScattering(out parameters))
                return false;

            if (camera == null)
                return false;

            viewParameters = BuildViewParameters(camera, parameters);
            if (!lutManager.EnsureSkyViewResources(parameters))
                return false;

            SyncImmediateShaderGlobals(parameters, viewParameters);
            return true;
        }

        public bool TryPrepareForAerialPerspective(Camera camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters)
        {
            viewParameters = default;
            if (!TryPrepareForSkyView(camera, out parameters, out viewParameters))
                return false;

            if (!lutManager.EnsureAerialPerspectiveResources(parameters))
                return false;

            SyncImmediateShaderGlobals(parameters, viewParameters);
            return true;
        }

        public bool TryGetRuntimeContext(Camera camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters)
        {
            viewParameters = default;
            if (!TryPrepareForSkyView(camera, out parameters, out viewParameters))
                return false;

            return true;
        }

        public bool NeedsTransmittanceRebuild(in AtmosphereParameters parameters)
        {
            return forceRebuild || lutManager == null || lutManager.NeedsTransmittanceRebuild(parameters);
        }

        public bool NeedsMultiScatteringRebuild(in AtmosphereParameters parameters)
        {
            return forceMultiScatteringRebuild || lutManager == null || lutManager.NeedsMultiScatteringRebuild(parameters);
        }

        public bool NeedsSkyViewRebuild(in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            return lutManager == null || lutManager.NeedsSkyViewRebuild(parameters, viewParameters.DynamicHash);
        }

        public bool NeedsAerialPerspectiveRebuild(in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            return forceAerialPerspectiveRebuild
                || lutManager == null
                || lutManager.NeedsAerialPerspectiveRebuild(parameters, viewParameters.DynamicHash);
        }

        public void RenderTransmittance(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            lutManager ??= new AtmosphereLutManager();
            lutManager.RenderTransmittance(cmd, parameters);
            lutManager.BindGlobals(cmd, parameters);
            forceRebuild = false;
            forceMultiScatteringRebuild = true;
            forceAerialPerspectiveRebuild = true;
        }

        public void MarkTransmittanceRendered(in AtmosphereParameters parameters)
        {
            lutManager?.MarkTransmittanceRendered(parameters);
            forceRebuild = false;
            forceMultiScatteringRebuild = true;
            forceAerialPerspectiveRebuild = true;
        }

        public void RenderMultiScattering(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            lutManager ??= new AtmosphereLutManager();
            lutManager.RenderMultiScattering(cmd, parameters);
            lutManager.BindGlobals(cmd, parameters);
            forceMultiScatteringRebuild = false;
            forceAerialPerspectiveRebuild = true;
        }

        public void MarkMultiScatteringRendered(in AtmosphereParameters parameters)
        {
            lutManager?.MarkMultiScatteringRendered(parameters);
            forceMultiScatteringRebuild = false;
            forceAerialPerspectiveRebuild = true;
        }

        public void RenderSkyView(CommandBuffer cmd, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            lutManager ??= new AtmosphereLutManager();
            lutManager.RenderSkyView(cmd, parameters, viewParameters);
            lutManager.BindGlobals(cmd, parameters);
            AtmosphereLutManager.BindSkyViewGlobals(cmd, parameters, viewParameters);
            forceAerialPerspectiveRebuild = true;
        }

        public void MarkSkyViewRendered(in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            lutManager?.MarkSkyViewRendered(parameters, viewParameters);
            forceAerialPerspectiveRebuild = true;
        }

        public void RenderAerialPerspective(CommandBuffer cmd, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            lutManager ??= new AtmosphereLutManager();
            lutManager.RenderAerialPerspective(cmd, parameters, viewParameters);
            lutManager.BindGlobals(cmd, parameters);
            AtmosphereLutManager.BindAerialPerspectiveGlobals(cmd, parameters, viewParameters);
            forceAerialPerspectiveRebuild = false;
        }

        public void MarkAerialPerspectiveRendered(in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            lutManager?.MarkAerialPerspectiveRendered(parameters, viewParameters);
            forceAerialPerspectiveRebuild = false;
        }

        public void BindSkyViewGlobals(CommandBuffer cmd, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            BindGlobals(cmd, parameters);
            AtmosphereLutManager.BindSkyViewGlobals(cmd, parameters, viewParameters);
        }

        public void BindAerialPerspectiveGlobals(CommandBuffer cmd, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            BindGlobals(cmd, parameters);
            AtmosphereLutManager.BindAerialPerspectiveGlobals(cmd, parameters, viewParameters);
        }

        public void BindGlobals(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (lutManager == null)
                return;

            lutManager.BindGlobals(cmd, parameters);
        }

        private void SyncImmediateShaderGlobals(in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            if (lutManager == null)
                return;

            if (TransmittanceTexture != null)
            {
                Shader.SetGlobalTexture(AtmosphereShaderIDs.TransmittanceLut, TransmittanceTexture);
                Shader.SetGlobalVector(
                    AtmosphereShaderIDs.TransmittanceSize,
                    new Vector4(
                        parameters.TransmittanceWidth,
                        parameters.TransmittanceHeight,
                        1.0f / parameters.TransmittanceWidth,
                        1.0f / parameters.TransmittanceHeight));
            }

            if (MultiScatteringTexture != null)
            {
                Shader.SetGlobalTexture(AtmosphereShaderIDs.MultiScatteringLut, MultiScatteringTexture);
                Shader.SetGlobalVector(
                    AtmosphereShaderIDs.MultiScatteringSize,
                    new Vector4(
                        parameters.MultiScatteringWidth,
                        parameters.MultiScatteringHeight,
                        1.0f / parameters.MultiScatteringWidth,
                        1.0f / parameters.MultiScatteringHeight));
            }

            if (SkyViewTexture != null)
            {
                Shader.SetGlobalTexture(AtmosphereShaderIDs.SkyViewLut, SkyViewTexture);
                Shader.SetGlobalVector(
                    AtmosphereShaderIDs.SkyViewSize,
                    new Vector4(
                        parameters.SkyViewWidth,
                        parameters.SkyViewHeight,
                        1.0f / parameters.SkyViewWidth,
                        1.0f / parameters.SkyViewHeight));
            }

            if (AerialScatteringTexture != null)
                Shader.SetGlobalTexture(AtmosphereShaderIDs.AerialScatteringLut, AerialScatteringTexture);

            if (AerialTransmittanceTexture != null)
                Shader.SetGlobalTexture(AtmosphereShaderIDs.AerialTransmittanceLut, AerialTransmittanceTexture);

            if (AerialScatteringTexture != null || AerialTransmittanceTexture != null)
            {
                Shader.SetGlobalVector(
                    AtmosphereShaderIDs.AerialPerspectiveSize,
                    new Vector4(
                        parameters.AerialPerspectiveWidth,
                        parameters.AerialPerspectiveHeight,
                        parameters.AerialPerspectiveDepth,
                        1.0f / parameters.AerialPerspectiveDepth));
                Shader.SetGlobalFloat(AtmosphereShaderIDs.AerialPerspectiveMaxDistanceKm, parameters.AerialPerspectiveMaxDistanceKm);
            }

            Shader.SetGlobalFloat(AtmosphereShaderIDs.GroundRadiusKm, parameters.GroundRadiusKm);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.TopRadiusKm, parameters.TopRadiusKm);
            Shader.SetGlobalVector(AtmosphereShaderIDs.SunDirection, viewParameters.SunDirection);
            Shader.SetGlobalVector(AtmosphereShaderIDs.SunIlluminance, viewParameters.SunIlluminance);
            Shader.SetGlobalVector(AtmosphereShaderIDs.SunDiskParams, AtmosphereLutManager.BuildSunDiskParams(parameters));
            Shader.SetGlobalFloat(AtmosphereShaderIDs.SkyExposure, parameters.SkyExposure);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.AerialPerspectiveExposure, parameters.AerialPerspectiveExposure);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.MiePhaseG, parameters.MiePhaseG);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraPositionKm, viewParameters.CameraPositionKm);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraBasisRight, viewParameters.CameraBasisRight);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraBasisUp, viewParameters.CameraBasisUp);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraBasisForward, viewParameters.CameraBasisForward);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.CameraTanHalfVerticalFov, viewParameters.TanHalfVerticalFov);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.CameraAspectRatio, viewParameters.AspectRatio);
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
                return;

            RenderTexture texture = TransmittanceTexture;
            RenderTexture multiScatteringTexture = MultiScatteringTexture;
            RenderTexture skyViewTexture = SkyViewTexture;
            RenderTexture aerialScatteringTexture = null;
            RenderTexture aerialTransmittanceTexture = null;
            if (showAerialPerspectiveDebug)
            {
                UpdateAerialPerspectiveDebugTextures();
                aerialScatteringTexture = this.aerialScatteringDebugTexture;
                aerialTransmittanceTexture = this.aerialTransmittanceDebugTexture;
            }

            if (texture == null
                && multiScatteringTexture == null
                && skyViewTexture == null
                && aerialScatteringTexture == null
                && aerialTransmittanceTexture == null)
                return;

            EnsureOverlayStyles();

            float maxPanelWidth = Mathf.Max(128.0f, Screen.width - 32.0f - OverlayPaddingLeft - OverlayPaddingRight);
            float width = Mathf.Min(Mathf.Max(128.0f, debugOverlaySize.x), maxPanelWidth);
            float height = Mathf.Max(32.0f, debugOverlaySize.y);
            float blockHeight = OverlayTitleHeight + height + OverlayMetadataSpacing + OverlayMetadataHeight;
            int blockCount = (texture != null ? 1 : 0)
                + (multiScatteringTexture != null ? 1 : 0)
                + (skyViewTexture != null ? 1 : 0)
                + (aerialScatteringTexture != null ? 1 : 0)
                + (aerialTransmittanceTexture != null ? 1 : 0);
            float panelHeight = OverlayPaddingTop
                + blockCount * blockHeight
                + Mathf.Max(0, blockCount - 1) * OverlaySectionSpacing
                + OverlayPaddingBottom;
            Rect panelRect = new Rect(16.0f, 16.0f, width + OverlayPaddingLeft + OverlayPaddingRight, panelHeight);
            GUI.Box(panelRect, GUIContent.none);

            float cursorY = panelRect.y + OverlayPaddingTop;
            if (texture != null)
            {
                DrawOverlayTexture(panelRect.x, ref cursorY, width, height, texture, "Atmosphere Transmittance LUT");
            }

            if (multiScatteringTexture != null)
            {
                DrawOverlayTexture(panelRect.x, ref cursorY, width, height, multiScatteringTexture, "Atmosphere Multi-scattering LUT");
            }

            if (skyViewTexture != null)
            {
                DrawOverlayTexture(panelRect.x, ref cursorY, width, height, skyViewTexture, "Atmosphere Sky-View LUT");
            }

            if (aerialScatteringTexture != null)
            {
                DrawOverlayTexture(panelRect.x, ref cursorY, width, height, aerialScatteringTexture, $"Aerial Scattering Slice ({Mathf.RoundToInt(aerialDebugSlice * 100.0f)}%)");
            }

            if (aerialTransmittanceTexture != null)
            {
                DrawOverlayTexture(panelRect.x, ref cursorY, width, height, aerialTransmittanceTexture, $"Aerial Transmittance Slice ({Mathf.RoundToInt(aerialDebugSlice * 100.0f)}%)");
            }
        }

        private void DrawOverlayTexture(float panelX, ref float cursorY, float width, float height, RenderTexture texture, string label)
        {
            Rect titleRect = new Rect(panelX + OverlayPaddingLeft, cursorY, width, OverlayTitleHeight);
            Rect textureRect = new Rect(
                panelX + OverlayPaddingLeft,
                titleRect.yMax,
                width,
                height);
            Rect metadataRect = new Rect(textureRect.x, textureRect.yMax + OverlayMetadataSpacing, textureRect.width, OverlayMetadataHeight);

            GUI.Label(titleRect, label, overlayTitleStyle);
            GUI.DrawTexture(textureRect, texture, ScaleMode.StretchToFill, false);
            GUI.Label(metadataRect, $"{texture.width}x{texture.height} ARGBHalf", overlayLabelStyle);
            cursorY = metadataRect.yMax + OverlaySectionSpacing;
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

        private void LoadDefaultProfileIfNeeded()
        {
            if (profile != null)
                return;

            profile = Resources.Load<AtmosphereProfile>(DefaultProfilePath);
        }

        private void TryAssignMainLight()
        {
            if (mainLight != null && mainLight.type == LightType.Directional)
                return;

            if (RenderSettings.sun != null)
            {
                mainLight = RenderSettings.sun;
                return;
            }

            Light[] lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    mainLight = lights[i];
                    return;
                }
            }
        }

        private AtmosphereViewParameters BuildViewParameters(Camera camera, in AtmosphereParameters parameters)
        {
            Transform cameraTransform = camera.transform;
            Vector3 worldPosition = cameraTransform.position;
            Vector3 cameraPositionKm = new Vector3(
                worldPosition.x * 0.001f,
                parameters.GroundRadiusKm + Mathf.Max(worldPosition.y * 0.001f, 0.001f),
                worldPosition.z * 0.001f);

            Vector3 up = cameraPositionKm.normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.95f ? Vector3.forward : Vector3.right;
            Vector3 right = Vector3.Cross(reference, up).normalized;
            Vector3 forward = Vector3.Cross(up, right).normalized;

            Vector3 sunDirection = GetSunDirection();
            Vector3 sunColorScale = GetSunColorScale(parameters);
            Vector3 sunIlluminance = Vector3.Scale(parameters.SunIlluminance, sunColorScale) * (GetSunIntensityScale() * parameters.SunIntensityMultiplier);
            float verticalFovRadians = Mathf.Deg2Rad * Mathf.Max(1.0f, camera.fieldOfView);
            float tanHalfVerticalFov = Mathf.Tan(verticalFovRadians * 0.5f);
            float aspectRatio = camera.aspect > 0.0f ? camera.aspect : 1.0f;
            return new AtmosphereViewParameters(cameraPositionKm, right, up, forward, sunDirection, sunIlluminance, tanHalfVerticalFov, aspectRatio);
        }

        private Vector3 GetSunDirection()
        {
            if (mainLight != null && mainLight.type == LightType.Directional)
                return (-mainLight.transform.forward).normalized;

            return Vector3.up;
        }

        private float GetSunIntensityScale()
        {
            if (mainLight == null || mainLight.type != LightType.Directional)
                return 1.0f;

            return Mathf.Max(0.0f, mainLight.intensity);
        }

        private Vector3 GetSunColorScale(in AtmosphereParameters parameters)
        {
            if (!parameters.UseDirectionalLightColor || mainLight == null || mainLight.type != LightType.Directional)
                return Vector3.one;

            Color linearColor = mainLight.color.linear;
            if (mainLight.useColorTemperature)
                linearColor *= Mathf.CorrelatedColorTemperatureToRGB(mainLight.colorTemperature);

            return new Vector3(
                Mathf.Max(0.0f, linearColor.r),
                Mathf.Max(0.0f, linearColor.g),
                Mathf.Max(0.0f, linearColor.b));
        }

        private void UpdateAerialPerspectiveDebugTextures()
        {
            if (AerialScatteringTexture == null || AerialTransmittanceTexture == null)
            {
                ReleaseDebugTexturesOnly();
                return;
            }

            Shader shader = Shader.Find(DebugSliceShaderPath);
            if (shader == null)
                return;

            if (debugSliceMaterial == null || debugSliceMaterial.shader != shader)
                debugSliceMaterial = new Material(shader);

            EnsureDebugTexture(ref aerialScatteringDebugTexture, AerialScatteringTexture.width, AerialScatteringTexture.height, "Atmosphere Aerial Scattering Debug");
            EnsureDebugTexture(ref aerialTransmittanceDebugTexture, AerialTransmittanceTexture.width, AerialTransmittanceTexture.height, "Atmosphere Aerial Transmittance Debug");

            debugSliceMaterial.SetFloat(AtmosphereShaderIDs.AerialDebugSlice, aerialDebugSlice);
            debugSliceMaterial.SetTexture("_MainTex3D", AerialScatteringTexture);
            Graphics.Blit(null, aerialScatteringDebugTexture, debugSliceMaterial, 0);
            debugSliceMaterial.SetTexture("_MainTex3D", AerialTransmittanceTexture);
            Graphics.Blit(null, aerialTransmittanceDebugTexture, debugSliceMaterial, 0);
        }

        private void EnsureDebugTexture(ref RenderTexture texture, int width, int height, string textureName)
        {
            if (texture != null && (texture.width != width || texture.height != height))
            {
                if (Application.isPlaying)
                    Destroy(texture);
                else
                    DestroyImmediate(texture);

                texture = null;
            }

            if (texture != null)
                return;

            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
        }

        private void ReleaseDebugTexturesOnly()
        {
            if (aerialScatteringDebugTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(aerialScatteringDebugTexture);
                else
                    DestroyImmediate(aerialScatteringDebugTexture);

                aerialScatteringDebugTexture = null;
            }

            if (aerialTransmittanceDebugTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(aerialTransmittanceDebugTexture);
                else
                    DestroyImmediate(aerialTransmittanceDebugTexture);

                aerialTransmittanceDebugTexture = null;
            }
        }

        private void ReleaseDebugResources()
        {
            ReleaseDebugTexturesOnly();

            if (debugSliceMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(debugSliceMaterial);
                else
                    DestroyImmediate(debugSliceMaterial);

                debugSliceMaterial = null;
            }
        }
    }
}
