Shader "Custom/VertexColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AtmosphereColor ("Atmosphere Color", Color) = (0.6, 0.8, 1.0, 1.0)
        _AtmosphereDensity ("Atmosphere Density", Range(0, 1)) = 0.3
        _AtmosphereHeight ("Atmosphere Height", Float) = 50
        _AtmosphereStartHeight ("Atmosphere Start Height", Float) = 20
        _AtmosphereEndHeight ("Atmosphere End Height", Float) = 100
        _AtmosphereMaxOrthoSize ("Atmosphere Max Ortho Size", Float) = 100
        _AtmosphereMinOrthoSize ("Atmosphere Min Ortho Size", Float) = 20
        _OrthoSize ("Ortho Size", Float) = 10
        _OrthoFalloffStart ("Ortho Falloff Start", Float) = 20
        _OrthoFalloffEnd ("Ortho Falloff End", Float) = 100
        [Header(Cloud Settings)]
        _CloudScale ("Cloud Scale", Range(1, 100)) = 50
        _CloudSpeed ("Cloud Speed", Range(0, 1)) = 0.1
        _CloudDensity ("Cloud Density", Range(0, 1)) = 0.5
        _CloudHeight ("Cloud Height", Range(10, 1000)) = 100
        _CloudColor ("Cloud Color", Color) = (1,1,1,0.6)
        _CloudShadowTexture ("Cloud Shadow Texture", 2D) = "white" {}
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.3
        _CloudMaxOrthoSize ("Cloud Max Ortho Size", Float) = 100
        _CloudMinOrthoSize ("Cloud Min Ortho Size", Float) = 20
        _ShadowOffset ("Shadow Offset", Vector) = (0, 0, 0, 0)
        [Header(Shadow Settings)]
        _ShadowSoftness ("Shadow Softness", Range(0, 1)) = 0.3
        _OvercastFactor ("Overcast Factor", Range(0, 1)) = 0
        _OrthoFalloffMultiplier ("Ortho Falloff Multiplier", Range(0.001, 10)) = 1.0
        _WorldSeed ("World Seed", Float) = 0
        _ColorVariationStrength ("Color Variation Strength", Range(0, 0.2)) = 0.05
        _ColorVariationScale ("Color Variation Scale", Range(1, 100)) = 25
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue"="Geometry"
        }

        // Main pass for the visible mesh
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalWS : NORMAL;
                float4 shadowCoord : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                float viewDist : TEXCOORD4;
                float4 positionNDC : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _AtmosphereColor;
            float _AtmosphereDensity;
            float _AtmosphereHeight;
            float _AtmosphereStartHeight;
            float _AtmosphereEndHeight;
            float _AtmosphereMaxOrthoSize;
            float _AtmosphereMinOrthoSize;
            float _OrthoSize;
            float _OrthoFalloffStart;
            float _OrthoFalloffEnd;

            float _CloudScale;
            float _CloudSpeed;
            float _CloudDensity;
            float _CloudHeight;
            float4 _CloudColor;
            float _ShadowStrength;
            float _CloudMaxOrthoSize;
            float _CloudMinOrthoSize;

            float2 _ShadowOffset;

            float _ShadowSoftness;
            float _OvercastFactor;

            float _OrthoFalloffMultiplier;

            float _WorldSeed;
            float _ColorVariationStrength;
            float _ColorVariationScale;

            // Noise functions
            float2 hash2(float2 p, float seed)
            {
                p = float2(dot(p,float2(127.1 + seed, 311.7)), dot(p,float2(269.5, 183.3 + seed)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float noise(float2 p, float seed)
            {
                const float K1 = 0.366025404;
                const float K2 = 0.211324865;
                
                float2 i = floor(p + (p.x + p.y) * K1);
                float2 a = p - i + (i.x + i.y) * K2;
                float2 o = (a.x > a.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float2 b = a - o + K2;
                float2 c = a - 1.0 + 2.0 * K2;
                
                float3 h = max(0.5 - float3(dot(a,a), dot(b,b), dot(c,c)), 0.0);
                float3 n = h * h * h * h * float3(dot(a, hash2(i, seed)), dot(b, hash2(i + o, seed)), dot(c, hash2(i + 1.0, seed)));
                
                return dot(n, float3(70.0, 70.0, 70.0));
            }

            float fbm(float2 p, float seed)
            {
                float f = 0.0;
                float w = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    f += w * noise(p, seed);
                    p *= 2.0;
                    w *= 0.5;
                }
                return f;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.normalWS = normalInput.normalWS;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.worldPos = vertexInput.positionWS;
                output.viewDist = length(_WorldSpaceCameraPos - output.worldPos);
                output.positionNDC = vertexInput.positionNDC;
                
                return output;
            }

            float3 ApplyAtmosphericScattering(float3 color, float3 worldPos)
            {
                // Height-based atmosphere with controlled range
                float heightRange = _AtmosphereEndHeight - _AtmosphereStartHeight;
                float normalizedHeight = (worldPos.y - _AtmosphereStartHeight) / heightRange;
                float heightFactor = 1.0 - saturate(normalizedHeight);
                heightFactor = pow(heightFactor, 0.5); // Make the height falloff more gradual
                
                // Scale with ortho size (zoom level)
                float orthoFactor = saturate((_OrthoSize - _AtmosphereMinOrthoSize) / 
                                            (_AtmosphereMaxOrthoSize - _AtmosphereMinOrthoSize));
                orthoFactor = smoothstep(0, 1, orthoFactor);
                
                // Combine factors
                float atmosphereFactor = heightFactor * _AtmosphereDensity * orthoFactor;
                atmosphereFactor = saturate(atmosphereFactor * 2.0);
                
                // Smoother blend
                atmosphereFactor = smoothstep(0.0, 0.7, atmosphereFactor);
                
                return lerp(color, _AtmosphereColor.rgb, atmosphereFactor);
            }

            float GetOrthoLinearDepth(float4 positionNDC)
            {
                #if UNITY_REVERSED_Z
                    real depth = positionNDC.z / positionNDC.w;
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, positionNDC.z / positionNDC.w);
                #endif
                
                return (1.0 - depth) * _ProjectionParams.z;
            }

            float GetOrthoFalloff(float4 positionNDC, float3 worldPos)
            {
                // Use ortho size for falloff instead of depth
                float orthoFactor = saturate((_OrthoSize - _OrthoFalloffStart) / 
                                            (_OrthoFalloffEnd - _OrthoFalloffStart));
                
                // Smooth the transition
                return 1.0 - smoothstep(0, 1, orthoFactor);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                
                // Get main light and shadow data
                Light mainLight = GetMainLight(input.shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                
                // Apply overcast and softness to all shadows
                shadow = lerp(shadow, 0.5, _OvercastFactor);  // Blend towards 0.5 for overcast
                shadow = lerp(0.0, 1.0, smoothstep(0.0, _ShadowSoftness + 0.1, shadow));
                
                // Modify the lighting calculation to better handle SSAO
                float3 lighting = mainLight.color;
                
                // Soften the SSAO interaction
                float aoFactor = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoFactors = GetScreenSpaceAmbientOcclusion(input.positionNDC.xy / input.positionNDC.w);
                    float falloff = GetOrthoFalloff(input.positionNDC, input.worldPos);
                    aoFactor = lerp(1.0, aoFactors.indirectAmbientOcclusion, falloff);
                #endif

                lighting *= (shadow * mainLight.distanceAttenuation * aoFactor);
                
                // Reduce lighting contrast in overcast conditions
                lighting = lerp(lighting, float3(0.7, 0.7, 0.7), _OvercastFactor);
                
                color.rgb *= lighting;
                
                // Calculate cloud density modifier based on ortho size
                float orthoFactor = saturate((_OrthoSize - _CloudMinOrthoSize) / (_CloudMaxOrthoSize - _CloudMinOrthoSize));
                orthoFactor = smoothstep(0, 1, orthoFactor);
                
                // Calculate cloud position
                float2 worldUV = input.worldPos.xz / _CloudScale;
                float time = _Time.y * _CloudSpeed;
                worldUV.x += time;
                
                // Calculate and apply cloud shadows BEFORE clouds
                float3 lightDir = normalize(_MainLightPosition.xyz);
                float3 worldPos = input.worldPos;
                float rayLength = (_CloudHeight - worldPos.y) / lightDir.y;
                float3 shadowSamplePos = worldPos + lightDir * rayLength;
                
                float2 shadowUV = shadowSamplePos.xz / _CloudScale;
                shadowUV.x += _Time.y * _CloudSpeed;
                float cloudShadow = fbm(shadowUV, _WorldSeed);
                cloudShadow = smoothstep(_CloudDensity - 0.3, _CloudDensity + 0.3, cloudShadow);
                
                float shadowFactor = cloudShadow * _ShadowStrength;
                shadowFactor *= (1.0 - saturate(rayLength / (_CloudHeight * 2.0)));
                shadowFactor *= max(0, dot(input.normalWS, lightDir));
                
                // Apply shadow darkening to base color
                color.rgb *= (1.0 - shadowFactor);

                // Now calculate and apply clouds ON TOP
                float clouds = fbm(worldUV, _WorldSeed);
                clouds = smoothstep(_CloudDensity - 0.3, _CloudDensity + 0.3, clouds);
                clouds *= orthoFactor;
                
                float cloudFade = 1.0 - saturate((input.worldPos.y - _CloudHeight) / (_CloudHeight * 0.5));
                clouds *= cloudFade;
                
                // Blend clouds over the already shadowed terrain
                color.rgb = lerp(color.rgb, _CloudColor.rgb, clouds * _CloudColor.a);
                
                // Apply atmospheric scattering AFTER clouds
                color.rgb = ApplyAtmosphericScattering(color.rgb, input.worldPos);
                
                // Apply fog last
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                // Calculate block position (round to nearest block)
                float3 blockPos = floor(input.worldPos);
                float2 noiseUV = blockPos.xz; // Use block coordinates directly, no scaling needed
                float colorNoise = fbm(noiseUV, _WorldSeed);
                float3 colorVariation = (colorNoise - 0.5) * _ColorVariationStrength;
                color.rgb += colorVariation;
                
                return color;
            }
            ENDHLSL
        }

        // Shadow casting pass
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : NORMAL;
                float4 positionNDC  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionNDC = vertexInput.positionNDC;
                
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoFactors = GetScreenSpaceAmbientOcclusion(input.positionNDC.xy / input.positionNDC.w);
                    return half4(0, 0, 0, aoFactors.directAmbientOcclusion);
                #else
                    return 0;
                #endif
            }
            ENDHLSL
        }
    }
} 