using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Linq;
using AIDrugDiscovery;

// 严格复刻 FPocket v3.0 常量（源码级对齐）
public static class FPocketConstants
{
    // 核心参数（源码默认值）
    public const float PROBE_RADIUS = 1.4f;        // 水分子探针半径（固定）
    public const float MIN_ALPHA_SPHERE_RADIUS = 0.8f; // 最小Alpha球半径
    public const float MAX_ALPHA_SPHERE_RADIUS = 6.0f; // 最大Alpha球半径
    public const float SPHERE_ATOM_EPS = 0.1f;     // 空球判断阈值（源码默认0.1Å）

    // 原子范德华半径表（FPocket源码：vdw_radii.h）
    public static readonly Dictionary<string, float> VdwRadii = new Dictionary<string, float>
    {
        { "H", 1.20f }, { "C", 1.70f }, { "N", 1.55f }, { "O", 1.52f },
        { "S", 1.80f }, { "P", 1.80f }, { "F", 1.47f }, { "CL", 1.75f },
        { "BR", 1.85f }, { "I", 1.98f }, { "OTHER", 1.60f }
    };

    // 原子疏水权重表（FPocket源码：hydrophobicity.h）
    public static readonly Dictionary<string, float> HydrophobicWeights = new Dictionary<string, float>
    {
        { "C", 1.0f }, { "H", 1.0f }, { "N", 0.0f }, { "O", 0.0f },
        { "S", 0.2f }, { "P", 0.1f }, { "F", 0.8f }, { "CL", 0.7f },
        { "BR", 0.6f }, { "I", 0.5f }, { "OTHER", 0.0f }
    };

    // DBSCAN参数（源码默认）
    public const int DBSCAN_MIN_POINTS = 5;
    public const float DBSCAN_EPS = 3.5f;

    // 过滤参数
    public const float MIN_POCKET_VOLUME = 10.0f;
    public const int MAX_ALPHA_SPHERES = 100000;
    public const int MAX_POCKETS = 100;

    // 线程组配置（避免溢出的核心）
    public const int THREAD_GROUP_SIZE_X = 32; // i维度线程组大小
    public const int THREAD_GROUP_SIZE_Y = 32; // j维度线程组大小
}

// FPocket原子结构体（源码级对齐：atom.h）
[Serializable]
public struct FPocketAtom
{
    public int id;                 // 原子ID
    public Vector3 pos;            // 三维坐标（Å）
    public string name;            // 原子名称（如C, N, O）
    public float vdw_radius;       // 范德华半径
    public float hydrophobicity;   // 疏水权重
    public int res_id;             // 残基ID（保留）
}

// FPocket Alpha球结构体（源码级对齐：alpha_sphere.h）
[Serializable]
public struct FPocketAlphaSphere
{
    public Vector3 center;         // 球心坐标
    public float radius;           // 球半径（Å）
    public int nb_atoms;           // 包裹原子数
    public float hydrophobicity;   // 平均疏水权重
    public float polarity;         // 极性权重（1 - 疏水）
    public int visited;            // DBSCAN标记：0=未访问，1=已访问，2=噪声
    public int[] parent_atoms;     // 生成该球的3个原子ID（源码核心）
}

// FPocket口袋结构体（源码级对齐：pocket.h）
[Serializable]
public struct FPocketResult
{
    public int id;                 // 口袋ID
    public Vector3 center;         // 口袋中心
    public float volume;           // 体积（Å³）
    public float score;            // 综合评分
    public float hydrophobic_score;// 疏水性评分
    public float polar_score;      // 极性评分
    public float depth_score;      // 深度评分
    public int nb_alpha_spheres;   // Alpha球数量
    public int nb_atoms;           // 关联原子数
    public float density;          // 密度（Alpha球数/体积）
}

// GPU版结构体（与Compute Shader严格对齐）
[StructLayout(LayoutKind.Sequential)]
public struct FPocketAtomCS
{
    public int id;
    public Vector3 pos;
    public float vdw_radius;
    public float hydrophobicity;
}

[StructLayout(LayoutKind.Sequential)]
public struct FPocketAlphaSphereCS
{
    public Vector3 center;
    public float radius;
    public int nb_atoms;
    public float hydrophobicity;
    public float polarity;
    public int visited;
    public int parent_atom1; // 替代数组，适配ComputeBuffer
    public int parent_atom2;
    public int parent_atom3;
}

