using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using VolumetricClouds.Runtime;

namespace VolumetricClouds.Rendering
{
    public sealed class VolumetricCloudRenderPass : ScriptableRenderPass
    {
        private const string ProfilingName = "Volumetric Cloud Lighting Trace";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingName);
        private static bool loggedMissingAtmosphereLuts;
        private static bool loggedMissingDepthTexture;

        public VolumetricCloudRenderPass()
        {
            profilingSampler = ProfilingSampler;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!VolumetricCloudRenderUtilities.ShouldRenderForCamera(cameraData.camera))
                return;

            VolumetricCloudController controller = VolumetricCloudController.Instance;
            if (controller == null || !controller.TryPrepare(cameraData.camera, true, out VolumetricCloudParameters parameters, out bool resourcesRecreated))
                return;

            controller.BeginTemporalFrame(cameraData.camera, parameters, resourcesRecreated);

            if (!controller.TryGetRaymarchComputeShader(out ComputeShader computeShader, out int kernelIndex))
                return;

            if (controller.TraceHandle == null
                || AtmosphereController.Instance == null
                || AtmosphereController.Instance.TransmittanceHandle == null
                || AtmosphereController.Instance.SkyViewHandle == null)
            {
                if (!loggedMissingAtmosphereLuts)
                {
                    Debug.LogWarning("VolumetricClouds: skipping cloud trace because Atmosphere transmittance/sky-view LUTs are not ready yet.");
                    loggedMissingAtmosphereLuts = true;
                }
                return;
            }

            loggedMissingAtmosphereLuts = false;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle depthHandle = resourceData.cameraDepthTexture;
            if (!depthHandle.IsValid())
            {
                if (!loggedMissingDepthTexture)
                {
                    Debug.LogWarning("VolumetricClouds: skipping cloud trace because cameraDepthTexture is unavailable for the current camera.");
                    loggedMissingDepthTexture = true;
                }
                return;
            }

            loggedMissingDepthTexture = false;

            TextureHandle traceHandle = renderGraph.ImportTexture(controller.TraceHandle);
            TextureHandle transmittanceHandle = renderGraph.ImportTexture(AtmosphereController.Instance.TransmittanceHandle);
            TextureHandle skyViewHandle = renderGraph.ImportTexture(AtmosphereController.Instance.SkyViewHandle);

            Shader.SetGlobalTexture(VolumetricCloudShaderIDs.CloudBaseShapeNoise, parameters.BaseShapeNoise);
            Shader.SetGlobalTexture(VolumetricCloudShaderIDs.CloudDetailShapeNoise, parameters.DetailShapeNoise);

