// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "ImageEffect/Glow/GlowOutLine/BlendToScreen_Simple" {
Properties {
}
       
        CGINCLUDE
       
        #include "UnityCG.cginc"
        struct v2f {
            half4 pos : POSITION;
            half2 uv : TEXCOORD0;
        };
       
        sampler2D _DstTex;
        sampler2D _SrcTex;

        v2f vert( appdata_base v )
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord.xy;

            return o;
        }
       
        fixed4 frag(v2f pixelData) : COLOR
        {
            fixed4 srcColor = tex2D(_SrcTex, pixelData.uv) ;
            fixed4 dstColor = tex2D(_DstTex, pixelData.uv);

            //只要光圈，减去原来的颜色就行了
            return clamp(dstColor - srcColor * 100,0,1);
        }
       
       
        ENDCG
       
    Subshader {
        Pass {
            // Additive
            Name "Add"
            Blend One One
            ZTest Always Cull Off ZWrite Off Fog { Mode Off }
            ColorMask RGB
            
            CGPROGRAM
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        
        //Pass {
        //    // Screen
        //    Name "Screen"
        //    Blend One OneMinusSrcColor          
        //    ZTest Always Cull Off ZWrite Off Fog { Mode Off }
        //    ColorMask RGB
            
        //   CGPROGRAM
        //    #pragma fragmentoption ARB_precision_hint_fastest
        //    #pragma vertex vert
        //    #pragma fragment frag
        //     ENDCG
       // }      
    }
    
    Fallback off
}