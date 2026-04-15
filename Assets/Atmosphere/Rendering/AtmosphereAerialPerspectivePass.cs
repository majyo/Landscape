using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Atmosphere.Rendering
{
    public sealed class AtmosphereAerialPerspectivePass : ScriptableRenderPass
    {
        private const string ProfilingName = "Atmosphere Aerial Perspective LUT";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingName);

        public AtmosphereAerialPerspectivePass()
        {
            profilingSampler = ProfilingSampler;
        }

#pragma warning disable CS0618
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForAerialPerspective(renderingData.cameraData.camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters))
                return;

            if (controller.TransmittanceHandle == null || controller.MultiScatteringHandle == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ProfilingName);
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                if (controller.NeedsAerialPerspectiveRebuild(parameters, viewParameters))
                    controller.RenderAerialPerspective(cmd, parameters, viewParameters);
                else
                    controller.BindAerialPerspectiveGlobals(cmd, parameters, viewParameters);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0618

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForAerialPerspective(cameraData.camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters))
                return;

            if (controller.TransmittanceHandle == null
                || controller.MultiScatteringHandle == null
                || controller.AerialScatteringHandle == null
                || controller.AerialTransmittanceHandle == null)
                return;

            TextureHandle transmittanceHandle = renderGraph.ImportTexture(controller.TransmittanceHandle);
            TextureHandle multiScatteringHandle = renderGraph.ImportTexture(controller.MultiScatteringHandle);
            TextureHandle scatteringHandle = renderGraph.ImportTexture(controller.AerialScatteringHandle);
            TextureHandle transmittanceVolumeHandle = renderGraph.ImportTexture(controller.AerialTransmittanceHandle);

            if (controller.NeedsAerialPerspectiveRebuild(parameters, viewParameters))
            {
                ComputeShader computeShader = controller.AerialPerspectiveComputeShader;
                int kernelIndex = controller.AerialPerspectiveKernelIndex;
                if (computeShader == null || kernelIndex < 0)
                    return;

                using (var builder = renderGraph.AddComputePass<ComputePassData>(ProfilingName, out ComputePassData passData))
                {
                    passData.computeShader = computeShader;
                    passData.kernelIndex = kernelIndex;
                    passData.transmittance = transmittanceHandle;
                    passData.multiScattering = multiScatteringHandle;
                    passData.scattering = scatteringHandle;
                    passData.transmittanceVolume = transmittanceVolumeHandle;
                    passData.parameters = parameters;
                    passData.viewParameters = viewParameters;

                    builder.UseTexture(passData.transmittance, AccessFlags.Read);
                    builder.UseTexture(passData.multiScattering, AccessFlags.Read);
                    builder.UseTexture(passData.scattering, AccessFlags.WriteAll);
                    builder.UseTexture(passData.transmittanceVolume, AccessFlags.WriteAll);
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
                        AtmosphereLutManager.ApplyAerialPerspectiveParameters(context.cmd, data.computeShader, data.parameters, data.viewParameters);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.TransmittanceLut, data.transmittance);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.MultiScatteringLut, data.multiScattering);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.AerialScatteringLut, data.scattering);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.AerialTransmittanceLut, data.transmittanceVolume);
                        context.cmd.DispatchCompute(
                            data.computeShader,
                            data.kernelIndex,
                            Mathf.CeilToInt(data.parameters.AerialPerspectiveWidth / 8.0f),
                            Mathf.CeilToInt(data.parameters.AerialPerspectiveHeight / 8.0f),
                            1);
                        AtmosphereController.Instance?.MarkAerialPerspectiveRendered(data.parameters, data.viewParameters);
                    });
                }
            }

            AddBindGlobalsPass(renderGraph, scatteringHandle, transmittanceVolumeHandle, parameters, viewParameters);
        }

        private static void AddBindGlobalsPass(
            RenderGraph renderGraph,
            TextureHandle scatteringHandle,
            TextureHandle transmittanceHandle,
            in AtmosphereParameters parameters,
            in AtmosphereViewParameters viewParameters)
        {
            using (var builder = renderGraph.AddRasterRenderPass<BindGlobalsPassData>("Atmosphere Bind Aerial Globals", out BindGlobalsPassData passData))
            {
                passData.parameters = parameters;
                passData.viewParameters = viewParameters;

                builder.UseTexture(scatteringHandle, AccessFlags.Read);
                builder.UseTexture(transmittanceHandle, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(scatteringHandle, AtmosphereShaderIDs.AerialScatteringLut);
                builder.SetGlobalTextureAfterPass(transmittanceHandle, AtmosphereShaderIDs.AerialTransmittanceLut);
                builder.SetRenderFunc(static (BindGlobalsPassData data, RasterGraphContext context) =>
                {
                    AtmosphereLutManager.BindAerialPerspectiveGlobals(context.cmd, data.parameters, data.viewParameters);
                });
            }
        }

        private sealed class ComputePassData
        {
            public ComputeShader computeShader;
            public int kernelIndex;
            public TextureHandle transmittance;
            public TextureHandle multiScattering;
            public TextureHandle scattering;
            public TextureHandle transmittanceVolume;
            public AtmosphereParameters parameters;
            public AtmosphereViewParameters viewParameters;
        }

        private sealed class BindGlobalsPassData
        {
            public AtmosphereParameters parameters;
            public AtmosphereViewParameters viewParameters;
        }
    }
}
