using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;
using MoleculeLogic;

namespace RobotUI
{
    public class StageManager : MonoBehaviour
    {
        public enum StageDef
        {
            None = -1,
            Welcome = 0,
            PlatformShow,
            DrugShow,
            DockMatchShow,
            DockResultShow,
        }

        public StageDef curStage = StageDef.Welcome;
        public StageDef lastStage = StageDef.None;

        //public const string welcomenpc = "Beauty2";
        //public const string drugnpc = "Coconut";
        //public const string docknpc = "Flower";
        public const string welcomenpc = "laoboshi";
        public const string drugnpc = "laoboshi";
        public const string docknpc = "laoboshi";
        public const float walkspeed = 30.0f;

        private MoleculeFactory mf;
        private Camera cam;

        public string curDrugName = "Memantine";

        private void Subscribe()
        {
            ActorController.OnChangedStates += OnChangedStates;
            if (ChatPanel.Instance)
            {
                ChatPanel.Instance.Selected += OnAnswer;
            }
            if (WelcomePanel.Instance)
            {
                WelcomePanel.Instance.Selected += OnAnswer;
            }
            if (DrugPanel.Instance)
            {
                DrugPanel.Instance.Selected += OnAnswer;
            }
            if (PlatformPanel.Instance)
            {
                PlatformPanel.Instance.Selected += OnAnswer;
            }
            if (DockMatchPanel.Instance)
            {
                DockMatchPanel.Instance.Selected += OnAnswer;
            }
            if (DockResultPanel.Instance)
            {
                DockResultPanel.Instance.Selected += OnAnswer;
            }
        }

        private void Unsubscribe()
        {
            ActorController.OnChangedStates -= OnChangedStates;
            if (ChatPanel.Instance)
            {
                ChatPanel.Instance.Selected -= OnAnswer;
            }
            if (WelcomePanel.Instance)
            {
                WelcomePanel.Instance.Selected -= OnAnswer;
            }
            if (DrugPanel.Instance)
            {
                DrugPanel.Instance.Selected -= OnAnswer;
            }
            if (PlatformPanel.Instance)
            {
                PlatformPanel.Instance.Selected -= OnAnswer;
            }
            if (DockMatchPanel.Instance)
            {
                DockMatchPanel.Instance.Selected -= OnAnswer;
            }
            if (DockResultPanel.Instance)
            {
                DockResultPanel.Instance.Selected -= OnAnswer;
            }
        }

        public void Awake()
        {
            Unsubscribe();
            Subscribe();

            GameObject go = GameObject.Find("MoleculeFactory");
            if (go != null)
            {
                mf = go.GetComponent<MoleculeFactory>();
            }

            cam = Camera.main;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void SetStage(StageDef stage)
        {
            curStage = stage;
        }

        private Transform GetTransformInChildren(Transform parent,string searchname)
        { 
            Transform[] ts = parent.GetComponentsInChildren<Transform>();
            foreach (Transform t in ts)
            {
                if (t.name == searchname)
                    return t;
            }
            return null;
        }

        private Vector3 GetEyePos(ActorController ac)
        {
            Transform tEye = null;
            if (tEye == null)
                tEye = GetTransformInChildren(ac.transform, "Eyeslash");
            if (tEye==null)
                tEye = GetTransformInChildren(ac.transform, "neckJT");
            if (tEye != null)
            {
                SkinnedMeshRenderer smr = tEye.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    Vector3 eyepos = smr.bounds.center;
                    return eyepos;
                }
            }
            return ac.transform.position;
        }

        public void Say(ActorController ac, string msg, string[] opitions)
        {
            if (!ChatPanel.Instance)
                return;

            Vector3 eyepos = GetEyePos(ac);

            Transform tChatPanel = ChatPanel.Instance.transform;
            Vector3 viewdir = cam.transform.forward;
            viewdir.y = 0.0f;
            viewdir = viewdir.normalized;

            tChatPanel.transform.position = eyepos - viewdir * 0.5f;
            tChatPanel.transform.forward = viewdir;

            ChatPanel.Instance.Show(ac.name, msg, opitions);
        }

        public void Echo(ActorController ac, string msg)
        {
            if (!EchoPanel.Instance)
                return;

            Vector3 eyepos = GetEyePos(ac);

            Transform tPanel = EchoPanel.Instance.transform;
            Vector3 viewdir = cam.transform.forward;
            viewdir.y = 0.0f;
            viewdir = viewdir.normalized;

            tPanel.transform.position = eyepos - viewdir * 0.5f;
            tPanel.transform.forward = viewdir;

            if (EchoPanel.Instance)
                EchoPanel.Instance.Show(msg);
        }

