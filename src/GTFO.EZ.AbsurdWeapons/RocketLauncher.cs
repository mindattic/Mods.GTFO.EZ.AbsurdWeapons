using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using FX_EffectSystem;
using GTFO.API;
using MindAttic.GTFO.Shared;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Rocket Launcher: Quake 1's own id Software code (<c>W_FireRocket</c> / <c>T_MissileTouch</c> /
/// <c>T_RadiusDamage</c>), with Doom's rocket's straight, gravity-free flight (<c>MT_ROCKET</c>'s
/// <c>MF_NOGRAVITY</c>) - neither game's rocket drops or bounces. Every shot (SeekerGuns' ray hook
/// calls <see cref="Launch"/>) leaves the barrel as an orange fireball and flies dead straight, kicking
/// off falling sparks and trailing dust from the muzzle on (the game's own concrete-impact puff, dense
/// near the gun, sparse further out) and glowing so it stays easy to track, to the first enemy, wall, teammate or bot (never the shooter)
/// in its way, then explodes there - same blast code as the Grenade Launcher (<see cref="AreaDamage"/>,
/// <see cref="Explosion"/>), plus the game's own <c>DamageUtil.DoExplosionDamage</c> so the blast
/// catches nearby players too - shooter, bots and teammates alike, Quake/Doom style. Quake's own
/// numbers: 120-damage splash with a half-distance falloff (<c>T_RadiusDamage</c>),
/// searched out to 160 units - zero past that, a third of the center damage left at the edge (the
/// same 160 u / a-third the Grenade Launcher's blast already uses) - and a 0.8 s refire. The gun's
/// own firing - sound, recoil, ammo (a magazine and reloads, like any gun) - stays vanilla. The shooter's
/// game deals the damage, through the bullet path so it syncs; everyone else is sent the launch and
/// the blast and sees the same rocket.
/// It can't aim down sights: right-click instead detonates the shooter's oldest rocket still in
/// flight, right where it is - one per click, so a volley can be set off as a chain
/// (<see cref="RocketProjectile"/>).
/// </summary>
[HarmonyPatch]
internal static class RocketLauncher
{
    internal static ConfigEntry<float> Speed = null!;           // launch speed, m/s
    internal static ConfigEntry<float> Lifetime = null!;        // seconds before an unexploded rocket just disappears
    internal static ConfigEntry<float> BlastRadius = null!;
    internal static ConfigEntry<float> BlastDamage = null!;     // at the blast's center, falling to BlastEdgeFraction at the edge
    internal static ConfigEntry<float> BlastEdgeFraction = null!;
    internal static ConfigEntry<float> BlastStagger = null!;
    internal static ConfigEntry<float> GunShotDelay = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    private const string LaunchEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.RocketLaunch.v2";
    private const string LegacyLaunchEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.RocketLaunch";
    // Unchanged since before v2 (its layout didn't change), so every version still shares it.
    private const string DetonateEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.RocketDetonate";

    internal static void Bind(ConfigFile config)
    {
        Speed = config.Bind("Rocket Launcher", "LaunchSpeed", 32.7f, "Launch speed along the aim, m/s (Quake 1: 1000 units/s).");
        Lifetime = config.Bind("Rocket Launcher", "Lifetime", 10f, "Seconds a rocket flies before it disappears unexploded if it never hits anything (Quake 1: 10 s auto-remove).");
        BlastRadius = config.Bind("Rocket Launcher", "BlastRadius", 5.2f, "Metres the blast reaches (Quake 1: its 120-damage, half-distance falloff is searched out to 160 units).");
        BlastDamage = config.Bind("Rocket Launcher", "BlastDamage", 110f, "Damage at the blast's center (Quake 1: 110 on a direct hit, 120 at the splash's own center).");
        BlastEdgeFraction = config.Bind("Rocket Launcher", "BlastEdgeFraction", 0.33f, "Share of damage left at the blast's edge (Quake 1's falloff leaves a third at 160 units - same as the Grenade Launcher's).");
        BlastStagger = config.Bind("Rocket Launcher", "BlastStagger", 5f, "Blast stagger multiplier.");
        GunShotDelay = config.Bind("Rocket Launcher", "GunShotDelay", 0.8f, "Seconds between shots (semi-auto; Quake 1: 0.8 s).");
        GunClipSize = config.Bind("Rocket Launcher", "GunClipSize", 5, "Magazine size.");
    }

