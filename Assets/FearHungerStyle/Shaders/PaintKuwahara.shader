// Full-screen oil-paint (Kuwahara) filter for URP's "Full Screen Pass Renderer Feature".
// Turns the whole rendered frame into flat brush-like patches with hard edges —
// this is what makes 3D read like a painted portrait.
// Setup: Renderer asset -> Add Renderer Feature -> Full Screen Pass
//   Pass Material = material using this shader, Injection Point = Before Rendering Post Processing,
//   Requirements = Color
Shader "Hidden/Custom/PaintKuwahara"
{
    Properties
    {
        _Radius ("Radius (samples)", Range(1,6)) = 3
        _Spacing ("Sample Spacing (px)", Range(1,4)) = 1.5
        _CanvasTex ("Canvas / Paper Texture", 2D) = "white" {}
        _CanvasStrength ("Canvas Strength", Range(0,1)) = 0.25
        _CanvasScale ("Canvas Scale", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "Kuwahara"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Radius;
            float _Spacing;
            TEXTURE2D(_CanvasTex); SAMPLER(sampler_CanvasTex);
            half _CanvasStrength;
            float _CanvasScale;

            void Quadrant(float2 uv, float2 texel, int2 xr, int2 yr, out float3 mean, out float variance)
            {
                float3 sum = 0, sq = 0; float n = 0;
                [loop] for (int x = xr.x; x <= xr.y; x++)
                [loop] for (int y = yr.x; y <= yr.y; y++)
                {
                    float3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(x, y) * texel).rgb;
                    sum += c; sq += c * c; n += 1;
                }
                mean = sum / n;
                float3 v = abs(sq / n - mean * mean);
                variance = v.r + v.g + v.b;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _Spacing / _ScreenParams.xy;
                int r = (int)_Radius;

                float3 m0, m1, m2, m3; float v0, v1, v2, v3;
                Quadrant(uv, texel, int2(-r, 0), int2(-r, 0), m0, v0);
                Quadrant(uv, texel, int2(0, r),  int2(-r, 0), m1, v1);
                Quadrant(uv, texel, int2(-r, 0), int2(0, r),  m2, v2);
                Quadrant(uv, texel, int2(0, r),  int2(0, r),  m3, v3);

                float3 col = m0; float best = v0;
                if (v1 < best) { best = v1; col = m1; }
                if (v2 < best) { best = v2; col = m2; }
                if (v3 < best) { best = v3; col = m3; }

                // canvas grain (multiply, centred on 1)
                float aspect = _ScreenParams.x / _ScreenParams.y;
                half canvas = SAMPLE_TEXTURE2D(_CanvasTex, sampler_CanvasTex, uv * float2(aspect, 1) * _CanvasScale).r;
                col *= lerp(1, canvas * 2, _CanvasStrength * 0.5);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
