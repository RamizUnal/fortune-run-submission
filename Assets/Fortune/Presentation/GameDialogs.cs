using System;
using TMPro;
using UnityEngine;
using Vertigo.Fortune.Content;

namespace Vertigo.Fortune.Presentation
{
    public sealed class GameDialogs : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] PopupView help;
        [SerializeField] PopupView failure;
        [SerializeField] PopupView banked;
        [SerializeField] PopupView collection;
        [SerializeField] CollectionView collectionView;

        [Header("Changing content")]
        [SerializeField] TMP_Text failureZone;
        [SerializeField] TMP_Text bankedCount;

        [Header("Controls")]
        [SerializeField] ActionButton retry;
        [SerializeField] ActionButton revive;
        [SerializeField] ActionButton runAgain;
        [SerializeField] ActionButton motion;
        [SerializeField] ActionButton helpClose;

        GameContent content;
        IRewardWallet wallet;
        PopupView active;
        bool initialized;

        public bool HasOpen => active != null;
        public event Action RestartRequested;
        public event Action ReviveRequested;
        public event Action MotionToggleRequested;

        public void Initialize(GameContent gameContent, IRewardWallet rewardWallet, Action clickFeedback)
        {
            content = gameContent;
            wallet = rewardWallet;
            ResolveReferences();
            foreach (var button in GetComponentsInChildren<ActionButton>(true))
                button.SetClickFeedback(clickFeedback);
            if (initialized) return;

            retry.Clicked += RequestRestart;
            runAgain.Clicked += RequestRestart;
            revive.Clicked += RequestRevive;
            motion.Clicked += RequestMotionToggle;
            helpClose.Clicked += Close;
            initialized = true;
        }

        public void ShowFailure(int zone, bool reducedMotion)
        {
            failureZone.text = "ZONE " + zone.ToString("00");
            Show(failure, reducedMotion);
        }

        public void ShowBanked(int count, bool reducedMotion)
        {
            bankedCount.text = count + " REWARD TYPES BANKED";
            Show(banked, reducedMotion);
        }

        public void ShowHelp(bool reducedMotion)
        {
            SetReducedMotion(reducedMotion);
            Show(help, reducedMotion);
        }

        public void SetReducedMotion(bool reducedMotion)
        {
            motion.Label = "MOTION: " + (reducedMotion ? "REDUCED" : "FULL");
        }

        public void ShowCollection(bool reducedMotion)
        {
            Show(collection, reducedMotion);
            collectionView.Initialize(content, wallet, reducedMotion, Close);
        }

        void Show(PopupView panel, bool reducedMotion)
        {
            Close();
            active = panel;
            active.Open(reducedMotion);
        }

        public void Close()
        {
            if (active == null) return;
            active.Hide();
            active = null;
        }

        void RequestRestart() => RestartRequested?.Invoke();
        void RequestRevive() => ReviveRequested?.Invoke();
        void RequestMotionToggle() => MotionToggleRequested?.Invoke();

        void OnDestroy()
        {
            if (!initialized) return;
            retry.Clicked -= RequestRestart;
            runAgain.Clicked -= RequestRestart;
            revive.Clicked -= RequestRevive;
            motion.Clicked -= RequestMotionToggle;
            helpClose.Clicked -= Close;
        }

        void OnValidate() => ResolveReferences();

        void ResolveReferences()
        {
            if (help == null) help = Find<PopupView>("ui_popup_help");
            if (failure == null) failure = Find<PopupView>("ui_popup_failure");
            if (banked == null) banked = Find<PopupView>("ui_popup_banked");
            if (collection == null) collection = Find<PopupView>("ui_popup_collection");
            if (collectionView == null && collection != null)
                collectionView = collection.GetComponentInChildren<CollectionView>(true);
            if (failureZone == null) failureZone = Find<TMP_Text>("ui_text_failure_zone_value");
            if (bankedCount == null) bankedCount = Find<TMP_Text>("ui_text_cashout_total_value");
            if (retry == null) retry = Find<ActionButton>("ui_button_retry");
            if (revive == null) revive = Find<ActionButton>("ui_button_revive");
            if (runAgain == null) runAgain = Find<ActionButton>("ui_button_run_again");
            if (motion == null) motion = Find<ActionButton>("ui_button_motion");
            if (helpClose == null) helpClose = Find<ActionButton>("ui_button_rules_close");
        }

        T Find<T>(string objectName) where T : Component
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.GetComponent<T>();
            return null;
        }
    }
}
