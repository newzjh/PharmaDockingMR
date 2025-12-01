// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "ImageEffect/GlowOutlineMaskEx" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
    _ColorStrength("ColorStrength[_ColorStrength]", Float) = 0.05     
	_Color ("Color[_Color]", Color) = (1,1,1,1)
	_Cutoff ("Alpha cutoff", Range (0,1)) = 0.01
	_CutOut ("CutOut (A)[_CutOut]", 2D) = "white" {}
}

SubShader {
	
	Pass {  

		LOD 200
        Blend Off
        Cull Back Lighting Off
        ZWrite Off
		AlphaTest Greater [_Cutoff]

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord0 : TEXCOORD0;
				half2 texcoord1 : TEXCOORD1;
			};

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;

			sampler2D _CutOut;
			float4 _CutOut_ST;

			uniform float _ColorStrength;
			uniform float4 _Color;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord0 = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.texcoord1 = TRANSFORM_TEX(v.texcoord, _CutOut);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float4 img = tex2D(_MainTex, i.texcoord0);
				float4 cut = tex2D(_CutOut, i.texcoord1);
				float4 col = _Color*_ColorStrength; 
				return float4(col.rgb,img.a*cut.r);
				return col;
			}

		ENDCG
	}


	Pass {  

		LOD 200
        Blend Off
        Cull Back Lighting Off
        ZWrite Off


		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord0 : TEXCOORD0;
			};

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord0 = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float4 img = tex2D(_MainTex, i.texcoord0);
				float val=img.r*100+img.g*100+img.b*100;
				return float4(val,val,val,1);
			}

		ENDCG
	}

	Pass {  

        Blend Off
        Cull Back Lighting Off
        ZWrite Off


		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord0 : TEXCOORD0;
			};

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;

			uniform sampler2D_float _CameraDepthTexture;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord0 = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			
			float4 frag (v2f i) : SV_Target
			{
				float3 lumfac=float3(0.299f,0.587f,0.114f);	

				float4 col = tex2D(_MainTex, i.texcoord0);
				if (dot(col.rgb,lumfac)>0.01)return col;

				for(int y=-1;y<=1;y++)
				{
					for(int x=-1;x<=1;x++)
					{
						float2 uv=i.texcoord0 + _MainTex_TexelSize * float2(x,y) ;
						float4 curcol = tex2D(_MainTex, uv);
						float lum1=dot(col.rgb,lumfac);
						float lum2=dot(curcol.rgb,lumfac);
						if (lum2>lum1)
						{
							col=curcol;
						}
					}
				}

				return col;
			}

		ENDCG
	}

	Pass {  

        Blend Off
        Cull Back Lighting Off
        ZWrite Off


		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord0 : TEXCOORD0;
			};

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float4 _MainTex_TexelSize;

			uniform sampler2D_float _CameraDepthTexture;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord0 = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			
			float4 frag (v2f i) : SV_Target
			{
				float4 col = tex2D(_MainTex, i.texcoord0);
				float dep =SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.texcoord0);
				dep=Linear01Depth(dep);
				return float4(col.rgb,dep);
			}

		ENDCG
	}
}

}
