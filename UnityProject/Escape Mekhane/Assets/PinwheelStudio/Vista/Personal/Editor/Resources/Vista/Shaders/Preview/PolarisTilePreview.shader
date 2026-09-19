Shader "Hidden/Vista/Preview/PolarisTilePreview"
{
    Properties
    {
        _Mask("Mask", 2D) = "black" {}
        _Color("Color", Color) = (1, 1, 1, 0.5)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }
        ZWrite Off
        ZTest LEqual
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers gles

            #include "UnityCG.cginc"

            sampler2D _Mask;
            float4 _MaskBounds;
            float4 _Color;
            float _Animated;

            static const float STRIPE_THICKNESS = 0.01;
            static const float STRIPE_BRIGHTNESS = 0.25;
            static const float OVERALL_OPACITY = 0.5;

            struct appdata
            {
                float4 positionOS : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
                output.positionCS = UnityWorldToClipPos(output.positionWS);
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                float2 maskUV = (input.positionWS.xz - _MaskBounds.xy) / _MaskBounds.zw;
                float inBounds = all(saturate(maskUV) == maskUV) ? 1 : 0;
                float mask = tex2D(_Mask, maskUV).r * inBounds;
                float3 normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 viewDirectionWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(1 - saturate(dot(normalWS, viewDirectionWS)), 3);
                float stripePhase = frac(input.positionWS.y * 0.1 - _Time.y * 3);
                float stripeHalfThickness = STRIPE_THICKNESS * 0.5;
                float tiledSheen = (1 - smoothstep(
                    stripeHalfThickness,
                    stripeHalfThickness + 0.1,
                    abs(stripePhase - 0.5))) * _Animated;
                float gloss = saturate(fresnel * 0.2 + tiledSheen * STRIPE_BRIGHTNESS);
                return float4(lerp(_Color.rgb * 0.85, 1, gloss), _Color.a * mask * OVERALL_OPACITY);
            }
            ENDCG
        }
    }
}
