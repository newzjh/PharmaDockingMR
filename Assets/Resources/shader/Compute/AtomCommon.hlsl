// 原子类型枚举（覆盖药物分子常见原子）
// 原子类型枚举
#define AtomTypeC 0
#define AtomTypeO 1
#define AtomTypeN 2
#define AtomTypeS 3
#define AtomTypeF 4
#define AtomTypeCl 5
#define AtomTypeUNKNOWN 9


#define MAX_ATOM_COUNT 50 // 分子最大原子数
#define smilesMaxLength 256 // 单个SMILES最大长度
#define fpSize 512   // 指纹长度（512/256）

float atomRadius;

// CPK配色与半径映射
float GetAtomRadius(int type)
{
    switch (type)
    {
        case AtomTypeC:
            return atomRadius * 1.0f;
        case AtomTypeO:
            return atomRadius * 0.9f;
        case AtomTypeN:
            return atomRadius * 0.95f;
        case AtomTypeS:
            return atomRadius * 1.2f;
        default:
            return atomRadius * 0.8f;
    }
}

float4 GetAtomColor(int type)
{
    switch (type)
    {
        case AtomTypeC:
            return float4(0.2f, 0.2f, 0.2f, 1.0f); // 碳-灰色
        case AtomTypeO:
            return float4(1.0f, 0.0f, 0.0f, 1.0f); // 氧-红色
        case AtomTypeN:
            return float4(0.0f, 0.0f, 1.0f, 1.0f); // 氮-蓝色
        case AtomTypeS:
            return float4(1.0f, 1.0f, 0.0f, 1.0f); // 硫-黄色
        default:
            return float4(0.5f, 0.5f, 0.5f, 1.0f); // 未知-浅灰
    }
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