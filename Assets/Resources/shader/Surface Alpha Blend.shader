Shader "Surface/Alpha Blended" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB) Gloss (A)", 2D) = "white" {}
		_Illum ("Illumin (A)", 2D) = "white" {}
		_Emission ("Emission (Lightmapper)", Float) = 1.0
	}
	SubShader {
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		LOD 200
	
		CGPROGRAM
		#pragma surface surf Lambert alpha:fade

		sampler2D _MainTex;
		sampler2D _Illum;
		fixed4 _Color;
		fixed _Emission;

		struct Input {
			float2 uv_MainTex;
			float2 uv_Illum;
			fixed4 color : COLOR;
		};

		void surf (Input IN, inout SurfaceOutput o) {
			fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
			fixed4 c = IN.color;
			o.Albedo = c.rgb * _Color;
			o.Emission = c.rgb *_Color* tex2D(_Illum, IN.uv_Illum).a;
			o.Alpha = c.a*_Color.a;	
		}
		ENDCG
	} 
	FallBack "Legacy Shaders/Transparent/Diffuse"
}