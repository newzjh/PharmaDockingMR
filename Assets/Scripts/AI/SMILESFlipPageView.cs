using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace AIDrugDiscovery.UI
{
    // Presents generated SMILES in pages and tabs so the user can request one ligand preview at a time.
    public class SMILESFlipPageView : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        [Header("Settings")]
        public int itemsPerPage = 1024;
        public float swipeThreshold = 50f;
        public float edgeThreshold = 50f;
        public float animationDuration = 0.3f;

        [Header("UI References")]
        public RectTransform contentPanel;
        public RectTransform tabPanel;
        public Text pageIndicator;
        public Text smilesCountText;

        [Header("Data And Prefabs")]
        public List<string> allSMILES = new List<string>();
        public GameObject smilesItemPrefab;
        public GameObject tabButtonPrefab;

        private int currentPage = 0;
        private int totalPages = 0;
        private float startDragX = 0f;
        private bool isDragging = false;

        private List<GameObject> activeItems = new List<GameObject>();
        private List<GameObject> activeTabs = new List<GameObject>();
        public Action<int, string> onSmilesSelected;

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

        public void AddSMILES(string smiles)
        {
            allSMILES.Add(smiles);
            CalculateTotalPages();
            if (currentPage == totalPages - 1 || totalPages <= 1)
                UpdatePageDisplay();
            else
                BuildTabs();
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

            // Only start page turns when the gesture begins near the screen edge.
            bool isLeftEdge = startDragX < edgeThreshold;
            bool isRightEdge = startDragX > Screen.width - edgeThreshold;

            if (isLeftEdge || isRightEdge)
            {
                if (Mathf.Abs(dragDistance) > swipeThreshold)
                {
                    if (dragDistance > 0)
                    {
                        
                        GoToPreviousPage();
                    }
                    else
                    {
                        
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
            BuildTabs();
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

        private void BuildTabs()
        {
            if (tabPanel == null || tabButtonPrefab == null)
                return;

            foreach (var tab in activeTabs)
                Destroy(tab);
            activeTabs.Clear();

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                int capturedPage = pageIndex;
                GameObject tabObject = Instantiate(tabButtonPrefab, tabPanel);
                tabObject.name = $"Tab_{capturedPage + 1}";
                Text tabText = tabObject.GetComponentInChildren<Text>(true);
                if (tabText != null)
                    tabText.text = $"Page {capturedPage + 1}";

                Button tabButton = tabObject.GetComponent<Button>();
                if (tabButton != null)
                {
                    tabButton.onClick.RemoveAllListeners();
                    tabButton.onClick.AddListener(() => GoToPage(capturedPage));
                }

                Image tabImage = tabObject.GetComponent<Image>();
                if (tabImage != null)
                    tabImage.color = capturedPage == currentPage ? new Color(0.25f, 0.5f, 0.9f, 1f) : Color.white;

                activeTabs.Add(tabObject);
            }
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
                
                Text textComponent = item.GetComponentInChildren<Text>(true);
                if (textComponent != null)
                {
                    textComponent.text = smiles;
                }

                Button itemButton = item.GetComponent<Button>();
                if (itemButton != null)
                {
                    int capturedIndex = i;
                    string capturedSmiles = smiles;
                    itemButton.onClick.RemoveAllListeners();
                    itemButton.onClick.AddListener(() => onSmilesSelected?.Invoke(capturedIndex, capturedSmiles));
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

        
        public string GetCurrentStatus()
        {
            return $"Page {currentPage + 1}/{totalPages}, Items: {allSMILES.Count}";
        }
    }
}
