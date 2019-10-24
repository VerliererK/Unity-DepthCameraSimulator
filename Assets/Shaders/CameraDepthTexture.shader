Shader "Unlit/CameraDepthTexture"
{
    Properties
    {
		[HideInInspector]
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
			   float d = SAMPLE_DEPTH_TEXTURE(_MainTex, i.uv);  // get R-channel
#if defined(UNITY_REVERSED_Z)
				d = 1.0 - d;
#endif

				fixed4 col = float4(d, d, d, 1);
				if (d >= 1 || d <= 0) {
					col.r = 1;
					col.g = 1;
					col.b = 1;
					col.a = 0;
				}

				return col;
            }
            ENDCG
        }
    }
}
