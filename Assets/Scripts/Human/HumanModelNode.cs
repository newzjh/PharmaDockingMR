using UnityEngine;
using System.Collections;
using MixedReality.Toolkit.SpatialManipulation;
using UnityEngine.XR.Interaction.Toolkit;

public class HumanModelNode : MonoBehaviour
{
    public enum OperationModeType
    {
        None = 0,
        Translate = 1,
        Rotation = 2,
        Scale = 3,
    }

    public float selectBoxRad = 0.0f;
    public bool isLocked = false;
    public OperationModeType OperationMode = OperationModeType.Translate;
    public bool autoRotate = false;
    public Vector3 nodeLocalOffset;
    public float modelLocalScale;
    public Vector2 currentangle;


    public Vector3 dynamicpos;
    public float rotationfactor=15.0f;
    public Vector2 dynamicangle;
    public float dynamicscale=1.0f;
    public float scalefactor=1.15f;
    public float blendfactor = 0.15f;

    public Transform modelTransform = null;

    public bool isMatch = false;
    public Material matchMaterial;

    private bool m_visible = true;
    public bool visible
    {
        set
        {
            m_visible = value;
            updateModelVisible();
        }
        get
        {
            return m_visible;
        }
    }

    void Start()
    {
        modelTransform = transform.Find(HumanModelController.Instance.modelName);
        if (modelTransform == null)
        {
            modelTransform = transform;
        }

        if (this.transform.parent)
        {
            this.dynamicpos = this.transform.parent.position - this.transform.position;
            bAnim = true;
        }

    }

    void Update()
    {
        if (autoRotate)
        {
            dynamicangle.x += Time.deltaTime * rotationfactor;
        }

        float oldscale = modelTransform.localScale.y;
        float newscale = dynamicscale * modelLocalScale;
        if (Mathf.Abs(newscale - oldscale) > 0.001f)
        {
            modelTransform.localScale = new Vector3(-newscale, newscale, newscale);
        }

        Vector3 deltaangle = (dynamicangle - currentangle) * blendfactor;
        currentangle.x = Mathf.Lerp(currentangle.x, dynamicangle.x, blendfactor);
        currentangle.y = Mathf.Lerp(currentangle.y, dynamicangle.y, blendfactor);
        modelTransform.Rotate(Vector3.up, -deltaangle.x, Space.World);
        modelTransform.Rotate(Vector3.right, deltaangle.y, Space.World);


        transform.localPosition = nodeLocalOffset;
        transform.position += dynamicpos;
        //modelTransform.localScale = new Vector3(dynamicscale, dynamicscale, dynamicscale);

        checkMatch();

        UpdateAnim();

    }

    private bool bAnim = false;
    void UpdateAnim()
    {
        if (!bAnim)
            return;

        this.dynamicpos = Vector3.Lerp(this.dynamicpos, Vector3.zero, 0.05f);
        float dis = this.dynamicpos.magnitude;
        if (dis < 0.1f)
            bAnim = false;
    }

    public void StartAnim()
    {
        bAnim = true;
    }

    public void StopAnim()
    {
        bAnim = false;
    }

    void checkMatch()
    {
        if (!isLocked)
        {
            return;
        }

        if (!(OperationMode == HumanModelNode.OperationModeType.Translate))
        {
            return;
        }

        //if (!Input.GetMouseButtonUp(0))
        //{
        //    return;
        //}

        Transform nodeInRoot = HumanModelController.Instance.getRootChild(transform.name); ;

        if (!nodeInRoot)
        {
            return;
        }

        BoxCollider colBox = nodeInRoot.GetComponent<BoxCollider>();
        if (!colBox)
        {
            Transform colTransform=modelTransform.Find(HumanModelController.Instance.colliderBoxName);
            if (colTransform)
            {
                colBox = nodeInRoot.gameObject.AddComponent<BoxCollider>();
                colBox.center = colTransform.localPosition;
                colBox.size = colTransform.localScale;
            }
        }


        Vector3 screenPosition = Camera.main.WorldToScreenPoint(transform.position);
        Ray textRay = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit[] hitInfos=Physics.RaycastAll(textRay,100.0f);
        foreach (var hitInfo in hitInfos)
        {
            if (colBox.transform == hitInfo.transform)
            {
                Debug.Log("Match box collider" + colBox.transform.name);
                MeshRenderer[] meshes = colBox.GetComponentsInChildren<MeshRenderer>();
                if (matchMaterial == null)
                {
                    matchMaterial = MaterialLib.GetStandardMaterialByColor(Color.HSVToRGB(Random.Range(0.0f, 1.0f), 1.0f, 1.0f));
                }
                foreach (MeshRenderer mesh in meshes)
                {
                    mesh.material = matchMaterial;
                }
                MeshRenderer[] meshes1 = this.modelTransform.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer mesh in meshes1)
                {
                    mesh.material = matchMaterial;
                }
                if (isMatch == false)
                {
                    HumanModelScorePanel.Instance.score += 10;
                }
                isMatch = true;
                Invoke("StartAnim", 1.0f);
            }           
        }
        //if (hitInfos!=null && hitInfos.Length >= 2)
        //{
        //    this.dynamicpos = Vector3.zero;
        //}


    }


    void updateModelVisible()
    {
        HumanModelController.Instance.rootModelVisible(transform.name, this.visible);
        if (modelTransform)
        {
            MeshRenderer[] meshes = modelTransform.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer mesh in meshes)
            {
                if (this.visible)
                {
                    if (isMatch && matchMaterial!=null)
                    {
                        mesh.material = matchMaterial;
                    }
                    else
                    {
                        mesh.material = HumanModelController.Instance.showMaterial;
                    }
                }
                else
                {
                    mesh.material = HumanModelController.Instance.hideMaterial;
                }
            }
        }
    }

}
