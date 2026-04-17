Shader "Hidden/Landscape/VolumetricCloudComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Volumetric Cloud Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_VolumetricCloudTexture);
            SAMPLER(sampler_VolumetricCloudTexture);
            float4 _VolumetricCloudTraceSize;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float3 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float4 cloudTrace = SAMPLE_TEXTURE2D(_VolumetricCloudTexture, sampler_VolumetricCloudTexture, uv);
                float3 finalColor = sceneColor * saturate(cloudTrace.a) + max(cloudTrace.rgb, 0.0);
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
