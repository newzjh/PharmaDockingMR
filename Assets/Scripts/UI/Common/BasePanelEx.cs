#if UNITY_WINRT_8_0 || UNITY_WINRT_8_1 || UNITY_WINRT_10_0
#if UNITY_5_3_OR_NEWER
#define WSA_VR
#endif
#endif

using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
using MixedReality.Toolkit.UX;
using UnityEngine.XR.Interaction.Toolkit;
using Mirror;

public interface IRefreshable
{
    void Refresh();
    void SetVisible(bool vis);
    bool GetVisible();
    void SwitchVisible();
    void OnShow();
    void OnHide();
}


public class BaseUIProperty
{
    public static Vector3 DefaultPanelPos = new Vector3(0.45f, 0.15f, 2.5f);

    public static Vector3 BasePos = new Vector3(0.3f, 0.2f, 4.0f);
    public static Vector3 BaseForward = Vector3.forward;
    public static Vector3 BaseRight = Vector3.right;
    public static Vector3 BaseUp = Vector3.up;

    public static void Update()
    {
        Camera cam = Camera.main;
        if (!cam)
            return;

        BaseUIProperty.BaseForward = cam.transform.forward;
        BaseUIProperty.BaseRight = cam.transform.right;
        BaseUIProperty.BaseUp = cam.transform.up;
        BaseUIProperty.BasePos = cam.transform.position + BaseUIProperty.BaseForward * DefaultPanelPos.z +
            BaseUIProperty.BaseRight * DefaultPanelPos.x + BaseUIProperty.BaseUp * DefaultPanelPos.y;
    }
}

public class BasePanelMonoBehaviour: NetworkBehaviour
{
    [Command(requiresAuthority = false)]
    public void CmdOnClick(string gopath)
    {
        RpcOnClick(gopath);
    }

    [ClientRpc]
    public void RpcOnClick(string gopath)
    {
        var sender = GameObject.Find(gopath);
        if (sender == null)
            return;

        OnClick(sender);
    }

    public virtual void OnClick(GameObject sender)
    {
    }
}

public class PanelSingleton<T> : BasePanelMonoBehaviour where T : PanelSingleton<T>
{
    private static T _Instance;

    public static T Instance
    {
        get
        {
            if (_Instance == null)
            {
                T[] talls = Resources.FindObjectsOfTypeAll<T>();
                if (talls != null && talls.Length > 0)
                {
                    _Instance = talls[0];
                }
            }
            return _Instance;
        }
    }
}

public class BasePanelEx<T> : PanelSingleton<T>, IRefreshable where T : BasePanelEx<T>
{
    public virtual void Refresh()
    { }



    public virtual void SetVisible(bool vis)
    {
        this.gameObject.SetActive(vis);
        if (vis)
            OnShow();
        else
            OnHide();
    }

    public virtual void OnShow() { }

    public virtual void OnHide() { }

    public virtual bool GetVisible()
    {
        return this.gameObject.activeSelf;
    }

    public virtual void SwitchVisible()
    {
        this.gameObject.SetActive(!this.gameObject.activeSelf);
    }

