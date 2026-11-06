using System;
using BepInEx.Configuration;
using GameData;
using HarmonyLib;
using Localization;
using UnityEngine;
using static GTFO.EZ.AbsurdWeapons.DataBlocks;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Adds this mod's guns. Each is a cloned archetype (stats), gear category (every fire mode
/// pointed at that archetype) and offline-gear entry (the model, which also decides the slot).
/// What they shoot is <see cref="SeekerGuns"/>' job; the datablocks only set ammo, rate of fire
/// and pellet count/spread.
/// <list type="bullet">
/// <item>Plasma Pistol (main) - Raptus Steigro, Doom-style blue plasma balls.</item>
/// <item>Plasma Rifle (special) - Omneco LRG, the same plasma, faster.</item>
/// <item>Hive Pistol (main) - Shelling S49, a Titanfall-style smart pistol.</item>
/// <item>Hive Rifle (special) - Malatack HXC, a smart rifle.</item>
/// <item>Laser Pistol (main) - Raptus Treffen 2, a continuous trip-mine laser beam.</item>
/// <item>Laser Rifle (special) - Accrat ND6 (forced into the special slot), the same beam.</item>
/// <item>Tentacle Cannon (special) - Buckland XDist2 (iron sights); every round is a striker tentacle.</item>
/// <item>Railgun (special) - Koning PR 11; a hitscan slug with infinite penetration (<see cref="Railgun"/>).</item>
/// <item>Chain Lightning Gun (special) - Omneco exp1; lightning that jumps enemy to enemy (<see cref="ChainLightning"/>).</item>
/// <item>Grenade Pistol (main) / Grenade Rifle (special) - Bataldo 3RB / TechMan Veruta XII; semi-auto lock-melter grenades (<see cref="GrenadeLauncher"/>).</item>
/// <item>Rocket Launcher (special) - Buckland XDist2; Quake 1's rocket, straight and gravity-free (<see cref="RocketLauncher"/>).</item>
/// <item>Shrink Beam (special) - Omneco exp1; a held ray that shrinks an enemy to nothing (<see cref="ShrinkBeam"/>).</item>
/// </list>
/// </summary>
[HarmonyPatch(typeof(GameDataInit), nameof(GameDataInit.Initialize))]
internal static class GunInjector
{
    // Vanilla archetypes run to the low hundreds at most; melee weapons here use 1001+.
    internal const uint PlasmaPistolId = 1101;
    internal const uint HivePistolId = 1102;
    internal const uint TentacleCannonId = 1103; // the Tentacle Cannon (once named the Tentacle Rifle)
    internal const uint HiveRifleId = 1104;
    internal const uint PlasmaRifleId = 1105;
    internal const uint LaserPistolId = 1106;
    internal const uint LaserRifleId = 1107;
    internal const uint RailgunId = 1108;
    internal const uint ChainLightningId = 1109;
    // 1110 was the Tentacle Pistol (removed); left unused so old loadouts never pick up a different gun.
    // 1111 was the Flamethrower (removed); left unused so old loadouts never pick up a different gun.
    // 1112 was the Flak Cannon (removed); left unused so old loadouts never pick up a different gun.
    // 1113 was the C-Foam Pistol (removed); left unused so old loadouts never pick up a different gun.
    // 1114 was the C-Foam Rifle (removed); left unused so old loadouts never pick up a different gun.
    internal const uint GrenadePistolId = 1115;
    internal const uint GrenadeRifleId = 1116;
    // 1117 was the Gravity Gun (removed); left unused so old loadouts never pick up a different gun.
    internal const uint RocketLauncherId = 1118;
    internal const uint ShrinkBeamId = 1119;
    // 1201 was the Grenade Launcher tool (now the Grenade Pistol/Rifle); left unused.
    internal const uint VictoryBannerId = 1202;   // a tool

    // The original guns' own stats (written straight into each gun's archetype, so 0 really is 0;
    // takes effect on restart - baked in at GameDataInit.Initialize, same as every other datablock here).
    internal static ConfigEntry<float> PlasmaDamage = null!, PlasmaShotDelay = null!;
    internal static ConfigEntry<int> PlasmaClipSize = null!;
    internal static ConfigEntry<float> HiveDamage = null!, HiveShotDelay = null!;
    internal static ConfigEntry<int> HiveClipSize = null!;
    internal static ConfigEntry<float> LaserDamage = null!, LaserShotDelay = null!;
    internal static ConfigEntry<int> LaserClipSize = null!;
    internal static ConfigEntry<float> RailgunDamage = null!, RailgunShotDelay = null!;
    internal static ConfigEntry<int> RailgunClipSize = null!;
    internal static ConfigEntry<float> ChainLightningDamage = null!, ChainLightningShotDelay = null!;
    internal static ConfigEntry<int> ChainLightningClipSize = null!;
    internal static ConfigEntry<float> TentacleDamage = null!, TentacleShotDelay = null!;
    internal static ConfigEntry<int> TentacleClipSize = null!;

