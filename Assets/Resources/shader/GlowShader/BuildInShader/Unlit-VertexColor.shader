// Unlit shader. Simplest possible textured shader.
// - no lighting
// - no lightmap support
// - no per-material color

Shader "Effect/Glow/Vertex Color" {
Properties {
    _GlowTex ("Glow", 2D) = "" {}
    _GlowColor ("Glow Color", Color)  = (1,1,1,1)
    _GlowStrength ("Glow Strength", Float) = 1.0
}

SubShader {
    Tags { "RenderEffect"="Glow" "RenderType"="Glow" }
    LOD 100
    
    Lighting Off
    ColorMaterial AmbientAndDiffuse
    Pass {}
}
CustomEditor "GlowMatInspector"
}
