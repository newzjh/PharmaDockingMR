// Shared atom and bond constants used by the compute pipelines.
// Aromatic atom types use distinct IDs so rendering and hashing can preserve them.
#define ATOM_TYPE_H 1
#define ATOM_TYPE_C 6
#define ATOM_TYPE_c 66 
#define ATOM_TYPE_N 7
#define ATOM_TYPE_n 77 
#define ATOM_TYPE_O 8
#define ATOM_TYPE_o 88 
#define ATOM_TYPE_S 16
#define ATOM_TYPE_s 166 
#define ATOM_TYPE_F 9
#define ATOM_TYPE_Cl 17
#define ATOM_TYPE_Br 35
#define ATOM_TYPE_I 53
#define ATOM_TYPE_P 15
#define ATOM_TYPE_p 155 
#define ATOM_TYPE_B 5
#define ATOM_TYPE_Si 14
#define ATOM_TYPE_As 33
#define ATOM_TYPE_Se 34
#define ATOM_TYPE_UNKNOWN 0


#define BOND_SINGLE 0
#define BOND_DOUBLE 1
#define BOND_TRIPLE 2
#define BOND_AROMATIC 3
#define BOND_UNKNOWN 4


#define MAX_ATOM_COUNT 60 
#define SMILES_MAX_LENGTH 256 
#define FP_SIZE 512 
#define MAX_RING_COUNT 10 
#define MAX_BRANCH_DEPTH 3 // Maximum nested SMILES branch depth handled on GPU.
#define MAX_GRAPH_NEIGHBORS 6 // Compact adjacency list capacity per atom.

// Legacy radius approximation kept for compatibility with existing visuals.
float GetAtomRadiusOld(int atomicNumber)
{
    float radius =
        0.35f +
        0.18f * log(atomicNumber) -
        0.005f * atomicNumber +
        0.02f * sin(atomicNumber);
    
    return clamp(radius, 0.5f, 2.5f);
}

// Atom radii are slightly reduced for aromatic variants to avoid inflated rings.
float GetAtomRadius(int atomicNumber)
{
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
        0.35f +
        0.18f * log(atomicNumber) -
        0.005f * atomicNumber +
        0.02f * sin(atomicNumber);
    
    if (atomicNumber >= 6 && atomicNumber <= 17)
    {
        radius += 0.4f;
    }
    
    return clamp(radius, 0.5f, 2.5f);
}

#define PI 3.1415926f
float4 GetAtomColor(int atomicNumber)
{
    switch(atomicNumber)
    {
        case ATOM_TYPE_c: return float4(0.2f, 0.8f, 0.2f, 1.0f);
        case ATOM_TYPE_n: return float4(0.2f, 0.2f, 0.8f, 1.0f);
        case ATOM_TYPE_o: return float4(0.8f, 0.2f, 0.2f, 1.0f);
        case ATOM_TYPE_s: return float4(0.8f, 0.8f, 0.2f, 1.0f);
        case ATOM_TYPE_p: return float4(0.8f, 0.2f, 0.8f, 1.0f);
        default:
            break;
    }
    
    float normZ = saturate((atomicNumber - 1) / 16.0f);
    float r = 0.5f + 0.4f * sin(normZ * PI * 2 - PI / 2);
    float g = 0.5f + 0.4f * cos(normZ * PI * 3 - PI / 3);
    float b = 0.5f + 0.4f * sin(normZ * PI * 2 + PI / 4);
    return float4(saturate(r), saturate(g), saturate(b), 1.0f);
}

// Stable hash used by the Morgan fingerprint kernels.
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

uint MixHash(uint seed, uint value)
{
    seed ^= value + 0x9e3779b9u + (seed << 6u) + (seed >> 2u);
    return seed;
}

bool IsAromaticAtomType(int atomType)
{
    return atomType == ATOM_TYPE_c || atomType == ATOM_TYPE_n || atomType == ATOM_TYPE_o ||
           atomType == ATOM_TYPE_s || atomType == ATOM_TYPE_p;
}

int GetGraphArrayIndex(int atomIdx, int slot)
{
    return atomIdx * MAX_GRAPH_NEIGHBORS + slot;
}

bool HasGraphBond(
    int a,
    int b,
    in int atomDegrees[MAX_ATOM_COUNT],
    in int neighborIndices[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS])
{
    int degree = min(atomDegrees[a], MAX_GRAPH_NEIGHBORS);
    for (int slot = 0; slot < degree; slot++)
    {
        if (neighborIndices[GetGraphArrayIndex(a, slot)] == b)
            return true;
    }
    return false;
}

