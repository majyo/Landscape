using UnityEngine;
using UnityEngine.Rendering;

namespace VolumetricClouds.Runtime
{
    public sealed class VolumetricCloudResources
    {
        private RenderTexture traceTexture;
        private RTHandle traceHandle;
        private int currentResourceHash = int.MinValue;

        public RenderTexture TraceTexture => traceTexture;
        public RTHandle TraceHandle => traceHandle;

        public bool EnsureTraceTarget(in VolumetricCloudParameters parameters)
        {
            if (traceTexture != null
                && traceHandle != null
                && currentResourceHash == parameters.ResourceHash
                && traceTexture.width == parameters.TraceWidth
                && traceTexture.height == parameters.TraceHeight)
            {
                return true;
            }

            Release();

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

            traceTexture = new RenderTexture(descriptor)
            {
                name = "Volumetric Cloud Trace",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            if (!traceTexture.Create())
            {
                Debug.LogError("VolumetricClouds: failed to create trace render texture.");
                Release();
                return false;
            }

            traceHandle = RTHandles.Alloc(traceTexture);
            currentResourceHash = parameters.ResourceHash;
            return traceHandle != null;
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
            if (traceHandle != null)
            {
                traceHandle.Release();
                traceHandle = null;
            }

            if (traceTexture != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(traceTexture);
                else
                    Object.DestroyImmediate(traceTexture);

                traceTexture = null;
            }

            currentResourceHash = int.MinValue;
        }
    }
}
