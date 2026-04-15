using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Atmosphere.Rendering
{
    public sealed class AtmosphereSkyViewPass : ScriptableRenderPass
    {
        private const string ProfilingName = "Atmosphere Sky-View LUT";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingName);

        public AtmosphereSkyViewPass()
        {
            profilingSampler = ProfilingSampler;
        }

#pragma warning disable CS0618
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForSkyView(renderingData.cameraData.camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters))
                return;

            if (controller.TransmittanceHandle == null || controller.MultiScatteringHandle == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ProfilingName);
            using (new ProfilingScope(cmd, ProfilingSampler))
            {
                if (controller.NeedsSkyViewRebuild(parameters, viewParameters))
                    controller.RenderSkyView(cmd, parameters, viewParameters);
                else
                    controller.BindSkyViewGlobals(cmd, parameters, viewParameters);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0618

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForSkyView(cameraData.camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters))
                return;

            if (controller.TransmittanceHandle == null || controller.MultiScatteringHandle == null || controller.SkyViewHandle == null)
                return;

            TextureHandle transmittanceHandle = renderGraph.ImportTexture(controller.TransmittanceHandle);
            TextureHandle multiScatteringHandle = renderGraph.ImportTexture(controller.MultiScatteringHandle);
            TextureHandle skyViewHandle = renderGraph.ImportTexture(controller.SkyViewHandle);

            if (controller.NeedsSkyViewRebuild(parameters, viewParameters))
            {
                ComputeShader computeShader = controller.SkyViewComputeShader;
                int kernelIndex = controller.SkyViewKernelIndex;
                if (computeShader == null || kernelIndex < 0)
                    return;

                using (var builder = renderGraph.AddComputePass<ComputePassData>(ProfilingName, out ComputePassData passData))
                {
                    passData.computeShader = computeShader;
                    passData.kernelIndex = kernelIndex;
                    passData.transmittance = transmittanceHandle;
                    passData.multiScattering = multiScatteringHandle;
                    passData.output = skyViewHandle;
                    passData.parameters = parameters;
                    passData.viewParameters = viewParameters;

                    builder.UseTexture(passData.transmittance, AccessFlags.Read);
                    builder.UseTexture(passData.multiScattering, AccessFlags.Read);
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
                        AtmosphereLutManager.ApplySkyViewParameters(context.cmd, data.computeShader, data.parameters, data.viewParameters);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.TransmittanceLut, data.transmittance);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.MultiScatteringLut, data.multiScattering);
                        context.cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, AtmosphereShaderIDs.SkyViewLut, data.output);
                        context.cmd.DispatchCompute(
                            data.computeShader,
                            data.kernelIndex,
                            Mathf.CeilToInt(data.parameters.SkyViewWidth / 8.0f),
                            Mathf.CeilToInt(data.parameters.SkyViewHeight / 8.0f),
                            1);
                        AtmosphereController.Instance?.MarkSkyViewRendered(data.parameters, data.viewParameters);
                    });
                }
            }

            AddBindGlobalsPass(renderGraph, skyViewHandle, parameters, viewParameters);
        }

        private static void AddBindGlobalsPass(RenderGraph renderGraph, TextureHandle textureHandle, in AtmosphereParameters parameters, in AtmosphereViewParameters viewParameters)
        {
            using (var builder = renderGraph.AddRasterRenderPass<BindGlobalsPassData>("Atmosphere Bind Sky-View Globals", out BindGlobalsPassData passData))
            {
                passData.skyViewSize = new Vector4(
                    parameters.SkyViewWidth,
                    parameters.SkyViewHeight,
                    1.0f / parameters.SkyViewWidth,
                    1.0f / parameters.SkyViewHeight);
                passData.parameters = parameters;
                passData.viewParameters = viewParameters;

                builder.UseTexture(textureHandle, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(textureHandle, AtmosphereShaderIDs.SkyViewLut);
                builder.SetRenderFunc(static (BindGlobalsPassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(AtmosphereShaderIDs.SkyViewSize, data.skyViewSize);
                    AtmosphereLutManager.BindSkyViewGlobals(context.cmd, data.parameters, data.viewParameters);
                });
            }
        }

        private sealed class ComputePassData
        {
            public ComputeShader computeShader;
            public int kernelIndex;
            public TextureHandle transmittance;
            public TextureHandle multiScattering;
            public TextureHandle output;
            public AtmosphereParameters parameters;
            public AtmosphereViewParameters viewParameters;
        }

        private sealed class BindGlobalsPassData
        {
            public Vector4 skyViewSize;
            public AtmosphereParameters parameters;
            public AtmosphereViewParameters viewParameters;
        }
    }
}
