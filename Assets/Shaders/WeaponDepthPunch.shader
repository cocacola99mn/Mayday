// Clears the depth buffer behind the first-person weapon so walls can never cut into it.
// Used by the WeaponDepthPunch renderer feature, which runs just before the weapon is drawn.
Shader "Hidden/Tower Crawler/Weapon Depth Punch"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        Pass
        {
            Name "DepthPunch"
            ZTest Always
            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Push every vertex to the far plane, keeping its place on screen.
                #if UNITY_REVERSED_Z
                    output.positionCS.z = output.positionCS.w * 0.000001;
                #else
                    output.positionCS.z = output.positionCS.w * 0.999999;
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