    internal static void Init()
    {
        // Teammates' copies fly at the shooter's own speed and last the shooter's own lifetime.
        Wire.Register<LaunchMessage>(LaunchEvent, (_, message) =>
            RocketProjectile.Spawn(message.Id, null, new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.DirX, message.DirY, message.DirZ), message.Speed, message.Lifetime));
        // An older version's launch carries no numbers: this game's own are the best guess.
        Wire.RegisterLegacy<LegacyLaunchMessage>(LegacyLaunchEvent, (_, message) =>
            RocketProjectile.Spawn(message.Id, null, new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.DirX, message.DirY, message.DirZ), Speed.Value, Lifetime.Value));
        RemoteEvents.Register<DetonateMessage>(DetonateEvent, (_, message) =>
            RocketProjectile.Detonate(message.Id, new Vector3(message.X, message.Y, message.Z)));
    }

    private static uint nextId;

    // One shot from the local shooter's Rocket Launcher: a rocket out of the muzzle, straight along
    // the aim to whatever the crosshair is on - no lob, no gravity - shown to teammates too.
    internal static void Launch(PlayerAgent owner, Vector3 position, Vector3 aim, uint gearCategoryId)
    {
        var direction = aim.normalized;
        var id = ((uint)(owner.Owner?.PlayerSlotIndex() ?? 0) << 24) | (++nextId & 0xFFFFFF);
        RocketProjectile.Spawn(id, owner, position, direction, Speed.Value, Lifetime.Value, gearCategoryId);
        Wire.Send(LaunchEvent, new LaunchMessage(id, position, direction, Speed.Value, Lifetime.Value),
            LegacyLaunchEvent, new LegacyLaunchMessage(id, position, direction));
    }

    // No aiming down sights: right-click is the remote detonator instead. The first-person holder's
    // aim-input step is skipped while it holds the Rocket Launcher. Not ItemEquippable.AimingAllowed:
    // that getter is a one-line body il2cpp folds together with other identical getters, so detouring
    // it also catches unrelated objects' calls (a NullReferenceException in the trampoline at startup).
    [HarmonyPatch(typeof(FirstPersonItemHolder), "UpdateAimInput")]
    [HarmonyPrefix]
    private static bool NoAiming(FirstPersonItemHolder __instance)
    {
        try
        {
            if (!IsRocketLauncher(__instance.WieldedItem)) return true;
            __instance.ItemAimTrigger = false;
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    // Right-click this frame while the local player holds the Rocket Launcher.
    // Only the local player's own rockets: the host's bots are locally owned too, and the host's
    // right-click mustn't set theirs off.
    internal static bool DetonatePressed(PlayerAgent owner) =>
        owner.IsLocallyOwned && PlayerManager.GetLocalPlayerAgent() is { } local && local.Pointer == owner.Pointer
        && FocusStateManager.CurrentState == eFocusState.FPS
        && IsRocketLauncher(owner.Inventory?.WieldedItem)
        && InputMapper.GetButtonDownKeyMouse(InputAction.Aim, eFocusState.FPS);

    // False for anything missing, half-built or destroyed rather than throwing.
    private static bool IsRocketLauncher(ItemEquippable? item)
    {
        if (item == null || item.Pointer == IntPtr.Zero) return false;
        try
        {
            return item.GearCategoryData?.persistentID == GunInjector.RocketLauncherId;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static void SendDetonate(uint id, Vector3 position) =>
        NetworkAPI.InvokeEvent(DetonateEvent, new DetonateMessage(id, position));

    // Plain fields so GTFO-API can copy them as raw bytes.
    private readonly struct LaunchMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z, DirX, DirY, DirZ;
        public readonly float Speed, Lifetime; // the shooter's, so every copy flies alike

        public LaunchMessage(uint id, Vector3 position, Vector3 direction, float speed, float lifetime)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
            (DirX, DirY, DirZ) = (direction.x, direction.y, direction.z);
            (Speed, Lifetime) = (speed, lifetime);
        }
    }

    // The pre-v2 layout, unchanged, for games on an older version.
    private readonly struct LegacyLaunchMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z, DirX, DirY, DirZ;

        public LegacyLaunchMessage(uint id, Vector3 position, Vector3 direction)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
            (DirX, DirY, DirZ) = (direction.x, direction.y, direction.z);
        }
    }

    private readonly struct DetonateMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z;

        public DetonateMessage(uint id, Vector3 position)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
        }
    }
}

