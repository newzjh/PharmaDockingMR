using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoleculeLogic;
using LogTrace;
using MoleculeUI;

[SerializeField]
public class iDockConfItem
{
    public string receptor_path;
    public string ligand_path;
    public Vector3 center;
    public Vector3 size;
}

public class iDock : Singleton<iDock>
{

    private class AsynIDockResult
    {
        public string ligand_path;
        public string receptor_path;
        public string result_path;
    }

    private Queue<AsynIDockResult> finishqueue = new Queue<AsynIDockResult>();
    private object finishguard = new object();


    private Dictionary<string, iDockConfItem> matchingmap = new Dictionary<string, iDockConfItem>();


    private void Start()
    {
 

        Trace.TraceLn("idock.Start success!");
    }

    private void OnDestroy()
    {
    }

    //public delegate void ResultHandler(string path);
    //public ResultHandler OnResult = null;

    void Update()
    {
        AsynIDockResult ret = null;
        lock (finishguard)
        {
            if (finishqueue.Count > 0)
            {
                ret = finishqueue.Dequeue();
            }
        }

        if (ret != null)
        {
            string key = Path.GetFileNameWithoutExtension(ret.receptor_path) + "&" + Path.GetFileNameWithoutExtension(ret.ligand_path);
            key = key.ToLower();
            if (matchingmap.ContainsKey(key))
                matchingmap.Remove(key);

            BigPanel.Instance.OpenFileCallback(ret.result_path);
            //MoleculeFactory.Instance.CreateFromFile(ret.result_path);
            tracemsg = "";
        }
    }



    private delegate void DetectHandler(iDockConfItem cfgitem);
    ThreadTask task;

    public void Detect(string ligand_path, string receptor_path, iDockConfItem cfgitem)
    {
        if (!File.Exists(ligand_path) || !File.Exists(receptor_path))
            return;

        string key = Path.GetFileNameWithoutExtension(receptor_path) + "&" + Path.GetFileNameWithoutExtension(ligand_path);
        key = key.ToLower();

        

        if (matchingmap.ContainsKey(key))
            return;
        matchingmap[key] = cfgitem;

        idock.AsynPool<int>.trace -= OnTrace;
        idock.AsynPool<int>.trace += OnTrace;

        DetectHandler detectfun = _DetectImp;
        task = new ThreadTask(detectfun, new object[] { cfgitem });
        //detectfun.BeginInvoke(cfgitem, null, null);
    }

    private void OnTrace(string msg,float progress)
    {
        tracemsg = msg;
    }

    private void _DetectImp(iDockConfItem cfgitem)
    {

        string resultpath = idock.imain.processByMatch(cfgitem.ligand_path, cfgitem.receptor_path,
            cfgitem.center.x,cfgitem.center.y,cfgitem.center.z,
            cfgitem.size.x,cfgitem.size.y,cfgitem.size.z);

        AsynIDockResult ret = new AsynIDockResult();
        ret.result_path = resultpath;
        ret.ligand_path = cfgitem.ligand_path;
        ret.receptor_path = cfgitem.receptor_path;

        lock (finishguard)
        {
            finishqueue.Enqueue(ret);
        }
    }

    string tracemsg = "";

    void OnGUI()
    {
        if (!Application.isEditor)
            return;
           
        int i = 0;
        foreach (KeyValuePair<string, iDockConfItem> pair in matchingmap)
        {
            Rect rc = new Rect(30, 60 + i * 30, 300, 30);
            GUI.Label(rc, "IDock... " + pair.Key);
            i++;
        }

        {
            Rect rc = new Rect(30, 30, 300, 30);
            GUI.Label(rc, tracemsg);
        }
    }

    private GameObject boundprefab;
    private GameObject boundgo;

    public void ShowBound(MoleculeController mc)
    { 
        if (boundprefab==null)
            boundprefab = Resources.Load<GameObject>("Platform/DockBound");

        if (boundprefab == null)
            return;

        if (boundgo == null)
        {
            boundgo = GameObject.Instantiate<GameObject>(boundprefab);
            boundgo.transform.parent = transform;// mc.mol.atomgroup.transform;
            boundgo.transform.localScale = Vector3.one;
            boundgo.transform.localPosition = Vector3.zero;
            boundgo.transform.localEulerAngles = Vector3.zero;
        }
        
    }

    public void HideBound()
    {
        if (boundgo != null)
            Destroy(boundgo);
        boundgo = null;
    }

    public void UpdateTransform(MoleculeController mc, Vector3 center, Vector3 size)
    {
        if (boundgo == null)
            return;

        //boundgo.transform.localPosition = center;
        //boundgo.transform.localScale = size*2.0f;

        boundgo.transform.position = mc.mol.atomgroup.transform.TransformPoint(center);
        Vector3 scale = mc.mol.atomgroup.transform.lossyScale;
        scale.x *= size.x;
        scale.y *= size.y;
        scale.z *= size.z;
        //scale *= 2;
        boundgo.transform.rotation = mc.mol.atomgroup.transform.rotation;
        boundgo.transform.localScale = scale;
    }
}
