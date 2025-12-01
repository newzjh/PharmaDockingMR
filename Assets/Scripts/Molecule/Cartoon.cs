//=============================================================================
// This file is part of The Scripps Research Institute's C-ME Application built
// by InterKnowlogy.  
//
// Copyright (C) 2006, 2007 Scripps Research Institute / InterKnowlogy, LLC.
// All rights reserved.
//
// For information about this application contact Tim Huckaby at
// TimHuck@InterKnowlogy.com or (760) 930-0075 x201.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
//=============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    /// <summary>
    /// Creates the 3D model for a particular residue when being displayed in cartoon mode.
    /// </summary>
    class Cartoon : HoverObject
    {
        private const int radialSegmentCount = 10;
        private const float turnWidth = 0.2f;
        private const float helixWidth = 1.4f;
        private const float helixHeight = 0.25f;
        private const float sheetWidth = 1.2f;
        private const float sheetHeight = 0.25f;
        private const float arrowWidth = 1.6f;

        public Residue residue;
        private List<Vector3> ribbonPoints = new List<Vector3>();
        private List<Vector3> torsionVectors = new List<Vector3>();
        private List<Vector3> normalVectors = new List<Vector3>();


        private GameObject displayobj;

        public void Represent(MoleculeScheme scheme, bool dirty)
        {
            if (!dirty)
                return;

            DestroyImmediate(displayobj);

            if (residue.atoms.Count > 0)
            {
                if (!scheme.showparts[residue.atoms[0].partIndex])
                    return;
            }

            if (scheme.ContainStyle(MoleculeStyle.Cartoon))
            {
                displayobj = new GameObject("Cartoon");
                displayobj.transform.parent = this.transform;
                displayobj.transform.localPosition = new Vector3(0, 0, 0);
                displayobj.transform.localScale = new Vector3(1, 1, 1);
                displayobj.transform.localEulerAngles = new Vector3(0, 0, 0);
                //MeshFilter mf = displayobj.AddComponent<MeshFilter>();
                //Mesh mesh = new Mesh();

                this.residue.Ribbon.GetResidueSpline(this.residue, out this.ribbonPoints,
                    out this.torsionVectors, out this.normalVectors);

                if (this.residue.IsHelix)
                {
                    this.AddTube(Cartoon.helixWidth, Cartoon.helixHeight);

                    if (this.residue.IsStructureStart)
                        this.AddTubeCap(Cartoon.helixWidth, Cartoon.helixHeight);

                    if (this.residue.IsStructureEnd)
                        this.AddTubeCap(Cartoon.helixWidth, Cartoon.helixHeight);
                }
                else if (this.residue.IsSheet)
                {
                    this.AddSheet();

                    if (this.residue.IsStructureStart || this.residue.IsStructureEnd)
                        this.AddSheetCap();
                }
                else
                {
                    this.AddTube(Cartoon.turnWidth, Cartoon.turnWidth);

                    if (this.residue.IsStructureStart)
                        this.AddTubeCap(Cartoon.turnWidth, Cartoon.turnWidth);

                    if (this.residue.IsStructureEnd)
                        this.AddTubeCap(Cartoon.turnWidth, Cartoon.turnWidth);
                }

                //if (Application.isPlaying)
                {
                    MeshRenderer[] mrs = displayobj.GetComponentsInChildren<MeshRenderer>();
                    foreach (MeshRenderer mr in mrs)
                    {
                        mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(residue.StructureColor);
                        //if (residue.Chain)
                        //    mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(residue.Chain.ChainColor);
                        //else
                        //    mr.sharedMaterial = MaterialLib.GetStandardMaterialByColor(residue.ResidueColor);
                    }
                }

            }
        }

        /// <summary>
        /// Gets and sets the color of the model.
        /// </summary>
        public Color cartooncolor;



        /// <summary>
        /// Creates a cylindrical tube along the spline path.
        /// </summary>
        /// <param name="width">The x-radius of the extrusion ellipse.</param>
        /// <param name="height">The y-radius of the extrusion ellipse.</param>
        private void AddTube(float width, float height)
        {
            GameObject subobj = new GameObject();
            subobj.name = "Tube";
            subobj.transform.parent = displayobj.transform;
            subobj.transform.localPosition = new Vector3(0, 0, 0);
            subobj.transform.localScale = new Vector3(1, 1, 1);
            subobj.transform.localEulerAngles = new Vector3(0, 0, 0);
            MeshFilter mf = subobj.AddComponent<MeshFilter>();

            List<Vector3> Positions = new List<Vector3>();
            List<Vector3> Normals = new List<Vector3>();
            List<int> TriangleIndices = new List<int>();

            for (int i = 0; i < this.ribbonPoints.Count; i++)
            {
                for (int j = 0; j < Cartoon.radialSegmentCount; j++)
                {
                    float t = (float)(2 * Math.PI * j / Cartoon.radialSegmentCount);

                    Vector3 radialVector = width * (float)Math.Cos(t) * this.torsionVectors[i] +
                        height * (float)Math.Sin(t) * this.normalVectors[i];
                    Positions.Add(this.ribbonPoints[i] + radialVector);

                    Vector3 normalVector = height * (float)Math.Cos(t) * this.torsionVectors[i] +
                        width * (float)Math.Sin(t) * this.normalVectors[i];
                    normalVector.Normalize();
                    Normals.Add(normalVector);
                }
            }

            int rsc = Cartoon.radialSegmentCount;

            for (int i = 0; i < this.ribbonPoints.Count - 1; i++)
            {
                for (int j = 0; j < Cartoon.radialSegmentCount; j++)
                {
                    TriangleIndices.Add(i * rsc + j);
                    TriangleIndices.Add((i + 1) * rsc + (j + 1) % rsc);
                    TriangleIndices.Add(i * rsc + (j + 1) % rsc);

                    TriangleIndices.Add(i * rsc + j);
                    TriangleIndices.Add((i + 1) * rsc + j);
                    TriangleIndices.Add((i + 1) * rsc + (j + 1) % rsc);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Tube";
            mesh.vertices = Positions.ToArray();
            mesh.normals = Normals.ToArray();
            mesh.triangles = TriangleIndices.ToArray();
            mf.mesh = mesh;
            MeshRenderer mr = subobj.AddComponent<MeshRenderer>();
            MeshCollider mc = subobj.AddComponent<MeshCollider>();
        }

        /// <summary>
        /// Creates an elliptical cap for a tube along the spline path.
        /// </summary>
        /// <param name="width">The x-radius of the cap ellipse.</param>
        /// <param name="height">The y-radius of the cap ellipse.</param>
        private void AddTubeCap(float width, float height)
        {

            GameObject subobj = new GameObject();
            subobj.name = "TubeCap";
            subobj.transform.parent = displayobj.transform;
            subobj.transform.localPosition = new Vector3(0, 0, 0);
            subobj.transform.localScale = new Vector3(1, 1, 1);
            subobj.transform.localEulerAngles = new Vector3(0, 0, 0);
            MeshFilter mf = subobj.AddComponent<MeshFilter>();

            List<Vector3> Positions = new List<Vector3>();
            List<Vector3> Normals = new List<Vector3>();
            List<int> TriangleIndices = new List<int>();

            Vector3 normalVector = Vector3.Cross(
                this.torsionVectors[0], this.normalVectors[0]);

            if (this.residue.IsStructureEnd) normalVector = -normalVector;

            int offset = this.residue.IsStructureStart ? 0 : this.ribbonPoints.Count - 1;

            Positions.Add(this.ribbonPoints[offset]);
            Normals.Add(normalVector);

            for (int i = 0; i < Cartoon.radialSegmentCount; i++)
            {
                double t = 2 * Math.PI * i / Cartoon.radialSegmentCount;

                Vector3 radialVector = width * (float)Math.Cos(t) * this.torsionVectors[offset] +
                    height * (float)Math.Sin(t) * this.normalVectors[offset];
                Positions.Add(this.ribbonPoints[offset] + radialVector);

                Normals.Add(normalVector);

                TriangleIndices.Add(0);

                if (this.residue.IsStructureStart)
                {
                    TriangleIndices.Add(i + 1);
                    TriangleIndices.Add((i + 1) % Cartoon.radialSegmentCount + 1);
                }
                else
                {
                    TriangleIndices.Add((i + 1) % Cartoon.radialSegmentCount + 1);
                    TriangleIndices.Add(i + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "TubeCap";
            mesh.vertices = Positions.ToArray();
            mesh.normals = Normals.ToArray();
            mesh.triangles = TriangleIndices.ToArray();
            mf.mesh = mesh;
            MeshRenderer mr = subobj.AddComponent<MeshRenderer>();
            MeshCollider mc = subobj.AddComponent<MeshCollider>();
        }

        /// <summary>
        /// Creates a rectangular solid sheet along the spline path.
        /// </summary>
        private void AddSheet()
        {
            GameObject subobj = new GameObject();
            subobj.name = "Sheet";
            subobj.transform.parent = displayobj.transform;
            subobj.transform.localPosition = new Vector3(0, 0, 0);
            subobj.transform.localScale = new Vector3(1, 1, 1);
            subobj.transform.localEulerAngles = new Vector3(0, 0, 0);
            MeshFilter mf = subobj.AddComponent<MeshFilter>();

            List<Vector3> Positions = new List<Vector3>();
            List<Vector3> Normals = new List<Vector3>();
            List<int> TriangleIndices = new List<int>();

            double offsetLength = 0;

            if (this.residue.IsStructureEnd)
            {
                Vector3 lengthVector = this.ribbonPoints[this.ribbonPoints.Count - 1] -
                    this.ribbonPoints[0];
                offsetLength = Cartoon.arrowWidth / lengthVector.magnitude;
            }

            for (int i = 0; i < this.ribbonPoints.Count; i++)
            {
                float actualWidth = !this.residue.IsStructureEnd ? Cartoon.sheetWidth :
                    Cartoon.arrowWidth * (1 - (float)i / (this.ribbonPoints.Count - 1));

                Vector3 horizontalVector = actualWidth * this.torsionVectors[i];
                Vector3 verticalVector = Cartoon.sheetHeight * this.normalVectors[i];

                Positions.Add(this.ribbonPoints[i] + horizontalVector + verticalVector);
                Positions.Add(this.ribbonPoints[i] - horizontalVector + verticalVector);
                Positions.Add(this.ribbonPoints[i] - horizontalVector + verticalVector);
                Positions.Add(this.ribbonPoints[i] - horizontalVector - verticalVector);
                Positions.Add(this.ribbonPoints[i] - horizontalVector - verticalVector);
                Positions.Add(this.ribbonPoints[i] + horizontalVector - verticalVector);
                Positions.Add(this.ribbonPoints[i] + horizontalVector - verticalVector);
                Positions.Add(this.ribbonPoints[i] + horizontalVector + verticalVector);

                Vector3 normalOffset = new Vector3();

                if (this.residue.IsStructureEnd)
                {
                    normalOffset = (float)offsetLength * Vector3.Cross(
                        this.normalVectors[i], this.torsionVectors[i]);
                }

                Normals.Add(this.normalVectors[i]);
                Normals.Add(this.normalVectors[i]);
                Normals.Add(-this.torsionVectors[i] + normalOffset);
                Normals.Add(-this.torsionVectors[i] + normalOffset);
                Normals.Add(-this.normalVectors[i]);
                Normals.Add(-this.normalVectors[i]);
                Normals.Add(this.torsionVectors[i] + normalOffset);
                Normals.Add(this.torsionVectors[i] + normalOffset);
            }

            for (int i = 0; i < this.ribbonPoints.Count - 1; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    TriangleIndices.Add(i * 8 + 2 * j);
                    TriangleIndices.Add((i + 1) * 8 + 2 * j + 1);
                    TriangleIndices.Add(i * 8 + 2 * j + 1);

                    TriangleIndices.Add(i * 8 + 2 * j);
                    TriangleIndices.Add((i + 1) * 8 + 2 * j);
                    TriangleIndices.Add((i + 1) * 8 + 2 * j + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Sheet";
            mesh.vertices = Positions.ToArray();
            mesh.normals = Normals.ToArray();
            mesh.triangles = TriangleIndices.ToArray();
            mf.mesh = mesh;
            MeshRenderer mr = subobj.AddComponent<MeshRenderer>();
            MeshCollider mc = subobj.AddComponent<MeshCollider>();
        }

        /// <summary>
        /// Creates a flat cap or an arrow head cap for a sheet.
        /// </summary>
        private void AddSheetCap()
        {
            GameObject subobj = new GameObject();
            subobj.name = "SheetCap";
            subobj.transform.parent = displayobj.transform;
            subobj.transform.localPosition = new Vector3(0, 0, 0);
            subobj.transform.localScale = new Vector3(1, 1, 1);
            subobj.transform.localEulerAngles = new Vector3(0, 0, 0);
            MeshFilter mf = subobj.AddComponent<MeshFilter>();

            List<Vector3> Positions = new List<Vector3>();
            List<Vector3> Normals = new List<Vector3>();
            List<int> TriangleIndices = new List<int>();

            Vector3 horizontalVector = Cartoon.sheetWidth * this.torsionVectors[0];
            Vector3 verticalVector = Cartoon.sheetHeight * this.normalVectors[0];

            Vector3 p1 = this.ribbonPoints[0] + horizontalVector + verticalVector;
            Vector3 p2 = this.ribbonPoints[0] - horizontalVector + verticalVector;
            Vector3 p3 = this.ribbonPoints[0] - horizontalVector - verticalVector;
            Vector3 p4 = this.ribbonPoints[0] + horizontalVector - verticalVector;

            if (this.residue.IsStructureStart)
            {
                this.AddSheetCapSection(ref Positions, ref Normals, ref TriangleIndices, p1, p2, p3, p4);
            }
            else
            {
                Vector3 arrowHorizontalVector = Cartoon.arrowWidth * this.torsionVectors[0];

                Vector3 p5 = this.ribbonPoints[0] + arrowHorizontalVector + verticalVector;
                Vector3 p6 = this.ribbonPoints[0] - arrowHorizontalVector + verticalVector;
                Vector3 p7 = this.ribbonPoints[0] - arrowHorizontalVector - verticalVector;
                Vector3 p8 = this.ribbonPoints[0] + arrowHorizontalVector - verticalVector;

                this.AddSheetCapSection(ref Positions, ref Normals, ref TriangleIndices, p5, p1, p4, p8);
                this.AddSheetCapSection(ref Positions, ref Normals, ref TriangleIndices, p2, p6, p7, p3);
            }

            Mesh mesh = new Mesh();
            mesh.name = "SheetCap";
            mesh.vertices = Positions.ToArray();
            mesh.normals = Normals.ToArray();
            mesh.triangles = TriangleIndices.ToArray();
            mf.mesh = mesh;
            MeshRenderer mr = subobj.AddComponent<MeshRenderer>();
            MeshCollider mc = subobj.AddComponent<MeshCollider>();
        }

        /// <summary>
        /// Helper method to add a quadrilateral surface for a sheet cap.
        /// </summary>
        /// <param name="capMesh">The mesh to add the triangles to.</param>
        /// <param name="p1">Point 1.</param>
        /// <param name="p2">Point 2.</param>
        /// <param name="p3">Point 3.</param>
        /// <param name="p4">Point 4.</param>
        private void AddSheetCapSection(ref List<Vector3> Positions, ref List<Vector3> Normals, ref List<int> TriangleIndices,
             Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            int indexOffset = Positions.Count;

            Positions.Add(p1);
            Positions.Add(p2);
            Positions.Add(p3);
            Positions.Add(p4);

            TriangleIndices.Add(indexOffset + 0);
            TriangleIndices.Add(indexOffset + 1);
            TriangleIndices.Add(indexOffset + 2);

            TriangleIndices.Add(indexOffset + 2);
            TriangleIndices.Add(indexOffset + 3);
            TriangleIndices.Add(indexOffset + 0);

            Vector3 normalVector = Vector3.Cross(p2 - p1, p4 - p1);
            normalVector.Normalize();

            for (int i = 0; i < 4; i++)
                Normals.Add(normalVector);
        }
    }
}
