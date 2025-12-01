// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "ImageEffect/DissloveGlowMask" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
    _ColorStrength("ColorStrength[_ColorStrength]", Float) = 0.05     
	_Color ("Color[_Color]", Color) = (1,1,1,1)
}

SubShader {
	LOD 100
	
	Pass {  

		LOD 200
        Blend One One
        Cull Back Lighting Off
        ZWrite Off
	
		//Stencil{
			//Ref 8
			//ReadMask 8
			//Comp Equal
		//}

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

			sampler2D _MainTex;
			float4 _MainTex_ST;

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
				fixed4 col = tex2D(_MainTex, i.texcoord)*_Color*_ColorStrength;
				return col;
			}

		ENDCG
	}
}

}
