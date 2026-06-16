Shader "Custom/DarkTemplar_BuiltIn"
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
        // 화면을 캡처해야 하므로 불투명한 물체가 다 그려진 뒤(Transparent)에 렌더링합니다.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        // 🔥 핵심: 현재 카메라에 보이는 뒷배경을 캡처해서 _BackgroundTexture에 저장합니다.
        GrabPass { "_BackgroundTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 grabPos : TEXCOORD1;
            };

            sampler2D _BackgroundTexture;
            sampler2D _DistortionMap;
            float4 _DistortionMap_ST;
            float _DistortionStrength;
            float _SpeedX;
            float _SpeedY;
            float _Darken;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _DistortionMap);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 시간에 따라 UV를 흘러가게 만들어 일렁임을 줍니다.
                float2 scrollingUV = i.uv + float2(_Time.y * _SpeedX, _Time.y * _SpeedY);

                // 2. 노말맵에서 왜곡(Distortion) 방향 값을 뽑아옵니다.
                half4 distortionNormal = tex2D(_DistortionMap, scrollingUV);
                float2 distortion = UnpackNormal(distortionNormal).xy * _DistortionStrength;

                // 3. 캡처된 뒷배경의 화면 좌표에 왜곡 값을 더해서 이미지를 구깁니다.
                float2 finalGrabUV = i.grabPos.xy / i.grabPos.w;
                finalGrabUV += distortion;

                // 4. 구겨진 좌표의 배경 색상을 가져옵니다.
                fixed4 col = tex2D(_BackgroundTexture, finalGrabUV);

                // 5. 너무 투명하기만 하면 안 보이므로, 살짝 어둡게(다크 템플러 느낌) 만듭니다.
                col.rgb *= _Darken;
                
                return col;
            }
            ENDCG
        }
    }
}