        public void ShowWelcome(ActorController ac)
        {
            Echo(ac, "welcome to our AI drug discovery platform, What can I do for you?");

            if (!WelcomePanel.Instance)
                return;

            Vector3 eyepos = GetEyePos(ac);

            Transform tPanel = WelcomePanel.Instance.transform;
            Vector3 viewdir = cam.transform.forward;
            viewdir.y = 0.0f;
            viewdir.Normalize();

            Vector3 panelpos = eyepos - viewdir * 0.01f;
            panelpos.y -= 0.5f;
            tPanel.position = panelpos;
            tPanel.transform.forward = viewdir;
            WelcomePanel.Instance.Show();

        }

        public void ShowPlatform(ActorController ac)
        {
            if (!PlatformPanel.Instance)
                return;

            Vector3 eyepos = GetEyePos(ac);

            Transform tPlatformPanel = PlatformPanel.Instance.transform;
            Vector3 viewdir = cam.transform.forward;
            viewdir.y = 0.0f;
            viewdir.Normalize();

            Vector3 panelpos = eyepos - viewdir * 0.01f;
            panelpos.y -= 0.5f;
            tPlatformPanel.position = panelpos;
            tPlatformPanel.transform.forward = viewdir;
            PlatformPanel.Instance.Show();

        }

        public void ShowDrug(ActorController ac)
        {
            if (!DrugPanel.Instance)
                return;

            Vector3 eyepos = GetEyePos(ac);
            Vector3 viewdir = cam.transform.forward;
            viewdir.y = 0.0f;
            viewdir.Normalize();

            Vector3 panelpos = eyepos - viewdir * 0.2f;
            panelpos.y -= 0.2f;
            Transform tPanel = DrugPanel.Instance.transform;
            tPanel.position = panelpos;
            tPanel.forward = viewdir;
            DrugPanel.Instance.Show(curDrugName);

            if (mf)
            {
                Vector3 mfpos = panelpos;
                mfpos -= viewdir * 3.0f;
                mf.transform.position = mfpos;
                mf.transform.forward = viewdir;
            }

            string zippath = "userdata/drugs/" + curDrugName + ".pdbqt";
            if (mf)
            {
                mf.CreateFromZip(zippath);
            }
        }

        public void ShowDockMatch(ActorController ac)
        {
            if (!DockMatchPanel.Instance)
                return;

            Vector3 eyepos = GetEyePos(ac);
            Vector3 viewdir = cam.transform.forward;
            viewdir.y = 0.0f;
            viewdir.Normalize();
            Vector3 rightdir = cam.transform.right;
            rightdir.y = 0.0f;
            rightdir.Normalize();

            Vector3 panelpos = eyepos - viewdir * 0.1f;
            panelpos.y -= 0.5f;
            Transform tPanel = DockMatchPanel.Instance.transform;
            tPanel.position = panelpos - rightdir * 1.7f;
            tPanel.forward = viewdir;
            Vector3 langle = tPanel.localEulerAngles;
            langle.y -= 10.0f;
            tPanel.localEulerAngles = langle;
            DockMatchPanel.Instance.Show(curDrugName);

            if (mf)
            {
                Vector3 mfpos = panelpos;
                mfpos -= viewdir * 3.0f;
                mfpos += rightdir * 0.4f;
                mf.transform.position = mfpos;
                mf.transform.forward = viewdir;
            }

            string zippath = "userdata/drugs/" + curDrugName + ".pdbqt";
            if (mf)
            {
                mf.CreateFromZip(zippath);
            }
        }

        public void ShowDockResult(ActorController ac)
        {
            if (!DockResultPanel.Instance)
                return;

            Vector3 eyepos = GetEyePos(ac);
            Vector3 viewdir = cam.transform.forward;
            viewdir.y = 0.0f;
            viewdir.Normalize();
            Vector3 rightdir = cam.transform.right;
            rightdir.y = 0.0f;
            rightdir.Normalize();

            Vector3 panelpos = eyepos - viewdir * 0.1f;
            panelpos.y -= 0.5f;
            Transform tPanel = DockResultPanel.Instance.transform;
            tPanel.position = panelpos + rightdir * 1.7f;
            tPanel.forward = viewdir;
            Vector3 langle = tPanel.localEulerAngles;
            langle.y += 10.0f;
            tPanel.localEulerAngles = langle;
            DockResultPanel.Instance.Show();

            if (mf)
            {
                Vector3 mfpos = panelpos;
                mfpos -= viewdir * 3.0f;
                mf.transform.position = mfpos;
                mf.transform.forward = viewdir;
            }

            iDockConfItem cfgitem = DockMatchPanel.Instance.curConfItem;
            iDock.Instance.Detect(cfgitem.ligand_path, cfgitem.receptor_path, cfgitem);
        }

