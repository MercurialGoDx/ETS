Shader "ETS/Effects/Stylized Ice Prison"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (0.08, 0.52, 0.95, 0.24)
        [HDR] _EdgeColor("Edge Color", Color) = (0.35, 0.9, 1.5, 1)
        [HDR] _FrostColor("Frost Color", Color) = (0.75, 0.96, 1, 1)
        _FresnelPower("Edge Sharpness", Range(0.5, 8)) = 2.7
        _FrostAmount("Frost Amount", Range(0, 1)) = 0.3
        _NoiseScale("Frost Scale", Range(0.2, 20)) = 5
        _ScrollSpeed("Shimmer Speed", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "IceForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _FrostColor;
                half _FresnelPower;
                half _FrostAmount;
                half _NoiseScale;
                half _ScrollSpeed;
            CBUFFER_END

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float SmoothNoise(float3 value)
            {
                float3 cell = floor(value);
                float3 fraction = frac(value);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);

                float x00 = lerp(Hash31(cell + float3(0, 0, 0)), Hash31(cell + float3(1, 0, 0)), fraction.x);
                float x10 = lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), fraction.x);
                float x01 = lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), fraction.x);
                float x11 = lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + float3(1, 1, 1)), fraction.x);
                float y0 = lerp(x00, x10, fraction.y);
                float y1 = lerp(x01, x11, fraction.y);
                return lerp(y0, y1, fraction.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), _FresnelPower);

                float3 noisePosition = input.positionWS * _NoiseScale;
                noisePosition += float3(0.13, 0.31, 0.19) * (_Time.y * _ScrollSpeed);
                half noise = SmoothNoise(noisePosition);
                half frost = smoothstep(1.0h - _FrostAmount, 1.0h, noise);

                half3 iceColor = lerp(_BaseColor.rgb, _FrostColor.rgb, frost * 0.55h);
                iceColor = lerp(iceColor, _EdgeColor.rgb, fresnel * 0.75h);
                iceColor *= input.color.rgb;

                half alpha = saturate(_BaseColor.a + fresnel * 0.42h + frost * 0.12h);
                alpha *= input.color.a;
                return half4(iceColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
