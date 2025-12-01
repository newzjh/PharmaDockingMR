// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "ImageEffect/Glow/GlowOutLine/BlendToScreenEx" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
}
       
        CGINCLUDE
       
        #include "UnityCG.cginc"
        struct v2f {
            half4 pos : POSITION;
            half2 uv : TEXCOORD0;
        };
       
        sampler2D _DstTex;
        sampler2D _SrcTex;
        sampler2D _ExpandTex;
		sampler2D _MainTex;

		uniform sampler2D_float _CameraDepthTexture;

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

			fixed4 mainColor = tex2D(_MainTex, pixelData.uv);

			fixed4 expandColor = tex2D(_ExpandTex, pixelData.uv);
			float3 lumfac=float3(0.299f,0.587f,0.114f);	
			float expandlum=dot(expandColor,lumfac);

            //只要光圈，减去原来的颜色就行了
			float4 ret=clamp(dstColor - srcColor * 100,0,1);
			float alpha=ret.g*1;

			float depth=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,pixelData.uv);
			depth=Linear01Depth(depth);
			float vis=clamp((depth-expandColor.a)*1000,0,1);
			//vis=expandColor.a*10;
			//vis=depth*10;
			alpha*=vis;

			float4 final;
			float fa=alpha;
			float fb=lerp(1-alpha,1,expandlum);
			final=expandColor*fa+mainColor*fb;
			final.a=1;
            return final;
        }
       
       
        ENDCG
       
    Subshader {
        Pass {
            // Additive
            Name "Add"
            ZTest Always Cull Off ZWrite Off Fog { Mode Off }
			Blend Off
            //ColorMask RGB
            
            CGPROGRAM
            //#pragma fragmentoption ARB_precision_hint_fastest
			#pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        

    }
    
    Fallback off
}