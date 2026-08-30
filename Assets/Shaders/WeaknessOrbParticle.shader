Shader "ETS/Effects/Weakness Orb Particle"
{
    Properties
    {
        [HDR] _Color("Core Color", Color) = (0.42, 0.06, 1.8, 0.88)
        [HDR] _EdgeColor("Arc Color", Color) = (1.35, 0.18, 1.8, 1)
        _PulseSpeed("Pulse Speed", Range(0, 8)) = 2.4
        _ArcSpeed("Arc Speed", Range(-8, 8)) = 2.8
        _ArcCount("Arc Count", Range(1, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+45"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WeaknessOrbForward"
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
                half4 _EdgeColor;
                half _PulseSpeed;
                half _ArcSpeed;
                half _ArcCount;
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
                float2 p = (input.uv - 0.5) * 2.0;
                float radius = length(p);
                float angle = atan2(p.y, p.x);

                half pulse = 0.82h + 0.18h * sin(_Time.y * _PulseSpeed);
                half body = 1.0h - smoothstep(0.34h, 0.98h, radius);
                half halo = saturate(1.0h - radius) * 0.42h;

                float arcWave = sin(angle * _ArcCount - _Time.y * _ArcSpeed + radius * 11.0);
                half arcs = pow(saturate(arcWave * 0.5 + 0.5), 7.0h);
                arcs *= smoothstep(0.18h, 0.48h, radius) * (1.0h - smoothstep(0.62h, 0.96h, radius));

                half rim = smoothstep(0.56h, 0.82h, radius) * (1.0h - smoothstep(0.82h, 1.0h, radius));
                half alpha = saturate(body * 0.78h + halo + arcs * 0.72h + rim * 0.45h);
                alpha *= input.color.a * _Color.a;

                half accent = saturate(arcs + rim * 0.7h);
                half3 color = lerp(_Color.rgb, _EdgeColor.rgb, accent);
                color *= pulse * (0.72h + body * 0.55h + arcs * 0.65h);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
