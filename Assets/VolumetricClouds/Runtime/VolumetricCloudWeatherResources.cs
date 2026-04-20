using UnityEngine;
using UnityEngine.Rendering;

namespace VolumetricClouds.Runtime
{
    internal sealed class VolumetricCloudWeatherResources
    {
        private RenderTexture weatherFieldTexture;
        private RenderTexture weatherFieldScratchTexture;
        private RTHandle weatherFieldHandle;
        private RTHandle weatherFieldScratchHandle;
        private int currentResolution = -1;
        private bool initialized;

        public RenderTexture WeatherFieldTexture => weatherFieldTexture;
        public RenderTexture WeatherFieldScratchTexture => weatherFieldScratchTexture;
        public RTHandle WeatherFieldHandle => weatherFieldHandle;
        public RTHandle WeatherFieldScratchHandle => weatherFieldScratchHandle;
        public bool Initialized => initialized;

        public bool EnsureTextures(int resolution, out bool resourcesRecreated)
        {
            resourcesRecreated = false;
            int clampedResolution = Mathf.Max(1, resolution);
            if (weatherFieldTexture != null
                && weatherFieldScratchTexture != null
                && weatherFieldHandle != null
                && weatherFieldScratchHandle != null
                && currentResolution == clampedResolution
                && weatherFieldTexture.width == clampedResolution
                && weatherFieldTexture.height == clampedResolution
                && weatherFieldScratchTexture.width == clampedResolution
                && weatherFieldScratchTexture.height == clampedResolution)
            {
                return true;
            }

            Release();
            resourcesRecreated = true;

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(clampedResolution, clampedResolution, RenderTextureFormat.ARGBHalf, 0)
            {
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                msaaSamples = 1,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };

            weatherFieldTexture = CreateTexture(descriptor, "Volumetric Cloud Weather Field");
            weatherFieldScratchTexture = CreateTexture(descriptor, "Volumetric Cloud Weather Field Scratch");
            if (weatherFieldTexture == null || weatherFieldScratchTexture == null)
            {
                Debug.LogError("VolumetricClouds: failed to create weather field textures.");
                Release();
                return false;
            }

            weatherFieldHandle = RTHandles.Alloc(weatherFieldTexture);
            weatherFieldScratchHandle = RTHandles.Alloc(weatherFieldScratchTexture);
            if (weatherFieldHandle == null || weatherFieldScratchHandle == null)
            {
                Debug.LogError("VolumetricClouds: failed to allocate weather field RTHandles.");
                Release();
                return false;
            }

            currentResolution = clampedResolution;
            initialized = false;
            return true;
        }

        public void SwapWeatherFieldBuffers()
        {
            (weatherFieldTexture, weatherFieldScratchTexture) = (weatherFieldScratchTexture, weatherFieldTexture);
            (weatherFieldHandle, weatherFieldScratchHandle) = (weatherFieldScratchHandle, weatherFieldHandle);
        }

        public void MarkInitialized()
        {
            initialized = weatherFieldHandle != null && weatherFieldScratchHandle != null;
        }

        public void Invalidate()
        {
            initialized = false;
        }

        public void Release()
        {
            ReleaseTexture(ref weatherFieldTexture, ref weatherFieldHandle);
            ReleaseTexture(ref weatherFieldScratchTexture, ref weatherFieldScratchHandle);
            currentResolution = -1;
            initialized = false;
        }

        private static RenderTexture CreateTexture(RenderTextureDescriptor descriptor, string textureName)
        {
            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };

            return texture.Create() ? texture : null;
        }

        private static void ReleaseTexture(ref RenderTexture texture, ref RTHandle handle)
        {
            if (handle != null)
            {
                handle.Release();
                handle = null;
            }

            if (texture == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);

            texture = null;
        }
    }
}
