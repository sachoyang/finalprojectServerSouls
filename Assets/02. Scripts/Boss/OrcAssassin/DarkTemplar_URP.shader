Shader "Custom/DarkTemplar_URP"
{
    Properties
    {
        [Normal] _DistortionMap ("일렁임 노말맵 (Distortion)", 2D) = "bump" {}
        _DistortionStrength ("왜곡 강도 (Strength)", Range(0, 0.5)) = 0.05
        _SpeedX ("가로 이동 속도 (Speed X)", Float) = 0.5
        _SpeedY ("세로 이동 속도 (Speed Y)", Float) = 0.5
        _Darken ("어두워지는 정도 (Darken)", Range(0, 1)) = 1.0
    }
    SubShader
    {
        // URP의 Transparent 설정
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "DarkTemplarDistortion"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP 전용 코어 라이브러리 포함
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 screenPos    : TEXCOORD1;
            };

            // 텍스처 선언 (URP 방식)
            TEXTURE2D(_DistortionMap);
            SAMPLER(sampler_DistortionMap);

            // 🔥 핵심: URP 카메라가 미리 찍어둔 뒷배경 텍스처
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            // 변수들은 CBUFFER 안에 넣어야 SRP 배칭(최적화)이 깨지지 않습니다.
            CBUFFER_START(UnityPerMaterial)
                float4 _DistortionMap_ST;
                float _DistortionStrength;
                float _SpeedX;
                float _SpeedY;
                float _Darken;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                // 정점 변환 (Built-in의 UnityObjectToClipPos 역할)
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * _DistortionMap_ST.xy + _DistortionMap_ST.zw;
                
                // 화면(스크린) 좌표 계산
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 1. 시간에 따라 UV를 흘러가게 만들어 일렁임을 줍니다.
                float2 scrollingUV = i.uv + float2(_Time.y * _SpeedX, _Time.y * _SpeedY);

                // 2. 노말맵에서 왜곡(Distortion) 방향 값을 뽑아옵니다.
                half4 distortionNormal = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, scrollingUV);
                float2 distortion = UnpackNormal(distortionNormal).xy * _DistortionStrength;

                // 3. 캡처된 뒷배경의 화면 좌표(0~1)를 구하고 왜곡 값을 더해 이미지를 구깁니다.
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                screenUV += distortion;

                // 4. URP 배경 텍스처에서 구겨진 좌표의 색상을 가져옵니다.
                half4 col = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);

                // 5. 살짝 어둡게(다크 템플러 느낌) 만듭니다.
                col.rgb *= _Darken;
                
                return col;
            }
            ENDHLSL
        }
    }
}