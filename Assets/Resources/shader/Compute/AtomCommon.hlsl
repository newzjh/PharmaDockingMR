// 原子类型枚举（覆盖药物分子常见原子）
// 原子类型枚举
#define AtomTypeH 1
#define AtomTypeC 6
#define AtomTypeO 8
#define AtomTypeN 7
#define AtomTypeS 16
#define AtomTypeF 9
#define AtomTypeCl 17
#define AtomTypeUNKNOWN 0


#define MAX_ATOM_COUNT 60 // 分子最大原子数
#define smilesMaxLength 256 // 单个SMILES最大长度
#define fpSize 512   // 指纹长度（512/256）

float atomRadius;

// CPK配色与半径映射
float GetAtomRadius(int atomicNumber)
{
    float radius =
        0.35f + // 常数项
        0.18f * log(atomicNumber) - // 对数项（原子序数越大，半径增长放缓）
        0.005f * atomicNumber + // 线性修正项
        0.02f * sin(atomicNumber); // 周期性修正（适配元素周期表）
    
    // 修正非金属原子的半径偏差（1AQ1核心原子均为非金属）
    if (atomicNumber >= 6 && atomicNumber <= 17)
    {
        radius += 0.4f; // 非金属原子基准修正
    }
    
    return clamp(radius, 0.5f, 2.5f); // H=0.53Å, Cl=1.75Å
}

#define PI 3.1415926f
float4 GetAtomColor(int atomicNumber)
{
    float normZ = saturate((atomicNumber - 1) / 16.0f);
    
    float r = 0.5f + 0.4f * sin(normZ * PI * 2 - PI / 2);
    float g = 0.5f + 0.4f * cos(normZ * PI * 3 - PI / 3);
    float b = 0.5f + 0.4f * sin(normZ * PI * 2 + PI / 4);

    return float4(saturate(r), saturate(g), saturate(b), 1.0f);
}

// 核心哈希函数：将原子邻域特征映射为512位内的索引
uint Hash(uint3 feature)
{
    // 优化的哈希算法，适配GPU无符号整数计算
    feature = feature * 1664525u + 1013904223u;
    feature.x += feature.y * feature.z;
    feature.y += feature.z * feature.x;
    feature.z += feature.x * feature.y;
    feature ^= feature >> 16u;
    feature.x += feature.y * feature.z;
    feature.y += feature.z * feature.x;
    feature.z += feature.x * feature.y;
    return feature.z % fpSize;
}

// GPU端轻量级SMILES解析：提取原子类型和数量
void ParseSMILES(in int smilesChars[smilesMaxLength], out int atomTypes[MAX_ATOM_COUNT], out int atomCount)
{
    atomCount = 0;
    //atomTypes = new AtomType[ MAX_ATOM_COUNT]; // 预分配最大长度

    //for (int i = 0; i < smilesMaxLength; i++)
    for (int i = 0; i < MAX_ATOM_COUNT; i++)
    {
        //if (atomCount >= MAX_ATOM_COUNT)
        //    break;
        
        int c = smilesChars[i];
        if (c == 0)
            break; // 到达SMILES字符串末尾
        // 过滤SMILES特殊符号（键、括号、分支符）
        if (c == '-' || c == '=' || c == '#' || c == '(' || c == ')' || c == '[' || c == ']' || c == '/')
            continue;

        // 解析原子类型（支持常见药物原子）
        switch (c)
        {
            case 'C':
                atomTypes[atomCount++] = AtomTypeC;
                break;
            case 'O':
                atomTypes[atomCount++] = AtomTypeO;
                break;
            case 'N':
                atomTypes[atomCount++] = AtomTypeN;
                break;
            case 'S':
                atomTypes[atomCount++] = AtomTypeS;
                break;
            case 'F':
                atomTypes[atomCount++] = AtomTypeF;
                break;
            case 'l': // Cl的第二个字符，需兼容简化SMILES
                if (i > 0 && smilesChars[i - 1] == 'C')
                    atomTypes[atomCount - 1] = AtomTypeCl;
                break;
            default:
                atomTypes[atomCount++] = AtomTypeUNKNOWN;
                break;
        }

    }
}