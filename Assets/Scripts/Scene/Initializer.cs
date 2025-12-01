using System;
using System.Collections.Generic;
using UnifiedInput;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Initializer : Singleton<Initializer>
{
    public void Awake()
    {
        GameObject globalgo = GameObject.Find("Global");
        if (globalgo == null)
        {
            GameObject globalprefab = Resources.Load("Global") as GameObject;
            if (globalprefab)
            {
                globalgo = GameObject.Instantiate(globalprefab);
                globalgo.name = "Global";
            }
        }
        if (globalgo!=null)
            DontDestroyOnLoad(globalgo);

        UnifiedInputManager.Instance.enabled = false;
        GazeManager.Instance.enabled = false;

        //var goMRTKXRRig= GameObject.Find("MRTK XR Rig");
        //if (goMRTKXRRig)
        //    DontDestroyOnLoad(goMRTKXRRig);

        //var goMRTKInputSimulator = GameObject.Find("MRTKInputSimulator");
        //if (goMRTKInputSimulator)
        //    DontDestroyOnLoad(goMRTKInputSimulator);

        //var goEventSystem = GameObject.Find("EventSystem");
        //if (goEventSystem)
        //    DontDestroyOnLoad(goEventSystem);
    }
}
