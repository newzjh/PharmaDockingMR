#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WINRT_8_0 || UNITY_WINRT_8_1 || UNITY_WINRT_10_0
#define NATIVE_ENABLE
#endif

using UnityEngine;
using System;
using System.Threading;
#if NETFX_CORE
using System.Threading.Tasks;
using Windows.System.Threading;
#endif
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoleculeLogic;
using MoleculeUI;
using LogTrace;

public class Pharmit : Singleton<Pharmit>
{

#if NATIVE_ENABLE

    struct s_PharmaPoint
    {
        public float x;
        public float y;
        public float z;
        public float radius;
    };

    private struct s_PharmaSet
    {
        public IntPtr pharmas;
        public int pharmanum;
    }

    private struct s_Pharma
    {
	    public IntPtr name;
	    public int atomic_number_label;
	    public float defaultSearchRadius;
	    public float clusterLimit;
        public IntPtr pts;
        public int ptnum;
    };


    private static T PtrToStruct<T>(IntPtr p)
    {
        T t = default(T);
#if NETFX_CORE 
        t = (T)Marshal.PtrToStructure<T>(p);
#else
        t = (T)Marshal.PtrToStructure(p, typeof(T));
#endif
        return t;
    }


    [DllImport("Pharmit")]
    private static extern void ReleasePharmaPoints(IntPtr p);

    [DllImport("Pharmit")]
    private static extern IntPtr IdentifyPharmaPoints(IntPtr pdbname);

    private class AsynResult
    {
        public string path;
        public Dictionary<string,d_Pharma> pharmas;
    }

    private Queue<string> requestingqueue = new Queue<string>();  
    private object requestguard = new object();

    private Queue<AsynResult> finishqueue = new Queue<AsynResult>();
    private object finishguard = new object();

    private Dictionary<string, Molecule> pathtomolmap = new Dictionary<string, Molecule>();

#if NETFX_CORE
    private Task task = null;
#else
    private Thread task = null;
#endif
    private void Start()
    {
#if NETFX_CORE 
        task = new Task(WorkThread);
        task.Start();
#else
        task = new Thread(new ThreadStart(WorkThread));
        task.Start();
#endif
        Trace.TraceLn("Pharmit Start success!");
    }

    private void OnDestroy()
    {
        Stop();
#if NETFX_CORE 
        task.Wait();
#else
        task.Join();
#endif
        task = null;
    }

    private class d_Pharma
    {
        public s_Pharma info;
        public List<s_PharmaPoint> pts = new List<s_PharmaPoint>();
    }

    private void AddPharmas(Molecule mol, ref Dictionary<string, d_Pharma>  pharmaset)
    {
        if (mol == null) 
            return;

        mol.pharmas = new List<Pharma>();
        foreach(KeyValuePair<string, d_Pharma> pair in pharmaset)
        {
            string pharmaname = pair.Key;
            s_Pharma _pharma = pair.Value.info;
            List<s_PharmaPoint> pts = pair.Value.pts;

            if (pts.Count <= 0)
                continue;

            int pharmaindex=mol.pharmas.Count;
            Pharma pharma = Pharma.CreatePharma(pharmaindex, mol.pharmagroup.transform);
            pharma.atomic_number_label = _pharma.atomic_number_label;
            pharma.defaultSearchRadius = _pharma.defaultSearchRadius;
            pharma.clusterLimit = _pharma.clusterLimit;
            pharma.name = pharmaname;
            Color color = MaterialLib.GetRandomColor();
            pharma.color = color;
            color.a = 0.35f;

            for (int j = 0; j < pts.Count; j++)
            {
                s_PharmaPoint srcvert = pts[j];
                PharmaPoint pt = PharmaPoint.CreatePharmaPoint(pharma.points.Count, pharma.transform);
                pt.pos.x = srcvert.x;
                pt.pos.y = srcvert.y;
                pt.pos.z = srcvert.z;
                pt.radius = srcvert.radius;
                pt.color = color;
                pt.parent = pharmaindex;
                pharma.points.Add(pt);
            }
            mol.pharmas.Add(pharma);
        }

        mol.scheme.showpharmas.Clear();
        for (int i = 0; i < mol.pharmas.Count; i++)
        {
            mol.scheme.showpharmas.Add(true);
        }
        mol.Represent();
        PharmaPanel.Instance.SetContent(mol);
        
    }

