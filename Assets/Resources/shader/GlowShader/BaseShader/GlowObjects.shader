
Shader "Effect/Glow/GlowObjects" {
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

Subshader {
    Tags { "RenderEffect"="GlowTransparentCutout" "RenderType"="GlowTransparentCutout" "Queue"="AlphaTest"}
    Pass {
        Lighting Off ZTest LEqual Fog { Mode off } 
        AlphaTest Greater [_Cutoff]
        CGPROGRAM    
            #define ALPHA 1        
            #include "GlowObjectsCG.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest            
            #pragma vertex vert
            #pragma fragment fragGlow            
            #pragma multi_compile GLOW_MAINTEX GLOW_MAINCOLOR GGLOW_GLOWTEX GLOW_ILLUMTEX GLOW_GLOWCOLOR GLOW_VERTEXCOLOR 
            #pragma multi_compile NO_MULTIPLY MULTIPLY_GLOWCOLOR MULTIPLY_VERT MULTIPLY_VERT_ALPHA MULTIPLY_ILLUMTEX_ALPHA MULTIPLY_MAINTEX_ALPHA
            #pragma multi_compile _ HIGH_QUALITY_GLOW
        ENDCG    
    }
}

Subshader {
    Tags { "RenderType"="Opaque" }
    Pass {
        Lighting Off Fog { Mode off } 
    }
}   

Subshader {
    Tags { "RenderType"="TreeOpaque" }
    Pass {
        Lighting Off Fog { Mode off } 
    }
}  
 
Subshader {
    Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
    Pass {
    	Cull Off
    	Lighting Off Fog { Mode off } 
    	AlphaTest Greater [_Cutoff]
        SetTexture [_MainTex] {
            constantColor (0,0,0,0)
            combine constant, texture
        }
    }    
}   

Subshader {
    Tags { "RenderType"="TreeTransparentCutout" "Queue"="AlphaTest"}
    Pass {
    	Cull Off
    	Lighting Off Fog { Mode off } 
    	AlphaTest Greater [_Cutoff]
        SetTexture [_MainTex] {
            constantColor (0,0,0,0)
            combine constant, texture
        }
    }    
}   


Subshader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent"}
    Pass {
    	Lighting Off Fog { Mode off } ZWrite Off
    	Blend SrcAlpha OneMinusSrcAlpha
    	Color [_Color]
        SetTexture [_MainTex] {
            constantColor (0,0,0,0)
            combine constant, previous * texture
        }
    }    
}   


 
Fallback off
   
}