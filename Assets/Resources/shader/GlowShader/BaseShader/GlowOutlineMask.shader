// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "ImageEffect/GlowOutlineMask" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
    _ColorStrength("ColorStrength[_ColorStrength]", Float) = 0.05     
	_Color ("Color[_Color]", Color) = (1,1,1,1)
	_Cutoff ("Alpha cutoff", Range (0,1)) = 0.01
}

SubShader {
	
	Pass {  

		LOD 200
        Blend Off
        Cull Back Lighting Off
        ZWrite Off
		AlphaTest Greater [_Cutoff]

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
			};

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;

			uniform float _ColorStrength;
			uniform float4 _Color;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 img = tex2D(_MainTex, i.texcoord);
				fixed4 col = _Color*_ColorStrength;//tex2D(_MainTex, i.texcoord);
				return float4(col.rgb,img.a);
				return col;
			}

		ENDCG
	}
}

}
