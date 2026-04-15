Shader "Hidden/Landscape/AtmosphereAerialComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Atmosphere Aerial Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "AtmosphereCommon.hlsl"

            TEXTURE3D(_AtmosphereAerialScatteringLut);
            SAMPLER(sampler_AtmosphereAerialScatteringLut);
            TEXTURE3D(_AtmosphereAerialTransmittanceLut);
            SAMPLER(sampler_AtmosphereAerialTransmittanceLut);
            TEXTURE2D(_AtmosphereSkyViewLut);
            SAMPLER(sampler_AtmosphereSkyViewLut);

            float4 _AtmosphereAerialPerspectiveSize;
            float _AtmosphereAerialPerspectiveMaxDistanceKm;
            float3 _AtmosphereSunDirection;
            float3 _AtmosphereCameraBasisRight;
            float3 _AtmosphereCameraBasisUp;
            float3 _AtmosphereCameraBasisForward;
            float _AtmosphereCameraTanHalfVerticalFov;
            float _AtmosphereCameraAspectRatio;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float3 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float rawDepth = SampleSceneDepth(uv);

            #if UNITY_REVERSED_Z
                if (rawDepth <= 0.0)
                    return float4(sourceColor, 1.0);
            #else
                if (rawDepth >= 1.0)
                    return float4(sourceColor, 1.0);
            #endif

                float linearEyeDepthMeters = LinearEyeDepth(rawDepth, _ZBufferParams);
                float distanceKm = max(0.0, linearEyeDepthMeters * 0.001);
                float normalizedZ = DistanceToSliceNormalized(distanceKm, _AtmosphereAerialPerspectiveMaxDistanceKm);
                float3 uvw = float3(uv, normalizedZ);

                float3 aerialScattering = SAMPLE_TEXTURE3D(_AtmosphereAerialScatteringLut, sampler_AtmosphereAerialScatteringLut, uvw).rgb;
                float3 aerialTransmittance = SAMPLE_TEXTURE3D(_AtmosphereAerialTransmittanceLut, sampler_AtmosphereAerialTransmittanceLut, uvw).rgb;

                float3 rayDirection = GetCameraRayDirection(
                    uv,
                    _AtmosphereCameraTanHalfVerticalFov,
                    _AtmosphereCameraAspectRatio,
                    normalize(_AtmosphereCameraBasisRight),
                    normalize(_AtmosphereCameraBasisUp),
                    normalize(_AtmosphereCameraBasisForward));
                float2 skyUv = DirectionToSkyViewUv(
                    rayDirection,
                    normalize(_AtmosphereSunDirection),
                    normalize(_AtmosphereCameraBasisRight),
                    normalize(_AtmosphereCameraBasisUp),
                    normalize(_AtmosphereCameraBasisForward));
                float3 skyColor = SAMPLE_TEXTURE2D(_AtmosphereSkyViewLut, sampler_AtmosphereSkyViewLut, skyUv).rgb;

                float farBlend = saturate((distanceKm - _AtmosphereAerialPerspectiveMaxDistanceKm * 0.85) / max(_AtmosphereAerialPerspectiveMaxDistanceKm * 0.15, 1e-4));
                aerialScattering = lerp(aerialScattering, skyColor, farBlend);
                aerialTransmittance = lerp(aerialTransmittance, 0.0.xxx, farBlend);

                float3 finalColor = sourceColor * aerialTransmittance + aerialScattering;
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
