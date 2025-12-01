using UnityEngine;
using System.Collections;

public class Symbol : MonoBehaviour {

	public string main="C";
	public string append="";

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	public void SetMain(string s)
	{
		main = s;
		Refresh ();
	}

	public void SetAppend(string s)
	{
		append = s;
		Refresh ();
	}

	public void SwitchAppend()
	{
		if (append == "+") {
			append="-";
		}
		if (append == "-") {
			append = "";
		} else {
			append="+";
		}
		Refresh ();
	}

	void Refresh()
	{
		TextMesh tm = GetComponent<TextMesh> ();
		if (tm == null) {
			return;
		}

		Color scol = Color.gray;
		if (main=="C")
		{
			if (append.Length<=0)
			{
				tm.color=new Color(0,0,0,0);
			}
			else
			{
				tm.color=Color.black;
			}
			scol=Color.gray;
		}
		else if (main=="S")
		{
			tm.color=new Color(178.0f/255.0f,178.0f/255.0f,0/255.0f);
			scol=tm.color;
		}
		else if (main=="N")
		{
			tm.color=Color.blue;
			scol=tm.color;
		}
        else if (main == "H")
        {
            tm.color = Color.cyan;
            scol = tm.color;
        }
		else if (main=="O")
		{
			tm.color=Color.red;
			scol=tm.color;
		}
		else if (main=="F")
		{
			tm.color=new Color(255.0f/255.0f,0.0f/255.0f,255.0f/255.0f);
			scol=tm.color;
		}
		else if (main=="Cl")
		{
			tm.color=new Color(255.0f/255.0f,0.0f/255.0f,255.0f/255.0f);
			scol=tm.color;
		}
		else if (main=="Br")
		{
			tm.color=new Color(255.0f/255.0f,0.0f/255.0f,255.0f/255.0f);
			scol=tm.color;
		}
		else if (main=="I")
		{
			tm.color=new Color(255.0f/255.0f,0.0f/255.0f,255.0f/255.0f);
			scol=tm.color;
		}
		else if (main=="P")
		{
			tm.color=new Color(255.0f/255.0f,200.0f/255.0f,0.0f/255.0f);
			scol=tm.color;
		}				
		else
		{
			tm.color=Color.black;
			scol=Color.gray;
		}

		tm.text = GetCombine1 ();

		MeshRenderer mr = tm.transform.parent.GetComponent<MeshRenderer> ();
		mr.material.color = scol;
	}

	string GetMainString()
	{
		return main;
	}

	string GetConnectString()
	{
		if (append.Length > 0) {
			if (main == "C") {
				return "H";
			}
			else if (main == "N") {
				return "H";
			}
			else
			{
				return "";
			}
		} 
		else {
			return "";
		}
	}

	string GetAppendString()
	{
		return append;
	}

	public string GetCombine1()
	{
		if (append.Length > 0) {
			return GetMainString()+GetConnectString()+GetAppendString();
		} else {
			return main;
		}
	}

	public string GetCombine2()
	{
		if (append.Length > 0) {
			return "["+GetMainString()+GetConnectString()+GetAppendString()+"]";
		} else {
			return main;
		}
	}
}
