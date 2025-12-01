using UnityEngine;
using System;
using System.Collections;
using System.IO;
using MixedReality.Toolkit.SpatialManipulation;
using UnityEngine.XR.Interaction.Toolkit;
using MoleculeUI;
using MixedReality.Toolkit.UX;
using Cysharp.Threading.Tasks;

namespace MoleculeLogic
{


    public class MoleculeController : MonoBehaviour
    {


        public float blendfactor = 0.15f;
        public float scalefactor = 1.025f;
        public float rotationfactor = 15.0f;
        public float translatefactor = 0.3f;
        public Vector3 dynamicpos = Vector3.zero;
        public float dynamicscale = 1.0f;
        public Vector2 dynamicangle = Vector2.zero;
        private Vector2 currentangle = Vector2.zero;
        private float levelheight = 1.5f;
        public bool auto=true;
        public enum OperationModeType
        {
            None=0,
            Translate=1,
            Rotation=2,
            Scale=3,
        }
        private OperationModeType operationmode = OperationModeType.Translate;
        public OperationModeType OperationMode
        {
            get 
            {
                return operationmode; 
            }
            set
            {
                if (operationmode != value)
                {
                    Platform platform = this.GetComponentInChildren<Platform>();
                    if (platform != null)
                    {
                        platform.SetVisable(false, (int)operationmode - 1);
                        platform.SetVisable(true, (int)value - 1);
                    }
                    operationmode = value;
                }
            }
        }
        
        private GameObject platformobj;
        private GameObject molobj;
        public Molecule mol;

        float[] defaultangles = new float[] { -270f, -230.0f, -310.0f };

        public string url;
        public string cachefile;
        public void CacheFile(string name, byte[] data)
        {
            string tempfolder = Application.persistentDataPath + "/cachepdb";
            if (Directory.Exists(tempfolder) == false)
            {
                Directory.CreateDirectory(tempfolder);
            }
            cachefile = tempfolder + "/" + name + ".pdb";
            try
            {
                File.WriteAllBytes(cachefile, data);
            }
            catch (Exception e)
            { 
            }
        }

        private static GameObject PlatformPrefab = null;
        private Transform titleTransform = null;
        public async UniTask CreateFromStream(Stream s,string ext)
        {
            molobj = new GameObject("molecule");
            molobj.transform.parent = this.transform;
            molobj.transform.localScale = new Vector3(0.9f,0.9f,0.9f);
            molobj.transform.localEulerAngles = Vector3.zero;
            molobj.transform.localPosition = Vector3.zero;
            mol = molobj.AddComponent<Molecule>();
            mol.molname = this.name;
            mol.Build(molobj.transform);

            await mol.LoadFromStream(s, ext);

            //int index = this.transform.parent.childCount - 1;
            //float angle = -(index % 3 - 1) * 22.5f;
            //float radius = defaultangles[index % 3] / 180.0f * Mathf.PI;
            //float distance = (index / 3 + 1) * 1.0f;
            //this.transform.localPosition = new Vector3(distance * Mathf.Cos(radius), 0.3f, distance * Mathf.Sin(radius) + 1);

            if (PlatformPrefab == null)
            {
                PlatformPrefab = Resources.Load<GameObject>("Platform/Platform");
            }
            if (PlatformPrefab != null)
            {
                platformobj = GameObject.Instantiate(PlatformPrefab);
                platformobj.name = "platform";
                platformobj.layer = LayerMask.NameToLayer("Selection");
                platformobj.transform.parent = this.transform;
                platformobj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                //platformobj.transform.localEulerAngles = new Vector3(0.0f, angle, 0.0f);
                platformobj.transform.localPosition = Vector3.zero;
                Platform platform = platformobj.AddComponent<Platform>();
                platform.SetAllVisable(false);
            }

            {
                GameObject textObj = new GameObject();
                titleTransform = textObj.transform;
                textObj.name = "MolName";
                textObj.transform.parent = transform;
                TextMesh tm = textObj.AddComponent<TextMesh>();
                tm.fontSize = 20;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.LowerCenter;
                tm.text = this.name;
                tm.color = Color.white;
                textObj.transform.localPosition = new Vector3(0, 0.5f, 0);
                textObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            }

            Update();

            gameObject.AddComponent<ConstraintManager>();
            var om = gameObject.AddComponent<ObjectManipulator>();
            gameObject.AddComponent<UGUIInputAdapterDraggable>();
            om.lastSelectExited.AddListener(delegate (SelectExitEventArgs e)
            {
                Selected.Invoke(e);
            });
        }

        [NonSerialized]
        public static SelectExitEvent Selected = new SelectExitEvent();

        public void SetNameTitleVisible(bool vis)
        {
            TextMesh tm=this.GetComponentInChildren<TextMesh>();
            if (tm)
                tm.gameObject.SetActive(vis);
        }

