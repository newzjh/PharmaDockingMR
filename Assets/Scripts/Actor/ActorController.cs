using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public enum ActorStates
{
    Idle = 0,
    Walk,
    Run,
    Talk,
}

public class ActorController : MonoBehaviour
{
    public ActorStates lastStates = ActorStates.Idle;
    public ActorStates curStates = ActorStates.Idle;

    public Vector3 initpos;
#if OLD_ANIM
    private Animation anim;
#else
    private Animator anim;
#endif
    private Vector3 start;
    private Vector3 end;
    private float speed;

    public delegate void ActorActionHandler(ActorController ac);
    public static ActorActionHandler OnWalkFinish;
    public static ActorActionHandler OnIdleFinish;
    public static ActorActionHandler OnRunFinish;

    public delegate void ActorStatesEventHandler(ActorController ac, ActorStates state);
    public static ActorStatesEventHandler OnEnterStates;
    public static ActorStatesEventHandler OnLeaveStates;

    public delegate void ActorStatesChangedHandler(ActorController ac, ActorStates oldstates, ActorStates newstates);
    public static ActorStatesChangedHandler OnChangedStates;

	// Use this for initialization
	void Start () {

#if OLD_ANIM
        anim = this.GetComponent<Animation>(); 
        if (anim==null)
            anim = this.GetComponentInChildren<Animation>(); 
#else
        anim = this.GetComponent<Animator>();
        if (anim == null)
            anim = this.GetComponentInChildren<Animator>();
#endif
        initpos = transform.localPosition;
        //initpos.y = 0.0f;
        transform.localPosition = initpos;

        var om = gameObject.AddComponent<PressableButton>();
        gameObject.AddComponent<UGUIInputAdapterDraggable>();
        om.lastSelectExited.AddListener(delegate (SelectExitEventArgs e)
        {
            Selected.Invoke(e);
        });
    }

    [NonSerialized]
    public static SelectExitEvent Selected = new SelectExitEvent();

    public void SetStates(ActorStates states)
    {
        curStates = states;
    }

    private void PlayAnim(string animname)
    {
        if (anim == null)
            return;

        animname = animname.Replace('_', ' ');

#if OLD_ANIM
        if (anim.GetClip(animname))
        {
            anim.CrossFade(animname);
            return;
        }
        else if (anim.GetClip(animname.ToLower()))
        {
            anim.CrossFade(animname.ToLower());
            return;
        }
#else

        if (anim.HasState(0,Animator.StringToHash(animname)))
        {
            anim.CrossFadeInFixedTime(animname, 0.1f);
            return;
        }
        else if (anim.HasState(0, Animator.StringToHash(animname.ToLower())))
        {
            anim.CrossFadeInFixedTime(animname, 0.1f);
            return;
        }
#endif
    }

	// Update is called once per frame
	void Update () {

        if (curStates != lastStates)
        {
            if (OnLeaveStates != null)
                OnLeaveStates(this,lastStates);

            string animname = curStates.ToString();
            PlayAnim(animname);

            if (OnChangedStates != null)
                OnChangedStates(this, lastStates, curStates);

            lastStates = curStates;

            if (OnEnterStates != null)
                OnEnterStates(this,curStates);

        }

        switch (curStates)
        { 
            case ActorStates.Walk:
            case ActorStates.Run:
                HandleMove();
                break;
            case ActorStates.Talk:
                HandleTalk();
                break;
            default:
                HandleIdle();
                break;
        }

	}

    private void HandleTalk()
    {
        
    }

    private void HandleIdle()
    { 
    }

    private void HandleMove()
    {
        float deltatime = Time.deltaTime;
        Vector3 dir = (end - start).normalized;
        float step = deltatime * speed;

        float dis = (end - transform.localPosition).magnitude;
        if (step < dis)
        {
            transform.localPosition += step * dir;
            Vector3 langle=Vector3.zero;
            transform.forward = dir;
        }
        else
        {
            transform.localPosition = end;
            SetStates(ActorStates.Idle);
            if (OnWalkFinish!=null)
                OnWalkFinish(this);
        }
    }

    public void Run(Vector3 _start, Vector3 _end, float _speed)
    {
        start = _start;
        end = _end;
        speed = _speed;
        transform.localPosition = _start;
        SetStates(ActorStates.Run);
    }

    public void Walk(Vector3 _start,Vector3 _end,float _speed)
    {
        start = _start;
        end = _end;
        speed = _speed;
        transform.localPosition = _start;
        SetStates(ActorStates.Walk);
    }


}
