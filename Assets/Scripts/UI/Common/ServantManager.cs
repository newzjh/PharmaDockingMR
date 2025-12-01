using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;
using MoleculeLogic;
using UnityEngine.XR.Interaction.Toolkit;

public class ServantManager : MonoBehaviour
{
    public enum StageDef
    {
        None = -1,
        Welcome = 0,
        Talk,
        Stay,
        SettingShow,
    }

    public StageDef curStage = StageDef.Welcome;
    public StageDef lastStage = StageDef.None;

    public const string welcomenpc = "laoboshiwarpper";
    public const float walkspeed = 30.0f;

    private Camera cam;

    private void Subscribe()
    {
        if (SettingPanel.Instance)
        {
            SettingPanel.Instance.Selected += OnAnswer;
        }
        ActorController.OnChangedStates += OnChangedStates;
    }

    private void Unsubscribe()
    {
        ActorController.OnChangedStates -= OnChangedStates;
        if (SettingPanel.Instance)
        {
            SettingPanel.Instance.Selected -= OnAnswer;
        }
    }

    public void Awake()
    {
        Unsubscribe();
        Subscribe();


        cam = Camera.main;

        ActorController.Selected.AddListener(delegate (SelectExitEventArgs e)
        {
            PickController(e.interactableObject.transform.GetComponent<ActorController>());
        });
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }



    public void SetStage(StageDef stage)
    {
        curStage = stage;
    }

    private Transform GetTransformInChildren(Transform parent, string searchname)
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
        Transform t= GetTransformInChildren(ac.transform, "neckJT");
        if (t != null)
        {
             return t.position;
        }
        return ac.transform.position;
    }

    public void ShowSetting(ActorController ac)
    {
        if (!SettingPanel.Instance)
            return;

        Vector3 eyepos = GetEyePos(ac);

        Transform tPanel = SettingPanel.Instance.transform;
        Vector3 viewdir = cam.transform.forward;
        viewdir.y = 0.0f;
        viewdir = viewdir.normalized;

        tPanel.transform.position = eyepos - viewdir * 0.5f;
        tPanel.transform.forward = viewdir;
        Vector3 langle = tPanel.transform.localEulerAngles;
        langle.y -= 15.0f;
        tPanel.transform.localEulerAngles = langle;

        SettingPanel.Instance.Show();
    }

    public void Echo(ActorController ac,string msg)
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

    public void PickController(ActorController ac)
    {
        if (ac)
        {
            if (ac.name == welcomenpc)
            {
                SetStage(StageDef.SettingShow);
            }
        }
    }

    public void Update()
    {
        //if (UnifiedInputManager.GetMouseButtonDown(0))
        //{
        //    int layerselection = 1 << LayerMask.NameToLayer("Default");
        //    PickResult ret = UnifiedInputManager.Pick(layerselection);
        //    if (ret.Hit)
        //    {
        //        ActorController ac = ret.FocusedObject.GetComponentInParent<ActorController>();
        //        if (ac)
        //        {
        //            if (ac.name == welcomenpc)
        //            {
        //                SetStage(StageDef.SettingShow);
        //            }
        //        }
        //    }
        //}

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
                SetStage(StageDef.None);
        }
        else if (curStage == StageDef.SettingShow)
        {
                SetStage(StageDef.Welcome);
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
            if (ac.name == welcomenpc)
            {
                if (curStage == StageDef.Welcome)
                {
                    SetStage(StageDef.Talk);
                }
                //else if (curStage == StageDef.SettingShow)
                //{
                //    ShowSetting(ac);
                //    StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                //}
            }
        }
    }

    public void OnUpdateStage(StageDef stage)
    {
        switch (stage)
        {
            case StageDef.Welcome:
                break;
            case StageDef.Talk:
                break;
            case StageDef.Stay:
                break;
            case StageDef.SettingShow:
                break;
            default:
                break;
        }
    }

    private Vector3 getNewTarget()
    {
        Vector3 end = cam.transform.position +
        cam.transform.forward * 0.1f - cam.transform.right * 4.0f;
        end /= 0.05f;
        end.y = 0.0f;
        return end;
    }

    private void OnNewAction()
    {
        ActorController ac = GetActor(welcomenpc);
        if (!ac)
            return;

        Vector3 destpos = getNewTarget();
        Vector3 srcpos = ac.transform.localPosition;
        float dis = Vector3.Distance(destpos, srcpos);
        if (dis < 5.1f)
        {
            if (curStage == StageDef.Talk)
            {
                SetStage(StageDef.Stay);
            }
            else if (curStage == StageDef.Stay)
            {
                SetStage(StageDef.Talk);
            }
        }
        else
        {
            SetStage(StageDef.Welcome);
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
                        Vector3 end = getNewTarget();
                        ac.Walk(start, end, walkspeed);
                    }
                }
                break;
            case StageDef.Talk:
                {
                    ActorController ac = GetActor(welcomenpc);
                    if (ac)
                    {
                        Echo(ac, "tap me to show the system setting");
                        Vector3 langle = cam.transform.eulerAngles;
                        langle.x = 0.0f;
                        langle.y += 180.0f-35.0f;
                        langle.z = 0.0f;
                        ac.transform.eulerAngles = langle;
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                    }
                    Invoke("OnNewAction", 4.0f);
                }
                break;
            case StageDef.Stay:
                {
                    ActorController ac = GetActor(welcomenpc);
                    if (ac)
                    {
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
                    }
                    Invoke("OnNewAction", 8.0f);
                }
                break;
            case StageDef.SettingShow:
                {
                    ActorController ac = GetActor(welcomenpc);
                    if (ac)
                    {
                        ShowSetting(ac);
                        StartCoroutine(DelaySetAction(ac, ActorStates.Talk));
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
                    //ActorController ac = GetActor(welcomenpc);
                    //if (ac)
                    //{
                    //    Vector3 start = ac.transform.localPosition;
                    //    Vector3 end = ac.initpos;
                    //    ac.Walk(start, end, walkspeed);
                    //}

                    //if (EchoPanel.Instance)
                    //    EchoPanel.Instance.Hide();
                }
                break;
            case StageDef.Talk:
                {
                    if (EchoPanel.Instance)
                        EchoPanel.Instance.Hide();
                }
                break;
            case StageDef.Stay:
                {
                }
                break;
            case StageDef.SettingShow:
                {
                    ActorController ac = GetActor(welcomenpc);
                    if (ac)
                    {
                        Vector3 start = ac.transform.localPosition;
                        Vector3 end = ac.initpos;
                        ac.Walk(start, end, walkspeed);
                    }

                    if (SettingPanel.Instance)
                        SettingPanel.Instance.Hide();
                }
                break;
            default:
                break;
        }
    }
}

