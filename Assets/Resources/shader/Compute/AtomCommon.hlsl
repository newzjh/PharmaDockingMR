#pragma kernel CSForwardDiffusion
#pragma enable_d3d11_debug_symbols

// ==================== 通用常量定义（整合AtomCommon.txt）====================
// 原子类型常量（使用原子序数作为标识，兼容药物分子常见原子）
#define ATOM_TYPE_H 1
#define ATOM_TYPE_C 6
#define ATOM_TYPE_c 66 // 小写c（芳香碳），用非原子序数标识避免冲突
#define ATOM_TYPE_N 7
#define ATOM_TYPE_n 77 // 小写n（芳香氮）
#define ATOM_TYPE_O 8
#define ATOM_TYPE_o 88 // 小写o（芳香氧）
#define ATOM_TYPE_S 16
#define ATOM_TYPE_s 166 // 小写s（芳香硫）
#define ATOM_TYPE_F 9
#define ATOM_TYPE_Cl 17
#define ATOM_TYPE_Br 35
#define ATOM_TYPE_I 53
#define ATOM_TYPE_P 15
#define ATOM_TYPE_p 155 // 芳香磷
#define ATOM_TYPE_B 5
#define ATOM_TYPE_Si 14
#define ATOM_TYPE_As 33
#define ATOM_TYPE_Se 34
#define ATOM_TYPE_UNKNOWN 0

// 键类型常量
#define BOND_SINGLE 0
#define BOND_DOUBLE 1
#define BOND_TRIPLE 2
#define BOND_AROMATIC 3
#define BOND_UNKNOWN 4

// 通用尺寸常量
#define MAX_ATOM_COUNT 60 // 分子最大原子数（沿用AtomCommon.txt定义）
#define SMILES_MAX_LENGTH 256 // 单个SMILES最大长度（沿用AtomCommon.txt）
#define FP_SIZE 512 // 指纹长度（沿用AtomCommon.txt）
#define MAX_RING_COUNT 10 // 最大环数量
#define MAX_BRANCH_DEPTH 3 // 最大支链深度


// ==================== AtomCommon.txt核心函数（保留并扩展）====================
// CPK配色与半径映射（原封保留）
float GetAtomRadiusOld(int atomicNumber)
{
    float radius =
        0.35f + // 常数项
        0.18f * log(atomicNumber) - // 对数项（原子序数越大，半径增长放缓）
        0.005f * atomicNumber + // 线性修正项
        0.02f * sin(atomicNumber); // 周期性修正（适配元素周期表）
    
    return clamp(radius, 0.5f, 2.5f); // H=0.53Å, Cl=1.75Å
}

// ==================== AtomCommon.txt核心函数（保留并扩展）====================
// CPK配色与半径映射（原封保留）
float GetAtomRadius(int atomicNumber)
{
    // 为芳香原子（小写）添加半径适配
    switch (atomicNumber)
    {
        case ATOM_TYPE_c:
            return GetAtomRadiusOld(ATOM_TYPE_C) * 0.95f;
        case ATOM_TYPE_n:
            return GetAtomRadiusOld(ATOM_TYPE_N) * 0.95f;
        case ATOM_TYPE_o:
            return GetAtomRadiusOld(ATOM_TYPE_O) * 0.95f;
        case ATOM_TYPE_s:
            return GetAtomRadiusOld(ATOM_TYPE_S) * 0.95f;
        case ATOM_TYPE_p:
            return GetAtomRadiusOld(ATOM_TYPE_P) * 0.95f;
        default:
            break;
    }
    
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
    // 为芳香原子（小写）定制颜色
    switch(atomicNumber)
    {
        case ATOM_TYPE_c: return float4(0.2f, 0.8f, 0.2f, 1.0f); // 芳香碳：深绿色
        case ATOM_TYPE_n: return float4(0.2f, 0.2f, 0.8f, 1.0f); // 芳香氮：深蓝色
        case ATOM_TYPE_o: return float4(0.8f, 0.2f, 0.2f, 1.0f); // 芳香氧：深红色
        case ATOM_TYPE_s: return float4(0.8f, 0.8f, 0.2f, 1.0f); // 芳香硫：深黄色
        case ATOM_TYPE_p: return float4(0.8f, 0.2f, 0.8f, 1.0f); // 芳香磷：深紫色
        default:
            break;
    }
    
    // 原有颜色计算逻辑（原封保留）
    float normZ = saturate((atomicNumber - 1) / 16.0f);
    float r = 0.5f + 0.4f * sin(normZ * PI * 2 - PI / 2);
    float g = 0.5f + 0.4f * cos(normZ * PI * 3 - PI / 3);
    float b = 0.5f + 0.4f * sin(normZ * PI * 2 + PI / 4);
    return float4(saturate(r), saturate(g), saturate(b), 1.0f);
}

// 核心哈希函数（原封保留）
uint Hash(uint3 feature)
{
    feature = feature * 1664525u + 1013904223u;
    feature.x += feature.y * feature.z;
    feature.y += feature.z * feature.x;
    feature.z += feature.x * feature.y;
    feature ^= feature >> 16u;
    feature.x += feature.y * feature.z;
    feature.y += feature.z * feature.x;
    feature.z += feature.x * feature.y;
    return feature.z % FP_SIZE;
}