        ActorController GetActor(string name)
        {
            ActorGroup ag = this.GetComponent<ActorGroup>();
            if (ag == null)
                return null;

            ActorController[] acs = ag.GetComponentsInChildren<ActorController>();
            if (acs == null)
                return null;

            foreach (ActorController ac in acs)
            {
                if (ac.name == name)
                    return ac;
            }

            return null;
        }

        public void Update()
        {
            if (UnifiedInputManager.GetMouseButtonDown(0))
            {
                int layerselection = 1 << LayerMask.NameToLayer("Default");
                PickResult ret = UnifiedInputManager.Pick(layerselection);
                if (ret.Hit)
                {
                    ActorController ac = ret.FocusedObject.GetComponentInParent<ActorController>();
                    if (ac)
                    {
                        if (ac.name == welcomenpc)
                            SetStage(StageDef.Welcome);
                        else if (ac.name == drugnpc)
                            SetStage(StageDef.DrugShow);
                        else if (ac.name == docknpc)
                            SetStage(StageDef.DockMatchShow);
                    }
                }
            }

            if (curStage != lastStage)
            {
                OnLeaveStage(lastStage);

                lastStage = curStage;

                OnEnterStage(curStage);
            }

            OnUpdateStage(curStage);
        }

        public void OnAnswer(int choice)
        {
            if (curStage == StageDef.Welcome)
            {
                if (choice == 0)
                    SetStage(StageDef.PlatformShow);
                else
                    SetStage(StageDef.None);
            }
            else if (curStage == StageDef.PlatformShow)
            {
                if (choice == 0)
                {
                    curDrugName = "Memantine";
                    SetStage(StageDef.DrugShow);
                }
                else
                    SetStage(StageDef.None);
            }
            else if (curStage == StageDef.DrugShow)
            {
                if (choice == 0)
                    SetStage(StageDef.DockMatchShow);
                else if (choice == 1)
                    SetStage(StageDef.PlatformShow);
                else
                    SetStage(StageDef.None);
            }
            else if (curStage == StageDef.DockMatchShow)
            {
                if (choice == 0)
                {
                    if (DockMatchPanel.Instance.curConfItem != null)
                    {
                        SetStage(StageDef.DockResultShow);
                    }
                    else
                    {
                        ActorController ac=GetActor(docknpc);
                        Echo(ac, "please select receptor first!");
                        DockMatchPanel.Instance.Show(curDrugName);
                    }
                }
                else if (choice == 1)
                {
                    SetStage(StageDef.DrugShow);
                }
                else
                {
                    SetStage(StageDef.None);
                }
            }
            else if (curStage == StageDef.DockResultShow)
            {
                if (choice == 0)
                    SetStage(StageDef.None);
                else if (choice == 1)
                    SetStage(StageDef.DockMatchShow);
                else
                    SetStage(StageDef.None);
            }
        }

        private IEnumerator DelaySetAction(ActorController ac,ActorStates state)
        {
            yield return new WaitForEndOfFrame();
            ac.SetStates(state);
            yield return true;
        }



        public void OnChangedStates(ActorController ac, ActorStates oldstates, ActorStates newstates)
        {
            if (oldstates == ActorStates.Walk && newstates == ActorStates.Idle)
            {
                if (curStage == StageDef.Welcome)
                {
                    if (ac.name == welcomenpc)
                    {
                        //string[] opitions = new string[] { "design Alzheimer drug", "cancel" };
                        //Say(ac, "welcome to our AI drug discovery platform, What can I do for you?", opitions);
                        ShowWelcome(ac);
                        Audios.Play("Welcome");
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                    }
                }
                else if (curStage == StageDef.PlatformShow)
                {
                    if (ac.name == welcomenpc)
                    {
                        ShowPlatform(ac);
                        Audios.Play("Platform");
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                    }
                }
                else if (curStage == StageDef.DrugShow)
                {
                    if (ac.name == drugnpc)
                    {
                        ShowDrug(ac);
                        Audios.Play("Mematine");
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                    }
                }
                else if (curStage == StageDef.DockMatchShow)
                {
                    if (ac.name == docknpc)
                    {
                        ShowDockMatch(ac);
                        Audios.Play("Docking");
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                    }
                }
                else if (curStage == StageDef.DockResultShow)
                {
                    if (ac.name == docknpc)
                    {
                        ShowDockResult(ac);
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                    }
                }
            }
        }

