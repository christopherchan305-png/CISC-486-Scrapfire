Shader "Scrapfire/Locked Region Boundary"
{
    Properties
    {
        _Tint("Boundary color", Color) = (1, 0.48, 0.08, 0.9)
        _LineWidth("Grid line width", Range(0.005, 0.1)) = 0.025
        _RevealDistance("Reveal distance", Float) = 6
        _FullRevealDistance("Full reveal distance", Float) = 1.2
        _ObserverPosition("Observer position", Vector) = (0, 0, 0, 0)
        _SurfaceFill("Draw area fill", Float) = 0
        _FillOpacity("Area opacity", Range(0, 1)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _ObserverPosition;
                float _RevealDistance;
                float _FullRevealDistance;
                float _LineWidth;
                float _SurfaceFill;
                float _FillOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 grid : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float2 grid : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.grid = input.grid;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float distanceToPlayer = distance(input.positionWS.xz, _ObserverPosition.xz);
                float proximity = 1 - smoothstep(_FullRevealDistance, _RevealDistance, distanceToPlayer);
                float heightFade = 1 - smoothstep(2.5, 4.0, abs(input.positionWS.y - _ObserverPosition.y));
                float2 edgePixels = min(input.uv, 1 - input.uv) / max(fwidth(input.uv), 0.00001);
                float outline = 1 - smoothstep(1.0, 2.5, min(edgePixels.x, edgePixels.y));
                float2 cell = frac(input.grid);
                float2 gridDistance = min(cell, 1 - cell);
                float gridLine = 1 - smoothstep(_LineWidth, _LineWidth + 0.02, min(gridDistance.x, gridDistance.y));
                float opacity = lerp(heightFade * max(outline, 0.04 + gridLine * 0.32), _FillOpacity, _SurfaceFill);
                half alpha = _Tint.a * proximity * opacity;
                return half4(_Tint.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
