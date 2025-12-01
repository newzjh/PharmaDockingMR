using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using ImageEffects;
using UnifiedInput;
using Base;
using MixedReality.Toolkit.SpatialManipulation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Rendering;
using MixedReality.Toolkit.UX;

public class HumanModelController : Singleton<HumanModelController>
{
    public string colliderBoxName = "ColliderBox";
    public string modelName = "Model";
    public string childNodeName = "ChildNode";

    public float rotationSpeed = 10.0f;
    public HumanModelNode showingNode;
    public Transform rootModel;
    public Color showColor = new Color(1, 1, 1, 1.0f);
    public Color hideColor = new Color(1, 1, 1, 0.4f);

    private Dictionary<string, Transform> rootModelList;
    public Material showMaterial;
    public Material hideMaterial;

	// Use this for initialization
	void Start () {
        rootModel = this.transform.GetChild(0);
        GameObject rootNode = new GameObject();
        rootNode.name = "Root";
        rootNode.transform.parent = this.transform;
        rootNode.transform.localPosition = Vector3.zero;
        rootNode.transform.localScale = Vector3.one;
        rootNode.transform.localEulerAngles = Vector3.zero;
        rootModel.name = modelName;
        rootModel.parent = rootNode.transform;
        infoNode(rootModel);
        showingNode = rootNode.GetComponent<HumanModelNode>();
        if (showingNode)
        {
            showingNode.autoRotate = true;
        }

        rootModelList = new Dictionary<string, Transform>();
        pushModelList(rootModel);

        showMaterial = MaterialLib.GetStandardMaterialByColor(showColor);
        hideMaterial = MaterialLib.GetAlphaMaterialByColor(hideColor);

        HumanModelScorePanel.Instance.rootTransoform = rootModel.transform;
	}

    private void pushModelList(Transform node)
    {
        if (!node) return;
        for (int childi = 0; childi < node.childCount; childi++)
        {
            Transform childt = node.GetChild(childi);
            rootModelList.Add(childt.name, childt);
            pushModelList(childt);
        }
    }

    public void infoNode(Transform modelNode,bool rootNode=true,float parentRad=0.0f)
    {
        if (!modelNode || !modelNode.parent) return;
        MeshRenderer[] meshes = modelNode.GetComponentsInChildren<MeshRenderer>();
        SkinnedMeshRenderer[] skinmeshes = modelNode.GetComponentsInChildren<SkinnedMeshRenderer>();
        Bounds bound = new Bounds(modelNode.position, new Vector3(0.1f, 0.1f, 0.1f));
        bool initBound = false;
        foreach (MeshRenderer mesh in meshes)
        {
            if (!initBound)
            {
                initBound = true;
                bound = mesh.bounds;
            }
            else
            {
                bound.Encapsulate(mesh.bounds.max);
                bound.Encapsulate(mesh.bounds.min);
            }

            if (!rootNode)
            {
                mesh.material = showMaterial;
                if (modelNode.parent.name == "Skin")
                {
                    mesh.material.color = new Color(0.0f, 1.0f, 1.0f);
                }
            }
        }
        foreach (SkinnedMeshRenderer mesh in skinmeshes)
        {
            if (!initBound)
            {
                initBound = true;
                bound = mesh.bounds;
            }
            else
            {
                bound.Encapsulate(mesh.bounds.max);
                bound.Encapsulate(mesh.bounds.min);
            }

            if (!rootNode)
            {
                mesh.material = showMaterial;
                if (modelNode.parent.name == "Skin")
                {
                    mesh.material.color = new Color(0.0f, 1.0f, 1.0f);
                }
            }
        }

        GameObject box=GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = colliderBoxName;
        box.transform.position = bound.center;
        box.transform.localScale = bound.size;
        box.transform.parent = modelNode;
        MeshRenderer boxMesh = box.GetComponent<MeshRenderer>();
        boxMesh.enabled = false;

        float scaleForParent=1.0f;
        if (parentRad > 0.001f)
        {
            float currentRad = Mathf.Max(bound.size.x, bound.size.y);
            scaleForParent=parentRad/currentRad;
            if (scaleForParent > 1.25f) //放大到1：1太夸张了，缩小一点
            {
                scaleForParent *= 0.8f;
            }
            modelNode.localScale *= scaleForParent;
        }
        Vector3 centerOffset = modelNode.parent.position - box.transform.position;
        modelNode.position += centerOffset;
        box.transform.position = modelNode.parent.position;

        HumanModelNode scr=modelNode.parent.gameObject.AddComponent<HumanModelNode>();
        scr.selectBoxRad = Mathf.Max(bound.size.x * scaleForParent, bound.size.y * scaleForParent);
        scr.visible = true;
        scr.nodeLocalOffset = modelNode.parent.localPosition;
        scr.modelLocalScale = modelNode.localScale.y;

        scr.gameObject.AddComponent<ConstraintManager>();
        var om = scr.gameObject.AddComponent<ObjectManipulator>();
        scr.gameObject.AddComponent<UGUIInputAdapterDraggable>();
        om.lastSelectExited.AddListener(delegate (SelectExitEventArgs e)
        {
            var c = e.interactableObject.transform.gameObject.GetComponentInChildren<Collider>();
            if (c)
            {
                OnClickBox(c.transform);
            }
        });

    }

