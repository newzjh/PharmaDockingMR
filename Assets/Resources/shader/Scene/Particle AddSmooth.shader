Shader "Scene/Particles/Additive (Soft)" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}

SubShader {
	Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 200

CGPROGRAM
#pragma surface surf Simple alpha:fade
#pragma multi_compile_instancing

sampler2D _MainTex;
fixed4 _Color;

half4 LightingSimple (SurfaceOutput s, half3 lightDir, half atten) {
    half4 c;
    c.rgb = s.Albedo *2 ;
    c.a = s.Alpha;
    return c;
}

struct Input {
	float2 uv_MainTex;
};

void surf (Input IN, inout SurfaceOutput o) {
	fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
	o.Albedo = c.rgb;
	o.Alpha = c.a;
}
ENDCG
}

Fallback "Legacy Shaders/Transparent/Diffuse"
}
