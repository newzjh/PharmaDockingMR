using UnityEngine;

public class DynamicWorldCanvas : MonoBehaviour
{
    private Canvas m_Canvas = null;
    private RectTransform m_RectTransform = null;
    private Vector3 m_InitPos = Vector3.zero;

    private void Awake()
    {
        m_Canvas = GetComponent<Canvas>();
        m_RectTransform = GetComponent<RectTransform>();
        m_InitPos = m_RectTransform.position;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private int sw = -1;
    private int sh = -1;

    private void ReLayout()
    {
        if (m_RectTransform)
        {
            float fw = 1080.0f * (float)sw / (float)sh - 48;
            float fh = 1080 - 36;
            m_RectTransform.sizeDelta = new Vector2(1080.0f * (float)fw / (float)fh, 1080);
        }
    }

    // Update is called once per frame
    void Update()
    {
        m_RectTransform.position = m_InitPos;

        if (Screen.width != sw || Screen.height != sh)
        {
            sw = Screen.width; 
            sh = Screen.height;

            ReLayout();
        }
    }
}
