Shader "Custom/BorderLineShader"
{
    Properties
    {
        _Color ("Line Color", Color) = (1, 1, 1, 1)
        _Thickness ("Line Thickness", Range(0.0, 0.5)) = 0.05
        [KeywordEnum(Left, Both, Right)] _Side ("Highlight Side", Float) = 0
        
        [Toggle] _UseDash ("Use Dash", Float) = 1
        _DashCount ("Dash Count", Float) = 10
        _DashRatio ("Dash Ratio", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UnlitPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Material Keywords
            #pragma shader_feature_local _SIDE_LEFT _SIDE_BOTH _SIDE_RIGHT
            #pragma shader_feature_local _USEDASH_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            half4 _Color;
            float _Thickness;
            float _DashCount;
            float _DashRatio;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;
                float mask = 0.0;

                #if defined(_SIDE_LEFT)
                mask = step(u, _Thickness);
                #elif defined(_SIDE_BOTH)
                mask = max(step(u, _Thickness), step(1.0 - _Thickness, u));
                #elif defined(_SIDE_RIGHT)
                mask = step(1.0 - _Thickness, u);
                #else
                mask = step(u, _Thickness);
                #endif

                #if defined(_USEDASH_ON)
                float dashMask = step(frac(v * _DashCount), _DashRatio);
                mask *= dashMask;
                #endif

                return half4(_Color.rgb, _Color.a * mask);
            }
            ENDHLSL
        }
    }
}