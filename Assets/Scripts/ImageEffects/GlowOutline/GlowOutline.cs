using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImageEffects
{

    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class GlowOutline : MonoBehaviour
    {
        protected bool supportHDRTextures = true;
        protected bool supportDX11 = false;
        protected bool isSupported = true;
        public GlowSettings settings = new GlowSettings();


        private Shader shader;
        private Shader BlendShader;
        private Material mat = null;
        private Material BlendToScreen = null;
        private IBlur blur = null;
        private Shader glowshader;
        private Material glowmat;
             

        public List<Renderer> selectRenders = new List<Renderer>();

        protected Material CheckShaderAndCreateMaterial(Shader s, Material m2Create)
        {
            if (!s)
            {
                Debug.Log("Missing shader in " + ToString());
                enabled = false;
                return null;
            }

            if (s.isSupported && m2Create && m2Create.shader == s)
                return m2Create;

            if (!s.isSupported)
            {
                NotSupported();
                Debug.Log("The shader " + s.ToString() + " on effect " + ToString() + " is not supported on this platform!");
                return null;
            }
            else
            {
                m2Create = new Material(s);
                m2Create.hideFlags = HideFlags.DontSave;
                if (m2Create)
                    return m2Create;
                else return null;
            }
        }

        /// <summary>
        /// 处理所有的渲染器
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="depthBuffer"></param>
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (CheckResources() == false)
            {
                Graphics.Blit(source, destination);
                return;
            }

            RenderTexture temp = RenderTexture.GetTemporary(source.width, source.height);
            RenderTexture temp2 = RenderTexture.GetTemporary(source.width, source.height);

            Graphics.SetRenderTarget(temp);
            GL.Clear(false, true, Color.black);

            foreach (Renderer r in selectRenders)
            {
                if (r == null) continue;
                Material mat = r.sharedMaterial;
                if (mat == null) continue;
                if (mat.HasProperty("_Color"))
                    glowmat.color = mat.color;
                else
                    glowmat.color = Color.white;
                CommandBuffer cb = new CommandBuffer();
                cb.DrawRenderer(r, glowmat);
                Graphics.ExecuteCommandBuffer(cb);
            }

            blur.BlurAndBlitBuffer(temp, temp2, settings, false);

            //设置源图和blur好的图，用于剔除中间的
            BlendToScreen.SetTexture("_SrcTex", temp);
            BlendToScreen.SetTexture("_DstTex", temp2);

            Graphics.Blit(source, destination, BlendToScreen);
            RenderTexture.ReleaseTemporary(temp);
            RenderTexture.ReleaseTemporary(temp2);


        }



        public virtual bool CheckResources()
        {

            CheckSupport(true);

            if (!shader)
            {
                shader = Shader.Find("ImageEffect/GlowOutlineMask");
            }
            if (!BlendShader)
            {
                BlendShader = Shader.Find("ImageEffect/Glow/GlowOutLine/BlendToScreen");
            }
            if (!glowshader)
            {
                glowshader = Shader.Find("Effect/Glow/Unlit/Color");
            }
            if (mat == null)
            {
                mat = CheckShaderAndCreateMaterial(shader, mat);
            }
            if (BlendToScreen == null)
            {
                BlendToScreen = CheckShaderAndCreateMaterial(BlendShader, BlendToScreen);
            }
            if (glowmat == null)
            {
                glowmat = CheckShaderAndCreateMaterial(glowshader, glowmat);
            }
            if (blur == null)
            {
                blur = new GlowOutLineBlur();
            }

            if (!isSupported)
                ReportAutoDisable();
            return isSupported;
        }



 

        public bool CheckSupport()
        {
            return CheckSupport(false);
        }

        protected bool CheckSupport(bool needDepth)
        {
            isSupported = true;
            supportHDRTextures = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
            supportDX11 = SystemInfo.graphicsShaderLevel >= 50 && SystemInfo.supportsComputeShaders;

            if (!SystemInfo.supportsImageEffects || !SystemInfo.supportsRenderTextures)
            {
                NotSupported();
                return false;
            }

            if (needDepth && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
            {
                NotSupported();
                return false;
            }

            return true;
        }

        protected bool CheckSupport(bool needDepth, bool needHdr)
        {
            if (!CheckSupport(needDepth))
                return false;

            if (needHdr && !supportHDRTextures)
            {
                NotSupported();
                return false;
            }

            return true;
        }


        protected void ReportAutoDisable()
        {
            Debug.LogWarning("The image effect " + ToString() + " has been disabled as it's not supported on the current platform.");
        }

        protected void NotSupported()
        {
            enabled = false;
            isSupported = false;
            return;
        }


  

    }

}