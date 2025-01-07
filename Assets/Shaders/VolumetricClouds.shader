Shader "Custom/VolumetricClouds"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CloudScale ("Cloud Scale", Range(1, 100)) = 50
        _CloudSpeed ("Cloud Speed", Range(0, 1)) = 0.1
        _CloudDensity ("Cloud Density", Range(0, 1)) = 0.5
        _CloudHeight ("Cloud Height", Range(10, 1000)) = 100
        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" "PreviewType"="Plane" "IgnoreProjector"="True" }
        
        Pass
        {
            Name "CloudPass"
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _CloudScale;
            float _CloudSpeed;
            float _CloudDensity;
            float _CloudHeight;
            float4 _CloudColor;
            float _ShadowStrength;
            float _OrthoSize;
            
            // Noise functions
            float2 hash2(float2 p)
            {
                p = float2(dot(p,float2(127.1,311.7)), dot(p,float2(269.5,183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }
            
            float noise(float2 p)
            {
                const float K1 = 0.366025404;
                const float K2 = 0.211324865;
                
                float2 i = floor(p + (p.x + p.y) * K1);
                float2 a = p - i + (i.x + i.y) * K2;
                float2 o = (a.x > a.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float2 b = a - o + K2;
                float2 c = a - 1.0 + 2.0 * K2;
                
                float3 h = max(0.5 - float3(dot(a,a), dot(b,b), dot(c,c)), 0.0);
                float3 n = h * h * h * h * float3(dot(a, hash2(i)), dot(b, hash2(i + o)), dot(c, hash2(i + 1.0)));
                
                return dot(n, float3(70.0, 70.0, 70.0));
            }
            
            float fbm(float2 p)
            {
                float f = 0.0;
                float w = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    f += w * noise(p);
                    p *= 2.0;
                    w *= 0.5;
                }
                return f;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _CloudSpeed;
                
                // Scale UV based on orthographic size
                float scale = _CloudScale * (_OrthoSize / 50.0); // Adjust clouds based on zoom level
                
                // Generate multiple layers of noise
                float2 q = uv * scale;
                q.x += time * 0.5;
                
                float f = fbm(q);
                float2 r = float2(fbm(q + float2(7.7, 4.6) + 0.6 * time), fbm(q + float2(8.3, -2.4) - 0.6 * time));
                float clouds = fbm(q + r);
                
                // Apply density and create soft edges
                clouds = smoothstep(_CloudDensity - 0.3, _CloudDensity + 0.3, clouds);
                
                // Fade clouds based on camera height
                float heightFade = 1.0 - saturate((_WorldSpaceCameraPos.y - _CloudHeight) / (_CloudHeight * 0.5));
                clouds *= heightFade;
                
                // Create final color with transparency
                float4 finalColor = _CloudColor;
                finalColor.a = clouds * _CloudColor.a;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
} 