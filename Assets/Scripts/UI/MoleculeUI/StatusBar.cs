using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using MoleculeLogic;

namespace MoleculeUI
{

    public class StatusBar : MonoBehaviour
    {
        public void OnProgress(float progress, string msg)
        {
            this.progress = progress;
            this.msg = msg;
        }

        public void Awake()
        {
            Molecule.ProgressEvent += OnProgress;
        }

        public void OnDestroy()
        {
            Molecule.ProgressEvent -= OnProgress;
        }



        public float progress=0.0f;
        public string msg="progress message";

        public string command = "input command here";
        public bool showWideInput = false;

        public bool showWideOutput = false;
        public string result = "result";

        public void OnGUI()
        {
            float sw = Screen.width;
            float sh = Screen.height;
            float mh = sh * 0.04f;

            if (GUI.Button(new Rect(0, sh - mh, sw * 0.05f, mh), showWideInput ? "V" : "^"))
            {
                showWideInput = !showWideInput;
            }
            if (showWideInput)
            {
                command = GUI.TextField(new Rect(sw * 0.05f, sh - sh * 0.3f, sw * 0.35f, sh * 0.3f), command);
            }
            else
            {
                command = GUI.TextField(new Rect(sw * 0.05f, sh - mh, sw * 0.35f, mh), command);
            }
   

            GUI.Box(new Rect(sw * 0.4f, sh - mh, sw * 0.2f, mh), "",GUI.skin.button);
            GUI.Box(new Rect(sw * 0.4f, sh - mh, sw * 0.2f * progress / 100.0f, mh), "", GUI.skin.button);
            GUI.Label(new Rect(sw*0.4f,sh-mh,sw*0.2f,mh),progress.ToString()+"%");



            if (GUI.Button(new Rect(sw* 0.95f, sh - mh, sw * 0.05f, mh), showWideOutput ? "V" : "^"))
            {
                showWideOutput = !showWideOutput;
            }
            if (showWideOutput)
            {
                result = GUI.TextField(new Rect(sw * 0.6f, sh - sh * 0.3f, sw * 0.35f, sh * 0.3f), result);
            }
            else
            {
                result = GUI.TextField(new Rect(sw * 0.6f, sh - mh, sw * 0.35f, mh), result);
            }
     
        }
    }

}
