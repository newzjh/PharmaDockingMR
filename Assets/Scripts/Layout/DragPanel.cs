using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Utility
{
    /// <summary>
    /// UGUI面板的拖拽移动功能
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        /// <summary>
        /// 静态方法，提供动态绑定拖拽面板的接口
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <returns></returns>
        public static DragPanel Get(RectTransform rectTransform)
        {
            DragPanel dragPanel = rectTransform.gameObject.GetComponent<DragPanel>();
            if (dragPanel == null)
            {
                dragPanel = rectTransform.gameObject.AddComponent<DragPanel>();
            }
            return dragPanel;
        }

        /// <summary>
        /// 当前拖拽面板的根节点，一般是Canvas
        /// </summary>
        private RectTransform canvasRect;
        private RectTransform myRect;
        private Canvas rootCanvas;
        private Camera uiCam;
        /// <summary>
        /// 是否允许拖拽
        /// </summary>
        private bool isAllowDrag;

        private Vector3 mMouseDownPosition;
        private Vector3 mPanelOriginPosition;

        private void Awake()
        {
            rootCanvas = gameObject.GetComponentInParent<Canvas>(true);
            canvasRect = rootCanvas.GetComponent<RectTransform>();
            myRect = transform.GetComponent<RectTransform>();
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                uiCam = rootCanvas.worldCamera;
            }
            isAllowDrag = rootCanvas != null;
        }

        Vector2 mousepos = Vector2.zero;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isAllowDrag) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.enabled)
            {
                mousepos = mouse.position.ReadValue();
            }
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                mousepos,
                uiCam,
                out mMouseDownPosition);
            mPanelOriginPosition = transform.position;
        }


        public void OnDrag(PointerEventData eventData)
        {
            if (!isAllowDrag) return;
            Vector3 currentMousePosInUGUI;
            var mouse = Mouse.current;
            if (mouse != null && mouse.enabled)
            {
                mousepos = mouse.position.ReadValue();
            }
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                mousepos,
                uiCam,
                out currentMousePosInUGUI);
            transform.position = mPanelOriginPosition + (currentMousePosInUGUI - mMouseDownPosition);

            Vector2 s = Vector2.zero;
            s.x = Mathf.Clamp01(myRect.anchoredPosition.x / 720.0f + 0.5f);
            s.y = Mathf.Clamp01(myRect.anchoredPosition.y / 720.0f + 0.5f);

            if (Dragged != null)
                Dragged.Invoke(eventData, s, currentMousePosInUGUI);
        }

        public UnityEvent<PointerEventData, Vector2, Vector3> Dragged = new ();
    }
}

