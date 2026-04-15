using UnityEngine;
using UnityEngine.Rendering;

namespace Atmosphere.Runtime
{
    public sealed class AtmosphereLutManager
    {
        private const string TransmittanceComputeShaderPath = "Atmosphere/AtmosphereTransmittance";
        private const string MultiScatteringComputeShaderPath = "Atmosphere/AtmosphereMultiScattering";
        private const string SkyViewComputeShaderPath = "Atmosphere/AtmosphereSkyView";
        private const string KernelName = "CSMain";

        private ComputeShader transmittanceComputeShader;
        private ComputeShader multiScatteringComputeShader;
        private ComputeShader skyViewComputeShader;
        private int transmittanceKernelIndex = -1;
        private int multiScatteringKernelIndex = -1;
        private int skyViewKernelIndex = -1;

        private RenderTexture transmittanceTexture;
        private RenderTexture multiScatteringTexture;
        private RenderTexture skyViewTexture;
        private RTHandle transmittanceHandle;
        private RTHandle multiScatteringHandle;
        private RTHandle skyViewHandle;

        private int currentTransmittanceHash = int.MinValue;
        private int currentMultiScatteringHash = int.MinValue;
        private int currentSkyViewHash = int.MinValue;
        private int currentSkyViewDynamicHash = int.MinValue;

        private bool loggedUnsupportedCompute;
        private bool loggedBuildInfo;
        private bool loggedMissingTransmittanceShader;
        private bool loggedMissingMultiScatteringShader;
        private bool loggedMissingSkyViewShader;

        public ComputeShader TransmittanceComputeShader => LoadTransmittanceComputeShader();
        public ComputeShader MultiScatteringComputeShader => LoadMultiScatteringComputeShader();
        public ComputeShader SkyViewComputeShader => LoadSkyViewComputeShader();
        public int TransmittanceKernelIndex => transmittanceKernelIndex;
        public int MultiScatteringKernelIndex => multiScatteringKernelIndex;
        public int SkyViewKernelIndex => skyViewKernelIndex;
        public RenderTexture TransmittanceTexture => transmittanceTexture;
        public RenderTexture MultiScatteringTexture => multiScatteringTexture;
        public RenderTexture SkyViewTexture => skyViewTexture;
        public RTHandle TransmittanceHandle => transmittanceHandle;
        public RTHandle MultiScatteringHandle => multiScatteringHandle;
        public RTHandle SkyViewHandle => skyViewHandle;

        public bool EnsureTransmittanceResources(in AtmosphereParameters parameters)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                LogUnsupportedCompute();
                ReleaseTextures(ref transmittanceTexture, ref transmittanceHandle);
                return false;
            }

            if (LoadTransmittanceComputeShader() == null)
            {
                if (!loggedMissingTransmittanceShader)
                {
                    Debug.LogError("Atmosphere: failed to load Resources/Atmosphere/AtmosphereTransmittance.compute.");
                    loggedMissingTransmittanceShader = true;
                }

                ReleaseTextures(ref transmittanceTexture, ref transmittanceHandle);
                return false;
            }

