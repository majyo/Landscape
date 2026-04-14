using UnityEngine;
using UnityEngine.Rendering;

namespace Landscape.Atmosphere
{
    public sealed class AtmosphereLutManager
    {
        private const string TransmittanceComputeShaderPath = "Atmosphere/AtmosphereTransmittance";
        private const string MultiScatteringComputeShaderPath = "Atmosphere/AtmosphereMultiScattering";
        private const string KernelName = "CSMain";

        private ComputeShader transmittanceComputeShader;
        private ComputeShader multiScatteringComputeShader;
        private int transmittanceKernelIndex = -1;
        private int multiScatteringKernelIndex = -1;

        private RenderTexture transmittanceTexture;
        private RenderTexture multiScatteringTexture;
        private RTHandle transmittanceHandle;
        private RTHandle multiScatteringHandle;

        private int currentTransmittanceHash = int.MinValue;
        private int currentMultiScatteringHash = int.MinValue;

        private bool loggedUnsupportedCompute;
        private bool loggedBuildInfo;
        private bool loggedMissingTransmittanceShader;
        private bool loggedMissingMultiScatteringShader;

        public ComputeShader TransmittanceComputeShader => LoadTransmittanceComputeShader();
        public ComputeShader MultiScatteringComputeShader => LoadMultiScatteringComputeShader();
        public int TransmittanceKernelIndex => transmittanceKernelIndex;
        public int MultiScatteringKernelIndex => multiScatteringKernelIndex;
        public RenderTexture TransmittanceTexture => transmittanceTexture;
        public RenderTexture MultiScatteringTexture => multiScatteringTexture;
        public RTHandle TransmittanceHandle => transmittanceHandle;
        public RTHandle MultiScatteringHandle => multiScatteringHandle;

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
            LogBuild(parameters, "transmittance", parameters.TransmittanceWidth, parameters.TransmittanceHeight);
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
            LogBuild(parameters, "multi-scattering", parameters.MultiScatteringWidth, parameters.MultiScatteringHeight);
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
        }

        public void Release()
        {
            ReleaseTextures(ref transmittanceTexture, ref transmittanceHandle);
            ReleaseTextures(ref multiScatteringTexture, ref multiScatteringHandle);
            transmittanceComputeShader = null;
            multiScatteringComputeShader = null;
            transmittanceKernelIndex = -1;
            multiScatteringKernelIndex = -1;
            currentTransmittanceHash = int.MinValue;
            currentMultiScatteringHash = int.MinValue;
            loggedBuildInfo = false;
            loggedMissingTransmittanceShader = false;
            loggedMissingMultiScatteringShader = false;
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
