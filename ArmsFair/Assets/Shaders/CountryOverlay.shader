Shader "ArmsFair/CountryOverlay"
{
    Properties
    {
        _CountryIDMap ("Country ID Map", 2D) = "black" {}
        _BorderMap    ("Border Map",     2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CountryIDMap); SAMPLER(sampler_CountryIDMap);
            TEXTURE2D(_BorderMap);    SAMPLER(sampler_BorderMap);

            // Updated every round from C# via MaterialPropertyBlock
            float _CountryTensions[250];

            // Stage colors  0=dormant 1=simmering 2=active 3=hot war 4=crisis 5=failed
            static float4 STAGE_COLORS[6] = {
                float4(0.05, 0.12, 0.05, 1),
                float4(0.05, 0.25, 0.25, 1),
                float4(0.50, 0.35, 0.02, 1),
                float4(0.65, 0.12, 0.05, 1),
                float4(0.80, 0.05, 0.05, 1),
                float4(0.20, 0.20, 0.20, 1)
            };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS   = normalize(GetWorldSpaceViewDir(
                                      TransformObjectToWorld(IN.positionOS.xyz)));
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 idSample = SAMPLE_TEXTURE2D(_CountryIDMap, sampler_CountryIDMap, IN.uv);

                // Ocean: both channels black
                if (idSample.r < 0.004 && idSample.g < 0.004)
                    return float4(0.04, 0.10, 0.25, 1);

                // Decode index (offset by 1 in baker to avoid ocean collision)
                int enc        = (int)(idSample.r * 255.0 + 0.5) + (int)(idSample.g * 255.0 + 0.5) * 256;
                int countryIdx = enc - 1;

                float tension    = _CountryTensions[clamp(countryIdx, 0, 249)];
                int   stage      = clamp((int)(tension / 20.0), 0, 5);
                float4 baseColor = STAGE_COLORS[stage];

                // Border glow
                float  border      = SAMPLE_TEXTURE2D(_BorderMap, sampler_BorderMap, IN.uv).r;
                float4 borderColor = lerp(baseColor, float4(0.6, 0.8, 1.0, 1.0), border * 0.6);

                // Atmosphere fresnel
                float  fresnel = pow(1.0 - saturate(dot(IN.normalWS, IN.viewDirWS)), 3.0);
                float4 atmo    = float4(0.1, 0.3, 0.8, 1.0) * fresnel * 0.4;

                // Emission pulse for hot zones
                float emission = (tension / 100.0) * 0.25;

                return borderColor + atmo + float4(baseColor.rgb * emission, 0);
            }
            ENDHLSL
        }
    }
}
