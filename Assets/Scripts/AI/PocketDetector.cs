using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

namespace AIDrugDiscovery
{


    // 对齐FPocket官方参数的常量
    public static class FPocketConstants
    {
        // 基础参数
        public const float PROBE_RADIUS = 1.4f;       // 溶剂探针半径（水）
        public const float GRID_STEP = 0.5f;          // 表面网格步长（FPocket默认0.5Å）
        public const float ALPHA_SPHERE_MIN_RADIUS = 0.8f; // alpha球最小半径
        public const float ALPHA_SPHERE_MAX_RADIUS = 6.0f; // alpha球最大半径

        // 聚类参数（DBSCAN）
        public const int DBSCAN_MIN_POINTS = 5;       // 最小聚类点数
        public const float DBSCAN_EPS = 3.5f;         // 邻域半径（FPocket默认3.5Å）

        // 评分参数（FPocket权重）
        public const float SCORE_VOLUME_WEIGHT = 0.4f;    // 体积权重
        public const float SCORE_HYDROPHOBIC_WEIGHT = 0.3f; // 疏水性权重
        public const float SCORE_POLAR_WEIGHT = 0.1f;      // 极性权重
        public const float SCORE_DEPTH_WEIGHT = 0.2f;      // 口袋深度权重

        // 过滤参数
        public const float MIN_POCKET_VOLUME = 10.0f;  // 最小口袋体积（Å³）
        public const float MIN_ALPHA_SPHERE_DENSITY = 0.05f; // 最小alpha球密度

        // 缓冲区尺寸限制
        public const int MAX_ALPHA_SPHERES = 2000000;   // 最大Alpha球数
        public const int MAX_POCKETS = 100;            // 最大口袋数
    }

    // 原子数据结构
    [Serializable]
    public struct AtomData
    {
        public Vector3 position; // 原子坐标
        public float vdwRadius;  // 范德华半径
        public float charge;     // 电荷
        public int atomType;     // 0=疏水(C/H), 1=极性(N/O/S/P), 2=其他
        public float hydrophobicity; // 疏水权重（C=1.0，其他=0）
    }

    // Alpha球结构（FPocket核心：包围口袋的空球）
    public struct AlphaSphere
    {
        public Vector3 center;       // Alpha球中心
        public float radius;         // Alpha球半径
        public int enclosedAtoms;    // 球内包裹的原子数
        public float hydrophobicity; // 球内疏水原子占比
        public float polarity;       // 球内极性原子占比
        public int visited;          // DBSCAN标记：0=未访问，1=已访问，2=噪声
    }

    // FPocket风格的口袋结果（扩展评分维度）
    [Serializable]
    public struct FPocketResult
    {
        public Vector3 center;          // 口袋中心点
        public float volume;            // 口袋体积（alpha球体积和）
        public float score;             // 综合评分
        public float hydrophobicScore;  // 疏水性评分
        public float polarScore;        // 极性评分
        public float depthScore;        // 口袋深度评分
        public int alphaSphereCount;    // alpha球数量
        public int atomCount;           // 关联原子数
        public int id;                  // 口袋ID
        public float density;           // 口袋密度（alpha球数/体积）
        public int lockFlag;            // 自旋锁标记：0=未锁定，1=已锁定
    }

    public class PocketDetector : MonoBehaviour
    {
        public ComputeShader fpocketComputeShader; // 赋值优化后的Compute Shader
        public string pdbqtFilePath; // PDBQT文件路径（如"Assets/protein.pdbqt"）

        // Compute Buffer定义（需手动释放）
        private ComputeBuffer atomBuffer;
        private ComputeBuffer alphaSphereBuffer;
        private ComputeBuffer pocketResultBuffer;
        private ComputeBuffer clusterCountBuffer;

