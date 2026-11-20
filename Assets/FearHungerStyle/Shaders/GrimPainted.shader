// Fear & Hunger–style "grim painted" character/environment shader for URP (Unity 2022.3 / Unity 6)
// - Banded (2-4 step) lighting with tinted shadows
// - Brush-stroke noise that breaks up the light/shadow edge so it looks hand painted
// - Noisy rim light
// - Inverted-hull ink outline with optional "boiling" jitter
Shader "Custom/GrimPainted"
{
    Properties
    {
        _BaseMap ("Albedo (hand-painted)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)

        [Header(Lighting)]
        _ShadowColor ("Shadow Color", Color) = (0.10,0.06,0.14,1)
        _Bands ("Light Bands", Range(2,6)) = 3
        _Ambient ("Ambient Lift", Range(0,1)) = 0.08

        [Header(Brush)]
        _BrushTex ("Brush Noise (grayscale, Repeat)", 2D) = "gray" {}
        _BrushScale ("Brush Scale", Float) = 2
        _BrushStrength ("Brush Breakup", Range(0,1)) = 0.4

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (0.95,0.9,0.85,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,2)) = 0.5

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.02,0.01,0.02,1)
        _OutlineWidth ("Outline Width (world)", Range(0,0.05)) = 0.008
        _OutlineJitter ("Outline Jitter", Range(0,1)) = 0.4
        _OutlineFPS ("Outline Boil FPS", Range(0,24)) = 8
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _ShadowColor;
            half   _Bands;
            half   _Ambient;
            float4 _BrushTex_ST;
            float  _BrushScale;
            half   _BrushStrength;
            half4  _RimColor;
            half   _RimPower;
            half   _RimStrength;
            half4  _OutlineColor;
            float  _OutlineWidth;
            half   _OutlineJitter;
            half   _OutlineFPS;
        CBUFFER_END

        TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BrushTex); SAMPLER(sampler_BrushTex);

        float Hash13(float3 p)
        {
            return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453);
        }
        ENDHLSL

        // ---------------- Main painted pass ----------------
        Pass
        {
            Name "GrimForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fog        : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.uv         = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog        = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Triplanar brush noise so strokes don't depend on UVs
            half SampleBrush(float3 posWS, float3 nWS)
            {
                float3 w = pow(abs(nWS), 4);
                w /= (w.x + w.y + w.z + 1e-5);
                float3 p = posWS * _BrushScale;
                half x = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, p.zy).r;
                half y = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, p.xz).r;
                half z = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, p.xy).r;
                return x * w.x + y * w.y + z * w.z;
            }

            half Band(half v)
            {
                return saturate(floor(saturate(v) * _Bands) / max(_Bands - 1, 1));
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half brush = SampleBrush(i.positionWS, N) - 0.5;   // -0.5..0.5

                // ---- main light: half-lambert -> brush-perturbed -> banded
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = dot(N, ml.direction) * 0.5 + 0.5;
                half lit = ndl * ml.shadowAttenuation;
                lit += brush * _BrushStrength;
                half band = Band(lit);

                half3 shadowCol = albedo * _ShadowColor.rgb + albedo * _Ambient;
                half3 col = lerp(shadowCol, albedo * ml.color, band);

                // ---- additional lights (torches, lanterns): hard banded pools of light
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1));
                    half a = saturate(dot(N, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    a += brush * _BrushStrength * saturate(a * 4);
                    col += albedo * l.color * Band(a);
                LIGHT_LOOP_END
                #endif

                // ---- rim light, also broken up by the brush
                half rim = pow(1 - saturate(dot(N, V)), _RimPower);
                rim = smoothstep(0.35, 0.6, rim + brush * 0.35);
                col += _RimColor.rgb * rim * _RimStrength * saturate(band + 0.25);

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------------- Ink outline (inverted hull) ----------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; half fog : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(v.normalOS);
                float  t     = floor(_Time.y * _OutlineFPS);           // stepped time = hand-drawn "boil"
                float  j     = Hash13(floor(posWS * 40) + t);
                float  w     = _OutlineWidth * lerp(1, 0.3 + j * 1.4, _OutlineJitter);
                posWS += nWS * w;
                o.positionCS = TransformWorldToHClip(posWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fog), 1);
            }
            ENDHLSL
        }

        // Shadows + depth (needed for shadows, SSAO and the depth texture)
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
