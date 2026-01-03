

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using MoleculeLogic;
using ImageEffects;
using UnifiedInput;
using MixedReality.Toolkit.UX;
using UnityEngine.XR.Interaction.Toolkit;
using Mirror;
using System.Linq;

namespace MoleculeUI
{

    public class SelectPanel : BasePanelEx<SelectPanel>
    {
        public MoleculeController curlockmc;
        public MoleculeController lastlockmc;
        public HoverObject lastselectobj;
        public HoverObject curselectobj;
        Camera cam=null;
        GlowOutline glowoutline=null;

        private Transform tDockButton = null;

        public float RotationSensitivity = 0.2f;
        public float TranslateSensitivity = 1.0f;

        delegate void ClickHandler(GameObject b);

        public override void Awake()
        {
            base.Awake();

            tDockButton = transform.Find("ButtonOp27");
            tDockButton.gameObject.SetActive(false);


            cam = null;
            glowoutline = null;

            MoleculeController.Selected.AddListener(delegate (SelectExitEventArgs e)
            {
                SwitchToMC(e.interactableObject.transform.GetComponent<MoleculeController>());
            });
        }

        private void Start()
        {
            StyleMenu.Instance.SetVisible(false);
            OpitionMenu.Instance.SetVisible(false);
        }



        private void OnDestroy()
        {
        }

        Dictionary<MonoBehaviour, UnityAction> clickactions = new Dictionary<MonoBehaviour, UnityAction>();
        Dictionary<Toggle, UnityAction<bool>> toggleactions = new Dictionary<Toggle, UnityAction<bool>>();
        Dictionary<MonoBehaviour, UnityAction<float>> toggleactions1 = new Dictionary<MonoBehaviour, UnityAction<float>>();
        Dictionary<MonoBehaviour, UnityAction<float>> toggleactions2 = new Dictionary<MonoBehaviour, UnityAction<float>>();

        private void UnBindEvents()
        {
            {
                var buttons = this.GetComponentsInChildren<Button>(true);
                if (buttons != null)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var btn = buttons[i];
                        if (btn)
                        {
                            if (clickactions.ContainsKey(btn))
                            {
                                UnityAction uaction = clickactions[btn];
                                btn.onClick.RemoveListener(uaction);
                                clickactions.Remove(btn);
                            }
                        }
                    }
                }
            }

