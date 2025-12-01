using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public struct AtomInfo
{
    public int ID; //原子索引
    public string Name; //元素名
    public Vector2 Position; //在元素表中的位置（行，列）
    public float nucleusRadScale; //原子核质子中子大小比例
    public float nucleusDisScale; //原子核质子中子离中心距离比例
    public float orbitStartDisScale; //电子轨道最内层半径比例
    public float orbitStepDisScale; //电子轨道两层之间半径差比例
    public Vector2[] Nucleus; //原子核分布（每层的质子数和中子数）
    public int[] Orbit; //轨道分布（每个轨道的电子数目）
}

public class AtomInfoReader
{
    public static Dictionary<string, AtomInfo> atomInfos;

    public static void loadAtomInfo()
    {
        if (atomInfos != null) return;

        atomInfos = new Dictionary<string, AtomInfo>();

        TextAsset ta = Resources.Load<TextAsset>("AtomList");
        StringReader sr = new StringReader(ta.text);
        while (true)
        {
            string sLine = sr.ReadLine();
            if (sLine == null) break;
            if (sLine == "") continue;
            string splitStr = " ";
            string[] subStrings=sLine.Split(splitStr.ToCharArray());
            if (subStrings.Length < 7) continue;

            AtomInfo newAtom = new AtomInfo();
            newAtom.ID = int.Parse(subStrings[0]);
            newAtom.Name = subStrings[1];
            newAtom.Position = new Vector2(int.Parse(subStrings[2]),int.Parse(subStrings[3]));
            newAtom.nucleusRadScale = float.Parse(subStrings[4]);
            newAtom.nucleusDisScale = float.Parse(subStrings[5]);
            newAtom.orbitStartDisScale = float.Parse(subStrings[6]);
            newAtom.orbitStepDisScale = float.Parse(subStrings[7]);
            int nucleusLayer = int.Parse(subStrings[8]);
            int strPtr = 9;
            newAtom.Nucleus = new Vector2[nucleusLayer];
            for (int i = 0; i < nucleusLayer; i++)
            {
                newAtom.Nucleus[i] =new Vector2(int.Parse(subStrings[strPtr]),int.Parse(subStrings[strPtr+1]));
                strPtr += 2;
            }

            int orbitNum = int.Parse(subStrings[strPtr]);
            strPtr++;
            newAtom.Orbit = new int[orbitNum];
            for (int i = 0; i < orbitNum; i++)
            {
                newAtom.Orbit[i] = int.Parse(subStrings[strPtr]);
                strPtr++;
            }

            atomInfos.Add(newAtom.Name, newAtom);

        }
    }

}
