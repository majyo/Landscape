using Atmosphere.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using VolumetricClouds.Runtime;

namespace VolumetricClouds.Rendering
{
    public sealed class VolumetricCloudTemporalAccumulationPass : ScriptableRenderPass
    {
        private const string ProfilingName = "Volumetric Cloud Temporal Accumulation";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingName);

        public VolumetricCloudTemporalAccumulationPass()
        {
            profilingSampler = ProfilingSampler;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!VolumetricCloudRenderUtilities.ShouldRenderForCamera(cameraData.camera))
                return;

            VolumetricCloudController controller = VolumetricCloudController.Instance;
            if (controller == null
                || !controller.TryPrepare(cameraData.camera, false, out VolumetricCloudParameters parameters, out _)
                || !parameters.EnableTemporalAccumulation)
            {
                controller?.SkipTemporalAccumulation();
                return;
            }

            if (!controller.TryGetTemporalAccumulationComputeShader(out ComputeShader computeShader, out int kernelIndex))
            {
                controller.SkipTemporalAccumulation();
                return;
            }

            VolumetricCloudResources resources = controller.Resources;
            if (resources == null
                || controller.TraceHandle == null
                || controller.StabilizedHandle == null
                || controller.HistoryReadHandle == null
                || controller.HistoryWriteHandle == null
                || resources.HistoryWeightHandle == null)
            {
                controller.SkipTemporalAccumulation();
                return;
            }

            TextureHandle currentTrace = renderGraph.ImportTexture(controller.TraceHandle);
            TextureHandle historyRead = renderGraph.ImportTexture(controller.HistoryReadHandle);
            TextureHandle historyWrite = renderGraph.ImportTexture(controller.HistoryWriteHandle);
            TextureHandle stabilizedTrace = renderGraph.ImportTexture(controller.StabilizedHandle);
            TextureHandle historyWeight = renderGraph.ImportTexture(resources.HistoryWeightHandle);
            resources.UseStabilizedTraceForComposite();

            using (var builder = renderGraph.AddComputePass<ComputePassData>(ProfilingName, out ComputePassData passData))
            {
                passData.controller = controller;
                passData.computeShader = computeShader;
                passData.kernelIndex = kernelIndex;
                passData.currentTrace = currentTrace;
                passData.historyRead = historyRead;
                passData.historyWrite = historyWrite;
                passData.stabilizedTrace = stabilizedTrace;
                passData.historyWeight = historyWeight;
                passData.parameters = parameters;
                passData.historyValid = resources.HistoryValid && controller.TemporalState.HasPreviousFrame;
                passData.previousFrame = controller.TemporalState.HasPreviousFrame
                    ? controller.TemporalState.PreviousFrame
                    : controller.TemporalState.CurrentFrame;

                builder.UseTexture(passData.currentTrace, AccessFlags.Read);
                builder.UseTexture(passData.historyRead, AccessFlags.Read);
                builder.UseTexture(passData.historyWrite, AccessFlags.WriteAll);
                builder.UseTexture(passData.stabilizedTrace, AccessFlags.WriteAll);
                builder.UseTexture(passData.historyWeight, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (ComputePassData data, ComputeGraphContext context) =>
                {
                    ApplyTemporalParameters(context.cmd, data);
                    data.controller.CompleteTemporalAccumulation();
                });
            }
        }

        private static void ApplyTemporalParameters(ComputeCommandBuffer cmd, ComputePassData data)
        {
            VolumetricCloudParameters parameters = data.parameters;
            VolumetricCloudTemporalState.CameraFrame previousFrame = data.previousFrame;

            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, VolumetricCloudShaderIDs.VolumetricCloudCurrentTexture, data.currentTrace);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, VolumetricCloudShaderIDs.VolumetricCloudHistoryTexture, data.historyRead);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, VolumetricCloudShaderIDs.VolumetricCloudHistoryOutputTexture, data.historyWrite);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, VolumetricCloudShaderIDs.VolumetricCloudStabilizedTexture, data.stabilizedTrace);
            cmd.SetComputeTextureParam(data.computeShader, data.kernelIndex, VolumetricCloudShaderIDs.VolumetricCloudHistoryWeightTexture, data.historyWeight);
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.VolumetricCloudTraceSize,
                new Vector4(parameters.TraceWidth, parameters.TraceHeight, 1.0f / parameters.TraceWidth, 1.0f / parameters.TraceHeight));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudViewBasisRight, new Vector4(parameters.ViewBasisRight.x, parameters.ViewBasisRight.y, parameters.ViewBasisRight.z, 0.0f));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudViewBasisUp, new Vector4(parameters.ViewBasisUp.x, parameters.ViewBasisUp.y, parameters.ViewBasisUp.z, 0.0f));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudViewBasisForward, new Vector4(parameters.ViewBasisForward.x, parameters.ViewBasisForward.y, parameters.ViewBasisForward.z, 0.0f));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudPreviousCameraPositionKm, new Vector4(previousFrame.CameraPositionKm.x, previousFrame.CameraPositionKm.y, previousFrame.CameraPositionKm.z, 0.0f));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudPreviousViewBasisRight, new Vector4(previousFrame.ViewBasisRight.x, previousFrame.ViewBasisRight.y, previousFrame.ViewBasisRight.z, 0.0f));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudPreviousViewBasisUp, new Vector4(previousFrame.ViewBasisUp.x, previousFrame.ViewBasisUp.y, previousFrame.ViewBasisUp.z, 0.0f));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudPreviousViewBasisForward, new Vector4(previousFrame.ViewBasisForward.x, previousFrame.ViewBasisForward.y, previousFrame.ViewBasisForward.z, 0.0f));
            cmd.SetComputeVectorParam(data.computeShader, VolumetricCloudShaderIDs.CloudPreviousCameraData, new Vector4(previousFrame.TanHalfVerticalFov, previousFrame.AspectRatio, previousFrame.TraceWidth, previousFrame.TraceHeight));
            cmd.SetComputeVectorParam(
                data.computeShader,
                VolumetricCloudShaderIDs.CloudTemporalData,
                new Vector4(
                    parameters.TemporalResponse,
                    data.historyValid ? 1.0f : 0.0f,
                    parameters.MaxRenderDistanceKm * 0.5f,
                    parameters.TemporalTransmittanceRejectThreshold));
            cmd.SetComputeVectorParam(data.computeShader, AtmosphereShaderIDs.CameraPositionKm, new Vector4(parameters.CameraPositionKm.x, parameters.CameraPositionKm.y, parameters.CameraPositionKm.z, 0.0f));
            cmd.SetComputeFloatParam(data.computeShader, AtmosphereShaderIDs.CameraTanHalfVerticalFov, parameters.TanHalfVerticalFov);
            cmd.SetComputeFloatParam(data.computeShader, AtmosphereShaderIDs.CameraAspectRatio, parameters.AspectRatio);

            cmd.DispatchCompute(
                data.computeShader,
                data.kernelIndex,
                Mathf.CeilToInt(parameters.TraceWidth / 8.0f),
                Mathf.CeilToInt(parameters.TraceHeight / 8.0f),
                1);
        }

        private sealed class ComputePassData
        {
            public VolumetricCloudController controller;
            public ComputeShader computeShader;
            public int kernelIndex;
            public TextureHandle currentTrace;
            public TextureHandle historyRead;
            public TextureHandle historyWrite;
            public TextureHandle stabilizedTrace;
            public TextureHandle historyWeight;
            public VolumetricCloudParameters parameters;
            public bool historyValid;
            public VolumetricCloudTemporalState.CameraFrame previousFrame;
        }
    }
}