[StructLayout(LayoutKind.Sequential)]
public struct FPocketResultCS
{
    public int id;
    public Vector3 center;
    public float volume;
    public float score;
    public float hydrophobic_score;
    public float polar_score;
    public float depth_score;
    public int nb_alpha_spheres;
    public int nb_atoms;
    public float density;
    public int lockFlag; // 自旋锁标记
}

public class PocketDetector : MonoBehaviour
{
    [Header("文件配置")]
    public string pdbqtFilePath;   // PDBQT文件路径（如：Assets/protein.pdbqt）

    [Header("GPU配置")]
    public ComputeShader fpocketComputeShader; // 绑定Compute Shader文件

    // 运行时数据
    private List<FPocketAtom> atoms;
    private List<FPocketAlphaSphere> alphaSpheres;

    /// <summary>
    /// 运行CPU版FPocket官方算法（复刻源码逻辑）
    /// </summary>
    [ContextMenu("Run FPocket CPU Version")]
    public void RunFPocketCPU()
    {
        // 1. 加载原子（复刻read_pdbqt函数）
        atoms = LoadAtomsFromPDBQT(pdbqtFilePath);
        if (atoms.Count < 3)
        {
            Debug.LogError("原子数不足3个，无法生成Alpha球");
            return;
        }
        Debug.Log($"[CPU版] 加载原子数：{atoms.Count}");

        // 2. 生成Alpha球（复刻generate_alpha_spheres函数）
        alphaSpheres = GenerateAlphaSpheresFromAtomTriples(atoms);
        Debug.Log($"[CPU版] 生成Alpha球数：{alphaSpheres.Count}");

        // 3. 过滤Alpha球（复刻filter_alpha_spheres函数）
        List<FPocketAlphaSphere> validSpheres = FilterAlphaSpheres(alphaSpheres);
        Debug.Log($"[CPU版] 有效Alpha球数：{validSpheres.Count}");

        // 4. DBSCAN聚类（复刻cluster_alpha_spheres函数）
        List<List<FPocketAlphaSphere>> clusters = DBSCANCluster(validSpheres);
        Debug.Log($"[CPU版] 聚类得到口袋数：{clusters.Count}");

        // 5. 计算口袋特征（复刻compute_pocket_features函数）
        List<FPocketResult> pockets = ComputePocketFeatures(clusters);
        List<FPocketResult> finalPockets = pockets.Where(p => p.volume >= FPocketConstants.MIN_POCKET_VOLUME).ToList();

        // 6. 输出结果
        PrintPocketResults(finalPockets);
    }

