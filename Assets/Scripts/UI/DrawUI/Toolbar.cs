using UnityEngine;
using System.Collections;

public class Toolbar : BasePanelEx<Toolbar>
{



	string[] ss=new string[]{"smiles","clear","delete",
		"style1","style2","style3","style4","style5","style6","style7","style8","style9","style10","style11","style12",
		"C","N","O","S","F","Cl","Br","I","P","X","",""};

    public string seloperation = "";
    public override void OnClick(GameObject sender)
    {
        seloperation = sender.name;
    }



    public static string fomular="";

    private void Update()
    {
        HandleDirectCommand(seloperation);
    }

    void HandleDirectCommand(string op)
    {
        if (op == "exit")
        {
            SceneController.LoadScene("LoginScene");
        }
        else if (op == "clear")
        {
            BasePrimite[] bps = GameObject.FindObjectsOfType<BasePrimite>();
            if (bps != null)
            {
                foreach (BasePrimite bp in bps)
                {
                    GameObject.Destroy(bp.gameObject);
                }
            }

            DrawPanel.Instance.clearPrimate();
            seloperation = "";
        }
        else if (op == "smiles")
        {
            fomular = "";
            BasePrimite[] bps = GameObject.FindObjectsOfType<BasePrimite>();
            if (bps != null)
            {
                foreach (BasePrimite bp in bps)
                {
                    Symbol[] ss = bp.GetComponentsInChildren<Symbol>();
                    if (ss != null)
                    {
                        foreach (Symbol s in ss)
                        {
                            fomular += s.GetCombine2();
                        }
                    }

                }
            }
            //select = -1;
        }

    }
}
