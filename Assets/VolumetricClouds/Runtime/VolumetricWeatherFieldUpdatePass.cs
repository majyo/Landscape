using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using VolumetricClouds.Runtime;

namespace VolumetricClouds.Rendering
{
    public sealed class VolumetricWeatherFieldUpdatePass : ScriptableRenderPass
    {
        private const string ProfilingName = "Volumetric Weather Field Update";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingName);

        public VolumetricWeatherFieldUpdatePass()
        {
            profilingSampler = ProfilingSampler;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!VolumetricCloudRenderUtilities.ShouldRenderForCamera(cameraData.camera))
                return;

            VolumetricCloudController controller = VolumetricCloudController.Instance;
            if (controller == null
                || !controller.TryPrepareWeatherField(out VolumetricCloudWeatherContext weatherContext, out _, out bool shouldUpdate)
                || !weatherContext.EnableRuntimeWeatherField
                || !shouldUpdate)
            {
                return;
            }

            if (!controller.TryGetWeatherFieldUpdateComputeShader(out ComputeShader computeShader, out int kernelIndex))
                return;

            if (controller.WeatherFieldHandle == null || controller.WeatherFieldScratchHandle == null)
                return;

            TextureHandle sourceHandle = renderGraph.ImportTexture(controller.WeatherFieldHandle);
            TextureHandle outputHandle = renderGraph.ImportTexture(controller.WeatherFieldScratchHandle);
            double updateTimeSeconds = Time.realtimeSinceStartupAsDouble;

            Shader.SetGlobalTexture(
                VolumetricCloudShaderIDs.CloudWeatherSeedTexture,
                weatherContext.WeatherSeedTexture != null ? weatherContext.WeatherSeedTexture : Texture2D.whiteTexture);

            using (var builder = renderGraph.AddComputePass<ComputePassData>(ProfilingName, out ComputePassData passData))
            {
                passData.computeShader = computeShader;
                passData.kernelIndex = kernelIndex;
                passData.sourceHandle = sourceHandle;
                passData.outputHandle = outputHandle;
                passData.weatherContext = weatherContext;

                builder.UseTexture(passData.sourceHandle, AccessFlags.Read);
                builder.UseTexture(passData.outputHandle, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                {
                    ApplyWeatherFieldParameters(context.cmd, data);
                });
            }

            controller.CommitScheduledWeatherFieldUpdate(updateTimeSeconds);
        }

        private static void ApplyWeatherFieldParameters(ComputeCommandBuffer cmd, ComputePassData data)
        {
            VolumetricCloudWeatherContext weatherContext = data.weatherContext;
            int resolution = Mathf.Max(1, weatherContext.WeatherFieldResolution);
            float invResolution = 1.0f / resolution;
            float seedTiling = 4.0f;
            float seedInjection = weatherContext.InitializeFromSeed ? 1.0f : 0.18f;
            float growthFactor = 0.22f;
            float dissipationFactor = 0.2f;

            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, VolumetricCloudShaderIDs.CloudWeatherFieldTexture, data.sourceHandle);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, VolumetricCloudShaderIDs.CloudWeatherFieldOutputTexture, data.outputHandle);
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudWeatherFieldSize,
                new Vector4(resolution, resolution, invResolution, invResolution));
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudWeatherFieldData,
                new Vector4(
                    weatherContext.WeatherFieldScaleKm,
                    weatherContext.WeatherFieldOffsetKm.x,
                    weatherContext.WeatherFieldOffsetKm.y,
                    weatherContext.GlobalCoverageGain));
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudWeatherPresetData,
                new Vector4(
                    weatherContext.TargetCoverage,
                    weatherContext.CloudType,
                    weatherContext.Wetness,
                    weatherContext.DensityBias));
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudWeatherRemapData,
                new Vector4(
                    weatherContext.CoverageBias,
                    weatherContext.CoverageContrast,
                    weatherContext.DetailErosionStrength,
                    weatherContext.EvolutionSpeed));
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudWeatherUpdateData,
                new Vector4(
                    weatherContext.DeltaTimeSeconds,
                    weatherContext.InitializeFromSeed ? 1.0f : 0.0f,
                    weatherContext.WeatherTransition01,
                    0.0f));
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudWeatherWindFieldData,
                new Vector4(
                    weatherContext.WindDirection.x,
                    weatherContext.WindDirection.y,
                    weatherContext.WindSpeedKmPerSecond,
                    weatherContext.EvolutionSpeed));
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudWeatherSeedScaleData,
                new Vector4(
                    seedTiling,
                    seedInjection,
                    growthFactor,
                    dissipationFactor));
            cmd.SetComputeIntParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudHasWeatherSeed,
                weatherContext.WeatherSeedTexture != null ? 1 : 0);

            cmd.DispatchCompute(
                data.computeShader,
                data.kernelIndex,
                Mathf.CeilToInt(resolution / 8.0f),
                Mathf.CeilToInt(resolution / 8.0f),
                1);
        }

        private sealed class ComputePassData
        {
            public ComputeShader computeShader;
            public int kernelIndex;
            public TextureHandle sourceHandle;
            public TextureHandle outputHandle;
            public VolumetricCloudWeatherContext weatherContext;
        }
    }
}
