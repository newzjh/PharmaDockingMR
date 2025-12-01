using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;

public class DrawPanel : BasePanelEx<DrawPanel>
{

	// Use this for initialization

    private int layermask;
    private float maxdistance = 15.0f;
    GameObject goDrawGroup;

	void Start ()
	{
        layermask = 1 << LayerMask.NameToLayer("Default");
		m_id = 0;
		m_primites = new Dictionary<int, Transform> ();

        goDrawGroup = new GameObject("DrawGroup");
        goDrawGroup.transform.localEulerAngles = Vector3.zero;
        goDrawGroup.transform.localPosition = Vector3.zero;
        goDrawGroup.transform.localScale = Vector3.one;

	}

	public void clearPrimate()
	{
		m_primites.Clear ();
	}

	private Dictionary<int,Transform> m_primites;
	private int m_id = 0;
	// Update is called once per frame
	void Update ()
	{
        if (UnifiedInputManager.OnUI)
            return;
	

		string selop = Toolbar.Instance.seloperation;
		if (selop == "C" ||
			selop == "N" ||
			selop == "O" ||
			selop == "S" ||
			selop == "F" ||
			selop == "Cl" ||
			selop == "Br" ||
			selop == "I" ||
			selop == "P" ||
            selop == "H" ||
			selop == "X") 
        {
            if (UnifiedInputManager.GetMouseButtonDown(0))
            {
				OnSetText (selop);
			}
		} 
        else if (selop == "+/-") 
        {
            if (UnifiedInputManager.GetMouseButtonDown(0))
            {
				OnSwitchAppend ();
			}
		} 
        else if (selop == "delete") 
        {
            if (UnifiedInputManager.GetMouseButtonDown(0))
            {
				OnDelete ();
			}
		} 
        else if (selop.Length > 5 && selop.Substring (0, 5) == "style")
        {
            if (UnifiedInputManager.GetMouseButtonDown(0))
            {
				string strstyle = selop.Substring (5, selop.Length - 5);
				int style = 1;
				int.TryParse (strstyle, out style);
				OnBuild (style);
			}
		} 
        else 
        {
		}
	}

	void OnDelete ()
	{
        Ray ray = Camera.main.ScreenPointToRay(UnifiedInputManager.mousePosition);
		RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxdistance, layermask)) 
        {
			Collider c = hit.collider;
			if (c) {
				BasePrimite bp = c.GetComponentInParent<BasePrimite> ();
				if (bp) {
					GameObject.Destroy (bp.gameObject);
				}
			}
		}
	}

	void OnSetText (string text)
	{
        Ray ray = Camera.main.ScreenPointToRay(UnifiedInputManager.mousePosition);
		RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxdistance, layermask))
        {
			Collider c = hit.collider;
			if (c) {
				Symbol s = c.gameObject.GetComponentInChildren<Symbol> ();
				if (s != null) {
					s.SetMain (text);
				}
			}
		}
	}

	void OnSwitchAppend ()
	{
        Ray ray = Camera.main.ScreenPointToRay(UnifiedInputManager.mousePosition);
		RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxdistance, layermask))
        {
			Collider c = hit.collider;
			if (c) {
				Symbol s = c.gameObject.GetComponentInChildren<Symbol> ();
				if (s != null) {
					s.SwitchAppend ();
				}
			}
		}
	}


	void OnBuild (int style=0)
	{
        
		LinkParam link = null;
        Ray ray = Camera.main.ScreenPointToRay(UnifiedInputManager.mousePosition);
		Transform newGoTr = null;
		RaycastHit hit;
		bool removeLink = false;
        if (Physics.Raycast(ray, out hit, maxdistance, layermask))
        {
			link = hit.collider.GetComponentInParent<LinkParam> ();
			if (link != null && link.applyLink ()) {
				link.updateParam ();
				GameObject newPriGo = new GameObject ();
				newGoTr = newPriGo.transform;
			}
		} 
        else if (m_primites.Count<=0)
        {
			GameObject newPriGo = new GameObject ();
			newGoTr = newPriGo.transform;
			link = newPriGo.AddComponent<LinkParam> ();
            link.linkPosition = Camera.main.ScreenToWorldPoint(UnifiedInputManager.mousePosition);
			link.linkPosition.z = 0.0f;
			removeLink = true;
		}
		if (removeLink)
        {
			MonoBehaviour.Destroy (link);
		}
		if (newGoTr) 
        {
			newGoTr.gameObject.name = "P" + m_id;
            newGoTr.parent = goDrawGroup.transform;

			BasePrimite newPri = null;
			if (style == 6) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite3> ();
			} else if (style == 7) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite4> ();
			} else if (style == 8) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite5> ();
			} else if (style == 9) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite6ex> ();
			} else if (style == 10) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite6> ();
			} else if (style == 12) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite8> ();
			}
			else if (style == 1) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite2a> ();
			}
			else if (style == 2) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite2> ();
			}
			else if (style == 3) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite2d> ();
			}
			else if (style == 4) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite2t> ();
			}
			else if (style == 5) {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite2t> ();
			}
			else {
				newPri = (BasePrimite)newGoTr.gameObject.AddComponent<Primite7> ();
			}
			newPri.createPrimate (link, newGoTr);

            if (m_primites.Count <= 0)
            {
                Ray mouseray = Camera.main.ScreenPointToRay(UnifiedInputManager.mousePosition);
                newGoTr.position = Camera.main.transform.position + mouseray.direction * 4.0f;
            }

			if (newGoTr.childCount<=0)
			{
				GameObject.Destroy(newGoTr.gameObject);
			}
			else
			{
				m_primites [m_id] = newGoTr;
				
				TextMesh[] textmeshes = newGoTr.GetComponentsInChildren<TextMesh> ();
				if (textmeshes != null) {
					foreach (TextMesh mesh in textmeshes) {
						mesh.transform.up = new Vector3 (0, 1, 0);
					}
				}
				m_id++;

			}
		}

	}
}
