using Cysharp.Threading.Tasks;
using UnityEngine;

[ExecuteInEditMode]
public class LayoutViewRoot : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private int lastsw = -1;
    private int lastsh = -1;

    // Update is called once per frame
    void Update()
    {
        int sw = Screen.width;
        int sh = Screen.height;

        if (sw!=lastsw || sh!=lastsh)
        {
            Layout();

            lastsw = sw;
            lastsh = sh;
        }
    }

    public async void Layout()
    {
        await UniTask.NextFrame();

        var canvas = gameObject.GetComponentInParent<Canvas>(true);
        var sizeDelta = canvas.GetComponent<RectTransform>().sizeDelta;
        if (sizeDelta.x != 0 && sizeDelta.y != 0)
        {
            sizeDelta.x -= 48;
            sizeDelta.y -= 36;
            var rc = GetComponent<RectTransform>();
            rc.sizeDelta = sizeDelta;
            rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f, sizeDelta.y * 0.5f);

            var lvs = GetComponentInChildren<LayoutViewStyle>(true);
            if (lvs)
                lvs.Layout(sizeDelta);
        }
    }
}
