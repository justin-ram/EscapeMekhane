Shader "Hidden/Vista/Graph/SamplePoint"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        ColorMask R

        Pass
        {
            CGPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex: POSITION;
            };

            struct v2f
            {
                float4 vertex: SV_POSITION;
            };

            StructuredBuffer<float> _Positions;
            int _SampleIndex;
            int _ChannelOffset;
            int _HasSample;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float frag(v2f input): SV_Target
            {
                if (_HasSample == 0)
                    return 0;

                return _Positions[_SampleIndex * 4 + _ChannelOffset];
            }
            ENDCG
        }
    }
}