            EnsureTexture(
                ref transmittanceTexture,
                ref transmittanceHandle,
                parameters.TransmittanceWidth,
                parameters.TransmittanceHeight,
                "Atmosphere Transmittance LUT");
            return transmittanceTexture != null && transmittanceHandle != null;
        }

        public bool EnsureMultiScatteringResources(in AtmosphereParameters parameters)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                LogUnsupportedCompute();
                ReleaseTextures(ref multiScatteringTexture, ref multiScatteringHandle);
                return false;
            }

            if (LoadMultiScatteringComputeShader() == null)
            {
                if (!loggedMissingMultiScatteringShader)
                {
                    Debug.LogError("Atmosphere: failed to load Resources/Atmosphere/AtmosphereMultiScattering.compute.");
                    loggedMissingMultiScatteringShader = true;
                }

                ReleaseTextures(ref multiScatteringTexture, ref multiScatteringHandle);
                return false;
            }

            EnsureTexture(
                ref multiScatteringTexture,
                ref multiScatteringHandle,
                parameters.MultiScatteringWidth,
                parameters.MultiScatteringHeight,
                "Atmosphere Multi-scattering LUT");
            return multiScatteringTexture != null && multiScatteringHandle != null;
        }

        public bool EnsureSkyViewResources(in AtmosphereParameters parameters)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                LogUnsupportedCompute();
                ReleaseTextures(ref skyViewTexture, ref skyViewHandle);
                return false;
            }

            if (LoadSkyViewComputeShader() == null)
            {
                if (!loggedMissingSkyViewShader)
                {
                    Debug.LogError("Atmosphere: failed to load Resources/Atmosphere/AtmosphereSkyView.compute.");
                    loggedMissingSkyViewShader = true;
                }

                ReleaseTextures(ref skyViewTexture, ref skyViewHandle);
                return false;
            }

            EnsureTexture(
                ref skyViewTexture,
                ref skyViewHandle,
                parameters.SkyViewWidth,
                parameters.SkyViewHeight,
                "Atmosphere Sky-View LUT");
            return skyViewTexture != null && skyViewHandle != null;
        }

        public bool NeedsTransmittanceRebuild(in AtmosphereParameters parameters)
        {
            return transmittanceTexture == null
                || transmittanceHandle == null
                || currentTransmittanceHash != parameters.TransmittanceHash;
        }

        public bool NeedsMultiScatteringRebuild(in AtmosphereParameters parameters)
        {
            return multiScatteringTexture == null
                || multiScatteringHandle == null
                || currentMultiScatteringHash != parameters.MultiScatteringHash
                || currentTransmittanceHash != parameters.TransmittanceHash;
        }

        public bool NeedsSkyViewRebuild(in AtmosphereParameters parameters, int dynamicHash)
        {
            return skyViewTexture == null
                || skyViewHandle == null
                || currentSkyViewHash != parameters.SkyViewHash
                || currentSkyViewDynamicHash != dynamicHash
                || currentTransmittanceHash != parameters.TransmittanceHash
                || currentMultiScatteringHash != parameters.MultiScatteringHash;
        }

        public void RenderTransmittance(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (!EnsureTransmittanceResources(parameters))
                return;

            ApplyCommonParameters(cmd, transmittanceComputeShader, parameters);
            cmd.SetComputeTextureParam(transmittanceComputeShader, transmittanceKernelIndex, AtmosphereShaderIDs.TransmittanceLut, transmittanceTexture);
            cmd.DispatchCompute(
                transmittanceComputeShader,
                transmittanceKernelIndex,
                Mathf.CeilToInt(parameters.TransmittanceWidth / 8.0f),
                Mathf.CeilToInt(parameters.TransmittanceHeight / 8.0f),
                1);

            currentTransmittanceHash = parameters.TransmittanceHash;
            currentMultiScatteringHash = int.MinValue;
            currentSkyViewHash = int.MinValue;
            LogBuild(parameters, "transmittance", parameters.TransmittanceWidth, parameters.TransmittanceHeight);
        }

        public void MarkTransmittanceRendered(in AtmosphereParameters parameters)
        {
            currentTransmittanceHash = parameters.TransmittanceHash;
            currentMultiScatteringHash = int.MinValue;
            currentSkyViewHash = int.MinValue;
        }

        public void RenderMultiScattering(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (!EnsureTransmittanceResources(parameters) || !EnsureMultiScatteringResources(parameters))
                return;

            ApplyCommonParameters(cmd, multiScatteringComputeShader, parameters);
            cmd.SetComputeVectorParam(multiScatteringComputeShader, AtmosphereShaderIDs.GroundAlbedo, parameters.GroundAlbedo);
            cmd.SetComputeVectorParam(
                multiScatteringComputeShader,
                AtmosphereShaderIDs.MultiScatteringSize,
                new Vector4(
                    parameters.MultiScatteringWidth,
                    parameters.MultiScatteringHeight,
                    1.0f / parameters.MultiScatteringWidth,
                    1.0f / parameters.MultiScatteringHeight));
            cmd.SetComputeIntParam(multiScatteringComputeShader, AtmosphereShaderIDs.MultiScatteringSphereSamples, parameters.MultiScatteringSphereSamples);
            cmd.SetComputeIntParam(multiScatteringComputeShader, AtmosphereShaderIDs.MultiScatteringRaySteps, parameters.MultiScatteringRaySteps);
            cmd.SetComputeTextureParam(multiScatteringComputeShader, multiScatteringKernelIndex, AtmosphereShaderIDs.TransmittanceLut, transmittanceTexture);
            cmd.SetComputeTextureParam(multiScatteringComputeShader, multiScatteringKernelIndex, AtmosphereShaderIDs.MultiScatteringLut, multiScatteringTexture);
            cmd.DispatchCompute(
                multiScatteringComputeShader,
                multiScatteringKernelIndex,
                Mathf.CeilToInt(parameters.MultiScatteringWidth / 8.0f),
                Mathf.CeilToInt(parameters.MultiScatteringHeight / 8.0f),
                1);

            currentTransmittanceHash = parameters.TransmittanceHash;
            currentMultiScatteringHash = parameters.MultiScatteringHash;
            currentSkyViewHash = int.MinValue;
            LogBuild(parameters, "multi-scattering", parameters.MultiScatteringWidth, parameters.MultiScatteringHeight);
        }

        public void MarkMultiScatteringRendered(in AtmosphereParameters parameters)
        {
            currentTransmittanceHash = parameters.TransmittanceHash;
            currentMultiScatteringHash = parameters.MultiScatteringHash;
            currentSkyViewHash = int.MinValue;
        }

        public void RenderSkyView(CommandBuffer cmd, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            if (!EnsureTransmittanceResources(parameters) || !EnsureMultiScatteringResources(parameters) || !EnsureSkyViewResources(parameters))
                return;

            ApplyCommonParameters(cmd, skyViewComputeShader, parameters);
            ApplySkyViewParameters(cmd, skyViewComputeShader, parameters, viewParameters);
            cmd.SetComputeTextureParam(skyViewComputeShader, skyViewKernelIndex, AtmosphereShaderIDs.TransmittanceLut, transmittanceTexture);
            cmd.SetComputeTextureParam(skyViewComputeShader, skyViewKernelIndex, AtmosphereShaderIDs.MultiScatteringLut, multiScatteringTexture);
            cmd.SetComputeTextureParam(skyViewComputeShader, skyViewKernelIndex, AtmosphereShaderIDs.SkyViewLut, skyViewTexture);
            cmd.DispatchCompute(
                skyViewComputeShader,
                skyViewKernelIndex,
                Mathf.CeilToInt(parameters.SkyViewWidth / 8.0f),
                Mathf.CeilToInt(parameters.SkyViewHeight / 8.0f),
                1);

            currentTransmittanceHash = parameters.TransmittanceHash;
            currentMultiScatteringHash = parameters.MultiScatteringHash;
            currentSkyViewHash = parameters.SkyViewHash;
            currentSkyViewDynamicHash = viewParameters.DynamicHash;
            LogBuild(parameters, "sky-view", parameters.SkyViewWidth, parameters.SkyViewHeight);
        }

        public void MarkSkyViewRendered(in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            currentTransmittanceHash = parameters.TransmittanceHash;
            currentMultiScatteringHash = parameters.MultiScatteringHash;
            currentSkyViewHash = parameters.SkyViewHash;
            currentSkyViewDynamicHash = viewParameters.DynamicHash;
        }

        public void BindGlobals(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (transmittanceTexture != null)
            {
                cmd.SetGlobalTexture(AtmosphereShaderIDs.TransmittanceLut, transmittanceTexture);
                cmd.SetGlobalVector(
                    AtmosphereShaderIDs.TransmittanceSize,
                    new Vector4(
                        parameters.TransmittanceWidth,
                        parameters.TransmittanceHeight,
                        1.0f / parameters.TransmittanceWidth,
                        1.0f / parameters.TransmittanceHeight));
            }

            if (multiScatteringTexture != null)
            {
                cmd.SetGlobalTexture(AtmosphereShaderIDs.MultiScatteringLut, multiScatteringTexture);
                cmd.SetGlobalVector(
                    AtmosphereShaderIDs.MultiScatteringSize,
                    new Vector4(
                        parameters.MultiScatteringWidth,
                        parameters.MultiScatteringHeight,
                        1.0f / parameters.MultiScatteringWidth,
                        1.0f / parameters.MultiScatteringHeight));
            }

            if (skyViewTexture != null)
            {
                cmd.SetGlobalTexture(AtmosphereShaderIDs.SkyViewLut, skyViewTexture);
                cmd.SetGlobalVector(
                    AtmosphereShaderIDs.SkyViewSize,
                    new Vector4(
                        parameters.SkyViewWidth,
                        parameters.SkyViewHeight,
                        1.0f / parameters.SkyViewWidth,
                        1.0f / parameters.SkyViewHeight));
            }
        }

        public void Release()
        {
            ReleaseTextures(ref transmittanceTexture, ref transmittanceHandle);
            ReleaseTextures(ref multiScatteringTexture, ref multiScatteringHandle);
            ReleaseTextures(ref skyViewTexture, ref skyViewHandle);
            transmittanceComputeShader = null;
            multiScatteringComputeShader = null;
            skyViewComputeShader = null;
            transmittanceKernelIndex = -1;
            multiScatteringKernelIndex = -1;
            skyViewKernelIndex = -1;
            currentTransmittanceHash = int.MinValue;
            currentMultiScatteringHash = int.MinValue;
            currentSkyViewHash = int.MinValue;
            currentSkyViewDynamicHash = int.MinValue;
            loggedBuildInfo = false;
            loggedMissingTransmittanceShader = false;
            loggedMissingMultiScatteringShader = false;
            loggedMissingSkyViewShader = false;
        }

        private void EnsureTexture(ref RenderTexture texture, ref RTHandle handle, int width, int height, string name)
        {
            if (texture != null && (texture.width != width || texture.height != height))
            {
                ReleaseTextures(ref texture, ref handle);
            }

            if (texture != null && handle != null)
                return;

            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = name,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex2D,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            texture.Create();
            handle = RTHandles.Alloc(texture, false);
        }

        private ComputeShader LoadTransmittanceComputeShader()
        {
            if (transmittanceComputeShader != null)
                return transmittanceComputeShader;

            transmittanceComputeShader = Resources.Load<ComputeShader>(TransmittanceComputeShaderPath);
            if (transmittanceComputeShader != null)
                transmittanceKernelIndex = transmittanceComputeShader.FindKernel(KernelName);

            return transmittanceComputeShader;
        }

        private ComputeShader LoadMultiScatteringComputeShader()
        {
            if (multiScatteringComputeShader != null)
                return multiScatteringComputeShader;

            multiScatteringComputeShader = Resources.Load<ComputeShader>(MultiScatteringComputeShaderPath);
            if (multiScatteringComputeShader != null)
                multiScatteringKernelIndex = multiScatteringComputeShader.FindKernel(KernelName);

            return multiScatteringComputeShader;
        }

        private ComputeShader LoadSkyViewComputeShader()
        {
            if (skyViewComputeShader != null)
                return skyViewComputeShader;

            skyViewComputeShader = Resources.Load<ComputeShader>(SkyViewComputeShaderPath);
            if (skyViewComputeShader != null)
                skyViewKernelIndex = skyViewComputeShader.FindKernel(KernelName);

            return skyViewComputeShader;
        }

        private void ApplyCommonParameters(CommandBuffer cmd, ComputeShader shader, in AtmosphereParameters parameters)
        {
            cmd.SetComputeFloatParam(shader, AtmosphereShaderIDs.GroundRadiusKm, parameters.GroundRadiusKm);
            cmd.SetComputeFloatParam(shader, AtmosphereShaderIDs.TopRadiusKm, parameters.TopRadiusKm);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.RayleighScattering, parameters.RayleighScattering);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.MieScattering, parameters.MieScattering);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.MieAbsorption, parameters.MieAbsorption);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.OzoneAbsorption, parameters.OzoneAbsorption);
            cmd.SetComputeVectorParam(
                shader,
                AtmosphereShaderIDs.ScaleHeights,
                new Vector4(parameters.RayleighScaleHeightKm, parameters.MieScaleHeightKm, 0.0f, 0.0f));
            cmd.SetComputeVectorParam(
                shader,
                AtmosphereShaderIDs.OzoneLayer,
                new Vector4(parameters.OzoneLayerCenterKm, parameters.OzoneLayerHalfWidthKm, 0.0f, 0.0f));
            cmd.SetComputeVectorParam(
                shader,
                AtmosphereShaderIDs.TransmittanceSize,
                new Vector4(
                    parameters.TransmittanceWidth,
                    parameters.TransmittanceHeight,
                    1.0f / parameters.TransmittanceWidth,
                    1.0f / parameters.TransmittanceHeight));
            cmd.SetComputeIntParam(shader, AtmosphereShaderIDs.TransmittanceSteps, parameters.TransmittanceSteps);
        }

        public static void ApplySkyViewParameters(CommandBuffer cmd, ComputeShader shader, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            cmd.SetComputeVectorParam(
                shader,
                AtmosphereShaderIDs.MultiScatteringSize,
                new Vector4(
                    parameters.MultiScatteringWidth,
                    parameters.MultiScatteringHeight,
                    1.0f / parameters.MultiScatteringWidth,
                    1.0f / parameters.MultiScatteringHeight));
            cmd.SetComputeVectorParam(
                shader,
                AtmosphereShaderIDs.SkyViewSize,
                new Vector4(
                    parameters.SkyViewWidth,
                    parameters.SkyViewHeight,
                    1.0f / parameters.SkyViewWidth,
                    1.0f / parameters.SkyViewHeight));
            cmd.SetComputeIntParam(shader, AtmosphereShaderIDs.SkyViewRaySteps, parameters.SkyViewRaySteps);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.SunDirection, viewParameters.SunDirection);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.SunIlluminance, viewParameters.SunIlluminance);
            cmd.SetComputeFloatParam(shader, AtmosphereShaderIDs.MiePhaseG, parameters.MiePhaseG);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraPositionKm, viewParameters.CameraPositionKm);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraBasisRight, viewParameters.CameraBasisRight);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraBasisUp, viewParameters.CameraBasisUp);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraBasisForward, viewParameters.CameraBasisForward);
        }

        public static void ApplySkyViewParameters(ComputeCommandBuffer cmd, ComputeShader shader, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            cmd.SetComputeVectorParam(
                shader,
                AtmosphereShaderIDs.MultiScatteringSize,
                new Vector4(
                    parameters.MultiScatteringWidth,
                    parameters.MultiScatteringHeight,
                    1.0f / parameters.MultiScatteringWidth,
                    1.0f / parameters.MultiScatteringHeight));
            cmd.SetComputeVectorParam(
                shader,
                AtmosphereShaderIDs.SkyViewSize,
                new Vector4(
                    parameters.SkyViewWidth,
                    parameters.SkyViewHeight,
                    1.0f / parameters.SkyViewWidth,
                    1.0f / parameters.SkyViewHeight));
            cmd.SetComputeIntParam(shader, AtmosphereShaderIDs.SkyViewRaySteps, parameters.SkyViewRaySteps);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.SunDirection, viewParameters.SunDirection);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.SunIlluminance, viewParameters.SunIlluminance);
            cmd.SetComputeFloatParam(shader, AtmosphereShaderIDs.MiePhaseG, parameters.MiePhaseG);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraPositionKm, viewParameters.CameraPositionKm);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraBasisRight, viewParameters.CameraBasisRight);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraBasisUp, viewParameters.CameraBasisUp);
            cmd.SetComputeVectorParam(shader, AtmosphereShaderIDs.CameraBasisForward, viewParameters.CameraBasisForward);
        }

        public static void BindSkyViewGlobals(CommandBuffer cmd, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            cmd.SetGlobalVector(AtmosphereShaderIDs.SunDirection, viewParameters.SunDirection);
            cmd.SetGlobalVector(AtmosphereShaderIDs.SunIlluminance, viewParameters.SunIlluminance);
            cmd.SetGlobalFloat(AtmosphereShaderIDs.MiePhaseG, parameters.MiePhaseG);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraPositionKm, viewParameters.CameraPositionKm);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraBasisRight, viewParameters.CameraBasisRight);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraBasisUp, viewParameters.CameraBasisUp);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraBasisForward, viewParameters.CameraBasisForward);
        }

        public static void BindSkyViewGlobals(RasterCommandBuffer cmd, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            cmd.SetGlobalVector(AtmosphereShaderIDs.SunDirection, viewParameters.SunDirection);
            cmd.SetGlobalVector(AtmosphereShaderIDs.SunIlluminance, viewParameters.SunIlluminance);
            cmd.SetGlobalFloat(AtmosphereShaderIDs.MiePhaseG, parameters.MiePhaseG);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraPositionKm, viewParameters.CameraPositionKm);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraBasisRight, viewParameters.CameraBasisRight);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraBasisUp, viewParameters.CameraBasisUp);
            cmd.SetGlobalVector(AtmosphereShaderIDs.CameraBasisForward, viewParameters.CameraBasisForward);
        }

        private void ReleaseTextures(ref RenderTexture texture, ref RTHandle handle)
        {
            if (handle != null)
            {
                handle.Release();
                handle = null;
            }

            if (texture != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(texture);
                else
                    Object.DestroyImmediate(texture);

                texture = null;
            }
        }

        private void LogUnsupportedCompute()
        {
            if (loggedUnsupportedCompute)
                return;

            Debug.LogWarning("Atmosphere: compute shaders are not supported on this device. Atmosphere LUT generation is disabled.");
            loggedUnsupportedCompute = true;
        }

        private void LogBuild(in AtmosphereParameters parameters, string label, int width, int height)
        {
            if (loggedBuildInfo && !Application.isEditor && !Debug.isDebugBuild)
                return;

            Debug.Log(
                $"Atmosphere: built {label} LUT {width}x{height}, transHash {parameters.TransmittanceHash}, multiHash {parameters.MultiScatteringHash}, compute {SystemInfo.supportsComputeShaders}.");
            loggedBuildInfo = true;
        }
    }
}
