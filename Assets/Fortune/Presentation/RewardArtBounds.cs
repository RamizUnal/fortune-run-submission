using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vertigo.Fortune.Presentation
{
    // Measured from original source artwork with alpha > 12. Coordinates are normalized, top-left based.
    // Regenerate with Tools/generate_reward_art_bounds.py after replacing reward artwork.
    internal static class RewardArtBounds
    {
        internal static readonly IReadOnlyDictionary<string, Rect> All = new Dictionary<string, Rect>(StringComparer.Ordinal)
        {
            { "UI_icon_cash", new Rect(0.01829268f, 0.00000000f, 0.96341463f, 0.98255814f) },
            { "UI_icon_gold", new Rect(0.10750000f, 0.00888889f, 0.79000000f, 0.93333333f) },
            { "UI_icon_chest_small_noligt", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_icon_chest_Bronze_nolight", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_icon_chest_standart_nolight", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_icon_chest_big_nolight", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_icon_chest_silver_nolight", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_icon_chest_gold_nolight", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_icon_chest_super_nolight", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Armor_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Knife_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Pistol_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Pistol_Points_", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Rifle_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_SMG_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Shotgun_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Sniper_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Submachine_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icons_Vest_Points", new Rect(0.00000000f, 0.00000000f, 1.00000000f, 1.00000000f) },
            { "UI_Icon_Renders_tier1_shotgun", new Rect(0.11944444f, 0.01250000f, 0.75972222f, 0.97500000f) },
            { "UI_Icon_Renders_tier2_mle", new Rect(0.04722222f, 0.17916667f, 0.90555556f, 0.64166667f) },
            { "UI_Icon_Renders_tier2_rifle", new Rect(0.11666667f, 0.01250000f, 0.76666667f, 0.97500000f) },
            { "UI_Icon_Renders_tier3_shotgun", new Rect(0.00416667f, 0.19583333f, 0.99027778f, 0.60833333f) },
            { "UI_Icon_Renders_tier3_smg", new Rect(0.17638889f, 0.01666667f, 0.64722222f, 0.96666667f) },
            { "UI_Icon_Renders_tier3_sniper", new Rect(0.00416667f, 0.15416667f, 0.99166667f, 0.68750000f) },
            { "ui_icon_mle_bayonet_easter_time", new Rect(0.01388889f, 0.05833333f, 0.97361111f, 0.85833333f) },
            { "ui_icon_mle_bayonet_summer_vice", new Rect(0.01388889f, 0.05833333f, 0.97361111f, 0.85833333f) },
            { "ui_icon_aviator_glasses_easter", new Rect(0.28906250f, 0.41406250f, 0.42187500f, 0.18359375f) },
            { "ui_icon_baseball_cap_easter", new Rect(0.16796875f, 0.22851562f, 0.61914062f, 0.35156250f) },
            { "ui_icon_helmet_pumpkin", new Rect(0.16406250f, 0.08203125f, 0.67187500f, 0.74609375f) },
            { "ui_icon_render_cons_grenade_m26", new Rect(0.24609375f, 0.04101562f, 0.57031250f, 0.94335938f) },
            { "ui_icon_render_cons_grenade_m67", new Rect(0.24218750f, 0.04296875f, 0.56835938f, 0.94140625f) },
            { "ui_icon_render_cons_healthshot_2_neurostim", new Rect(0.44531250f, 0.03125000f, 0.11132812f, 0.93750000f) },
            { "ui_icon_render_cons_healthshot_2_regenerator", new Rect(0.44531250f, 0.03125000f, 0.11132812f, 0.93750000f) },
            { "ui_icon_render_t_cons_molotov", new Rect(0.37304688f, 0.06445312f, 0.24609375f, 0.89843750f) },
            { "ui_card_icon_death", new Rect(0.25195312f, 0.16601562f, 0.54687500f, 0.66406250f) },
        };
    }
}