/// <summary>
/// One rocket in flight: an orange-glowing fireball (no visible missile), flown by hand in a dead straight line (no
/// gravity, no bounce - unlike <see cref="LauncherGrenade"/>) until it touches an enemy or the
/// world, kicking off a falling spark every few centimetres. Only the shooter's own copy decides when it
/// hits something (through the same enemy/world sphere cast its blast then uses); a teammate's copy
/// is purely visual and flies on - rockets never bounce, so there's nothing for it to simulate -
/// until the shooter's detonation message (<see cref="RocketLauncher.SendDetonate"/>) snaps it to
/// the real impact point and shows the same blast.
/// </summary>
internal sealed class RocketProjectile : MonoBehaviour
{
    private const float Radius = 0.15f;
    private const float SparkInterval = 0.05f;
    // The dust trail starts at the muzzle: a puff every DustNearSpacing metres for the first
    // DustDenseRange metres, so it reads as one continuous trail out of the gun, then thinning out
    // until by DustRampEnd it's the old sparse look - a puff every DustInterval seconds of flight,
    // DustBehind metres back. Sparse and well behind it out there: thick dust hid the rocket, and
    // you need to see it to time a right-click detonation.
    private const float DustInterval = 0.4f;
    private const float DustBehind = 1.5f;
    private const float DustNearSpacing = 0.75f;
    private const float DustDenseRange = 6f;
    private const float DustRampEnd = 20f;
    private const int MaxDustPerFrame = 8;
    private static readonly Color GlowColor = new(1f, 0.45f, 0.15f);
    private static readonly Color FlameColor = new(1f, 0.45f, 0.15f, 0.55f); // the fireball's outer flame, see-through
    private static readonly Color CoreColor = new(1f, 0.85f, 0.45f);         // its white-hot heart
    private const float FlameSize = 0.32f, CoreSize = 0.18f;
    private static Material? flameMaterial, coreMaterial;
    private static int HitMask => LayerManager.MASK_ENEMY_DAMAGABLE | LayerManager.MASK_WORLD;

    private static readonly Dictionary<uint, RocketProjectile> live = new();

    private uint id, gearCategoryId;
    private PlayerAgent? owner; // null on a teammate's (visual-only) copy
    private Vector3 direction, origin;
    private float speed, lifetime; // the shooter's, on every copy
    private float age, nextSpark, nextDustDistance, launchedAt, flickerSeed;
    private Transform? flame, core;
    private EffectLight? glow;
    private static int detonatedFrame = -1; // a right-click detonates one rocket, whichever runs first that frame
    private bool exploded;

