using UnityEngine;
using UnityEngine.Rendering;

namespace VolumetricClouds.Runtime
{
    public sealed class VolumetricCloudResources
    {
        private RenderTexture traceTexture;
        private RenderTexture stabilizedTexture;
        private RenderTexture historyReadTexture;
        private RenderTexture historyWriteTexture;
        private RenderTexture historyWeightTexture;
        private RTHandle traceHandle;
        private RTHandle stabilizedHandle;
        private RTHandle historyReadHandle;
        private RTHandle historyWriteHandle;
        private RTHandle historyWeightHandle;
        private int currentResourceHash = int.MinValue;
        private bool historyValid;
        private bool useStabilizedForComposite;

        public RenderTexture TraceTexture => traceTexture;
        public RenderTexture StabilizedTexture => stabilizedTexture;
        public RenderTexture HistoryReadTexture => historyReadTexture;
        public RenderTexture HistoryWriteTexture => historyWriteTexture;
        public RenderTexture HistoryWeightTexture => historyWeightTexture;
        public RTHandle TraceHandle => traceHandle;
        public RTHandle StabilizedHandle => stabilizedHandle;
        public RTHandle HistoryReadHandle => historyReadHandle;
        public RTHandle HistoryWriteHandle => historyWriteHandle;
        public RTHandle HistoryWeightHandle => historyWeightHandle;
        public RTHandle CompositeHandle => useStabilizedForComposite && stabilizedHandle != null ? stabilizedHandle : traceHandle;
        public bool HistoryValid => historyValid;

        public bool EnsureTraceTarget(in VolumetricCloudParameters parameters, out bool resourcesRecreated)
        {
            resourcesRecreated = false;
            if (traceTexture != null
                && stabilizedTexture != null
                && historyReadTexture != null
                && historyWriteTexture != null
                && historyWeightTexture != null
                && traceHandle != null
                && stabilizedHandle != null
                && historyReadHandle != null
                && historyWriteHandle != null
                && historyWeightHandle != null
                && currentResourceHash == parameters.ResourceHash
                && traceTexture.width == parameters.TraceWidth
                && traceTexture.height == parameters.TraceHeight
                && stabilizedTexture.width == parameters.TraceWidth
                && stabilizedTexture.height == parameters.TraceHeight
                && historyReadTexture.width == parameters.TraceWidth
                && historyReadTexture.height == parameters.TraceHeight
                && historyWriteTexture.width == parameters.TraceWidth
                && historyWriteTexture.height == parameters.TraceHeight
                && historyWeightTexture.width == parameters.TraceWidth
                && historyWeightTexture.height == parameters.TraceHeight)
            {
                return true;
            }

            Release();
            resourcesRecreated = true;

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(parameters.TraceWidth, parameters.TraceHeight, RenderTextureFormat.ARGBHalf, 0)
            {
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                msaaSamples = 1,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };

            traceTexture = CreateTexture(descriptor, "Volumetric Cloud Trace");
            stabilizedTexture = CreateTexture(descriptor, "Volumetric Cloud Stabilized Trace");
            historyReadTexture = CreateTexture(descriptor, "Volumetric Cloud History Read");
            historyWriteTexture = CreateTexture(descriptor, "Volumetric Cloud History Write");
            historyWeightTexture = CreateTexture(descriptor, "Volumetric Cloud History Weight");

            if (traceTexture == null || stabilizedTexture == null || historyReadTexture == null || historyWriteTexture == null || historyWeightTexture == null)
            {
                Debug.LogError("VolumetricClouds: failed to create cloud render textures.");
                Release();
                return false;
            }

            traceHandle = RTHandles.Alloc(traceTexture);
            stabilizedHandle = RTHandles.Alloc(stabilizedTexture);
            historyReadHandle = RTHandles.Alloc(historyReadTexture);
            historyWriteHandle = RTHandles.Alloc(historyWriteTexture);
            historyWeightHandle = RTHandles.Alloc(historyWeightTexture);
            if (traceHandle == null || stabilizedHandle == null || historyReadHandle == null || historyWriteHandle == null || historyWeightHandle == null)
            {
                Debug.LogError("VolumetricClouds: failed to allocate cloud RTHandles.");
                Release();
                return false;
            }

            currentResourceHash = parameters.ResourceHash;
            historyValid = false;
            useStabilizedForComposite = false;
            return true;
        }

