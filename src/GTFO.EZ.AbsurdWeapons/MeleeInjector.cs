using System;
using BepInEx.Configuration;
using GameData;
using HarmonyLib;
using Localization;
using static GTFO.EZ.AbsurdWeapons.DataBlocks;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Adds this mod's melee weapons as new selectable loadout entries. Each one is three new
/// datablocks registered right after the game loads its own: a melee archetype (stats + which
/// animation/sound set to use), a gear category pointing at it, and an offline-gear entry (what
/// appears in the loadout screen, and which model it carries). Nothing vanilla is modified.
/// <list type="bullet">
/// <item>Katana - the Mastaba Fixed Blade with a ~1.85 m blade, wielded like the bat; full-charge dash.</item>
/// <item>Mjolnir - the Santonian Mallet hammer model wielded exactly like the bat.</item>
/// <item>Warhammer - the Kovac Sledgehammer with a giant head: enormous hits and knockback.</item>
/// <item>Shadow Dagger - the Mastaba Fixed Blade as a knife; a fully charged hit makes you unseen for 3 s (StealthSeconds).</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
internal static class MeleeInjector
{
    // Fixed rather than "max existing + 1" so every player in a lobby agrees on them - gear syncs
    // by category id. Vanilla uses 1-4 for melee archetypes and two-digit ids for the rest. Each
    // weapon uses the same id for all three of its blocks.
    // 1001 was the Yari (once the Glaive; removed); left unused so old loadouts never pick up a different weapon.
    internal const uint KatanaId = 1002;
    internal const uint MjolnirId = 1003;
    // 1004 was the Rapier (removed); left unused so old loadouts never pick up a different weapon.
    internal const uint WarhammerId = 1005;
    internal const uint ShadowDaggerId = 1006;
    // 1007-1009 were the Wakizashi, Odachi and Tanto (removed); left unused for the same reason.
    // 1010 was the Gravity Hammer (removed); left unused for the same reason.

    // After GTFO.EZ (if installed) has tuned the vanilla weapons, so ours start from its tuned
    // values and EZ - which keys its tuning on vanilla names and ids - never touches ours.
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void Postfix()
    {
        TryAdd("Katana", KatanaId, WeaponSettings.Katana, AddKatana);
        TryAdd("Mjolnir", MjolnirId, WeaponSettings.Mjolnir, AddMjolnir);
        TryAdd("Warhammer", WarhammerId, WeaponSettings.Warhammer, AddWarhammer);
        TryAdd("Shadow Dagger", ShadowDaggerId, WeaponSettings.ShadowDagger, AddShadowDagger);
    }

    private static void TryAdd(string name, uint id, WeaponSettings settings, Action<string, uint> add)
    {
        try
        {
            // Registered even when disabled (G.AWPN-T236): every machine in a lobby then holds the
            // same blocks whatever its own config says; Enabled only keeps it out of the loadout.
            InLoadout[id] = () => settings.Enabled.Value;
            if (!settings.Enabled.Value)
                Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {name} disabled in the config - registered, but left out of the loadout.");
            // Already added by this mod (the game initialized its data again): nothing to do.
            if (AddedCategories.Contains(id) && GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)) return;
            if (GameDataBlockBase<MeleeArchetypeDataBlock>.HasBlock(id)
                || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)
                || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(id))
            {
                Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons: id {id} is already taken by another mod's datablock - not adding the {name}.");
                return;
            }

            add(name, id);

