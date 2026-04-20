using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VolumetricClouds.Rendering;

namespace Atmosphere.Rendering
{
    public sealed class AtmosphereRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
        [SerializeField] private bool runInSceneView = true;
        [SerializeField] private Shader aerialCompositeShader;
        [SerializeField] private Shader volumetricCloudCompositeShader;

        private AtmosphereTransmittancePass transmittancePass;
        private AtmosphereMultiScatteringPass multiScatteringPass;
        private AtmosphereSkyViewPass skyViewPass;
        private AtmosphereAerialPerspectivePass aerialPerspectivePass;
        private VolumetricWeatherFieldUpdatePass volumetricWeatherFieldUpdatePass;
        private VolumetricCloudRenderPass volumetricCloudRenderPass;
        private VolumetricCloudTemporalAccumulationPass volumetricCloudTemporalAccumulationPass;
        private VolumetricCloudCompositePass volumetricCloudCompositePass;
        private AtmosphereAerialCompositePass aerialCompositePass;
        private Material volumetricCloudCompositeMaterial;
        private Material aerialCompositeMaterial;
        private bool loggedMissingVolumetricCloudCompositeShader;

        public override void Create()
        {
            transmittancePass = new AtmosphereTransmittancePass
            {
                renderPassEvent = renderPassEvent
            };

            multiScatteringPass = new AtmosphereMultiScatteringPass
            {
                renderPassEvent = renderPassEvent
            };

            skyViewPass = new AtmosphereSkyViewPass
            {
                renderPassEvent = renderPassEvent
            };

            aerialPerspectivePass = new AtmosphereAerialPerspectivePass
            {
                renderPassEvent = renderPassEvent
            };

            volumetricWeatherFieldUpdatePass = new VolumetricWeatherFieldUpdatePass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };

            volumetricCloudRenderPass = new VolumetricCloudRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };

            volumetricCloudTemporalAccumulationPass = new VolumetricCloudTemporalAccumulationPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };

            if (volumetricCloudCompositeShader == null)
                volumetricCloudCompositeShader = Shader.Find("Hidden/Landscape/VolumetricCloudComposite");

            if (volumetricCloudCompositeShader != null)
                volumetricCloudCompositeMaterial = CoreUtils.CreateEngineMaterial(volumetricCloudCompositeShader);
            else if (!loggedMissingVolumetricCloudCompositeShader)
            {
                Debug.LogError("VolumetricClouds: failed to resolve Hidden/Landscape/VolumetricCloudComposite for AtmosphereRendererFeature integration.");
                loggedMissingVolumetricCloudCompositeShader = true;
            }

            volumetricCloudCompositePass = new VolumetricCloudCompositePass(volumetricCloudCompositeMaterial)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };

            if (aerialCompositeShader == null)
                aerialCompositeShader = Shader.Find("Hidden/Landscape/AtmosphereAerialComposite");

            if (aerialCompositeShader != null)
                aerialCompositeMaterial = CoreUtils.CreateEngineMaterial(aerialCompositeShader);

            aerialCompositePass = new AtmosphereAerialCompositePass(aerialCompositeMaterial)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (transmittancePass == null
                || multiScatteringPass == null
                || skyViewPass == null
                || aerialPerspectivePass == null
                || volumetricWeatherFieldUpdatePass == null
                || volumetricCloudRenderPass == null
                || volumetricCloudTemporalAccumulationPass == null
                || volumetricCloudCompositePass == null
                || aerialCompositePass == null)
            {
                Create();
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            if (!runInSceneView && cameraType == CameraType.SceneView)
                return;

            if (AtmosphereController.Instance == null)
                return;

            renderer.EnqueuePass(transmittancePass);
            renderer.EnqueuePass(multiScatteringPass);
            renderer.EnqueuePass(skyViewPass);
            renderer.EnqueuePass(aerialPerspectivePass);
            renderer.EnqueuePass(volumetricWeatherFieldUpdatePass);
            renderer.EnqueuePass(volumetricCloudRenderPass);
            renderer.EnqueuePass(volumetricCloudTemporalAccumulationPass);
            renderer.EnqueuePass(volumetricCloudCompositePass);
            renderer.EnqueuePass(aerialCompositePass);
        }

        protected override void Dispose(bool disposing)
        {
            volumetricCloudCompositePass?.Dispose();
            aerialCompositePass?.Dispose();
            CoreUtils.Destroy(volumetricCloudCompositeMaterial);
            volumetricCloudCompositeMaterial = null;
            CoreUtils.Destroy(aerialCompositeMaterial);
            aerialCompositeMaterial = null;
        }
    }
}