            {
                var buttons = this.GetComponentsInChildren<PressableButton>(true);
                if (buttons != null)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var btn = buttons[i];
                        if (btn && btn.ToggleMode == MixedReality.Toolkit.StatefulInteractable.ToggleType.Button)
                        {
                            if (clickactions.ContainsKey(btn))
                            {
                                UnityAction uaction = clickactions[btn];
                                btn.OnClicked.RemoveListener(uaction);
                                clickactions.Remove(btn);
                            }
                        }
                    }
                }
            }

            Toggle[] toggles = this.GetComponentsInChildren<Toggle>(true);
            if (toggles != null)
            {
                for (int i = 0; i < toggles.Length; i++)
                {
                    Toggle tog = toggles[i];
                    if (tog)
                    {
                        if (toggleactions.ContainsKey(tog))
                        {
                            UnityAction<bool> uaction = toggleactions[tog];
                            tog.onValueChanged.RemoveListener(uaction);
                            toggleactions.Remove(tog);
                        }
                    }
                }
            }

            {
                var buttons = this.GetComponentsInChildren<PressableButton>(true);
                if (buttons != null)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var btn = buttons[i];
                        if (btn && btn.ToggleMode == MixedReality.Toolkit.StatefulInteractable.ToggleType.Toggle)
                        {
                            if (toggleactions1.ContainsKey(btn))
                            {
                                UnityAction<float> uaction = toggleactions1[btn];
                                btn.IsToggled.OnEntered.RemoveListener(uaction);
                                toggleactions1.Remove(btn);
                            }
                            if (toggleactions2.ContainsKey(btn))
                            {
                                UnityAction<float> uaction = toggleactions2[btn];
                                btn.IsToggled.OnExited.RemoveListener(uaction);
                                toggleactions2.Remove(btn);
                            }
                        }
                    }
                }
            }
        }

        private void BindEvents()
        {
            {
                var buttons = this.GetComponentsInChildren<Button>(true);
                if (buttons != null)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var btn = buttons[i];
                        if (btn)
                        {
                            UnityAction uaction = delegate () 
                            {
                                this.OnSelfClickBroadcast(btn.gameObject); 
                            };
                            btn.onClick.AddListener(uaction);
                            clickactions[btn] = uaction;
                        }
                    }
                }
            }

            {
                var buttons = this.GetComponentsInChildren<PressableButton>(true);
                if (buttons != null)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var btn = buttons[i];
                        if (btn && btn.ToggleMode == MixedReality.Toolkit.StatefulInteractable.ToggleType.Button)
                        {
                            UnityAction uaction = delegate () 
                            {
                                this.OnSelfClickBroadcast(btn.gameObject); 
                            };
                            btn.OnClicked.AddListener(uaction);
                            clickactions[btn] = uaction;
                        }
                    }
                }
            }

            Toggle[] toggles = this.GetComponentsInChildren<Toggle>(true);
            if (toggles != null)
            {
                for (int i = 0; i < toggles.Length; i++)
                {
                    Toggle tog = toggles[i];
                    if (tog)
                    {
                        UnityAction<bool> uaction = delegate(bool isOn) 
                        {
                            this.OnToggleClickBroadcast(tog, isOn); 
                        };
                        tog.onValueChanged.AddListener(uaction);
                        toggleactions[tog] = uaction;
                    }
                }
            }

            {
                var buttons = this.GetComponentsInChildren<PressableButton>(true);
                if (buttons != null)
                {
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var btn = buttons[i];
                        if (btn && btn.ToggleMode == MixedReality.Toolkit.StatefulInteractable.ToggleType.Toggle)
                        {
                            {
                                UnityAction<float> uaction = delegate (float e)
                                {
                                    this.OnToggleClickBroadcast(btn,true);
                                };
                                btn.IsToggled.OnEntered.AddListener(uaction);
                                toggleactions1[btn] = uaction;
                            }
                            {
                                UnityAction<float> uaction = delegate (float e)
                                {
                                    this.OnToggleClickBroadcast(btn,false);
                                };
                                btn.IsToggled.OnExited.AddListener(uaction);
                                toggleactions2[btn] = uaction;
                            }
                        }
                    }
                }
            }
        }

        private void RefreshOperation(MoleculeController mc)
        { 
            for (int i = 0; i < this.transform.childCount; i++)
            {
                Transform tchild = this.transform.GetChild(i);
                if (tchild.name.StartsWith("ButtonOp"))
                {
                    int id = -1;
                    int.TryParse(tchild.name.Substring(tchild.name.Length - 2, 2), out id);

                    if (id >= 22 && id <= 24)
                    {
                        {
                            var b = tchild.GetComponent<Button>();
                            if (b)
                            {
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
            }
        }

        private void Refresh()
        {
            if (curlockmc == null) 
                return;
            MoleculeController mc = curlockmc;
            if (!mc || !mc.mol) 
                return;

            RefreshOperation(mc);

            Toggle[] togs = this.GetComponentsInChildren<Toggle>(true);
            if (togs != null)
            {
                foreach (Toggle tog in togs)
                {
                    if (tog.name == "Toggle0")
                    {
                        tog.isOn = mc.mol.scheme.showtext!=0;
                    }
                    else if (tog.name == "Toggle1")
                    {
                        tog.isOn = mc.mol.scheme.showwater != 0;
                    }
                }
            }

            PocketPanel.Instance.SetContent(mc.mol);
            PharmaPanel.Instance.SetContent(mc.mol);
            AtomPanel.Instance.SetContent(mc.mol);
            CompondPanel.Instance.SetContent(mc.mol);

            OpitionMenu.Instance.SetVisible(false);
            StyleMenu.Instance.SetVisible(false);
        }

        private void OnClickEx()
        { }

        private void OnSelfClick(GameObject b)
        {
            if (curlockmc == null) return;
            MoleculeController mc = curlockmc;

            if (!mc || !mc.mol) return;

            if (b.name.StartsWith("ButtonStyle"))
            {
                StyleMenu.Instance.SetVisible(false);

                int id = int.Parse(b.name.Substring(b.name.Length - 1, 1));

                if (mc.mol.scheme.style != id)
                {
                    Audios.Play(id);
                }

                int laststyle = mc.mol.scheme.style;
                mc.mol.scheme.style = id;
                if (mc.mol.scheme.ContainStyle(MoleculeStyle.Micro) && mc.mol.atoms.Count > 100)
                    mc.mol.scheme.style = laststyle;
                mc.mol.Represent();

            }
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
                else if (id == 21)
                {
                    StyleMenu.Instance.SwitchVisible();
                }
                else if (id == 22)
                {
                    //mc.OperationMode = MoleculeController.OperationModeType.Translate;
                    //UnifiedInputManager.ResetGestureMode(false);
                    //RefreshOperation(mc);
                    mc.dynamicscale *= 1.2f;
                    //transform.localScale *= 1.2f;
                }
                else if (id == 23)
                {
                    //mc.OperationMode = MoleculeController.OperationModeType.Rotation;
                    //UnifiedInputManager.ResetGestureMode(true);
                    //RefreshOperation(mc);
                }
                else if (id == 24)
                {
                    //mc.OperationMode = MoleculeController.OperationModeType.Scale;
                    //UnifiedInputManager.ResetGestureMode(true);
                    //RefreshOperation(mc);
                    mc.dynamicscale /= 1.2f;
                    //transform.localScale /= 1.2f;
                }
                else if (id == 25)
                {
                    OpitionMenu.Instance.SwitchVisible();
                }
                else if (id == 26)
                {
                    mc.auto = !mc.auto;
                }
                else if (id == 27)
                {
                    if (cursrcmc != null && curdestmc != null)
                    {
                        if (IDockPanel.Instance)
                            IDockPanel.Instance.Show(cursrcmc, curdestmc);
                        curlockmc = null;
                    }
                }
                else if (id == 30)
                {
                    if (QrCodeViewPanel.Instance)
                        QrCodeViewPanel.Instance.Show(curlockmc);
                }
            }

            UpdateTransform();
        }

        public void OnSelfClickBroadcast(GameObject go)
        {
            string path = GetGameObjectPath(go);
            CmdOnSelfClick(path);
        }

        [Command(requiresAuthority = false)]
        public void CmdOnSelfClick(string gopath)
        {
            RpcOnSelfClick(gopath);
        }

        [ClientRpc]
        public void RpcOnSelfClick(string gopath)
        {
            var sender = GameObject.Find(gopath);
            if (sender == null)
                return;

            OnSelfClick(sender);
        }


        void OnToggleClick(GameObject toggle, bool value)
        {
            if (curlockmc == null) return;
            MoleculeController mc = curlockmc;
            if (!mc || !mc.mol) return;

            if (toggle.name == "Toggle0")
            {
                mc.mol.scheme.showtext = value ? 1 : 0;
                Audios.Play(10);
                mc.mol.Represent();
            }
            else if (toggle.name == "Toggle1")
            {
                mc.mol.scheme.showwater = value ? 1 : 0;
                Audios.Play(11);
                mc.mol.Represent();
            }
            else if (toggle.name == "Toggle2")
            {
                PocketPanel.Instance.SetVisible(value);
                if (value)
                {
                    Audios.Play(12);
                    if (mc.mol.pockets==null)
                    {
                        if (File.Exists(mc.cachefile))
                        {
                            FPocket.Instance.DetectPockets(mc.mol, mc.cachefile);
                        }
                    }
                    else
                    {
                        PocketPanel.Instance.SetContent(mc.mol);
                    }
                }
            }
            else if (toggle.name == "Toggle3")
            {
                PharmaPanel.Instance.SetVisible(value);
                if (value)
                {
                    Audios.Play(13);
                    if (mc.mol.pharmas==null)
                    {
                        if (File.Exists(mc.cachefile))
                        {
                            Pharmit.Instance.DetectPharmaPoints(mc.mol, mc.cachefile);
                        }
                    }
                    else
                    {
                        PharmaPanel.Instance.SetContent(mc.mol);
                    }
                }
            }
            else if (toggle.name == "Toggle4")
            {
                AtomPanel.Instance.SetVisible(value);
                if (value)
                {
                    AtomPanel.Instance.SetContent(mc.mol);
                }
            }
            else if (toggle.name == "Toggle5")
            {
                CompondPanel.Instance.SetVisible(value);
                if (value)
                {
                    CompondPanel.Instance.SetContent(mc.mol);
                }
            }
            else if (toggle.name == "ToggleX")
            {
                mc.mol.gameObject.SetActive(value);
            }
            else if (toggle.name == "ToggleA")
            {
                mc.auto = value;
            }
        }

        public void OnToggleClickBroadcast(MonoBehaviour mono, bool value)
        {
            string path = GetGameObjectPath(mono.gameObject);
            CmdOnToggleClick(path, value);
        }

        [Command(requiresAuthority = false)]
        public void CmdOnToggleClick(string gopath, bool value)
        {
            RpcOnToggleClick(gopath, value);
        }

        [ClientRpc]
        public void RpcOnToggleClick(string gopath, bool value)
        {
            var sender = GameObject.Find(gopath);
            if (sender == null)
                return;

            OnToggleClick(sender, value);
        }

        // Update is called once per frame
        void Update()
        {
            CheckSelection();

            CheckLock();

            UpdateTransform();

            CheckOperation();

            CheckInteractive();
        }



        private void CheckSelection()
        {
            if (cam == null)
                cam = Camera.main;
            if (cam == null) 
                return;
            if (glowoutline == null) 
                glowoutline = cam.GetComponent<GlowOutline>();

            curselectobj = null;
            if (!UnifiedInputManager.OnUI)
            {
                if (UnifiedInputManager.CurObj != null)
                {
                    curselectobj = UnifiedInputManager.CurObj.GetComponentInParent<HoverObject>();
                }
            }

            if (curselectobj != null) 
            {
                if (curselectobj is Residue)
                {
                    curselectobj = curselectobj.GetComponentInParent<Chain>();
                }
                else if (curselectobj is PocketPoint)
                {
                    curselectobj = curselectobj.GetComponentInParent<Pocket>();
                }
            }

            if (curselectobj != lastselectobj)
            {
                if (curselectobj != null)
                {
                    if (glowoutline)
                    {
                        Renderer[] rs = curselectobj.GetComponentsInChildren<Renderer>();
                        if (rs != null)
                        {
                            foreach (Renderer r in rs)
                            {
                                glowoutline.selectRenders.Add(r);
                            }
                        }
                    }
                }

                if (lastselectobj != null)
                {
                    if (glowoutline)
                    {
                        Renderer[] rs = lastselectobj.GetComponentsInChildren<Renderer>();
                        if (rs != null)
                        {
                            foreach (Renderer r in rs)
                            {
                                if (glowoutline.selectRenders.Contains(r))
                                {
                                    glowoutline.selectRenders.Remove(r);
                                }
                            }
                        }
                    }
                }

                lastselectobj = curselectobj;
            }
        }

        private void CheckLock()
        {
            if (UnifiedInputManager.GetMouseButtonDown(0))
            {
                if (!UnifiedInputManager.OnUI)
                {
                    MoleculeController curselectmc = null;
                    if (curselectobj != null)
                    {
                        curselectmc = curselectobj.GetComponentInParent<MoleculeController>();
                    }
                    if (curselectmc == null) //roough pick
                    {
                        int layerselection = 1 << LayerMask.NameToLayer("Selection");
                        PickResult ret = UnifiedInputManager.Pick(layerselection);
                        if (ret.Hit)
                        {
                            curselectmc = ret.FocusedObject.GetComponentInParent<MoleculeController>();
                        }
                    }

                    SwitchToMC(curselectmc);
                }
            }


            if (curlockmc != lastlockmc)
            {
                UnBindEvents();
                if (lastlockmc != null)
                {
                    lastlockmc.Unlock();
                }
                if (curlockmc != null)
                {
                    curlockmc.Lock();
                    Refresh();
                }
                BindEvents();
                lastlockmc = curlockmc;
            }
        }

        public void SwitchToMC(MoleculeController mc)
        {
            if (mc)
                CmdSwitchToMC(mc.name);
            else
                CmdSwitchToMC("");
        }


        [Command(requiresAuthority = false)]
        public void CmdSwitchToMC(string name)
        {
            RpcSwitchToMC(name);
        }

        [ClientRpc]
        public void RpcSwitchToMC(string name)
        {
            if (name == "")
            {
                curlockmc = null;
            }
            else
            {
                var mcs = GameObject.FindObjectsOfType<MoleculeController>(true);
                var mc = mcs.Where(_ => _.name == name).FirstOrDefault();
                if (mc == null)
                    return;

                curlockmc = mc;
            }

            if (curlockmc != lastlockmc)
            {
                UnBindEvents();
                if (lastlockmc != null)
                {
                    lastlockmc.Unlock();
                }
                if (curlockmc != null)
                {
                    curlockmc.Lock();
                    Refresh();
                }
                BindEvents();
                lastlockmc = curlockmc;
            }
        }

        private void UpdateTransform()
        {
            if (curlockmc != null && curlockmc.mol)
            {
                //this.transform.position = curlockmc.mol.transform.position;
                //Vector3 lpos = this.transform.localPosition;
                //lpos.z -= 150.0f;
                //lpos.y += 100.0f;
                //this.transform.localPosition = lpos;
                //float scale = curlockmc.mol.transform.localScale.y;
                //this.transform.localScale = new Vector3(scale, scale, scale);

                Transform tcam = Camera.main.transform;
                Vector3 pos = curlockmc.mol.transform.position;
                pos += tcam.up * (0.1f - (curlockmc.mol.transform.localScale.y - 1.0f)*0.2f);
                pos -= tcam.forward * 0.1f;
                this.transform.position = pos;
                this.transform.forward = Camera.main.transform.forward;
            }
            else
            {
                //this.transform.localPosition = new Vector3(-2500, 0, 0);
                this.transform.position = new Vector3(-2500, 0, 0);
            }

        }

        private MoleculeController lastsrcmc;
        private MoleculeController lastdestmc;
        private MoleculeController cursrcmc;
        private MoleculeController curdestmc;

        private void CheckInteractive()
        {
            cursrcmc = null;
            curdestmc = null;

            if (curlockmc != null && curlockmc.mol)
            {
                Vector3 halfExtents = curlockmc.mol.transform.localScale*0.25f;
                halfExtents.x = Mathf.Abs(halfExtents.x);
                halfExtents.y = Mathf.Abs(halfExtents.y);
                halfExtents.z = Mathf.Abs(halfExtents.z);
                Vector3 dir = curlockmc.mol.transform.forward;
                Quaternion q = curlockmc.mol.transform.rotation;
                float maxdistance = 15.0f;
                int layerselection = 1 << LayerMask.NameToLayer("Selection");
                RaycastHit[] hits=Physics.BoxCastAll(curlockmc.mol.transform.position, halfExtents, dir,q,maxdistance,layerselection);
                if (hits != null)
                {
                    foreach(RaycastHit hit in hits)
                    {
                        if (hit.collider && hit.collider.gameObject)
                        {
                            MoleculeController mc=hit.collider.gameObject.GetComponentInParent<MoleculeController>();
                            if (mc != null && mc!=curlockmc)
                            {
                                if (curlockmc.mol.type == MolType.Ligand && mc.mol.type == MolType.Receptor)
                                {
                                    cursrcmc = curlockmc;
                                    curdestmc = mc;
                                }
                                else if (curlockmc.mol.type == MolType.Receptor && mc.mol.type == MolType.Ligand)
                                {
                                    cursrcmc = mc;
                                    curdestmc = curlockmc;
                                }
                                break;
                            }
                        }
                    }
                }
            }

            if (cursrcmc != lastsrcmc || curdestmc != lastdestmc)
            {
                if (cursrcmc!=null)
                {
                    if (tDockButton != null)
                    {
                        tDockButton.gameObject.SetActive(true);
                    }
                    //showbutton
                }
                else
                {
                    if (tDockButton != null)
                    {
                        tDockButton.gameObject.SetActive(false);
                    }
                    //hidebutton
                }
                lastsrcmc = cursrcmc;
                lastdestmc = curdestmc;
            }
        }

        private Vector2 lastmousepos;
        private void CheckOperation()
        {
            MoleculeController mc = curlockmc;
            if (!mc) return;

            if (UnifiedInputManager.GetKeyDown(KeyCode.Delete))
            {
                Destroy(mc.gameObject);
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }

            Vector2 curmousepos = UnifiedInputManager.mousePosition;
            float deltatime = Time.deltaTime;


            if (mc.OperationMode == MoleculeController.OperationModeType.Translate) //translate
            {
                Vector3 gesturedelta = Vector3.zero;
                gesturedelta = UnifiedInputManager.GetManipulation(mc.transform.position);
                if (gesturedelta.magnitude > 0)
                {
                    mc.dynamicpos += gesturedelta * TranslateSensitivity;
                }
            }
            else if (mc.OperationMode == MoleculeController.OperationModeType.Rotation) //rotation
            {
                Vector3 delta = UnifiedInputManager.GetNavigation();
                Vector2 deltaangle;
                deltaangle.x = delta.x * RotationSensitivity * mc.rotationfactor;
                deltaangle.y = delta.y * RotationSensitivity * mc.rotationfactor;
                mc.dynamicangle += deltaangle;
            }

            else if (mc.OperationMode == MoleculeController.OperationModeType.Scale) //scale
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


}