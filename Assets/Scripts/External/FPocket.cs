#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WINRT_8_0 || UNITY_WINRT_8_1 || UNITY_WINRT_10_0
#define NATIVE_ENABLE
#endif

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoleculeLogic;
using MoleculeUI;
using LogTrace;

public class FPocket : Singleton<FPocket>
{
 #if NATIVE_ENABLE

    private struct s_vvertice
    {
        public int resid; /**< residue ID*/
        public int id; /**< vertice ID*/
        public int seen;       /**< Say if we have seen this vertice during a neighbor search */
        public int qhullId;    /**< ID of the vertice in qhull output*/
        public int type;	/**< 0 if apolar contacts, 1 if polar */
        public float ray;	/**< Ray of voronoi vertice */
        public float x;	/**< X coord */
        public float y;	/**< Y coord */
        public float z;	/**< Z coord */
        public int sort_x;		/**< Index in the sorted tab by X coord */
        public int apol_neighbours;	/**< number of neighbouring apolar alpha spheres */
        public int vneigh0;         /**< vertice neighbours (4 contact atoms)*/
        public int vneigh1;         /**< vertice neighbours (4 contact atoms)*/
        public int vneigh2;         /**< vertice neighbours (4 contact atoms)*/
        public int vneigh3;         /**< vertice neighbours (4 contact atoms)*/
        public IntPtr neigh0;	/**< The theorical 4 contacted atoms */
        public IntPtr neigh1;	/**< The theorical 4 contacted atoms */
        public IntPtr neigh2;	/**< The theorical 4 contacted atoms */
        public IntPtr neigh3;	/**< The theorical 4 contacted atoms */
        public float baryx;         /**< Barycenter of the pocket */
        public float baryy;         /**< Barycenter of the pocket */
        public float baryz;         /**< Barycenter of the pocket */
    }


    /** Chained list stuff for vertices in a pocket (to enable dynamic modifications) */
    private struct node_vertice
    {
        public IntPtr next;  /**<pointer to next node*/
        public IntPtr prev;  /**< pointer to previous node*/
        public IntPtr vertice; /**< pointer to current vertice*/

    }

    /** Chained list stuff for vertices in a pocket (to enable dynamic modifications) */
    private struct c_lst_vertices
    {
        public IntPtr first;    /**< pointer to first node*/
        public IntPtr last;     /**< pointer to last node */
        public IntPtr current;  /**< pointer to current node*/
        public int n_vertices;     /**< number of vertices*/

    }

    private struct s_pocket
    {
        public IntPtr pdesc; /**< pointer to pocket descriptor structure */
        public IntPtr v_lst; /**< chained list of Voronoi vertices*/
        public float score;	/**< Discretize different parts of the score */
        public float ovlp;         /**< Atomic overlap	*/
        public float ovlp2;        /**< second overlap criterion*/
        public float vol_corresp;	/**< Volume overlap */
        public float baryx;			/**< Barycenter (center of mass) of the pocket */
        public float baryy;			/**< Barycenter (center of mass) of the pocket */
        public float baryz;			/**< Barycenter (center of mass) of the pocket */
        public int rank;				/**< Rank of the pocket */
        public int size;
        public int nAlphaApol;			/**< Number of apolar alpha spheres*/
        public int nAlphaPol;			/**< Number of polar alpha spheres */
    }

    /** Chained list stuff for pockets */
    private struct node_pocket
    {
        public IntPtr next; /**< pointer to next pocket*/
        public IntPtr prev; /**< pointer to previous pocket*/
        public IntPtr pocket;
    }

    /** Chained list stuff for pockets */
    private struct c_lst_pockets
    {
        public IntPtr first; /**< pointer to the first pocket*/
        public IntPtr last; /**< pointer to the last pocket*/
        public IntPtr current; /**< pointer to the current pocket*/
        public int n_pockets;  /**< number of pockets in the chained list*/
        public IntPtr vertices; /**< access to vertice list*/
    }


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



    [DllImport("FPocket")]
    private static extern void setTempPath(IntPtr path);

    [DllImport("FPocket")]
    private static extern void releasePockets(IntPtr p);

    [DllImport("FPocket")]
    private static extern IntPtr getPocketsFromPDB(IntPtr pdbname);
    private struct d_PocketPoint
    {
        public float radius;
        public Vector3 pos;
    }

    private class d_Pocket
    { 
        public Vector3 barycenter;
        public List<d_PocketPoint> points = new List<d_PocketPoint>();
    }

    private class AsynPocketResult
    {
        public string path;
        public List<d_Pocket> pockets;
    }

    private Queue<AsynPocketResult> finishqueue = new Queue<AsynPocketResult>();
    private object finishguard = new object();

    private Dictionary<string, Molecule> pathtomolmap = new Dictionary<string, Molecule>();

    private string temppath = "";

    private void Start()
    {
        temppath = Application.persistentDataPath;
        if (temppath != null && temppath.Length>0)
        {
            string nexttemppath = temppath + "/temp";
            if (Directory.Exists(nexttemppath) == false)
            {
                Directory.CreateDirectory(nexttemppath);
            }
        }


        Trace.TraceLn("FPocket.Start success!");
    }

    private void OnDestroy()
    {
    }

