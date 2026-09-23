using TMPro;
using UnityEngine;
using Vertigo.Fortune.Content;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Presentation
{
    public sealed class FortuneScreen : MonoBehaviour
    {
        [Header("Views")]
        public WheelView Wheel;
        public RunLootView RunLoot;
        public GameDialogs Dialogs;
        public RewardRevealView RewardReveal;
        public BombRevealView BombReveal;

        [Header("Controls")]
        public ActionButton Spin;
        public ActionButton Hub;
        public ActionButton CashOut;
        public ActionButton Collection;
        public ActionButton Help;
        public ActionButton Sound;

        [Header("Run")]
        public TMP_Text WheelTitle;
        public TMP_Text ZoneNumber;
        public TMP_Text SpinHint;
        public TMP_Text NextZone;
        public TMP_Text ExtractionHint;
        public TMP_Text RunCount;
        public TMP_Text LootEmpty;
        public TMP_Text CheckpointTitle;
        public UnityEngine.UI.Image LootIcon;
        public UnityEngine.UI.Image CheckpointChest;
        public UnityEngine.UI.Image[] Progress;

        [Header("Route")]
        public TMP_Text[] ZoneLabels;
        public TMP_Text[] ZoneKinds;
        public UnityEngine.UI.Image[] ZonePanels;
        public UnityEngine.UI.Image[] ZoneFrames;

        [Header("Layout")]
        public RectTransform LootRows;
        public RectTransform ModalLayer;
        public RectTransform AmbientGlow;
        public TMP_FontAsset BodyFont;
        public TMP_FontAsset DisplayFont;

        public void Render(GameSession session, GameContent content, bool presentingReward,
            bool presentingBomb, bool audioMuted)
        {
            int zone = session.Zone;
            bool safe = session.Kind != ZoneKind.Bronze;
            bool golden = session.Kind == ZoneKind.Golden;

            WheelTitle.text = golden ? "GOLDEN" : safe ? "SILVER" : "BRONZE";
            WheelTitle.color = golden || !safe ? UiPalette.Gold : UiPalette.Teal;
            ZoneNumber.text = "ZONE " + zone.ToString("00");
            RunCount.text = session.Rewards.Count.ToString("00");
            SpinHint.color = UiPalette.Teal;
            SpinHint.text = "";

            RenderActions(session, presentingReward, presentingBomb);
            RenderCheckpoint(zone, safe, content);
            RenderRoute(zone, content);
            SetSoundMuted(audioMuted);
        }

        void RenderActions(GameSession session, bool presentingReward, bool presentingBomb)
        {
            bool pending = session.State == RunState.RewardPending;
            bool spinning = session.State == RunState.Spinning || presentingReward || presentingBomb;

            Spin.Interactable = !spinning && (session.State == RunState.Ready || pending);
            Hub.Interactable = !spinning && session.State == RunState.Ready;
            if (presentingBomb) Spin.Label = "BOMB";
            else if (presentingReward) Spin.Label = "COLLECTING...";
            else if (spinning) Spin.Label = "SPINNING...";
            else Spin.Label = pending ? "CONTINUE" : "SPIN";

            CashOut.Interactable = !spinning && session.CanCashOut;
            CashOut.Label = "EXTRACT";
            Collection.Interactable = !spinning;
            Help.Interactable = !spinning;
        }

        void RenderCheckpoint(int zone, bool safe, GameContent content)
        {
            int checkpoint = safe ? zone : ZoneRules.NextSafeZone(zone);
            bool golden = ZoneRules.KindFor(checkpoint) == ZoneKind.Golden;
            int remaining = ZoneRules.NextSafeZone(zone) - zone;

            NextZone.text = "ZONE " + checkpoint.ToString("00");
            CheckpointTitle.text = golden ? "SUPER ZONE" : "SAFE ZONE";
            CheckpointChest.sprite = content.GetSprite(golden
                ? "UI_icon_chest_super_nolight" : "UI_icon_chest_silver_nolight");
            ExtractionHint.text = safe ? "EXTRACTION OPEN"
                : remaining + " " + (remaining == 1 ? "ZONE" : "ZONES") + " AWAY";

            for (int index = 0; index < Progress.Length; index++)
                Progress[index].color = safe || index < zone % 5
                    ? UiPalette.Teal : UiPalette.Hex("274153");
        }

        void RenderRoute(int zone, GameContent content)
        {
            int first = ((zone - 1) / 10) * 10 + 1;
            for (int index = 0; index < ZoneLabels.Length; index++)
            {
                int number = first + index;
                bool current = number == zone;
                var kind = ZoneRules.KindFor(number);

                ZoneLabels[index].text = number.ToString("00");
                ZoneLabels[index].color = current ? UiPalette.Ink
                    : number < zone ? UiPalette.Muted : UiPalette.White;
                ZoneKinds[index].text = kind == ZoneKind.Golden ? "SUPER"
                    : kind == ZoneKind.Silver ? "SAFE" : "";
                ZoneKinds[index].color = current ? UiPalette.Teal
                    : kind == ZoneKind.Golden ? UiPalette.Gold : UiPalette.Muted;

                string sprite;
                if (current) sprite = "ui_card_panel_zone_current";
                else if (number < zone) sprite = "ui_card_panel_zone_white";
                else if (kind == ZoneKind.Golden) sprite = "ui_card_panel_zone_super";
                else if (kind == ZoneKind.Silver) sprite = "ui_card_panel_zone_current_white";
                else sprite = number == zone + 1 ? "ui_card_panel_zone_coming" : "ui_card_panel_zone_bg";

                ZonePanels[index].sprite = content.GetSprite(sprite);
                ZonePanels[index].color = current ? UiPalette.Teal
                    : kind == ZoneKind.Silver ? UiPalette.Hex("829CAF")
                    : kind == ZoneKind.Golden ? UiPalette.Gold : UiPalette.Hex("233B4C");
                ZoneFrames[index].sprite = content.GetSprite(current
                    ? "ui_card_zone_map_frame" : "ui_card_frame_4px_zone");
                ZoneFrames[index].color = current ? UiPalette.Teal : UiPalette.Hex("355164");
            }
        }

        public void SetSoundMuted(bool muted) => Sound.Label = muted ? "OFF" : "SFX";

        public void ResolveReferences()
        {
            if (Spin == null) Spin = Find<ActionButton>("ui_button_spin");
            if (Hub == null) Hub = Find<ActionButton>("ui_button_wheel_spin");
            if (CashOut == null) CashOut = Find<ActionButton>("ui_button_cashout");
            if (Collection == null) Collection = Find<ActionButton>("ui_button_collection");
            if (Help == null) Help = Find<ActionButton>("ui_button_help");
            if (Sound == null) Sound = Find<ActionButton>("ui_button_sound");
            if (Wheel == null) Wheel = GetComponentInChildren<WheelView>(true);
            if (RunLoot == null) RunLoot = GetComponentInChildren<RunLootView>(true);
            if (Dialogs == null) Dialogs = GetComponentInChildren<GameDialogs>(true);
            if (RewardReveal == null) RewardReveal = GetComponentInChildren<RewardRevealView>(true);
            if (BombReveal == null) BombReveal = GetComponentInChildren<BombRevealView>(true);
        }

        T Find<T>(string objectName) where T : Component
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.GetComponent<T>();
            return null;
        }

        void OnValidate() => ResolveReferences();
    }

    internal static class UiPalette
    {
        public static readonly Color Ink = Hex("09131F");
        public static readonly Color Panel = Hex("102333");
        public static readonly Color Muted = Hex("8096AB");
        public static readonly Color White = Hex("EFF7FF");
        public static readonly Color Teal = Hex("62E8CF");
        public static readonly Color Gold = Hex("F6BE63");
        public static readonly Color Red = Hex("FF6A73");

        public static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
