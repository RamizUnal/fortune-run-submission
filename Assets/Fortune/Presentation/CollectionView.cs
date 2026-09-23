using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vertigo.Fortune.Content;

namespace Vertigo.Fortune.Presentation
{
    public sealed class CollectionView : MonoBehaviour
    {
        const int PageSize = 12;
        const float PageOffset = 8f;

        [SerializeField] RectTransform grid;
        [SerializeField] CanvasGroup gridGroup;
        [SerializeField] CollectionCard[] cards = new CollectionCard[PageSize];
        [SerializeField] TMP_Text pageLabel;
        [SerializeField] ActionButton previous;
        [SerializeField] ActionButton next;
        [SerializeField] ActionButton close;

        GameContent content;
        IRewardWallet wallet;
        Action closeAction;
        Sequence pageTransition;
        Vector2 gridRestingPosition;
        bool capturedLayout;
        bool reducedMotion;
        bool initialized;

        public int CurrentPage { get; private set; }
        public int PageCount => content == null ? 0 : Mathf.Max(1, (content.Catalog.Items.Count + PageSize - 1) / PageSize);
        public bool IsTransitioning { get; private set; }

        public void Initialize(GameContent gameContent, IRewardWallet rewardWallet,
            bool useReducedMotion, Action onClose)
        {
            content = gameContent != null ? gameContent : throw new ArgumentNullException(nameof(gameContent));
            wallet = rewardWallet ?? throw new ArgumentNullException(nameof(rewardWallet));
            closeAction = onClose ?? throw new ArgumentNullException(nameof(onClose));
            reducedMotion = useReducedMotion;

            ResolveReferences();
            CaptureLayout();
            ResetTransition();
            if (!initialized)
            {
                previous.Clicked += PreviousPage;
                next.Clicked += NextPage;
                close.Clicked += Close;
                initialized = true;
            }
            BindPage(0);
        }

        public void RenderPage(int page, bool animate = true)
        {
            if (!initialized || IsTransitioning) return;
            int destination = Mathf.Clamp(page, 0, PageCount - 1);
            if (destination == CurrentPage) return;

            if (reducedMotion || !animate)
            {
                BindPage(destination);
                return;
            }

            int direction = destination > CurrentPage ? 1 : -1;
            IsTransitioning = true;
            pageTransition = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            pageTransition.Append(gridGroup.DOFade(0, .045f).SetEase(Ease.InQuad));
            pageTransition.Join(grid.DOAnchorPos(gridRestingPosition + Vector2.left * (direction * PageOffset), .045f)
                .SetEase(Ease.InQuad));
            pageTransition.AppendCallback(() =>
            {
                BindPage(destination);
                grid.anchoredPosition = gridRestingPosition + Vector2.right * (direction * PageOffset);
            });
            pageTransition.Append(gridGroup.DOFade(1, .075f).SetEase(Ease.OutQuad));
            pageTransition.Join(grid.DOAnchorPos(gridRestingPosition, .075f).SetEase(Ease.OutQuad));
            pageTransition.OnComplete(() =>
            {
                pageTransition = null;
                IsTransitioning = false;
                UpdateNavigation();
            });
        }

        void BindPage(int page)
        {
            CurrentPage = page;
            int first = page * PageSize;
            for (int index = 0; index < cards.Length; index++)
            {
                var card = cards[index];
                bool visible = first + index < content.Catalog.Items.Count;
                card.Root.SetActive(visible);
                if (!visible) continue;

                var reward = content.Catalog.Items[first + index];
                Color rarityColor = reward.Rarity == "Legendary" ? UiPalette.Gold :
                    reward.Rarity == "Epic" ? UiPalette.Hex("B499FF") : UiPalette.Teal;
                card.Accent.color = rarityColor;
                card.Rarity.color = rarityColor;
                card.Rarity.text = reward.Rarity.ToUpperInvariant();
                card.Name.text = reward.DisplayName;
                card.Art.sprite = content.GetSprite(reward.AssetName);
                card.Art.enabled = card.Art.sprite != null;
                if (card.Art.sprite != null)
                    RewardArtLayout.FitInto(reward.AssetName, card.Art, card.ArtBounds);

                int owned = wallet.GetAmount(reward.Id);
                card.Owned.text = owned > 0 ? "BANKED  x" + WheelView.Compact(owned) : reward.Category.ToUpperInvariant();
                card.Owned.color = owned > 0 ? UiPalette.Teal : UiPalette.Muted;
            }

            pageLabel.text = (CurrentPage + 1) + " / " + PageCount;
            UpdateNavigation();
        }

