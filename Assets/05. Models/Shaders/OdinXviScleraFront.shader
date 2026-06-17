Shader "Odin XVI/Sclera Front URP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.18, 0.22, 0.24, 0.12)
        _FrontFade("Front Fade", Range(0, 1)) = 0.55
        _RimPower("Rim Power", Range(0.5, 8)) = 2.2
        _RimAlpha("Rim Alpha", Range(0, 0.5)) = 0.08
        _RimColor("Rim Color", Color) = (0.55, 0.75, 0.9, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ScleraVertex
            #pragma fragment ScleraFragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _FrontFade;
                half _RimPower;
                half _RimAlpha;
                half4 _RimColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ScleraVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInput.positionCS;
                output.positionWS = positionInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.fogFactor = ComputeFogFactor(positionInput.positionCS.z);
                return output;
            }

            half4 ScleraFragment(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half faceSign = IS_FRONT_VFACE(facing, 1.0h, -1.0h);
                half3 normalWS = normalize(input.normalWS * faceSign);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half front = saturate(dot(normalWS, viewWS));
                half rim = pow(saturate(1.0h - front), _RimPower);

                half alpha = saturate(_BaseColor.a * lerp(_FrontFade, 1.0h, front) + rim * _RimAlpha);
                half3 color = lerp(_BaseColor.rgb, _RimColor.rgb, rim * 0.35h);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