	// Update is called once per frame
	void Update () 
    {
        if (UnifiedInputManager.GetMouseButtonDown(0))
        {
            OnClickLeft();
        }
        else if (UnifiedInputManager.GetMouseButtonDown(1))
        {
            OnClickRight();
        }
	}

    Camera cam = null;
    public GameObject curselectobj = null;
    void OnClickLeft()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        //if (glowoutline == null) glowoutline = cam.GetComponent<GlowOutline>();

        curselectobj = null;
        if (!UnifiedInputManager.OnUI)
            curselectobj = UnifiedInputManager.CurObj;

        if (curselectobj)
        {
            //Debug.Log("left click"+curselectobj.name);

            OnClickBox(curselectobj.transform);
        }


        //RaycastHit hitInfo;
        //Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        //if (Physics.Raycast(mouseRay, out hitInfo))
        //{
        //    if (hitInfo.transform)
        //    {
        //        OnClickBox(hitInfo.transform);
        //    }
        //}
    }


    void OnClickRight()
    {
        //RaycastHit hitInfo;
        //Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        //if (Physics.Raycast(mouseRay, out hitInfo))
        //{
        //    if (hitInfo.transform && hitInfo.transform.parent && hitInfo.transform.parent.parent)
        //    {
        //        OnHideNode(hitInfo.transform.parent.parent);
        //    }
        //}
    }


    public void rootModelVisible (string name,bool visible)
    {
        if (rootModelList == null) return;
        if (rootModelList.ContainsKey(name))
        {
            GameObject findModel = rootModelList[name].gameObject;
            findModel.SetActive(visible);
        }
    }

    public Transform getRootChild(string name)
    {
        if (rootModelList == null) return null;
        if (rootModelList.ContainsKey(name))
        {
            GameObject findModel = rootModelList[name].gameObject;
            return findModel.transform;
        }
        return null;
    }

    public static bool enableextend=true;

    void OnClickBox(Transform box)
    {
        if (!box)
        {
            return;
        }
        
        if (box.parent && box.parent.parent)
        {
            Debug.Log("left Click " + box.parent.parent.name);

            if (box.parent.parent.childCount==1)
            {
                if (enableextend)
                    ExtendTreeNode(box.parent);
            }
            showingNode = box.GetComponentInParent<HumanModelNode>();
        }
    }

    void ExtendTreeNode(Transform modelNode)
    {
        if (!modelNode || !modelNode.parent)
        {
            return;
        }

        HumanModelNode scr = modelNode.parent.GetComponent<HumanModelNode>();
        if (scr == null) return;

        if (modelNode.childCount <= 2) //大于或等于2个子节点时扩展，排除掉一个ColliderBox
        {
            return;
        }


        float stepWidth = Mathf.Sin(Mathf.PI / (modelNode.childCount - 2) / 2.0f) * 2.0f * scr.selectBoxRad;
        stepWidth = Mathf.Min(stepWidth, scr.selectBoxRad);
        for (int childi = 0; childi < modelNode.childCount; childi++)
        {
            Transform childt = modelNode.GetChild(childi);
            if (childt.name != colliderBoxName)
            {
                GameObject newGo = new GameObject();
                newGo.name = childt.name;
                newGo.transform.parent = modelNode.parent;
                newGo.transform.localPosition = Vector3.zero;
                newGo.transform.localScale = Vector3.one;
                newGo.transform.localEulerAngles = Vector3.zero;
                GameObject newChildModel = GameObject.Instantiate(childt.gameObject, newGo.transform) as GameObject;
                newChildModel.transform.localEulerAngles = Vector3.zero;
                newChildModel.name = modelName;

                float angle = Mathf.PI / (modelNode.childCount - 2) * childi; //子节点展开半圈，排除掉ColliderBox要-1，起点终点都排布节点要-1
                Vector3 offset = new Vector3(-Mathf.Cos(angle), Mathf.Sin(angle), 0.0f);
                newGo.transform.position += offset * scr.selectBoxRad;
                infoNode(newChildModel.transform, false,stepWidth);
            }
        }

    }
}