        /// <summary>
        /// 启动纯C#版本口袋检测
        /// </summary>
        [ContextMenu("Run FPocket C# Detection")]
        public void RunFPocketCSharpDetection()
        {
            // 原有C#版本逻辑（无修改）
            List<AtomData> atoms = LoadAndPreprocessPDBQT(pdbqtFilePath);
            if (atoms.Count == 0) return;

            Bounds moleculeBounds = GetMoleculeBounds(atoms);
            Debug.Log($"分子边界框：{moleculeBounds.min} ~ {moleculeBounds.max}");

            List<AlphaSphere> alphaSpheres = GenerateAlphaSpheres(atoms, moleculeBounds);
            Debug.Log($"生成Alpha球数量：{alphaSpheres.Count}");

            List<AlphaSphere> validAlphaSpheres = FilterAlphaSpheres(alphaSpheres);
            Debug.Log($"过滤后有效Alpha球数量：{validAlphaSpheres.Count}");

            List<List<AlphaSphere>> pocketClusters = DBSCANCluster(validAlphaSpheres);
            Debug.Log($"DBSCAN聚类得到口袋数：{pocketClusters.Count}");

            List<FPocketResult> pocketResults = CalculatePocketFeatures(pocketClusters, atoms);
            List<FPocketResult> finalPockets = pocketResults.Where(p => p.volume >= FPocketConstants.MIN_POCKET_VOLUME).ToList();

            PrintFPocketResults(finalPockets, "C#版本");
        }

        /// <summary>
        /// 启动Compute Shader版本FPocket检测（新增核心逻辑）
        /// </summary>
        [ContextMenu("Run FPocket Compute Shader Detection")]
        public void RunFPocketComputeShaderDetection()
        {
            // 1. 加载并预处理原子数据
            List<AtomData> atoms = LoadAndPreprocessPDBQT(pdbqtFilePath);
            if (atoms.Count == 0)
            {
                Debug.LogError("原子数据加载失败，终止Compute Shader检测");
                return;
            }

            // 2. 计算分子边界框
            Bounds moleculeBounds = GetMoleculeBounds(atoms);
            Debug.Log($"[Compute Shader] 分子边界框：{moleculeBounds.min} ~ {moleculeBounds.max}");

            // 3. 计算网格点总数（用于初始化Alpha球缓冲区）
            int gridPointCount = CalculateGridPointCount(moleculeBounds);
            if (gridPointCount > FPocketConstants.MAX_ALPHA_SPHERES)
            {
                Debug.LogWarning($"网格点数量({gridPointCount})超过最大限制({FPocketConstants.MAX_ALPHA_SPHERES})，将截断");
                gridPointCount = FPocketConstants.MAX_ALPHA_SPHERES;
            }

            try
            {
                // 4. 初始化所有Compute Buffer
                InitComputeBuffers(atoms, gridPointCount);

                // 5. 分步调度Compute Shader Kernel
                // 5.1 Kernel 1：生成Alpha球
                var alphaSpheres = DispatchGenerateAlphaSpheresKernel(atoms.Count, moleculeBounds, gridPointCount);

                List<AlphaSphere> validAlphaSpheres = FilterAlphaSpheres(alphaSpheres);
                Debug.Log($"过滤后有效Alpha球数量：{validAlphaSpheres.Count}");

                List<List<AlphaSphere>> pocketClusters = DBSCANCluster(validAlphaSpheres);
                Debug.Log($"DBSCAN聚类得到口袋数：{pocketClusters.Count}");

                List<FPocketResult> pocketResults = CalculatePocketFeatures(pocketClusters, atoms);
                List<FPocketResult> finalPockets = pocketResults.Where(p => p.volume >= FPocketConstants.MIN_POCKET_VOLUME).ToList();

                PrintFPocketResults(finalPockets, "Compute Shader版本");

                // 5.2 Kernel 2：过滤有效Alpha球
                //DispatchFilterAlphaSpheresKernel(gridPointCount);

                //// 5.3 Kernel 3：DBSCAN聚类生成口袋
                //DispatchDBSCANClusterKernel(gridPointCount);

                //// 5.4 Kernel 4：计算口袋最终评分
                //DispatchCalculatePocketScoresKernel(atoms.Count);

                //// 6. 读取并输出结果
                //List<FPocketResult> finalPockets = ReadComputeShaderResults(atoms.Count);
                //PrintFPocketResults(finalPockets, "Compute Shader版本");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Compute Shader] 检测过程出错：{e.Message}\n{e.StackTrace}");
            }
            finally
            {
                // 7. 释放所有Compute Buffer（必须释放，避免内存泄漏）
                ReleaseComputeBuffers();
            }
        }

        #region Compute Shader核心调用逻辑（新增）
        /// <summary>
        /// 初始化所有Compute Buffer（与Shader中结构体对齐）
        /// </summary>
        private void InitComputeBuffers(List<AtomData> atoms, int gridPointCount)
        {
            // 原子缓冲区：尺寸=原子数，步长=结构体大小
            int atomStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AtomData));
            atomBuffer = new ComputeBuffer(atoms.Count, atomStride);
            atomBuffer.SetData(atoms);
            Debug.Log($"[Compute Shader] 原子缓冲区初始化完成，原子数：{atoms.Count}");

