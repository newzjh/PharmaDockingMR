using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;

public class HumanModelSelectPanel : MonoBehaviour
{
    public HumanModelNode curlockmc;
    public HumanModelNode lastlockmc;
    Camera cam=null;


    public float RotationSensitivity = 0.2f;
    public float TranslateSensitivity = 1.0f;

    private void Awake()
    {
        Button[] buttons = this.GetComponentsInChildren<Button>();
        if (buttons != null)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn)
                {
                    btn.onClick.AddListener(delegate() { this.OnClick(btn); });
                }
            }
        }

        if (this.gameObject.scene.name == "HeartScene")
            HumanModelController.enableextend = false;
        else
            HumanModelController.enableextend = true;
        cam = null;
    }

    private void SwitchMenuVisible(string menuname)
    {
        Transform tmenu = this.transform.Find(menuname);
        if (tmenu != null)
        {
            tmenu.gameObject.SetActive(!tmenu.gameObject.activeSelf);
        }
    }

    private void OnDestroy()
    {
    }



    private void Refresh()
    {
        if (curlockmc == null)
            return;

        RefreshOperation(curlockmc);
    }

    private void RefreshOperation(HumanModelNode mc)
    {
        return;
        for (int i = 0; i < this.transform.childCount; i++)
        {
            Transform tchild = this.transform.GetChild(i);
            if (tchild.name.StartsWith("ButtonOp"))
            {
                int id = -1;
                int.TryParse(tchild.name.Substring(tchild.name.Length - 2, 2), out id);

                if (id >= 22 && id <= 24)
                {
                    Button b = tchild.GetComponent<Button>();
                    if (id - 22 == (int)mc.OperationMode - 1)
                    {
                        ColorBlock cb = b.colors;
                        Color32 cc = new Color32(0x09, 0x4B, 0x67, 0xFF);
                        cb.normalColor = cc;
                        b.colors = cb;
                    }
                    else
                    {
                        ColorBlock cb = b.colors;
                        Color32 cc = new Color32(0x31, 0x31, 0x31, 0xFF);
                        cb.normalColor = cc;
                        b.colors = cb;
                    }
                }

            }
        }
    }


    private void OnClick(Button b)
    {
        if (curlockmc == null) return;
        HumanModelNode mc = curlockmc;

        if (!mc) return;


        else if (b.name.StartsWith("ButtonOp"))
        {
            int id = -1;
            int.TryParse(b.name.Substring(b.name.Length - 2, 2), out id);
            if (id == 20)
            {
                Destroy(mc.gameObject);
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }
            else if (id == 22)
            {
                mc.OperationMode = HumanModelNode.OperationModeType.Translate;
                UnifiedInputManager.ResetGestureMode(false);
                RefreshOperation(mc);
            }
            else if (id == 23)
            {
                mc.OperationMode = HumanModelNode.OperationModeType.Rotation;
                UnifiedInputManager.ResetGestureMode(true);
                RefreshOperation(mc);
            }
            else if (id == 24)
            {
                mc.OperationMode = HumanModelNode.OperationModeType.Scale;
                UnifiedInputManager.ResetGestureMode(true);
                RefreshOperation(mc);
            }
            else if (id == 25)
            {
                mc.visible = !mc.visible;
            }
            else if (id == 26)
            {
                mc.autoRotate = !mc.autoRotate;
            }
            Refresh();
        }

        UpdateTransform();
    }

    //void OnToggleClick(Toggle toggle, bool value)
    //{
    //    SetMenuVisable("OpitionMenu", false);

    //    if (curlockmc == null) return;
    //    MoleculeController mc = curlockmc;
    //    if (!mc || !mc.mol) return;

    //    Audios audios = Camera.main.transform.gameObject.GetComponentInChildren<Audios>();

    //    if (toggle.name == "Toggle0")
    //    {
    //        mc.mol.scheme.showtext = value ? 1 : 0;
    //        audios.audios[10].Play();
    //        mc.mol.Represent();
    //    }
    //    else if (toggle.name == "Toggle1")
    //    {
    //        mc.mol.scheme.showwater = value ? 1 : 0;
    //        audios.audios[11].Play();
    //        mc.mol.Represent();
    //    }
    //    else if (toggle.name == "Toggle2")
    //    {
    //        mc.mol.scheme.showpocket = value ? 1 : 0;
    //        audios.audios[12].Play();
    //        if (value && mc.mol.pockets.Count <= 0)
    //        {
    //            if (File.Exists(mc.cachefile))
    //            {
    //                FPocket.DetectPockets(mc.mol, mc.cachefile);
    //            }
    //        }
    //        else
    //        {
    //            mc.mol.Represent();
    //        }
    //    }
    //    else if (toggle.name == "Toggle3")
    //    {
    //        mc.mol.scheme.showpharma = value ? 1 : 0;
    //        audios.audios[13].Play();
    //        if (value && mc.mol.pharmas.Count <= 0)
    //        {
    //            if (File.Exists(mc.cachefile))
    //            {
    //                Pharmit.DetectPharmaPoints(mc.mol, mc.cachefile);
    //            }
    //        }
    //        else
    //        {
    //            mc.mol.Represent();
    //        }
    //    }
    //    else if (toggle.name == "ToggleX")
    //    {
    //        mc.mol.gameObject.SetActive(toggle.isOn);
    //    }
    //    else if (toggle.name == "ToggleA")
    //    {
    //        mc.auto = toggle.isOn;
    //    }
    //}
 
    // Update is called once per frame
    void Update()
    {

        //CheckSelection();

        CheckLock();

        UpdateTransform();

        CheckOperation();

    }



    private void CheckSelection()
    {
        //if (cam == null) cam = Camera.main;
        //if (cam == null) return;

        //if (!UnifiedInputManager.OnUI)
        //{
        //    if (UnifiedInputManager.CurObj != null)
        //    {
        //        Debug.Log("UnifiedInputManager.CurObj=" + UnifiedInputManager.CurObj);
        //    }
        //}
    }

    private void CheckLock()
    {
        if (UnifiedInputManager.GetMouseButtonDown(0))
        {
            if (!UnifiedInputManager.OnUI && UnifiedInputManager.CurObj != null)
            {
                HumanModelNode curselectmc =UnifiedInputManager.CurObj.GetComponentInParent<HumanModelNode>();
                if (curlockmc != curselectmc)
                {
                    if (curlockmc)//handle last lock obj
                    {
                        curlockmc.isLocked = false;
                        curlockmc.autoRotate = false;
                    }
                    if (curselectmc != null) //roough pick
                    {
                        curlockmc = curselectmc;
                        curselectmc.isLocked = true;
                        curselectmc.autoRotate = true;
                        Refresh();
                    }
                }
            }
        }


    //    if (curlockmc != lastlockmc)
    //    {
    //        if (lastlockmc != null)
    //        {
    //            lastlockmc.Unlock();
    //        }
    //        if (curlockmc != null)
    //        {
    //            curlockmc.Lock();
    //            Refresh();
    //        }
    //        lastlockmc = curlockmc;
    //    }
    }

    public Transform orgTransform = null;
    private void UpdateTransform()
    {
        if (curlockmc != null)
        {
            this.transform.position = curlockmc.transform.position;
            Vector3 lpos = this.transform.localPosition;
            lpos.z -= 150.0f;
            lpos.y += 100.0f;
            this.transform.localPosition = lpos;
            float scale = 2.0f;// curlockmc.transform.localScale.y;
            this.transform.localScale = new Vector3(scale, scale, scale);

            if (orgTransform && curlockmc && curlockmc.OperationMode == HumanModelNode.OperationModeType.Translate)
            {
                Vector3 scrPos = Camera.main.WorldToScreenPoint(curlockmc.transform.position);
                scrPos.z = 2.0f;
                orgTransform.position = Camera.main.ScreenToWorldPoint(scrPos);
            }
        }
        else
        {
            this.transform.localPosition = new Vector3(-2500, 0, 0);
            if (orgTransform)
            {
                orgTransform.position = this.transform.localPosition;
            }
        }
    }

    private Vector2 lastmousepos;
    private void CheckOperation()
    {
        HumanModelNode mc = curlockmc;
        if (!mc) return;

        //if (Input.GetKeyDown(KeyCode.Delete))
        //{
        //    Destroy(mc.gameObject);
        //    Resources.UnloadUnusedAssets();
        //    System.GC.Collect();
        //}

        Vector2 curmousepos = UnifiedInputManager.mousePosition;
        float deltatime = Time.deltaTime;


        if (mc.OperationMode == HumanModelNode.OperationModeType.Translate) //translate
        {
            Vector3 gesturedelta = Vector3.zero;
            gesturedelta = UnifiedInputManager.GetManipulation(mc.transform.position);
            if (gesturedelta.magnitude > 0)
            {
                mc.dynamicpos += gesturedelta * TranslateSensitivity;
            }
        }
        else if (mc.OperationMode == HumanModelNode.OperationModeType.Rotation) //rotation
        {
            Vector3 delta = UnifiedInputManager.GetNavigation();
            Vector2 deltaangle;
            deltaangle.x = delta.x * RotationSensitivity * mc.rotationfactor;
            deltaangle.y = delta.y * RotationSensitivity * mc.rotationfactor;
            mc.dynamicangle += deltaangle;
        }

        else if (mc.OperationMode == HumanModelNode.OperationModeType.Scale) //scale
        {
            Vector3 delta = UnifiedInputManager.GetNavigation();
            if (delta.x > 0.01f)
                mc.dynamicscale *= mc.scalefactor;
            else if (delta.x < -0.01f)
                mc.dynamicscale /= mc.scalefactor;

            if (delta.z > 0.01f)
                mc.dynamicscale *= mc.scalefactor;
            else if (delta.z < -0.01f)
                mc.dynamicscale /= mc.scalefactor;
        }

        lastmousepos = curmousepos;
    }
}