    public static string GetGameObjectPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = "/" + obj.name + path;
        }
        return path;
    }

    // Use this for initialization
    public virtual void Awake()
    {
        //for (int i = 0; i < this.transform.childCount; i++)
        //{
        //    Transform t = this.transform.GetChild(i);
        //    Button b = t.GetComponent<Button>();
        //    if (b)
        //    {
        //        b.onClick.AddListener(delegate() { this.OnClick(t.gameObject); });
        //    }
        //}

        HashSet<Button> otherbuttons = new HashSet<Button>();
        HashSet<Toggle> othertoggles = new HashSet<Toggle>();
        BasePanelMonoBehaviour[] subpanels = this.GetComponentsInChildren<BasePanelMonoBehaviour>(true);
        if (subpanels!=null)
        {
            for (int i = 1; i < subpanels.Length; i++)
            {
                var other = subpanels[i].GetComponentsInChildren<Button>(true);
                foreach(var b in other)
                    otherbuttons.Add(b);
                var other2 = subpanels[i].GetComponentsInChildren<Toggle>(true);
                foreach (var t in other2)
                    othertoggles.Add(t);
            }
        }
        List<Button> mybuttons = new List<Button>();
        List<Toggle> mytoggles = new List<Toggle>();
        Button[] buttons = this.GetComponentsInChildren<Button>(true);
        foreach(var b in buttons)
        {
            if (!otherbuttons.Contains(b))
                mybuttons.Add(b);
        }
        Toggle[] toggles = this.GetComponentsInChildren<Toggle>(true);
        foreach (var t in toggles)
        {
            if (!othertoggles.Contains(t))
                mytoggles.Add(t);
        }

        for (int i = 0; i < mybuttons.Count; i++)
        {
            Button b = mybuttons[i];

            var go = b.gameObject;
            Button.DestroyImmediate(b);
            var rc = go.GetComponent<RectTransform>();
            var box = go.AddComponent<BoxCollider>();
            var delta = Vector2.one * 0.5f - rc.pivot;
            delta.x *= rc.sizeDelta.x;
            delta.y *= rc.sizeDelta.y;
            box.center = new Vector3(delta.x, delta.y, 4.606735f);
            box.size = new Vector3(rc.sizeDelta.x, rc.sizeDelta.y, 32);
            var newb = go.AddComponent<PressableButton>();
            if (newb)
            {
                newb.OnClicked.AddListener(delegate ()
                {
                    string path = GetGameObjectPath(newb.gameObject);
                    this.CmdOnClick(path);
                    //this.OnClick(newb.gameObject);
                });
                newb.firstHoverEntered.AddListener(delegate (HoverEnterEventArgs e)
                {
                    e.interactableObject.transform.localScale = 1.1f * Vector3.one;
                });
                newb.lastHoverExited.AddListener(delegate (HoverExitEventArgs e)
                {
                    e.interactableObject.transform.localScale = 1.0f * Vector3.one;
                });
            }
            var adapter = go.AddComponent<UGUIInputAdapter>();
            adapter.interactable = true;

        }

        for (int i = 0; i < mytoggles.Count; i++)
        {
            Toggle t = mytoggles[i];
            var graphic = t.graphic;
            bool istoggle = t.isOn;

            var go = t.gameObject;
            Toggle.DestroyImmediate(t);
            var rc = go.GetComponent<RectTransform>();
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0, 4.606735f);
            box.size = new Vector3(rc.sizeDelta.x, rc.sizeDelta.y, 32);
            var newb = go.AddComponent<PressableButton>();
            newb.ToggleMode = MixedReality.Toolkit.StatefulInteractable.ToggleType.Toggle;
            newb.ForceSetToggled(istoggle);
            //var newgo = GameObject.Instantiate(graphic.gameObject, graphic.transform.parent);
            //newgo.SetActive(istoggle);
            graphic.gameObject.SetActive(istoggle);
            if (newb)
            {
                //newb.IsToggled.OnEntered.AddListener(delegate (float e)
                //{
                //    newgo.SetActive(true);
                //    //this.OnToggleClick(newb.gameObject);
                //});
                //newb.IsToggled.OnExited.AddListener(delegate (float e)
                //{
                //    newgo.SetActive(false);
                //    //this.OnToggleClick(newb.gameObject);
                //});
                newb.firstHoverEntered.AddListener(delegate (HoverEnterEventArgs e)
                {
                    e.interactableObject.transform.localScale = 1.1f * Vector3.one;
                });
                newb.lastHoverExited.AddListener(delegate (HoverExitEventArgs e)
                {
                    e.interactableObject.transform.localScale = 1.0f * Vector3.one;
                });
            }
            var adapter = go.AddComponent<UGUIInputAdapter>();
            adapter.interactable = true;

        }

        Invoke("ReCalc", 0.1f);


    }

    private void ReCalc()
    {
        PressableButton[] buttons = this.GetComponentsInChildren<PressableButton>(true);
        foreach(var b in buttons)
        {
            var rc = b.GetComponent<RectTransform>();

            var box = b.GetComponent<BoxCollider>();
            if (rc && box)
            {
                box.size = new Vector3(rc.sizeDelta.x, rc.sizeDelta.y, 256);
            }
        }
    }

}
