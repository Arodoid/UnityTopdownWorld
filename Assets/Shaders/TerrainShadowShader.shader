Shader "Custom/TerrainShadowShader"
{
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue"="Geometry" // This is 2000
        }
        
        // Main pass with colors, shadows, and AO compatibility
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            
            ZWrite On // Enable depth writing
            ZTest Less  // Changed from LEqual to Less
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL; // Add normal attribute
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0; // Pass world-space normals
                float4 color : COLOR;
                float4 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); // Transform normals to world space
                output.color = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Get shadow coordinates
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS.xyz);
                
                // Get main light and shadow attenuation
                Light mainLight = GetMainLight(shadowCoord);
                
                // Calculate final color with light intensity and color
                float3 litColor = input.color.rgb * mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // Include normals in lighting calculations (AO uses normals indirectly)
                float3 normal = normalize(input.normalWS);

                // Final output
                return float4(litColor, input.color.a);
            }
            ENDHLSL
        }
        
        // Shadow casting pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            float4 frag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