int GetGraphBondType(
    int a,
    int b,
    in int atomDegrees[MAX_ATOM_COUNT],
    in int neighborIndices[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS],
    in int neighborBondTypes[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS])
{
    int degree = min(atomDegrees[a], MAX_GRAPH_NEIGHBORS);
    for (int slot = 0; slot < degree; slot++)
    {
        int idx = GetGraphArrayIndex(a, slot);
        if (neighborIndices[idx] == b)
            return neighborBondTypes[idx];
    }
    return BOND_UNKNOWN;
}

bool TryAddGraphNeighbor(
    int atomIdx,
    int neighborIdx,
    int bondType,
    inout int atomDegrees[MAX_ATOM_COUNT],
    inout int neighborIndices[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS],
    inout int neighborBondTypes[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS])
{
    int degree = atomDegrees[atomIdx];
    if (degree >= MAX_GRAPH_NEIGHBORS)
        return false;

    int writeIdx = GetGraphArrayIndex(atomIdx, degree);
    neighborIndices[writeIdx] = neighborIdx;
    neighborBondTypes[writeIdx] = bondType;
    atomDegrees[atomIdx] = degree + 1;
    return true;
}

bool TryAddGraphBond(
    int atomA,
    int atomB,
    int bondType,
    inout int atomDegrees[MAX_ATOM_COUNT],
    inout int neighborIndices[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS],
    inout int neighborBondTypes[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS])
{
    if (atomA < 0 || atomB < 0 || atomA == atomB)
        return false;

    if (HasGraphBond(atomA, atomB, atomDegrees, neighborIndices))
        return false;

    bool addedAB = TryAddGraphNeighbor(atomA, atomB, bondType, atomDegrees, neighborIndices, neighborBondTypes);
    bool addedBA = TryAddGraphNeighbor(atomB, atomA, bondType, atomDegrees, neighborIndices, neighborBondTypes);
    return addedAB && addedBA;
}

void BuildAtomLayoutGraph(
    int atomCount,
    in int atomDegrees[MAX_ATOM_COUNT],
    in int neighborIndices[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS],
    in int neighborBondTypes[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS],
    float bondLength,
    out float3 atomPositions[MAX_ATOM_COUNT])
{
    int parent[MAX_ATOM_COUNT];
    int depth[MAX_ATOM_COUNT];
    int placed[MAX_ATOM_COUNT];
    int queue[MAX_ATOM_COUNT];
    int queueHead = 0;
    int queueTail = 0;

    for (int i = 0; i < MAX_ATOM_COUNT; i++)
    {
        atomPositions[i] = float3(0, 0, 0);
        parent[i] = -1;
        depth[i] = 0;
        placed[i] = 0;
    }

    if (atomCount <= 0)
        return;

    atomPositions[0] = float3(0, 0, 0);
    placed[0] = 1;
    queue[queueTail++] = 0;

    while (queueHead < queueTail)
    {
        int current = queue[queueHead++];
        int children[MAX_GRAPH_NEIGHBORS];
        int childCount = 0;

        int currentDegree = min(atomDegrees[current], MAX_GRAPH_NEIGHBORS);
        for (int slot = 0; slot < currentDegree; slot++)
        {
            int neighbor = neighborIndices[GetGraphArrayIndex(current, slot)];
            if (neighbor < 0 || neighbor >= atomCount || placed[neighbor] != 0)
                continue;

            children[childCount++] = neighbor;
        }

        float3 parentDir = float3(1, 0, 0);
        if (parent[current] >= 0)
        {
            parentDir = atomPositions[current] - atomPositions[parent[current]];
            if (length(parentDir) < 1e-4f)
                parentDir = float3(1, 0, 0);
            else
                parentDir = normalize(parentDir);
        }

        float baseAngle = atan2(parentDir.y, parentDir.x);
        float spread = childCount > 1 ? 1.8f : 0.0f;
        for (int childIdx = 0; childIdx < childCount; childIdx++)
        {
            int child = children[childIdx];
            float angleOffset = childCount == 1 ? 0.0f : lerp(-spread, spread, (float)childIdx / (float)(childCount - 1));
            float angle = baseAngle + angleOffset;
            float3 offset = float3(cos(angle), sin(angle), 0.15f * (depth[current] % 2 == 0 ? 1 : -1));
            atomPositions[child] = atomPositions[current] + normalize(offset) * bondLength;
            parent[child] = current;
            depth[child] = depth[current] + 1;
            placed[child] = 1;
            queue[queueTail++] = child;
        }
    }

    [loop]
    for (int iter = 0; iter < 4; iter++)
    {
        for (int a = 0; a < atomCount; a++)
        {
            int degree = min(atomDegrees[a], MAX_GRAPH_NEIGHBORS);
            for (int slot = 0; slot < degree; slot++)
            {
                int b = neighborIndices[GetGraphArrayIndex(a, slot)];
                if (b <= a || b >= atomCount)
                    continue;

                float3 delta = atomPositions[b] - atomPositions[a];
                float dist = max(length(delta), 1e-4f);
                float3 correction = delta * ((dist - bondLength) / dist) * 0.18f;
                if (a != 0)
                    atomPositions[a] += correction * 0.5f;
                atomPositions[b] -= correction * 0.5f;
            }
        }

        for (int a = 0; a < atomCount; a++)
        {
            for (int b = a + 1; b < atomCount; b++)
            {
                if (HasGraphBond(a, b, atomDegrees, neighborIndices))
                    continue;

                float3 delta = atomPositions[b] - atomPositions[a];
                float dist = max(length(delta), 1e-4f);
                if (dist >= bondLength * 0.75f)
                    continue;

                float3 repulse = delta * ((bondLength * 0.75f - dist) / dist) * 0.06f;
                if (a != 0)
                    atomPositions[a] -= repulse;
                atomPositions[b] += repulse;
            }
        }
    }
}