    void Update()
    {
        AsynResult ret = null;
        lock (finishguard)
        {
            if (finishqueue.Count > 0)
            {
                ret = finishqueue.Dequeue();
            }
        }

        if (ret != null)
        {
            string path = ret.path;
            Molecule mol = pathtomolmap[path]; 
            Dictionary<string, d_Pharma> pharmaset = ret.pharmas;
            AddPharmas(mol, ref pharmaset);
            //pathtomolmap[path]=null;
            pathtomolmap.Remove(path);
        }
    }

    public void DetectPharmaPoints(Molecule mol, string path)
    {
        if (mol == null)
            return;

        if (!File.Exists(path))
            return;

        //it is already being processing, wait
        if (pathtomolmap.ContainsKey(path))
            return;

        pathtomolmap[path] = mol;

        lock (requestguard)
        {
            requestingqueue.Enqueue(path);
        }
    }

    volatile bool run = false;

    private void WorkThread()
    {
        run = true;
        while (run)
        {
            string requestpath = null;
            lock(requestguard)
            {
                if (requestingqueue.Count > 0) requestpath = requestingqueue.Dequeue();
            }

            Dictionary<string,d_Pharma> pharmaset = null;
            if (requestpath != null && requestpath.Length>0)
            {
                pharmaset = _FindPharmaPoints(requestpath);
            }

            if (pharmaset == null)
            {
#if NETFX_CORE
                Task.Delay(300).Wait();
#else
                Thread.Sleep(300);
#endif
                continue;
            }

            lock (finishguard)
            {
                AsynResult ret = new AsynResult();
                ret.path = requestpath;
                ret.pharmas = pharmaset;
                finishqueue.Enqueue(ret);
            }
        }
    }

    private void Stop()
    {
        run = false;
    }

    private Dictionary<string, d_Pharma> _FindPharmaPoints(string path)
    {

        IntPtr ppath = Marshal.StringToHGlobalAnsi(path);
        if (ppath == IntPtr.Zero) return null;
        IntPtr ppharmapts = IdentifyPharmaPoints(ppath);
        if (ppharmapts == IntPtr.Zero) return null;

        Dictionary<string,d_Pharma> pharmadict = new Dictionary<string, d_Pharma>();
        s_PharmaSet pharmaset = PtrToStruct<s_PharmaSet>(ppharmapts);
        for (int i = 0; i < pharmaset.pharmanum; i++)
        {
            if (pharmaset.pharmas == IntPtr.Zero) continue;
            IntPtr ppharma = new IntPtr(pharmaset.pharmas.ToInt32() + Marshal.SizeOf(typeof(s_Pharma)) * i);
            if (ppharma == IntPtr.Zero) continue;
            s_Pharma _pharma = PtrToStruct<s_Pharma>(ppharma);

            d_Pharma d_pharma = new d_Pharma();
            d_pharma.info = _pharma;
            string pharmaname = Marshal.PtrToStringAnsi(_pharma.name);

            for (int j = 0; j < _pharma.ptnum; j++)
            {
                if (_pharma.pts == IntPtr.Zero) continue;
                IntPtr ppt = new IntPtr(_pharma.pts.ToInt32() + Marshal.SizeOf(typeof(s_PharmaPoint)) * j);
                if (ppt == IntPtr.Zero) continue;
                s_PharmaPoint srcvert = PtrToStruct<s_PharmaPoint>(ppt);
                if (float.IsNaN(srcvert.x))
                    srcvert.x = 0.0f;
                if (float.IsNaN(srcvert.y))
                    srcvert.y = 0.0f;
                if (float.IsNaN(srcvert.z))
                    srcvert.z = 0.0f;
                d_pharma.pts.Add(srcvert);
            }

            pharmadict[pharmaname] = d_pharma;
        }
        ReleasePharmaPoints(ppharmapts);
        Marshal.FreeHGlobal(ppath);

        return pharmadict;
    }

    void OnGUI()
    {
        int i=0;
        foreach (KeyValuePair<string, Molecule> pair in pathtomolmap)
        {
            if (pair.Value == null) continue;
            string filename = Path.GetFileName(pair.Key);
            Rect rc=new Rect(30,30+i*30,300,30);
            GUI.Label(rc, "Pharmit.detecting: " + filename);
            i++;
        }
    }

#else

    public void DetectPharmaPoints(Molecule mol, string path)
    {
    }

#endif
}
