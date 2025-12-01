using UnityEngine;
using System.Collections;

public class Primite2a : BasePrimite {

    private static GameObject arrow = null;



	private Transform m_parent;
	public override void createPrimate(LinkParam link,Transform parent)
	{
		if (link == null || !parent) {
			Debug.Log("input is null!");
			return;
		}
		m_parent = parent;
		link.rotateLink ();
		if (link.linkType == 2) //none
		{
			buildPrimiteEx(0,m_parent,false);
			m_parent.position=link.linkPosition;
		}
		else if (link.linkType==0) //node
		{
			buildPrimiteEx(0,m_parent,true);
            m_parent.position = link.linkPosition + link.linkNormal * 0.5f * globalscale;
			m_parent.right=link.linkNormal;
		}
		else if (link.linkType==1) //edge
		{
			//do nothing
		}
	}
	
	protected void buildPrimiteEx(int id,Transform parent,bool shareVector)
	{
		rad = 1.0f;

        if (arrow == null)
            arrow = Resources.Load("Drawing/arrow") as GameObject;
		if (arrow == null)
			return;
			
		if (!shareVector) 
		{
            buildSphere("V0", new Vector3(0.4f * globalscale, 0.4f * globalscale, 0.4f * globalscale), new Vector3(-0.5f * globalscale, 0.0f, 0.0f), parent);
		}
        buildSphere("V1", new Vector3(0.4f * globalscale, 0.4f * globalscale, 0.4f * globalscale), new Vector3(0.5f * globalscale, 0.0f, 0.0f), parent);
		/*
		GameObject newVetexB = GameObject.CreatePrimitive (PrimitiveType.Sphere);
		newVetexB.name = "V1";
		Transform tVertexB = newVetexB.transform;
		tVertexB.parent = parent;
		tVertexB.localScale = new Vector3 (0.4f, 0.4f, 0.4f);
		tVertexB.position = new Vector3 (0.5f,0.0f,0.0f);
		LinkParam newLinkB = newVetexB.AddComponent<LinkParam> ();
		newLinkB.pointMe = tVertexB;
		newLinkB.pointCenter = parent;
		newLinkB.linkType = 0;
		
		GameObject textgoB = new GameObject ("text");
		textgoB.transform.parent = newVetexB.transform;
		TextMesh tmB = textgoB.AddComponent<TextMesh> ();
		tmB.fontSize = 32;
		tmB.anchor = TextAnchor.MiddleCenter;
		tmB.color = new Color (0, 0, 0, 0);
		tmB.fontStyle = FontStyle.Bold;
		tmB.text = "C";
		tmB.anchor = TextAnchor.UpperCenter;
		textgoB.transform.localPosition = Vector3.zero;
		textgoB.transform.localScale = new Vector3 (0.5f, 0.5f, 0.5f);
		textgoB.transform.eulerAngles = new Vector3 (0, 0, 0);
		*/

			

		GameObject newEdge = GameObject.Instantiate (arrow);
		newEdge.name = "E0";
		Transform tEdge = newEdge.transform;
        float length = 0.8f * globalscale;
        tEdge.localScale = new Vector3(length , length * globalscale, length * globalscale);
			
		tEdge.parent = parent;
		tEdge.position = Vector3.zero;
		LinkParam newLinkE = tEdge.gameObject.AddComponent<LinkParam> ();
		newLinkE.pointMe = tEdge;
		newLinkE.pointCenter = parent;
		newLinkE.linkType = 1;
	}
}
