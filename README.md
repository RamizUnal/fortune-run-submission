# Fortune Run

A Unity wheel game with escalating rewards, safe zones, and a persistent collection.

## Download

The [latest release](https://github.com/RamizUnal/fortune-run-submission/releases/latest) contains the Android APK, gameplay video, and screenshots at 20:9, 16:9, and 4:3.

## Run

Open the project in **Unity 6000.6.0f1**, open `Assets/Fortune/Scenes/FortuneRun.unity`, and press **Play**.

Spin to earn rewards, Continue to advance, and Extract at a safe zone to bank the run's loot. Every fifth zone is safe; every thirtieth zone has a golden wheel. A bomb loses the current loot. Revive recovers it and continues to the next zone; the demo revive is always available. Collection shows banked rewards. The help menu includes reduced motion, and SFX toggles sound.

## Editing the UI

The scene uses `Assets/Fortune/Prefabs/FortuneScreen.prefab`. Open it in Prefab Mode to edit the main layout. Its popup panels, run loot, reward reveal, and bomb reveal are nested prefabs in `Assets/Fortune/Prefabs/UI`.

- Change panel size, spacing, fonts, and artwork in the prefab's RectTransforms and components.
- Edit `CollectionCard` to update every collection card, and `LootRow` to update every run loot row.
- The reward and bomb prefabs have position markers and animation settings in the Inspector. Animations run on child motion transforms.
- Button references are resolved in `OnValidate`. Actions and click sounds are connected in code; leave Inspector OnClick lists empty.

The collection reuses twelve cards, and run loot reuses four rows. Their content changes when the game state changes. The reward joins the run loot only after its flight finishes.

## Code and content

`FortunePresenter` coordinates the game session and the views. `FortuneScreen` updates the HUD; `GameDialogs` manages the four panels; `PopupView` handles their shared opening animation. Each view handles its own presentation. The domain has no Unity dependency. Wheel selection, randomness, and saving have interfaces so the game rules can be tested without a scene.

- `Assets/Fortune/Domain`: game rules and state.
- `Assets/Fortune/Content`: reward catalog, wheel configurations, and sprite atlas.
- `Assets/Fortune/Presentation`: UI behavior, animation, audio, and local saves.
- `Assets/Fortune/Prefabs`: editable screen and UI components.
- `Assets/Fortune/Editor`: content validation, UI checks, and build commands.
- `Assets/Fortune/Tests`: domain, content, and UI prefab tests.
- `demo_content`: original supplied artwork, retained for content validation.
- `Tools`: artwork bounds tool used when replacing reward images.

Edit reward names, rarity, images, and base amounts in `Assets/Fortune/Content/RewardCatalog.asset`. Edit wheel slots and progression pools in `BronzeWheel.asset`, `SilverWheel.asset`, and `GoldenWheel.asset`. Quantity is base amount × zone × slot multiplier × wheel multiplier. The 34 reward entries use the supplied art; both identical pistol-point images belong to one reward.

## Verify and build

Run **Window → General → Test Runner → EditMode → Run All**. Use **Fortune → Validate Content** and **Fortune → Validate UI** for content and scene checks.

Install Android Build Support, Android SDK & NDK Tools, and OpenJDK through Unity Hub. **Fortune → Build Android APK** writes `Builds/FortuneRun.apk`. **Fortune → Build macOS** creates a desktop player. Android requires an ARM64 device running Android 8.0 or later. Device performance has not been measured.

## Third-party assets

DOTween sequences the animations; its bundled notices are in `Assets/Plugins/Demigiant/DOTween`. TextMeshPro renders the UI text. Barlow font licenses are in `Assets/Fortune/Fonts/OFL.txt`. Kenney Particle Pack's CC0 notice is in `Assets/Fortune/Art/Effects`. Counter-Strike: Global Offensive sound effects are Valve assets, sourced from [sourcesounds/csgo](https://github.com/sourcesounds/csgo).
