// 조준 대상 실루엣을 스텐실에 기록 (색/깊이는 쓰지 않음). 같은 대상의 여러 Renderer 를 하나의 덩어리로 묶어
// 부품 사이의 안쪽 외곽선이 생기지 않게 한다. 원본 Material 은 건드리지 않는다.
Shader "DeadSignal/AimOutlineStamp"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+499" }
        Pass
        {
            Name "Stamp"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            ColorMask 0
            Cull Off
            Stencil { Ref 21 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
