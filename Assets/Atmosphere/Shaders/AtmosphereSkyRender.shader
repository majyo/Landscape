Shader "Landscape/Atmosphere/Skybox"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
        }

        Pass
        {
            Name "Atmosphere Sky Render"

            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "AtmosphereCommon.hlsl"

            TEXTURE2D(_AtmosphereSkyViewLut);
            SAMPLER(sampler_AtmosphereSkyViewLut);
            TEXTURE2D(_AtmosphereTransmittanceLut);
            SAMPLER(sampler_AtmosphereTransmittanceLut);

            float _AtmosphereGroundRadiusKm;
            float _AtmosphereTopRadiusKm;
            float3 _AtmosphereSunDirection;
            float3 _AtmosphereSunIlluminance;
            float4 _AtmosphereSunDiskParams;
            float _AtmosphereSkyExposure;
            float3 _AtmosphereCameraPositionKm;
            float3 _AtmosphereCameraBasisRight;
            float3 _AtmosphereCameraBasisUp;
            float3 _AtmosphereCameraBasisForward;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.directionWS = TransformObjectToWorldDir(input.positionOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 worldDirection = normalize(input.directionWS);
                float2 skyUv = DirectionToSkyViewUv(
                    worldDirection,
                    normalize(_AtmosphereSunDirection),
                    normalize(_AtmosphereCameraBasisRight),
                    normalize(_AtmosphereCameraBasisUp),
                    normalize(_AtmosphereCameraBasisForward));
                float4 sky = SAMPLE_TEXTURE2D_LOD(_AtmosphereSkyViewLut, sampler_AtmosphereSkyViewLut, skyUv, 0);

                float3 sunDirection = normalize(_AtmosphereSunDirection);
                float cosViewSun = dot(worldDirection, sunDirection);
                float cosSunRadius = _AtmosphereSunDiskParams.x;
                float cosSoftSunRadius = _AtmosphereSunDiskParams.y;
                float sunDiskMask = _AtmosphereSunDiskParams.x > _AtmosphereSunDiskParams.y
                    ? smoothstep(cosSoftSunRadius, cosSunRadius, cosViewSun)
                    : step(cosSunRadius, cosViewSun);

                float angularRadius = max(_AtmosphereSunDiskParams.w, 0.0);
                float solarSolidAngle = max(2.0 * PI * (1.0 - cos(angularRadius)), 1e-6);
                float3 sunRadiance = _AtmosphereSunIlluminance * (_AtmosphereSunDiskParams.z / solarSolidAngle);
                float3 sunTransmittance = SampleTransmittanceLut(
                    _AtmosphereTransmittanceLut,
                    sampler_AtmosphereTransmittanceLut,
                    _AtmosphereCameraPositionKm,
                    worldDirection,
                    _AtmosphereGroundRadiusKm,
                    _AtmosphereTopRadiusKm);
                float earthShadow = GetPlanetShadow(_AtmosphereCameraPositionKm, worldDirection, _AtmosphereGroundRadiusKm);
                float3 sunDisk = sunRadiance * sunTransmittance * earthShadow * sunDiskMask;

                float3 color = (sky.rgb + sunDisk) * _AtmosphereSkyExposure;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
