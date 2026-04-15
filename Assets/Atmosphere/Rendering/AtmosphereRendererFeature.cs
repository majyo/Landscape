using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Atmosphere.Rendering
{
    public sealed class AtmosphereRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
        [SerializeField] private bool runInSceneView = true;
        [SerializeField] private Shader aerialCompositeShader;

        private AtmosphereTransmittancePass transmittancePass;
        private AtmosphereMultiScatteringPass multiScatteringPass;
        private AtmosphereSkyViewPass skyViewPass;
        private AtmosphereAerialPerspectivePass aerialPerspectivePass;
        private AtmosphereAerialCompositePass aerialCompositePass;
        private Material aerialCompositeMaterial;

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
            if (transmittancePass == null || multiScatteringPass == null || skyViewPass == null || aerialPerspectivePass == null || aerialCompositePass == null)
                Create();

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
            renderer.EnqueuePass(aerialCompositePass);
        }

        protected override void Dispose(bool disposing)
        {
            aerialCompositePass?.Dispose();
            CoreUtils.Destroy(aerialCompositeMaterial);
            aerialCompositeMaterial = null;
        }
    }
}
