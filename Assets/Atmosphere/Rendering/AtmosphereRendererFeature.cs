using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Atmosphere.Rendering
{
    public sealed class AtmosphereRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
        [SerializeField] private bool runInSceneView = true;

        private AtmosphereTransmittancePass transmittancePass;
        private AtmosphereMultiScatteringPass multiScatteringPass;
        private AtmosphereSkyViewPass skyViewPass;

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
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (transmittancePass == null || multiScatteringPass == null || skyViewPass == null)
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
        }
    }
}
