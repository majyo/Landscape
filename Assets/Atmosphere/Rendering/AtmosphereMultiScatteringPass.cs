using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Atmosphere.Rendering
{
    public sealed class AtmosphereMultiScatteringPass : ScriptableRenderPass
    {
        private const string ProfilingName = "Atmosphere Multi-scattering LUT";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingName);

        public AtmosphereMultiScatteringPass()
        {
            profilingSampler = ProfilingSampler;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForMultiScattering(out AtmosphereParameters parameters))
                return;

            if (controller.TransmittanceHandle == null || controller.MultiScatteringHandle == null)
                return;

            TextureHandle transmittanceHandle = renderGraph.ImportTexture(controller.TransmittanceHandle);
            TextureHandle multiScatteringHandle = renderGraph.ImportTexture(controller.MultiScatteringHandle);

            if (controller.NeedsMultiScatteringRebuild(parameters))
            {
                ComputeShader computeShader = controller.MultiScatteringComputeShader;
                int kernelIndex = controller.MultiScatteringKernelIndex;
                if (computeShader == null || kernelIndex < 0)
                    return;

                using (var builder = renderGraph.AddComputePass<ComputePassData>(ProfilingName, out ComputePassData passData))
                {
                    passData.computeShader = computeShader;
                    passData.kernelIndex = kernelIndex;
                    passData.transmittance = transmittanceHandle;
                    passData.output = multiScatteringHandle;
                    passData.parameters = parameters;

                    builder.UseTexture(passData.transmittance, AccessFlags.Read);
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
                        context.cmd.SetComputeVectorParam(data.computeShader, AtmosphereShaderIDs.GroundAlbedo, data.parameters.GroundAlbedo);
                        context.cmd.SetComputeVectorParam(
                            data.computeShader,
                            AtmosphereShaderIDs.MultiScatteringSize,
                            new Vector4(
                                data.parameters.MultiScatteringWidth,
                                data.parameters.MultiScatteringHeight,
                                1.0f / data.parameters.MultiScatteringWidth,
                                1.0f / data.parameters.MultiScatteringHeight));
                        context.cmd.SetComputeIntParam(data.computeShader, AtmosphereShaderIDs.MultiScatteringSphereSamples, data.parameters.MultiScatteringSphereSamples);
                        context.cmd.SetComputeIntParam(data.computeShader, AtmosphereShaderIDs.MultiScatteringRaySteps, data.parameters.MultiScatteringRaySteps);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.TransmittanceLut, data.transmittance);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.MultiScatteringLut, data.output);
                        context.cmd.DispatchCompute(
                            data.computeShader,
                            data.kernelIndex,
                            Mathf.CeilToInt(data.parameters.MultiScatteringWidth / 8.0f),
                            Mathf.CeilToInt(data.parameters.MultiScatteringHeight / 8.0f),
                            1);
                        AtmosphereController.Instance?.MarkMultiScatteringRendered(data.parameters);
                    });
                }
            }

            AddBindGlobalsPass(renderGraph, multiScatteringHandle, parameters);
        }

        private static void AddBindGlobalsPass(RenderGraph renderGraph, TextureHandle textureHandle, in AtmosphereParameters parameters)
        {
            using (var builder = renderGraph.AddRasterRenderPass<BindGlobalsPassData>("Atmosphere Bind Multi-scattering Globals", out BindGlobalsPassData passData))
            {
                passData.multiScatteringSize = new Vector4(
                    parameters.MultiScatteringWidth,
                    parameters.MultiScatteringHeight,
                    1.0f / parameters.MultiScatteringWidth,
                    1.0f / parameters.MultiScatteringHeight);

                builder.UseTexture(textureHandle, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(textureHandle, AtmosphereShaderIDs.MultiScatteringLut);
                builder.SetRenderFunc(static (BindGlobalsPassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(AtmosphereShaderIDs.MultiScatteringSize, data.multiScatteringSize);
                });
            }
        }

        private sealed class ComputePassData
        {
            public ComputeShader computeShader;
            public int kernelIndex;
            public TextureHandle transmittance;
            public TextureHandle output;
            public AtmosphereParameters parameters;
        }

        private sealed class BindGlobalsPassData
        {
            public Vector4 multiScatteringSize;
        }
    }
}
