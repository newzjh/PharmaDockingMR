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
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    /// <summary>
    /// Calculates spline paths for all residues in a particular structure.
    /// </summary>
    public class Ribbon
    {
        private const int linearSegmentCount = 10;

        private List<Residue> residues = new List<Residue>();
        private List<bool> isHelixList = new List<bool>();
        private List<Vector3> caList = new List<Vector3>();
        private List<Vector3> oList = new List<Vector3>();
        private List<Vector3> pList = new List<Vector3>();
        private List<Vector3> cList = new List<Vector3>();
        private List<Vector3> dList = new List<Vector3>();
        private List<Vector3> ribbonPoints = new List<Vector3>();
        private List<Vector3> torsionVectors = new List<Vector3>();
        private List<Vector3> normalVectors = new List<Vector3>();

        /// <summary>
        /// All of the residues in the secondary strucuture associated with this
        /// <see cref="Ribbon" />.
        /// </summary>
        public List<Residue> Residues { get { return this.residues; } }

        /// <summary>
        /// Initiates the spine calculation logic for all constituent residues.
        /// </summary>
        public void CreateControlPoints()
        {
            if (this.residues.Count < 4)
            {
                foreach (Residue residue in this.residues)
                    residue.Ribbon = null;

                return;
            }

			//remove the residue that without a structure, becase it may cause Vector3.zero CAlphaPosition
            int structureCount = 0;
            List<Residue> removeRes = new List<Residue>();
            foreach (Residue residue in this.residues)
            {
                if (residue.IsStructureStart) structureCount++;
                if (structureCount <= 0)
                {
                    removeRes.Add(residue);
                }
               if (residue.IsStructureEnd) structureCount--;
            }

            if (this.residues.Count <= removeRes.Count)
            {
                foreach (Residue residue in this.residues)
                    residue.Ribbon = null;
                removeRes.Clear();
                return;
            }

            foreach (Residue revResidue in removeRes)
            {
                this.residues.Remove(revResidue);
            }

            removeRes.Clear();

            this.PopulateAtomLists();
            this.PopulateControlLists();
            this.PopulateSplineLists();
        }

        /// <summary>
        /// Gets all of the values that represent the spline for a particular residue.
        /// </summary>
        /// <param name="residue">A residue in the corresponding secondary structure.</param>
        /// <param name="residueRibbonPoints">A list control points for the spline.</param>
        /// <param name="residueTorsionVectors">A list of the torsion vectors for the
        /// spline.</param>
        /// <param name="residueNormalVectors">A list of the normal vectors for the spline.</param>
        public void GetResidueSpline(Residue residue, out List<Vector3> residueRibbonPoints,
            out List<Vector3> residueTorsionVectors, out List<Vector3> residueNormalVectors)
        {
            residueRibbonPoints = new List<Vector3>();
            residueTorsionVectors = new List<Vector3>();
            residueNormalVectors = new List<Vector3>();

            if (!this.residues.Contains(residue))
            {
                return;
            }

            int startIndex = this.residues.IndexOf(residue) * Ribbon.linearSegmentCount;

            for (int i = startIndex; i <= startIndex + Ribbon.linearSegmentCount; i++)
            {
                residueRibbonPoints.Add(this.ribbonPoints[i]);
                residueTorsionVectors.Add(this.torsionVectors[i]);
                residueNormalVectors.Add(this.normalVectors[i]);
            }
        }

        /// <summary>
        /// Helper function used by <see cref="CreateControlPoints" /> to populate the data
        /// stuctures which refence certain atom types.
        /// </summary>
        private void PopulateAtomLists()
        {
            foreach (Residue residue in this.residues)
            {
                this.isHelixList.Add(residue.IsHelix);
                this.caList.Add(residue.CAlphaPosition);
                this.oList.Add(residue.CarbonylOxygenPosition);
            }

            this.isHelixList.Insert(0, this.isHelixList[0]);
            this.isHelixList.Insert(0, this.isHelixList[1]);

            this.isHelixList.Add(this.isHelixList[this.isHelixList.Count - 1]);
            this.isHelixList.Add(this.isHelixList[this.isHelixList.Count - 2]);

            this.caList.Insert(0, this.Reflect(this.caList[0], this.caList[1], 0.4f));
            this.caList.Insert(0, this.Reflect(this.caList[1], this.caList[2], 0.6f));

            this.caList.Add(this.Reflect(
                this.caList[this.caList.Count - 1], this.caList[this.caList.Count - 2], 0.4f));
            this.caList.Add(this.Reflect(
                this.caList[this.caList.Count - 2], this.caList[this.caList.Count - 3], 0.6f));

            this.oList.Insert(0, this.Reflect(this.oList[0], this.oList[1], 0.4f));
            this.oList.Insert(0, this.Reflect(this.oList[1], this.oList[2], 0.6f));

            this.oList.Add(this.Reflect(
                this.oList[this.oList.Count - 1], this.oList[this.oList.Count - 2], 0.4f));
            this.oList.Add(this.Reflect(
                this.oList[this.oList.Count - 2], this.oList[this.oList.Count - 3], 0.6f));
        }

        /// <summary>
        /// Helper function used by <see cref="CreateControlPoints" /> to populate the data
        /// stuctures which hold control point data.
        /// </summary>
        private void PopulateControlLists()
        {
            Vector3 previousD = new Vector3();

            for (int i = 0; i < this.caList.Count - 1; i++)
            {
                Vector3 ca1 = this.caList[i];
                Vector3 o1 = this.oList[i];
                Vector3 ca2 = this.caList[i + 1];

                Vector3 p = new Vector3((ca1.x + ca2.x) / 2, (ca1.y + ca2.y) / 2,
                    (ca1.z + ca2.z) / 2);

                Vector3 a = ca2 - ca1;
                Vector3 b = o1 - ca1;

                Vector3 c = Vector3.Cross(a, b);
                Vector3 d = Vector3.Cross(c, a);

                c.Normalize();
                d.Normalize();

                if (this.isHelixList[i] && this.isHelixList[i + 1])
                    p+=new Vector3(1.5f * c.x, 1.5f * c.y, 1.5f * c.z);

                if (i > 0 && Vector3.Angle(d, previousD) > 90)
                    d = -d;
                previousD = d;

                this.pList.Add(p);
                this.dList.Add(p + d);
            }
        }

        /// <summary>
        /// Helper function used by <see cref="CreateControlPoints" /> to populate the data
        /// stuctures which hold the spline data.
        /// </summary>
        private void PopulateSplineLists()
        {
            Vector3 previousRibbonPoint = new Vector3();
            Vector3 ribbonPoint;
            Vector3 torsionPoint;

            for (int i = 0; i < this.residues.Count; i++)
            {
                Vector3 p1 = pList[i];
                Vector3 p2 = pList[i + 1];
                Vector3 p3 = pList[i + 2];
                Vector3 p4 = pList[i + 3];

                Vector3 d1 = dList[i];
                Vector3 d2 = dList[i + 1];
                Vector3 d3 = dList[i + 2];
                Vector3 d4 = dList[i + 3];

                for (int j = 1; j <= Ribbon.linearSegmentCount; j++)
                {
                    float t = (float)j / Ribbon.linearSegmentCount;

                    if (t < 0.5f)
                    {
                        ribbonPoint = this.Spline(p1, p2, p3, t + 0.5f);
                        torsionPoint = this.Spline(d1, d2, d3, t + 0.5f);
                    }
                    else
                    {
                        ribbonPoint = this.Spline(p2, p3, p4, t - 0.5f);
                        torsionPoint = this.Spline(d2, d3, d4, t - 0.5f);
                    }

                    if (i == 0 && j == 1)
                    {
                        previousRibbonPoint = this.Spline(p1, p2, p3, 0.5f);

                        Vector3 previousTorsionPoint = this.Spline(d1, d2, d3, 0.5f);

                        Vector3 extrapolatedRibbonPoint =
                            this.Reflect(previousRibbonPoint, ribbonPoint, 1);

                        this.AddSplineNode(extrapolatedRibbonPoint, previousRibbonPoint,
                            previousTorsionPoint);
                    }

                    this.AddSplineNode(previousRibbonPoint, ribbonPoint, torsionPoint);

                    previousRibbonPoint = ribbonPoint;
                }
            }
        }

        /// <summary>
        /// Helper function used by <see cref="PopulateSplineLists" /> to populate the data
        /// stuctures for a particular point along the spline.
        /// </summary>
        private void AddSplineNode(
            Vector3 previousRibbonPoint, Vector3 ribbonPoint, Vector3 torsionPoint)
        {
            this.ribbonPoints.Add(ribbonPoint);

            Vector3 torsionVector = torsionPoint - ribbonPoint;
            torsionVector.Normalize();
            this.torsionVectors.Add(torsionVector);

            Vector3 ribbonVector = ribbonPoint - previousRibbonPoint;
            Vector3 normalVector = Vector3.Cross(torsionVector, ribbonVector);
            normalVector.Normalize();
            this.normalVectors.Add(normalVector);
        }

        /// <summary>
        /// Reflects one point across another by a specified amount.
        /// </summary>
        /// <param name="p1">Point 1.</param>
        /// <param name="p2">Point 2.</param>
        /// <param name="amount">The reflection scaling factor.</param>
        /// <returns></returns>
        private Vector3 Reflect(Vector3 p1, Vector3 p2, float amount)
        {
            float x = p1.x - amount * (p2.x - p1.x);
            float y = p1.y - amount * (p2.y - p1.y);
            float z = p1.z - amount * (p2.z - p1.z);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Calculates the actual spline position.
        /// </summary>
        /// <param name="p1">Point 1.</param>
        /// <param name="p2">Point 2.</param>
        /// <param name="p3">Point 3.</param>
        /// <param name="t">The parametric value along the spline section.</param>
        /// <returns></returns>
        private Vector3 Spline(Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float a = (float)Math.Pow(1.0f - t, 2.0f) / 2.0f;
            float c = (float)Math.Pow(t, 2.0f) / 2.0f;
            float b = 1 - a - c;

            float x = a * p1.x + b * p2.x + c * p3.x;
            float y = a * p1.y + b * p2.y + c * p3.y;
            float z = a * p1.z + b * p2.z + c * p3.z;

            return new Vector3(x, y, z);
        }
    }
}
