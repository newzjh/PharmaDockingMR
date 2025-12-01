using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshLib 
{
    public enum MeshType
    { 
        Sphere=0,
        Cylinder,
    }

    private static Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
    public static Mesh CreateMesh(MeshType type,int level)
    {
        int key=(int)type*10+level;
        Mesh mesh = null;
        if (meshes.ContainsKey(key))
        {
            mesh=meshes[key];
        }
        if (mesh==null)
        {
            if (type == MeshType.Sphere)
            {
                mesh = CreateSphere(level);
            }
            else if (type == MeshType.Cylinder)
            {
                mesh = CreateCylinder(level);
            }
            meshes[key] = mesh;
        }
        return mesh;
    }

    private static Mesh CreateSphere(int level)
    {
        Mesh mesh = new Mesh();
        mesh.name = "SphereMesh"+level.ToString();

        int m_lotnumber = 4;
        int m_latnumber = 4;
        if (level == 5)
        {
            m_lotnumber = 32;
            m_latnumber = 16;
        }
        else if (level == 4)
        {
            m_lotnumber = 16;
            m_latnumber = 16;
        }
        else if (level == 3)
        {
            m_lotnumber = 16;
            m_latnumber = 8;
        }
        else if (level == 2)
        {
            m_lotnumber = 8;
            m_latnumber = 8;
        }
        else if (level == 1)
        {
            m_lotnumber = 8;
            m_latnumber = 4;
        }
        else
        {
            m_lotnumber = 4;
            m_latnumber = 4; 
        }
        int vertexnumber = (m_lotnumber + 1) * (m_latnumber + 1);
        int indexnumber = m_lotnumber * m_latnumber * 6;
        Vector3[] vertices = new Vector3[vertexnumber];
        Vector3[] normals = new Vector3[vertexnumber];
        int[] indices = new int[indexnumber];

        float dyangle = 1.0f * Mathf.PI / (float)m_latnumber;
        float dxangle = 2.0f * Mathf.PI / (float)m_lotnumber;
        for (int y = 0; y <= m_latnumber; y++)
        {
            for (int x = 0; x <= m_lotnumber; x++)
            {
                float xangle = (float)x * dxangle - Mathf.PI / 2.0f;
                float yangle = (float)y * dyangle + Mathf.PI / 2.0f;
                Vector3 normal;
                normal.y = Mathf.Sin(yangle);
                normal.x = Mathf.Cos(yangle) * Mathf.Cos(xangle);
                normal.z = Mathf.Cos(yangle) * Mathf.Sin(xangle);
                normals[y * (m_lotnumber + 1) + x] = normal;
                Vector3 pos = normal * 0.5f;
                vertices[y * (m_lotnumber + 1) + x] = pos;
            }
        }

        int iindexcount = 0;
        for (int y = 0; y < m_latnumber; y++)
        {
            for (int x = 0; x < m_lotnumber; x++)
            {
                indices[iindexcount + 2] = y * (m_lotnumber + 1) + x;
                indices[iindexcount + 1] = (y + 1) * (m_lotnumber + 1) + x;
                indices[iindexcount + 0] = y * (m_lotnumber + 1) + x + 1;
                indices[iindexcount + 5] = y * (m_lotnumber + 1) + x + 1;
                indices[iindexcount + 4] = (y + 1) * (m_lotnumber + 1) + x;
                indices[iindexcount + 3] = (y + 1) * (m_lotnumber + 1) + x + 1;
                iindexcount += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = indices;

        return mesh;
    }

    private static Mesh CreateCylinder(int level)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CylinderMesh" + level.ToString();

        int m_lotnumber = 8;
        int m_latnumber = 1;
        if (level == 4)
        {
            m_lotnumber = 32;
            m_latnumber = 1;
        }
        else if (level == 3)
        {
            m_lotnumber = 16;
            m_latnumber = 1;
        }
        else if (level == 2)
        {
            m_lotnumber = 16;
            m_latnumber = 1;
        }
        else if (level == 1)
        {
            m_lotnumber = 8;
            m_latnumber = 1;
        }
        else
        {
            m_lotnumber = 8;
            m_latnumber = 1;
        }
        int vertexnumber = (m_lotnumber + 1) * (m_latnumber + 1);
        int indexnumber = m_lotnumber * m_latnumber * 6;
        Vector3[] vertices = new Vector3[vertexnumber];
        Vector3[] normals = new Vector3[vertexnumber];
        int[] indices = new int[indexnumber];

        float dyangle = 1.0f * Mathf.PI / (float)m_latnumber;
        float dheight = 2.0f / (float)m_latnumber;
        float dxangle = 2.0f * Mathf.PI / (float)m_lotnumber;
        for (int y = 0; y <= m_latnumber; y++)
        {
            for (int x = 0; x <= m_lotnumber; x++)
            {
                float xangle = (float)x * dxangle - Mathf.PI / 2.0f;
                float yheight = (float)y * dheight - 1.0f ;
                float yangle = (float)y * dyangle + Mathf.PI / 2.0f;
                Vector3 normal;
                normal.y = 0.0f;
                normal.x = 1.0f * Mathf.Cos(xangle);
                normal.z = 1.0f * Mathf.Sin(xangle);
                normals[y * (m_lotnumber + 1) + x] = normal;
                Vector3 pos;
                pos.y = yheight;
                pos.x = 0.5f * Mathf.Cos(xangle);
                pos.z = 0.5f * Mathf.Sin(xangle);
                vertices[y * (m_lotnumber + 1) + x] = pos;
            }
        }

        int iindexcount = 0;
        for (int y = 0; y < m_latnumber; y++)
        {
            for (int x = 0; x < m_lotnumber; x++)
            {
                indices[iindexcount + 0] = y * (m_lotnumber + 1) + x;
                indices[iindexcount + 1] = (y + 1) * (m_lotnumber + 1) + x;
                indices[iindexcount + 2] = y * (m_lotnumber + 1) + x + 1;
                indices[iindexcount + 3] = y * (m_lotnumber + 1) + x + 1;
                indices[iindexcount + 4] = (y + 1) * (m_lotnumber + 1) + x;
                indices[iindexcount + 5] = (y + 1) * (m_lotnumber + 1) + x + 1;
                iindexcount += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = indices;

        return mesh;
    }

}
