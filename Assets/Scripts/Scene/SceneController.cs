using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public enum SceneScheme
{ 
    None = -1,
    HouseScene = 0,
    OfficeScene,
    ShowroomScene,
    ArScene,
}

public class SceneController : Singleton<SceneController>
{
    private void Start()
    {
        Invoke("SetStyle", 0.01f);
    }


    private static string[] staticscenenames = new string[] { "HouseScene", "OfficeScene","ShowroomScene", "ArScene" };
    private SceneScheme curscene = SceneScheme.None;

    public void SetStyle()
    {
#if UNITY_WSA
        //SetStyle(SceneScheme.ArScene);
#else
        SetStyle(SceneScheme.HouseScene);
#endif
    }

    public void SetStyle(SceneScheme index)
    {
        if (curscene == index)
            return;

        return;

        CameraScan cs = CameraScan.Instance;
        if (index == SceneScheme.HouseScene)
        {
            foreach (string scenename in staticscenenames)
            {
                Scene s=SceneManager.GetSceneByName(scenename);
                if (s != null && s.isLoaded)
                {
                    SceneManager.UnloadScene(s);
                }
            }
            SceneManager.LoadScene(staticscenenames[0], LoadSceneMode.Additive);
            if (cs)
                cs.Stop();
        }
        else if (index == SceneScheme.OfficeScene)
        {
            foreach (string scenename in staticscenenames)
            {
                Scene s = SceneManager.GetSceneByName(scenename);
                if (s != null && s.isLoaded)
                {
                    SceneManager.UnloadScene(s);
                }
            }
            SceneManager.LoadScene(staticscenenames[1], LoadSceneMode.Additive);
            if (cs)
                cs.Stop();
        }
        else if (index == SceneScheme.ShowroomScene)
        {
            foreach (string scenename in staticscenenames)
            {
                Scene s = SceneManager.GetSceneByName(scenename);
                if (s != null && s.isLoaded)
                {
                    SceneManager.UnloadScene(s);
                }
            }
            SceneManager.LoadScene(staticscenenames[2], LoadSceneMode.Additive);
            if (cs)
                cs.Stop();
        }
        else
        {
            foreach (string scenename in staticscenenames)
            {
                Scene s=SceneManager.GetSceneByName(scenename);
                if (s != null && s.isLoaded)
                {
                    SceneManager.UnloadScene(s);
                }
            }
            SceneManager.LoadScene(staticscenenames[3], LoadSceneMode.Additive);
            if (cs)
                cs.Stop();
        }

        curscene = index;
    }

    public static void Reload()
    {
        List<string> unloadlist = new List<string>();
        List<string> exceptlist = new List<string>(staticscenenames);
        exceptlist.Add("InitScene");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!exceptlist.Contains(scene.name))
                unloadlist.Add(scene.name);
        }
        for (int i = 0; i < unloadlist.Count; i++)
        {
            SceneManager.UnloadScene(unloadlist[i]);
            SceneManager.LoadSceneAsync(unloadlist[i], LoadSceneMode.Additive);
        }
    }

    private static string currentscenename = string.Empty;

    public static void LoadScene(string newscenename)
    {
        List<string> unloadlist=new List<string>();
        List<string> exceptlist = new List<string>(staticscenenames);
        exceptlist.Add("InitScene");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!exceptlist.Contains(scene.name))
                unloadlist.Add(scene.name);
        }

        for (int i = 0; i < unloadlist.Count; i++)
        {
            SceneManager.UnloadScene(unloadlist[i]);
        }

        //currentscenename = newscenename;
        var asynoperation = SceneManager.LoadSceneAsync (newscenename, LoadSceneMode.Additive);
        asynoperation.completed += OnSceneLoaded;

    }
    public static void OnSceneLoaded(AsyncOperation asynoperation)
    {
        UnifiedInput.UnifiedInputManager.Instance.RefreshInputModule();

        //if (!string.IsNullOrEmpty(currentscenename))
        //{
        //    var scene = SceneManager.GetSceneByName(currentscenename);
        //    if (scene != null && scene.isLoaded)
        //        SceneManager.SetActiveScene(scene);
        //}

    }

}
