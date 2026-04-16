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

        public AtmosphereAerialCompositePass(Material material)
        {
            compositeMaterial = material;
            profilingSampler = new ProfilingSampler(ProfilingName);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

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
            Shader.SetGlobalFloat(AtmosphereShaderIDs.SkyExposure, parameters.SkyExposure);
            Shader.SetGlobalFloat(AtmosphereShaderIDs.AerialPerspectiveExposure, parameters.AerialPerspectiveExposure);
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
            renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero, passName: "Atmosphere Aerial Composite Copy Back");
        }

        public void Dispose()
        {
        }
    }
}
