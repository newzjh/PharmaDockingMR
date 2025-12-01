using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;
using MixedReality.Toolkit.UX;
using UnityEngine.XR.Interaction.Toolkit;

/*
    File browser for selecting files or folders at runtime.
 */


public class ResourceBrowserPanelEx : BasePanelEx<ResourceBrowserPanelEx>
{


    public class FileItem : MonoBehaviour
    {
        public int index;
    }


    

    private int buttonWidth;

    // Called when the user clicks cancel or select
    public delegate void FinishedCallback(string path);
    // Defaults to working directory
    protected string m_currentDirectory;




 
    protected string[] m_files;
    protected int m_selectedFile;


    protected bool m_currentDirectoryMatches;

    protected string m_name;

    GameObject fileitemprefab;

    public delegate void FileBrowserEventHandler(string path);
    public event FileBrowserEventHandler Selected;
    public event FileBrowserEventHandler Canceled;

    void OnSelect()
    {
        this.gameObject.SetActive(false);
        if (Selected != null)
        {
            if (m_files != null && m_selectedFile >= 0 && m_selectedFile < m_files.Length)
            {
                Selected(m_files[m_selectedFile]);
            }
        }
    }

    void OnCancel()
    {
        this.gameObject.SetActive(false);
        if (Canceled != null)
        {
            Canceled(m_currentDirectory);
        }
    }

    void Start()
    {
        {
            Transform t = this.transform.Find("SelectButton");
            if (t)
            {
                //t.gameObject.GetComponent<Button>().onClick.AddListener(delegate() { this.OnSelect(); });
                t.gameObject.GetComponent<PressableButton>().OnClicked.AddListener(delegate () { this.OnSelect(); });
            }
        }
        {
            Transform t = this.transform.Find("CancelButton");
            if (t)
            {
                //t.gameObject.GetComponent<Button>().onClick.AddListener(delegate() { this.OnCancel(); });
                t.gameObject.GetComponent<PressableButton>().OnClicked.AddListener(delegate () { this.OnCancel(); });
            }
        }
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
    }

    //Alex Tek modif
    public void Init()
    {
        m_name = name;
        m_currentDirectory = "mollist";
        m_selectedFile = -1;
        ReadDirectoryContents();
    }


    protected void ReadDirectoryContents()
    {
        List<string> files = new List<string>();
        TextAsset ta=Resources.Load<TextAsset>(m_currentDirectory);
        StringReader sr = new StringReader(ta.text);
        while (true)
        {
            string sLine = sr.ReadLine();
            if (sLine == null) break;
            files.Add(sLine);
        }
        m_files = files.ToArray();
        Array.Sort(m_files);

        BuildContent();
    }


    void OnSelectFile(PressableButton b)
    {
        FileItem[] fis = this.GetComponentsInChildren<FileItem>();
        if (fis == null)return;

        foreach (FileItem fi in fis)
        {
            Image img = fi.GetComponent<Image>();
            if (!img)continue;
            if (fi.gameObject == b.gameObject)
            {
                img.color = new Color(255 / 255.0f, 235.0f / 255.0f, 55.0f / 255.0f, 1.0f); ;
                m_selectedFile = fi.index;
            }
            else
            {
                img.color = new Color(154/255.0f, 240.0f/255.0f, 179.0f/255.0f,1.0f); ;
            }
        }
    }

    void BuildContent()
    {
        if (fileitemprefab == null) fileitemprefab = Resources.Load<GameObject>("UI/FileItem");

        Transform tPathLabel = this.transform.Find("PathLabel");
        if (tPathLabel)
        {
            Text text = tPathLabel.GetComponent<Text>();
            if (text)
            {
                text.text = m_currentDirectory;
            }
        }

        Transform tgrid = this.transform.Find("ScrollView").Find("Viewport").Find("Content");
        List<GameObject> deletelist = new List<GameObject>();
        for (int i = 0; i < tgrid.childCount; i++)
        {
            Transform t = tgrid.GetChild(i);
            deletelist.Add(t.gameObject);
        }
        for (int i = 0; i < deletelist.Count; i++)
        {
            DestroyImmediate(deletelist[i]);
        }

        for(int i=0;i<m_files.Length;i++)
        {
            string file = m_files[i];
            GameObject fileitemgo = GameObject.Instantiate(fileitemprefab);
            fileitemgo.name = "filebutton_" + i.ToString();
            fileitemgo.transform.SetParent(tgrid, false);
            FileItem fi=fileitemgo.AddComponent<FileItem>();
            fi.index = i;
            Text text = fileitemgo.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = file;
            }
            Button b = fileitemgo.GetComponent<Button>();
            if (b)
            {
                //b.onClick.AddListener(delegate() { this.OnSelectFile(b); });

                var go = b.gameObject;
                Button.DestroyImmediate(b);
                var rc = go.GetComponent<RectTransform>();
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0, 4.606735f);
                box.size = new Vector3(370, 40, 32);
                var newb = go.AddComponent<PressableButton>();
                if (newb)
                {
                    newb.OnClicked.AddListener(delegate ()
                    {
                        this.OnSelectFile(newb);
                    });

                }
                var adapter = go.AddComponent<UGUIInputAdapter>();
                adapter.interactable = true;
            }
        }

    }


  

   

}