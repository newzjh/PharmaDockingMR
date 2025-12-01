/// @file PDBtoDEN.cs
/// @brief Details to be specified
/// @author FvNano/LBT team
/// @author Marc Baaden <baaden@smplinux.de>
/// @date   2013-4
///
/// Copyright Centre National de la Recherche Scientifique (CNRS)
///
/// contributors :
/// FvNano/LBT team, 2010-13
/// Marc Baaden, 2010-13
///
/// baaden@smplinux.de
/// http://www.baaden.ibpc.fr
///
/// This software is a computer program based on the Unity3D game engine.
/// It is part of UnityMol, a general framework whose purpose is to provide
/// a prototype for developing molecular graphics and scientific
/// visualisation applications. More details about UnityMol are provided at
/// the following URL: "http://unitymol.sourceforge.net". Parts of this
/// source code are heavily inspired from the advice provided on the Unity3D
/// forums and the Internet.
///
/// This software is governed by the CeCILL-C license under French law and
/// abiding by the rules of distribution of free software. You can use,
/// modify and/or redistribute the software under the terms of the CeCILL-C
/// license as circulated by CEA, CNRS and INRIA at the following URL:
/// "http://www.cecill.info".
/// 
/// As a counterpart to the access to the source code and rights to copy, 
/// modify and redistribute granted by the license, users are provided only 
/// with a limited warranty and the software's author, the holder of the 
/// economic rights, and the successive licensors have only limited 
/// liability.
///
/// In this respect, the user's attention is drawn to the risks associated 
/// with loading, using, modifying and/or developing or reproducing the 
/// software by the user in light of its specific status of free software, 
/// that may mean that it is complicated to manipulate, and that also 
/// therefore means that it is reserved for developers and experienced 
/// professionals having in-depth computer knowledge. Users are therefore 
/// encouraged to load and test the software's suitability as regards their 
/// requirements in conditions enabling the security of their systems and/or 
/// data to be ensured and, more generally, to use and operate it in the 
/// same conditions as regards security.
///
/// The fact that you are presently reading this means that you have had 
/// knowledge of the CeCILL-C license and that you accept its terms.
///
/// $Id: PDBtoDEN.cs 613 2014-07-22 13:31:09Z tubiana $
///
/// References : 
/// If you use this code, please cite the following reference : 	
/// Z. Lv, A. Tek, F. Da Silva, C. Empereur-mot, M. Chavent and M. Baaden:
/// "Game on, Science - how video game technology may help biologists tackle
/// visualization challenges" (2013), PLoS ONE 8(3):e57990.
/// doi:10.1371/journal.pone.0057990
///
/// If you use the HyperBalls visualization metaphor, please also cite the
/// following reference : M. Chavent, A. Vanel, A. Tek, B. Levy, S. Robert,
/// B. Raffin and M. Baaden: "GPU-accelerated atom and dynamic bond visualization
/// using HyperBalls, a unified algorithm for balls, sticks and hyperboloids",
/// J. Comput. Chem., 2011, 32, 2924
///

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using MoleculeLogic;



public class PDBtoDEN : MonoBehaviour {


    public static List<string> sugarResname = new List<string> {"ABE","ACE","ALT","API","ARA","DHA","FRU","FUC","GAL",
			"GLC","GUL","IDO","DKN","KDO","MAN","NEG","RHA","RIB","SIA","TAG","TAL","XYL",
			"GLA","FUL","GLB","NAG","NDG","BMA","MMA","A2G","AAL","BGC"};

	private float[,,] gridS;
	
	public float[,,] GridS {
		get {return gridS;}
	}
	
	byte[,,] VertColor;
	//public Color[] colors;

	public static int X;
	public static int Y;
	public static int Z;
	
	// I have no idea where this 18f vector comes from, but it's used everywhere in this class and by others,
	// so I might as well make it a public static member. --- Alexandre
	public static Vector3 fudgeFactor = new Vector3(18f,18f,18f);