        public void Update()
        {
            if (!mol) 
                return;

            if (auto)
            {
                dynamicangle.x += Time.deltaTime*rotationfactor;
            }

            float oldscale = molobj.transform.localScale.y;
            float newscale = dynamicscale;
            if (Mathf.Abs(newscale - oldscale) > 0.001f)
            {
                molobj.transform.localScale = new Vector3(-newscale, newscale, newscale);
            }

            Vector3 deltaangle = (dynamicangle - currentangle) * blendfactor;
            currentangle.x = Mathf.Lerp(currentangle.x, dynamicangle.x, blendfactor);
            currentangle.y = Mathf.Lerp(currentangle.y, dynamicangle.y, blendfactor);
            molobj.transform.Rotate(Vector3.up, -deltaangle.x, Space.World);
            molobj.transform.Rotate(Vector3.right, deltaangle.y, Space.World);

            //Vector3 oldangle = molobj.transform.localEulerAngles;
            //Vector3 newangle;
            //newangle.x = Mathf.LerpAngle(oldangle.x, dynamicangle.x, blendfactor);
            //newangle.y = Mathf.LerpAngle(oldangle.y, dynamicangle.y, blendfactor);
            //newangle.z = Mathf.LerpAngle(oldangle.z, dynamicangle.z, blendfactor);
            //molobj.transform.localEulerAngles = newangle;

            //Vector3 oldpos = molobj.transform.localPosition;
            //Vector3 newpos;
            //newpos.x = Mathf.Lerp(oldpos.x, dynamicpos.x, blendfactor);
            //newpos.y = Mathf.Lerp(oldpos.y, dynamicpos.y, blendfactor);
            //newpos.z = Mathf.Lerp(oldpos.z, dynamicpos.z, blendfactor);
            //molobj.transform.position = dynamicpos;

            //Vector3 oldpos = transform.position + dynamicpos;
            mol.transform.position = transform.position + dynamicpos;
            Vector3 newpos = mol.transform.localPosition;
            //molobj.transform.localPosition = newpos;

            if (platformobj != null)
            {
                float platformscale = newscale * 0.7f;
                platformobj.transform.localEulerAngles = mol.transform.localEulerAngles;
                platformobj.transform.localScale = new Vector3(platformscale, platformscale, platformscale);
                platformobj.transform.localPosition = newpos;
            }

            if (this.OperationMode == OperationModeType.Scale)
            {
                TextMesh tm = platformobj.GetComponentInChildren<TextMesh>();
                if (tm != null)
                {
                    float len = newscale * 1.414f;
                    tm.text = len.ToString("F2") + "m";
                    tm.transform.forward = Camera.main.transform.forward;
                }
            }
            else if (this.OperationMode == OperationModeType.Rotation)
            {
                TextMesh[] tms = platformobj.GetComponentsInChildren<TextMesh>();
                foreach (TextMesh tm in tms)
                {
                    if (tm != null)
                    {
                        tm.transform.forward = Camera.main.transform.forward;
                    }
                }
            }

            if (titleTransform!=null)
            {
                Vector3 titleScale = new Vector3(0.05f, 0.05f, 0.05f);
                titleTransform.localScale = titleScale*newscale;
                Vector3 titlePos = newpos;
                titlePos.y += 0.5f*newscale;
                titleTransform.localPosition = titlePos;
                titleTransform.forward = Camera.main.transform.forward;
            }

 
        }

        public void Lock()
        {
            Platform platform = this.GetComponentInChildren<Platform>();
            if (platform == null) return;
            platform.SetVisable(true,(int)operationmode-1);
        }

        public void Unlock()
        {
            Platform platform = this.GetComponentInChildren<Platform>();
            if (platform == null) return;
            platform.SetAllVisable(false);
        }

        public void OnUILayout()
        {

            GUILayout.Box(this.name);

            if (mol) mol.OnUILayout();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("+"))
            {
                dynamicscale *= scalefactor;
                Update();
            }
            if (GUILayout.Button("-"))
            {
                dynamicscale /= scalefactor;
                Update();
            }
            if (GUILayout.Button("<"))
            {
                dynamicangle.y += rotationfactor;
                Update();
            }
            if (GUILayout.Button(">"))
            {
                dynamicangle.y -= rotationfactor;
                Update();
            }
            if (GUILayout.Button("∧"))
            {
                dynamicangle.x += rotationfactor;
                Update();
            }
            if (GUILayout.Button("∨"))
            {
                dynamicangle.x -= rotationfactor;
                Update();
            }
            if (GUILayout.Button("←"))
            {
                dynamicpos.x -= translatefactor;
                Update();
            }
            if (GUILayout.Button("→"))
            {
                dynamicpos.x += translatefactor;
                Update();
            }
            if (GUILayout.Button("↓"))
            {
                dynamicpos.y -= translatefactor;
                Update();
            }
            if (GUILayout.Button("↑"))
            {
                dynamicpos.y += translatefactor;
                Update();
            }
            if (GUILayout.Button("↙"))
            {
                dynamicpos.z -= translatefactor;
                Update();
            }
            if (GUILayout.Button("↗"))
            {
                dynamicpos.z += translatefactor;
                Update();
            }    
            GUILayout.EndHorizontal();
        }
    }
}