        void UpdateNavigation()
        {
            // RenderPage gates clicks during the fade without flashing the footer disabled.
            previous.Interactable = CurrentPage > 0;
            next.Interactable = CurrentPage + 1 < PageCount;
        }

        void PreviousPage() => RenderPage(CurrentPage - 1);
        void NextPage() => RenderPage(CurrentPage + 1);
        void Close() => closeAction?.Invoke();

        void Awake()
        {
            ResolveReferences();
            CaptureLayout();
        }

        void OnValidate() => ResolveReferences();

        void ResolveReferences()
        {
            if (grid == null) grid = transform.Find("ui_collection_grid_motion") as RectTransform;
            if (gridGroup == null && grid != null) gridGroup = grid.GetComponent<CanvasGroup>();
            if (pageLabel == null) pageLabel = Find<TMP_Text>(transform, "ui_text_collection_page_value");
            if (previous == null) previous = Find<ActionButton>(transform, "ui_button_collection_previous");
            if (next == null) next = Find<ActionButton>(transform, "ui_button_collection_next");
            if (close == null) close = Find<ActionButton>(transform, "ui_button_collection_close");
            if (cards == null || cards.Length != PageSize) Array.Resize(ref cards, PageSize);
            for (int index = 0; index < cards.Length; index++)
            {
                if (cards[index] == null) cards[index] = new CollectionCard();
                cards[index].Resolve(grid, index);
            }
        }

        void CaptureLayout()
        {
            if (capturedLayout || grid == null) return;
            gridRestingPosition = grid.anchoredPosition;
            capturedLayout = true;
        }

        void ResetTransition()
        {
            pageTransition?.Kill();
            pageTransition = null;
            IsTransitioning = false;
            if (gridGroup != null) gridGroup.alpha = 1;
            if (grid != null && capturedLayout) grid.anchoredPosition = gridRestingPosition;
        }

        void OnDisable()
        {
            ResetTransition();
            if (initialized) UpdateNavigation();
        }

        void OnDestroy()
        {
            pageTransition?.Kill();
            if (previous != null) previous.Clicked -= PreviousPage;
            if (next != null) next.Clicked -= NextPage;
            if (close != null) close.Clicked -= Close;
            closeAction = null;
        }

        static T Find<T>(Transform parent, string name) where T : Component
        {
            var child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<T>() : null;
        }

        [Serializable]
        sealed class CollectionCard
        {
            public GameObject Root;
            public RoundedSurfaceGraphic Accent;
            public TMP_Text Rarity;
            public Image Art;
            public RectTransform ArtBounds;
            public TMP_Text Name;
            public TMP_Text Owned;

            public void Resolve(Transform parent, int index)
            {
                if (Root == null)
                {
                    var root = parent != null ? parent.Find("ui_collection_card_" + index + "_value") : null;
                    if (root != null) Root = root.gameObject;
                }
                if (Root == null) return;
                var card = Root.transform;
                if (Accent == null) Accent = Find<RoundedSurfaceGraphic>(card, "ui_image_rarity_value");
                if (Rarity == null) Rarity = Find<TMP_Text>(card, "ui_text_rarity_value");
                if (ArtBounds == null) ArtBounds = card.Find("ui_reward_art_bounds") as RectTransform;
                if (Art == null) Art = Find<Image>(ArtBounds, "ui_image_reward_value");
                if (Name == null) Name = Find<TMP_Text>(card, "ui_text_reward_name_value");
                if (Owned == null) Owned = Find<TMP_Text>(card, "ui_text_owned_value");
            }
        }
    }
}
