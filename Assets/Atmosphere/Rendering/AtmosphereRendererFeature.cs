using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Landscape.Atmosphere
{
    public sealed class AtmosphereRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
        [SerializeField] private bool runInSceneView = true;

        private AtmosphereTransmittancePass transmittancePass;
        private AtmosphereMultiScatteringPass multiScatteringPass;

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
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (transmittancePass == null)
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
        }
    }
}
