Shader "Hidden/Landscape/AtmosphereDebugSlice"
{
    Properties {}

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Atmosphere Debug Slice"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE3D(_MainTex3D);
            SAMPLER(sampler_MainTex3D);
            float _AtmosphereAerialDebugSlice;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 uvw = float3(input.texcoord.xy, saturate(_AtmosphereAerialDebugSlice));
                return float4(SAMPLE_TEXTURE3D(_MainTex3D, sampler_MainTex3D, uvw).rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
