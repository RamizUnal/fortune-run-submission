using System;
using DG.Tweening;
using UnityEngine;
using Vertigo.Fortune.Content;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    public sealed class FortunePresenter : MonoBehaviour
    {
        public GameContent Content;
        public FortuneScreen Screen;

        IRewardWallet wallet;
        GameAudio audioPlayer;
        RunLootView lootView;
        bool reducedMotion;

        public GameSession Session { get; private set; }
        public bool HasModal => Screen != null && Screen.Dialogs != null && Screen.Dialogs.HasOpen;
        public bool IsPresentingReward => Screen != null && Screen.RewardReveal != null && Screen.RewardReveal.IsPlaying;
        public bool IsPresentingBomb => Screen != null && Screen.BombReveal != null && Screen.BombReveal.IsPlaying;
        public bool IsPresentingOutcome => IsPresentingReward || IsPresentingBomb;

        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);

            audioPlayer = GetComponent<GameAudio>();
            wallet = new LocalWallet();
            reducedMotion = PlayerPrefs.GetInt("fortune.reducedMotion", 0) == 1;
            Screen.ResolveReferences();

            foreach (var button in Screen.GetComponentsInChildren<ActionButton>(true))
                button.SetClickFeedback(audioPlayer.Click);

            Screen.Spin.Clicked += SpinOrAdvance;
            Screen.Hub.Clicked += SpinOrAdvance;
            Screen.CashOut.Clicked += Extract;
            Screen.Collection.Clicked += ShowCollection;
            Screen.Help.Clicked += ShowHelp;
            Screen.Sound.Clicked += ToggleSound;

            Screen.Dialogs.Initialize(Content, wallet, audioPlayer.Click);
            Screen.Dialogs.RestartRequested += Restart;
            Screen.Dialogs.ReviveRequested += Revive;
            Screen.Dialogs.MotionToggleRequested += ToggleMotion;
            Screen.Dialogs.SetReducedMotion(reducedMotion);

            lootView = Screen.RunLoot;
            lootView.Initialize(Screen, Content);
            Session = new GameSession(Content.CreateProvider(), new SystemRandomSource());
            Refresh(true);
            UpdateAmbientMotion();
        }

        public void SetSessionForVerification(GameSession session)
        {
            if (Screen.Wheel.IsAnimating || IsPresentingOutcome)
                throw new InvalidOperationException("Cannot replace a session during a spin or outcome animation.");

            CloseModal();
            Session = session;
            Refresh(true);
        }

        public void SpinOrAdvance()
        {
            if (HasModal || Screen.Wheel.IsAnimating || IsPresentingOutcome) return;

            if (Session.State == RunState.RewardPending)
            {
                Session.Advance();
                Refresh(true);
                ZoneTransition();
                return;
            }

            if (Session.State != RunState.Ready) return;

            int selected = Session.Spin();
            Refresh(false);
            audioPlayer.SpinStart();
            Screen.Wheel.Play(selected, Content.SpinDuration, reducedMotion, audioPlayer.Tick,
                () =>
                {
                    audioPlayer.SpinStop();
                    PresentOutcome(selected);
                });
        }

        void PresentOutcome(int selected)
        {
            var reward = Session.Slices[selected];
            if (reward.IsBomb)
            {
                PresentBomb(selected);
                return;
            }

            var source = Screen.Wheel.Icons[selected].rectTransform;
            var sourcePosition = RewardArtLayout.VisibleCenter(reward.Reward.AssetName, source);
            Screen.RewardReveal.Play(Content, reward, sourcePosition, reducedMotion,
                () =>
                {
                    audioPlayer.Fly();
                    return lootView.PrepareArrival(reward, reducedMotion);
                },
                () =>
                {
                    // Commit the reward only after its artwork reaches the loot tray.
                    Session.ResolveSpin();
                    lootView.CompleteArrival(Session.Rewards, reward.Reward.Id, reducedMotion);
                    audioPlayer.Land();
                    Refresh(false, false);
                },
                () => Refresh(false, false),
                () => audioPlayer.Reward(reward.Reward));
            Refresh(false, false);
        }

        void PresentBomb(int selected)
        {
            var source = Screen.Wheel.Icons[selected].rectTransform;
            var sourcePosition = RewardArtLayout.VisibleCenter("ui_card_icon_death", source);
            Screen.BombReveal.Play(sourcePosition, Screen.Wheel.ImpactMotion, reducedMotion,
                audioPlayer.BombArm,
                () =>
                {
                    // Keep the previous loot visible while the blast clears it.
                    Session.ResolveSpin();
                    audioPlayer.Bomb();
                    lootView.PlayLoss(reducedMotion, () => Screen.RunCount.text = "00");
                },
                audioPlayer.BombAftermath,
                () =>
                {
                    Refresh(false);
                    ShowFailure();
                });
            Refresh(false, false);
        }

        void ZoneTransition()
        {
            var motion = Screen.Wheel.ImpactMotion;
            motion.DOKill();
            motion.localScale = Vector3.one;
            if (reducedMotion) return;

            motion.localScale = Vector3.one * .94f;
            motion.DOScale(1, .38f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        void Refresh(bool newWheel, bool refreshLoot = true)
        {
            Screen.Render(Session, Content, IsPresentingReward, IsPresentingBomb, audioPlayer.Muted);
            if (refreshLoot) lootView.Refresh(Session.Rewards);
            if (newWheel) Screen.Wheel.SetContent(Content, Session.Zone, Session.Slices);
        }

        public void Extract()
        {
            if (!Session.CanCashOut || HasModal || IsPresentingOutcome) return;

            var rewards = Session.CashOut();
            wallet.Deposit(rewards, Session.Zone);
            Refresh(false);
            audioPlayer.Bank();
            Screen.Dialogs.ShowBanked(rewards.Count, reducedMotion);
        }

        void ShowFailure() => Screen.Dialogs.ShowFailure(Session.Zone, reducedMotion);

        public void Revive()
        {
            if (IsPresentingOutcome || Screen.Wheel.IsAnimating || Session.State != RunState.Failed) return;

            Session.Revive();
            audioPlayer.StopAll();
            CloseModal();
            Refresh(true);
            audioPlayer.Bank();
            ZoneTransition();
        }

        public void Restart()
        {
            if (IsPresentingOutcome || Session.State == RunState.Spinning) return;

            audioPlayer.StopAll();
            CloseModal();
            Session.Restart();
            Refresh(true);
            ZoneTransition();
        }

        void ToggleSound()
        {
            audioPlayer.Toggle();
            Screen.SetSoundMuted(audioPlayer.Muted);
        }

        void ToggleMotion()
        {
            reducedMotion = !reducedMotion;
            PlayerPrefs.SetInt("fortune.reducedMotion", reducedMotion ? 1 : 0);
            PlayerPrefs.Save();
            Screen.Dialogs.SetReducedMotion(reducedMotion);
            UpdateAmbientMotion();
        }

        void UpdateAmbientMotion()
        {
            Screen.AmbientGlow.DOKill();
            if (reducedMotion) return;

            Screen.AmbientGlow.DORotate(new Vector3(0, 0, 360), 50, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1);
        }

        public void ShowHelp()
        {
            if (HasModal || Session.State == RunState.Spinning || IsPresentingOutcome) return;
            Screen.Dialogs.ShowHelp(reducedMotion);
        }

        public void ShowCollection()
        {
            if (HasModal || Session.State == RunState.Spinning || IsPresentingOutcome) return;
            Screen.Dialogs.ShowCollection(reducedMotion);
        }

        public void CloseModal()
        {
            if (Screen != null && Screen.Dialogs != null) Screen.Dialogs.Close();
        }

        void OnDestroy()
        {
            if (Screen != null)
            {
                if (Screen.Spin != null) Screen.Spin.Clicked -= SpinOrAdvance;
                if (Screen.Hub != null) Screen.Hub.Clicked -= SpinOrAdvance;
                if (Screen.CashOut != null) Screen.CashOut.Clicked -= Extract;
                if (Screen.Collection != null) Screen.Collection.Clicked -= ShowCollection;
                if (Screen.Help != null) Screen.Help.Clicked -= ShowHelp;
                if (Screen.Sound != null) Screen.Sound.Clicked -= ToggleSound;

                if (Screen.Dialogs != null)
                {
                    Screen.Dialogs.RestartRequested -= Restart;
                    Screen.Dialogs.ReviveRequested -= Revive;
                    Screen.Dialogs.MotionToggleRequested -= ToggleMotion;
                }

                if (Screen.Wheel != null && Screen.Wheel.ImpactMotion != null)
                    Screen.Wheel.ImpactMotion.DOKill();
                if (Screen.AmbientGlow != null) Screen.AmbientGlow.DOKill();
            }

            CloseModal();
            DOTween.Kill(this);
        }
    }
}