        public void InvalidateHistory()
        {
            historyValid = false;
        }

        public void MarkHistoryValid()
        {
            historyValid = historyReadHandle != null && historyWriteHandle != null;
        }

        public void SwapHistoryBuffers()
        {
            (historyReadTexture, historyWriteTexture) = (historyWriteTexture, historyReadTexture);
            (historyReadHandle, historyWriteHandle) = (historyWriteHandle, historyReadHandle);
        }

        public void UseCurrentTraceForComposite()
        {
            useStabilizedForComposite = false;
        }

        public void UseStabilizedTraceForComposite()
        {
            useStabilizedForComposite = stabilizedHandle != null;
        }

        public void BindGlobals(CommandBuffer cmd, in VolumetricCloudParameters parameters)
        {
            if (cmd == null)
                return;

            if (traceTexture != null)
                cmd.SetGlobalTexture(VolumetricCloudShaderIDs.VolumetricCloudTexture, traceTexture);

            cmd.SetGlobalVector(
                VolumetricCloudShaderIDs.VolumetricCloudTraceSize,
                new Vector4(
                    parameters.TraceWidth,
                    parameters.TraceHeight,
                    1.0f / parameters.TraceWidth,
                    1.0f / parameters.TraceHeight));

            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudBottomRadiusKm, parameters.CloudBottomRadiusKm);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudTopRadiusKm, parameters.CloudTopRadiusKm);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudThicknessKm, parameters.CloudThicknessKm);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudCoverage, parameters.CloudCoverage);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudDensityMultiplier, parameters.DensityMultiplier);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudLightAbsorption, parameters.LightAbsorption);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudAmbientStrength, parameters.AmbientStrength);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudPhaseG, parameters.ForwardScatteringG);
            cmd.SetGlobalFloat(VolumetricCloudShaderIDs.CloudMaxRenderDistanceKm, parameters.MaxRenderDistanceKm);
            cmd.SetGlobalInt(VolumetricCloudShaderIDs.CloudStepCount, parameters.StepCount);
            cmd.SetGlobalInt(VolumetricCloudShaderIDs.CloudShadowStepCount, parameters.ShadowStepCount);
            cmd.SetGlobalVector(
                VolumetricCloudShaderIDs.CloudShapeScaleData,
                new Vector4(
                    parameters.ShapeBaseScaleKm,
                    parameters.DetailScaleKm,
                    1.0f / parameters.ShapeBaseScaleKm,
                    1.0f / parameters.DetailScaleKm));
            cmd.SetGlobalVector(
                VolumetricCloudShaderIDs.CloudWindData,
                new Vector4(
                    parameters.WindDirection.x,
                    parameters.WindDirection.y,
                    parameters.WindOffset.x,
                    parameters.WindOffset.y));

            if (parameters.BaseShapeNoise != null)
                cmd.SetGlobalTexture(VolumetricCloudShaderIDs.CloudBaseShapeNoise, parameters.BaseShapeNoise);

            if (parameters.DetailShapeNoise != null)
                cmd.SetGlobalTexture(VolumetricCloudShaderIDs.CloudDetailShapeNoise, parameters.DetailShapeNoise);

            cmd.SetGlobalInt(VolumetricCloudShaderIDs.CloudHasDetailShapeNoise, parameters.DetailShapeNoise != null ? 1 : 0);
        }

        public void Release()
        {
            ReleaseTexture(ref traceTexture, ref traceHandle);
            ReleaseTexture(ref stabilizedTexture, ref stabilizedHandle);
            ReleaseTexture(ref historyReadTexture, ref historyReadHandle);
            ReleaseTexture(ref historyWriteTexture, ref historyWriteHandle);
            ReleaseTexture(ref historyWeightTexture, ref historyWeightHandle);

            currentResourceHash = int.MinValue;
            historyValid = false;
            useStabilizedForComposite = false;
        }

        private static RenderTexture CreateTexture(RenderTextureDescriptor descriptor, string textureName)
        {
            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
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
