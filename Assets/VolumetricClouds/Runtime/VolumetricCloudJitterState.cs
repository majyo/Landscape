using UnityEngine;

namespace VolumetricClouds.Runtime
{
    public readonly struct VolumetricCloudJitterState
    {
        public static readonly VolumetricCloudJitterState Legacy = new VolumetricCloudJitterState(0, 0, new Vector2(0.5f, 0.5f));

        public readonly int FrameIndex;
        public readonly int JitterIndex;
        public readonly Vector2 JitterOffset;

        public VolumetricCloudJitterState(int frameIndex, int jitterIndex, Vector2 jitterOffset)
        {
            FrameIndex = frameIndex;
            JitterIndex = jitterIndex;
            JitterOffset = jitterOffset;
        }
    }
}
