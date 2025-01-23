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
        _ColorVariationStrength ("Color Variation Strength", Range(0, 0.2)) = 0.02
        _ColorVariationScale ("Color Variation Scale", Range(1, 100)) = 25
        _WaterNoiseScale ("Water Noise Scale", Range(1, 100)) = 20
        _WaterNoiseSpeed ("Water Noise Speed", Range(0, 10)) = 2
        _WaterShineStrength ("Water Shine Strength", Range(0, 1)) = 0.3
        _WaterRippleStrength ("Water Ripple Strength", Range(0, 1)) = 0.15
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1
        _WaveStrength ("Wave Strength", Range(0, 0.5)) = 0.2
        _WaveDistance ("Wave Distance", Range(1, 3)) = 1
        [Header(Cloud Shadow Settings)]
        _CloudShadowColor ("Cloud Shadow Color", Color) = (0.2, 0.23, 0.27, 1.0)  // Slightly bluish shadow
        _CloudShadowStrength ("Cloud Shadow Strength", Range(0, 1)) = 0.6
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

            #define CHUNK_SIZE 32  // This should match ChunkData.SIZE

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

            float _WaterNoiseScale;
            float _WaterNoiseSpeed;
            float _WaterShineStrength;
            float _WaterRippleStrength;

            float _WaveSpeed;
            float _WaveStrength;
            float _WaveDistance;

            float4 _CloudShadowColor;
            float _CloudShadowStrength;

            struct WaterEdgeInfo {
                bool isEdge;
                float4 edgeDir; // x,y,z,w represent right,left,up,down edges
            };

            WaterEdgeInfo GetWaterEdgeInfo(float2 uv)
            {
                WaterEdgeInfo info;
                info.isEdge = false;
                info.edgeDir = float4(0, 0, 0, 0);
                
                // Check neighboring blocks (using UV coordinates)
                float2 offsets[4] = {
                    float2(1.0/CHUNK_SIZE, 0),    // right
                    float2(-1.0/CHUNK_SIZE, 0),   // left
                    float2(0, 1.0/CHUNK_SIZE),    // up
                    float2(0, -1.0/CHUNK_SIZE)    // down
                };
                
                for (int i = 0; i < 4; i++)
                {
                    float2 samplePos = uv + offsets[i];
                    float4 sampleColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePos);
                    
                    // If neighbor is not water (alpha == 1.0)
                    if (sampleColor.a >= 1.0)
                    {
                        info.isEdge = true;
                        info.edgeDir[i] = 1.0;
                    }
                }
                
                return info;
            }

            float GetWaveOffset(float2 blockPos, WaterEdgeInfo edgeInfo, float time)
            {
                if (!edgeInfo.isEdge)
                    return 0;
                    
                float waveOffset = 0;
                float2 waveDir = float2(0, 0);
                
                // Combine wave directions based on edges
                if (edgeInfo.edgeDir.x > 0) waveDir += float2(-1, 0);  // right edge, wave moves left
                if (edgeInfo.edgeDir.y > 0) waveDir += float2(1, 0);   // left edge, wave moves right
                if (edgeInfo.edgeDir.z > 0) waveDir += float2(0, -1);  // up edge, wave moves down
                if (edgeInfo.edgeDir.w > 0) waveDir += float2(0, 1);   // down edge, wave moves up
                
                if (length(waveDir) > 0)
                {
                    waveDir = normalize(waveDir);
                    float2 waveFront = blockPos + waveDir * time * _WaveSpeed;
                    float distanceFromEdge = length(waveFront - blockPos);
                    
                    // Create a more pronounced wave pattern
                    float wave = sin(distanceFromEdge * 3.14159 * 2.0 - time * _WaveSpeed * 3.0);
                    wave = sign(wave) * 0.5; // Make it more blocky
                    waveOffset = wave * _WaveStrength * (1.0 - saturate(distanceFromEdge / _WaveDistance));
                }
                
                return waveOffset;
            }

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

            float waterNoise(float2 uv, float time)
            {
                // Scale down the UV coordinates and round to create distinct blocks
                float2 blockUV = floor(uv * (1.0 / _WaterNoiseScale)) * _WaterNoiseScale;
                
                // Add world position offset to reduce pattern repetition
                float2 worldOffset = floor(uv / 100.0) * 73.156; // Large prime number helps break repetition
                
                // Create two separate block-based patterns that will alternate
                float noise1 = noise(blockUV + float2(time, 0) + worldOffset, _WorldSeed);
                float noise2 = noise(blockUV * 1.5 + float2(0, time * 0.7) - worldOffset * 0.7, _WorldSeed + 1.0);
                
                // Make the pattern more discrete/blocky
                noise1 = floor(noise1 * 3) / 3.0;
                noise2 = floor(noise2 * 3) / 3.0;
                
                // Add a third noise layer at a different scale to break up patterns
                float noise3 = noise(blockUV * 0.7 + float2(time * 0.5, time * 0.3) + worldOffset * 0.3, _WorldSeed + 2.0);
                noise3 = floor(noise3 * 2) / 2.0;
                
                // Combine the patterns with varying weights
                float combined = (noise1 * 0.5 + noise2 * 0.3 + noise3 * 0.2);
                
                // Make it even more discrete
                return floor(combined * 4) / 4.0;
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
                
                // Calculate ray length to cloud height
                float rayLength = (_CloudHeight - worldPos.y) / lightDir.y;
                float3 shadowSamplePos = worldPos + lightDir * rayLength;

                // Check if the shadow sample point is below terrain
                bool shadowPointInTerrain = shadowSamplePos.y < worldPos.y;

                // Calculate and apply cloud shadows
                float2 shadowUV = shadowSamplePos.xz / _CloudScale;
                shadowUV.x += _Time.y * _CloudSpeed;
                float cloudShadow = fbm(shadowUV, _WorldSeed);
                cloudShadow = smoothstep(_CloudDensity - 0.3, _CloudDensity + 0.3, cloudShadow);

                float shadowFactor = cloudShadow * _ShadowStrength;
                shadowFactor *= (1.0 - saturate(rayLength / (_CloudHeight * 2.0)));
                shadowFactor *= max(0, dot(input.normalWS, lightDir));

                // Apply the same ortho scaling as clouds (not inverted)
                shadowFactor *= orthoFactor;

                // Smooth transition for shadows when they would intersect terrain
                float shadowTerrainFade = smoothstep(0, 20, shadowSamplePos.y - worldPos.y);
                shadowFactor *= shadowTerrainFade;

                // Only apply shadow if the ray hasn't intersected terrain
                if (shadowPointInTerrain) {
                    shadowFactor = 0;
                }

                // Apply block-based color variation BEFORE shadow calculations
                float2 blockPos = floor(input.worldPos.xz / _ColorVariationScale) * _ColorVariationScale;

                // First noise layer
                float2 noiseUV = blockPos / (_ColorVariationScale * 1.0);
                float colorNoise1 = fbm(noiseUV, _WorldSeed + 42.1);

                // Second noise layer at different scale and rotation
                float2 rotatedUV1 = float2(
                    blockPos.x * cos(0.7) - blockPos.y * sin(0.7),
                    blockPos.x * sin(0.7) + blockPos.y * cos(0.7)
                ) / (_ColorVariationScale * 2.3);
                float colorNoise2 = fbm(rotatedUV1, _WorldSeed + 13.7);

                // Third noise layer with different rotation and scale
                float2 rotatedUV2 = float2(
                    blockPos.x * cos(1.3) - blockPos.y * sin(1.3),
                    blockPos.x * sin(1.3) + blockPos.y * cos(1.3)
                ) / (_ColorVariationScale * 3.7);
                float colorNoise3 = fbm(rotatedUV2, _WorldSeed + 89.3);

                // Fourth noise layer for large-scale variation
                float2 largeScaleUV = blockPos / (_ColorVariationScale * 5.5);
                float colorNoise4 = fbm(largeScaleUV, _WorldSeed + 127.1);

                // Combine all noise layers with different weights
                float colorNoise = colorNoise1 * 0.4 + 
                                  colorNoise2 * 0.3 + 
                                  colorNoise3 * 0.2 + 
                                  colorNoise4 * 0.1;

                // Make it more discrete/blocky
                colorNoise = floor(colorNoise * 6) / 6.0;

                float3 colorVariation = (colorNoise - 0.5) * _ColorVariationStrength;

                // Apply variation BEFORE shadow calculations
                color.rgb += colorVariation;

                float3 shadowColor = lerp(float3(1,1,1), _CloudShadowColor.rgb, _CloudShadowStrength);
                float3 adjustedShadowStrength = float3(
                    shadowFactor * (1.0 - (1.0 - shadowColor.r) * 0.7),
                    shadowFactor * (1.0 - (1.0 - shadowColor.g) * 0.7),
                    shadowFactor * (1.0 - (1.0 - shadowColor.b) * 0.7)
                );

                // Clamp shadow factor to prevent extreme values
                adjustedShadowStrength = min(adjustedShadowStrength, 0.85);

                // Apply shadow directly without luminance preservation
                float3 shadowedColor = color.rgb * (float3(1,1,1) - adjustedShadowStrength);

                // Soften very dark areas
                float darknessFactor = 1.0 - smoothstep(0.1, 0.3, length(shadowedColor));
                shadowedColor = lerp(shadowedColor, color.rgb * 0.3, darknessFactor);

                // Apply a slight ambient light contribution in shadowed areas
                float3 ambientLight = float3(0.4, 0.4, 0.5) * 0.2;
                float3 finalShadowedColor = shadowedColor + (ambientLight * shadowFactor);

                // Ensure we don't get values outside valid range
                finalShadowedColor = saturate(finalShadowedColor);

                // Now calculate and apply clouds ON TOP
                float clouds = fbm(worldUV, _WorldSeed);
                clouds = smoothstep(_CloudDensity - 0.3, _CloudDensity + 0.3, clouds);
                clouds *= orthoFactor;
                
                float cloudFade = 1.0 - saturate((input.worldPos.y - _CloudHeight) / (_CloudHeight * 0.5));
                clouds *= cloudFade;
                
                // Smooth transition when clouds intersect terrain
                float terrainFade = smoothstep(0, 20, _CloudHeight - input.worldPos.y); // 20 units fade distance
                clouds *= terrainFade;
                
                // Blend clouds over the already shadowed terrain
                color.rgb = lerp(finalShadowedColor, _CloudColor.rgb, clouds * _CloudColor.a);
                
                // Apply atmospheric scattering AFTER clouds
                color.rgb = ApplyAtmosphericScattering(color.rgb, input.worldPos);
                
                // Apply fog last
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                if (input.color.a < 1.0) // Water check
                {
                    // Calculate block position
                    float2 blockPos = floor(input.worldPos.xz);
                    float time = _Time.y * _WaterNoiseSpeed;
                    
                    // Get edge information and wave offset
                    WaterEdgeInfo edgeInfo = GetWaterEdgeInfo(input.uv);
                    float waveOffset = GetWaveOffset(blockPos, edgeInfo, _Time.y);
                    
                    // Generate block-based water pattern
                    float waterPattern = waterNoise(blockPos, time);
                    
                    // Simplified shininess that varies per block
                    float3 viewDir = normalize(_WorldSpaceCameraPos - input.worldPos);
                    float3 normalUp = float3(0, 1, 0);
                    float fresnel = pow(1.0 - saturate(dot(normalUp, viewDir)), 2);
                    fresnel = floor(fresnel * 3) / 3.0;
                    
                    // Combine effects
                    float3 waterColor = color.rgb;
                    waterColor += waterPattern * _WaterRippleStrength;
                    waterColor += fresnel * _WaterShineStrength;
                    
                    // Add wave effect more prominently
                    waterColor += float3(waveOffset, waveOffset, waveOffset) * 0.5; // Make waves more visible
                    
                    // Make water more vibrant
                    waterColor *= float3(1.1, 1.2, 1.3);
                    
                    color.rgb = waterColor;
                    color.a = 0.9;
                    
                    // Debug visualization - uncomment to see edge detection
                    // if (edgeInfo.isEdge) color.rgb = float3(1, 0, 0);
                }
                
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