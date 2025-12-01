
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoleculeLogic
{
    public class LoadingTab : MonoBehaviour
    {
        Transform loadingIconTrans;
        Transform textTrans;
        public void Create(string text,Vector3 offset)
        {
            GameObject loadPrefab = Resources.Load<GameObject>("UI/ProgressCanvas");
            GameObject newObj = GameObject.Instantiate(loadPrefab);
            loadingIconTrans = newObj.transform;
            loadingIconTrans.parent = transform;
            loadingIconTrans.localPosition = offset;
            loadingIconTrans.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            loadingIconTrans.localEulerAngles = Vector3.zero;

            //GameObject textobj = new GameObject("text");
            //textTrans = textobj.transform;
            //textTrans.parent = this.transform;
            //textTrans.localPosition = offset;
            //textTrans.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            //textTrans.eulerAngles = Vector3.zero;
            //TextMesh tm = textobj.AddComponent<TextMesh>();
            //tm.fontSize = 24;
            //tm.anchor = TextAnchor.MiddleCenter;
            //tm.fontStyle = FontStyle.Bold;
            //tm.anchor = TextAnchor.UpperCenter;
            //tm.text = text;
            //tm.color = Color.white;
        }

        void Update()
        {
        }

        void OnDestroy()
        {
            if (loadingIconTrans)
            {
                GameObject.Destroy(loadingIconTrans.gameObject);
            }
           // GameObject.Destroy(textTrans.gameObject);
        }
    }

}
