#if UNITY_WINRT_8_0 || UNITY_WINRT_8_1 || UNITY_WINRT_10_0
#if UNITY_5_3_OR_NEWER
#define WSA_VR
#endif
#endif

using UnityEngine;
#if WSA_VR
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Persistence;
#endif
using System.Collections;
using System.Collections.Generic;
using Base;



public class SceneAnchorManager : Singleton<SceneAnchorManager>
{
//#if WSA_VR
//#else
//    private GameObject centergo; 
//    private void Start()
//    {
//        centergo = GameObject.Find("CruveCanvas");
//        if (centergo == null) 
//            return;

//        Camera cam=Camera.main;
//        cam.transform.position = centergo.transform.position
//            - centergo.transform.up * 0.2f
//            - centergo.transform.forward * 2.8f;
//           // - centergo.transform.right*0.5f;
//        cam.transform.forward = centergo.transform.forward;
//    }
//#endif

    private Dictionary<string, bool> SceneObjects = new Dictionary<string, bool>();

#if WSA_VR
    private WorldAnchorStore anchorStore;

    public bool enable
    {
        get 
        {
            return anchorStore != null; 
        }
    }

	// Use this for initialization
    void Start()
    {
        WorldAnchorStore.GetAsync(WorldAnchorStoreLoaded);
    }

    private void WorldAnchorStoreLoaded(WorldAnchorStore store)
    {
        this.anchorStore = store;

        string[] ids = this.anchorStore.GetAllIds();
        foreach (string id in ids)
        {
            SceneObjects.Add(id, true);
        }
    }

    //保存场景对象信息
    public bool SaveSceneObject(string objectId, GameObject target, WorldAnchor anchor)
    {
        if (anchorStore == null) 
            return false;

        var result = this.anchorStore.Save(objectId, anchor);
        if (result)
        {
            SceneObjects[objectId] = true;
        }
        return result;
    }

    //载入场景对象信息
    public WorldAnchor LoadSceneObject(string objectId,GameObject target)
    {
        if (anchorStore == null)
            return null;

        if (SceneObjects.ContainsKey(objectId))
        {
            return this.anchorStore.Load(objectId, target);
        }
        return null;

    }

    ////还原场景全部内容
    //public void RestoreAllSceneObjects()
    //{
    //    if (anchorStore == null)
    //        return;

    //    foreach (var key in SceneObjects.Keys)
    //    {
    //        var target = SceneObjects[key];
    //        this.anchorStore.Load(key, target);
    //    }
    //}
	
#endif
}
