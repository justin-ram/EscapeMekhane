Shader "Hidden/Vista/Preview/UnityTerrainTilePreview"
{
    SubShader
    {
        ZTest Always
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers gles

            #include "UnityCG.cginc"
            #include "TerrainPreview.cginc"

            sampler2D _BrushTex;
            float4 _Color;
            float _Animated;

            static const float STRIPE_THICKNESS = 0.01;
            static const float STRIPE_BRIGHTNESS = 0.25;
            static const float OVERALL_OPACITY = 0.5;

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 brushUV : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                float2 paintContextPixels = BuildProceduralQuadMeshVertex(vertexID);
                float2 heightmapUV = PaintContextPixelsToHeightmapUV(paintContextPixels);
                float height = UnpackHeightmap(tex2Dlod(_Heightmap, float4(heightmapUV, 0, 0)));
                float3 positionOS = PaintContextPixelsToObjectPosition(paintContextPixels, height);
                float3 positionWS = TerrainObjectToWorldPosition(positionOS);

                v2f output;
                output.positionCS = UnityWorldToClipPos(positionWS);
                output.brushUV = PaintContextPixelsToBrushUV(paintContextPixels);
                output.positionWS = positionWS;
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                float inBounds = all(saturate(input.brushUV) == input.brushUV) ? 1 : 0;
                float mask = tex2D(_BrushTex, input.brushUV).r * inBounds;

                float3 normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 viewDirectionWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 lightDirectionWS = normalize(UnityWorldSpaceLightDir(input.positionWS));
                float3 halfDirectionWS = normalize(viewDirectionWS + lightDirectionWS);
                float diffuse = saturate(dot(normalWS, lightDirectionWS));
                float specular = pow(saturate(dot(normalWS, halfDirectionWS)), 32);
                float fresnel = pow(1 - saturate(dot(normalWS, viewDirectionWS)), 3);
                float stripePhase = frac(input.positionWS.y * 0.1 - _Time.y * 3);
                float stripeHalfThickness = STRIPE_THICKNESS * 0.5;
                float tiledSheen = (1 - smoothstep(
                    stripeHalfThickness,
                    stripeHalfThickness + 0.1,
                    abs(stripePhase - 0.5))) * _Animated;

                float3 baseColor = _Color.rgb * lerp(0.7, 1, diffuse);
                float gloss = saturate(specular * 0.5 + fresnel * 0.2 + tiledSheen * STRIPE_BRIGHTNESS);
                float3 glossyColor = lerp(baseColor, 1, gloss);
                return float4(glossyColor, _Color.a * mask * OVERALL_OPACITY);
            }
            ENDCG
        }
    }
}