        public void OnUpdateStage(StageDef stage)
        {
            switch (stage)
            {
                case StageDef.Welcome:
                    break;
                case StageDef.PlatformShow:
                    break;
                case StageDef.DrugShow:
                    break;
                case StageDef.DockMatchShow:
                    break;
                case StageDef.DockResultShow:
                    break;
                default:
                    break;
            }
        }

        public void OnEnterStage(StageDef stage)
        {
            switch (stage)
            {
                case StageDef.Welcome:
                    {
                        ActorController ac = GetActor(welcomenpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end;
                            end.x = 0.0f;
                            end.y = start.y;
                            end.z = 5.0f;
                            ac.Walk(start, end, walkspeed);
                        }
                    }
                    break;
                case StageDef.PlatformShow:
                    {
                        ActorController ac = GetActor(welcomenpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end;
                            end.x = 0.0f;
                            end.y = start.y;
                            end.z = 5.0f;
                            ac.Walk(start, end, walkspeed);
                        }
                    }
                    break;
                case StageDef.DrugShow:
                    {
                        ActorController ac = GetActor(drugnpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end;
                            end.x = 0.0f;
                            end.y = start.y;
                            end.z = 5.0f;
                            ac.Walk(start, end, walkspeed);
                        }
                    }
                    break;
                case StageDef.DockMatchShow:
                    {
                        ActorController ac = GetActor(docknpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end;
                            end.x = 0.0f;
                            end.y = start.y;
                            end.z = 5.0f;
                            ac.Walk(start, end, walkspeed);
                        }
                    }
                    break;
                case StageDef.DockResultShow:
                    {
                        ActorController ac = GetActor(docknpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end;
                            end.x = 0.0f;
                            end.y = start.y;
                            end.z = 5.0f;
                            ac.Walk(start, end, walkspeed);
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public void OnLeaveStage(StageDef stage)
        {
            switch (stage)
            {
                case StageDef.Welcome:
                    {
                        ActorController ac = GetActor(welcomenpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end = ac.initpos;
                            ac.Walk(start, end, walkspeed);
                        }

                        if (WelcomePanel.Instance)
                            WelcomePanel.Instance.Hide();

                        if (ChatPanel.Instance)
                            ChatPanel.Instance.Hide();

                        if (EchoPanel.Instance)
                            EchoPanel.Instance.Hide();
                    }
                    break;
                case StageDef.PlatformShow:
                    {
                        ActorController ac = GetActor(welcomenpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end = ac.initpos;
                            ac.Walk(start, end, walkspeed);
                        }

                        if (PlatformPanel.Instance)
                            PlatformPanel.Instance.Hide();
                    }
                    break;
                case StageDef.DrugShow:
                    {
                        ActorController ac = GetActor(drugnpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end = ac.initpos;
                            ac.Walk(start, end, walkspeed);
                        }

                        if (DrugPanel.Instance)
                            DrugPanel.Instance.Hide();

                        if (mf)
                            mf.Clear();
                    }
                    break;
                case StageDef.DockMatchShow:
                    {
                        ActorController ac = GetActor(docknpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end = ac.initpos;
                            ac.Walk(start, end, walkspeed);
                        }

                        if (DockMatchPanel.Instance)
                            DockMatchPanel.Instance.Hide();

                        if (mf)
                            mf.Clear();
                    }
                    break;
                case StageDef.DockResultShow:
                    {
                        ActorController ac = GetActor(docknpc);
                        if (ac)
                        {
                            Vector3 start = ac.transform.localPosition;
                            Vector3 end = ac.initpos;
                            ac.Walk(start, end, walkspeed);
                        }

                        if (DockResultPanel.Instance)
                            DockResultPanel.Instance.Hide();

                        if (mf)
                            mf.Clear();
                    }
                    break;
                default:
                    break;
            }
        }
    }

}