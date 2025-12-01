// Unlit alpha-cutout shader.
// - no lighting
// - no lightmap support
// - no per-material color

Shader "Effect/Glow/Unlit/Transparent Cutout" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    _GlowTex ("Glow", 2D) = "" {}
    _GlowColor ("Glow Color", Color)  = (1,1,1,1)  
    _GlowStrength ("Glow Strength", Float) = 1.0
}

SubShader {
	Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderEffect"="GlowTransparentCutout" "RenderType"="GlowTransparentCutout" }
	LOD 100
	
	Pass {
		Lighting Off
		Alphatest Greater [_Cutoff]
		SetTexture [_MainTex] { combine texture } 
	}
}
CustomEditor "GlowMatInspector"
}