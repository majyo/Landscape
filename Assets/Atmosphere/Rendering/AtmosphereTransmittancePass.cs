using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Landscape.Atmosphere
{
    public sealed class AtmosphereTransmittancePass : ScriptableRenderPass
    {
        private const string ProfilingName = "Atmosphere Transmittance LUT";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingName);

        public AtmosphereTransmittancePass()
        {
            profilingSampler = ProfilingSampler;
        }

#pragma warning disable CS0618
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForRender(out AtmosphereParameters parameters))
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ProfilingName);
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                if (controller.NeedsTransmittanceRebuild(parameters))
                    controller.RenderTransmittance(cmd, parameters);
                else
                    controller.BindGlobals(cmd, parameters);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0618

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForRender(out AtmosphereParameters parameters))
                return;

            if (controller.TransmittanceHandle == null)
                return;

            TextureHandle transmittanceHandle = renderGraph.ImportTexture(controller.TransmittanceHandle);

            if (controller.NeedsTransmittanceRebuild(parameters))
            {
                ComputeShader computeShader = controller.TransmittanceComputeShader;
                int kernelIndex = controller.TransmittanceKernelIndex;
                if (computeShader == null || kernelIndex < 0)
                    return;

                using (var builder = renderGraph.AddComputePass<ComputePassData>(ProfilingName, out ComputePassData passData))
                {
                    passData.computeShader = computeShader;
                    passData.kernelIndex = kernelIndex;
                    passData.output = transmittanceHandle;
                    passData.parameters = parameters;

                    builder.UseTexture(passData.output, AccessFlags.WriteAll);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                    {
                        context.cmd.SetComputeFloatParam(data.computeShader, AtmosphereShaderIDs.GroundRadiusKm, data.parameters.GroundRadiusKm);
                        context.cmd.SetComputeFloatParam(data.computeShader, AtmosphereShaderIDs.TopRadiusKm, data.parameters.TopRadiusKm);
                        context.cmd.SetComputeVectorParam(data.computeShader, AtmosphereShaderIDs.RayleighScattering, data.parameters.RayleighScattering);
                        context.cmd.SetComputeVectorParam(data.computeShader, AtmosphereShaderIDs.MieScattering, data.parameters.MieScattering);
                        context.cmd.SetComputeVectorParam(data.computeShader, AtmosphereShaderIDs.MieAbsorption, data.parameters.MieAbsorption);
                        context.cmd.SetComputeVectorParam(data.computeShader, AtmosphereShaderIDs.OzoneAbsorption, data.parameters.OzoneAbsorption);
                        context.cmd.SetComputeVectorParam(
                            data.computeShader,
                            AtmosphereShaderIDs.ScaleHeights,
                            new Vector4(data.parameters.RayleighScaleHeightKm, data.parameters.MieScaleHeightKm, 0.0f, 0.0f));
                        context.cmd.SetComputeVectorParam(
                            data.computeShader,
                            AtmosphereShaderIDs.OzoneLayer,
                            new Vector4(data.parameters.OzoneLayerCenterKm, data.parameters.OzoneLayerHalfWidthKm, 0.0f, 0.0f));
                        context.cmd.SetComputeVectorParam(
                            data.computeShader,
                            AtmosphereShaderIDs.TransmittanceSize,
                            new Vector4(
                                data.parameters.TransmittanceWidth,
                                data.parameters.TransmittanceHeight,
                                1.0f / data.parameters.TransmittanceWidth,
                                1.0f / data.parameters.TransmittanceHeight));
                        context.cmd.SetComputeIntParam(data.computeShader, AtmosphereShaderIDs.TransmittanceSteps, data.parameters.TransmittanceSteps);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.TransmittanceLut, data.output);
                        context.cmd.DispatchCompute(
                            data.computeShader,
                            data.kernelIndex,
                            Mathf.CeilToInt(data.parameters.TransmittanceWidth / 8.0f),
                            Mathf.CeilToInt(data.parameters.TransmittanceHeight / 8.0f),
                            1);
                    });
                }
            }

            AddBindGlobalsPass(renderGraph, transmittanceHandle, parameters);
        }

        private static void AddBindGlobalsPass(RenderGraph renderGraph, TextureHandle textureHandle, in AtmosphereParameters parameters)
        {
            using (var builder = renderGraph.AddRasterRenderPass<BindGlobalsPassData>("Atmosphere Bind Globals", out BindGlobalsPassData passData))
            {
                passData.transmittanceSize = new Vector4(
                    parameters.TransmittanceWidth,
                    parameters.TransmittanceHeight,
                    1.0f / parameters.TransmittanceWidth,
                    1.0f / parameters.TransmittanceHeight);

                builder.UseTexture(textureHandle, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(textureHandle, AtmosphereShaderIDs.TransmittanceLut);
                builder.SetRenderFunc(static (BindGlobalsPassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(AtmosphereShaderIDs.TransmittanceSize, data.transmittanceSize);
                });
            }
        }

        private sealed class ComputePassData
        {
            public ComputeShader computeShader;
            public int kernelIndex;
            public TextureHandle output;
            public AtmosphereParameters parameters;
        }

        private sealed class BindGlobalsPassData
        {
            public Vector4 transmittanceSize;
        }
    }
}
