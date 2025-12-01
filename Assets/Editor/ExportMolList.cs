using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using idock;

public static class ExportMolList
{
    static string[] exts = new string[] { ".pdb", ".ent", ".mol2", ".ml2", ".sy2", ".pdbqt" };

    //[MenuItem("Tools/Build Molecule List")]
    //private static void BuildMoleculeList()
    //{
    //    string folder = Application.dataPath + "/Resources/DownloadPDB";
    //    if (!Directory.Exists(folder))
    //        return;

    //    List<string> finallist = new List<string>();
    //    List<string> mollist = new List<string>();
    //    foreach (string ext in exts)
    //    {
    //        string[] files = Directory.GetFiles(folder, "*" + ext + ".txt");
    //        mollist.AddRange(files);
    //    }
    //    foreach (string molfile in mollist)
    //    {
    //        finallist.Add(Path.GetFileNameWithoutExtension(molfile));
    //    }
    //    string listpath = Application.dataPath + "/Resources/mollist.txt";
    //    File.WriteAllLines(listpath, finallist.ToArray());
    //}

    [MenuItem("Tools/Build Match Table")]
    private static void BuildMatchTable()
    {
        string examplefolder = EditorUtility.OpenFolderPanel("select idock example folder", Application.dataPath, "");
        if (examplefolder == null || examplefolder.Length <= 0)
            return;

        if (!Directory.Exists(examplefolder))
            return;

        List<string> outlines = new List<string>();
        string firstline = "#,receptor,ligand,centerx,centery,centerz,sizex,sizey,sizez";
        outlines.Add(firstline);

        string[] confs = Directory.GetFiles(examplefolder, "*.conf", SearchOption.AllDirectories);
        foreach (string cfgpath in confs)
        {
            iConfig cfg = new iConfig(cfgpath);
            string cfgfolder = Path.GetDirectoryName(cfgpath);
            string receptor_path = "";
            string ligand_folder = "";
            Vec3 center = Vec3.zero, size = Vec3.zero;
            receptor_path = cfgfolder + "/" + cfg.GetValue("receptor");
            ligand_folder = cfgfolder + "/" + cfg.GetValue("ligand");
            receptor_path = Path.GetFullPath(receptor_path);
            ligand_folder = Path.GetFullPath(ligand_folder);
            float.TryParse(cfg.GetValue("center_x"), out center.x);
            float.TryParse(cfg.GetValue("center_y"), out center.y);
            float.TryParse(cfg.GetValue("center_z"), out center.z);
            float.TryParse(cfg.GetValue("size_x"), out size.x);
            float.TryParse(cfg.GetValue("size_y"), out size.y);
            float.TryParse(cfg.GetValue("size_z"), out size.z);

            if (Directory.Exists(ligand_folder))
            {
                string[] ligand_files = Directory.GetFiles(ligand_folder, "*.pdbqt", SearchOption.AllDirectories);
                if (ligand_files == null)
                    continue;

                foreach (string ligand_path in ligand_files)
                {
                    string outline = "i," +
                        Path.GetFileNameWithoutExtension(receptor_path) + "," +
                        Path.GetFileNameWithoutExtension(ligand_path) + "," +
                        center.x.ToString("F3") + "," +
                        center.y.ToString("F3") + "," +
                        center.z.ToString("F3") + "," +
                        size.x.ToString("F3") + "," +
                        size.y.ToString("F3") + "," +
                        size.z.ToString("F3");
                    outlines.Add(outline);
                }
            }


        }

        string tablepath = Application.dataPath + "/Resources/matchtable.txt";
        File.WriteAllLines(tablepath, outlines.ToArray());
    }
}
