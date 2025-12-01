#define WeakCacheForm

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class MaterialLib 
{
    public static Color GetRandomColor(bool withalpha=false)
    {
        Color c=new Color();
        c.r = UnityEngine.Random.Range(0.0f,1.0f);
        c.g = UnityEngine.Random.Range(0.0f, 1.0f);
        c.b = UnityEngine.Random.Range(0.0f, 1.0f);
        if (withalpha)
        {
            c.a = UnityEngine.Random.Range(0.0f, 1.0f);
        }
        else
        {
            c.a = 1.0f;
        }
        return c;
    }

    private static Shader standardshader = null;
    public static Shader GetStandardShader()
    {
        if (standardshader == null) standardshader = Shader.Find("Scene/Standard");
        return standardshader;
    }

    private static Shader alphashader = null;
    public static Shader GetAlphaShader()
    {
        if (alphashader == null) alphashader = Shader.Find("Surface/Alpha Blended");
        return alphashader;
    }

    private static Shader wireframeshader = null;
    public static Shader GetWireframeShader()
    {
        if (wireframeshader == null) wireframeshader = Shader.Find("UCLA Game Lab/Wireframe/Double-Sided");
        return wireframeshader;
    }

    private static Material wireframemat = null;
    public static Material GetWireframeMaterial()
    {
        if (wireframemat==null)
        {
            Shader s = GetWireframeShader();
            wireframemat = new Material(s);
            wireframemat.name = "Mat_Wireframe_";
        }
        return wireframemat;
    }

#if WeakCacheForm
    private static Dictionary<uint, WeakReference> alphastandardmaterials = new Dictionary<uint, WeakReference>();
    public static Material GetAlphaMaterialByColor(Color c)
    {
        uint r = (uint)Mathf.Clamp(c.r * 255.0f, 0.0f, 255.0f);
        uint g = (uint)Mathf.Clamp(c.g * 255.0f, 0.0f, 255.0f);
        uint b = (uint)Mathf.Clamp(c.b * 255.0f, 0.0f, 255.0f);
        uint a = (uint)Mathf.Clamp(c.a * 255.0f, 0.0f, 255.0f);
        uint cc = (a << 24) + (r << 16) + (g << 8) + b;
        Material mat = null;
        if (alphastandardmaterials.ContainsKey(cc))
        {
            WeakReference wr=alphastandardmaterials[cc];
            if (wr.IsAlive)
            {
                mat = alphastandardmaterials[cc].Target as Material;
            }
        }
        if (mat == null)
        {
            Shader s = GetAlphaShader();
            mat = new Material(s);
            mat.name = "Mat_Alpha_" + cc.ToString();
            mat.color = c;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 1);
            mat.SetInt("_IntensityVC", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_VERTEXCOLOR_LERP");
            mat.EnableKeyword("_VERTEXCOLOR");
            mat.DisableKeyword("_VERTEXCOLOR_OFF");
            mat.renderQueue = 3000;
            if (alphastandardmaterials.ContainsKey(cc))
                alphastandardmaterials[cc].Target = mat;
            else
                alphastandardmaterials[cc] = new WeakReference(mat);
        }
        return mat;
    }

    private static Dictionary<uint, WeakReference> standardmaterials = new Dictionary<uint, WeakReference>();
    public static Material GetStandardMaterialByColor(Color c)
    {
        uint r = (uint)Mathf.Clamp(c.r * 255.0f, 0.0f, 255.0f);
        uint g = (uint)Mathf.Clamp(c.g * 255.0f, 0.0f, 255.0f);
        uint b = (uint)Mathf.Clamp(c.b * 255.0f, 0.0f, 255.0f);
        uint a = (uint)Mathf.Clamp(c.a * 255.0f, 0.0f, 255.0f);
        uint cc = (a << 24) + (r << 16) + (g << 8) + b;
        Material mat = null;
        if (standardmaterials.ContainsKey(cc))
        {
            WeakReference wr = standardmaterials[cc];
            if (wr.IsAlive)
            {
                mat = standardmaterials[cc].Target as Material;
            }
        }
        if (mat == null)
        {
            Shader s = GetStandardShader();
            mat = new Material(s);
            mat.name = "Mat_Standard_" + cc.ToString();
            mat.color = c;
            mat.DisableKeyword("_VERTEXCOLOR_LERP");
            mat.EnableKeyword("_VERTEXCOLOR");
            if (standardmaterials.ContainsKey(cc))
                standardmaterials[cc].Target = mat;
            else
                standardmaterials[cc] = new WeakReference(mat);
        }
        return mat;
    }
#else
    private static Dictionary<uint, Material> alphastandardmaterials = new Dictionary<uint, Material>();
    public static Material GetAlphaMaterialByColor(Color c)
    {
        uint r = (uint)Mathf.Clamp(c.r * 255.0f, 0.0f, 255.0f);
        uint g = (uint)Mathf.Clamp(c.g * 255.0f, 0.0f, 255.0f);
        uint b = (uint)Mathf.Clamp(c.b * 255.0f, 0.0f, 255.0f);
        uint a = (uint)Mathf.Clamp(c.a * 255.0f, 0.0f, 255.0f);
        uint cc = (a << 24) + (r << 16) + (g << 8) + b;
        Material mat = null;
        if (alphastandardmaterials.ContainsKey(cc))
        {
            mat = alphastandardmaterials[cc];
        }
        if (mat == null)
        {
            Shader s = GetAlphaShader();
            mat = new Material(s);
            mat.name = "Mat_Alpha_" + cc.ToString();
            mat.color = c;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 1);
            mat.SetInt("_IntensityVC", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_VERTEXCOLOR_LERP");
            mat.EnableKeyword("_VERTEXCOLOR");
            mat.DisableKeyword("_VERTEXCOLOR_OFF");
            mat.renderQueue = 3000;
            alphastandardmaterials[cc] = mat;
        }
        return mat;
    }

    private static Dictionary<uint, Material> standardmaterials = new Dictionary<uint, Material>();
    public static Material GetStandardMaterialByColor(Color c)
    {
        uint r = (uint)Mathf.Clamp(c.r * 255.0f, 0.0f, 255.0f);
        uint g = (uint)Mathf.Clamp(c.g * 255.0f, 0.0f, 255.0f);
        uint b = (uint)Mathf.Clamp(c.b * 255.0f, 0.0f, 255.0f);
        uint a = (uint)Mathf.Clamp(c.a * 255.0f, 0.0f, 255.0f);
        uint cc = (a << 24) + (r << 16) + (g << 8) + b;
        Material mat = null;
        if (standardmaterials.ContainsKey(cc))
        {
            mat = standardmaterials[cc];
        }
        if (mat == null)
        {
            Shader s = GetStandardShader();
            mat = new Material(s);
            mat.name = "Mat_Standard_" + cc.ToString();
            mat.color = c;
            mat.DisableKeyword("_VERTEXCOLOR_LERP");
            mat.EnableKeyword("_VERTEXCOLOR");
            standardmaterials[cc] = mat;
        }
        return mat;
    }
#endif
}
