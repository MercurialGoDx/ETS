Shader "ETS/Effects/Holy Chain Lightning"
{
    Properties
    {
        [HDR] _Color("Tint", Color) = (4, 2.5, 0.1, 1)
        _FlowSpeed("Flow Speed", Range(-20, 20)) = 7
        _PulseDensity("Pulse Density", Range(1, 30)) = 12
        _EdgeSoftness("Edge Softness", Range(0.01, 0.5)) = 0.24
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HolyChainForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FlowSpeed;
                half _PulseDensity;
                half _EdgeSoftness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half distanceFromCore = abs(input.uv.y - 0.5h) * 2.0h;
                half softEdge = 1.0h - smoothstep(1.0h - _EdgeSoftness, 1.0h, distanceFromCore);
                half hotCore = 1.0h - smoothstep(0.0h, 0.34h, distanceFromCore);
                half flow = sin(input.uv.x * _PulseDensity * 6.28318h - _Time.y * _FlowSpeed);
                flow = flow * 0.5h + 0.5h;
                half intensity = 0.68h + flow * 0.32h + hotCore * 0.28h;
                half alpha = _Color.a * input.color.a * softEdge;
                return half4(_Color.rgb * input.color.rgb * intensity, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
