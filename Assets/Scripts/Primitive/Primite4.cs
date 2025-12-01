using UnityEngine;
using System.Collections;

public class Primite4 : BasePrimite {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}


	private Transform m_parent;
	public override void createPrimate(LinkParam link,Transform parent)
	{
		if (link == null || !parent) {
			Debug.Log("input is null!");
			return;
		}
		m_parent = parent;

		if (link.linkType == 2) //none
		{
			buildPrimite (4, 0 , m_parent,Mathf.PI/4.0f,false);
			m_parent.position=link.linkPosition;
		}
		else if (link.linkType==0) //node
		{
			buildPrimite (4, 0 , m_parent,0,false);
			m_parent.position=link.linkPosition+link.linkNormal*(rad+1.0f*globalscale);
			m_parent.right=link.linkNormal;
			GameObject newEdge=GameObject.CreatePrimitive(PrimitiveType.Cube);
			newEdge.name="ELink";
			Transform tEdge=newEdge.transform;
			tEdge.parent=m_parent;
            tEdge.localScale = new Vector3(0.1f * globalscale, 0.1f * globalscale, 1.0f * globalscale);
            tEdge.position = link.linkPosition + link.linkNormal * 0.5f * globalscale;
			tEdge.LookAt(link.linkPosition);
		}
		else if (link.linkType==1) //edge
		{
			buildPrimite (4, 0 , m_parent,Mathf.PI/4.0f+Mathf.PI,true);
            m_parent.position = link.linkPosition + link.linkNormal * 0.5f * globalscale;
			m_parent.right=link.linkNormal;
		}
	}

}
