// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

#include "UnityCG.cginc"

struct appdata_color {
    float4 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
    
    #if GLOW_VERTEXCOLOR || MULTIPLY_VERT || MULTIPLY_VERT_ALPHA
        float4 color : COLOR;
    #endif
};
            
 
struct v2f {
    float4 pos : POSITION;
    
    
    float4 color : COLOR;
    
    #if GLOW_MAINTEX || ALPHA || MULTIPLY_MAINTEX_ALPHA || GLOW_DISSLOVE
        float2 uv : TEXCOORD0;
    #endif

    #if GLOW_GLOWTEX
        float2 uvGlow : TEXCOORD1;
    #endif
    
    #if GLOW_ILLUMTEX || MULTIPLY_ILLUMTEX_ALPHA
        float2 uvIllum : TEXCOORD2;
    #endif
};

struct PixelOutput {
    float4 color : COLOR0;
};

#if GLOW_MAINTEX || ALPHA || MULTIPLY_MAINTEX_ALPHA || GLOW_DISSLOVE
    sampler2D _MainTex;
    float4 _MainTex_ST;
#endif
    
#if GLOW_GLOWTEX
    sampler2D _GlowTex;
    float4 _GlowTex_ST;
#endif        

#if GLOW_ILLUMTEX || MULTIPLY_ILLUMTEX_ALPHA
sampler2D _Illum;
float4 _Illum_ST;
#endif        

float _GlowStrength;

#if GLOW_GLOWCOLOR || MULTIPLY_GLOWCOLOR
fixed4 _GlowColor;
#endif

#if GLOW_MAINCOLOR ||  GLOW_OUTLINE
fixed4 _Color;
#endif



v2f vert( appdata_color v )
{
    v2f o = (v2f)0;
    o.pos = UnityObjectToClipPos(v.vertex);
    
    #if GLOW_MAINTEX || ALPHA
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex).xy;
    #endif            
        
    #if GLOW_GLOWTEX
           o.uvGlow = TRANSFORM_TEX(v.texcoord, _GlowTex).xy;
    #endif
    
    #if GLOW_ILLUMTEX || MULTIPLY_ILLUMTEX_ALPHA
        o.uvIllum = TRANSFORM_TEX(v.texcoord, _Illum).xy;
    #endif
    
    #if GLOW_VERTEXCOLOR || MULTIPLY_VERT || MULTIPLY_VERT_ALPHA
           o.color = v.color;
    #endif
                      
    return o;
}    
             
PixelOutput fragGlow(v2f pixelData)
{
    PixelOutput o = (PixelOutput)0;

    #if GLOW_MAINTEX //对mainTex做Glow
        o.color = tex2D(_MainTex, pixelData.uv);
    #elif GLOW_MAINCOLOR //对miancolor做Glow
        o.color = _Color;
    #elif GLOW_GLOWTEX  //对GlowTex做Glow
        o.color = tex2D(_GlowTex, pixelData.uvGlow);
    #elif GLOW_ILLUMTEX //对Illum做Glow
        o.color = tex2D(_Illum, pixelData.uvIllum);
    #elif GLOW_GLOWCOLOR //对GlowColor做Glow
        o.color = _GlowColor;
    #elif GLOW_VERTEXCOLOR //对顶点色做Glow
        o.color = pixelData.color;
    #else 
        o.color = float4(1,0,1,1);
    #endif
    
    #if ALPHA && GLOW_MAINTEX
        fixed alpha = o.color.a;
    #elif ALPHA
        fixed alpha = tex2D(_MainTex, pixelData.uv).a;
    #endif
    
    #if HIGH_QUALITY_GLOW
        o.color *= step(0.001,_GlowStrength) * exp(_GlowStrength * 5.0f);
    #else
        o.color *= _GlowStrength;
    #endif


    #if MULTIPLY_GLOWCOLOR
        o.color *= _GlowColor;
    #elif MULTIPLY_VERT
        o.color *= pixelData.color;
    #elif MULTIPLY_VERT_ALPHA
        o.color *= pixelData.color.a;
    #elif MULTIPLY_ILLUMTEX_ALPHA
        #if GLOW_ILLUMTEX
            o.color *= o.color.a;
        #else
            o.color *= UNITY_SAMPLE_1CHANNEL(_Illum, pixelData.uvIllum);
        #endif
    #elif MULTIPLY_MAINTEX_ALPHA
        #if GLOW_MAINTEX
            o.color *= o.color.a;
        #elif ALPHA
            o.color *= alpha;            
        #else
            o.color *= UNITY_SAMPLE_1CHANNEL(_MainTex, pixelData.uv);
        #endif
    #endif
    
    #if ALPHA
        o.color.a = alpha;
    #endif
    return o;
}

