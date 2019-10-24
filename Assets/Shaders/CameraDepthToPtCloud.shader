Shader "Hidden/CameraDepthToPtCloud"
{
	Properties
	{
		[HideInInspector]
		_MainTex("Texture", 2D) = "white" {}
		_Tint("Tint", Color) = (0.5, 0.5, 0.5, 1)
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			half4 _Tint;
			float4x4 _CameraInverseVP;
			RWStructuredBuffer<float3> _PointCloudBuffer : register(u1);

			v2f vert(uint vid : SV_VertexID)  //use DrawProcedural(MeshTopology.Points, width*height, 1);
			{
				float4 pos = float4(0, 0, 0, 0);
				float width = _MainTex_TexelSize.z;
				float height = _MainTex_TexelSize.w;
				float y = floor(vid / width);
				float x = floor(vid % width);
				x = x / width;
				y = y / height;
				float2 bufferOffset = float2(0.5 / width, 0.5 / height);
				float2 uv = float2(x, y) + bufferOffset;
				half4 tex = tex2Dlod(_MainTex, float4(uv, 0, 0));
				float depth = tex.r;
#if defined(UNITY_REVERSED_Z)
				depth = 1 - depth;
#endif
				if (depth > 0.01 && depth < 1)
				{
					float4 H = float4(uv.x * 2 - 1, uv.y * 2 - 1, depth * 2 - 1, 1); // Normalized Device Coordinate
					float4 D = mul(_CameraInverseVP, H);  // To World Coordinate
					float4 W = D / D.w;  // Homogeneous Coordinate
					pos = W;
				}

				_PointCloudBuffer[vid] = pos.xyz;

				v2f o;
				o.vertex = UnityObjectToClipPos(pos);

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return _Tint;
			}
			ENDCG
		}
	}
}