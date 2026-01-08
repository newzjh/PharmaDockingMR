Shader "Custom/Standard_VertexColor" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        // 其他属性保持不变...
    }

    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        CGPROGRAM
        // 保留原有编译指令，新增顶点色相关定义
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;

        // 定义输入结构体，新增顶点色变量
        struct Input {
            float2 uv_MainTex;
            float4 color : COLOR; // 接收顶点色
        };

        // 顶点函数（可选，如需在顶点阶段处理顶点色）
        void vert (inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // 如需顶点阶段修改顶点色，可在此处理
        }

        // 表面着色函数：将顶点色叠加到主颜色
        void surf (Input IN, inout SurfaceOutputStandard o) {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb * IN.color.rgb; // 顶点色叠加到漫反射
            o.Alpha = c.a * IN.color.a; // 顶点色透明度叠加（可选）
        }
        ENDCG
    }
    FallBack "Diffuse"
}