Shader "Hidden/Vista/Graph/Curvature"
{
	Properties
	{
		_MainTex("Texture", 2D) = "black" {}
	}

	CGINCLUDE
	#include "UnityCG.cginc"
	#pragma vertex vert

	struct appdata
	{
		float4 vertex: POSITION;
		float2 uv: TEXCOORD0;
	};

	struct v2f
	{
		float2 uv: TEXCOORD0;
		float4 vertex: SV_POSITION;
	};

	sampler2D _MainTex;
	float4 _MainTex_ST;
	float4 _MainTex_TexelSize;
	sampler2D _LowPassTex;
	float _Radius;
	float _DeviationScale;
	float _Sign;

	v2f vert(appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex);
		return o;
	}

	// The C# side keeps the texel radius around 4 by resampling the input to a meter locked grid,
	// so a fixed kernel extent of 8 always covers the full radius.
	#define KERNEL_EXTENT 8

	float fragBlur(v2f input): SV_Target
	{
		float2 texelSize = _MainTex_TexelSize.xy;
		float sigma = max(0.0001, _Radius * 0.5);
		float totalValue = 0;
		float totalWeight = 0;
		for (int offsetX = -KERNEL_EXTENT; offsetX <= KERNEL_EXTENT; ++offsetX)
		{
			for (int offsetY = -KERNEL_EXTENT; offsetY <= KERNEL_EXTENT; ++offsetY)
			{
				float d = sqrt(offsetX * offsetX + offsetY * offsetY);
				float2 uv = input.uv + float2(offsetX * texelSize.x, offsetY * texelSize.y);
				float inRange = d <= _Radius;
				float inBounds = (uv.x >= 0) * (uv.x <= 1) * (uv.y >= 0) * (uv.y <= 1);
				float weight = inRange * inBounds * exp(-d * d / (2 * sigma * sigma));
				totalValue += tex2D(_MainTex, uv).r * weight;
				totalWeight += weight;
			}
		}
		return totalValue / max(0.0001, totalWeight);
	}

	float fragComposite(v2f input): SV_Target
	{
		float heightValue = tex2D(_MainTex, input.uv).r;
		float lowPassValue = tex2D(_LowPassTex, input.uv).r;
		float deviation = (heightValue - lowPassValue) * _DeviationScale;
		return saturate(_Sign * deviation);
	}
	ENDCG

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		ColorMask R

		Pass
		{
			CGPROGRAM
			#pragma fragment fragBlur
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma fragment fragComposite
			ENDCG
		}
	}
}