bool TryParseAtomToken(in int smilesChars[SMILES_MAX_LENGTH], int idx, out int atomType, out int consumedChars)
{
    atomType = ATOM_TYPE_UNKNOWN;
    consumedChars = 0;

    int c = smilesChars[idx];
    int c2 = idx + 1 < SMILES_MAX_LENGTH ? smilesChars[idx + 1] : 0;

    switch (c)
    {
        case 'C':
            if (c2 == 'l')
            {
                atomType = ATOM_TYPE_Cl;
                consumedChars = 2;
            }
            else
            {
                atomType = ATOM_TYPE_C;
                consumedChars = 1;
            }
            return true;
        case 'c': atomType = ATOM_TYPE_c; consumedChars = 1; return true;
        case 'N': atomType = ATOM_TYPE_N; consumedChars = 1; return true;
        case 'n': atomType = ATOM_TYPE_n; consumedChars = 1; return true;
        case 'O': atomType = ATOM_TYPE_O; consumedChars = 1; return true;
        case 'o': atomType = ATOM_TYPE_o; consumedChars = 1; return true;
        case 'S':
            if (c2 == 'i')
            {
                atomType = ATOM_TYPE_Si;
                consumedChars = 2;
            }
            else if (c2 == 'e')
            {
                atomType = ATOM_TYPE_Se;
                consumedChars = 2;
            }
            else
            {
                atomType = ATOM_TYPE_S;
                consumedChars = 1;
            }
            return true;
        case 's': atomType = ATOM_TYPE_s; consumedChars = 1; return true;
        case 'P': atomType = ATOM_TYPE_P; consumedChars = 1; return true;
        case 'p': atomType = ATOM_TYPE_p; consumedChars = 1; return true;
        case 'F': atomType = ATOM_TYPE_F; consumedChars = 1; return true;
        case 'B':
            if (c2 == 'r')
            {
                atomType = ATOM_TYPE_Br;
                consumedChars = 2;
            }
            else
            {
                atomType = ATOM_TYPE_B;
                consumedChars = 1;
            }
            return true;
        case 'I': atomType = ATOM_TYPE_I; consumedChars = 1; return true;
        case 'H': atomType = ATOM_TYPE_H; consumedChars = 1; return true;
        case 'A':
            if (c2 == 's')
            {
                atomType = ATOM_TYPE_As;
                consumedChars = 2;
                return true;
            }
            break;
    }

    return false;
}

