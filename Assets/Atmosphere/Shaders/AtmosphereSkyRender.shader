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

            float3 _AtmosphereSunDirection;
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
                return half4(sky.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