    void Update()
    {
        AsynPocketResult ret = null;
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
            List<d_Pocket> pocketset = ret.pockets;
            if (pocketset!=null)
                AddPockets(mol, ref pocketset);
            pathtomolmap.Remove(path);
        }
    }

    private Pocket AddPocket(Molecule mol, d_Pocket srcpocket)
    {
        int pocketindex=mol.pockets.Count;
        Pocket pocket = Pocket.CreatePocket(pocketindex, mol.pocketgroup.transform);
        pocket.barycenter = srcpocket.barycenter;
        Color color = MaterialLib.GetRandomColor();
        pocket.color = color;
        color.a = 0.35f;

        for(int i=0;i<srcpocket.points.Count;i++)
        {
            d_PocketPoint srcvert = srcpocket.points[i];

            PocketPoint vert = PocketPoint.CreatePocketPoint(pocket.points.Count, pocket.transform);
            vert.pos = srcvert.pos;
            vert.color = color;
            vert.parent = pocketindex;
            pocket.points.Add(vert);
        }
        return pocket;
    }

    private void AddPockets(Molecule mol,ref List<d_Pocket> pocketset)
    {
        if (mol == null)
            return;

        mol.pockets = new List<Pocket>();
        for(int i=0;i<pocketset.Count;i++)
        {
            d_Pocket _pocket = pocketset[i];
            Pocket pocket=AddPocket(mol, _pocket);
            mol.pockets.Add(pocket);
        }

        mol.scheme.showpockets.Clear();
        for (int i = 0; i < pocketset.Count; i++)
        {
            mol.scheme.showpockets.Add(true);
        }

        mol.Represent();
        PocketPanel.Instance.SetContent(mol);
    }

    private ThreadTask task;


    public void DetectPockets(Molecule mol, string path)
    {
        if (mol == null)
            return;

        if (!File.Exists(path))
            return;

        if (File.Exists(path + "qt"))
            path+= "qt";

        //it is already being processing, wait
        if (pathtomolmap.ContainsKey(path))
            return;

        pathtomolmap[path] = mol;
        //DetectHandler detectfun = _DetectPockets;
        //detectfun.BeginInvoke(path, null, null);

        Action<string> action = new Action<string>(_DetectPockets);
        task = new ThreadTask(action,new object[]{path});
    }



    private d_Pocket _FetchPocketResult(IntPtr _data)
    {
        s_pocket srcpocket = PtrToStruct<s_pocket>(_data);

        //Pocket pocket = Pocket.CreatePocket(mol.pockets.Count, mol.pocketgroup.transform);
        d_Pocket pocket = new d_Pocket();
        pocket.barycenter.x = srcpocket.baryx;
        pocket.barycenter.y = srcpocket.baryy;
        pocket.barycenter.z = -srcpocket.baryz;

        c_lst_vertices vertices = PtrToStruct<c_lst_vertices>(srcpocket.v_lst);
        IntPtr pcur = vertices.first;
        while (pcur != IntPtr.Zero)
        {
            node_vertice vcur = PtrToStruct<node_vertice>(pcur);
            s_vvertice srcvert = PtrToStruct<s_vvertice>(vcur.vertice);

            //PocketPoint vert = PocketPoint.CreatePocketPoint(pocket.points.Count, pocket.transform);
            d_PocketPoint vert = new d_PocketPoint();
            vert.pos.x = srcvert.x;
            vert.pos.y = srcvert.y;
            vert.pos.z = srcvert.z;
            pocket.points.Add(vert);

            pcur = vcur.next;
        }

        return pocket;
    }

    private void _DetectPockets(string path)
    {
        AsynPocketResult ret = new AsynPocketResult();
        ret.pockets = _DetectPocketsImp(path);
        ret.path = path;

        lock (finishguard)
        {
            finishqueue.Enqueue(ret);
        }
    }

    private List<d_Pocket> _DetectPocketsImp(string path)
    {
        IntPtr tpath = Marshal.StringToHGlobalAnsi(temppath);
        if (tpath == IntPtr.Zero)
            return null;
        setTempPath(tpath);

        IntPtr ppath = Marshal.StringToHGlobalAnsi(path);
        if (ppath == IntPtr.Zero)
            return null;

        List<d_Pocket> pockets = new List<d_Pocket>();

        IntPtr ppockets = getPocketsFromPDB(ppath);
        if (ppockets != IntPtr.Zero)
        {
            c_lst_pockets _pockets = PtrToStruct<c_lst_pockets>(ppockets);
            int i = 0;
            IntPtr pcur = _pockets.first;
            while (pcur != IntPtr.Zero)
            {
                node_pocket _cur = PtrToStruct<node_pocket>(pcur);
                d_Pocket pocket = _FetchPocketResult(_cur.pocket);
                pockets.Add(pocket);
                pcur = _cur.next;
                i++;
            }
        }

        releasePockets(ppockets);
        Marshal.FreeHGlobal(tpath);
        Marshal.FreeHGlobal(ppath);

        return pockets;
    }

    void OnGUI()
    {
        int i = 0;
        foreach (KeyValuePair<string, Molecule> pair in pathtomolmap)
        {
            if (pair.Value == null) continue;
            string filename = Path.GetFileName(pair.Key);
            Rect rc = new Rect(30, 30 + i * 30, 300, 30);
            GUI.Label(rc, "FPocket.detecting: " + filename);
            i++;
        }
    }

#else 

    public void DetectPockets(Molecule mol, string path)
    {
    }

#endif
}
