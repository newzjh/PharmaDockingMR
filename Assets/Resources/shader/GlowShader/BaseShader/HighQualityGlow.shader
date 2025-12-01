// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "ImageEffect/Glow/HighQualityGlow" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_GlowTex0 ("Bloom0 (RGB)", 2D) = "black" {}
		_GlowTex1 ("Bloom1 (RGB)", 2D) = "black" {}
		_GlowTex2 ("Bloom2 (RGB)", 2D) = "black" {}
		_GlowTex3 ("Bloom3 (RGB)", 2D) = "black" {}
		_GlowTex4 ("Bloom4 (RGB)", 2D) = "black" {}
		_GlowTex5 ("Bloom5 (RGB)", 2D) = "black" {}
	}
	
	
	CGINCLUDE
		#include "UnityCG.cginc"
		#pragma target 3.0

		sampler2D _MainTex;
		sampler2D _GlowTex0;
		sampler2D _GlowTex1;
		sampler2D _GlowTex2;
		sampler2D _GlowTex3;
		sampler2D _GlowTex4;
		sampler2D _GlowTex5;
		
		uniform half4 _MainTex_TexelSize;
		
		uniform float _BlurSize;
		uniform float _GlowIntensity;
		
		struct v2f_simple 
		{
			float4 pos : SV_POSITION; 
			half4 uv : TEXCOORD0;

        #if UNITY_UV_STARTS_AT_TOP
			half4 uv2 : TEXCOORD1;
		#endif
		};	
		 
		v2f_simple vertGlow ( appdata_img v )
		{
			v2f_simple o;
			
			o.pos = UnityObjectToClipPos (v.vertex);
        	o.uv = float4(v.texcoord.xy, 1, 1);		
        	
        	#if UNITY_UV_STARTS_AT_TOP
        		o.uv2 = float4(v.texcoord.xy, 1, 1);				
        		if (_MainTex_TexelSize.y < 0.0)
        			o.uv.y = 1.0 - o.uv.y;
        	#endif
        	        	
			return o; 
		}
		
		fixed3 Fixed3(float x)
		{
			return fixed3(x, x, x);
		}
		
		fixed4 fragGlow ( v2f_simple i ) : COLOR
		{	
			fixed4 color = tex2D(_MainTex, i.uv.xy);
        	#if UNITY_UV_STARTS_AT_TOP
				float2 coord = i.uv2.xy;
			#else
				float2 coord = i.uv.xy;
			#endif
			
			fixed3 b0 = tex2D(_GlowTex0, coord).rgb;
			fixed3 b1 = tex2D(_GlowTex1, coord).rgb;
			fixed3 b2 = tex2D(_GlowTex2, coord).rgb;
			fixed3 b3 = tex2D(_GlowTex3, coord).rgb;
			fixed3 b4 = tex2D(_GlowTex4, coord).rgb;
			fixed3 b5 = tex2D(_GlowTex5, coord).rgb;
			
			//权重公式
			fixed3 glow = b0 * 0.5f
						 + b1 * 0.8f * 0.75f
						 + b2 * 0.6f
						 + b3 * 0.45f 
						 + b4 * 0.35f
						 + b5 * 0.23f;
						 ;
			
			glow /= 2.2;
			
			color.rgb = lerp(color.rgb, glow.rgb, Fixed3(_GlowIntensity));
			//color.rgb = glow;
			return color;
		} 
		
		
		struct v2f_tap
		{
			float4 pos : SV_POSITION;
			half4 uv20 : TEXCOORD0;
			half4 uv21 : TEXCOORD1;
			half4 uv22 : TEXCOORD2;
			half4 uv23 : TEXCOORD3;
		};
		
		v2f_tap vert4Tap ( appdata_img v )
		{
			v2f_tap o;

			half2 offset = _MainTex_TexelSize.xy;
			o.pos = UnityObjectToClipPos (v.vertex);
        	o.uv20 = half4(v.texcoord.xy + offset, 0.0, 0.0);				
			o.uv21 = half4(v.texcoord.xy + offset * half2(-0.5h,-0.5h), 0.0, 0.0);	
			o.uv22 = half4(v.texcoord.xy + offset * half2(0.5h,-0.5h), 0.0, 0.0);		
			o.uv23 = half4(v.texcoord.xy + offset * half2(-0.5h,0.5h), 0.0, 0.0);			
  
			return o; 
		}		
		
		fixed4 fragDownsample ( v2f_tap i ) : COLOR
		{				
			fixed4 color = tex2D (_MainTex, i.uv20.xy);
			color += tex2D (_MainTex, i.uv21.xy);
			color += tex2D (_MainTex, i.uv22.xy);
			color += tex2D (_MainTex, i.uv23.xy);
			return max(color/4, 0);
		}
		
		//blur权重
		static const half curve[7] = { 0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205 };

		static const half4 curve4[7] = { half4(0.0205,0.0205,0.0205,0), 
										 half4(0.0855,0.0855,0.0855,0), 
										 half4(0.232,0.232,0.232,0),
										 half4(0.324,0.324,0.324,1), 
										 half4(0.232,0.232,0.232,0), 
										 half4(0.0855,0.0855,0.0855,0), 
										 half4(0.0205,0.0205,0.0205,0) };
										 
		
		struct v2f_withBlurCoords8 
		{
			float4 pos : SV_POSITION;
			half4 uv : TEXCOORD0;
			half4 offs : TEXCOORD1;
		};		
		
		v2f_withBlurCoords8 vertBlurHorizontal (appdata_img v)
		{
			v2f_withBlurCoords8 o;
			o.pos = UnityObjectToClipPos (v.vertex);
			
			o.uv = half4(v.texcoord.xy,1,1);
			o.offs = half4(_MainTex_TexelSize.xy * half2(1.0, 0.0) * _BlurSize,1,1);

			return o; 
		}
		
		v2f_withBlurCoords8 vertBlurVertical (appdata_img v)
		{
			v2f_withBlurCoords8 o;
			o.pos = UnityObjectToClipPos (v.vertex);
			
			o.uv = half4(v.texcoord.xy,1,1);
			o.offs = half4(_MainTex_TexelSize.xy * half2(0.0, 1.0) * _BlurSize,1,1);
			 
			return o; 
		}	

		half4 fragBlur8 ( v2f_withBlurCoords8 i ) : COLOR
		{
			half2 uv = i.uv.xy; 
			half2 netFilterWidth = i.offs.xy;  
			half2 coords = uv - netFilterWidth * 3.0;  
			
			half4 color = 0;
  			for( int l = 0; l < 7; l++ )  
  			{   
				half4 tap = tex2D(_MainTex, coords);
				color += tap * curve4[l];
				coords += netFilterWidth;
  			}
			return color;
		}
		
	ENDCG

	SubShader 
	{
		ZTest Off Cull Off ZWrite Off Blend Off
		Fog {Mode off}
		
		Pass	//0 main
		{
			Blend One One
			CGPROGRAM
			#pragma vertex vertGlow
			#pragma fragment fragGlow
			#pragma fragmentoption ARB_precision_hint_fastest 
			ENDCG
		}
		
		Pass 	//1 Downsample
		{ 	
			CGPROGRAM			
			#pragma vertex vert4Tap
			#pragma fragment fragDownsample
			#pragma fragmentoption ARB_precision_hint_fastest 			
			ENDCG		 
		}
		
		Pass 	//2 垂直blur
		{ 	
			CGPROGRAM			
			#pragma vertex vertBlurVertical
			#pragma fragment fragBlur8
			#pragma fragmentoption ARB_precision_hint_fastest 			
			ENDCG		 
		}
		
		Pass 	//2 水平blur
		{ 	
			CGPROGRAM			
			#pragma vertex vertBlurHorizontal
			#pragma fragment fragBlur8
			#pragma fragmentoption ARB_precision_hint_fastest 			
			ENDCG		 
		}
	} 
	FallBack Off
}