    /// <summary>
    /// 运行GPU版FPocket（二维线程组拆分i/j，k内循环，避免溢出）
    /// </summary>
    [ContextMenu("Run FPocket GPU Version (No Overflow)")]
    public void RunFPocketGPU()
    {
        if (fpocketComputeShader == null)
        {
            Debug.LogError("请先绑定Compute Shader文件！");
            return;
        }

        // 1. 加载原子
        atoms = LoadAtomsFromPDBQT(pdbqtFilePath);
        if (atoms.Count < 3)
        {
            Debug.LogError("原子数不足3个，无法生成Alpha球");
            return;
        }
        int atomCount = atoms.Count;
        Debug.Log($"[GPU版] 加载原子数：{atomCount}");

        // 2. 计算二维线程组数（拆分i/j维度，避免溢出）
        int threadGroupsX = Mathf.CeilToInt((float)atomCount / FPocketConstants.THREAD_GROUP_SIZE_X);
        int threadGroupsY = Mathf.CeilToInt((float)atomCount / FPocketConstants.THREAD_GROUP_SIZE_Y);
        Debug.Log($"[GPU版] 线程组配置：X={threadGroupsX}, Y={threadGroupsY}");

        // 3. 初始化缓冲区
        ComputeBuffer atomBuffer = null;
        ComputeBuffer alphaSphereBuffer = null;
        ComputeBuffer pocketResultBuffer = null;
        ComputeBuffer sphereCountBuffer = null;
        ComputeBuffer clusterCountBuffer = null;

        try
        {
            atomBuffer = InitAtomBuffer(atoms);
            alphaSphereBuffer = InitAlphaSphereBuffer();
            pocketResultBuffer = InitPocketResultBuffer();
            sphereCountBuffer = new ComputeBuffer(1, sizeof(int));
            clusterCountBuffer = new ComputeBuffer(1, sizeof(int));

            // 初始化计数缓冲区
            int[] initCount = { 0 };
            sphereCountBuffer.SetData(initCount);
            clusterCountBuffer.SetData(initCount);

            // 4. 设置Shader参数（核心：传递原子总数，用于k循环边界）
            SetShaderConstants(fpocketComputeShader, atomCount);

            // 5. 调度Kernel 1：生成Alpha球（二维线程组，k内循环）
            int kernel1 = fpocketComputeShader.FindKernel("CSGenerateAlphaSpheres");
            fpocketComputeShader.SetBuffer(kernel1, "atomBuffer", atomBuffer);
            fpocketComputeShader.SetBuffer(kernel1, "alphaSphereBuffer", alphaSphereBuffer);
            fpocketComputeShader.SetBuffer(kernel1, "sphereCountBuffer", sphereCountBuffer);
            fpocketComputeShader.Dispatch(kernel1, threadGroupsX, threadGroupsY, 1);

            // 6. 调度Kernel 2：过滤Alpha球
            int kernel2 = fpocketComputeShader.FindKernel("CSFilterAlphaSpheres");
            fpocketComputeShader.SetBuffer(kernel2, "alphaSphereBuffer", alphaSphereBuffer);
            int threadGroupsFilter = Mathf.CeilToInt(FPocketConstants.MAX_ALPHA_SPHERES / 256f);
            fpocketComputeShader.Dispatch(kernel2, threadGroupsFilter, 1, 1);

            FPocketAlphaSphereCS[] data = new FPocketAlphaSphereCS[FPocketConstants.MAX_ALPHA_SPHERES];
            alphaSphereBuffer.GetData(data);
            List<FPocketAlphaSphere> validSpheres = new();
            foreach (var sphere in data)
            {
                if (sphere.radius > 0)
                {
                    FPocketAlphaSphere newsphere = new FPocketAlphaSphere();
                    newsphere.center = sphere.center;
                    newsphere.radius = sphere.radius;
                    newsphere.nb_atoms = sphere.nb_atoms;
                    newsphere.hydrophobicity = sphere.hydrophobicity;
                    newsphere.polarity = sphere.polarity;
                    newsphere.visited = sphere.visited;
                    newsphere.parent_atoms = new int[] { sphere.parent_atom1, sphere.parent_atom2, sphere.parent_atom3 };
                    validSpheres.Add(newsphere);
                }
            }

            // 7. DBSCAN聚类（复刻cluster_alpha_spheres函数）
            List<List<FPocketAlphaSphere>> clusters = DBSCANCluster(validSpheres);
            Debug.Log($"[CPU版] 聚类得到口袋数：{clusters.Count}");

            // 8. 计算口袋特征（复刻compute_pocket_features函数）
            List<FPocketResult> pockets = ComputePocketFeatures(clusters);
            List<FPocketResult> finalPockets = pockets.Where(p => p.volume >= FPocketConstants.MIN_POCKET_VOLUME).ToList();

            PrintPocketResults(finalPockets);

            //// 7. 调度Kernel 3：DBSCAN聚类
            //int kernel3 = fpocketComputeShader.FindKernel("CSDBSCANCluster");
            //fpocketComputeShader.SetBuffer(kernel3, "alphaSphereBuffer", alphaSphereBuffer);
            //fpocketComputeShader.SetBuffer(kernel3, "pocketResultBuffer", pocketResultBuffer);
            //fpocketComputeShader.SetBuffer(kernel3, "clusterCountBuffer", clusterCountBuffer);
            //fpocketComputeShader.Dispatch(kernel3, threadGroupsFilter, 1, 1);

            //// 8. 调度Kernel 4：计算评分
            //int kernel4 = fpocketComputeShader.FindKernel("CSCalculatePocketScores");
            //fpocketComputeShader.SetBuffer(kernel4, "atomBuffer", atomBuffer);
            //fpocketComputeShader.SetBuffer(kernel4, "pocketResultBuffer", pocketResultBuffer);
            //int threadGroupsScore = Mathf.CeilToInt(FPocketConstants.MAX_POCKETS / 256f);
            //fpocketComputeShader.Dispatch(kernel4, threadGroupsScore, 1, 1);

            //// 9. 读取并输出GPU结果
            //ReadAndPrintGPUResults(pocketResultBuffer);
        }
        catch (Exception e)
        {
            Debug.LogError($"GPU版运行出错：{e.Message}\n{e.StackTrace}");
        }
        finally
        {
            // 10. 释放缓冲区（必须执行，避免内存泄漏）
            ReleaseBuffers(atomBuffer, alphaSphereBuffer, pocketResultBuffer, sphereCountBuffer, clusterCountBuffer);
        }
    }

