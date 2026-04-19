using UnityEngine;

namespace VolumetricClouds.Runtime
{
    public sealed class VolumetricCloudTemporalState
    {
        public enum HistoryResetReason
        {
            None = 0,
            MissingPreviousFrame = 1,
            ResourceRecreated = 2,
            CameraChanged = 3,
            TraceSizeChanged = 4,
            ParameterChanged = 5,
            CameraPositionJump = 6,
            CameraViewChanged = 7,
            CameraFovChanged = 8,
            CameraAspectChanged = 9
        }

        public readonly struct CameraFrame
        {
            public readonly EntityId CameraInstanceId;
            public readonly Vector3 CameraPositionKm;
            public readonly Vector3 ViewBasisRight;
            public readonly Vector3 ViewBasisUp;
            public readonly Vector3 ViewBasisForward;
            public readonly float TanHalfVerticalFov;
            public readonly float AspectRatio;
            public readonly int TraceWidth;
            public readonly int TraceHeight;
            public readonly int HistoryResetHash;

            public CameraFrame(EntityId cameraInstanceId, in VolumetricCloudParameters parameters)
            {
                CameraInstanceId = cameraInstanceId;
                CameraPositionKm = parameters.CameraPositionKm;
                ViewBasisRight = parameters.ViewBasisRight;
                ViewBasisUp = parameters.ViewBasisUp;
                ViewBasisForward = parameters.ViewBasisForward;
                TanHalfVerticalFov = parameters.TanHalfVerticalFov;
                AspectRatio = parameters.AspectRatio;
                TraceWidth = parameters.TraceWidth;
                TraceHeight = parameters.TraceHeight;
                HistoryResetHash = parameters.HistoryResetHash;
            }
        }

        private CameraFrame currentFrame;
        private CameraFrame previousFrame;
        private bool hasCurrentFrame;
        private bool hasPreviousFrame;
        private HistoryResetReason lastResetReason = HistoryResetReason.MissingPreviousFrame;

        public CameraFrame CurrentFrame => currentFrame;
        public CameraFrame PreviousFrame => previousFrame;
        public bool HasCurrentFrame => hasCurrentFrame;
        public bool HasPreviousFrame => hasPreviousFrame;
        public HistoryResetReason LastResetReason => lastResetReason;

        public HistoryResetReason BeginFrame(EntityId cameraInstanceId, in VolumetricCloudParameters parameters, bool resourcesRecreated)
        {
            currentFrame = new CameraFrame(cameraInstanceId, parameters);
            hasCurrentFrame = true;
            lastResetReason = GetResetReason(currentFrame, parameters, resourcesRecreated);
            return lastResetReason;
        }

        public void CommitFrame()
        {
            if (!hasCurrentFrame)
                return;

            previousFrame = currentFrame;
            hasPreviousFrame = true;
        }

        public void Reset()
        {
            hasCurrentFrame = false;
            hasPreviousFrame = false;
            lastResetReason = HistoryResetReason.MissingPreviousFrame;
        }

        private HistoryResetReason GetResetReason(in CameraFrame frame, in VolumetricCloudParameters parameters, bool resourcesRecreated)
        {
            if (!hasPreviousFrame)
                return HistoryResetReason.MissingPreviousFrame;

            if (resourcesRecreated)
                return HistoryResetReason.ResourceRecreated;

            if (!frame.CameraInstanceId.Equals(previousFrame.CameraInstanceId))
                return HistoryResetReason.CameraChanged;

            if (frame.TraceWidth != previousFrame.TraceWidth || frame.TraceHeight != previousFrame.TraceHeight)
                return HistoryResetReason.TraceSizeChanged;

            if (frame.HistoryResetHash != previousFrame.HistoryResetHash)
                return HistoryResetReason.ParameterChanged;

            float resetDistanceKm = Mathf.Max(0.0f, parameters.TemporalCameraResetDistanceKm);
            if (resetDistanceKm > 0.0f && Vector3.Distance(frame.CameraPositionKm, previousFrame.CameraPositionKm) > resetDistanceKm)
                return HistoryResetReason.CameraPositionJump;

            float resetAngleDegrees = Mathf.Clamp(parameters.TemporalCameraResetAngleDegrees, 0.0f, 180.0f);
            if (resetAngleDegrees < 180.0f)
            {
                float forwardDot = Vector3.Dot(frame.ViewBasisForward.normalized, previousFrame.ViewBasisForward.normalized);
                float minDot = Mathf.Cos(resetAngleDegrees * Mathf.Deg2Rad);
                if (forwardDot < minDot)
                    return HistoryResetReason.CameraViewChanged;
            }

            float currentFovDegrees = TanHalfFovToDegrees(frame.TanHalfVerticalFov);
            float previousFovDegrees = TanHalfFovToDegrees(previousFrame.TanHalfVerticalFov);
            if (Mathf.Abs(currentFovDegrees - previousFovDegrees) > Mathf.Max(0.0f, parameters.TemporalFovResetDegrees))
                return HistoryResetReason.CameraFovChanged;

            if (Mathf.Abs(frame.AspectRatio - previousFrame.AspectRatio) > 0.01f)
                return HistoryResetReason.CameraAspectChanged;

            return HistoryResetReason.None;
        }

        private static float TanHalfFovToDegrees(float tanHalfVerticalFov)
        {
            return Mathf.Atan(Mathf.Max(0.0f, tanHalfVerticalFov)) * 2.0f * Mathf.Rad2Deg;
        }
    }
}
