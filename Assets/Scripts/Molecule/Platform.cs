using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
//using ImageEffects;

namespace MoleculeLogic
{
    public class Platform : MonoBehaviour
    {

        //GlowOutline glowoutline;

        private void Start()
        {
            //glowoutline = Camera.main.GetComponent<GlowOutline>();
        }

        public void SetVisable(bool vis,int index)
        {
            if (index < 0 || index >= this.transform.childCount) 
                return;
            Transform tchild = this.transform.GetChild(index);
            tchild.gameObject.SetActive(vis);

            ////if (glowoutline)
            ////{
            ////    Renderer[] rs = tchild.GetComponentsInChildren<Renderer>();
            ////    if (rs != null)
            ////    {
            ////        foreach (Renderer r in rs)
            ////        {
            ////            if (vis)
            ////                glowoutline.selectRenders.Add(r);
            ////            else
            ////                glowoutline.selectRenders.Remove(r);
            ////        }
            ////    }
            ////}
        }

        public void SetAllVisable(bool vis)
        {
            for (int i = 0; i < this.transform.childCount; i++)
            {
                Transform tchild = this.transform.GetChild(i);
                tchild.gameObject.SetActive(vis);

                //if (glowoutline)
                //{
                //    Renderer[] rs = tchild.GetComponentsInChildren<Renderer>();
                //    if (rs != null)
                //    {
                //        foreach (Renderer r in rs)
                //        {
                //            if (vis)
                //                glowoutline.selectRenders.Add(r);
                //            else
                //                glowoutline.selectRenders.Remove(r);
                //        }
                //    }
                //}

            }
        }
    }
}
