using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricClouds.Rendering
{
    public sealed class VolumetricCloudRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        [SerializeField] private bool runInSceneView = true;
        [SerializeField] private Shader compositeShader;

        private VolumetricCloudRenderPass renderPass;
        private VolumetricCloudCompositePass compositePass;
        private Material compositeMaterial;

        public override void Create()
        {
            renderPass = new VolumetricCloudRenderPass
            {
                renderPassEvent = renderPassEvent
            };

            if (compositeShader == null)
                compositeShader = Shader.Find("Hidden/Landscape/VolumetricCloudComposite");

            if (compositeShader != null)
                compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);

            compositePass = new VolumetricCloudCompositePass(compositeMaterial)
            {
                renderPassEvent = renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass == null || compositePass == null)
                Create();

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            if (!runInSceneView && cameraType == CameraType.SceneView)
                return;

            if (!VolumetricCloudRenderUtilities.ShouldRenderForCamera(renderingData.cameraData.camera))
                return;

            renderer.EnqueuePass(renderPass);
            renderer.EnqueuePass(compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            compositePass?.Dispose();
            CoreUtils.Destroy(compositeMaterial);
            compositeMaterial = null;
        }
    }
}