	public static Vector3 delta;
	public static Vector3 origin;// = new Vector3(MoleculeModel.MinValue.x,
								//			   MoleculeModel.MinValue.y,
		 						//			   MoleculeModel.MinValue.z);
	private const float DEFAULT_RESOLUTION = 1.275f;//2.75f;




	/// <summary>
	/// First we lower the resolution for bigger molecules.
	/// Perhaps this could be exposed to the interface, but it might cause
	/// users to choose unreasonable values.
	/// Bear in mind that the "threshold" value in VertexTree effectively caps
	/// the resolution of the final mesh, but not of the original voxels, or of
	/// the MarchingCubes algorithm itself.
	/// So they serve slightly different purposes.
	/// </summary>
	/// <returns>
	/// The resolution.
	/// </returns>
	private float CapResolution(float resolution) {

        //if (molecule)
        //{
        //    return resolution;
        //}

        int nbAtoms = molecule.atoms.Count;
		//float resolution = DEFAULT_RESOLUTION;
		
		if(nbAtoms > 500)
			resolution = 2.5f;
		if(nbAtoms > 1000)
			resolution = 2.2f;
		if(nbAtoms > 2000)
			resolution = 2.0f;
		if(nbAtoms > 4000)
			resolution = 1.8f;
		if(nbAtoms > 5000)
			resolution = 1.7f;
		if(nbAtoms > 6000)
			resolution = 1.6f;
		if(nbAtoms > 8000)
			resolution = 1.5f;
		if(nbAtoms > 10000)
			resolution = 1.4f;
		if(nbAtoms > 14000)
			resolution = 1.2f;
		if(nbAtoms > 20000)
			resolution = 1.0f;
		
		return resolution;
	}

    MoleculeLogic.Molecule molecule = null;