void ParseSMILESGraph(
    in int smilesChars[SMILES_MAX_LENGTH],
    out int atomTypes[MAX_ATOM_COUNT],
    out int atomCount,
    out int atomDegrees[MAX_ATOM_COUNT],
    out int neighborIndices[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS],
    out int neighborBondTypes[MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS],
    out int bondCount)
{
    atomCount = 0;
    bondCount = 0;

    for (int i = 0; i < MAX_ATOM_COUNT; i++)
    {
        atomTypes[i] = ATOM_TYPE_UNKNOWN;
        atomDegrees[i] = 0;
    }

    for (int i = 0; i < MAX_ATOM_COUNT * MAX_GRAPH_NEIGHBORS; i++)
    {
        neighborIndices[i] = -1;
        neighborBondTypes[i] = BOND_UNKNOWN;
    }

    int branchStack[MAX_BRANCH_DEPTH];
    for (int i = 0; i < MAX_BRANCH_DEPTH; i++)
        branchStack[i] = -1;

    int ringAtomIndex[10];
    int ringBondType[10];
    for (int i = 0; i < 10; i++)
    {
        ringAtomIndex[i] = -1;
        ringBondType[i] = BOND_UNKNOWN;
    }

    int branchDepth = 0;
    int currentAtom = -1;
    int pendingBondType = BOND_UNKNOWN;

    for (int i = 0; i < SMILES_MAX_LENGTH; i++)
    {
        int c = smilesChars[i];
        if (c == 0 || atomCount >= MAX_ATOM_COUNT)
            break;

        if (c == '-' || c == '=' || c == '#' || c == ':')
        {
            pendingBondType =
                c == '-' ? BOND_SINGLE :
                c == '=' ? BOND_DOUBLE :
                c == '#' ? BOND_TRIPLE : BOND_AROMATIC;
            continue;
        }

        if (c == '(')
        {
            if (branchDepth < MAX_BRANCH_DEPTH && currentAtom >= 0)
                branchStack[branchDepth++] = currentAtom;
            continue;
        }

        if (c == ')')
        {
            if (branchDepth > 0)
                currentAtom = branchStack[--branchDepth];
            continue;
        }

        if (c >= '0' && c <= '9')
        {
            int ringNumber = c - '0';
            if (currentAtom >= 0)
            {
                if (ringAtomIndex[ringNumber] < 0)
                {
                    ringAtomIndex[ringNumber] = currentAtom;
                    ringBondType[ringNumber] = pendingBondType;
                }
                else
                {
                    int bondType = pendingBondType;
                    if (bondType == BOND_UNKNOWN)
                        bondType = ringBondType[ringNumber];
                    if (bondType == BOND_UNKNOWN)
                    {
                        bool aromaticPair = IsAromaticAtomType(atomTypes[currentAtom]) && IsAromaticAtomType(atomTypes[ringAtomIndex[ringNumber]]);
                        bondType = aromaticPair ? BOND_AROMATIC : BOND_SINGLE;
                    }

                    if (TryAddGraphBond(currentAtom, ringAtomIndex[ringNumber], bondType, atomDegrees, neighborIndices, neighborBondTypes))
                    {
                        bondCount++;
                    }

                    ringAtomIndex[ringNumber] = -1;
                    ringBondType[ringNumber] = BOND_UNKNOWN;
                }
            }
            pendingBondType = BOND_UNKNOWN;
            continue;
        }

        if (c == '[' || c == ']' || c == '/' || c == 92 || c == '+' || c == '%' || c == '@')
            continue;

        int atomType;
        int consumedChars;
        if (!TryParseAtomToken(smilesChars, i, atomType, consumedChars))
            continue;

        int newAtom = atomCount++;
        atomTypes[newAtom] = atomType;

        if (currentAtom >= 0)
        {
            int bondType = pendingBondType;
            if (bondType == BOND_UNKNOWN)
            {
                bool aromaticPair = IsAromaticAtomType(atomTypes[currentAtom]) && IsAromaticAtomType(atomType);
                bondType = aromaticPair ? BOND_AROMATIC : BOND_SINGLE;
            }

            if (TryAddGraphBond(currentAtom, newAtom, bondType, atomDegrees, neighborIndices, neighborBondTypes))
            {
                bondCount++;
            }
        }

        currentAtom = newAtom;
        pendingBondType = BOND_UNKNOWN;
        i += consumedChars - 1;
    }
}

// The legacy parser keeps the old atom-only behavior for lightweight kernels.
void ParseSMILESLegacy(in int smilesChars[SMILES_MAX_LENGTH], out int atomTypes[MAX_ATOM_COUNT], out int atomCount)
{
    atomCount = 0;

    for (int i = 0; i < MAX_ATOM_COUNT; i++)
        atomTypes[i] = ATOM_TYPE_UNKNOWN;

    for (int i = 0; i < SMILES_MAX_LENGTH; i++)
    {
        if (atomCount >= MAX_ATOM_COUNT)
            break;

        int c = smilesChars[i];
        if (c == 0)
            break;

        if (c == '-' || c == '=' || c == '#' || c == ':' || c == '$' || c == '%' ||
            c == '(' || c == ')' || c == '[' || c == ']' || c == '/' || c == 92 ||
            (c >= '0' && c <= '9') || c == '+' || c == '@')
        {
            continue;
        }

        int atomType;
        int consumedChars;
        if (!TryParseAtomToken(smilesChars, i, atomType, consumedChars))
            continue;

        atomTypes[atomCount++] = atomType;
        i += consumedChars - 1;
    }
}

// Convenience wrapper retained for kernels that only need atom identities.
void ParseSMILES(in int smilesChars[SMILES_MAX_LENGTH], out int atomTypes[MAX_ATOM_COUNT], out int atomCount)
{
    ParseSMILESLegacy(smilesChars, atomTypes, atomCount);
}




void AtomTypeToSMILES(int atomType, out int chars[3])
{
    
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



