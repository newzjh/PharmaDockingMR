using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIDrugDiscovery.UI
{
    public enum SmilesDynamicAnimationMode
    {
        GridShuffle = 0,
        FlyInGrid = 1,
        StackCarousel = 2,
        MasonryFlow = 3
    }

    /// <summary>
    /// Runtime-only, code-generated UIToolkit view for animated SMILES stream presentation.
    /// </summary>
    public class SmilesDynamicUIToolkitView : MonoBehaviour
    {
        [Header("UIToolkit Root")]
        public UIDocument uiDocument;

        [Header("Layout")]
        [Range(10, 120)] public int visibleSlots = 50;
        [Range(2, 8)] public int gridColumns = 5;
        [Range(2, 20)] public int stagedUpdateGroupSize = 8;
        [Range(0.01f, 0.5f)] public float stagedUpdateInterval = 0.06f;

        [Header("Batch Ceremony")]
        public bool enableBatchCeremony = true;
        public bool highlightNewItem = true;

        private Action<int, string> onSmilesSelected;
        private bool initialized;

        private VisualElement hostRoot;
        private VisualElement board;
        private VisualElement pulseOverlay;
        private Label batchLabel;
        private Label totalLabel;
        private VisualElement contentRoot;

        private VisualElement gridRoot;
        private VisualElement stackRoot;
        private VisualElement masonryRoot;
        private readonly List<VisualElement> masonryColumns = new List<VisualElement>();

        private readonly List<SmilesCard> gridCards = new List<SmilesCard>();
        private readonly List<SmilesCard> stackCards = new List<SmilesCard>();
        private readonly List<SmilesCard> masonryCards = new List<SmilesCard>();
        private int totalGeneratedCount;

        private class SmilesCard
        {
            public VisualElement element;
            public Label label;
            public int smilesIndex = -1;
            public string smiles = string.Empty;
        }

        public void Initialize(Action<int, string> onItemSelected)
        {
            onSmilesSelected = onItemSelected;
            TryBuildVisualTree();
        }

        public void ResetView()
        {
            totalGeneratedCount = 0;
            gridCards.Clear();
            stackCards.Clear();
            masonryCards.Clear();
            masonryColumns.Clear();
            initialized = false;
            if (hostRoot != null && hostRoot.parent != null)
                hostRoot.parent.Remove(hostRoot);
        }

        public async UniTask ShowBatchAsync(IReadOnlyList<string> batchSmiles, int batchNumber, int globalStartIndex, SmilesDynamicAnimationMode mode)
        {
            if (batchSmiles == null || batchSmiles.Count == 0)
                return;

            if (!TryBuildVisualTree())
                return;

            totalGeneratedCount = Mathf.Max(totalGeneratedCount, globalStartIndex + batchSmiles.Count);
            totalLabel.text = string.Format("Total generated: {0}", totalGeneratedCount);

            if (enableBatchCeremony)
                await PlayBatchCeremonyAsync(batchNumber, batchSmiles.Count);

            if (mode == SmilesDynamicAnimationMode.StackCarousel)
                await PlayStackCarouselAsync(batchSmiles, globalStartIndex);
            else if (mode == SmilesDynamicAnimationMode.MasonryFlow)
                await PlayMasonryFlowAsync(batchSmiles, globalStartIndex);
            else if (mode == SmilesDynamicAnimationMode.FlyInGrid)
                await PlayGridAsync(batchSmiles, globalStartIndex, true);
            else
                await PlayGridAsync(batchSmiles, globalStartIndex, false);
        }

        private bool TryBuildVisualTree()
        {
            if (initialized)
                return true;

            if (uiDocument == null)
            {
                Debug.LogWarning("SmilesDynamicUIToolkitView requires a UIDocument reference.");
                return false;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
                return false;

            hostRoot = new VisualElement();
            hostRoot.name = "smiles-dynamic-host";
            hostRoot.style.position = Position.Absolute;
            hostRoot.style.left = 18;
            hostRoot.style.top = 18;
            hostRoot.style.right = 18;
            hostRoot.style.bottom = 18;

            board = new VisualElement();
            board.style.flexDirection = FlexDirection.Column;
            board.style.flexGrow = 1f;
            board.style.backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.82f);
            board.style.borderBottomLeftRadius = 10;
            board.style.borderBottomRightRadius = 10;
            board.style.borderTopLeftRadius = 10;
            board.style.borderTopRightRadius = 10;
            board.style.borderLeftWidth = 1;
            board.style.borderRightWidth = 1;
            board.style.borderTopWidth = 1;
            board.style.borderBottomWidth = 1;
            board.style.borderLeftColor = new Color(0.32f, 0.52f, 0.95f, 0.5f);
            board.style.borderRightColor = new Color(0.32f, 0.52f, 0.95f, 0.5f);
            board.style.borderTopColor = new Color(0.32f, 0.52f, 0.95f, 0.5f);
            board.style.borderBottomColor = new Color(0.32f, 0.52f, 0.95f, 0.5f);
            board.style.paddingLeft = 12;
            board.style.paddingRight = 12;
            board.style.paddingTop = 10;
            board.style.paddingBottom = 10;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            batchLabel = new Label("Batch waiting...");
            batchLabel.style.color = new Color(0.8f, 0.92f, 1f, 0.98f);
            batchLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            batchLabel.style.fontSize = 14;

            totalLabel = new Label("Total generated: 0");
            totalLabel.style.color = new Color(0.65f, 0.74f, 0.9f, 0.92f);
            totalLabel.style.fontSize = 12;

            header.Add(batchLabel);
            header.Add(totalLabel);

            contentRoot = new VisualElement();
            contentRoot.style.flexGrow = 1;
            contentRoot.style.overflow = Overflow.Hidden;

            pulseOverlay = new VisualElement();
            pulseOverlay.style.position = Position.Absolute;
            pulseOverlay.style.left = 0;
            pulseOverlay.style.right = 0;
            pulseOverlay.style.top = 0;
            pulseOverlay.style.bottom = 0;
            pulseOverlay.style.backgroundColor = new Color(0.55f, 0.8f, 1f, 0f);
            pulseOverlay.pickingMode = PickingMode.Ignore;

            board.Add(header);
            board.Add(contentRoot);
            board.Add(pulseOverlay);
            hostRoot.Add(board);
            root.Add(hostRoot);

            initialized = true;
            return true;
        }

        private async UniTask PlayBatchCeremonyAsync(int batchNumber, int batchSize)
        {
            batchLabel.text = string.Format("Batch #{0} generated · {1} molecules", batchNumber, batchSize);
            await AnimateAsync(0.1f, t =>
            {
                pulseOverlay.style.backgroundColor = new Color(0.55f, 0.8f, 1f, Mathf.Lerp(0f, 0.14f, t));
                board.transform.scale = Vector3.Lerp(Vector3.one, new Vector3(0.99f, 0.99f, 1f), t);
            });
            await AnimateAsync(0.2f, t =>
            {
                pulseOverlay.style.backgroundColor = new Color(0.55f, 0.8f, 1f, Mathf.Lerp(0.14f, 0f, t));
                board.transform.scale = Vector3.Lerp(new Vector3(0.99f, 0.99f, 1f), Vector3.one, t);
            });
        }

        private async UniTask PlayGridAsync(IReadOnlyList<string> batchSmiles, int globalStartIndex, bool flyIn)
        {
            EnsureGrid();

            int replaceCount = Mathf.Clamp(visibleSlots / 5, 5, 12);
            if (gridCards.Count > 0)
                replaceCount = Mathf.Min(replaceCount, gridCards.Count);

            HashSet<int> selectedSlots = new HashSet<int>();
            while (selectedSlots.Count < replaceCount)
                selectedSlots.Add(UnityEngine.Random.Range(0, gridCards.Count));

            List<int> slotOrder = new List<int>(selectedSlots);
            int groupCounter = 0;
            for (int i = 0; i < slotOrder.Count; i++)
            {
                int slotIndex = slotOrder[i];
                int sourceIndex = UnityEngine.Random.Range(0, batchSmiles.Count);
                await ReplaceGridCardAsync(gridCards[slotIndex], batchSmiles[sourceIndex], globalStartIndex + sourceIndex, flyIn);

                groupCounter++;
                if (groupCounter >= stagedUpdateGroupSize)
                {
                    groupCounter = 0;
                    await UniTask.Delay(TimeSpan.FromSeconds(stagedUpdateInterval));
                }
            }
        }

        private async UniTask ReplaceGridCardAsync(SmilesCard card, string smiles, int smilesIndex, bool flyIn)
        {
            if (!string.IsNullOrEmpty(card.smiles))
            {
                Vector3 outPos = flyIn ? new Vector3(UnityEngine.Random.Range(-140f, 140f), UnityEngine.Random.Range(-60f, 60f), 0f) : Vector3.zero;
                await AnimateCardAsync(card.element, 0.12f, card.element.transform.position, outPos, card.element.transform.scale, new Vector3(0.9f, 0.9f, 1f), card.element.resolvedStyle.opacity, 0f);
            }

            card.smiles = smiles;
            card.smilesIndex = smilesIndex;
            card.label.text = smiles;
            card.label.tooltip = smiles;
            card.element.transform.scale = new Vector3(0.92f, 0.92f, 1f);
            card.element.style.opacity = 0f;

            if (flyIn)
            {
                Vector3 from = RandomEdgeOffset();
                card.element.transform.position = from;
                await AnimateCardAsync(card.element, 0.22f, from, Vector3.zero, new Vector3(0.92f, 0.92f, 1f), Vector3.one, 0f, 1f);
            }
            else
            {
                await AnimateCardAsync(card.element, 0.18f, Vector3.zero, Vector3.zero, new Vector3(0.92f, 0.92f, 1f), Vector3.one, 0f, 1f);
            }

            if (highlightNewItem)
                await FlashHighlightAsync(card.element);
        }

        private async UniTask PlayStackCarouselAsync(IReadOnlyList<string> batchSmiles, int globalStartIndex)
        {
            EnsureStack();
            int updates = Mathf.Clamp(stagedUpdateGroupSize + 2, 5, 12);
            updates = Mathf.Min(updates, batchSmiles.Count);
            for (int i = 0; i < updates; i++)
            {
                int sourceIndex = batchSmiles.Count - 1 - i;
                SmilesCard card = CreateCard(52);
                card.smiles = batchSmiles[sourceIndex];
                card.smilesIndex = globalStartIndex + sourceIndex;
                card.label.text = card.smiles;
                card.label.tooltip = card.smiles;
                card.element.transform.position = new Vector3(160f, 0f, 0f);
                card.element.style.opacity = 0f;

                stackRoot.Add(card.element);
                stackCards.Add(card);
                await AnimateCardAsync(card.element, 0.22f, new Vector3(160f, 0f, 0f), Vector3.zero, Vector3.one, Vector3.one, 0f, 1f);
                if (highlightNewItem)
                    await FlashHighlightAsync(card.element);

                if (stackCards.Count > visibleSlots)
                {
                    SmilesCard oldest = stackCards[0];
                    stackCards.RemoveAt(0);
                    await AnimateCardAsync(oldest.element, 0.16f, Vector3.zero, new Vector3(-120f, 0f, 0f), Vector3.one, new Vector3(0.95f, 0.95f, 1f), oldest.element.resolvedStyle.opacity, 0f);
                    oldest.element.RemoveFromHierarchy();
                }

                await UniTask.Delay(TimeSpan.FromSeconds(stagedUpdateInterval));
            }
        }

        private async UniTask PlayMasonryFlowAsync(IReadOnlyList<string> batchSmiles, int globalStartIndex)
        {
            EnsureMasonry();
            int updates = Mathf.Clamp(stagedUpdateGroupSize + 2, 5, 12);
            updates = Mathf.Min(updates, batchSmiles.Count);
            for (int i = 0; i < updates; i++)
            {
                int sourceIndex = i;
                SmilesCard card = CreateCard(48);
                card.smiles = batchSmiles[sourceIndex];
                card.smilesIndex = globalStartIndex + sourceIndex;
                card.label.text = card.smiles;
                card.label.tooltip = card.smiles;
                card.element.transform.position = new Vector3(0f, 40f, 0f);
                card.element.transform.scale = new Vector3(0.96f, 0.96f, 1f);
                card.element.style.opacity = 0f;

                VisualElement targetColumn = GetShortestColumn();
                targetColumn.Add(card.element);
                masonryCards.Add(card);
                await AnimateCardAsync(card.element, 0.2f, new Vector3(0f, 40f, 0f), Vector3.zero, new Vector3(0.96f, 0.96f, 1f), Vector3.one, 0f, 1f);
                if (highlightNewItem)
                    await FlashHighlightAsync(card.element);

                if (masonryCards.Count > visibleSlots)
                {
                    SmilesCard oldest = masonryCards[0];
                    masonryCards.RemoveAt(0);
                    await AnimateCardAsync(oldest.element, 0.14f, Vector3.zero, new Vector3(0f, -28f, 0f), Vector3.one, new Vector3(0.96f, 0.96f, 1f), oldest.element.resolvedStyle.opacity, 0f);
                    oldest.element.RemoveFromHierarchy();
                }

                await UniTask.Delay(TimeSpan.FromSeconds(stagedUpdateInterval));
            }
        }

        private void EnsureGrid()
        {
            if (gridRoot != null)
            {
                gridRoot.style.display = DisplayStyle.Flex;
                if (stackRoot != null) stackRoot.style.display = DisplayStyle.None;
                if (masonryRoot != null) masonryRoot.style.display = DisplayStyle.None;
                return;
            }

            ClearContentRoot();

            gridRoot = new VisualElement();
            gridRoot.style.flexDirection = FlexDirection.Row;
            gridRoot.style.flexWrap = Wrap.Wrap;
            gridRoot.style.alignContent = Align.FlexStart;
            gridRoot.style.alignItems = Align.FlexStart;
            gridRoot.style.flexGrow = 1;
            contentRoot.Add(gridRoot);

            gridCards.Clear();
            int effectiveColumns = Mathf.Max(1, gridColumns - 1);
            float basisPercent = Mathf.Max(8f, (100f / effectiveColumns) - 1.2f);
            for (int i = 0; i < visibleSlots; i++)
            {
                SmilesCard card = CreateCard(52);
                card.element.style.flexBasis = new StyleLength(new Length(basisPercent, LengthUnit.Percent));
                card.element.style.marginRight = 4;
                card.element.style.marginBottom = 4;
                card.element.style.opacity = 0f;
                gridRoot.Add(card.element);
                gridCards.Add(card);
            }
        }

        private void EnsureStack()
        {
            if (stackRoot != null)
            {
                stackRoot.style.display = DisplayStyle.Flex;
                if (gridRoot != null) gridRoot.style.display = DisplayStyle.None;
                if (masonryRoot != null) masonryRoot.style.display = DisplayStyle.None;
                return;
            }

            ClearContentRoot();

            stackRoot = new VisualElement();
            stackRoot.style.flexDirection = FlexDirection.Row;
            stackRoot.style.alignItems = Align.FlexStart;
            stackRoot.style.flexGrow = 1;
            stackRoot.style.overflow = Overflow.Hidden;
            contentRoot.Add(stackRoot);
        }

        private void EnsureMasonry()
        {
            if (masonryRoot != null)
            {
                masonryRoot.style.display = DisplayStyle.Flex;
                if (gridRoot != null) gridRoot.style.display = DisplayStyle.None;
                if (stackRoot != null) stackRoot.style.display = DisplayStyle.None;
                return;
            }

            ClearContentRoot();

            masonryRoot = new VisualElement();
            masonryRoot.style.flexDirection = FlexDirection.Row;
            masonryRoot.style.flexGrow = 1;
            masonryRoot.style.alignItems = Align.FlexStart;
            contentRoot.Add(masonryRoot);

            masonryColumns.Clear();
            for (int i = 0; i < 4; i++)
            {
                VisualElement col = new VisualElement();
                col.style.flexDirection = FlexDirection.Column;
                col.style.flexGrow = 1;
                col.style.marginRight = i < 3 ? 4 : 0;
                masonryRoot.Add(col);
                masonryColumns.Add(col);
            }
        }

        private SmilesCard CreateCard(float minHeight)
        {
            VisualElement card = new VisualElement();
            card.style.minHeight = minHeight;
            card.style.backgroundColor = new Color(0.09f, 0.13f, 0.22f, 0.95f);
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftColor = new Color(0.35f, 0.58f, 0.95f, 0.45f);
            card.style.borderRightColor = new Color(0.35f, 0.58f, 0.95f, 0.45f);
            card.style.borderTopColor = new Color(0.35f, 0.58f, 0.95f, 0.45f);
            card.style.borderBottomColor = new Color(0.35f, 0.58f, 0.95f, 0.45f);
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.marginBottom = 4;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 5;
            card.style.paddingBottom = 5;
            card.style.justifyContent = Justify.FlexStart;

            Label text = new Label();
            text.style.unityTextAlign = TextAnchor.UpperLeft;
            text.style.color = new Color(0.86f, 0.93f, 1f, 0.95f);
            text.style.fontSize = 12;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.overflow = Overflow.Visible;
            card.Add(text);

            SmilesCard smilesCard = new SmilesCard
            {
                element = card,
                label = text
            };

            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (smilesCard.smilesIndex >= 0 && !string.IsNullOrEmpty(smilesCard.smiles))
                    onSmilesSelected?.Invoke(smilesCard.smilesIndex, smilesCard.smiles);
            });

            return smilesCard;
        }

        private async UniTask FlashHighlightAsync(VisualElement card)
        {
            Color baseColor = new Color(0.35f, 0.58f, 0.95f, 0.45f);
            Color hiColor = new Color(0.62f, 0.86f, 1f, 0.95f);
            await AnimateAsync(0.06f, t =>
            {
                Color c = Color.Lerp(baseColor, hiColor, t);
                card.style.borderLeftColor = c;
                card.style.borderRightColor = c;
                card.style.borderTopColor = c;
                card.style.borderBottomColor = c;
            });
            await AnimateAsync(0.18f, t =>
            {
                Color c = Color.Lerp(hiColor, baseColor, t);
                card.style.borderLeftColor = c;
                card.style.borderRightColor = c;
                card.style.borderTopColor = c;
                card.style.borderBottomColor = c;
            });
        }

        private async UniTask AnimateCardAsync(VisualElement element, float duration, Vector3 fromPos, Vector3 toPos, Vector3 fromScale, Vector3 toScale, float fromAlpha, float toAlpha)
        {
            element.transform.position = fromPos;
            element.transform.scale = fromScale;
            element.style.opacity = fromAlpha;
            await AnimateAsync(duration, t =>
            {
                element.transform.position = Vector3.Lerp(fromPos, toPos, t);
                element.transform.scale = Vector3.Lerp(fromScale, toScale, t);
                element.style.opacity = Mathf.Lerp(fromAlpha, toAlpha, t);
            });
        }

        private async UniTask AnimateAsync(float duration, Action<float> updater)
        {
            float timer = 0f;
            float safeDuration = Mathf.Max(0.0001f, duration);
            while (timer < safeDuration)
            {
                timer += Time.deltaTime;
                updater(Mathf.Clamp01(timer / safeDuration));
                await UniTask.Yield();
            }
            updater(1f);
        }

        private Vector3 RandomEdgeOffset()
        {
            int edge = UnityEngine.Random.Range(0, 4);
            float x = 0f;
            float y = 0f;
            if (edge == 0) { x = -220f; y = UnityEngine.Random.Range(-100f, 100f); }
            else if (edge == 1) { x = 220f; y = UnityEngine.Random.Range(-100f, 100f); }
            else if (edge == 2) { x = UnityEngine.Random.Range(-180f, 180f); y = 120f; }
            else { x = UnityEngine.Random.Range(-180f, 180f); y = -120f; }
            return new Vector3(x, y, 0f);
        }

        private VisualElement GetShortestColumn()
        {
            if (masonryColumns.Count == 0)
                return masonryRoot;

            VisualElement best = masonryColumns[0];
            int bestCount = best.childCount;
            for (int i = 1; i < masonryColumns.Count; i++)
            {
                if (masonryColumns[i].childCount < bestCount)
                {
                    best = masonryColumns[i];
                    bestCount = masonryColumns[i].childCount;
                }
            }
            return best;
        }

        private void ClearContentRoot()
        {
            contentRoot.Clear();
            gridRoot = null;
            stackRoot = null;
            masonryRoot = null;
            gridCards.Clear();
            stackCards.Clear();
            masonryCards.Clear();
            masonryColumns.Clear();
        }
    }
}