    public RocketProjectile(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(uint id, PlayerAgent? owner, Vector3 position, Vector3 direction, float speed, float lifetime, uint gearCategoryId = 0)
    {
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Rocket");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        var rocket = go.AddComponent<RocketProjectile>();
        rocket.BuildFireball(go.transform);

        // The fireball lights its surroundings orange, so it can be tracked in flight (and in the dark).
        var glow = go.AddComponent<EffectLight>();
        glow.Color = GlowColor;
        glow.Range = 3f;
        glow.Intensity = 2.5f;
        glow.enabled = true;

        rocket.glow = glow;
        rocket.id = id;
        rocket.gearCategoryId = gearCategoryId;
        rocket.owner = owner;
        rocket.direction = direction.normalized;
        rocket.origin = position;
        rocket.speed = speed;
        rocket.lifetime = lifetime;
        rocket.launchedAt = Time.time;
        rocket.flickerSeed = UnityEngine.Random.Range(0f, 100f);

        live[id] = rocket;
    }

    // A teammate's detonation message: blow that rocket up where the shooter's game says it hit.
    [HideFromIl2Cpp]
    internal static void Detonate(uint id, Vector3 position)
    {
        if (live.TryGetValue(id, out var rocket) && rocket != null)
        {
            rocket.transform.position = position;
            rocket.Explode();
        }
        else
        {
            Explosion.Show(position); // it never arrived here; still show the blast
        }
    }

    private void Update()
    {
        if (exploded) return;
        try
        {
            var dt = Time.deltaTime;
            age += dt;
            if (age >= lifetime) { Remove(); return; } // Quake 1: flies 10 s, then just disappears

            Spark();
            Dust();
            Flicker();

            var position = transform.position;

            // Right-click: the shooter's oldest rocket in flight stops dead and blows up right
            // where it is - one per click.
            if (owner != null && detonatedFrame != Time.frameCount && RocketLauncher.DetonatePressed(owner))
            {
                detonatedFrame = Time.frameCount;
                var oldest = Oldest(owner);
                if (oldest != null)
                {
                    RocketLauncher.SendDetonate(oldest.id, oldest.transform.position);
                    oldest.Explode();
                    if (oldest == this) return;
                }
            }

            var step = speed * dt;

            // Only the shooter's own copy ever decides it hit something; a teammate's copy has
            // nothing to simulate (no bounce, no gravity) and just waits for Detonate above.
            // An enemy, the world, or a teammate or bot (never the shooter) - whichever is nearest.
            if (owner != null)
            {
                var hitSomething = Physics.SphereCast(position, Radius, direction, out var hit, step, HitMask);
                if (ProjectileHits.OtherPlayer(position, Radius, direction, step, owner, out var playerHit)
                    && (!hitSomething || playerHit.distance < hit.distance))
                {
                    hit = playerHit;
                    hitSomething = true;
                }
                if (hitSomething)
                {
                    // The rocket's own centre at contact, just off the surface: a blast centred exactly
                    // on a wall or door can see (and hurt) what's behind it.
                    var at = hit.point + hit.normal * Radius;
                    transform.position = at;
                    RocketLauncher.SendDetonate(id, at);
                    Explode();
                    return;
                }
            }

            transform.position = position + direction * step;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons rocket failed: {e.Message}");
            Remove();
        }
    }

    // The shooter's rocket launched longest ago that's still flying.
    [HideFromIl2Cpp]
    private static RocketProjectile? Oldest(PlayerAgent owner)
    {
        RocketProjectile? oldest = null;
        foreach (var rocket in live.Values)
        {
            if (rocket == null || rocket.exploded || rocket.owner == null || rocket.owner.Pointer != owner.Pointer) continue;
            if (oldest == null || rocket.launchedAt < oldest.launchedAt) oldest = rocket;
        }
        return oldest;
    }

    // No smoke trail - just a spray of sparks every few centimetres of flight, kicked out behind
    // and to the sides, that fall away under gravity as the rocket keeps going.
    [HideFromIl2Cpp]
    private void Spark()
    {
        if (age < nextSpark) return;
        nextSpark = age + SparkInterval;
        var position = transform.position;
        var count = UnityEngine.Random.Range(1, 3);
        for (var i = 0; i < count; i++)
        {
            var scatter = UnityEngine.Random.insideUnitSphere * 2f;
            var velocity = -direction * UnityEngine.Random.Range(1f, 3f) + scatter;
            FallingSpark.Spawn(position, velocity);
        }
    }

    // Dust behind it - the game's own concrete-impact puff, no decal (there's nothing to mark, it's
    // not hitting anything), just the cloud of debris that effect already throws up. Spaced by
    // distance flown, from the muzzle on (the first puff on launch): dense near the gun, the old
    // sparse spacing further out. Puffs land at their own spot on the flight line, so a slow frame
    // that covers several of them still leaves no gap, and never behind the muzzle.
    [HideFromIl2Cpp]
    private void Dust()
    {
        var traveled = Vector3.Distance(origin, transform.position);
        for (var i = 0; i < MaxDustPerFrame && traveled >= nextDustDistance; i++)
        {
            var at = nextDustDistance;
            nextDustDistance = at + DustSpacing(at);
            var back = Mathf.Min(DustBehind, at);
            try
            {
                FX_Manager.PlayEffect(false, FX_GroupName.Impact_Concrete, origin + direction * (at - back),
                    Quaternion.LookRotation(-direction), false);
            }
            catch (Exception)
            {
                return; // dust is a nicety; the sparks and the fireball still show
            }
        }
        if (traveled >= nextDustDistance) nextDustDistance = traveled; // fell behind: pick up from here, don't burst
    }

    // Metres to the next puff, from the dust trail's distance so far.
    [HideFromIl2Cpp]
    private float DustSpacing(float traveled)
    {
        var far = speed * DustInterval;
        if (traveled <= DustDenseRange || far <= DustNearSpacing) return DustNearSpacing;
        return Mathf.Lerp(DustNearSpacing, far, (traveled - DustDenseRange) / (DustRampEnd - DustDenseRange));
    }

    // The fireball churns: both shells pulse and the light wavers, out of step from rocket to rocket.
    [HideFromIl2Cpp]
    private void Flicker()
    {
        var t = (age + flickerSeed) * 14f;
        var a = Mathf.PerlinNoise(t, 0.3f);
        var b = Mathf.PerlinNoise(0.7f, t * 1.3f);
        if (flame != null) flame.localScale = Vector3.one * (FlameSize * (0.85f + 0.3f * a));
        if (core != null) core.localScale = Vector3.one * (CoreSize * (0.85f + 0.3f * b));
        if (glow != null) glow.Intensity = 2.5f * (0.8f + 0.4f * a);
    }

    [HideFromIl2Cpp]
    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        live.Remove(id);
        var position = transform.position;
        Destroy(gameObject);

        Explosion.Show(position);
        if (owner == null) return; // a teammate's copy: visual only, no damage
        AreaDamage.Apply(position, RocketLauncher.BlastRadius.Value, RocketLauncher.BlastDamage.Value, RocketLauncher.BlastEdgeFraction.Value,
            RocketLauncher.BlastStagger.Value, owner.Cast<Agents.Agent>(), null, gearCategoryId);

        // Quake/Doom rockets hurt whoever's standing in the blast - shooter, bots and teammates
        // alike - not just enemies: the game's own explosion-damage sweep (MASK_EXPLOSION_TARGETS is
        // players and destructibles, not enemies - those are the AreaDamage.Apply above, with their
        // own falloff), blocked by world geometry. The shooter's game alone runs it, and that reaches
        // every player, not just the ones this game owns: a player's ExplosionDamage is the synced
        // damage base's (Dam_PlayerDamageBase/Local don't override it), which sends the explosion-
        // damage packet the player's own game then applies (ReceiveExplosionDamage) - the way a
        // bullet's friendly fire lands. Running it on every game would hurt everyone once per game in
        // the lobby (checked for G.AWPN-T235).
        try
        {
            DamageUtil.DoExplosionDamage(position, RocketLauncher.BlastRadius.Value, RocketLauncher.BlastDamage.Value,
                LayerManager.MASK_EXPLOSION_TARGETS, LayerManager.MASK_EXPLOSION_BLOCKERS, false, 0f);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons rocket couldn't damage nearby players: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private void Remove()
    {
        live.Remove(id);
        Destroy(gameObject);
    }

    // A fireball, not a missile: a see-through orange flame shell around a white-hot core, both
    // unlit (Sprites/Default, like the other mods' glowing bits) so they burn bright whatever the
    // lighting, with no shadows - fire doesn't cast them.
    [HideFromIl2Cpp]
    private void BuildFireball(Transform parent)
    {
        if (flameMaterial == null) flameMaterial = new Material(Shader.Find("Sprites/Default")) { color = FlameColor };
        if (coreMaterial == null) coreMaterial = new Material(Shader.Find("Sprites/Default")) { color = CoreColor };
        flame = FireballShell(parent, FlameSize, flameMaterial);
        core = FireballShell(parent, CoreSize, coreMaterial);
    }

    [HideFromIl2Cpp]
    private static Transform FireballShell(Transform parent, float size, Material material)
    {
        var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.Destroy(shell.GetComponent<Collider>());
        shell.transform.SetParent(parent, false);
        shell.transform.localScale = Vector3.one * size;
        var renderer = shell.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return shell.transform;
    }
}

/// <summary>
/// One ember kicked off a flying rocket: a tiny bright point that falls under gravity, dimming out
/// over its short life - no trail, just a shower of sparks peeling away behind the rocket.
/// </summary>
internal sealed class FallingSpark : MonoBehaviour
{
    private const float Lifetime = 0.5f;
    private const float Gravity = 9.8f;
    private static readonly Color SparkColor = new(1f, 0.6f, 0.2f);

    private Vector3 velocity;
    private float age;
    private EffectLight light = null!;

    public FallingSpark(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 position, Vector3 velocity)
    {
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Rocket Spark");
        go.transform.position = position;
        var spark = go.AddComponent<FallingSpark>();
        spark.velocity = velocity;
        spark.light = go.AddComponent<EffectLight>();
        spark.light.Color = SparkColor;
        spark.light.Range = 1.5f;
        spark.light.Intensity = 2f;
        spark.light.enabled = true;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }
        velocity += Vector3.down * (Gravity * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;
        light.Intensity = 2f * (1f - age / Lifetime);
    }
}
