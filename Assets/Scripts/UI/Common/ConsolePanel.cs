using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using LogTrace;



public class ConsolePanel : BasePanelEx<ConsolePanel>
{


    GameObject fileitemprefab;


    public class STraceMsg
    {
        public DateTime time;
        public LogType logtype;
        public string content;
        public string stack;
        public bool expand = false;
    }

    List<STraceMsg> mLines = new List<STraceMsg>();

    void Awake()
    {
        base.Awake();
        Application.logMessageReceived += onTrace;
        Trace.logMessageReceived += onTrace;
        Trace.TraceLn(this.GetType() + " Awake success!");
    }

    void OnDestroy()
    {
        Trace.logMessageReceived -= onTrace;
        Application.logMessageReceived -= onTrace;
    }

    private void Start()
    {
        this.transform.localPosition = Vector3.zero;
        SetVisible(false);
    }

    public void onTrace(string content, string stack, LogType logtype)
    {
        if (!Application.isPlaying)
            return;

        //string sline = time.ToString ("yyyy-MM-dd HH:mm:ss") + " :" + msg ;
        //string sline = time.ToString() + " :" + msg ;

        lock (mLines)
        {
            if (mLines.Count > 50)
            {
                mLines.RemoveAt(0);
            }
            STraceMsg msg = new STraceMsg();
            msg.content = content;
            msg.stack = stack;
            msg.logtype = logtype;
            msg.time = DateTime.Now;
            mLines.Add(msg);
        }

        if (this.gameObject.activeSelf)
        {
            Transform tscrollview = this.transform.Find("ScrollView");
            Transform tviewport = tscrollview.Find("Viewport");
            Transform tgrid = tviewport.Find("Content");
            if (mLines.Count != tgrid.childCount)
                BuildContent();
        }
    }

    public override void OnHide()
    {
        base.OnHide();
    }

    public override void OnShow()
    {
        base.OnShow();

        Transform tscrollview = this.transform.Find("ScrollView");
        Transform tviewport = tscrollview.Find("Viewport");
        Transform tgrid = tviewport.Find("Content");

        Vector3 localpos = tgrid.localPosition;
        localpos.y = 0.0f;
        tgrid.localPosition = localpos;

        BuildContent();
    }

    void BuildContent()
    {
  
        if (fileitemprefab == null)
            fileitemprefab = Resources.Load<GameObject>("UI/ConsoleItem");



        Transform tgrid = this.transform.Find("ScrollView").Find("Viewport").Find("Content");
        List<GameObject> deletelist = new List<GameObject>();
        for (int i = 0; i < tgrid.childCount; i++)
        {
            Transform t = tgrid.GetChild(i);
            deletelist.Add(t.gameObject);
        }
        for (int i = 0; i < deletelist.Count; i++)
        {
            Destroy(deletelist[i]);
        }


        for (int i = 0; i < mLines.Count; i++)
        {
            STraceMsg msg = mLines[i];
            GameObject fileitemgo = GameObject.Instantiate(fileitemprefab);
            fileitemgo.name = "msgitem_" + i.ToString();
            fileitemgo.transform.SetParent(tgrid, false);
            Text text = fileitemgo.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = msg.content;
                if (msg.logtype == LogType.Error || msg.logtype == LogType.Exception)
                    text.color = new Color(1.0f, 0.7f, 0.7f);
                else if (msg.logtype == LogType.Warning)
                    text.color = new Color(1.0f, 0.9f, 0.6f);
                else
                    text.color = new Color(0.8f, 0.9f, 1.0f);
            }
        }


    }

    public override void OnClick(GameObject b)
    {
        if (b.name.StartsWith("OkButton"))
        {
            this.SetVisible(false);
        }
    }


}