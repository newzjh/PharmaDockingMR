// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "Effect/Glow/GlowDissloveAlphaObjects" {

    Properties
     {
    _TintColor ("Tint Color[_TintColor]", Color) = (1,1,1,1)
    _MainTex ("Main Texture[_MainTex]", 2D) = "white" {}
    _DissolveMap ("Dissolve Map (R)[_DissolveMap]", 2D) = "white" {}
    _DissolvePower("DissolvePower[_DissolvePower]",Range(0,0.99)) = 0
    _ColorStrength("ColorStrength[_ColorStrength]",Float) = 1
    _DissolveColor("DissolveColor[_DissolveColor]",Color) = (1,1,1,1)
    _DissolveColorStrength("DissolveColorStrength[_DissolveColorStrength]",Float) = 1
    }


Subshader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" "DissolveGolw"="TransparentDissolveGolw"}
    Pass {    
        Cull Off Lighting Off ZWrite Off ZTest LEqual Fog { Mode off } 
        Blend SrcAlpha OneMinusSrcAlpha
        CGPROGRAM
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma vertex vert_Disslove
            #pragma fragment frag_Disslove
            #include "UnityCG.cginc"

            struct appdata_color
             {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
             {
                float4 pos : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct PixelOutput
            {
                float4 color : COLOR0;
            };


            uniform sampler2D _DissolveMap;
            uniform fixed4 _DissolveColor;
            uniform half _DissolveColorStrength; 
            uniform half _DissolvePower;
            uniform sampler2D _MainTex;
            float4 _MainTex_ST;
            
            fixed4 DisslovedColor(float2 uv)
            {
                fixed4 mainColor = tex2D(_MainTex, uv);
                fixed4 dissolveColor = tex2D(_DissolveMap,  uv);
                fixed grayscale = dissolveColor.r ;

                grayscale = grayscale.r - _DissolvePower;
                fixed4 col = fixed4(0,0,0,0);
                if(grayscale < 0 && grayscale > -0.02)
                {
                    float s = grayscale / -0.02;
                    col = _DissolveColor * s + mainColor * (1 - s);
                    col =  col * _DissolveColorStrength *  mainColor.a ;
                    //col =  _DissolveColor * _DissolveColorStrength *  mainColor.a ;
                }
                else if(grayscale < -0.02)
                {
                    clip(-0.1);
                    col = fixed4(0,0,0,0);
                }
                return col; 
            }

            v2f vert_Disslove( appdata_color v )
            {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex).xy; 
                return o;
            }  

            PixelOutput frag_Disslove(v2f pixelData)
            {
                PixelOutput o = (PixelOutput)0;
                o.color = DisslovedColor(pixelData.uv);

                return o;
            }

        ENDCG            
    }
}

Fallback off
   
}