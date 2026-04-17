using UnityEngine;

namespace VolumetricClouds.Rendering
{
    internal static class VolumetricCloudRenderUtilities
    {
        public static bool ShouldRenderForCamera(Camera camera)
        {
            if (camera == null)
                return false;

            CameraType cameraType = camera.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return false;

            if (Application.isPlaying)
                return cameraType == CameraType.Game;

            return cameraType == CameraType.Game || cameraType == CameraType.SceneView;
        }
    }
}
