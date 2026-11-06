using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace GTFO.EZ.AbsurdWeapons;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("GTFO.exe")]
[BepInDependency("dev.gtfomodding.gtfo-api")]
[BepInDependency(GtfoEzGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public const string GUID = "MindAttic.GTFO.EZ.AbsurdWeapons";
    public const string NAME = "GTFO.EZ.AbsurdWeapons";
    public const string VERSION = "21.0.0";

    // Optional: only used to order our datablock injection after it.
    internal const string GtfoEzGuid = "MindAttic.GTFO.EZ";

    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;

        // The master switch is the cfg's first entry ("0. Mod" sorts ahead of every other section), so
        // the mod can be turned off from its .cfg where a mod manager profile doesn't list it. When it's
        // off nothing else is bound, migrated, registered or patched: the other settings stay in the
        // file untouched, and the migrations still due run the next time the mod is switched on.
        var enabled = Config.Bind("0. Mod", "Enabled", true,
            "Turn the whole mod on or off. When false, the mod loads nothing: no patches, no weapons, no hotkeys.");
        if (!enabled.Value)
        {
            Logger.LogInfo($"{NAME} is switched off in its config ([0. Mod] Enabled) - not loading.");
            return;
        }

        WeaponSettings.Bind(Config);
        TentaclePool.Bind(Config);
        GearCycler.Bind(Config);
        InfiniteAmmo.Bind(Config);
        Stealth.Bind(Config);
        KatanaDash.Bind(Config);
        WarhammerSlam.Bind(Config);
        ImpactBurst.Bind(Config);
        Knockback.Bind(Config);
        MjolnirThrow.Bind(Config);
        GunInjector.Bind(Config);
        SeekerKind.Bind(Config);
        LaserGuns.Bind(Config);
        Railgun.Bind(Config);
        ChainLightning.Bind(Config);
        TentacleCannon.Bind(Config);
        GrenadeLauncher.Bind(Config);
        RocketLauncher.Bind(Config);
        ShrinkBeam.Bind(Config);
        Sounds.Bind(Config);
        HiveLocks.Bind(Config);
        VictoryBanner.Bind(Config);
        ConfigMigrations.Apply(Config); // after every Bind: stale old defaults move to the new ones
        ClassInjector.RegisterTypeInIl2Cpp<SeekerProjectile>();
        ClassInjector.RegisterTypeInIl2Cpp<BurstFlash>();
        ClassInjector.RegisterTypeInIl2Cpp<TentacleLash>();
        ClassInjector.RegisterTypeInIl2Cpp<LaserBeam>();
        ClassInjector.RegisterTypeInIl2Cpp<LauncherGrenade>();
        ClassInjector.RegisterTypeInIl2Cpp<RocketProjectile>();
        ClassInjector.RegisterTypeInIl2Cpp<FallingSpark>();
        ClassInjector.RegisterTypeInIl2Cpp<ShrinkStream>();
        ClassInjector.RegisterTypeInIl2Cpp<RailBeam>();
        ClassInjector.RegisterTypeInIl2Cpp<LightningBolt>();
        ClassInjector.RegisterTypeInIl2Cpp<ThrownHammer>();
        ClassInjector.RegisterTypeInIl2Cpp<BannerPylon>();
        ClassInjector.RegisterTypeInIl2Cpp<TentacleAmmoDriver>();
        ClassInjector.RegisterTypeInIl2Cpp<NetPresence>();
        NetPresence.Init();
        GearCycler.Init();
        SeekerGuns.Init();
        ImpactBursts.Init();
        Stealth.Init();
        TentacleCannon.Init();
        LaserGuns.Init();
        GrenadeLauncher.Init();
        RocketLauncher.Init();
        ShrinkBeam.Init();
        Railgun.Init();
        ChainLightning.Init();
        VictoryBanner.Init();
        Knockback.Init();
        new Harmony(GUID).PatchAll();
        AddComponent<StealthVisuals>();
        AddComponent<KnockbackSlides>();
        AddComponent<DashRunner>();
        AddComponent<WarhammerSlam>();
        AddComponent<GearCycler>();
        AddComponent<LaserDriver>();
        AddComponent<LoopSounds>();
        AddComponent<HiveLocks>();
        AddComponent<ChainLightningDriver>();
        AddComponent<ChainLightningGlow>();
        AddComponent<ShrinkBeamDriver>();
        AddComponent<ShrinkRegrowth>();
        AddComponent<TentacleAmmoDriver>();
        AddComponent<NetPresence>();
        MjolnirThrow.Init();
        Logger.LogInfo($"{NAME} v{VERSION} loaded.");
    }
}
