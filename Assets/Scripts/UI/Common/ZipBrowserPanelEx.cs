using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MoleculeLogic;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using MixedReality.Toolkit.UX;
using UnityEngine.XR.Interaction.Toolkit;

/*
    File browser for selecting files or folders at runtime.
 */


public class ZipBrowserPanelEx : BasePanelEx<ZipBrowserPanelEx>
{
    public class FolderItem : MonoBehaviour
    {
        public int index;
    }

    public class FileItem : MonoBehaviour
    {
        public int index;
    }


    public enum FileBrowserType
    {
        File,
        Directory
    }

    private int buttonWidth;

    // Called when the user clicks cancel or select
    public delegate void FinishedCallback(string path);
    // Defaults to working directory
    public string CurrentDirectory
    {
        get
        {
            return m_currentDirectory;
        }
        set
        {
            SetNewDirectory(value);
        }
    }
    protected string m_currentDirectory;
    public string SelectionPattern
    {
        get
        {
            return m_filePattern;
        }
        set
        {
            m_filePattern = value;
            //ReadDirectoryContents();
        }
    }
    protected string m_filePattern;




    // Browser type. Defaults to File, but can be set to Folder
    public FileBrowserType BrowserType
    {
        get
        {
            return m_browserType;
        }
        set
        {
            m_browserType = value;
            ReadDirectoryContents();
        }
    }
    protected FileBrowserType m_browserType;
    protected string[] m_currentDirectoryParts;
    protected string[] m_files;
    protected int m_selectedFile;
    protected string[] m_directories;


    protected bool m_currentDirectoryMatches;

    protected string m_name;

    GameObject folderitemprefab;
    GameObject fileitemprefab;

    public delegate void FileBrowserEventHandler(string path);
    public event FileBrowserEventHandler Selected;
    public event FileBrowserEventHandler Canceled;