            var archetype = GameDataBlockBase<MeleeArchetypeDataBlock>.GetBlock(id);
            if (archetype == null) return;
            ApplyOverrides(archetype, settings);
            var light = SwordTempo.SpeedFor(id);
            var charged = SwordTempo.ChargedSpeedFor(id);
            if (light != 1f || charged != 1f) WithTempo(archetype, id, light, charged);
            SwordTempo.Baked(id, light, charged); // only once the timings really are at this pace
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons failed to add the {name}: {e}");
        }
    }

    // Every non-zero number in the weapon's config section replaces the built-in one.
    private static void ApplyOverrides(MeleeArchetypeDataBlock archetype, WeaponSettings settings)
    {
        static void Set(ConfigEntry<float> entry, Action<float> apply)
        {
            if (entry.Value > 0f) apply(entry.Value);
        }
        Set(settings.Reach, v => archetype.CameraDamageRayLength = v);
        Set(settings.LightAttackDamage, v => archetype.LightAttackDamage = v);
        Set(settings.ChargedAttackDamage, v => archetype.ChargedAttackDamage = v);
        Set(settings.LightStaggerMulti, v => archetype.LightStaggerMulti = v);
        Set(settings.ChargedStaggerMulti, v => archetype.ChargedStaggerMulti = v);
        Set(settings.ChargedEnvironmentMulti, v => archetype.ChargedEnvironmentMulti = v);
        Set(settings.AttackSphereRadius, v => archetype.AttackSphereRadius = v);
        Set(settings.ChargeSpeedMulti, v => archetype.PlayerRunSpeedMultiWhileCharging = v);
    }

    // The Mastaba Fixed Blade's blade is a separate part, so BladeStretcher sizes just the blade
    // (see WeaponSettings) and the grip stays knife-sized.
    // A katana with a ~1.85 m blade (a real one's is ~70 cm). The bat's wide swings, and it cuts through every
    // enemy in the arc. The full-charge dash is runtime (KatanaDash).
    private static void AddKatana(string name, uint id)
    {
        var bat = Lookup<MeleeArchetypeDataBlock>("Bat");
        var batCategory = Lookup<GearCategoryDataBlock>("MeleeBat");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var mastaba = Lookup<PlayerOfflineGearDataBlock>("Knife");

        var archetype = Clone(bat);
        archetype.LightAttackDamage = 4f;       // bat 3
        archetype.ChargedAttackDamage = 14f;    // bat 12
        archetype.CameraDamageRayLength = 3.5f; // bat 1.75; reaches the tip of its ~1.85 m blade
        archetype.CanHitMultipleEnemies = true;

        Register(name, id, archetype, Clone(batCategory), knifeCategory.persistentID, mastaba,
            "A Japanese long sword swung in wide two-handed arcs, cutting through everything in its path. Release a full charge to dash forward, slashing all you pass.");
    }

    private static void AddMjolnir(string name, uint id)
    {
        var bat = Lookup<MeleeArchetypeDataBlock>("Bat");
        var batCategory = Lookup<GearCategoryDataBlock>("MeleeBat");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var mallet = Lookup<PlayerOfflineGearDataBlock>("Mallet"); // Santonian Mallet - short-hafted, like the real thing

        // Everything but the model is the bat's: stats, swing, sounds, third-person movement.
        Register(name, id, Clone(bat), Clone(batCategory), hammerCategory.persistentID, mallet,
            "A short-hafted war hammer swung in fast, wide arcs. Handles like a bat, lands like thunder.");
    }

    private static void AddWarhammer(string name, uint id)
    {
        var hammer = Lookup<MeleeArchetypeDataBlock>("Hammer");
        var hammerCategory = Lookup<GearCategoryDataBlock>("MeleeSledgehammer");
        var sledgehammer = Lookup<PlayerOfflineGearDataBlock>("Sledgehammer"); // Kovac Sledgehammer; BladeStretcher gives it the giant head

        // The hammer's own swing and charge (full damage at 1.8 s). A fully charged blow kills
        // almost anything short of a tank; taps stay hammer-light.
        var archetype = Clone(hammer);
        archetype.ChargedAttackDamage = 500f; // vanilla hammer 20
        archetype.LightAttackDamage = 6f;     // vanilla hammer 3
        // Stagger makes an enemy reel: x55 charged (vanilla 1.3), x11 light (vanilla 1). The actual
        // shove - sliding enemies back - is runtime (ImpactBursts.Shockwave / Knockback).
        archetype.ChargedStaggerMulti = 55f;
        archetype.LightStaggerMulti = 11f;
        archetype.ChargedEnvironmentMulti = 10f; // doors and locks too
        archetype.CanHitMultipleEnemies = true; // the head is big enough to catch a crowd
        archetype.AttackSphereRadius = 0.6f;    // vanilla hammer 0.3
        archetype.CameraDamageRayLength = 2.4f; // vanilla hammer 1.8 - the giant head reaches further
        archetype.PlayerRunSpeedMultiWhileCharging = 0.5f; // lugging it at the ready slows you down

        Register(name, id, archetype, Clone(hammerCategory), hammerCategory.persistentID, sledgehammer,
            "A sledgehammer with a head the size of a crate. Every hit sends enemies reeling, and a full charge leaves nothing standing.");
    }

    private static void AddShadowDagger(string name, uint id)
    {
        var knife = Lookup<MeleeArchetypeDataBlock>("Knife");
        var knifeCategory = Lookup<GearCategoryDataBlock>("MeleeKnife");
        var mastaba = Lookup<PlayerOfflineGearDataBlock>("Knife"); // Mastaba Fixed Blade, normal size

        // The knife, with stabs 2.5x as fast (WeaponSettings); what it does on a hit is runtime (Stealth).
        var archetype = Clone(knife);
        Register(name, id, archetype, Clone(knifeCategory), knifeCategory.persistentID, mastaba,
            $"Strike from the shadows. A fully charged hit makes you vanish for {Stealth.Seconds.Value:0.##} seconds: no enemy can see, hear or chase you. Kills while hidden sharpen the blade.");
    }

    // The weapon gets its own copy of its animation set (id = the weapon's id) with the light
    // attacks' timings - hit, damage window, combo, length - divided by `light`, and the charged
    // swing's release and hit divided by `charged` (not the charge-up, which the full-charge checks
    // time). SwordTempo speeds the animation itself up to match.
    private static void WithTempo(MeleeArchetypeDataBlock archetype, uint id, float light, float charged)
    {
        var source = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet)
            ?? throw new InvalidOperationException($"melee animation set {archetype.MeleeAnimationSet} wasn't found");
        if (GameDataBlockBase<MeleeAnimationSetDataBlock>.HasBlock(id))
            throw new InvalidOperationException($"melee animation set id {id} is taken by another mod");
        var set = Clone(source);
        set.persistentID = id;
        set.name = archetype.name + "_Tempo";
        if (light != 1f)
        {
            set.FPAttackMissRight = Faster(set.FPAttackMissRight, light);
            set.FPAttackMissLeft = Faster(set.FPAttackMissLeft, light);
            set.FPAttackHitRight = Faster(set.FPAttackHitRight, light);
            set.FPAttackHitLeft = Faster(set.FPAttackHitLeft, light);
        }
        if (charged != 1f)
        {
            set.FPAttackChargeUpReleaseLeft = Faster(set.FPAttackChargeUpReleaseLeft, charged);
            set.FPAttackChargeUpReleaseRight = Faster(set.FPAttackChargeUpReleaseRight, charged);
            set.FPAttackChargeUpHitLeft = Faster(set.FPAttackChargeUpHitLeft, charged);
            set.FPAttackChargeUpHitRight = Faster(set.FPAttackChargeUpHitRight, charged);
        }
        GameDataBlockBase<MeleeAnimationSetDataBlock>.AddBlock(set, -1);
        archetype.MeleeAnimationSet = id;
    }

    private static MeleeAnimationSetDataBlock.MeleeAttackData Faster(MeleeAnimationSetDataBlock.MeleeAttackData data, float speed)
    {
        // Each timing is stored twice - in seconds (AttackHitTime, ...) and in frames
        // (m_attackHitFrameTime, ...) - and the native AttackHitFrameTime-style properties read and
        // write those same frame fields. So: scale the seconds and m_ frame fields, each once, from
        // the original values, and leave the native properties alone.
        var copy = CopyObject(data);
        var timings = new System.Collections.Generic.List<(System.Reflection.PropertyInfo Property, float Value)>();
        foreach (var property in typeof(MeleeAnimationSetDataBlock.MeleeAttackData).GetProperties())
        {
            if (property.PropertyType != typeof(float) || !property.CanWrite || !property.Name.EndsWith("Time")) continue;
            if (property.Name.EndsWith("FrameTime") && !property.Name.StartsWith("m_")) continue;
            timings.Add((property, (float)property.GetValue(copy)!));
        }
        foreach (var (property, value) in timings) property.SetValue(copy, Formulas.ScaledTiming(value, speed));
        return copy;
    }

    // Gives the cloned archetype and category the weapon's id and name, builds its loadout entry
    // from the model's gear (re-pointed at the new category), and registers all three.
    private static void Register(string name, uint id, MeleeArchetypeDataBlock archetype, GearCategoryDataBlock category,
        uint modelCategoryId, PlayerOfflineGearDataBlock modelGear, string description)
    {
        var gearJson = RewriteGearJson(modelGear.GearJSON, modelCategoryId, id, DisplayName(name));
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons: couldn't find category {modelCategoryId} in '{modelGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        // Block names are lookup keys; the display name (which may have spaces) goes in PublicName.
        var blockName = name.Replace(" ", "");
        archetype.persistentID = id;
        archetype.name = blockName;
        archetype.PublicName = DisplayName(name);

        category.persistentID = id;
        category.name = "Melee" + blockName;
        category.MeleeArchetype = id;
        // Fresh instances: the cloned PublicName/Description still point at the source weapon's,
        // and editing those would rename it too.
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = description };

        var gear = Clone(modelGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<MeleeArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        AddedCategories.Add(id);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons added the {name} (archetype, category and gear id {id}).");
    }
}
