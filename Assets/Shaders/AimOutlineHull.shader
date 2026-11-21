// 얇은 외곽선(부풀린 앞면 헐). 대상의 합쳐진 월드 Bounds 중심에서 바깥쪽으로 정점을 밀어 낸다(위치 기반이라 모서리가 갈라지지 않고,
// 읽기 불가(non-readable) 메시/FBX 에서도 동작). 두께는 화면 픽셀 기준으로 거의 일정. 스텐실로 실루엣 안쪽은 그리지 않는다.
Shader "DeadSignal/AimOutlineHull"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Center ("Center (world)", Vector) = (0,0,0,0)
        _Extents ("Half Extents (world)", Vector) = (1,1,1,0)
        _PixelWidth ("Pixel Width", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+500" }
        Pass
        {
            Name "Hull"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back      // 카메라를 향한 면만 바깥으로 부풀려 그린다 → 가려진 부분은 깊이 검사로 사라지고, 인접 파츠와 같은 면 높이여도 링이 보인다
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 21 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Center;
                float4 _Extents;
                float _PixelWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i)
            {
                float3 wp = TransformObjectToWorld(i.positionOS.xyz);
                float3 d = (wp - _Center.xyz) / max(_Extents.xyz, 0.03);
                float3 dir = d / max(length(d), 1e-4);
                float dist = distance(_WorldSpaceCameraPos, wp);
                float worldPerPixel = dist * 2.0 / (unity_CameraProjection._m11 * _ScreenParams.y);
                wp += dir * worldPerPixel * _PixelWidth;
                Varyings o;
                o.positionCS = TransformWorldToHClip(wp);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