    void OnSelect()
    {
        this.gameObject.SetActive(false);
        PlayerPrefs.SetString("LastZip", m_currentDirectory);
        if (Selected != null)
        {
            if (m_files != null && m_selectedFile >= 0 && m_selectedFile < m_files.Length)
            {
                Selected(m_currentDirectory + "/" + m_files[m_selectedFile]);
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

    Dictionary<string,ZipEntry> entries;
    ZipInputStream zis;
    string root;

    //Alex Tek modif
    public void Init(string selectpattern, string startingPath)
    {
        m_name = name;
        m_filePattern = selectpattern;
        m_browserType = FileBrowserType.File;

        TextAsset ta = Resources.Load<TextAsset>("userdata.zip");
        if (ta == null)
            return;
        MemoryStream ms = new MemoryStream(ta.bytes);

        entries = new Dictionary<string,ZipEntry>();
        zis = new ZipInputStream(ms);
        root = null;

        ZipEntry zipEntry;
        while ((zipEntry = zis.GetNextEntry()) != null)
        {
            string key = zipEntry.Name;
            if (zipEntry.IsDirectory)
            { 
                if (key.EndsWith("/"))
                    key=key.Substring(0,key.Length-1);
            }

            entries[key] = zipEntry;

            if (root == null)
                root = key;

        }

        if (startingPath == null || startingPath.Length <= 0)
        {
            string lastOpenDir = null;
            //lastOpenDir = PlayerPrefs.GetString("LastZip");
            if (lastOpenDir == null && entries.Count>0)
            {
                lastOpenDir = root;
//#if !UNITY_WEBPLAYER
//                lastOpenDir = System.IO.Directory.GetCurrentDirectory();
//#else
//                lastOpenDir = Application.persistentDataPath;                
//#endif
            }
            startingPath = lastOpenDir;
        }

        startingPath = startingPath.Replace("\\", "/");
        SetNewDirectory(startingPath);
    }

    protected void SetNewDirectory(string directory)
    {
        m_currentDirectory = directory.Replace("\\", "/");
        m_selectedFile = -1;
        ReadDirectoryContents();
    }

    private string[] GetDirectories(string path, string searchpattern=null)
    {
        string[] parts = path.Split('/');

        List<string> results = new List<string>();
        foreach(var pair in entries)
        {
            string key = pair.Key;
            ZipEntry entry = pair.Value;

            if (entry.IsDirectory)
            {
                if (entry.Name.StartsWith(path))
                {
                    string[] enumparts = key.Split('/');
                    if (enumparts.Length == parts.Length + 1)
                    {
                        if (searchpattern == null ||
                            Path.GetExtension(enumparts[enumparts.Length - 1])==Path.GetExtension(searchpattern))
                        {
                            results.Add(key);
                        }
                    }
                }
            }
        }
        return results.ToArray();
    }

    private string[] GetFiles(string path, string searchpattern=null)
    {
        string[] parts = path.Split('/');

        List<string> results = new List<string>();
        foreach (var pair in entries)
        {
            string key = pair.Key;
            ZipEntry entry = pair.Value;

            if (entry.IsFile)
            {
                if (entry.Name.StartsWith(path))
                {
                    string[] enumparts = key.Split('/');
                    if (enumparts.Length == parts.Length + 1)
                    {
                        if (searchpattern == null ||
                            Path.GetExtension(enumparts[enumparts.Length - 1]) == Path.GetExtension(searchpattern))
                        {
                            results.Add(key);
                        }
                    }
                }
            }
        }
        return results.ToArray();
    }

    protected void ReadDirectoryContents()
    {
        m_currentDirectoryParts = m_currentDirectory.Split('/');
        if (m_currentDirectoryParts.Length > 1 &&
            (m_currentDirectoryParts[m_currentDirectoryParts.Length - 1] == null
            || m_currentDirectoryParts[m_currentDirectoryParts.Length - 1].Length <= 0))
        {
            List<string> parts = new List<string>();
            parts.AddRange(m_currentDirectoryParts);
            parts.RemoveAt(parts.Count - 1);
            m_currentDirectoryParts = parts.ToArray();
        }

        List<string> dirs = new List<string>();
        if (m_currentDirectoryParts.Length > 1)
        {
            dirs.Add("..");
        }
        if (BrowserType == FileBrowserType.File || SelectionPattern == null)
        {
            dirs.AddRange(GetDirectories(m_currentDirectory));
        }
        else
        {
            string[] searchpatterns = SelectionPattern.Split('|');
            foreach (string searchpattern in searchpatterns)
            {
                string[] tempdirs = GetDirectories(m_currentDirectory, searchpattern);
                if (tempdirs != null) dirs.AddRange(tempdirs);
            }
        }

        for (int i = 0; i < dirs.Count; ++i)
        {
            dirs[i] = dirs[i].Replace("\\", "/");
            dirs[i] = dirs[i].Substring(dirs[i].LastIndexOf('/') + 1);
        }

        m_directories = dirs.ToArray();


        List<string> files = new List<string>();
        if (BrowserType == FileBrowserType.Directory || SelectionPattern == null)
        {
            files.AddRange(GetFiles(m_currentDirectory));
        }
        else
        {
            string[] searchpatterns = SelectionPattern.Split('|');
            Dictionary<string, string> filemap = new Dictionary<string, string>();
            foreach (string searchpattern in searchpatterns)
            {
                string[] tempfiles = GetFiles(m_currentDirectory, searchpattern);
                if (tempfiles != null)
                {
                    foreach (string tempfile in tempfiles)
                    {
                        filemap[tempfile] = tempfile;
                    }
                }
            }
            files.AddRange(filemap.Keys);
        }
        for (int i = 0; i < files.Count; ++i)
        {
            files[i] = files[i].Replace("\\", "/");
            files[i] = Path.GetFileName(files[i]);
        }
        m_files = files.ToArray();
        Array.Sort(m_files);

        BuildContent();
    }

    private string GetFullPath(string path)
    {
        string newpath = path;
        string[] parts = path.Split('/');
        if (parts != null && parts.Length>2 && parts[parts.Length-1]=="..")
        {
            newpath = "";
            for (int i = 0; i < parts.Length - 2;i++ )
            {
                newpath += parts[i];
                if (i<parts.Length-3)
                    newpath += "/";
            }
        }
        return newpath;
    }

    void OnSelectFolder(PressableButton b)
    {
        Text text = b.gameObject.GetComponentInChildren<Text>();
        string newpath = GetFullPath(m_currentDirectory + "/" + text.text);
        SetNewDirectory(newpath);
    }

    void OnSelectFile(PressableButton b)
    {
        FileItem[] fis = this.GetComponentsInChildren<FileItem>();
        if (fis == null) return;

        foreach (FileItem fi in fis)
        {
            Image img = fi.GetComponent<Image>();
            if (!img) continue;
            if (fi.gameObject == b.gameObject)
            {
                img.color = new Color(255 / 255.0f, 235.0f / 255.0f, 55.0f / 255.0f, 1.0f); ;
                m_selectedFile = fi.index;
            }
            else
            {
                img.color = new Color(154 / 255.0f, 240.0f / 255.0f, 179.0f / 255.0f, 1.0f); ;
            }
        }
    }

    void BuildContent()
    {
        if (folderitemprefab == null) folderitemprefab = Resources.Load<GameObject>("UI/FolderItem");
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

        for (int i = 0; i < m_directories.Length; i++)
        {
            string dir = m_directories[i];
            GameObject folderitemgo = GameObject.Instantiate(folderitemprefab);
            folderitemgo.name = "dirbutton_" + i.ToString();
            folderitemgo.transform.SetParent(tgrid, false);
            FolderItem fi = folderitemgo.AddComponent<FolderItem>();
            fi.index = i;
            Text text = folderitemgo.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = dir;
            }
            Button b = folderitemgo.GetComponent<Button>();
            if (b)
            {
                //b.onClick.AddListener(delegate() { this.OnSelectFolder(b); });

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
                        this.OnSelectFolder(newb);
                    });

                }
                var adapter = go.AddComponent<UGUIInputAdapter>();
                adapter.interactable = true;
            }
        }
        for (int i = 0; i < m_files.Length; i++)
        {
            string file = m_files[i];
            GameObject fileitemgo = GameObject.Instantiate(fileitemprefab);
            fileitemgo.name = "filebutton_" + i.ToString();
            fileitemgo.transform.SetParent(tgrid, false);
            FileItem fi = fileitemgo.AddComponent<FileItem>();
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


    //public void OnGUI()
    //{

    //    GUILayout.BeginArea(
    //        m_screenRect,
    //        m_name,
    //        GUI.skin.window
    //    );
    //    GUILayout.BeginHorizontal();
    //    for (int parentIndex = 0; parentIndex < m_currentDirectoryParts.Length; ++parentIndex)
    //    {
    //        if (parentIndex == m_currentDirectoryParts.Length - 1)
    //        {
    //            GUILayout.Label(m_currentDirectoryParts[parentIndex], CentredText);
    //        }
    //        else if (GUILayout.Button(m_currentDirectoryParts[parentIndex]))
    //        {
    //            string parentDirectoryName = m_currentDirectory;
    //            for (int i = m_currentDirectoryParts.Length - 1; i > parentIndex; --i)
    //            {
    //                parentDirectoryName = Path.GetDirectoryName(parentDirectoryName);
    //            }
    //            SetNewDirectory(parentDirectoryName);
    //        }
    //        GUILayout.Label("/", CentredText);
    //    }
    //    GUILayout.FlexibleSpace();
    //    GUILayout.EndHorizontal();
    //    m_scrollPosition = GUILayout.BeginScrollView(
    //        m_scrollPosition,
    //        false,
    //        true,
    //        GUI.skin.horizontalScrollbar,
    //        GUI.skin.verticalScrollbar,
    //        GUI.skin.box
    //    );
    //    m_selectedDirectory = GUILayoutx.SelectionList(
    //        m_selectedDirectory,
    //        m_directoriesWithImages,
    //        DirectoryDoubleClickCallback
    //    );
    //    if (m_selectedDirectory > -1)
    //    {
    //        m_selectedFile = m_selectedNonMatchingDirectory = -1;
    //    }
    //    m_selectedNonMatchingDirectory = GUILayoutx.SelectionList(
    //        m_selectedNonMatchingDirectory,
    //        m_nonMatchingDirectoriesWithImages,
    //        NonMatchingDirectoryDoubleClickCallback
    //    );
    //    if (m_selectedNonMatchingDirectory > -1)
    //    {
    //        m_selectedDirectory = m_selectedFile = -1;
    //    }
    //    GUI.enabled = BrowserType == FileBrowserType.File;
    //    m_selectedFile = GUILayoutx.SelectionList(
    //        m_selectedFile,
    //        m_filesWithImages,
    //        FileDoubleClickCallback
    //    );
    //    GUI.enabled = true;
    //    if (m_selectedFile > -1)
    //    {
    //        m_selectedDirectory = m_selectedNonMatchingDirectory = -1;
    //    }
    //    GUI.enabled = false;
    //    GUILayoutx.SelectionList(
    //        -1,
    //        m_nonMatchingFilesWithImages
    //    );
    //    GUI.enabled = true;
    //    GUILayout.EndScrollView();
    //    GUILayout.BeginHorizontal();

    //    GUILayout.EndHorizontal();
    //    GUILayout.BeginHorizontal();
    //    GUILayout.FlexibleSpace();
    //    if (GUILayout.Button("Cancel", GUILayout.Width(buttonWidth)))
    //    {
    //        m_callback(null);
    //    }
    //    if (BrowserType == FileBrowserType.File)
    //    {
    //        GUI.enabled = m_selectedFile > -1;
    //    }
    //    else
    //    {
    //        if (SelectionPattern == null)
    //        {
    //            GUI.enabled = m_selectedDirectory > -1;
    //        }
    //        else
    //        {
    //            GUI.enabled = m_selectedDirectory > -1 ||
    //                            (
    //                                m_currentDirectoryMatches &&
    //                                m_selectedNonMatchingDirectory == -1 &&
    //                                m_selectedFile == -1
    //                            );
    //        }
    //    }
    //    if (GUILayout.Button("Select", GUILayout.Width(buttonWidth)))
    //    {
    //        if (BrowserType == FileBrowserType.File)
    //        {
    //            m_callback(Path.Combine(m_currentDirectory, m_files[m_selectedFile]));
    //        }
    //        else
    //        {
    //            if (m_selectedDirectory > -1)
    //            {
    //                m_callback(Path.Combine(m_currentDirectory, m_directories[m_selectedDirectory]));
    //            }
    //            else
    //            {
    //                m_callback(m_currentDirectory);
    //            }
    //        }
    //    }
    //    GUI.enabled = true;
    //    GUILayout.EndHorizontal();
    //    GUILayout.EndArea();

    //    if (Event.current.type == EventType.Repaint)
    //    {
    //        SwitchDirectoryNow();
    //    }
    //}

    protected void FileDoubleClickCallback(int i)
    {
        if (BrowserType == FileBrowserType.File)
        {
            //m_callback(Path.Combine(m_currentDirectory, m_files[i]));
        }
    }

    protected void DirectoryDoubleClickCallback(int i)
    {
        SetNewDirectory(Path.Combine(m_currentDirectory, m_directories[i]));
    }



}