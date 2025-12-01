#if UNITY_WINRT_8_0 || UNITY_WINRT_8_1 || UNITY_WINRT_10_0
#if UNITY_5_3_OR_NEWER
#define WSA_VR
#endif
#endif

using UnityEngine;
using System.Collections;
#if WSA_VR
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Persistence;
#endif

public class SceneAnchor : MonoBehaviour 
{
#if WSA_VR
    private WorldAnchor wa;
#endif

	// Use this for initialization
	void Start () {
        StartCoroutine(Load());
	}

    YieldInstruction wait = new WaitForSeconds(0.1f);
    IEnumerator Load()
    {
#if WSA_VR
        while (!SceneAnchorManager.Instance.enable)
        {
            yield return wait;
        }

        wa=SceneAnchorManager.Instance.LoadSceneObject(this.name,this.gameObject);
        if (wa == null)
        {
            wa=this.gameObject.AddComponent<WorldAnchor>();
            SceneAnchorManager.Instance.SaveSceneObject(this.name, this.gameObject, wa);
        }
#endif
        yield return true;
    }


    void OnDestroy()
    {
        StopAllCoroutines();
    }

}
