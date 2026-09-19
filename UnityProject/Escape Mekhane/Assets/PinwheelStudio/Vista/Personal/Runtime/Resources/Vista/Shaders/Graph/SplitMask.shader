Shader "Hidden/Vista/Graph/SplitMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        ColorMask R

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _SourceMap;
            float _Fraction;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float frag(v2f input) : SV_Target
            {
                return tex2D(_SourceMap, input.uv).r * _Fraction;
            }
            ENDCG
        }
    }
}
