using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class AttachPanel : MonoBehaviour
{
    public RectTransform attachTransform;
    public Vector3 attachOffset = Vector3.zero;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!attachTransform)
            return;

        Vector3 localOffset = attachOffset;
        //localOffset.z += -0.1f;
        attachTransform.anchoredPosition3D = localOffset;
    }

    public void Attach()
    {
        if (!Application.isPlaying)
            return;

        if (Application.isMobilePlatform)
            return;

        if (!attachTransform)
            return;

#if UNITY_EDITOR || UNITY_STANDALONE

        attachTransform.transform.parent = transform;
        attachTransform.localScale = Vector3.one;
        attachTransform.localRotation = Quaternion.identity;
        Vector3 localOffset = attachOffset;
        //localOffset.z += -0.1f;
        attachTransform.anchoredPosition3D = localOffset;

        var caster1 = GetComponentInChildren<TrackedDeviceGraphicRaycaster>(true);
        if (caster1)
            MonoBehaviour.Destroy(caster1);

        var caster2 = GetComponentInChildren<GraphicRaycaster>(true);
        if (caster2)
            MonoBehaviour.Destroy(caster2);

        var scaler = GetComponentInChildren<CanvasScaler>(true);
        if (scaler)
            MonoBehaviour.Destroy(scaler);

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas)
            MonoBehaviour.Destroy(canvas);
#endif
    }

    public void Deatch()
    {
        if (!Application.isPlaying)
            return;

        if (Application.isMobilePlatform)
            return;

        if (!attachTransform)
            return;

#if UNITY_EDITOR || UNITY_STANDALONE

#endif
    }
}
