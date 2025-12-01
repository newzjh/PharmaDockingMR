using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;


[ExecuteInEditMode]
public class LayoutViewStyle : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public int style = 1;
    public float dockingwidth = 300;

    public void Layout(Vector2 sizeDelta)
    {
        var imgs = GetComponentsInChildren<Image>(true);
        if (imgs == null)
            return;

        

        if (style == 1)
        {
            if (transform.GetChild(4).gameObject.gameObject.activeSelf)
            {
                var rc = transform.GetChild(4).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2(dockingwidth, sizeDelta.y);
                rc.anchoredPosition = new Vector2(sizeDelta.x * 0.5f - dockingwidth * 0.5f, 0);
            }
            float slicesize = sizeDelta.y / 3.0f;
            {
                var rc = transform.GetChild(0).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2(slicesize, slicesize);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + slicesize * 0.5f, slicesize);
            }
            {
                var rc = transform.GetChild(1).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2(slicesize, slicesize);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + slicesize * 0.5f, 0);
            }
            {
                var rc = transform.GetChild(2).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2(slicesize, slicesize);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + slicesize * 0.5f, -slicesize);
            }
            {
                var rc = transform.GetChild(3).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2(sizeDelta.x - dockingwidth - slicesize, sizeDelta.y);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + slicesize + rc.sizeDelta.x*0.5f, 0);
            }
        }
        else
        {
            if (transform.GetChild(4).gameObject.gameObject.activeSelf)
            {
                var rc = transform.GetChild(4).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2(dockingwidth, sizeDelta.y);
                rc.anchoredPosition = new Vector2(sizeDelta.x * 0.5f - dockingwidth*0.5f, 0);
            }
            {
                var rc = transform.GetChild(0).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2((sizeDelta.x - dockingwidth) * 0.5f, sizeDelta.y * 0.5f);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + (sizeDelta.x - dockingwidth) * 0.25f, sizeDelta.y * 0.25f);
            }
            {
                var rc = transform.GetChild(1).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2((sizeDelta.x - dockingwidth) * 0.5f, sizeDelta.y * 0.5f);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + (sizeDelta.x - dockingwidth) * 0.75f, sizeDelta.y * 0.25f);
            }
            {
                var rc = transform.GetChild(2).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2((sizeDelta.x - dockingwidth) * 0.5f, sizeDelta.y * 0.5f);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + (sizeDelta.x - dockingwidth) * 0.25f, -sizeDelta.y * 0.25f);
            }
            {
                var rc = transform.GetChild(3).gameObject.GetComponent<RectTransform>();
                rc.sizeDelta = new Vector2((sizeDelta.x - dockingwidth) * 0.5f, sizeDelta.y * 0.5f);
                rc.anchoredPosition = new Vector2(-sizeDelta.x * 0.5f + (sizeDelta.x - dockingwidth) * 0.75f, -sizeDelta.y * 0.25f);
            }
        }
    }
}
