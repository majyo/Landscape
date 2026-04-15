using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Atmosphere.Rendering
{
    public sealed class AtmosphereAerialCompositePass : ScriptableRenderPass
    {
        private const string ProfilingName = "Atmosphere Aerial Composite";

        private readonly Material compositeMaterial;
        private RTHandle tempColorHandle;

        public AtmosphereAerialCompositePass(Material material)
        {
            compositeMaterial = material;
            profilingSampler = new ProfilingSampler(ProfilingName);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

#pragma warning disable CS0618
        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (compositeMaterial == null)
                return;

            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForAerialPerspective(renderingData.cameraData.camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters))
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ProfilingName);
            using (new ProfilingScope(cmd, profilingSampler))
            {
                controller.BindAerialPerspectiveGlobals(cmd, parameters, viewParameters);
                RTHandle colorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                EnsureTempHandle(renderingData.cameraData.cameraTargetDescriptor);
                if (tempColorHandle == null)
                {
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                    return;
                }

                Blitter.BlitCameraTexture(cmd, colorTarget, tempColorHandle, compositeMaterial, 0);
                Blitter.BlitCameraTexture(cmd, tempColorHandle, colorTarget, 0, false);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0618

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (compositeMaterial == null)
                return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            AtmosphereController controller = AtmosphereController.Instance;
            if (controller == null || !controller.TryPrepareForAerialPerspective(cameraData.camera, out AtmosphereParameters parameters, out AtmosphereViewParameters viewParameters))
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle depth = resourceData.cameraDepthTexture;
            if (!source.IsValid() || !depth.IsValid())
                return;

            Shader.SetGlobalVector(
                AtmosphereShaderIDs.AerialPerspectiveSize,
                new Vector4(
                    parameters.AerialPerspectiveWidth,
                    parameters.AerialPerspectiveHeight,
                    parameters.AerialPerspectiveDepth,
                    1.0f / parameters.AerialPerspectiveDepth));
            Shader.SetGlobalFloat(AtmosphereShaderIDs.AerialPerspectiveMaxDistanceKm, parameters.AerialPerspectiveMaxDistanceKm);
            Shader.SetGlobalVector(AtmosphereShaderIDs.SunDirection, viewParameters.SunDirection);
            Shader.SetGlobalVector(AtmosphereShaderIDs.SunIlluminance, viewParameters.SunIlluminance);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.MiePhaseG, parameters.MiePhaseG);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraPositionKm, viewParameters.CameraPositionKm);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraBasisRight, viewParameters.CameraBasisRight);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraBasisUp, viewParameters.CameraBasisUp);
            Shader.SetGlobalVector(AtmosphereShaderIDs.CameraBasisForward, viewParameters.CameraBasisForward);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.CameraTanHalfVerticalFov, viewParameters.TanHalfVerticalFov);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.CameraAspectRatio, viewParameters.AspectRatio);

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_AtmosphereAerialCompositeTemp", false);

            RenderGraphUtils.BlitMaterialParameters compositeParameters = new(source, temp, compositeMaterial, 0);
            renderGraph.AddBlitPass(compositeParameters, ProfilingName);

            RenderGraphUtils.BlitMaterialParameters copyBackParameters = new(temp, source, null, 0);
            renderGraph.AddBlitPass(copyBackParameters, "Atmosphere Aerial Composite Copy Back");
        }

        public void Dispose()
        {
            tempColorHandle?.Release();
            tempColorHandle = null;
        }

        private void EnsureTempHandle(RenderTextureDescriptor descriptor)
        {
            descriptor.msaaSamples = 1;
            descriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref tempColorHandle,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_AtmosphereAerialCompositeTemp");
        }
    }
}