// 扩展SMILES解析函数（支持小写原子和多字符原子）
void ParseSMILES(in int smilesChars[SMILES_MAX_LENGTH], out int atomTypes[MAX_ATOM_COUNT], out int atomCount)
{
    atomCount = 0;
    for (int i = 0; i < SMILES_MAX_LENGTH; i++)
    {
        if (atomCount >= MAX_ATOM_COUNT) break;
        
        int c = smilesChars[i];
        if (c == 0) break; // 到达SMILES字符串末尾
        
        // 过滤SMILES特殊符号（键、括号、分支符、环编号、电荷）
        if (c == '-' || c == '=' || c == '#' || c == ':' || c == '$' || c == '%' ||
            c == '(' || c == ')' || c == '[' || c == ']' || c == '/' || /*c == '\\' ||*/
            (c >= '0' && c <= '9') || c == '+' || c == '-')
        {
            continue;
        }
        
        // 解析原子类型（支持常见药物原子+芳香原子）
        switch (c)
        {
            case 'C': atomTypes[atomCount++] = ATOM_TYPE_C; break;
            case 'c': atomTypes[atomCount++] = ATOM_TYPE_c; break;
            case 'N': atomTypes[atomCount++] = ATOM_TYPE_N; break;
            case 'n': atomTypes[atomCount++] = ATOM_TYPE_n; break;
            case 'O': atomTypes[atomCount++] = ATOM_TYPE_O; break;
            case 'o': atomTypes[atomCount++] = ATOM_TYPE_o; break;
            case 'S': atomTypes[atomCount++] = ATOM_TYPE_S; break;
            //case 's': atomTypes[atomCount++] = ATOM_TYPE_s; break;
            case 'F': atomTypes[atomCount++] = ATOM_TYPE_F; break;
            case 'P': atomTypes[atomCount++] = ATOM_TYPE_P; break;
            case 'p': atomTypes[atomCount++] = ATOM_TYPE_p; break;
            case 'B': atomTypes[atomCount++] = ATOM_TYPE_B; break;
            case 'I': atomTypes[atomCount++] = ATOM_TYPE_I; break;
            // 多字符原子处理
            case 'l': // Cl的第二个字符
                if (i > 0 && smilesChars[i - 1] == 'C')
                {
                    atomTypes[atomCount - 1] = ATOM_TYPE_Cl;
                }
                break;
            case 'r': // Br的第二个字符
                if (i > 0 && smilesChars[i - 1] == 'B')
                {
                    atomTypes[atomCount - 1] = ATOM_TYPE_Br;
                }
                break;
            case 'i': // Si的第二个字符
                if (i > 0 && smilesChars[i - 1] == 'S')
                {
                    atomTypes[atomCount - 1] = ATOM_TYPE_Si;
                }
                break;
            case 's': // As的第二个字符
                if (i > 0 && smilesChars[i - 1] == 'A')
                {
                    atomTypes[atomCount - 1] = ATOM_TYPE_As;
                }
                else
                {
                    atomTypes[atomCount++] = ATOM_TYPE_s;
                }
                break;
            case 'e': // Se的第二个字符
                if (i > 0 && smilesChars[i - 1] == 'S')
                {
                    atomTypes[atomCount - 1] = ATOM_TYPE_Se;
                }
                break;
            default: atomTypes[atomCount++] = ATOM_TYPE_UNKNOWN; break;
        }
    }
}



// 原子类型转SMILES字符（支持所有常见原子）
void AtomTypeToSMILES(int atomType, out int chars[3])
{
    // 初始化为空字符（0）
    chars[0] = 0;
    chars[1] = 0;
    chars[2] = 0;
    
    switch(atomType)
    {
        case ATOM_TYPE_C: chars[0] = 'C'; break;
        case ATOM_TYPE_c: chars[0] = 'c'; break;
        case ATOM_TYPE_N: chars[0] = 'N'; break;
        case ATOM_TYPE_n: chars[0] = 'n'; break;
        case ATOM_TYPE_O: chars[0] = 'O'; break;
        case ATOM_TYPE_o: chars[0] = 'o'; break;
        case ATOM_TYPE_S: chars[0] = 'S'; break;
        case ATOM_TYPE_s: chars[0] = 's'; break;
        case ATOM_TYPE_P: chars[0] = 'P'; break;
        case ATOM_TYPE_p: chars[0] = 'p'; break;
        case ATOM_TYPE_F: chars[0] = 'F'; break;
        case ATOM_TYPE_Cl: chars[0] = 'C'; chars[1] = 'l'; break;
        case ATOM_TYPE_Br: chars[0] = 'B'; chars[1] = 'r'; break;
        case ATOM_TYPE_I: chars[0] = 'I'; break;
        case ATOM_TYPE_H: chars[0] = 'H'; break;
        case ATOM_TYPE_B: chars[0] = 'B'; break;
        case ATOM_TYPE_Si: chars[0] = 'S'; chars[1] = 'i'; break;
        case ATOM_TYPE_As: chars[0] = 'A'; chars[1] = 's'; break;
        case ATOM_TYPE_Se: chars[0] = 'S'; chars[1] = 'e'; break;
        default: chars[0] = 'X'; break;
    }
}

// 键类型转SMILES字符
int BondTypeToSMILES(int bondType)
{
    switch(bondType)
    {
        case BOND_SINGLE: return '-';
        case BOND_DOUBLE: return '=';
        case BOND_TRIPLE: return '#';
        case BOND_AROMATIC: return ':';
        default: return '-';
    }
}

// 生成电荷标记（支持正负电荷）
void GenerateCharge(int charge, out int chars[3])
{
    chars[0] = 0;
    chars[1] = 0;
    chars[2] = 0;
    
    if (charge == 0) return;
    
    if (charge > 0)
    {
        chars[0] = '+';
        if (charge > 1) chars[1] = '0' + charge;
    }
    else
    {
        chars[0] = '-';
        if (charge < -1) chars[1] = '0' - charge;
    }
}



