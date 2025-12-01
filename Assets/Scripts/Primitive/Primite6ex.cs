using UnityEngine;
using System.Collections;

public class Primite6ex : BasePrimite {

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
			buildPrimiteEx (6, 0 , m_parent,Mathf.PI/2.0f,false);
			m_parent.position=link.linkPosition;
		}
		else if (link.linkType==0) //node
		{
			buildPrimiteEx (6, 0 , m_parent,0,false);
            m_parent.position = link.linkPosition + link.linkNormal * 2.0f * globalscale;
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
			float offset=Mathf.PI;
			if (link.nValue==2)
			{
				offset+=Mathf.PI/6.0f;
			}
			buildPrimiteEx (6, 0 , m_parent,offset,true);
            m_parent.position = link.linkPosition + link.linkNormal * Mathf.Sqrt(3.0f) * 0.5f * globalscale;
			m_parent.right=link.linkNormal;
		}
	}


	protected void buildPrimiteEx(int vertexCount,int id,Transform parent,float startAngle,bool shareEdge)
	{
		if (vertexCount <= 1) {
			Debug.Log ("vertexCount <= 1");
			return;
		}
		Vector3[] vets = new Vector3[vertexCount];
		for (int i=0; i<vertexCount; i++) {
			Vector3 newPoint = Vector3.zero;
			float angle = Mathf.PI * 2.0f / (float)vertexCount * i;
			newPoint.x = Mathf.Cos (angle + startAngle);
			newPoint.y = Mathf.Sin (angle + startAngle);
			vets [i] = newPoint;
		}
		
		rad = 1.0f / ((vets [1] - vets [0]).magnitude) * globalscale;
		for (int i=0; i<vertexCount; i++) {
			vets [i] = vets [i] * rad;
		}
		
		for (int i=0; i<vertexCount; i++) {
			if (shareEdge && i == vertexCount - 1) {
				continue;
			}
			
			if (!shareEdge || i > 0) {
				GameObject newVetex = GameObject.CreatePrimitive (PrimitiveType.Sphere);
				newVetex.name = "V" + i;
				Transform tVertex = newVetex.transform;
				tVertex.parent = parent;
                tVertex.localScale = new Vector3(0.4f * globalscale, 0.4f * globalscale, 0.4f * globalscale);
				tVertex.position = vets [i];
				LinkParam newLink = newVetex.AddComponent<LinkParam> ();
				newLink.pointMe = tVertex;
				newLink.pointCenter = parent;
				newLink.linkType = 0;
				
				GameObject textgo = new GameObject ("text");
				textgo.transform.parent = newVetex.transform;
				TextMesh tm = textgo.AddComponent<TextMesh> ();
				tm.fontSize = 32;
				tm.anchor = TextAnchor.MiddleCenter;
				tm.color = new Color (0, 0, 0, 0);
				tm.fontStyle = FontStyle.Bold;
				tm.text = "C";
				tm.anchor = TextAnchor.UpperCenter;
				textgo.transform.localPosition = Vector3.zero;
				textgo.transform.localScale = new Vector3 (0.5f, 0.5f, 0.5f);
				textgo.transform.eulerAngles = new Vector3 (0, 0, 0);
			}
			
			int nexti = i + 1;
			if (nexti >= vertexCount)
				nexti = 0;
			Transform tEdge = null;
			if (i % 2 == 0) {
				GameObject newEdge = GameObject.CreatePrimitive (PrimitiveType.Cube);
				newEdge.name = "E" + i;
				tEdge = newEdge.transform;
                float length = (vets[nexti] - vets[i]).magnitude - 0.2f * globalscale;
                tEdge.localScale = new Vector3(length, 0.1f * globalscale, 0.1f * globalscale);

			} else {
                float length = (vets[nexti] - vets[i]).magnitude - 0.2f * globalscale;
				tEdge = buildSubEdge (i, 2, length);
			}

			tEdge.parent = parent;
			tEdge.position = (vets [nexti] + vets [i]) / 2.0f;
			tEdge.right=(vets [nexti] - vets [i]).normalized;
			LinkParam newLinkE = tEdge.gameObject.AddComponent<LinkParam> ();
			newLinkE.pointMe = tEdge;
			newLinkE.pointCenter = parent;
			newLinkE.linkType = 1;
			newLinkE.nValue = 2;
		}
	}

}
