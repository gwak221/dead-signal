// 추격 중 tall_cabinet 모서리 라인용 Unlit 가산 셰이더.
// 깊이 테스트(ZTest LEqual)를 하므로 벽 뒤 캐비넷은 보이지 않고, 깊이는 쓰지 않는다.
Shader "DeadSignal/ChaseEdge"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.78, 0.45, 1)
        _Fade ("Fade", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Edge"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(_Color.rgb * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