	public void TranPDBtoDEN(MoleculeLogic.Molecule molecule, float resolution = DEFAULT_RESOLUTION, bool cap = true)
    {
        this.molecule=molecule;

        if (molecule == null || molecule.atoms.Count <= 0) return;

		if(cap)
			resolution = CapResolution(resolution);
		
		delta = new Vector3(resolution, resolution, resolution);

		// We need to refresh the molecule's origin when it's not
		// the first molecule for which we generate a surface.

        origin = molecule.minAtomPosition;
		Debug.Log("Entering :: Generation of density from PDB");
        X = (int)(((molecule.maxAtomPosition.x - molecule.minAtomPosition.x) * resolution) + 40);
        Y = (int)(((molecule.maxAtomPosition.y - molecule.minAtomPosition.y) * resolution) + 40);
        Z = (int)(((molecule.maxAtomPosition.z - molecule.minAtomPosition.z) * resolution) + 40);
		
		Debug.Log("Density point X,Y,Z :: "+ X+","+Y+","+Z);
        Debug.Log("Density minValue :: " + origin);
		gridS = new float[X,Y,Z];
		VertColor = new byte[X,Y,Z];


		int i;
		int j;
		int k;
		float Dist;
		float bfactor;
		int atomnumber=0;
		//Color atomColor;
		string type;
		float density;
		
        //float maxValue =(float)MoleculeModel.BFactorList[0];
        //foreach (float f in MoleculeModel.BFactorList)
        //    if (f>maxValue)
        //        maxValue =f;
		
		for (i=0;i<X;i++)
			for(j=0;j<Y;j++)
				for(k=0;k<Z;k++)
                    VertColor[i, j, k] = (byte)AtomType.Other;


		int index = -1;
		foreach (MoleculeLogic.Atom atom in molecule.atoms) {
			//index++;

			bool useAtomForCalc = true;

			if (true) //(!MoleculeModel.useHetatmForSurface)
            {
                if ((atom.hetType) && (!sugarResname.Contains(atom.residueName)))
				//if ((MoleculeModel.atomHetTypeList[index]=="HETATM") && (!MoleculeModel.sugarResname.Contains(MoleculeModel.atomsResnamelist[index])))
                {
					useAtomForCalc = false;
				}
			}

			if (true)//(!MoleculeModel.useSugarForSurface)
            {
                if (sugarResname.Contains(atom.residueName))
				//if (MoleculeModel.sugarResname.Contains(MoleculeModel.atomsResnamelist[index]))
					useAtomForCalc = false;
			}

			if (useAtomForCalc){
                i = Mathf.RoundToInt((atom.position.x - molecule.minAtomPosition.x) * delta.x + fudgeFactor.x);
                j = Mathf.RoundToInt((atom.position.y - molecule.minAtomPosition.y) * delta.y + fudgeFactor.y);
                k = Mathf.RoundToInt((atom.position.z - molecule.minAtomPosition.z) * delta.z + fudgeFactor.z);
			
			/*
			float scaleFactor = 10f;
			i = Mathf.RoundToInt(coord[0] * scaleFactor);
			j = Mathf.RoundToInt(coord[1] * scaleFactor);
			k = Mathf.RoundToInt(coord[2] * scaleFactor);
			*/
			
//			Debug.Log("i,j,k : " + i +","+j+","+k);
//			gridS[i,j,k]=2;
//		}
               
//			type = "C";
//			Debug.Log("i j k : "+i+","+j+","+k);
			// Vector3 v1 = new Vector3((coord[0]-MoleculeModel.MinValue.x-MoleculeModel.Offset.x)*2+18,
			// 						 (coord[1]-MoleculeModel.MinValue.y-MoleculeModel.Offset.y)*2+18,
			// 						 (coord[2]-MoleculeModel.MinValue.z-MoleculeModel.Offset.z)*2+18);


            Vector3 v1 = new Vector3((atom.position.x - molecule.minAtomPosition.x) * delta.x + fudgeFactor.x,
                                        (atom.position.y - molecule.minAtomPosition.y) * delta.y + fudgeFactor.y,
                                        (atom.position.z - molecule.minAtomPosition.z) * delta.z + fudgeFactor.z);
			
			
			/*
			Vector3 v1 = new Vector3( 	coord[0] * scaleFactor,
										coord[1] * scaleFactor,
										coord[2] * scaleFactor);
			*/
										
			float AtomRadius = 1f;
			
			// Possibilité de créer une liste a la lecture du pdb et de la reprendre ici.
			// Comme cela on peut lire d'autre propriétés biologiques

            type = atom.atomName.Substring(0,1); //(MoleculeModel.atomsTypelist[atomnumber]).type;
            AtomType atomType = AtomType.Other;

			switch(type) {
				case "C": 
					AtomRadius =3.4f;
                    atomType = AtomType.C;
					break;
				case "N": 
					AtomRadius =3.1f;
                    atomType = AtomType.N;
					break;	
				case "O": 
					AtomRadius =3.04f;
                    atomType = AtomType.O;
					break;
				case "S": 
					AtomRadius =4.54f;
                    atomType = AtomType.S;
					break;
				case "P": 
					AtomRadius =3.6f;
                    atomType = AtomType.P;
					break;
				case "H": 
					AtomRadius =2.4f;
                    atomType = AtomType.H;
					break;
				default: 
					AtomRadius =2f;
                    atomType = AtomType.Other;
					break;
			}
            
			if (true)//(UIData.toggleSurf && !UIData.toggleBfac)
            {
			    for (int l = i-8 ;l < i+9 ; l++)
					for ( int m = j-8 ; m < j+9 ; m++)
						for ( int n = k-8 ; n < k+9 ; n++){
							Vector3 v2 = new Vector3(l,m,n);
							Dist = Vector3.Distance(v1,v2);
							density = (float)Math.Exp(-((Dist/AtomRadius)*(Dist/AtomRadius)));
							if (density > gridS[l,m,n])
									VertColor[l,m,n] = (byte)atomType;
							gridS[l,m,n] += density;
						}
			
          } //else if (false) //(UIData.toggleBfac) ...

			atomnumber++;
			}
		}

//				}
//			}
//		}
		
		// export the density in a .dx file readable by pymol or vmd
//		StreamWriter test;
//		test = new StreamWriter("grille.dx");
//		test.Write("# Data from APBS 1.3\n#\n# POTENTIAL (kT/e)\n#\nobject 1 class gridpositions counts "+X+" "+Y+" "+Z+"\n
//					origin -2.330000e+01 -2.34000e+01 -2.550000e+01\ndelta 5.000000e-01 0.000000e+00 0.000000e+00\n
//					delta 0.000000e+00 5.000000e-01 0.000000e+00\ndelta 0.000000e+00 0.000000e+00 5.000000e-01\n
//					object 2 class gridconnections counts "+X+" "+Y+" "+Z+"\nobject 3 class array type double rank 0 items "+X*Y*Z+" data follows\n");
//		for (i=0 ; i< X ; i++){
//			for (j=0 ; j<Y ; j++){
//				for (k=0 ; k<Z ; k++){
//					test.WriteLine(gridS[i,j,k]);
//					}
//				}
//			}
//			test.Write("attribute \"dep\" string \"positions\"\nobject \"regular positions regular connections\" class field\n
//						component \"positions\" value 1\ncomponent \"connections\" value 2\ncomponent \"data\" value 3");
//			test.Close();
	}

