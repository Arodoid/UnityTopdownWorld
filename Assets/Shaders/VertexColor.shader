Shader "Custom/VertexColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AtmosphereColor ("Atmosphere Color", Color) = (0.6, 0.8, 1.0, 1.0)
        _AtmosphereDensity ("Atmosphere Density", Range(0, 1)) = 0.3
        _AtmosphereHeight ("Atmosphere Height", Float) = 50
        _OrthoSize ("Ortho Size", Float) = 10
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _AtmosphereColor;
            float _AtmosphereDensity;
            float _AtmosphereHeight;
            float _OrthoSize;

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
                
                return output;
            }

            float3 ApplyAtmosphericScattering(float3 color, float3 worldPos)
            {
                // Height-based atmosphere
                float heightFactor = 1 - saturate(worldPos.y / _AtmosphereHeight);
                
                // Scale with ortho size (zoom level), but with a reasonable limit
                float zoomFactor = saturate(_OrthoSize / 100.0); // Adjust 100.0 to change where atmosphere maxes out
                
                // Combine factors with a softer curve
                float atmosphereFactor = heightFactor * _AtmosphereDensity * zoomFactor;
                atmosphereFactor = saturate(atmosphereFactor); // Ensure we don't exceed 1
                
                // Use smoothstep for a more pleasing blend
                atmosphereFactor = smoothstep(0, 0.8, atmosphereFactor);
                
                return lerp(color, _AtmosphereColor.rgb, atmosphereFactor);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                
                // Get main light and shadow data
                Light mainLight = GetMainLight(input.shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                
                // Apply lighting and shadows
                float3 lighting = mainLight.color * (shadow * mainLight.distanceAttenuation);
                color.rgb *= lighting;
                
                // Apply atmospheric scattering
                color.rgb = ApplyAtmosphericScattering(color.rgb, input.worldPos);
                
                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
} 