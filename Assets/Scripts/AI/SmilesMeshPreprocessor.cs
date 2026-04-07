using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIDrugDiscovery
{
    [Serializable]
    public struct SmilesMeshBond
    {
        public int AtomA;
        public int AtomB;
        public int BondType;
    }

    [Serializable]
    public struct SmilesMeshBondIndex
    {
        public int AtomA;
        public int AtomB;
    }

    public sealed class SmilesMeshDescription
    {
        public readonly List<int> AtomTypes = new List<int>();
        public readonly List<Vector3> AtomPositions = new List<Vector3>();
        public readonly List<SmilesMeshBond> Bonds = new List<SmilesMeshBond>();
    }

    public static class SmilesMeshPreprocessor
    {
        public const int MaxAtomCount = 60;
        private const int MaxRingCount = 10;

        public static string DecodeAsciiSmiles(int[] smilesData)
        {
            if (smilesData == null || smilesData.Length == 0)
                return string.Empty;

            char[] chars = new char[smilesData.Length];
            int count = 0;
            for (int i = 0; i < smilesData.Length; i++)
            {
                int value = smilesData[i];
                if (value <= 0)
                    break;

                chars[count++] = (char)value;
            }

            return new string(chars, 0, count);
        }

        public static async UniTask<List<string>> ReadSmilesBatchAsync(
            ComputeBuffer smilesBuffer,
            int batchSize,
            int smilesMaxLength,
            Texture legacySmilesTexture = null)
        {
            if (batchSize <= 0)
                return new List<string>();

            if (smilesBuffer != null)
            {
                var request = await AsyncGPUReadback.RequestAsync(smilesBuffer);
                int[] raw = request.GetData<int>().ToArray();
                return DecodeSmilesBatch(raw, batchSize, smilesMaxLength);
            }

            if (legacySmilesTexture != null)
            {
                int[] raw = ReadSmilesTextureData(legacySmilesTexture, batchSize, smilesMaxLength);
                return DecodeSmilesBatch(raw, batchSize, smilesMaxLength);
            }

            return new List<string>();
        }

        public static List<SmilesMeshDescription> BuildBatch(IReadOnlyList<string> smilesList, float bondLength)
        {
            List<SmilesMeshDescription> descriptions = new List<SmilesMeshDescription>(smilesList?.Count ?? 0);
            if (smilesList == null)
                return descriptions;

            for (int i = 0; i < smilesList.Count; i++)
                descriptions.Add(Build(smilesList[i], bondLength));

            return descriptions;
        }

        public static SmilesMeshDescription Build(string smiles, float bondLength)
        {
            SmilesMeshDescription description = new SmilesMeshDescription();
            if (string.IsNullOrWhiteSpace(smiles))
                return description;

            int currentAtom = -1;
            int pendingBondType = 0;
            Stack<int> branchStack = new Stack<int>();
            int[] ringAtoms = new int[MaxRingCount];
            int[] ringBondTypes = new int[MaxRingCount];
            for (int i = 0; i < MaxRingCount; i++)
            {
                ringAtoms[i] = -1;
                ringBondTypes[i] = 0;
            }

            int cursor = 0;
            while (cursor < smiles.Length && description.AtomTypes.Count < MaxAtomCount)
            {
                char c = smiles[cursor];
                if (char.IsWhiteSpace(c))
                {
                    cursor++;
                    continue;
                }

                if (TryParseBond(c, out int explicitBondType))
                {
                    pendingBondType = explicitBondType;
                    cursor++;
                    continue;
                }

                if (c == '(')
                {
                    if (currentAtom >= 0)
                        branchStack.Push(currentAtom);
                    cursor++;
                    continue;
                }

                if (c == ')')
                {
                    if (branchStack.Count > 0)
                        currentAtom = branchStack.Pop();
                    cursor++;
                    continue;
                }

                if (char.IsDigit(c))
                {
                    int ringIndex = c - '0';
                    if (ringIndex >= 0 && ringIndex < MaxRingCount && currentAtom >= 0)
                    {
                        if (ringAtoms[ringIndex] >= 0)
                        {
                            AddBond(description, currentAtom, ringAtoms[ringIndex], pendingBondType != 0 ? pendingBondType : ringBondTypes[ringIndex]);
                            ringAtoms[ringIndex] = -1;
                            ringBondTypes[ringIndex] = 0;
                        }
                        else
                        {
                            ringAtoms[ringIndex] = currentAtom;
                            ringBondTypes[ringIndex] = pendingBondType;
                        }
                    }

                    pendingBondType = 0;
                    cursor++;
                    continue;
                }

                if (c == '[')
                {
                    if (TryParseBracketAtom(smiles, ref cursor, out int bracketAtomType))
                    {
                        currentAtom = AddAtomAndConnect(description, currentAtom, bracketAtomType, pendingBondType);
                    }
                    pendingBondType = 0;
                    continue;
                }

                if (TryParseAtom(smiles, ref cursor, out int atomType))
                {
                    currentAtom = AddAtomAndConnect(description, currentAtom, atomType, pendingBondType);
                    pendingBondType = 0;
                    continue;
                }

                cursor++;
            }

            BuildAtomLayout(description, bondLength);
            return description;
        }

        private static List<string> DecodeSmilesBatch(int[] raw, int batchSize, int smilesMaxLength)
        {
            List<string> smilesList = new List<string>(batchSize);
            for (int molIdx = 0; molIdx < batchSize; molIdx++)
            {
                char[] chars = new char[smilesMaxLength];
                int count = 0;
                int baseIndex = molIdx * smilesMaxLength;
                for (int i = 0; i < smilesMaxLength && baseIndex + i < raw.Length; i++)
                {
                    int value = raw[baseIndex + i];
                    if (value <= 0)
                        break;

                    chars[count++] = (char)value;
                }

                smilesList.Add(new string(chars, 0, count));
            }

            return smilesList;
        }

        private static int[] ReadSmilesTextureData(Texture texture, int batchSize, int smilesMaxLength)
        {
            Texture2D readableTexture = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                if (texture is Texture2D texture2D)
                {
                    readableTexture = texture2D;
                }
                else if (texture is RenderTexture renderTexture)
                {
                    RenderTexture.active = renderTexture;
                    readableTexture = new Texture2D(smilesMaxLength, batchSize, TextureFormat.RGBA32, false);
                    readableTexture.ReadPixels(new Rect(0, 0, smilesMaxLength, batchSize), 0, 0);
                    readableTexture.Apply();
                }
                else
                {
                    return new int[batchSize * smilesMaxLength];
                }

                int[] raw = new int[batchSize * smilesMaxLength];
                for (int y = 0; y < batchSize; y++)
                {
                    for (int x = 0; x < smilesMaxLength; x++)
                    {
                        raw[y * smilesMaxLength + x] = Mathf.RoundToInt(readableTexture.GetPixel(x, y).r * 255f);
                    }
                }

                return raw;
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture is RenderTexture && readableTexture != null)
                    UnityEngine.Object.Destroy(readableTexture);
            }
        }

        private static bool TryParseBond(char c, out int bondType)
        {
            switch (c)
            {
                case '=':
                    bondType = 1;
                    return true;
                case '#':
                    bondType = 2;
                    return true;
                case ':':
                    bondType = 3;
                    return true;
                case '-':
                    bondType = 0;
                    return true;
                default:
                    bondType = 0;
                    return false;
            }
        }

        private static bool TryParseBracketAtom(string smiles, ref int cursor, out int atomType)
        {
            atomType = 0;
            int end = smiles.IndexOf(']', cursor + 1);
            if (end < 0)
            {
                cursor++;
                return false;
            }

            for (int i = cursor + 1; i < end; i++)
            {
                char c = smiles[i];
                if (!char.IsLetter(c))
                    continue;

                if (TryMapAtom(smiles, i, end, out atomType, out int consumed))
                {
                    cursor = end + 1;
                    return true;
                }
            }

            cursor = end + 1;
            return false;
        }

        private static bool TryParseAtom(string smiles, ref int cursor, out int atomType)
        {
            atomType = 0;
            if (!TryMapAtom(smiles, cursor, smiles.Length, out atomType, out int consumed))
                return false;

            cursor += consumed;
            return true;
        }

        private static bool TryMapAtom(string smiles, int start, int limit, out int atomType, out int consumed)
        {
            atomType = 0;
            consumed = 0;
            char c = smiles[start];

            if (char.IsLower(c))
            {
                consumed = 1;
                switch (c)
                {
                    case 'c': atomType = 66; return true;
                    case 'n': atomType = 77; return true;
                    case 'o': atomType = 88; return true;
                    case 's': atomType = 166; return true;
                    case 'p': atomType = 155; return true;
                }
            }

            if (!char.IsUpper(c))
                return false;

            if (start + 1 < limit)
            {
                char next = smiles[start + 1];
                if (c == 'C' && next == 'l') { atomType = 17; consumed = 2; return true; }
                if (c == 'B' && next == 'r') { atomType = 35; consumed = 2; return true; }
                if (c == 'S' && next == 'i') { atomType = 14; consumed = 2; return true; }
                if (c == 'A' && next == 's') { atomType = 33; consumed = 2; return true; }
                if (c == 'S' && next == 'e') { atomType = 34; consumed = 2; return true; }
            }

            consumed = 1;
            switch (c)
            {
                case 'H': atomType = 1; return true;
                case 'B': atomType = 5; return true;
                case 'C': atomType = 6; return true;
                case 'N': atomType = 7; return true;
                case 'O': atomType = 8; return true;
                case 'F': atomType = 9; return true;
                case 'P': atomType = 15; return true;
                case 'S': atomType = 16; return true;
                case 'I': atomType = 53; return true;
                default:
                    atomType = 0;
                    return false;
            }
        }

        private static int AddAtomAndConnect(SmilesMeshDescription description, int currentAtom, int atomType, int pendingBondType)
        {
            int newAtomIndex = description.AtomTypes.Count;
            description.AtomTypes.Add(atomType);
            description.AtomPositions.Add(Vector3.zero);

            if (currentAtom >= 0)
                AddBond(description, currentAtom, newAtomIndex, pendingBondType);

            return newAtomIndex;
        }

        private static void AddBond(SmilesMeshDescription description, int atomA, int atomB, int bondType)
        {
            if (atomA < 0 || atomB < 0 || atomA == atomB)
                return;

            for (int i = 0; i < description.Bonds.Count; i++)
            {
                SmilesMeshBond existing = description.Bonds[i];
                if ((existing.AtomA == atomA && existing.AtomB == atomB) || (existing.AtomA == atomB && existing.AtomB == atomA))
                    return;
            }

            description.Bonds.Add(new SmilesMeshBond
            {
                AtomA = atomA,
                AtomB = atomB,
                BondType = bondType
            });
        }

        private static void BuildAtomLayout(SmilesMeshDescription description, float bondLength)
        {
            int atomCount = description.AtomTypes.Count;
            if (atomCount == 0)
                return;

            List<int>[] adjacency = new List<int>[atomCount];
            for (int i = 0; i < atomCount; i++)
                adjacency[i] = new List<int>(4);

            for (int i = 0; i < description.Bonds.Count; i++)
            {
                SmilesMeshBond bond = description.Bonds[i];
                adjacency[bond.AtomA].Add(bond.AtomB);
                adjacency[bond.AtomB].Add(bond.AtomA);
            }

            Vector3[] positions = new Vector3[atomCount];
            int[] parent = new int[atomCount];
            int[] depth = new int[atomCount];
            bool[] placed = new bool[atomCount];
            for (int i = 0; i < atomCount; i++)
                parent[i] = -1;

            Queue<int> queue = new Queue<int>();
            placed[0] = true;
            queue.Enqueue(0);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                List<int> children = new List<int>();
                for (int i = 0; i < adjacency[current].Count; i++)
                {
                    int neighbor = adjacency[current][i];
                    if (placed[neighbor])
                        continue;

                    children.Add(neighbor);
                }

                Vector3 parentDir = Vector3.right;
                if (parent[current] >= 0)
                {
                    parentDir = positions[current] - positions[parent[current]];
                    if (parentDir.sqrMagnitude < 1e-6f)
                        parentDir = Vector3.right;
                    else
                        parentDir.Normalize();
                }

                float baseAngle = Mathf.Atan2(parentDir.y, parentDir.x);
                float spread = children.Count > 1 ? 1.8f : 0f;
                for (int childIdx = 0; childIdx < children.Count; childIdx++)
                {
                    int child = children[childIdx];
                    float t = children.Count == 1 ? 0.5f : childIdx / (float)(children.Count - 1);
                    float angle = baseAngle + Mathf.Lerp(-spread, spread, t);
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), (depth[current] & 1) == 0 ? 0.15f : -0.15f).normalized * bondLength;
                    positions[child] = positions[current] + offset;
                    parent[child] = current;
                    depth[child] = depth[current] + 1;
                    placed[child] = true;
                    queue.Enqueue(child);
                }
            }

            for (int iter = 0; iter < 4; iter++)
            {
                for (int i = 0; i < description.Bonds.Count; i++)
                {
                    SmilesMeshBond bond = description.Bonds[i];
                    int a = bond.AtomA;
                    int b = bond.AtomB;
                    Vector3 delta = positions[b] - positions[a];
                    float dist = Mathf.Max(delta.magnitude, 1e-4f);
                    Vector3 correction = delta * ((dist - bondLength) / dist) * 0.18f;
                    if (a != 0)
                        positions[a] += correction * 0.5f;
                    positions[b] -= correction * 0.5f;
                }

                for (int a = 0; a < atomCount; a++)
                {
                    for (int b = a + 1; b < atomCount; b++)
                    {
                        if (AreBonded(description, a, b))
                            continue;

                        Vector3 delta = positions[b] - positions[a];
                        float dist = Mathf.Max(delta.magnitude, 1e-4f);
                        if (dist >= bondLength * 0.75f)
                            continue;

                        Vector3 repulse = delta * ((bondLength * 0.75f - dist) / dist) * 0.06f;
                        if (a != 0)
                            positions[a] -= repulse;
                        positions[b] += repulse;
                    }
                }
            }

            description.AtomPositions.Clear();
            description.AtomPositions.AddRange(positions);
        }

        private static bool AreBonded(SmilesMeshDescription description, int atomA, int atomB)
        {
            for (int i = 0; i < description.Bonds.Count; i++)
            {
                SmilesMeshBond bond = description.Bonds[i];
                if ((bond.AtomA == atomA && bond.AtomB == atomB) || (bond.AtomA == atomB && bond.AtomB == atomA))
                    return true;
            }

            return false;
        }
    }
}
