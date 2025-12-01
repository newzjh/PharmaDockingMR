using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

[CustomEditor(typeof(GIF))]
public class GIFInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Import GIF"))
        {
            GIF gif = target as GIF;
            gif.textures.Clear();

            string path = EditorUtility.OpenFilePanel("import gif", null, "gif");
            if (path == null || path.Length <= 0) return;

            string filename = Path.GetFileNameWithoutExtension(path);

            // 获取图片对象
            Bitmap imgGif = new Bitmap(path);
            // 先判断图片是否是动画图片（gif）
            if (ImageAnimator.CanAnimate(imgGif))
            {
                FrameDimension imgFrmDim = new FrameDimension(imgGif.FrameDimensionsList[0]);
                // 获取帧数
                int nFdCount = imgGif.GetFrameCount(imgFrmDim);
                for (int i = 0; i < nFdCount; i++)
                {
                    // 把每一帧保存为jpg图片
                    imgGif.SelectActiveFrame(imgFrmDim, i);
                    string subpath = Application.dataPath + "/ui/gif/" + filename + "_" + i.ToString() + ".jpg";
                    imgGif.Save(subpath);
                }
                AssetDatabase.Refresh();
                for (int i = 0; i < nFdCount; i++)
                {
                    string subpath = "Assets/ui/gif/" + filename + "_" + i.ToString() + ".jpg";
                    Texture frameTexture = AssetDatabase.LoadAssetAtPath<Texture>(subpath);
                    gif.textures.Add(frameTexture);
                }
                if (nFdCount > 0)
                {
                    UnityEngine.UI.RawImage rawimagecomponent = gif.GetComponent<UnityEngine.UI.RawImage>();
                    if (rawimagecomponent != null)
                    {
                        rawimagecomponent.texture = gif.textures[0];
                    }
                }
            }
        }
    }
}
