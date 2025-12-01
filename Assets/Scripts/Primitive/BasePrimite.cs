using UnityEngine;
using System.Collections;

public class LinkParam: MonoBehaviour
{
	public int linkType; //0-vertex;1-edge;2-none
	public Vector3 linkPosition;
	public Vector3 linkNormal;
	public int maxLink;
	public int nLinkCount;
	public int nValue;

	public Transform pointCenter;
	public Transform pointMe;

	public void updateParam()
	{
		if (linkType == 2)
			return;
		linkPosition=pointMe.position;
		linkNormal = (pointMe.position - pointCenter.position).normalized;
	}

	public void rotateLink()
	{
		Vector3 view = Camera.main.transform.forward;
		view.y = 0.0f;
		view.Normalize ();
		Vector3 up = Vector3.Cross (view, linkNormal);
		Vector3 normal = Vector3.Cross (up, view);
		//Debug.Log (linkNormal.ToString()+"->"+normal);
		linkNormal = normal;
	}

	public bool applyLink()
	{
		if (nLinkCount<maxLink)
		{
			nLinkCount++;
			return true;
		}
		else
		{
			return false;
		}
	}

	void Awake()
	{
		linkType = 2;
		linkPosition = Vector3.zero;
		linkNormal = new Vector3 (0.0f, 0.0f, 1.0f);
		nLinkCount = 0;
		maxLink = 1;
		nValue = 1;
	}
}

public class BasePrimite : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}
	
	public virtual void createPrimate(LinkParam link,Transform parent)
	{
	}

	public float rad;
    public float globalscale = 0.25f;

	protected void buildSphere(string name, Vector3 scale, Vector3 position, Transform parent)
	{
		GameObject newVetex = GameObject.CreatePrimitive (PrimitiveType.Sphere);
		Collider c = newVetex.GetComponent<Collider> ();
		if (c)
			Destroy (c);
		newVetex.AddComponent<BoxCollider> ();
		newVetex.name = name;
		Transform tVertex = newVetex.transform;
		tVertex.parent = parent;
		tVertex.localScale = scale;
		tVertex.position = position;
		LinkParam newLink = newVetex.AddComponent<LinkParam> ();
		newLink.pointMe = tVertex;
		newLink.pointCenter = parent;
		newLink.linkType = 0;

		GameObject textgo = new GameObject ("text");
		textgo.transform.parent = newVetex.transform;
		TextMesh tm = textgo.AddComponent<TextMesh> ();
		tm.fontSize = 32;
		tm.anchor = TextAnchor.MiddleCenter;
		tm.fontStyle = FontStyle.Bold;
		tm.anchor = TextAnchor.UpperCenter;
		textgo.transform.localPosition = Vector3.zero;
		textgo.transform.localScale = new Vector3 (0.5f, 0.5f, 0.5f);
		textgo.transform.eulerAngles = new Vector3 (0, 0, 0);
		
		Symbol s=textgo.AddComponent<Symbol>();
		s.SetMain("C");
	}

	protected Transform buildSubEdge(int i,int subEdgeNum,float length)
	{
		GameObject newEdge=GameObject.CreatePrimitive(PrimitiveType.Cube);
		newEdge.name="D"+i;
		Transform tEdge=newEdge.transform;
        tEdge.localScale = new Vector3(length , 0.3f * globalscale, 0.3f * globalscale);
		MeshRenderer mesh = tEdge.GetComponent<MeshRenderer> ();
		if (mesh!=null)
		{
			MonoBehaviour.Destroy(mesh);
		}

		float width = 1.0f / (float)subEdgeNum - 0.1f;
		float totalLen = 0.9f;
		float step = width + 0.1f;

		for (int edgei=0;edgei<subEdgeNum;edgei++)
		{
			GameObject newEdge1=GameObject.CreatePrimitive(PrimitiveType.Cube);
			Transform tEdge1=newEdge1.transform;
			tEdge1.parent = tEdge;
            tEdge1.localScale = new Vector3(1.0f , width , width );
			tEdge1.localPosition = new Vector3 (0.0f, 0.0f-totalLen/2.0f+step*edgei, 0.0f);
			Collider coll1 = tEdge.GetComponent<Collider> ();
			if (coll1!=null)
			{
				MonoBehaviour.Destroy(coll1);
			}
		}

		return tEdge;
	}

	protected void buildPrimite(int vertexCount,int id,Transform parent,float startAngle,bool shareEdge)
	{
		if (vertexCount <= 1) 
		{
			Debug.Log("vertexCount <= 1");
			return;
		}
		Vector3[] vets=new Vector3[vertexCount];
		for (int i=0;i<vertexCount;i++)
		{
			Vector3 newPoint=Vector3.zero;
			float angle=Mathf.PI*2.0f/(float)vertexCount*i;
			newPoint.x=Mathf.Cos(angle+startAngle);
			newPoint.y=Mathf.Sin(angle+startAngle);
			vets[i]=newPoint;
		}

        rad = 1.0f / ((vets[1] - vets[0]).magnitude) * globalscale;
		for (int i=0;i<vertexCount;i++)
		{
			vets[i]=vets[i]*rad;
		}

		for (int i=0; i<vertexCount; i++) {
			if (shareEdge && i==vertexCount-1)
			{continue;}

			if (!shareEdge || i>0)
			{
                buildSphere("V" + i, new Vector3(0.4f * globalscale, 0.4f * globalscale, 0.4f * globalscale), vets[i], parent);
				/*
				GameObject newVetex=GameObject.CreatePrimitive(PrimitiveType.Sphere);
				newVetex.name="V"+i;
				Transform tVertex=newVetex.transform;
				tVertex.parent=parent;
				tVertex.localScale=new Vector3(0.4f,0.4f,0.4f);
				tVertex.position=vets[i];
				
				LinkParam newLink=newVetex.AddComponent<LinkParam>();
				newLink.pointMe=tVertex;
				newLink.pointCenter=parent;
				newLink.linkType=0;

				GameObject textgo=new GameObject("text");
				textgo.transform.parent=newVetex.transform;
				TextMesh tm=textgo.AddComponent<TextMesh>();
				tm.fontSize=32;
				tm.anchor=TextAnchor.MiddleCenter;
				tm.fontStyle=FontStyle.Bold;
				tm.anchor=TextAnchor.UpperCenter;
				textgo.transform.localPosition=Vector3.zero;
				textgo.transform.localScale=new Vector3(0.5f,0.5f,0.5f);
				textgo.transform.eulerAngles=new Vector3(0,0,0);

				Symbol s=textgo.AddComponent<Symbol>();
				s.SetMain("C");
				*/
			}
			
			int nexti=i+1;
			if (nexti>=vertexCount) nexti=0;
			GameObject newEdge=GameObject.CreatePrimitive(PrimitiveType.Cube);
			newEdge.name="E"+i;
			Transform tEdge=newEdge.transform;
			tEdge.parent=parent;
            float length = (vets[nexti] - vets[i]).magnitude - 0.2f * globalscale;
            tEdge.localScale = new Vector3(length, 0.1f * globalscale, 0.1f * globalscale);
			tEdge.position=(vets[nexti]+vets[i])/2.0f;
			tEdge.right=(vets [nexti] - vets [i]).normalized;
			LinkParam newLinkE=newEdge.AddComponent<LinkParam>();
			newLinkE.pointMe=tEdge;
			newLinkE.pointCenter=parent;
			newLinkE.linkType=1;
		}
	}

}
