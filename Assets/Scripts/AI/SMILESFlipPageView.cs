using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AIDrugDiscovery.UI
{
    public class SMILESFlipPageView : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        [Header("翻页设置")]
        public int itemsPerPage = 1024; // 每页显示的SMILES数量
        public float swipeThreshold = 50f; // 划动阈值（像素）
        public float edgeThreshold = 50f; // 边缘检测阈值（像素）
        public float animationDuration = 0.3f; // 翻页动画持续时间

        [Header("UI组件")]
        public RectTransform contentPanel; // 内容面板
        public Text pageIndicator; // 页码指示器
        public Text smilesCountText; // SMILES总数显示

        [Header("SMILES数据")]
        public List<string> allSMILES = new List<string>(); // 所有SMILES数据
        public GameObject smilesItemPrefab; // SMILES项目预制体

        private int currentPage = 0;
        private int totalPages = 0;
        private float startDragX = 0f;
        private bool isDragging = false;

        private List<GameObject> activeItems = new List<GameObject>();

        private void Start()
        {
            CalculateTotalPages();
            UpdatePageDisplay();
        }

        private void CalculateTotalPages()
        {
            totalPages = Mathf.CeilToInt((float)allSMILES.Count / itemsPerPage);
            UpdatePageIndicator();
        }

        public void SetSMILESData(List<string> smilesList)
        {
            allSMILES = smilesList;
            CalculateTotalPages();
            currentPage = 0;
            UpdatePageDisplay();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging)
            {
                startDragX = eventData.position.x;
                isDragging = true;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            float endDragX = eventData.position.x;
            float dragDistance = endDragX - startDragX;

            // 检测是否在屏幕边缘进行划动
            bool isLeftEdge = startDragX < edgeThreshold;
            bool isRightEdge = startDragX > Screen.width - edgeThreshold;

            if (isLeftEdge || isRightEdge)
            {
                if (Mathf.Abs(dragDistance) > swipeThreshold)
                {
                    if (dragDistance > 0)
                    {
                        // 向右划动，上一页
                        GoToPreviousPage();
                    }
                    else
                    {
                        // 向左划动，下一页
                        GoToNextPage();
                    }
                }
            }

            isDragging = false;
        }

        public void GoToNextPage()
        {
            if (currentPage < totalPages - 1)
            {
                currentPage++;
                UpdatePageDisplay();
            }
        }

        public void GoToPreviousPage()
        {
            if (currentPage > 0)
            {
                currentPage--;
                UpdatePageDisplay();
            }
        }

        public void GoToPage(int pageIndex)
        {
            currentPage = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            UpdatePageDisplay();
        }

        public void UpdatePageDisplay()
        {
            ClearCurrentItems();
            DisplayCurrentPageItems();
            UpdatePageIndicator();
        }

        private void ClearCurrentItems()
        {
            foreach (var item in activeItems)
            {
                Destroy(item);
            }
            activeItems.Clear();
        }

        private void DisplayCurrentPageItems()
        {
            int startIndex = currentPage * itemsPerPage;
            int endIndex = Mathf.Min(startIndex + itemsPerPage, allSMILES.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                string smiles = allSMILES[i];
                GameObject item = Instantiate(smilesItemPrefab, contentPanel);
                item.name = smiles;
                item.SetActive(true);
                // 设置SMILES文本
                Text textComponent = item.GetComponentInChildren<Text>(true);
                if (textComponent != null)
                {
                    textComponent.text = smiles;
                }

                activeItems.Add(item);
            }
        }

        private void UpdatePageIndicator()
        {
            if (pageIndicator != null)
            {
                pageIndicator.text = $"Page {currentPage + 1} / {totalPages}";
            }

            if (smilesCountText != null)
            {
                smilesCountText.text = $"Total SMILES: {allSMILES.Count}";
            }
        }

        // 公开方法，供外部调用
        public void NextPage()
        {
            GoToNextPage();
        }

        public void PreviousPage()
        {
            GoToPreviousPage();
        }

        public void FirstPage()
        {
            GoToPage(0);
        }

        public void LastPage()
        {
            GoToPage(totalPages - 1);
        }

        // 获取当前状态
        public string GetCurrentStatus()
        {
            return $"Page {currentPage + 1}/{totalPages}, Items: {allSMILES.Count}";
        }
    }
}
