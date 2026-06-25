Shader "Odin XVI/Original Material URP"
{
    Properties
    {
        [MainTexture] _BaseMap("Base", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2)) = 1
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
        _RoughnessMap("Roughness", 2D) = "white" {}
        _RoughnessStrength("Roughness Strength", Range(0, 1)) = 1
        _SmoothnessBias("Smoothness Bias", Range(-1, 1)) = 0
        _Metallic("Metallic", Range(0, 1)) = 0
        _BlenderMaterialStrength("Original Material Strength", Range(0, 1)) = 0
        _MetalnessScale("Original Metalness Scale", Range(0, 1)) = 0.25
        _BaseMetalnessStrength("Base Metalness Strength", Range(0, 30)) = 0
        _Tile0MetalnessStrength("Tile 0 Metalness Strength", Range(0, 30)) = 0
        _Tile1MetalnessStrength("Tile 1 Metalness Strength", Range(0, 30)) = 0
        _Tile2MetalnessStrength("Tile 2 Metalness Strength", Range(0, 30)) = 0
        _Tile3MetalnessStrength("Tile 3 Metalness Strength", Range(0, 30)) = 0
        _BaseDetailAlpha("Base Detail Alpha", Range(0, 1)) = 0.5
        _Tile0DetailAlpha("Tile 0 Detail Alpha", Range(0, 1)) = 0.5
        _Tile1DetailAlpha("Tile 1 Detail Alpha", Range(0, 1)) = 0.5
        _Tile2DetailAlpha("Tile 2 Detail Alpha", Range(0, 1)) = 0.5
        _Tile3DetailAlpha("Tile 3 Detail Alpha", Range(0, 1)) = 0
        _DetailRoughnessStrength("Detail Roughness Strength", Range(-1, 1)) = 0
        _Tile0Mask("Tile 0 Mask", 2D) = "black" {}
        _Tile1Mask("Tile 1 Mask", 2D) = "black" {}
        _Tile2Mask("Tile 2 Mask", 2D) = "black" {}
        _Tile3Mask("Tile 3 Mask", 2D) = "black" {}

        _AlphaMap("Alpha Mask", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission", 2D) = "black" {}
        _EyeIrisMap("Eye Iris Base", 2D) = "black" {}
        [Normal] _EyeIrisNormalMap("Eye Iris Normal", 2D) = "bump" {}
        _EyeDirectionMap("Eye Direction", 2D) = "black" {}
        _EyeDisplacementMap("Eye Displacement", 2D) = "black" {}
        _EyeFakeReflectionMap("Eye Fake Reflection", 2D) = "black" {}
        _EyeBlueEmissionMap("Eye Blue Emission", 2D) = "black" {}
        _EyeYellowEmissionMap("Eye Yellow Emission", 2D) = "black" {}
        _EyeIrisStrength("Eye Iris Strength", Range(0, 1)) = 0
        _EyeIrisNormalStrength("Eye Iris Normal Strength", Range(0, 1)) = 0
        _EyeDisplacementStrength("Eye Displacement Strength", Range(0, 0.05)) = 0
        _EyeReflectionStrength("Eye Reflection Strength", Range(0, 2)) = 0
        _EyeEmissionStrength("Eye Emission Strength", Range(0, 2)) = 0
        _ScleraShellStrength("Sclera Front Shell Strength", Range(0, 1)) = 0
        _ScleraRimPower("Sclera Front Rim Power", Range(0.5, 8)) = 2
        _ScleraRimAlpha("Sclera Front Rim Alpha", Range(0, 0.5)) = 0.08
        _ScleraRimColor("Sclera Front Rim Color", Color) = (0.55, 0.75, 0.9, 1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            // [Forward+ 지원]
            // URP 14의 Forward+는 조명 목록을 GPU 버퍼에서 읽으므로 Shader Model 4.5가 필요하다.
            // 이 셰이더는 플레이어 본체용이므로 Forward+를 사용하는 PC 품질 설정에 맞춘다.
            #pragma target 4.5
            #pragma vertex OdinVertex
            #pragma fragment OdinFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // Forward+ Renderer가 활성화되면 이 변형에서 클러스터링된 추가 조명 목록을 사용한다.
            // 이 키워드가 없으면 Point/Spot Light가 많은 씬에서 캐릭터만 추가 조명을 받지 못한다.
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _BumpScale;
                half _OcclusionStrength;
                half _RoughnessStrength;
                half _SmoothnessBias;
                half _Metallic;
                half _BlenderMaterialStrength;
                half _MetalnessScale;
                half _BaseMetalnessStrength;
                half _Tile0MetalnessStrength;
                half _Tile1MetalnessStrength;
                half _Tile2MetalnessStrength;
                half _Tile3MetalnessStrength;
                half _BaseDetailAlpha;
                half _Tile0DetailAlpha;
                half _Tile1DetailAlpha;
                half _Tile2DetailAlpha;
                half _Tile3DetailAlpha;
                half _DetailRoughnessStrength;
                half _Cutoff;
                half _AlphaClip;
                half _Cull;
                half _ZWrite;
                half _EyeIrisStrength;
                half _EyeIrisNormalStrength;
                half _EyeDisplacementStrength;
                half _EyeReflectionStrength;
                half _EyeEmissionStrength;
                half _ScleraShellStrength;
                half _ScleraRimPower;
                half _ScleraRimAlpha;
                half4 _ScleraRimColor;
            CBUFFER_END

            TEXTURE2D(_OcclusionMap);
            TEXTURE2D(_RoughnessMap);
            TEXTURE2D(_Tile0Mask);
            TEXTURE2D(_Tile1Mask);
            TEXTURE2D(_Tile2Mask);
            TEXTURE2D(_Tile3Mask);
            TEXTURE2D(_AlphaMap);
            TEXTURE2D(_EyeIrisMap);
            TEXTURE2D(_EyeIrisNormalMap);
            TEXTURE2D(_EyeDirectionMap);
            TEXTURE2D(_EyeDisplacementMap);
            TEXTURE2D(_EyeFakeReflectionMap);
            TEXTURE2D(_EyeBlueEmissionMap);
            TEXTURE2D(_EyeYellowEmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half3 vertexSH : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OdinVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInput.positionCS;
                output.positionWS = positionInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positionInput.positionWS);
                output.fogFactor = ComputeFogFactor(positionInput.positionCS.z);
                output.vertexSH = SampleSH(output.normalWS);
                return output;
            }

            half MaskValue(half4 maskSample)
            {
                return saturate(max(maskSample.r, max(maskSample.g, maskSample.b)));
            }

            half BlenderLerpMetalness(half m0, half m1, half m2, half m3)
            {
                half baseMetal = _BaseDetailAlpha * _BaseMetalnessStrength;
                half tile0Metal = _Tile0DetailAlpha * _Tile0MetalnessStrength;
                half tile1Metal = _Tile1DetailAlpha * _Tile1MetalnessStrength;
                half tile2Metal = _Tile2DetailAlpha * _Tile2MetalnessStrength;
                half tile3Metal = _Tile3DetailAlpha * _Tile3MetalnessStrength;

                half metal = lerp(baseMetal, tile0Metal, m0);
                metal = lerp(metal, tile1Metal, m1);
                metal = lerp(metal, tile2Metal, m2);
                metal = lerp(metal, tile3Metal, m3);
                return saturate(metal * _MetalnessScale);
            }

            half4 OdinFragment(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseSample.a * _BaseColor.a;
                half alphaMask = SAMPLE_TEXTURE2D(_AlphaMap, sampler_BaseMap, input.uv).r;
                alpha *= alphaMask;

                #if defined(_ALPHATEST_ON)
                    clip(alpha - _Cutoff);
                #endif

                half m0 = MaskValue(SAMPLE_TEXTURE2D(_Tile0Mask, sampler_BaseMap, input.uv));
                half m1 = MaskValue(SAMPLE_TEXTURE2D(_Tile1Mask, sampler_BaseMap, input.uv));
                half m2 = MaskValue(SAMPLE_TEXTURE2D(_Tile2Mask, sampler_BaseMap, input.uv));
                half m3 = MaskValue(SAMPLE_TEXTURE2D(_Tile3Mask, sampler_BaseMap, input.uv));
                half2 eyeDirection = SAMPLE_TEXTURE2D(_EyeDirectionMap, sampler_BaseMap, input.uv).rg * 2.0h - 1.0h;
                half eyeDisplacement = SAMPLE_TEXTURE2D(_EyeDisplacementMap, sampler_BaseMap, input.uv).r;
                float2 eyeUv = input.uv + eyeDirection * eyeDisplacement * _EyeDisplacementStrength;
                half4 irisSample = SAMPLE_TEXTURE2D(_EyeIrisMap, sampler_BaseMap, eyeUv);
                half irisMask = saturate(max(irisSample.a, max(irisSample.r, max(irisSample.g, irisSample.b))));

                half3 albedo = baseSample.rgb * _BaseColor.rgb;
                albedo = lerp(albedo, irisSample.rgb * _BaseColor.rgb, irisMask * _EyeIrisStrength);

                half4 roughnessSample = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_BaseMap, input.uv);
                half roughness = roughnessSample.r;
                half blenderRoughness = saturate(roughness + abs(roughnessSample.b - 0.5h) * _DetailRoughnessStrength);
                roughness = lerp(0.5h, roughness, _RoughnessStrength);
                roughness = lerp(roughness, blenderRoughness, _BlenderMaterialStrength);
                half smoothness = saturate(1.0h - roughness + _SmoothnessBias);
                half metallic = lerp(_Metallic, BlenderLerpMetalness(m0, m1, m2, m3), _BlenderMaterialStrength);

                half occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, input.uv).g;
                half occlusion = lerp(1.0h, occlusionSample, _OcclusionStrength);

                half3 normalTS = half3(0.0h, 0.0h, 1.0h);
                #if defined(_NORMALMAP)
                    normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BaseMap), _BumpScale);
                    half3 irisNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_EyeIrisNormalMap, sampler_BaseMap, eyeUv), _BumpScale);
                    normalTS = normalize(lerp(normalTS, irisNormalTS, irisMask * _EyeIrisNormalStrength));
                #endif
                half faceSign = IS_FRONT_VFACE(facing, 1.0h, -1.0h);
                half3 normalWS = input.normalWS.xyz * faceSign;
                half3 bitangentWS = input.tangentWS.w * faceSign * cross(normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSHPixel(input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.tangentToWorld = tangentToWorld;

                half scleraShell = saturate(_ScleraShellStrength);
                half frontFacing = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half scleraRim = pow(saturate(1.0h - frontFacing), _ScleraRimPower);
                half3 scleraAlbedo = lerp(_BaseColor.rgb, _ScleraRimColor.rgb, scleraRim * 0.35h);
                albedo = lerp(albedo, scleraAlbedo, scleraShell);
                alpha = lerp(alpha, saturate(_BaseColor.a + scleraRim * _ScleraRimAlpha), scleraShell);
                smoothness = lerp(smoothness, 0.95h, scleraShell);
                metallic = lerp(metallic, 0.0h, scleraShell);
                occlusion = lerp(occlusion, 1.0h, scleraShell);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = normalTS;
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                emission += _EmissionColor.rgb * _EyeEmissionStrength;
                emission += SAMPLE_TEXTURE2D(_EyeYellowEmissionMap, sampler_BaseMap, eyeUv).rgb * _EyeEmissionStrength;
                emission += SAMPLE_TEXTURE2D(_EyeBlueEmissionMap, sampler_BaseMap, eyeUv).rgb * _EyeEmissionStrength;
                emission += SAMPLE_TEXTURE2D(_EyeFakeReflectionMap, sampler_BaseMap, eyeUv).rgb * _EyeReflectionStrength;
                surfaceData.emission = emission;
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = alpha;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = alpha;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