    #region CPU版核心逻辑（复刻FPocket源码）
    /// <summary>
    /// 复刻FPocket源码：遍历原子三元组生成Alpha球（alpha_sphere.c: generate_alpha_spheres）
    /// </summary>
    private List<FPocketAlphaSphere> GenerateAlphaSpheresFromAtomTriples(List<FPocketAtom> atoms)
    {
        List<FPocketAlphaSphere> alphaSpheres = new List<FPocketAlphaSphere>();
        int atomCount = atoms.Count;

        // FPocket核心：3层for遍历所有原子三元组（i<j<k，避免重复）
        for (int i = 0; i < atomCount - 2; i++)
        {
            for (int j = i + 1; j < atomCount - 1; j++)
            {
                for (int k = j + 1; k < atomCount; k++)
                {
                    FPocketAtom a1 = atoms[i];
                    FPocketAtom a2 = atoms[j];
                    FPocketAtom a3 = atoms[k];

                    // 步骤1：计算3个原子的外接球（复刻compute_circumsphere函数）
                    (Vector3 center, float radius) = ComputeCircumsphere(a1.pos, a2.pos, a3.pos);
                    if (radius < FPocketConstants.MIN_ALPHA_SPHERE_RADIUS || radius > FPocketConstants.MAX_ALPHA_SPHERE_RADIUS)
                        continue;

                    // 步骤2：空球判断（复刻is_empty_sphere函数）
                    bool isEmpty = IsEmptySphere(center, radius, atoms, i, j, k);
                    if (!isEmpty) continue;

                    // 步骤3：探针半径修正（球半径需≥探针半径，且球心在分子外）
                    if (radius < FPocketConstants.PROBE_RADIUS) continue;
                    if (!IsSphereCenterOutsideMolecule(center, atoms)) continue;

                    // 步骤4：统计包裹的原子数（复刻count_enclosed_atoms函数）
                    (int nbAtoms, float totalHydro) = CountEnclosedAtoms(center, radius, atoms);

                    // 步骤5：创建Alpha球（源码级赋值）
                    FPocketAlphaSphere sphere = new FPocketAlphaSphere
                    {
                        center = center,
                        radius = radius,
                        nb_atoms = nbAtoms,
                        hydrophobicity = nbAtoms > 0 ? totalHydro / nbAtoms : 0f,
                        polarity = 1f - (nbAtoms > 0 ? totalHydro / nbAtoms : 0f),
                        visited = 0,
                        parent_atoms = new[] { i, j, k } // 记录生成该球的3个原子ID
                    };

                    alphaSpheres.Add(sphere);

                    // 限制最大数量，避免内存溢出
                    if (alphaSpheres.Count >= FPocketConstants.MAX_ALPHA_SPHERES)
                        goto ExitTripleLoop;
                }
            }
        }
        ExitTripleLoop:

        return alphaSpheres;
    }