	public void ProSurface(float seuil){
		// to create the structure from the pdb
		
        //Vector4[] points;
        //points = new Vector4[ (X) * (Y) * (Z)];
        //colors = new Color[(X) * (Y) * (Z)];
        //// convert grid
        //for (int j = 0; j < Y; j++) {

        //    for (int i = 0; i < Z; i++) {
        //        for (int k = 0; k < X; k++) {
        //                points[j*(Z)*(X) + i*(X) + k] = new Vector4 (k, j, i , gridS[k,j,i]);
        //                colors[j*(Z)*(X) + i*(X) + k] = VertColor[k,j,i];
        //        }
        //    }
        //}
		
		/*
		Debug.Log("Entering :: Marching Cubes");
		MarchingCubesRec MCInstance;
		MCInstance = new MarchingCubesRec();
		DestroySurface();
		MCInstance.MCRecMain(X, Y, Z, seuil, points, 0f,false, delta, origin, colors);
		*/
        //GenerateMesh.CENTER = -(molecule.minAtomPosition + molecule.maxAtomPosition) / 2.0f;

        GenerateMesh.CreateSurfaceObjects(gridS, VertColor, seuil, delta, origin, this.transform);
		
		//points = null;
		//colors = null;
//		long bytebefore = GC.GetTotalMemory(false);
//		long byteafter = GC.GetTotalMemory(true);
		GC.GetTotalMemory(true);
		GC.Collect();
//		long byteafter2 = GC.GetTotalMemory(false);
//		Debug.Log ("before: "+(bytebefore/1000000)+"+ " afterCollet: " +(byteafter2/1000000));
	}
	
    //public void initColors(int X,int Y,int Z, Color col) {
    //    colors = new Color[(X) * (Y) * (Z)];

    //    for (int j = 0; j < Y; j++) {
    //        for (int i = 0; i < Z; i++) {
    //            for (int k = 0; k < X; k++) {
    //                colors[j*(Z)*(X) + i*(X) + k] = col;
    //            }
    //        }
    //    }
    //}
	
	/// <summary>
	/// Destroys previously displayed isosurface at GUI change
	/// </summary>
	public static void DestroySurface() {
		GameObject[] surfaceOBJ = GameObject.FindGameObjectsWithTag("SurfaceManager");
		for (int l = 0; l < surfaceOBJ.Length; l++) {
			Destroy(surfaceOBJ[l]);
		}
	}
	
}

