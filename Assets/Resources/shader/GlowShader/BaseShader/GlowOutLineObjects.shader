// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "Effect/Glow/GlowOutLineObjects" {
Properties
 {
    _Color ("Color[_Color]", Color) = (1,1,1,1)
    _ColorStrength("ColorStrength[_ColorStrength]", Float) = 0.05
    _Cutoff ("Alpha cutoff", Range (0,1)) = 0.01               
}
Subshader {
    Tags { "GlowOutLine"="true"}

    Pass {

        LOD 200
        Blend One One
        Cull Back Lighting Off
        ZWrite Off
        //AlphaTest Greater [_Cutoff]
        
        CGPROGRAM
            #pragma fragmentoption ARB_precision_hint_fastest

            #pragma vertex vert_GlowOutLine
            #pragma fragment frag_GlowOutLine
            fixed4 _Color;
            float _ColorStrength;

            struct appdata_GlowOutLine
            {
                float4 vertex : POSITION;
                float3 normal:NORMAL;
                fixed4 color :COLOR;

            };

            struct v2f_GlowOutLine
            {
                float4 pos : POSITION;
                fixed4 color : COLOR0;
            };


            v2f_GlowOutLine vert_GlowOutLine( appdata_GlowOutLine v )
            {
                v2f_GlowOutLine o = (v2f_GlowOutLine)0;

                o.pos=UnityObjectToClipPos(v.vertex);
                o.color = v.color;

                return o;
            }  

            fixed4 frag_GlowOutLine( v2f_GlowOutLine v ) : COLOR
            {
                return v.color * _Color * _ColorStrength ;
            }  


        ENDCG  
    }
}
Fallback off
   
}