    // Whether each individual weapon is added to the loadout screen at all (0 keeps nothing; this is
    // a straight on/off). Pistol/rifle pairs share one set of stats above but are toggled separately,
    // since they sit in different loadout slots.
    internal static ConfigEntry<bool> PlasmaPistolEnabled = null!, PlasmaRifleEnabled = null!;
    internal static ConfigEntry<bool> HivePistolEnabled = null!, HiveRifleEnabled = null!;
    internal static ConfigEntry<bool> LaserPistolEnabled = null!, LaserRifleEnabled = null!;
    internal static ConfigEntry<bool> TentacleCannonEnabled = null!;
    internal static ConfigEntry<bool> RailgunEnabled = null!;
    internal static ConfigEntry<bool> ChainLightningGunEnabled = null!;
    internal static ConfigEntry<bool> GrenadePistolEnabled = null!, GrenadeRifleEnabled = null!;
    internal static ConfigEntry<bool> RocketLauncherEnabled = null!;
    internal static ConfigEntry<bool> ShrinkBeamEnabled = null!;
    internal static ConfigEntry<bool> VictoryBannerEnabled = null!;

    internal static void Bind(ConfigFile config)
    {
        PlasmaDamage = config.Bind("Plasma Guns", "Damage", 22.5f, "Average damage per ball (it's actually rolled, 5 x 1d8).");
        PlasmaShotDelay = config.Bind("Plasma Guns", "ShotDelay", 3f / 35f, "Seconds between balls (Doom: one every 3 tics, ~11.7 a second).");
        PlasmaClipSize = config.Bind("Plasma Guns", "ClipSize", 100, "Magazine size.");
        PlasmaPistolEnabled = config.Bind("Plasma Guns", "Plasma Pistol Enabled", true, "Adds the Plasma Pistol (main slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        PlasmaRifleEnabled = config.Bind("Plasma Guns", "Plasma Rifle Enabled", true, "Adds the Plasma Rifle (special slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        HiveDamage = config.Bind("Hive Guns", "Damage", 1f, "Damage per round.");
        HiveShotDelay = config.Bind("Hive Guns", "ShotDelay", 0.09f, "Seconds between rounds.");
        HiveClipSize = config.Bind("Hive Guns", "ClipSize", 30, "Magazine size.");
        HivePistolEnabled = config.Bind("Hive Guns", "Hive Pistol Enabled", true, "Adds the Hive Pistol (main slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        HiveRifleEnabled = config.Bind("Hive Guns", "Hive Rifle Enabled", true, "Adds the Hive Rifle (special slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        LaserDamage = config.Bind("Laser Guns", "Damage", 2f, "Damage per tick (20 ticks a second).");
        LaserShotDelay = config.Bind("Laser Guns", "ShotDelay", 0.05f, "Seconds between ticks.");
        LaserClipSize = config.Bind("Laser Guns", "ClipSize", 100, "Magazine size.");
        LaserPistolEnabled = config.Bind("Laser Guns", "Laser Pistol Enabled", true, "Adds the Laser Pistol (main slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        LaserRifleEnabled = config.Bind("Laser Guns", "Laser Rifle Enabled", true, "Adds the Laser Rifle (special slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        RailgunDamage = config.Bind("Railgun", "Damage", 9999f, "Damage to every enemy on the line (kills anything outright).");
        RailgunShotDelay = config.Bind("Railgun", "ShotDelay", 1.5f, "Seconds between shots (Quake's 1.5 s).");
        RailgunClipSize = config.Bind("Railgun", "ClipSize", 5, "Magazine size.");
        RailgunEnabled = config.Bind("Railgun", "Railgun Enabled", true, "Adds the Railgun to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        ChainLightningDamage = config.Bind("Chain Lightning Gun", "Damage", 5f, "Damage to the first enemy per ShotDelay of held trigger (the stream strikes every 0.08 s, each strike this scaled to the tick; each jump then does JumpFalloff of the one before).");
        ChainLightningShotDelay = config.Bind("Chain Lightning Gun", "ShotDelay", 0.15f, "Seconds of held trigger that deal Damage once (it never fires shots: Damage / ShotDelay is its damage a second).");
        ChainLightningClipSize = config.Bind("Chain Lightning Gun", "ClipSize", 60, "Magazine size.");
        ChainLightningGunEnabled = config.Bind("Chain Lightning Gun", "Chain Lightning Gun Enabled", true, "Adds the Chain Lightning Gun to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        TentacleDamage = config.Bind("Tentacle Guns", "Damage", 25f, "Damage per Tentacle Cannon bite (the gun's only - enemies' tentacles are untouched).");
        TentacleShotDelay = config.Bind("Tentacle Guns", "ShotDelay", 0.235f, "Seconds between tentacles.");
        TentacleClipSize = config.Bind("Tentacle Guns", "ClipSize", 60, "Magazine size.");
        TentacleCannonEnabled = config.Bind("Tentacle Guns", "Tentacle Cannon Enabled", true, "Adds the Tentacle Cannon to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        GrenadePistolEnabled = config.Bind("Grenade Launcher", "Grenade Pistol Enabled", true, "Adds the Grenade Pistol (main slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        GrenadeRifleEnabled = config.Bind("Grenade Launcher", "Grenade Rifle Enabled", true, "Adds the Grenade Rifle (special slot) to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        RocketLauncherEnabled = config.Bind("Rocket Launcher", "Rocket Launcher Enabled", true, "Adds the Rocket Launcher to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        ShrinkBeamEnabled = config.Bind("Shrink Beam", "Shrink Beam Enabled", true, "Adds the Shrink Beam to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
        VictoryBannerEnabled = config.Bind("Victory Banner", "Victory Banner Enabled", true, "Adds the Victory Banner to the loadout screen (it stays registered either way, so lobbies agree on which weapons exist).");
    }

    // Every gun this mod adds.
    internal static bool IsOurs(uint id) => GunArchetypes.Contains(id);

    // The gun archetypes this mod registered (melee and the tool have none of these).
    private static readonly System.Collections.Generic.HashSet<uint> GunArchetypes = new();

    // After GTFO.EZ (if installed) has tuned the vanilla weapons, so ours start from its tuned
    // values and EZ - which keys its tuning on vanilla names and ids - never touches ours.
    [HarmonyPostfix]
    [HarmonyAfter(Plugin.GtfoEzGuid)]
    private static void Postfix()
    {
        // Each pistol/rifle pair shares one set of stats (built from the same source archetype), so
        // the pistol and the rifle behave identically; the model only decides the look and hold.
        const string plasma = "Rapid-fire blue plasma, straight out of Doom: no recoil, no homing, just a stream of energy that shocks whatever it hits.";
        TryAdd("Plasma Pistol", PlasmaPistolId, PlasmaPistolEnabled, (name, id) => AddPlasma(name, id, "Autopistol staggering", plasma)); // Raptus Steigro, main slot
        TryAdd("Plasma Rifle", PlasmaRifleId, PlasmaRifleEnabled, (name, id) => AddPlasma(name, id, "HELRifleSemi", plasma));           // Omneco LRG, special slot
        const string hive = "A smart gun: locks onto the enemies around your crosshair and curves a fast stream of rounds onto them.";
        TryAdd("Hive Pistol", HivePistolId, HivePistolEnabled, (name, id) => AddHive(name, id, "PistolSemi", hive));    // Shelling S49, main slot
        TryAdd("Hive Rifle", HiveRifleId, HiveRifleEnabled, (name, id) => AddHive(name, id, "RifleHeavyAuto", hive)); // Malatack HXC, special slot
        const string tentacle = "Fires the strikers' own tentacles, full-auto: every round is a tentacle that lashes out at an enemy near your crosshair and bites hard.";
        // Buckland XDist2: the biggest special-slot gun with no optic, held out low (AddFarLowHold) so the tentacles have room.
        TryAdd("Tentacle Cannon", TentacleCannonId, TentacleCannonEnabled, (name, id) => AddTentacle(name, id, "ShotgunRanged", AddFarLowHold(name, id), tentacle));
        const string laser = "A continuous laser beam, like a trip mine's: it burns whatever it's held on, out to 70 metres, for as long as you hold it there.";
        TryAdd("Laser Pistol", LaserPistolId, LaserPistolEnabled, (name, id) => AddLaser(name, id,
            "Pistolauto", // Raptus Treffen 2: full-auto pistol, no sight, main slot
            48, // held low (ItemFPSSettings "Pistol_Chunky", 10 cm down) so the beam is visible
            laser));
        TryAdd("Laser Rifle", LaserRifleId, LaserRifleEnabled, (name, id) => AddLaser(name, id, "SMGHeavyAuto", null, laser, forceSpecialSlot: true));   // Accrat ND6, no animated parts, forced into the special slot (it's a main-slot SMG in vanilla)
        TryAdd("Railgun", RailgunId, RailgunEnabled, AddRailgun);
        TryAdd("Chain Lightning Gun", ChainLightningId, ChainLightningGunEnabled, AddChainLightning);
        var grenade = $"A heavy semi-auto launcher: every shot is a lock melter rigged to blow - it goes off on the first enemy it hits, or bounces around and blows after {GrenadeLauncher.FuseSeconds.Value:0.##} seconds.";
        TryAdd("Grenade Pistol", GrenadePistolId, GrenadePistolEnabled, (name, id) => AddGrenadeGun(name, id, "RevolverHEL", grenade));    // Bataldo 3RB (heavy revolver), main slot
        TryAdd("Grenade Rifle", GrenadeRifleId, GrenadeRifleEnabled, (name, id) => AddGrenadeGun(name, id, "MachinegunAuto", grenade)); // TechMan Veruta XII (heavy machine gun), special slot
        TryAdd("Rocket Launcher", RocketLauncherId, RocketLauncherEnabled, AddRocketLauncher);
        TryAdd("Shrink Beam", ShrinkBeamId, ShrinkBeamEnabled, AddShrinkBeam);
        TryAdd("Victory Banner", VictoryBannerId, VictoryBannerEnabled, AddVictoryBanner);
    }

    // A tool: the C-Foam launcher's category and gear (Stalwart Flow G2) cloned under a new id -
    // the glob becomes a plantable banner (VictoryBanner) instead.
    private static void AddVictoryBanner(string name, uint id) => AddGlueTool(name, id,
        "Sets a banner down in front of you like a sentry; walk up to it and press Use to pick it back up. Pulses healing to every teammate standing in its ring.");

    // The C-Foam launcher's category and gear cloned under `id` with a new name and description.
    // Tools have no archetype - the launcher's own prefab drives its pressure and bursts - so only
    // those two blocks are added; what each glob becomes is the tool's runtime job.
    private static void AddGlueTool(string name, uint id, string description)
    {
        var sourceCategory = Lookup<GearCategoryDataBlock>("GlueGun");
        var sourceGear = Lookup<PlayerOfflineGearDataBlock>("Glue_distance");
        var gearJson = RewriteGearJson(sourceGear.GearJSON, sourceCategory.persistentID, id, DisplayName(name));
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons: couldn't find category {sourceCategory.persistentID} in '{sourceGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        var blockName = name.Replace(" ", "");
        var category = Clone(sourceCategory);
        category.persistentID = id;
        category.name = blockName;
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = description };

        var gear = Clone(sourceGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        AddedCategories.Add(id);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);
        Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons added the {name} (tool; category and gear id {id}).");
    }

    // A grenade launcher on the given (heavy) model: semi-auto, one round a shot, every round a
    // lock-melter grenade (GrenadeLauncher.Launch). Both models share one set of stats - the
    // Arid 5's, so a shot does the same from either - but each kicks like its own model (3RB / Veruta XII), and its
    // grenade leaves its own barrel (Barrel).
    private static void AddGrenadeGun(string name, uint id, string model, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        var archetype = StatsFrom("HighCalPistol");
        archetype.FireMode = eWeaponFireMode.Semi;
        archetype.ShotgunBulletCount = 1;
        archetype.ShotgunConeSize = 0;
        archetype.ShotgunBulletSpread = 0;
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.Damage = GrenadeLauncher.BlastDamage.Value; // the blast's center damage, for the loadout screen
        archetype.ShotDelay = GrenadeLauncher.GunShotDelay.Value;
        SetClipSize(archetype, GrenadeLauncher.GunClipSize.Value);
        archetype.ShellCasingSize = 0f; // grenades, not cartridges: no brass
        archetype.RecoilDataID = ModelSource(gear).Archetype.RecoilDataID; // the look of the shot: this model's own kick
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    private static void TryAdd(string name, uint id, ConfigEntry<bool> enabled, Action<string, uint> add)
    {
        try
        {
            // Registered even when disabled (G.AWPN-T236): every machine in a lobby then holds the
            // same blocks whatever its own config says; Enabled only keeps it out of the loadout.
            InLoadout[id] = () => enabled.Value;
            if (!enabled.Value)
                Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {name} disabled in the config - registered, but left out of the loadout.");
            // Already added by this mod (the game initialized its data again): nothing to do.
            if (AddedCategories.Contains(id) && GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)) return;
            if (GameDataBlockBase<ArchetypeDataBlock>.HasBlock(id)
                || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(id)
                || GameDataBlockBase<PlayerOfflineGearDataBlock>.HasBlock(id))
            {
                Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons: id {id} is already taken by another mod's datablock - not adding the {name}.");
                return;
            }

            add(name, id);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons failed to add the {name}: {e}");
        }
    }

    // A Doom plasma gun on the given model (gear block name): forced full-auto, no recoil. Damage is
    // rolled per ball (SeekerKind.Plasma, 5 x 1d8); 22.5 is that roll's average, for the loadout
    // screen. Stats come from the Omneco LRG for both plasma guns.
    private static void AddPlasma(string name, uint id, string model, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        gear = WithHold(gear, AddLowHold(name, id, gear));
        var archetype = StatsFrom("HELRifleSemi");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = PlasmaDamage.Value;
        archetype.ShotDelay = PlasmaShotDelay.Value;
        SetClipSize(archetype, PlasmaClipSize.Value);
        archetype.ShellCasingSize = 0f; // plasma, not bullets: no brass ejected
        archetype.HipFireSpread = 0f;   // extremely tight grouping: every ball goes exactly where the crosshair is
        archetype.AimSpread = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    // A Titanfall-style smart gun on the given model: forced full-auto, no recoil; each round
    // curves onto an enemy locked near the crosshair (SeekerKind.Hive; HiveLocks marks them). 1
    // damage a round: they never miss, so it takes a stream of them to kill anything. Stats come
    // from the Malatack HXC.
    private static void AddHive(string name, uint id, string model, string description)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>(model);
        var archetype = StatsFrom("RifleHeavyAuto");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = HiveDamage.Value;
        archetype.ShotDelay = HiveShotDelay.Value;
        SetClipSize(archetype, HiveClipSize.Value);
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    // A laser beam gun (LaserGuns) on the given model: forced full-auto at 20 ticks a second - every
    // tick burns 2 into whatever the beam is on - with no spread and no recoil.
    // Stats come from the Drekker DEL P1. `hold`, if given, replaces the model's first-person hold.
    // `forceSpecialSlot`: the Accrat ND6 (SMGHeavyAuto) is actually a main-slot SMG in vanilla - its
    // gear category's own BaseItem decides the loadout slot - so swap that BaseItem for the special
    // slot's one to put the Laser Rifle there despite the model it's borrowed for its look.
    private static void AddLaser(string name, uint id, string model, uint? hold, string description, bool forceSpecialSlot = false)
    {
        var source = Lookup<PlayerOfflineGearDataBlock>(model);
        var gear = WithHold(source, AddSteadyHold(name, id, source, hold));
        var archetype = StatsFrom("Precision_Rifle");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = LaserDamage.Value;
        archetype.ShotDelay = LaserShotDelay.Value;
        SetClipSize(archetype, LaserClipSize.Value);
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f; // a beam, not bullets: no brass ejected
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        var category = CategoryOf(gear);
        if (forceSpecialSlot)
        {
            // Both places the slot is read from: the category's BaseItem and the gear JSON's own
            // BaseItem component (3). Changing only the category left the gear claiming the main
            // slot while its category claimed special, and that mismatch could cost a whole slot
            // its weapon list in the loadout.
            var special = Lookup<ItemDataBlock>("GEAR_Bulletweapon_Special").persistentID;
            category = Clone(category);
            category.BaseItem = special;
            gear = Clone(gear);
            gear.GearJSON = SetGearComponent(gear.GearJSON, 3, special);
        }
        Register(name, id, archetype, category, gear, description);
    }

    // The Koning PR 11 sniper, semi-auto: every shot is one Railgun slug through everything in its
    // line. It keeps the sniper's scope, with no kick at all.
    private static void AddRailgun(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("SniperSemi");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Semi;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = RailgunDamage.Value;     // to every enemy on the line (sniper 40): slow but devastating, like Quake's rail
        archetype.ShotDelay = RailgunShotDelay.Value; // sniper 0.8
        SetClipSize(archetype, RailgunClipSize.Value);
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f;  // a slug thrown by magnets: no brass
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, category, gear,
            "A hitscan slug with infinite penetration: it goes through every enemy and every wall in its path.");
    }

    // The Omneco exp1 HEL machine gun, no recoil, no spread, no brass. It never fires a shot: while
    // the trigger is held ChainLightningDriver strikes every 0.08 s, each strike the archetype's
    // Damage (5) scaled by 0.08 / ShotDelay (0.15) - about 2.7, 33 a second - to its first enemy,
    // then 75% of the last on each of up to five jumps (ChainLightning).
    private static void AddChainLightning(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("HELGun");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = ChainLightningDamage.Value;
        archetype.ShotDelay = ChainLightningShotDelay.Value;
        SetClipSize(archetype, ChainLightningClipSize.Value);
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, category, gear,
            "A continuous stream of lightning while you hold the trigger, leaping from enemy to enemy, up to five jumps, weaker with each.");
    }

    // The Omneco exp1 (the Chain Lightning Gun's own model), full-auto, no recoil, no spread, no
    // brass: a continuous ray (ShrinkBeam) while the trigger is held, same as Chain
    // Lightning's own stream.
    private static void AddShrinkBeam(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("HELGun");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.Damage = 0f; // the loadout screen: the beam shrinks, it doesn't deal a number
        archetype.ShotDelay = ShrinkBeam.GunShotDelay.Value;
        SetClipSize(archetype, ShrinkBeam.GunClipSize.Value);
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f;
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, category, gear,
            "A continuous ray while you hold the trigger: whatever it's held on shrinks - smaller and slower by the same share - until there's nothing left of it.");
    }

    // The Buckland XDist2 (the Tentacle Cannon's own model), semi-auto: every shot is a rocket
    // (RocketLauncher) that flies dead straight - no drop, no bounce - to the first thing in its
    // way and blows up there, Quake 1's rocket launcher. Kept this model's own punchy kick, unlike
    // the kickless guns above - a rocket launcher should thump.
    private static void AddRocketLauncher(string name, uint id)
    {
        var gear = Lookup<PlayerOfflineGearDataBlock>("ShotgunRanged");
        var (category, archetype) = ModelSource(gear);
        archetype.FireMode = eWeaponFireMode.Semi;
        archetype.ShotgunBulletCount = 1;
        archetype.ShotgunConeSize = 0;
        archetype.ShotgunBulletSpread = 0;
        archetype.Damage = RocketLauncher.BlastDamage.Value; // the blast's center damage, for the loadout screen
        archetype.ShotDelay = RocketLauncher.GunShotDelay.Value;
        SetClipSize(archetype, RocketLauncher.GunClipSize.Value);
        archetype.HipFireSpread = 0f;
        archetype.AimSpread = 0f;
        archetype.ShellCasingSize = 0f; // a rocket, not a shell: no brass ejected
        Register(name, id, archetype, category, gear,
            "Quake 1's own rocket launcher: every shot is a rocket that flies dead straight out to the first thing in its way - no drop, no bounce - trailing sparks and dust, and blows it up in a blast that reaches everyone nearby.");
    }

    // A tentacle gun on the given model, forced full-auto: a tentacle (TentacleCannon) for every
    // round while the trigger is held - one "pellet" per shot, no cone. (Shotgun pellets carry no
    // owner in the hit data; SeekerGuns falls back to the gun's owner.) Stats come from the
    // Buckland XDist2. `hold`, if given, replaces the model's first-person hold.
    private static void AddTentacle(string name, uint id, string model, uint? hold, string description)
    {
        var gear = WithHold(Lookup<PlayerOfflineGearDataBlock>(model), hold);
        var archetype = StatsFrom("ShotgunRanged");
        archetype.FireMode = eWeaponFireMode.Auto;
        archetype.ShotgunBulletCount = 1;
        archetype.ShotgunConeSize = 0;
        archetype.ShotgunBulletSpread = 0;
        archetype.HipFireSpread = 0f; // the tentacles bring their own spread
        archetype.AimSpread = 0f;
        archetype.Damage = TentacleDamage.Value;       // per tentacle
        archetype.ShotDelay = TentacleShotDelay.Value; // ~4.3 tentacles a second by default, so each has time to live
        archetype.ShellCasingSize = 0f; // tentacles, not bullets: no brass ejected
        SetClipSize(archetype, TentacleClipSize.Value);
        archetype.RecoilDataID = AddKicklessRecoil(name, id, archetype.RecoilDataID);
        Register(name, id, archetype, CategoryOf(gear), gear, description);
    }

    // The laser guns' hold: the model's own (or `hold`), but never relaxed - so it sits at hip-fire
    // height the whole time, as a gun being fired does - and with no recoil animation. The lasers
    // never fire (their beam is trigger-driven), so this is what keeps them raised, kick-free.
    // Registered at the gun's id; returns the hold id to use (the source's if that id is taken).
    private static uint? AddSteadyHold(string name, uint id, PlayerOfflineGearDataBlock gear, uint? hold)
    {
        var sourceId = hold ?? GearJson.Component(gear.GearJSON, 4);
        var source = sourceId.HasValue ? GameDataBlockBase<ItemFPSSettingsDataBlock>.GetBlock(sourceId.Value) : null;
        if (source == null || GameDataBlockBase<ItemFPSSettingsDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons: couldn't give the {name} its steady hold - it keeps its model's.");
            return hold;
        }
        var steady = Clone(source);
        steady.persistentID = id;
        steady.name = name.Replace(" ", "") + "_Steady";
        steady.canRelax = false;    // always up at hip height
        steady.RecoilAnimation = 0; // no kick animation
        GameDataBlockBase<ItemFPSSettingsDataBlock>.AddBlock(steady, -1);
        return id;
    }

    // The model's own first-person hold carried lower and tipped up to the crosshair, so what it
    // fires (the plasma balls) shows above it on screen. Registered at the gun's id; null (keep the
    // model's) if that id is taken or the model has no hold.
    private static uint? AddLowHold(string name, uint id, PlayerOfflineGearDataBlock gear)
    {
        var sourceId = GearJson.Component(gear.GearJSON, 4);
        var source = sourceId.HasValue ? GameDataBlockBase<ItemFPSSettingsDataBlock>.GetBlock(sourceId.Value) : null;
        if (source == null || GameDataBlockBase<ItemFPSSettingsDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons: couldn't hold the {name} low - it keeps its model's hold.");
            return null;
        }
        var low = Clone(source);
        low.persistentID = id;
        low.name = name.Replace(" ", "") + "_Low";
        low.localPosHip += new Vector3(0.02f, -0.12f, 0f);
        low.localPosZoom += new Vector3(0.04f, -0.1f, 0f);
        low.localRotHip += new Vector3(-4f, 0f, 0f);
        low.localRotZoom += new Vector3(-3f, 0f, 0f);
        GameDataBlockBase<ItemFPSSettingsDataBlock>.AddBlock(low, -1);
        return id;
    }

    private static PlayerOfflineGearDataBlock WithHold(PlayerOfflineGearDataBlock gear, uint? hold)
    {
        if (!hold.HasValue) return gear;
        var held = Clone(gear);
        held.GearJSON = SetGearComponent(gear.GearJSON, 4, hold.Value);
        return held;
    }

    // The Tentacle Cannon's first-person hold (ItemFPSSettingsDataBlock): the XDist2's own
    // "Shotgun" pose - so the hands sit where a shotgun's do - carried very low like a minigun. Returns the id to use.
    private static uint AddFarLowHold(string name, uint id)
    {
        if (GameDataBlockBase<ItemFPSSettingsDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons: first-person hold id {id} is taken by another mod - the {name} keeps its model's hold.");
            return Lookup<ItemFPSSettingsDataBlock>("Shotgun").persistentID;
        }

        var hold = Clone(Lookup<ItemFPSSettingsDataBlock>("Shotgun"));
        hold.persistentID = id;
        hold.name = name.Replace(" ", "") + "_Hold";
        // Held very low at the waist like a minigun, tipped up so the barrel still points at the
        // crosshair; aiming only draws it in a little, it never comes up to the eye.
        hold.localPosHip = new Vector3(0.14f, -0.34f, 0.42f);   // shotgun (0.076, -0.085, 0.38): way down at the hip
        hold.localPosZoom = new Vector3(0.06f, -0.3f, 0.42f);   // shotgun (0, -0.154, 0.35): still low when aiming
        hold.localRotHip = new Vector3(-9f, -4f, 0f);           // tipped up (and in) toward the crosshair
        hold.localRotZoom = new Vector3(-8f, -2f, 0f);
        hold.ItemCameraFOVZoom = 34;                            // shotgun 23: barely any aim zoom
        GameDataBlockBase<ItemFPSSettingsDataBlock>.AddBlock(hold, -1);
        return id;
    }

    // No recoil at all: the auto pistol's profile (for its crosshair) with every kick, view-push
    // and shake weight zeroed. (The Arid 5's own, High_Cal_Pistol, is one of the game's heaviest -
    // a big rotational kick, double camera rotation and a hard screen shake every shot.)
    // Registered at the gun's id; returns the id to use.
    private static uint AddKicklessRecoil(string name, uint id, uint modelRecoil)
    {
        if (GameDataBlockBase<RecoilDataBlock>.HasBlock(id))
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons: recoil id {id} is taken by another mod - the {name} keeps its model's recoil.");
            return modelRecoil;
        }

        var recoil = Clone(Lookup<RecoilDataBlock>("PistolAuto"));
        recoil.persistentID = id;
        recoil.name = name.Replace(" ", "") + "_NoKick";
        // MinMaxValue is a shared reference type: fresh instances, so the auto pistol keeps its own.
        recoil.power = new MinMaxValue { Min = 0f, Max = 0f };
        recoil.horizontalScale = new MinMaxValue { Min = 0f, Max = 0f };
        recoil.verticalScale = new MinMaxValue { Min = 0f, Max = 0f };
        recoil.recoilRotImpulseWeight = 0f;
        recoil.recoilPosImpulseWeight = 0f;
        recoil.recoilPosShiftWeight = 0f;
        recoil.recoilCameraRotWeight = 0f;
        recoil.recoilCameraPosWeight = 0f;
        recoil.recoilAimingWeight = 0f;
        recoil.hipFireCrosshairRecoilPop = 0f;
        recoil.concussionIntensity = 0f;
        recoil.concussionDuration = 0f;

        GameDataBlockBase<RecoilDataBlock>.AddBlock(recoil, -1);
        return id;
    }

    private static (GearCategoryDataBlock Category, ArchetypeDataBlock Archetype) ModelSource(PlayerOfflineGearDataBlock gear)
    {
        var category = CategoryOf(gear);
        var archetypeId = FireModeOf(gear) switch
        {
            eWeaponFireMode.Burst => category.BurstArchetype,
            eWeaponFireMode.Auto => category.AutoArchetype,
            eWeaponFireMode.SemiBurst => category.SemiBurstArchetype,
            _ => category.SemiArchetype,
        };
        var archetype = GameDataBlockBase<ArchetypeDataBlock>.GetBlock(archetypeId)
            ?? throw new InvalidOperationException($"archetype {archetypeId} not found");
        return (category, Clone(archetype));
    }

    // A clone of the archetype the named gear fires with: a pair's shared stat source.
    private static ArchetypeDataBlock StatsFrom(string gearName) => ModelSource(Lookup<PlayerOfflineGearDataBlock>(gearName)).Archetype;

    // A new magazine size, with the cost of a round scaled to match: a full magazine takes the same
    // share of the slot's reserve as the source gun's did. Left at the source's cost, a gun copied
    // from a 5-round heavy weapon with a 60-100 round magazine spent its whole reserve on one fill.
    private static void SetClipSize(ArchetypeDataBlock archetype, int clipSize)
    {
        clipSize = Math.Max(1, clipSize);
        if (archetype.DefaultClipSize > 0)
            archetype.CostOfBullet *= (float)archetype.DefaultClipSize / clipSize;
        archetype.DefaultClipSize = clipSize;
    }

    private static GearCategoryDataBlock CategoryOf(PlayerOfflineGearDataBlock gear)
    {
        var categoryId = CategoryIdOf(gear) ?? throw new InvalidOperationException($"no category in '{gear.name}' gear JSON");
        return GameDataBlockBase<GearCategoryDataBlock>.GetBlock(categoryId)
            ?? throw new InvalidOperationException($"gear category {categoryId} not found");
    }

    private static void Register(string name, uint id, ArchetypeDataBlock archetype, GearCategoryDataBlock sourceCategory,
        PlayerOfflineGearDataBlock modelGear, string description)
    {
        var gearJson = RewriteGearJson(modelGear.GearJSON, sourceCategory.persistentID, id, DisplayName(name));
        if (gearJson == null)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons: couldn't find category {sourceCategory.persistentID} in '{modelGear.name}' gear JSON - not adding the {name}.");
            return;
        }

        // Block names are lookup keys; the display name (which may have spaces) goes in PublicName.
        var blockName = name.Replace(" ", "");
        archetype.persistentID = id;
        archetype.name = blockName;
        archetype.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };
        // No gun of ours charges up before firing or cools down after: a stat source like the Omneco
        // LRG HEL (the Plasma Pistol/Rifle's) carries a charge-up that would otherwise come along.
        archetype.SpecialChargetupTime = 0f;
        archetype.SpecialCooldownTime = 0f;

        var category = Clone(sourceCategory);
        category.persistentID = id;
        category.name = blockName;
        // Whichever fire mode the model's gear asks for, it gets this archetype.
        category.SemiArchetype = id;
        category.BurstArchetype = id;
        category.AutoArchetype = id;
        category.SemiBurstArchetype = id;
        category.PublicName = new LocalizedText { Id = 0, UntranslatedText = DisplayName(name) };
        category.Description = new LocalizedText { Id = 0, UntranslatedText = description };

        var gear = Clone(modelGear);
        gear.persistentID = id;
        gear.name = blockName;
        gear.GearJSON = gearJson;

        GameDataBlockBase<ArchetypeDataBlock>.AddBlock(archetype, -1);
        GameDataBlockBase<GearCategoryDataBlock>.AddBlock(category, -1);
        AddedCategories.Add(id);
        GunArchetypes.Add(id);
        GameDataBlockBase<PlayerOfflineGearDataBlock>.AddBlock(gear, -1);

        Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons added the {name} (archetype, category and gear id {id}).");
    }
}
