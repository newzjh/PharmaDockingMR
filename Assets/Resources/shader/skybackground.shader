// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Skybox/Background" { 
Properties {  
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB) Gloss (A)", 2D) = "white" {}
		_ScaleX ("ScaleX", Float) = 1.0
		_ScaleY ("ScaleY", Float) = 1.0
}  
  
SubShader {  
    Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }  
    Cull Off ZWrite Off  
  
    Pass {  
          
        CGPROGRAM  
        #pragma vertex vert  
        #pragma fragment frag  
  
        #include "UnityCG.cginc"  
          
        struct appdata_t {  
            float4 vertex : POSITION;  
        };  
  
        struct v2f {  
            float4 vertex : SV_POSITION;  
            float2 texcoord : TEXCOORD0;  
        };  

		sampler2D _MainTex;
		float _ScaleX;
		float _ScaleY;
  
        v2f vert (appdata_t v)  
        {  
            v2f o;  
            o.vertex = UnityObjectToClipPos(v.vertex);  
			float2 uv=(o.vertex.xy/o.vertex.w);
			uv.x*=-_ScaleX;
			uv.y*=_ScaleY;
		#if UNITY_UV_STARTS_AT_TOP
            uv.y = -uv.y;  
			#endif 
			o.texcoord=uv*0.5+0.5;
            return o;  
        }  


  
        fixed4 frag (v2f i) : SV_Target  
        {  
			fixed4 tex = tex2D(_MainTex, i.texcoord);
            return tex;  
        }  
        ENDCG   
    }  
}     
  
  
Fallback Off  
  
}  