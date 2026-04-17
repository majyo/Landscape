using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using VolumetricClouds.Runtime;

namespace VolumetricClouds.Rendering
{
    public sealed class VolumetricCloudCompositePass : ScriptableRenderPass
    {
        private const string ProfilingName = "Volumetric Cloud Composite";

        private readonly Material compositeMaterial;

        public VolumetricCloudCompositePass(Material material)
        {
            compositeMaterial = material;
            profilingSampler = new ProfilingSampler(ProfilingName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (compositeMaterial == null)
                return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!VolumetricCloudRenderUtilities.ShouldRenderForCamera(cameraData.camera))
                return;

            VolumetricCloudController controller = VolumetricCloudController.Instance;
            if (controller == null || !controller.TryPrepare(cameraData.camera, out VolumetricCloudParameters parameters))
                return;

            if (controller.TraceHandle == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
                return;

            TextureHandle trace = renderGraph.ImportTexture(controller.TraceHandle);

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_VolumetricCloudCompositeTemp", false);

            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(ProfilingName, out CompositePassData passData))
            {
                passData.source = source;
                passData.trace = trace;
                passData.material = compositeMaterial;
                passData.traceSize = new Vector4(
                    parameters.TraceWidth,
                    parameters.TraceHeight,
                    1.0f / parameters.TraceWidth,
                    1.0f / parameters.TraceHeight);

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(trace, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(VolumetricCloudShaderIDs.VolumetricCloudTexture, data.trace);
                    data.material.SetVector(VolumetricCloudShaderIDs.VolumetricCloudTraceSize, data.traceSize);
                    Blitter.BlitTexture(context.cmd, data.source, Vector2.one, data.material, 0);
                });
            }

            renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero, passName: "Volumetric Cloud Composite Copy Back");
        }

        public void Dispose()
        {
        }

        private sealed class CompositePassData
        {
            public TextureHandle source;
            public TextureHandle trace;
            public Material material;
            public Vector4 traceSize;
        }
    }
}
