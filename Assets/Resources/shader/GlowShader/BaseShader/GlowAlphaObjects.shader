
Shader "Effect/Glow/GlowAlphaObjects" {
    Properties {
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _GlowStrength ("Glow Strength", Float) = 1.0
        _GlowColor ("Glow Color", Color)  = (1,1,1,1)
    }


Subshader {
    Tags { "RenderEffect"="GlowTransparent" "RenderType"="GlowTransparent" "Queue"="Transparent"}
    Pass {    
        Cull Off Lighting Off ZWrite Off ZTest LEqual Fog { Mode off } 
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
            #define ALPHA 1
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
Fallback off
   
}