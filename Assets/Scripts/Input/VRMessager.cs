using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

namespace UnifiedInput
{

    public struct TransfromRecord
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    public class VRMessager 
    {


        public static GameObject GetSelectableUIObj(GameObject srcgo)
        {
            GameObject go = srcgo;
            while (true)
            {
                if (go == null) 
                    break;

                Selectable uiwight = go.GetComponent<Selectable>();
                ILayoutElement layoutelement = go.GetComponent<ILayoutElement>();
                if (uiwight != null)
                {
                    break;
                }
                else if (layoutelement != null)
                {
                    Transform tparent = go.transform.parent;
                    if (tparent != null)
                    {
                        go = tparent.gameObject;
                    }
                    else
                    {
                        go = null;
                    }
                }
                else
                {
                    go = null;
                    break;
                }
            }
            return go;
        }


        private static Dictionary<GameObject, TransfromRecord> records = new Dictionary<GameObject, TransfromRecord>();
        public static void OnEnterUIObj(GameObject go)
        {
            if (go == null)
                return;

            if (go.GetComponent<Scrollbar>())
                return;

            TransfromRecord record;
            if (!records.ContainsKey(go))
            {
                record.scale = go.transform.localScale;
                record.rotation = go.transform.localRotation;
                record.position = go.transform.localPosition;
                records[go] = record;
            }
            else
            {
                record = records[go];
            }
            Vector3 newscale = record.scale;
            go.transform.localScale = newscale;
        }

        public static void OnLeaveUIObj(GameObject go)
        {
            if (go == null)
                return;

            Selectable uiwight = go.GetComponent<Selectable>();
            if (records.ContainsKey(go))
            {
                TransfromRecord record = records[go];
                go.transform.localScale = record.scale;
            }
        }
    }

}