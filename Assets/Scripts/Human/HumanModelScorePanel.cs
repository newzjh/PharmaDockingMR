using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;


public class HumanModelScorePanel : BasePanelEx<HumanModelScorePanel>
{
    public Text scoreText;
    public Text resultText;

    public int score = 0;
    public int result = 0;

    public Transform rootTransoform;
    void Update()
    {
        if (!rootTransoform) return;

        this.transform.position = rootTransoform.position;

        scoreText.text = score.ToString();

        switch (result)
        {
            case 1:
                resultText.text="Match!";
                break;
            case -1:
                resultText.text="Try again";
                break;
            default:
                resultText.text="";
                break;
        }

    }


    public override void OnClick(GameObject sender)
    {
        if (sender.name == "BackButton")
        {
            SceneController.LoadScene("LoginScene");
        }
        else if (sender.name == "ResetButton")
        {
            SceneController.LoadScene("human_abdomen");
        }
    }

    // Use this for initialization
    //public virtual void Awake()
    //{
    //    //for (int i = 0; i < this.transform.childCount; i++)
    //    //{
    //    //    Transform t = this.transform.GetChild(i);
    //    //    Button b = t.GetComponent<Button>();
    //    //    if (b)
    //    //    {
    //    //        b.onClick.AddListener(delegate() { this.OnClick(t.gameObject); });
    //    //    }
    //    //}

    //    Button[] buttons = this.GetComponentsInChildren<Button>();
    //    for (int i = 0; i < buttons.Length; i++)
    //    {
    //        Button b = buttons[i];
    //        if (b)
    //        {
    //            b.onClick.AddListener(delegate() { this.OnClick(b.gameObject); });
    //        }
    //    }
    //}

}