            using (var builder = renderGraph.AddComputePass<ComputePassData>(ProfilingName, out ComputePassData passData))
            {
                passData.computeShader = computeShader;
                passData.kernelIndex = kernelIndex;
                passData.depthHandle = depthHandle;
                passData.traceHandle = traceHandle;
                passData.transmittanceHandle = transmittanceHandle;
                passData.skyViewHandle = skyViewHandle;
                passData.parameters = parameters;
                TextureDesc depthDescriptor = depthHandle.GetDescriptor(renderGraph);
                passData.screenWidth = Mathf.Max(1, depthDescriptor.width);
                passData.screenHeight = Mathf.Max(1, depthDescriptor.height);

                builder.UseTexture(passData.depthHandle, AccessFlags.Read);
                builder.UseTexture(passData.transmittanceHandle, AccessFlags.Read);
                builder.UseTexture(passData.skyViewHandle, AccessFlags.Read);
                builder.UseTexture(passData.traceHandle, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                {
                    ApplyCloudParameters(context.cmd, data.computeShader, data.kernelIndex, data.depthHandle, data.traceHandle, data.transmittanceHandle, data.skyViewHandle, data.parameters, data.screenWidth, data.screenHeight);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<BindGlobalsPassData>("Volumetric Cloud Bind Globals", out BindGlobalsPassData passData))
            {
                passData.parameters = parameters;
                builder.UseTexture(traceHandle, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(traceHandle, VolumetricCloudShaderIDs.VolumetricCloudTexture);
                builder.SetRenderFunc(static (BindGlobalsPassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(
                        VolumetricCloudShaderIDs.VolumetricCloudTraceSize,
                        new Vector4(
                            data.parameters.TraceWidth,
                            data.parameters.TraceHeight,
                            1.0f / data.parameters.TraceWidth,
                            1.0f / data.parameters.TraceHeight));
                });
            }

        }

        private static void ApplyCloudParameters(
            ComputeCommandBuffer cmd,
            ComputeShader computeShader,
            int kernelIndex,
            TextureHandle depthHandle,
            TextureHandle traceHandle,
            TextureHandle transmittanceHandle,
            TextureHandle skyViewHandle,
            in VolumetricCloudParameters parameters,
            int screenWidth,
            int screenHeight)
        {
            cmd.SetComputeTextureParam(computeShader, kernelIndex, VolumetricCloudShaderIDs.VolumetricCloudTexture, traceHandle);
            cmd.SetComputeTextureParam(computeShader, kernelIndex, VolumetricCloudShaderIDs.CloudSceneDepthTexture, depthHandle);
            cmd.SetComputeTextureParam(computeShader, kernelIndex, AtmosphereShaderIDs.TransmittanceLut, transmittanceHandle);
            cmd.SetComputeTextureParam(computeShader, kernelIndex, AtmosphereShaderIDs.SkyViewLut, skyViewHandle);

            cmd.SetComputeVectorParam(
                computeShader,
                VolumetricCloudShaderIDs.VolumetricCloudTraceSize,
                new Vector4(parameters.TraceWidth, parameters.TraceHeight, 1.0f / parameters.TraceWidth, 1.0f / parameters.TraceHeight));
            cmd.SetComputeVectorParam(computeShader, VolumetricCloudShaderIDs.CloudViewBasisRight, new Vector4(parameters.ViewBasisRight.x, parameters.ViewBasisRight.y, parameters.ViewBasisRight.z, 0.0f));
            cmd.SetComputeVectorParam(computeShader, VolumetricCloudShaderIDs.CloudViewBasisUp, new Vector4(parameters.ViewBasisUp.x, parameters.ViewBasisUp.y, parameters.ViewBasisUp.z, 0.0f));
            cmd.SetComputeVectorParam(computeShader, VolumetricCloudShaderIDs.CloudViewBasisForward, new Vector4(parameters.ViewBasisForward.x, parameters.ViewBasisForward.y, parameters.ViewBasisForward.z, 0.0f));
            cmd.SetComputeVectorParam(
                computeShader,
                VolumetricCloudShaderIDs.CloudScreenSize,
                new Vector4(
                    Mathf.Max(1, screenWidth),
                    Mathf.Max(1, screenHeight),
                    1.0f / Mathf.Max(1, screenWidth),
                    1.0f / Mathf.Max(1, screenHeight)));
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudBottomRadiusKm, parameters.CloudBottomRadiusKm);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudTopRadiusKm, parameters.CloudTopRadiusKm);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudThicknessKm, parameters.CloudThicknessKm);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudCoverage, parameters.CloudCoverage);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudDensityMultiplier, parameters.DensityMultiplier);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudLightAbsorption, parameters.LightAbsorption);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudAmbientStrength, parameters.AmbientStrength);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudPhaseG, parameters.ForwardScatteringG);
            cmd.SetComputeFloatParam(computeShader, VolumetricCloudShaderIDs.CloudMaxRenderDistanceKm, parameters.MaxRenderDistanceKm);
            cmd.SetComputeIntParam(computeShader, VolumetricCloudShaderIDs.CloudStepCount, parameters.StepCount);
            cmd.SetComputeIntParam(computeShader, VolumetricCloudShaderIDs.CloudShadowStepCount, parameters.ShadowStepCount);
            cmd.SetComputeIntParam(computeShader, VolumetricCloudShaderIDs.CloudHasDetailShapeNoise, parameters.DetailShapeNoise != null ? 1 : 0);
            cmd.SetComputeIntParam(computeShader, VolumetricCloudShaderIDs.CloudEnableJitter, parameters.EnableJitter ? 1 : 0);
            cmd.SetComputeVectorParam(
                computeShader,
                VolumetricCloudShaderIDs.CloudShapeScaleData,
                new Vector4(parameters.ShapeBaseScaleKm, parameters.DetailScaleKm, 1.0f / parameters.ShapeBaseScaleKm, 1.0f / parameters.DetailScaleKm));
            cmd.SetComputeVectorParam(
                computeShader,
                VolumetricCloudShaderIDs.CloudWindData,
                new Vector4(parameters.WindDirection.x, parameters.WindDirection.y, parameters.WindOffset.x, parameters.WindOffset.y));
            cmd.SetComputeVectorParam(
                computeShader,
                VolumetricCloudShaderIDs.CloudJitterData,
                new Vector4(parameters.JitterOffset.x, parameters.JitterOffset.y, parameters.JitterStrength, 0.0f));

            cmd.SetComputeFloatParam(computeShader, AtmosphereShaderIDs.GroundRadiusKm, parameters.GroundRadiusKm);
            cmd.SetComputeFloatParam(computeShader, AtmosphereShaderIDs.TopRadiusKm, parameters.TopRadiusKm);
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.SunDirection, new Vector4(parameters.SunDirection.x, parameters.SunDirection.y, parameters.SunDirection.z, 0.0f));
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.SunIlluminance, new Vector4(parameters.SunIlluminance.x, parameters.SunIlluminance.y, parameters.SunIlluminance.z, 0.0f));
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.CameraPositionKm, new Vector4(parameters.CameraPositionKm.x, parameters.CameraPositionKm.y, parameters.CameraPositionKm.z, 0.0f));
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.CameraBasisRight, new Vector4(parameters.CameraBasisRight.x, parameters.CameraBasisRight.y, parameters.CameraBasisRight.z, 0.0f));
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.CameraBasisUp, new Vector4(parameters.CameraBasisUp.x, parameters.CameraBasisUp.y, parameters.CameraBasisUp.z, 0.0f));
            cmd.SetComputeVectorParam(computeShader, AtmosphereShaderIDs.CameraBasisForward, new Vector4(parameters.CameraBasisForward.x, parameters.CameraBasisForward.y, parameters.CameraBasisForward.z, 0.0f));
            cmd.SetComputeFloatParam(computeShader, AtmosphereShaderIDs.CameraTanHalfVerticalFov, parameters.TanHalfVerticalFov);
            cmd.SetComputeFloatParam(computeShader, AtmosphereShaderIDs.CameraAspectRatio, parameters.AspectRatio);
            cmd.SetComputeFloatParam(computeShader, AtmosphereShaderIDs.SkyExposure, Shader.GetGlobalFloat(AtmosphereShaderIDs.SkyExposure));

            cmd.DispatchCompute(
                computeShader,
                kernelIndex,
                Mathf.CeilToInt(parameters.TraceWidth / 8.0f),
                Mathf.CeilToInt(parameters.TraceHeight / 8.0f),
                1);
        }

        private sealed class ComputePassData
        {
            public ComputeShader computeShader;
            public int kernelIndex;
            public TextureHandle depthHandle;
            public TextureHandle traceHandle;
            public TextureHandle transmittanceHandle;
            public TextureHandle skyViewHandle;
            public VolumetricCloudParameters parameters;
            public int screenWidth;
            public int screenHeight;
        }

        private sealed class BindGlobalsPassData
        {
            public VolumetricCloudParameters parameters;
        }
    }
}