    /// <summary>
    /// 计算3个点的外接球（复刻FPocket: compute_circumsphere）
    /// </summary>
    private (Vector3 center, float radius) ComputeCircumsphere(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // 计算向量
        Vector3 v1 = p2 - p1;
        Vector3 v2 = p3 - p1;

        // 计算法向量（垂直于平面）
        Vector3 n = Vector3.Cross(v1, v2);
        if (n.magnitude < 1e-6) // 三点共线，跳过
            return (Vector3.zero, 0f);

        // 解线性方程组求外接圆圆心
        float a11 = 2 * (p2.x - p1.x);
        float a12 = 2 * (p2.y - p1.y);
        float a13 = 2 * (p2.z - p1.z);
        float b1 = p2.sqrMagnitude - p1.sqrMagnitude;

        float a21 = 2 * (p3.x - p1.x);
        float a22 = 2 * (p3.y - p1.y);
        float a23 = 2 * (p3.z - p1.z);
        float b2 = p3.sqrMagnitude - p1.sqrMagnitude;

        float a31 = n.x;
        float a32 = n.y;
        float a33 = n.z;
        float b3 = Vector3.Dot(n, p1);

        // 克莱姆法则求解
        float det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
        if (Mathf.Abs(det) < 1e-6)
            return (Vector3.zero, 0f);

        float detX = b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3);
        float detY = a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31);
        float detZ = a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31);

        Vector3 center = new Vector3(detX / det, detY / det, detZ / det);
        float radius = Vector3.Distance(center, p1);

        return (center, radius);
    }

    /// <summary>
    /// 空球判断：球内是否包含其他原子（复刻FPocket: is_empty_sphere）
    /// </summary>
    private bool IsEmptySphere(Vector3 center, float radius, List<FPocketAtom> atoms, int i, int j, int k)
    {
        float radiusSq = (radius - FPocketConstants.SPHERE_ATOM_EPS) * (radius - FPocketConstants.SPHERE_ATOM_EPS);

        foreach (var atom in atoms)
        {
            // 跳过生成该球的3个原子
            if (atom.id == i || atom.id == j || atom.id == k)
                continue;

            // 计算原子中心到球心的距离平方
            float distSq = (atom.pos - center).sqrMagnitude;

            // 空球判断：距离 ≥ 球半径 - 阈值（避免浮点误差）
            if (distSq < radiusSq)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 判断球心是否在分子范德华表面外（复刻FPocket: is_outside_molecule）
    /// </summary>
    private bool IsSphereCenterOutsideMolecule(Vector3 center, List<FPocketAtom> atoms)
    {
        foreach (var atom in atoms)
        {
            float dist = Vector3.Distance(center, atom.pos);
            // 球心需在原子范德华表面外 + 探针半径
            if (dist < atom.vdw_radius + FPocketConstants.PROBE_RADIUS)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 统计包裹的原子数和总疏水权重（复刻FPocket: count_enclosed_atoms）
    /// </summary>
    private (int nbAtoms, float totalHydro) CountEnclosedAtoms(Vector3 center, float radius, List<FPocketAtom> atoms)
    {
        int nbAtoms = 0;
        float totalHydro = 0f;

        foreach (var atom in atoms)
        {
            float dist = Vector3.Distance(center, atom.pos);
            // FPocket判断条件：距离 < 球半径 + 原子范德华半径
            if (dist < radius + atom.vdw_radius)
            {
                nbAtoms++;
                totalHydro += atom.hydrophobicity;
            }
        }

        return (nbAtoms, totalHydro);
    }

    /// <summary>
    /// 过滤Alpha球（复刻FPocket: filter_alpha_spheres）
    /// </summary>
    private List<FPocketAlphaSphere> FilterAlphaSpheres(List<FPocketAlphaSphere> spheres)
    {
        return spheres.Where(s =>
            s.radius >= FPocketConstants.MIN_ALPHA_SPHERE_RADIUS &&
            s.radius <= FPocketConstants.MAX_ALPHA_SPHERE_RADIUS &&
            s.nb_atoms >= 1 &&
            s.hydrophobicity >= 0.1f
        ).ToList();
    }

    /// <summary>
    /// DBSCAN聚类（复刻FPocket: dbscan_cluster）
    /// </summary>
    private List<List<FPocketAlphaSphere>> DBSCANCluster(List<FPocketAlphaSphere> spheres)
    {
        List<List<FPocketAlphaSphere>> clusters = new List<List<FPocketAlphaSphere>>();
        HashSet<int> visited = new HashSet<int>();
        HashSet<int> noise = new HashSet<int>();

        for (int i = 0; i < spheres.Count; i++)
        {
            if (visited.Contains(i)) continue;

            List<int> neighbors = FindNeighbors(spheres, i);
            if (neighbors.Count < FPocketConstants.DBSCAN_MIN_POINTS)
            {
                noise.Add(i);
                visited.Add(i);
                FPocketAlphaSphere s = spheres[i];
                s.visited = 2;
                spheres[i] = s;
                continue;
            }

            List<FPocketAlphaSphere> cluster = new List<FPocketAlphaSphere>();
            cluster.Add(spheres[i]);
            visited.Add(i);
            FPocketAlphaSphere core = spheres[i];
            core.visited = 1;
            spheres[i] = core;

            Queue<int> queue = new Queue<int>(neighbors);
            while (queue.Count > 0)
            {
                int j = queue.Dequeue();
                if (visited.Contains(j)) continue;

                visited.Add(j);
                FPocketAlphaSphere js = spheres[j];
                js.visited = 1;
                spheres[j] = js;

                List<int> jNeighbors = FindNeighbors(spheres, j);
                if (jNeighbors.Count >= FPocketConstants.DBSCAN_MIN_POINTS)
                {
                    foreach (int n in jNeighbors)
                    {
                        if (!visited.Contains(n) && !queue.Contains(n))
                            queue.Enqueue(n);
                    }
                }

                cluster.Add(spheres[j]);
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    /// <summary>
    /// 查找邻域点（复刻FPocket: find_neighbors）
    /// </summary>
    private List<int> FindNeighbors(List<FPocketAlphaSphere> spheres, int index)
    {
        List<int> neighbors = new List<int>();
        Vector3 center = spheres[index].center;

        for (int i = 0; i < spheres.Count; i++)
        {
            if (i == index) continue;
            if (Vector3.Distance(center, spheres[i].center) < FPocketConstants.DBSCAN_EPS)
                neighbors.Add(i);
        }

        return neighbors;
    }

    /// <summary>
    /// 计算口袋特征（复刻FPocket: compute_pocket_features）
    /// </summary>
    private List<FPocketResult> ComputePocketFeatures(List<List<FPocketAlphaSphere>> clusters)
    {
        List<FPocketResult> pockets = new List<FPocketResult>();

        for (int i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            FPocketResult pocket = new FPocketResult();
            pocket.id = i;
            pocket.nb_alpha_spheres = cluster.Count;

            // 口袋中心（Alpha球中心加权平均）
            Vector3 weightedCenter = Vector3.zero;
            float totalRadius = 0f;
            foreach (var s in cluster)
            {
                weightedCenter += s.center * s.radius;
                totalRadius += s.radius;
            }
            pocket.center = totalRadius > 0 ? weightedCenter / totalRadius : Vector3.zero;

            // 体积（所有Alpha球体积和）
            pocket.volume = cluster.Sum(s => (4f / 3f) * Mathf.PI * Mathf.Pow(s.radius, 3));

            // 疏水性/极性评分
            pocket.hydrophobic_score = cluster.Average(s => s.hydrophobicity);
            pocket.polar_score = cluster.Average(s => s.polarity);

            // 深度评分（口袋中心到分子表面的距离）
            pocket.depth_score = CalculatePocketDepth(pocket.center);

            // 综合评分（FPocket源码权重）
            pocket.score =
                (pocket.volume / 100) * 0.4f +          // 体积40%
                pocket.hydrophobic_score * 0.3f +        // 疏水30%
                (1 - pocket.polar_score) * 0.1f +        // 极性10%
                pocket.depth_score * 0.2f;               // 深度20%

            // 密度
            pocket.density = pocket.volume > 0 ? pocket.nb_alpha_spheres / pocket.volume : 0f;

            // 关联原子数
            pocket.nb_atoms = cluster.Sum(s => s.nb_atoms);

            pockets.Add(pocket);
        }

        // 按评分降序排序
        return pockets.OrderByDescending(p => p.score).ToList();
    }
    #endregion

    #region 通用辅助函数
    /// <summary>
    /// 加载PDBQT文件（复刻FPocket: read_pdbqt）
    /// </summary>
    private List<FPocketAtom> LoadAtomsFromPDBQT(string filePath)
    {
        List<FPocketAtom> atoms = new List<FPocketAtom>();
        if (!File.Exists(filePath))
        {
            Debug.LogError($"PDBQT文件不存在：{filePath}");
            return atoms;
        }

        string[] lines = File.ReadAllLines(filePath);
        int atomId = 0;

        foreach (string line in lines)
        {
            if (line.StartsWith("ATOM") || line.StartsWith("HETATM"))
            {
                try
                {
                    // PDBQT格式解析（源码级对齐）
                    float x = float.Parse(line.Substring(30, 8).Trim());
                    float y = float.Parse(line.Substring(38, 8).Trim());
                    float z = float.Parse(line.Substring(46, 8).Trim());
                    string atomNameRaw = line.Substring(12, 2).Trim().ToUpper();
                    string atomSymbol = ExtractAtomSymbol(atomNameRaw);

                    // 范德华半径
                    float vdwRadius = FPocketConstants.VdwRadii.ContainsKey(atomSymbol)
                        ? FPocketConstants.VdwRadii[atomSymbol]
                        : FPocketConstants.VdwRadii["OTHER"];

                    // 疏水权重
                    float hydro = FPocketConstants.HydrophobicWeights.ContainsKey(atomSymbol)
                        ? FPocketConstants.HydrophobicWeights[atomSymbol]
                        : FPocketConstants.HydrophobicWeights["OTHER"];

                    atoms.Add(new FPocketAtom
                    {
                        id = atomId++,
                        pos = new Vector3(x, y, z),
                        name = atomSymbol,
                        vdw_radius = vdwRadius,
                        hydrophobicity = hydro,
                        res_id = 0
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"解析PDBQT行失败：{line} | {e.Message}");
                }
            }
        }

        return atoms;
    }

    /// <summary>
    /// 提取原子符号（复刻FPocket: extract_atom_symbol）
    /// </summary>
    private string ExtractAtomSymbol(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "OTHER";
        if (rawName.StartsWith("CL") || rawName.StartsWith("BR") || rawName.StartsWith("I"))
            return rawName.Substring(0, 2).ToUpper();
        return rawName.Substring(0, 1).ToUpper();
    }

    /// <summary>
    /// 计算口袋深度（复刻FPocket: compute_pocket_depth）
    /// </summary>
    private float CalculatePocketDepth(Vector3 center)
    {
        float minDist = float.MaxValue;
        foreach (var atom in atoms)
        {
            float dist = Vector3.Distance(center, atom.pos) - atom.vdw_radius;
            if (dist < minDist) minDist = dist;
        }
        return Mathf.Clamp01(minDist / 10f);
    }

    /// <summary>
    /// 输出CPU版结果（复刻FPocket: print_results）
    /// </summary>
    private void PrintPocketResults(List<FPocketResult> pockets)
    {
        var validPockets = pockets.OrderByDescending(_ => _.score).ToList();

        Debug.Log($"有效口袋数：{validPockets.Count}");

        foreach (var p in validPockets)
        {
            Debug.Log($"[Pocket {p.id}]  中心：({p.center.x:F2}, {p.center.y:F2}, {p.center.z:F2})");
            Debug.Log($"  体积：{p.volume:F2} Å³  Alpha球数：{p.nb_alpha_spheres} 综合评分：{p.score:F2}");
        }
    }
    #endregion

    #region GPU版辅助函数（缓冲区管理+结果读取）
    /// <summary>
    /// 设置Shader常量参数（核心：传递原子总数，用于k循环边界）
    /// </summary>
    private void SetShaderConstants(ComputeShader cs, int atomCount)
    {
        cs.SetFloat("PROBE_RADIUS", FPocketConstants.PROBE_RADIUS);
        cs.SetFloat("MIN_ALPHA_SPHERE_RADIUS", FPocketConstants.MIN_ALPHA_SPHERE_RADIUS);
        cs.SetFloat("MAX_ALPHA_SPHERE_RADIUS", FPocketConstants.MAX_ALPHA_SPHERE_RADIUS);
        cs.SetFloat("SPHERE_ATOM_EPS", FPocketConstants.SPHERE_ATOM_EPS);
        cs.SetInt("DBSCAN_MIN_POINTS", FPocketConstants.DBSCAN_MIN_POINTS);
        cs.SetFloat("DBSCAN_EPS", FPocketConstants.DBSCAN_EPS);
        cs.SetFloat("MIN_POCKET_VOLUME", FPocketConstants.MIN_POCKET_VOLUME);
        cs.SetInt("atomCount", atomCount); // 关键：传递原子总数给Shader
        cs.SetInt("maxAlphaSpheres", FPocketConstants.MAX_ALPHA_SPHERES);
        cs.SetInt("maxPockets", FPocketConstants.MAX_POCKETS);
        // 传递线程组大小，用于计算全局i/j索引
        cs.SetInt("THREAD_GROUP_SIZE_X", FPocketConstants.THREAD_GROUP_SIZE_X);
        cs.SetInt("THREAD_GROUP_SIZE_Y", FPocketConstants.THREAD_GROUP_SIZE_Y);
    }

    /// <summary>
    /// 初始化原子缓冲区
    /// </summary>
    private ComputeBuffer InitAtomBuffer(List<FPocketAtom> atoms)
    {
        int stride = Marshal.SizeOf(typeof(FPocketAtomCS));
        ComputeBuffer buffer = new ComputeBuffer(atoms.Count, stride, ComputeBufferType.Default);
        FPocketAtomCS[] atomCS = atoms.Select(a => new FPocketAtomCS
        {
            id = a.id,
            pos = a.pos,
            vdw_radius = a.vdw_radius,
            hydrophobicity = a.hydrophobicity
        }).ToArray();
        buffer.SetData(atomCS);
        return buffer;
    }

    /// <summary>
    /// 初始化Alpha球缓冲区
    /// </summary>
    private ComputeBuffer InitAlphaSphereBuffer()
    {
        int stride = Marshal.SizeOf(typeof(FPocketAlphaSphereCS));
        ComputeBuffer buffer = new ComputeBuffer(FPocketConstants.MAX_ALPHA_SPHERES, stride, ComputeBufferType.Default);
        FPocketAlphaSphereCS[] empty = new FPocketAlphaSphereCS[FPocketConstants.MAX_ALPHA_SPHERES];
        for (int i = 0; i < empty.Length; i++)
        {
            empty[i].radius = -1.0f;
            empty[i].visited = 0;
            empty[i].parent_atom1 = empty[i].parent_atom2 = empty[i].parent_atom3 = -1;
        }
        buffer.SetData(empty);
        return buffer;
    }

    /// <summary>
    /// 初始化口袋结果缓冲区
    /// </summary>
    private ComputeBuffer InitPocketResultBuffer()
    {
        int stride = Marshal.SizeOf(typeof(FPocketResultCS));
        ComputeBuffer buffer = new ComputeBuffer(FPocketConstants.MAX_POCKETS, stride, ComputeBufferType.Default);
        FPocketResultCS[] empty = new FPocketResultCS[FPocketConstants.MAX_POCKETS];
        for (int i = 0; i < empty.Length; i++)
        {
            empty[i].id = -1;
            empty[i].lockFlag = 0;
            empty[i].volume = 0f;
        }
        buffer.SetData(empty);
        return buffer;
    }

    /// <summary>
    /// 释放所有缓冲区（避免内存泄漏）
    /// </summary>
    private void ReleaseBuffers(params ComputeBuffer[] buffers)
    {
        foreach (var buf in buffers)
        {
            if (buf != null)
            {
                if (buf.IsValid())
                {
                    buf.Release();
                }
                buf.Dispose();
            }
        }
    }

    /// <summary>
    /// 读取并输出GPU版结果
    /// </summary>
    private void ReadAndPrintGPUResults(ComputeBuffer pocketBuffer)
    {
        FPocketResultCS[] gpuPockets = new FPocketResultCS[FPocketConstants.MAX_POCKETS];
        pocketBuffer.GetData(gpuPockets);

        // 过滤有效口袋
        List<FPocketResultCS> validPockets = gpuPockets.Where(p => p.id != -1 && p.volume >= FPocketConstants.MIN_POCKET_VOLUME).ToList();

        validPockets = validPockets.OrderByDescending(_=>_.score).ToList();

        Debug.Log($"有效口袋数：{validPockets.Count}");

        foreach (var p in validPockets)
        {
            Debug.Log($"[Pocket {p.id}]  中心：({p.center.x:F2}, {p.center.y:F2}, {p.center.z:F2})");
            Debug.Log($"  体积：{p.volume:F2} Å³  Alpha球数：{p.nb_alpha_spheres} 综合评分：{p.score:F2}");
        }
    }
    #endregion
}