using UnityEngine;
using UnityEngine.Rendering;

namespace Landscape.Atmosphere
{
    public sealed class AtmosphereLutManager
    {
        private const string ComputeShaderPath = "Atmosphere/AtmosphereTransmittance";
        private const string KernelName = "CSMain";

        private ComputeShader computeShader;
        private int kernelIndex = -1;
        private RenderTexture transmittanceTexture;
        private RTHandle transmittanceHandle;
        private int currentHash = int.MinValue;
        private bool loggedUnsupportedCompute;
        private bool loggedBuildInfo;
        private bool loggedMissingShader;

        public ComputeShader ComputeShader => LoadComputeShader();
        public int KernelIndex => kernelIndex;
        public RenderTexture TransmittanceTexture => transmittanceTexture;
        public RTHandle TransmittanceHandle => transmittanceHandle;

        public bool EnsureResources(in AtmosphereParameters parameters)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                if (!loggedUnsupportedCompute)
                {
                    Debug.LogWarning("Atmosphere: compute shaders are not supported on this device. Phase1 transmittance LUT is disabled.");
                    loggedUnsupportedCompute = true;
                }

                ReleaseTextures();
                return false;
            }

            if (LoadComputeShader() == null)
            {
                if (!loggedMissingShader)
                {
                    Debug.LogError("Atmosphere: failed to load Resources/Atmosphere/AtmosphereTransmittance.compute.");
                    loggedMissingShader = true;
                }

                ReleaseTextures();
                return false;
            }

            EnsureTexture(parameters.TransmittanceWidth, parameters.TransmittanceHeight);
            return transmittanceTexture != null && transmittanceHandle != null;
        }

        public bool NeedsRebuild(in AtmosphereParameters parameters)
        {
            return transmittanceTexture == null
                || transmittanceHandle == null
                || currentHash != parameters.Hash;
        }

        public void RenderTransmittance(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (!EnsureResources(parameters))
                return;

            ApplyParameters(cmd, parameters);
            cmd.SetComputeTextureParam(computeShader, kernelIndex, AtmosphereShaderIDs.TransmittanceLut, transmittanceTexture);
            cmd.DispatchCompute(
                computeShader,
                kernelIndex,
                Mathf.CeilToInt(parameters.TransmittanceWidth / 8.0f),
                Mathf.CeilToInt(parameters.TransmittanceHeight / 8.0f),
                1);

            currentHash = parameters.Hash;
            LogBuild(parameters);
        }

        public void BindGlobals(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (transmittanceTexture == null)
                return;

            cmd.SetGlobalTexture(AtmosphereShaderIDs.TransmittanceLut, transmittanceTexture);
            cmd.SetGlobalVector(
                AtmosphereShaderIDs.TransmittanceSize,
                new Vector4(
                    parameters.TransmittanceWidth,
                    parameters.TransmittanceHeight,
                    1.0f / parameters.TransmittanceWidth,
                    1.0f / parameters.TransmittanceHeight));
        }

        public void Release()
        {
            ReleaseTextures();
            computeShader = null;
            kernelIndex = -1;
            currentHash = int.MinValue;
            loggedBuildInfo = false;
            loggedMissingShader = false;
        }

        private void EnsureTexture(int width, int height)
        {
            if (transmittanceTexture != null && (transmittanceTexture.width != width || transmittanceTexture.height != height))
            {
                ReleaseTextures();
            }

            if (transmittanceTexture != null && transmittanceHandle != null)
                return;

            transmittanceTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "Atmosphere Transmittance LUT",
                enableRandomWrite = true,
                dimension = TextureDimension.Tex2D,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            transmittanceTexture.Create();
            transmittanceHandle = RTHandles.Alloc(transmittanceTexture, false);
        }

        private ComputeShader LoadComputeShader()
        {
            if (computeShader != null)
                return computeShader;

            computeShader = Resources.Load<ComputeShader>(ComputeShaderPath);
            if (computeShader != null)
                kernelIndex = computeShader.FindKernel(KernelName);

            return computeShader;
        }

        private void ApplyParameters(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            cmd.SetComputeFloatParam(computeShader, AtmosphereShaderIDs.GroundRadiusKm, parameters.GroundRadiusKm);
            cmd.SetComputeFloatParam(computeShader, AtmosphereShaderIDs.TopRadiusKm, parameters.TopRadiusKm);
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.RayleighScattering, parameters.RayleighScattering);
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.MieScattering, parameters.MieScattering);
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.MieAbsorption, parameters.MieAbsorption);
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.OzoneAbsorption, parameters.OzoneAbsorption);
            cmd.SetComputeVectorParam(
                computeShader,
                AtmosphereShaderIDs.ScaleHeights,
                new Vector4(parameters.RayleighScaleHeightKm, parameters.MieScaleHeightKm, 0.0f, 0.0f));
            cmd.SetComputeVectorParam(
                computeShader,
                AtmosphereShaderIDs.OzoneLayer,
                new Vector4(parameters.OzoneLayerCenterKm, parameters.OzoneLayerHalfWidthKm, 0.0f, 0.0f));
            cmd.SetComputeVectorParam(
                computeShader,
                AtmosphereShaderIDs.TransmittanceSize,
                new Vector4(
                    parameters.TransmittanceWidth,
                    parameters.TransmittanceHeight,
                    1.0f / parameters.TransmittanceWidth,
                    1.0f / parameters.TransmittanceHeight));
            cmd.SetComputeIntParam(computeShader, AtmosphereShaderIDs.TransmittanceSteps, parameters.TransmittanceSteps);
        }

        private void ReleaseTextures()
        {
            if (transmittanceHandle != null)
            {
                transmittanceHandle.Release();
                transmittanceHandle = null;
            }

            if (transmittanceTexture != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(transmittanceTexture);
                else
                    Object.DestroyImmediate(transmittanceTexture);

                transmittanceTexture = null;
            }
        }

        private void LogBuild(in AtmosphereParameters parameters)
        {
            if (loggedBuildInfo && !Application.isEditor && !Debug.isDebugBuild)
                return;

            Debug.Log(
                $"Atmosphere: built transmittance LUT {parameters.TransmittanceWidth}x{parameters.TransmittanceHeight}, steps {parameters.TransmittanceSteps}, format ARGBHalf, hash {parameters.Hash}, compute {SystemInfo.supportsComputeShaders}.");
            loggedBuildInfo = true;
        }
    }
}
