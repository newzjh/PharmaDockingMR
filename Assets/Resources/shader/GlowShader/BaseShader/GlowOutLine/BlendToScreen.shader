// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "ImageEffect/Glow/GlowOutLine/BlendToScreen" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
}
       
        CGINCLUDE
       
        #include "UnityCG.cginc"
        struct v2f {
            half4 pos : POSITION;
            half2 uv1 : TEXCOORD0;
			half2 uv2 : TEXCOORD1;
        };
       
        sampler2D _DstTex;
        sampler2D _SrcTex;
		sampler2D _MainTex;
		float4 _MainTex_ST;
		half4 _MainTex_TexelSize;

        v2f vert( appdata_base v )
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv1 = v.texcoord.xy;
			o.uv2 = v.texcoord.xy;
		#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0.0)
			o.uv2.y = 1.0 - o.uv2.y;
		#endif

            return o;
        }
       
        fixed4 frag(v2f pixelData) : COLOR
        {
            fixed4 srcColor = tex2D(_SrcTex, pixelData.uv2) ;
            fixed4 dstColor = tex2D(_DstTex, pixelData.uv2);
			fixed4 mainColor = tex2D(_MainTex, pixelData.uv1);

            //只要光圈，减去原来的颜色就行了
            return clamp(dstColor - srcColor * 100,0,1)+mainColor;
        }
       
       
        ENDCG
       
    Subshader {
        Pass {
            // Additive
            Name "Add"
            Blend Off
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