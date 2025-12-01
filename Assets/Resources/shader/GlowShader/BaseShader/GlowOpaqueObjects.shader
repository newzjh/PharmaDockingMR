
Shader "Effect/Glow/GlowOpaqueObjects" {
    Properties {
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _GlowStrength ("Glow Strength", Float) = 1.0
        _GlowColor ("Glow Color", Color)  = (1,1,1,1)
    }

Subshader {
    Tags { "RenderEffect"="Glow" "RenderType"="Glow" }
    Pass {
        Lighting Off  Fog { Mode off } 
        Name "OpaqueGlow"
        CGPROGRAM            
            #include "GlowObjectsCG.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest            
            #pragma vertex vert
            #pragma fragment fragGlow
            #pragma multi_compile GLOW_MAINTEX GLOW_MAINCOLOR GLOW_GLOWTEX GLOW_ILLUMTEX GLOW_GLOWCOLOR GLOW_VERTEXCOLOR 
            #pragma multi_compile NO_MULTIPLY MULTIPLY_GLOWCOLOR MULTIPLY_VERT MULTIPLY_VERT_ALPHA MULTIPLY_ILLUMTEX_ALPHA MULTIPLY_MAINTEX_ALPHA
            #pragma multi_compile _ HIGH_QUALITY_GLOW
        ENDCG    
    }
    
}
   
}