using UnityEngine;

namespace Atmosphere.Runtime
{
    public readonly struct AtmosphereViewParameters
    {
        public readonly Vector4 CameraPositionKm;
        public readonly Vector4 CameraBasisRight;
        public readonly Vector4 CameraBasisUp;
        public readonly Vector4 CameraBasisForward;
        public readonly Vector4 SunDirection;
        public readonly Vector4 SunIlluminance;
        public readonly float TanHalfVerticalFov;
        public readonly float AspectRatio;
        public readonly int DynamicHash;

        public AtmosphereViewParameters(
            Vector3 cameraPositionKm,
            Vector3 cameraBasisRight,
            Vector3 cameraBasisUp,
            Vector3 cameraBasisForward,
            Vector3 sunDirection,
            Vector3 sunIlluminance,
            float tanHalfVerticalFov,
            float aspectRatio)
        {
            CameraPositionKm = cameraPositionKm;
            CameraBasisRight = cameraBasisRight;
            CameraBasisUp = cameraBasisUp;
            CameraBasisForward = cameraBasisForward;
            SunDirection = sunDirection;
            SunIlluminance = sunIlluminance;
            TanHalfVerticalFov = tanHalfVerticalFov;
            AspectRatio = aspectRatio;
            DynamicHash = ComputeDynamicHash(cameraPositionKm, cameraBasisRight, cameraBasisUp, cameraBasisForward, sunDirection, sunIlluminance, tanHalfVerticalFov, aspectRatio);
        }

        private static int ComputeDynamicHash(
            Vector3 cameraPositionKm,
            Vector3 cameraBasisRight,
            Vector3 cameraBasisUp,
            Vector3 cameraBasisForward,
            Vector3 sunDirection,
            Vector3 sunIlluminance,
            float tanHalfVerticalFov,
            float aspectRatio)
        {
            unchecked
            {
                int hash = 17;
                hash = AppendVector(hash, cameraPositionKm, 1000.0f);
                hash = AppendVector(hash, cameraBasisRight, 100000.0f);
                hash = AppendVector(hash, cameraBasisUp, 100000.0f);
                hash = AppendVector(hash, cameraBasisForward, 100000.0f);
                hash = AppendVector(hash, sunDirection, 100000.0f);
                hash = AppendVector(hash, sunIlluminance, 100000.0f);
                hash = (hash * 31) + Mathf.RoundToInt(tanHalfVerticalFov * 100000.0f);
                hash = (hash * 31) + Mathf.RoundToInt(aspectRatio * 100000.0f);
                return hash;
            }
        }

        private static int AppendVector(int hash, Vector3 value, float scale)
        {
            hash = (hash * 31) + Mathf.RoundToInt(value.x * scale);
            hash = (hash * 31) + Mathf.RoundToInt(value.y * scale);
            hash = (hash * 31) + Mathf.RoundToInt(value.z * scale);
            return hash;
        }
    }
}