            // Alpha球缓冲区：尺寸=网格点总数，步长=结构体大小
            int alphaSphereStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(AlphaSphere));
            alphaSphereBuffer = new ComputeBuffer(gridPointCount, alphaSphereStride);
            // 初始化空Alpha球数据
            AlphaSphere[] emptyAlphaSpheres = new AlphaSphere[gridPointCount];
            for (int i = 0; i < emptyAlphaSpheres.Length; i++)
            {
                emptyAlphaSpheres[i] = new AlphaSphere
                {
                    center = Vector3.zero,
                    radius = -1f, // 初始标记为无效
                    enclosedAtoms = 0,
                    hydrophobicity = 0f,
                    polarity = 0f,
                    visited = 0
                };
            }
            alphaSphereBuffer.SetData(emptyAlphaSpheres);
            Debug.Log($"[Compute Shader] Alpha球缓冲区初始化完成，网格点数量：{gridPointCount}");

            // 口袋结果缓冲区：尺寸=最大口袋数，步长=结构体大小
            int pocketStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FPocketResult));
            pocketResultBuffer = new ComputeBuffer(FPocketConstants.MAX_POCKETS, pocketStride);
            // 初始化空口袋数据
            FPocketResult[] emptyPockets = new FPocketResult[FPocketConstants.MAX_POCKETS];
            for (int i = 0; i < emptyPockets.Length; i++)
            {
                emptyPockets[i] = new FPocketResult
                {
                    center = Vector3.zero,
                    volume = 0f,
                    score = 0f,
                    hydrophobicScore = 0f,
                    polarScore = 0f,
                    depthScore = 0f,
                    alphaSphereCount = 0,
                    atomCount = 0,
                    id = -1, // 标记为未使用
                    density = 0f,
                    lockFlag = 0 // 初始未锁定
                };
            }
            pocketResultBuffer.SetData(emptyPockets);

            // 聚类计数缓冲区：尺寸=1，步长=int大小（用于生成唯一聚类ID）
            clusterCountBuffer = new ComputeBuffer(1, sizeof(int));
            int[] clusterCount = { 0 }; // 初始聚类数=0
            clusterCountBuffer.SetData(clusterCount);

            Debug.Log("[Compute Shader] 所有缓冲区初始化完成");
        }

        /// <summary>
        /// 调度Kernel 1：生成Alpha球
        /// </summary>
        private AlphaSphere[] DispatchGenerateAlphaSpheresKernel(int atomCount, Bounds bounds, int gridPointCount)
        {
            int kernelId = fpocketComputeShader.FindKernel("CSGenerateAlphaSpheres");
            if (kernelId == -1)
            {
                Debug.LogError("[Compute Shader] 找不到Kernel：CSGenerateAlphaSpheres");
                return null;
            }

            // 设置常量缓冲区参数
            fpocketComputeShader.SetFloat("PROBE_RADIUS", FPocketConstants.PROBE_RADIUS);
            fpocketComputeShader.SetFloat("GRID_STEP", FPocketConstants.GRID_STEP);
            fpocketComputeShader.SetFloat("ALPHA_SPHERE_MIN_RADIUS", FPocketConstants.ALPHA_SPHERE_MIN_RADIUS);
            fpocketComputeShader.SetFloat("ALPHA_SPHERE_MAX_RADIUS", FPocketConstants.ALPHA_SPHERE_MAX_RADIUS);
            fpocketComputeShader.SetVector("boundsMin", bounds.min);
            fpocketComputeShader.SetVector("boundsMax", bounds.max);
            fpocketComputeShader.SetInt("atomCount", atomCount);
            fpocketComputeShader.SetInt("gridPointCount", gridPointCount);
            fpocketComputeShader.SetInt("maxAlphaSpheres", FPocketConstants.MAX_ALPHA_SPHERES);

            // 设置缓冲区
            fpocketComputeShader.SetBuffer(kernelId, "atomBuffer", atomBuffer);
            fpocketComputeShader.SetBuffer(kernelId, "alphaSphereBuffer", alphaSphereBuffer);

            // 调度Kernel（线程组大小=64，向上取整）
            int threadGroups = Mathf.CeilToInt(gridPointCount / 64f);
            fpocketComputeShader.Dispatch(kernelId, threadGroups, 1, 1);
            Debug.Log($"[Compute Shader] Kernel 1（生成Alpha球）调度完成，线程组数：{threadGroups}");

            AlphaSphere[] AlphaSpheres = new AlphaSphere[gridPointCount];
            alphaSphereBuffer.GetData(AlphaSpheres);
            return AlphaSpheres;
        }

        /// <summary>
        /// 调度Kernel 2：过滤有效Alpha球
        /// </summary>
        private void DispatchFilterAlphaSpheresKernel(int gridPointCount)
        {
            int kernelId = fpocketComputeShader.FindKernel("CSFilterAlphaSpheres");
            if (kernelId == -1)
            {
                Debug.LogError("[Compute Shader] 找不到Kernel：CSFilterAlphaSpheres");
                return;
            }

            // 设置参数和缓冲区
            fpocketComputeShader.SetInt("maxAlphaSpheres", FPocketConstants.MAX_ALPHA_SPHERES);
            fpocketComputeShader.SetBuffer(kernelId, "alphaSphereBuffer", alphaSphereBuffer);

            // 调度Kernel
            int threadGroups = Mathf.CeilToInt(gridPointCount / 64f);
            fpocketComputeShader.Dispatch(kernelId, threadGroups, 1, 1);
            Debug.Log($"[Compute Shader] Kernel 2（过滤Alpha球）调度完成，线程组数：{threadGroups}");
        }

        /// <summary>
        /// 调度Kernel 3：DBSCAN聚类生成口袋
        /// </summary>
        private void DispatchDBSCANClusterKernel(int gridPointCount)
        {
            int kernelId = fpocketComputeShader.FindKernel("CSDBSCANCluster");
            if (kernelId == -1)
            {
                Debug.LogError("[Compute Shader] 找不到Kernel：CSDBSCANCluster");
                return;
            }

            // 设置常量参数
            fpocketComputeShader.SetInt("DBSCAN_MIN_POINTS", FPocketConstants.DBSCAN_MIN_POINTS);
            fpocketComputeShader.SetFloat("DBSCAN_EPS", FPocketConstants.DBSCAN_EPS);
            fpocketComputeShader.SetInt("maxAlphaSpheres", FPocketConstants.MAX_ALPHA_SPHERES);
            fpocketComputeShader.SetInt("maxPockets", FPocketConstants.MAX_POCKETS);

            // 设置缓冲区
            fpocketComputeShader.SetBuffer(kernelId, "alphaSphereBuffer", alphaSphereBuffer);
            fpocketComputeShader.SetBuffer(kernelId, "pocketResultBuffer", pocketResultBuffer);
            fpocketComputeShader.SetBuffer(kernelId, "clusterCountBuffer", clusterCountBuffer);

            // 调度Kernel
            int threadGroups = Mathf.CeilToInt(gridPointCount / 64f);
            fpocketComputeShader.Dispatch(kernelId, threadGroups, 1, 1);
            Debug.Log($"[Compute Shader] Kernel 3（DBSCAN聚类）调度完成，线程组数：{threadGroups}");
        }

        /// <summary>
        /// 调度Kernel 4：计算口袋最终评分
        /// </summary>
        private void DispatchCalculatePocketScoresKernel(int atomCount)
        {
            int kernelId = fpocketComputeShader.FindKernel("CSCalculatePocketScores");
            if (kernelId == -1)
            {
                Debug.LogError("[Compute Shader] 找不到Kernel：CSCalculatePocketScores");
                return;
            }

            // 设置常量参数
            fpocketComputeShader.SetFloat("MIN_POCKET_VOLUME", FPocketConstants.MIN_POCKET_VOLUME);
            fpocketComputeShader.SetInt("atomCount", atomCount);
            fpocketComputeShader.SetInt("maxPockets", FPocketConstants.MAX_POCKETS);

            // 设置缓冲区
            fpocketComputeShader.SetBuffer(kernelId, "atomBuffer", atomBuffer);
            fpocketComputeShader.SetBuffer(kernelId, "pocketResultBuffer", pocketResultBuffer);

            // 调度Kernel（按最大口袋数调度）
            int threadGroups = Mathf.CeilToInt(FPocketConstants.MAX_POCKETS / 64f);
            fpocketComputeShader.Dispatch(kernelId, threadGroups, 1, 1);
            Debug.Log($"[Compute Shader] Kernel 4（计算评分）调度完成，线程组数：{threadGroups}");
        }

        /// <summary>
        /// 读取Compute Shader计算结果并过滤有效口袋
        /// </summary>
        private List<FPocketResult> ReadComputeShaderResults(int atomCount)
        {
            // 读取口袋结果缓冲区
            FPocketResult[] allPockets = new FPocketResult[FPocketConstants.MAX_POCKETS];
            pocketResultBuffer.GetData(allPockets);

            // 过滤有效口袋（ID有效+体积达标）
            List<FPocketResult> validPockets = allPockets
                .Where(p => p.id >= 0 && p.volume >= FPocketConstants.MIN_POCKET_VOLUME)
                .OrderByDescending(p => p.score) // 按评分降序
                .ToList();

            // 补充计算关联原子数（Shader中未计算，C#端补充）
            AtomData[] atoms = new AtomData[atomCount];
            atomBuffer.GetData(atoms);
            foreach (var pocket in validPockets)
            {
                FPocketResult updatedPocket = pocket;
                // 计算中心点6Å内的疏水原子数
                updatedPocket.atomCount = atoms.Count(a =>
                    a.atomType == 0 &&
                    Vector3.Distance(a.position, pocket.center) < 6.0f);
                // 重新计算密度（确保准确性）
                updatedPocket.density = updatedPocket.volume > 0
                    ? updatedPocket.alphaSphereCount / updatedPocket.volume
                    : 0f;
            }

            Debug.Log($"[Compute Shader] 读取到有效口袋数：{validPockets.Count}");
            return validPockets;
        }

        /// <summary>
        /// 释放所有Compute Buffer（避免内存泄漏）
        /// </summary>
        private void ReleaseComputeBuffers()
        {
            if (atomBuffer != null)
            {
                atomBuffer.Release();
                atomBuffer = null;
            }
            if (alphaSphereBuffer != null)
            {
                alphaSphereBuffer.Release();
                alphaSphereBuffer = null;
            }
            if (pocketResultBuffer != null)
            {
                pocketResultBuffer.Release();
                pocketResultBuffer = null;
            }
            if (clusterCountBuffer != null)
            {
                clusterCountBuffer.Release();
                clusterCountBuffer = null;
            }
            Debug.Log("[Compute Shader] 所有缓冲区已释放");
        }

        /// <summary>
        /// 计算网格点总数（边界框内按GRID_STEP划分）
        /// </summary>
        private int CalculateGridPointCount(Bounds bounds)
        {
            int xCount = Mathf.CeilToInt((bounds.max.x - bounds.min.x) / FPocketConstants.GRID_STEP) + 1;
            int yCount = Mathf.CeilToInt((bounds.max.y - bounds.min.y) / FPocketConstants.GRID_STEP) + 1;
            int zCount = Mathf.CeilToInt((bounds.max.z - bounds.min.z) / FPocketConstants.GRID_STEP) + 1;

            int totalCount = xCount * yCount * zCount;
            Debug.Log($"[Compute Shader] 网格点计算：X={xCount}, Y={yCount}, Z={zCount}，总数={totalCount}");
            return totalCount;
        }
        #endregion

        #region 原有C#版本逻辑（无修改）
        /// <summary>
        /// 加载并预处理PDBQT（对齐FPocket原子类型和疏水性权重）
        /// </summary>
        private List<AtomData> LoadAndPreprocessPDBQT(string filePath)
        {
            List<AtomData> atoms = PDBQTLoader.LoadPDBQT(filePath);
            // 重新赋值原子类型和疏水性权重（FPocket规则）
            for (int i = 0; i < atoms.Count; i++)
            {
                AtomData atom = atoms[i];
                string atomSymbol = GetAtomSymbolFromRadius(atom.vdwRadius);
                // FPocket原子类型规则：C/H=疏水(0), N/O/S/P=极性(1), 其他=2
                if (atomSymbol == "C" || atomSymbol == "H")
                {
                    atom.atomType = 0;
                    atom.hydrophobicity = 1.0f; // 疏水权重1.0
                }
                else if (new[] { "N", "O", "S", "P" }.Contains(atomSymbol))
                {
                    atom.atomType = 1;
                    atom.hydrophobicity = 0.0f; // 极性权重0.0
                }
                else
                {
                    atom.atomType = 2;
                    atom.hydrophobicity = 0.0f;
                }
                atoms[i] = atom;
            }
            return atoms;
        }

        /// <summary>
        /// 计算分子边界框（扩展探针半径，避免网格遗漏）
        /// </summary>
        private Bounds GetMoleculeBounds(List<AtomData> atoms)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var atom in atoms)
            {
                min = Vector3.Min(min, atom.position - Vector3.one * (FPocketConstants.PROBE_RADIUS + 2.0f));
                max = Vector3.Max(max, atom.position + Vector3.one * (FPocketConstants.PROBE_RADIUS + 2.0f));
            }
            return new Bounds((min + max) / 2, max - min);
        }

        /// <summary>
        /// 生成Alpha球（FPocket核心：检测不与原子重叠的空球）
        /// </summary>
        private List<AlphaSphere> GenerateAlphaSpheres(List<AtomData> atoms, Bounds bounds)
        {
            List<AlphaSphere> alphaSpheres = new List<AlphaSphere>();

            // 生成网格点（遍历分子边界框内的所有网格）
            for (float x = bounds.min.x; x <= bounds.max.x; x += FPocketConstants.GRID_STEP)
            {
                for (float y = bounds.min.y; y <= bounds.max.y; y += FPocketConstants.GRID_STEP)
                {
                    for (float z = bounds.min.z; z <= bounds.max.z; z += FPocketConstants.GRID_STEP)
                    {
                        Vector3 gridPos = new Vector3(x, y, z);
                        AlphaSphere sphere = CalculateAlphaSphere(gridPos, atoms);
                        if (sphere.radius >= FPocketConstants.ALPHA_SPHERE_MIN_RADIUS &&
                            sphere.radius <= FPocketConstants.ALPHA_SPHERE_MAX_RADIUS)
                        {
                            alphaSpheres.Add(sphere);
                        }
                    }
                }
            }

            return alphaSpheres;
        }

        /// <summary>
        /// 计算单个网格点的Alpha球（FPocket核心：最大空球）
        /// </summary>
        private AlphaSphere CalculateAlphaSphere(Vector3 gridPos, List<AtomData> atoms)
        {
            AlphaSphere sphere = new AlphaSphere();
            sphere.center = gridPos;
            sphere.enclosedAtoms = 0;
            sphere.hydrophobicity = 0.0f;
            sphere.polarity = 0.0f;

            // 计算到所有原子的最小距离（减去原子范德华半径+探针半径）
            float minDistance = float.MaxValue;
            int hydrophobicCount = 0;
            int polarCount = 0;

            foreach (var atom in atoms)
            {
                float distance = Vector3.Distance(gridPos, atom.position);
                float effectiveDistance = distance - (atom.vdwRadius + FPocketConstants.PROBE_RADIUS);

                if (effectiveDistance < 0) continue; // 与原子重叠，跳过
                if (effectiveDistance < minDistance) minDistance = effectiveDistance;

                // 统计球内原子的疏水性/极性（FPocket规则：球内原子=距离<球半径+原子半径）
                if (distance < (minDistance + atom.vdwRadius))
                {
                    sphere.enclosedAtoms++;
                    if (atom.atomType == 0) hydrophobicCount++;
                    else if (atom.atomType == 1) polarCount++;
                }
            }

            // Alpha球半径=最小有效距离
            sphere.radius = minDistance;
            // 疏水性占比=疏水原子数/总原子数
            sphere.hydrophobicity = sphere.enclosedAtoms > 0 ? (float)hydrophobicCount / sphere.enclosedAtoms : 0.0f;
            // 极性占比=极性原子数/总原子数
            sphere.polarity = sphere.enclosedAtoms > 0 ? (float)polarCount / sphere.enclosedAtoms : 0.0f;

            return sphere;
        }

        /// <summary>
        /// 过滤Alpha球（FPocket规则：高疏水性+有效半径）
        /// </summary>
        private List<AlphaSphere> FilterAlphaSpheres(List<AlphaSphere> spheres)
        {
            return spheres.Where(s =>
                s.hydrophobicity >= 0.5f && // 至少50%疏水原子
                s.radius >= FPocketConstants.ALPHA_SPHERE_MIN_RADIUS &&
                s.enclosedAtoms >= 3 // 至少包裹3个原子
            ).ToList();
        }

        /// <summary>
        /// 过滤Alpha球（FPocket规则：高疏水性+有效半径）
        /// </summary>
        private List<AlphaSphere> FilterAlphaSpheres(AlphaSphere[] spheres)
        {
            return spheres.Where(s =>
                s.hydrophobicity >= 0.5f && // 至少50%疏水原子
                s.radius >= FPocketConstants.ALPHA_SPHERE_MIN_RADIUS &&
                s.enclosedAtoms >= 3 // 至少包裹3个原子
            ).ToList();
        }

        /// <summary>
        /// DBSCAN密度聚类（FPocket使用的聚类算法）
        /// </summary>
        private List<List<AlphaSphere>> DBSCANCluster(List<AlphaSphere> spheres)
        {
            List<List<AlphaSphere>> clusters = new List<List<AlphaSphere>>();
            HashSet<int> visited = new HashSet<int>();
            HashSet<int> noise = new HashSet<int>();

            for (int i = 0; i < spheres.Count; i++)
            {
                if (visited.Contains(i)) continue;

                // 查找邻域点
                List<int> neighbors = FindNeighbors(spheres, i);
                if (neighbors.Count < FPocketConstants.DBSCAN_MIN_POINTS)
                {
                    noise.Add(i);
                    visited.Add(i);
                    continue;
                }

                // 生成新聚类
                List<AlphaSphere> cluster = new List<AlphaSphere>();
                cluster.Add(spheres[i]);
                visited.Add(i);

                // 扩展聚类（迭代查找邻域）
                Queue<int> queue = new Queue<int>(neighbors);
                while (queue.Count > 0)
                {
                    int j = queue.Dequeue();
                    if (visited.Contains(j)) continue;

                    visited.Add(j);
                    List<int> jNeighbors = FindNeighbors(spheres, j);
                    if (jNeighbors.Count >= FPocketConstants.DBSCAN_MIN_POINTS)
                    {
                        foreach (int n in jNeighbors)
                        {
                            if (!visited.Contains(n) && !queue.Contains(n))
                            {
                                queue.Enqueue(n);
                            }
                        }
                    }

                    cluster.Add(spheres[j]);
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        /// <summary>
        /// 查找邻域点（距离<EPS的点）
        /// </summary>
        private List<int> FindNeighbors(List<AlphaSphere> spheres, int index)
        {
            List<int> neighbors = new List<int>();
            Vector3 center = spheres[index].center;
            for (int i = 0; i < spheres.Count; i++)
            {
                if (i == index) continue;
                if (Vector3.Distance(center, spheres[i].center) < FPocketConstants.DBSCAN_EPS)
                {
                    neighbors.Add(i);
                }
            }
            return neighbors;
        }

        /// <summary>
        /// 计算口袋特征（对齐FPocket评分体系）
        /// </summary>
        private List<FPocketResult> CalculatePocketFeatures(List<List<AlphaSphere>> clusters, List<AtomData> atoms)
        {
            List<FPocketResult> results = new List<FPocketResult>();

            for (int i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                FPocketResult result = new FPocketResult();
                result.id = i;
                result.alphaSphereCount = cluster.Count;

                // 1. 计算中心点（Alpha球中心的加权平均，权重=半径）
                Vector3 weightedCenter = Vector3.zero;
                float totalRadius = 0.0f;
                foreach (var sphere in cluster)
                {
                    weightedCenter += sphere.center * sphere.radius;
                    totalRadius += sphere.radius;
                }
                result.center = totalRadius > 0 ? weightedCenter / totalRadius : Vector3.zero;

                // 2. 计算体积（所有Alpha球体积之和）
                result.volume = cluster.Sum(s => (4.0f / 3.0f) * Mathf.PI * Mathf.Pow(s.radius, 3));

                // 3. 计算疏水性评分（平均疏水性占比）
                result.hydrophobicScore = cluster.Average(s => s.hydrophobicity);

                // 4. 计算极性评分（平均极性占比）
                result.polarScore = cluster.Average(s => s.polarity);

                // 5. 计算口袋深度（FPocket：中心点到分子表面的距离）
                result.depthScore = CalculatePocketDepth(result.center, atoms);

                // 6. 综合评分（FPocket权重）
                result.score =
                    result.volume / 100 * FPocketConstants.SCORE_VOLUME_WEIGHT +
                    result.hydrophobicScore * FPocketConstants.SCORE_HYDROPHOBIC_WEIGHT +
                    (1 - result.polarScore) * FPocketConstants.SCORE_POLAR_WEIGHT +
                    result.depthScore * FPocketConstants.SCORE_DEPTH_WEIGHT;

                // 7. 计算密度（Alpha球数/体积）
                result.density = result.volume > 0 ? result.alphaSphereCount / result.volume : 0.0f;

                // 8. 关联原子数（中心点周围6Å内的疏水原子数）
                result.atomCount = atoms.Count(a =>
                    a.atomType == 0 &&
                    Vector3.Distance(a.position, result.center) < 6.0f);

                results.Add(result);
            }

            // 按评分降序排序（FPocket默认按评分排序）
            return results.OrderByDescending(r => r.score).ToList();
        }

        /// <summary>
        /// 计算口袋深度（FPocket：中心点到分子表面的最短距离）
        /// </summary>
        private float CalculatePocketDepth(Vector3 pocketCenter, List<AtomData> atoms)
        {
            float minDistance = float.MaxValue;
            foreach (var atom in atoms)
            {
                float distance = Vector3.Distance(pocketCenter, atom.position) - atom.vdwRadius;
                if (distance < minDistance) minDistance = distance;
            }
            // 归一化深度（0~1）
            return Mathf.Clamp01(minDistance / 10.0f);
        }

        /// <summary>
        /// 输出FPocket风格的结果
        /// </summary>
        private void PrintFPocketResults(List<FPocketResult> pockets, string version)
        {
            Debug.Log($"===== {version} 对齐FPocket的口袋检测结果 =====");
            Debug.Log($"总计有效口袋数：{pockets.Count}（按评分降序）");
            foreach (var pocket in pockets)
            {
                Debug.Log($"[Pocket {pocket.id}]");
                Debug.Log($"  中心点：({pocket.center.x:F2}, {pocket.center.y:F2}, {pocket.center.z:F2})");
                Debug.Log($"  体积：{pocket.volume:F2} Å³");
                Debug.Log($"  Alpha球数：{pocket.alphaSphereCount}");
                Debug.Log($"  疏水评分：{pocket.hydrophobicScore:F2}");
                Debug.Log($"  极性评分：{pocket.polarScore:F2}");
                Debug.Log($"  深度评分：{pocket.depthScore:F2}");
                Debug.Log($"  综合评分：{pocket.score:F2}");
                Debug.Log($"  密度：{pocket.density:F4}");
                Debug.Log($"  关联疏水原子数：{pocket.atomCount}");
                Debug.Log("----------------------------------");
            }
        }

        /// <summary>
        /// 从范德华半径反推原子类型
        /// </summary>
        private string GetAtomSymbolFromRadius(float radius)
        {
            if (Mathf.Abs(radius - 1.2f) < 0.1f) return "H";
            if (Mathf.Abs(radius - 1.7f) < 0.1f) return "C";
            if (Mathf.Abs(radius - 1.55f) < 0.1f) return "N";
            if (Mathf.Abs(radius - 1.52f) < 0.1f) return "O";
            if (Mathf.Abs(radius - 1.80f) < 0.1f) return "S";
            return "OTHER";
        }
        #endregion

        #region PDBQT加载器（无修改）
        public class PDBQTLoader
        {
            public static List<AtomData> LoadPDBQT(string filePath)
            {
                List<AtomData> atoms = new List<AtomData>();
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"PDBQT文件不存在：{filePath}");
                    return atoms;
                }

                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("ATOM") || line.StartsWith("HETATM"))
                    {
                        try
                        {
                            float x = float.Parse(line.Substring(30, 8).Trim());
                            float y = float.Parse(line.Substring(38, 8).Trim());
                            float z = float.Parse(line.Substring(46, 8).Trim());
                            string atomSymbol = line.Substring(77, 2).Trim();
                            float vdwRadius = GetVdwRadius(atomSymbol);
                            float charge = float.Parse(line.Substring(69, 7).Trim());

                            atoms.Add(new AtomData
                            {
                                position = new Vector3(x, y, z),
                                vdwRadius = vdwRadius,
                                charge = charge,
                                atomType = 0, // 后续会重新赋值
                                hydrophobicity = 0.0f // 后续会重新赋值
                            });
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"解析PDBQT行失败：{line} | 错误：{e.Message}");
                        }
                    }
                }

                Debug.Log($"成功加载PDBQT文件：{filePath}，原子数：{atoms.Count}");
                return atoms;
            }

            private static float GetVdwRadius(string atomSymbol)
            {
                return atomSymbol switch
                {
                    "H" => 1.2f,
                    "C" => 1.7f,
                    "N" => 1.55f,
                    "O" => 1.52f,
                    "S" => 1.80f,
                    "P" => 1.80f,
                    _ => 1.6f
                };
            }
        }
        #endregion

        /// <summary>
        /// 确保退出时释放缓冲区
        /// </summary>
        private void OnDestroy()
        {
            ReleaseComputeBuffers();
        }
    }

}