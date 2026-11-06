using System;
using System.Collections.Generic;
using AK;
using AssetShards;
using BepInEx.Configuration;
using FX_EffectSystem;
using GTFO.API;
using MindAttic.GTFO.Shared;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Grenade Pistol and Grenade Rifle: plain semi-auto guns on heavy models (one shared set of
/// stats - the pair handles identically) whose every shot launches a lock melter rigged to blow
/// instead of a bullet (SeekerGuns' ray hook calls <see cref="Launch"/>). The gun's own firing -
/// sound, recoil, ammo (a magazine and reloads, like any gun) - stays vanilla. A grenade that hits
/// an enemy, a teammate or a bot (never the shooter) explodes at once. One that misses bounces off
/// whatever it hits, flashing red faster and faster, and explodes <see cref="FuseSeconds"/> (2.5 s
/// by default) after it was fired. The blast damages and staggers enemies only, through the bullet path, on the
/// shooter's game; teammates are sent each launch and detonation and see the same grenades.
/// </summary>
internal static class GrenadeLauncher
{
    internal static ConfigEntry<float> Speed = null!;           // launch speed, m/s
    internal static ConfigEntry<float> FuseSeconds = null!;
    internal static ConfigEntry<float> BlastRadius = null!;
    internal static ConfigEntry<float> BlastDamage = null!;     // at the center, falling to BlastEdgeFraction at the edge
    internal static ConfigEntry<float> BlastEdgeFraction = null!;
    internal static ConfigEntry<float> BlastStagger = null!;
    internal static ConfigEntry<float> GrenadeBounciness = null!;
    internal static ConfigEntry<float> Lift = null!;
    internal static ConfigEntry<float> Gravity = null!;
    internal static ConfigEntry<float> GunShotDelay = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    private const string LaunchEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.GrenadeLaunch.v2";
    private const string LegacyLaunchEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.GrenadeLaunch";
    private const string DetonateEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.GrenadeDetonate";

    internal static void Bind(ConfigFile config)
    {
        Speed = config.Bind("Grenade Launcher", "LaunchSpeed", 30f, "Launch speed along the aim, m/s.");
        Lift = config.Bind("Grenade Launcher", "LaunchLift", 5f, "Extra upward speed every grenade is thrown with, m/s - the lob.");
        Gravity = config.Bind("Grenade Launcher", "Gravity", 18f, "Gravity on a grenade in flight, m/s2.");
        FuseSeconds = config.Bind("Grenade Launcher", "FuseSeconds", 2.5f, "Seconds before a missed grenade explodes on its own (Quake 1: 2.5).");
        BlastRadius = config.Bind("Grenade Launcher", "BlastRadius", 5.2f, "Metres the blast reaches (Quake 1: 160 units).");
        BlastDamage = config.Bind("Grenade Launcher", "BlastDamage", 75f, "Damage at the blast's center.");
        BlastEdgeFraction = config.Bind("Grenade Launcher", "BlastEdgeFraction", 0.33f, "Share of damage left at the blast's edge (Quake 1: a third).");
        BlastStagger = config.Bind("Grenade Launcher", "BlastStagger", 5f, "Blast stagger multiplier.");
        GrenadeBounciness = config.Bind("Grenade Launcher", "GrenadeBounciness", 0.5f, "Speed kept per bounce off the world (Quake 1: half).");
        GunShotDelay = config.Bind("Grenade Launcher", "GunShotDelay", 0.6f, "Seconds between shots (semi-auto) - the Grenade Pistol and Rifle alike.");
        GunClipSize = config.Bind("Grenade Launcher", "GunClipSize", 6, "Magazine size.");
    }

    internal static void Init()
    {
        // Teammates' copies fly with the shooter's own gravity, bounce and fuse (sent with the
        // launch), not this game's config.
        Wire.Register<LaunchMessage>(LaunchEvent, (_, message) =>
            LauncherGrenade.Spawn(message.Id, null, new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.VelX, message.VelY, message.VelZ),
                new GrenadeFlight(message.Gravity, message.Bounciness, message.Fuse)));
        // An older version's launch carries no numbers: this game's own are the best guess.
        Wire.RegisterLegacy<LegacyLaunchMessage>(LegacyLaunchEvent, (_, message) =>
            LauncherGrenade.Spawn(message.Id, null, new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.VelX, message.VelY, message.VelZ), GrenadeFlight.FromConfig()));
        RemoteEvents.Register<DetonateMessage>(DetonateEvent, (_, message) =>
            LauncherGrenade.Detonate(message.Id, new Vector3(message.X, message.Y, message.Z)));
    }

    private static uint nextId;

    // One shot from the local shooter's Grenade Pistol or Rifle: a grenade out of the muzzle along
    // the aim, shown to teammates too.
    internal static void Launch(PlayerAgent owner, Vector3 position, Vector3 aim, uint gearCategoryId)
    {
        // Quake 1: along the aim plus a fixed upward lob.
        var velocity = aim.normalized * Speed.Value + Vector3.up * Lift.Value;
        var id = ((uint)(owner.Owner?.PlayerSlotIndex() ?? 0) << 24) | (++nextId & 0xFFFFFF);
        var flight = GrenadeFlight.FromConfig();
        LauncherGrenade.Spawn(id, owner, position, velocity, flight, gearCategoryId);
        Wire.Send(LaunchEvent, new LaunchMessage(id, position, velocity, flight),
            LegacyLaunchEvent, new LegacyLaunchMessage(id, position, velocity));
    }

    internal static void SendDetonate(uint id, Vector3 position) =>
        NetworkAPI.InvokeEvent(DetonateEvent, new DetonateMessage(id, position));

    // Plain fields so GTFO-API can copy them as raw bytes.
    private readonly struct LaunchMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z, VelX, VelY, VelZ;
        public readonly float Gravity, Bounciness, Fuse; // the shooter's, so every copy flies alike

        public LaunchMessage(uint id, Vector3 position, Vector3 velocity, GrenadeFlight flight)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
            (VelX, VelY, VelZ) = (velocity.x, velocity.y, velocity.z);
            (Gravity, Bounciness, Fuse) = (flight.Gravity, flight.Bounciness, flight.Fuse);
        }
    }

    // The pre-v2 layout, unchanged, for games on an older version.
    private readonly struct LegacyLaunchMessage
    {
        public readonly uint Id;
        public readonly float X, Y, Z, VelX, VelY, VelZ;

        public LegacyLaunchMessage(uint id, Vector3 position, Vector3 velocity)
        {
            Id = id;
            (X, Y, Z) = (position.x, position.y, position.z);
            (VelX, VelY, VelZ) = (velocity.x, velocity.y, velocity.z);
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

/// <summary>How one grenade flies: the shooter's gravity, bounce and fuse, fixed at launch.</summary>
internal readonly struct GrenadeFlight
{
    public readonly float Gravity, Bounciness, Fuse;

    public GrenadeFlight(float gravity, float bounciness, float fuse)
    {
        Gravity = gravity;
        Bounciness = bounciness;
        Fuse = fuse;
    }

    internal static GrenadeFlight FromConfig() =>
        new(GrenadeLauncher.Gravity.Value, GrenadeLauncher.GrenadeBounciness.Value, GrenadeLauncher.FuseSeconds.Value);
}

/// <summary>
/// One grenade in flight: a lock melter's meshes, flown by hand (gravity, sphere casts) so it
/// bounces off the level. It blinks a red light (and beeps) faster and faster until its fuse runs
/// out, or until it hits an enemy, a teammate or a bot (on the shooter's game). Teammates'
/// copies only bounce; they explode when the shooter's game says so.
/// </summary>
internal sealed class LauncherGrenade : MonoBehaviour
{
    private const float Radius = 0.12f;
    private const float RestSpeed = 1.2f;
    private const float RemoteGraceSeconds = 0.75f; // a teammate's copy waits this long past the fuse for the detonation message
    private static readonly Color FlashColor = new(1f, 0.05f, 0.05f, 1f);

    private static readonly string[] LockMelterPrefabs =
    {
        "Assets/AssetPrefabs/Items/Consumables/LockMelter/Consumable_LockMelter_Pickup.prefab",
        "Assets/AssetPrefabs/Items/Consumables/LockMelter/Consumable_LockMelter.prefab",
        "Assets/AssetPrefabs/Items/Consumables/LockMelter/Consumable_LockMelter_FirstPerson.prefab",
    };

    private static readonly Dictionary<uint, LauncherGrenade> live = new();
    // Teammates' copies that blew on their own fuse before the shooter's detonation arrived.
    private static readonly HashSet<uint> selfDetonated = new();
    private static bool modelLogged;
    private static Material? ledMaterial;

    private uint id, gearCategoryId;
    private PlayerAgent? owner; // null on a teammate's (visual-only) copy
    private GrenadeFlight flight; // the shooter's gravity, bounce and fuse, on every copy
    private Vector3 velocity, spinAxis;
    private float age, spinSpeed, nextFlash, lastBounceSound;
    private bool resting, exploded, lit;
    private EffectLight light = null!;
    private LineRenderer led = null!;

    public LauncherGrenade(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(uint id, PlayerAgent? owner, Vector3 position, Vector3 velocity, GrenadeFlight flight, uint gearCategoryId = 0)
    {
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Launcher Grenade");
        go.transform.position = position;
        BuildModel(go.transform);

        var grenade = go.AddComponent<LauncherGrenade>();
        grenade.id = id;
        grenade.gearCategoryId = gearCategoryId;
        grenade.owner = owner;
        grenade.flight = flight;
        grenade.velocity = velocity;
        grenade.spinAxis = UnityEngine.Random.onUnitSphere;
        grenade.spinSpeed = UnityEngine.Random.Range(360f, 900f);

        var lightObject = new GameObject("Flash");
        lightObject.transform.SetParent(go.transform, false);
        grenade.light = lightObject.AddComponent<EffectLight>();
        grenade.light.Color = FlashColor;
        grenade.light.Range = 2.5f;
        grenade.light.Intensity = 0f;
        grenade.light.enabled = true;

        grenade.led = lightObject.AddComponent<LineRenderer>();
        grenade.led.positionCount = 2;
        grenade.led.useWorldSpace = true;
        grenade.led.numCapVertices = 4;
        grenade.led.startWidth = grenade.led.endWidth = 0.09f;
        grenade.led.startColor = grenade.led.endColor = FlashColor;
        grenade.led.material = LedMaterial();
        grenade.led.enabled = false;

        live[id] = grenade;
    }

    // A teammate's detonation message: blow that grenade up where their game says it went off.
    [HideFromIl2Cpp]
    internal static void Detonate(uint id, Vector3 position)
    {
        if (live.TryGetValue(id, out var grenade) && grenade != null)
        {
            grenade.transform.position = position;
            grenade.Explode(fromMessage: true);
        }
        else if (!selfDetonated.Remove(id))
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
            Flash();

            if (owner == null ? age >= flight.Fuse + RemoteGraceSeconds : age >= flight.Fuse)
            {
                Explode();
                return;
            }

            if (!resting) Fly(dt);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons launcher grenade failed: {e.Message}");
            exploded = true;
            live.Remove(id);
            Destroy(gameObject);
        }
    }

    [HideFromIl2Cpp]
    private void Fly(float dt)
    {
        velocity += Vector3.down * (flight.Gravity * dt);
        var position = transform.position;
        var step = velocity * dt;
        var distance = step.magnitude;
        if (distance < 0.0001f) return;
        var direction = step / distance;

        // The world first, so a wall or door between the grenade and whoever stands behind it
        // stops it: an enemy or player only counts when it's nearer than the world along the sweep.
        var hitWorld = Physics.SphereCast(position, Radius, direction, out var hit, distance, LayerManager.MASK_WORLD);
        var reach = hitWorld ? hit.distance : distance;

        // An enemy, a teammate or a bot in the way: it goes off on contact (the shooter's game
        // decides; the shooter themselves never sets it off).
        if (owner != null && Physics.SphereCast(position, Radius, direction, out var enemyHit, distance, LayerManager.MASK_ENEMY_DAMAGABLE)
            && (!hitWorld || enemyHit.distance < reach))
        {
            transform.position = enemyHit.point;
            Explode();
            return;
        }
        if (owner != null && ProjectileHits.OtherPlayer(position, Radius, direction, distance, owner, out var playerHit)
            && (!hitWorld || playerHit.distance < reach))
        {
            transform.position = playerHit.point;
            Explode();
            return;
        }

        if (hitWorld)
        {
            transform.position = hit.point + hit.normal * (Radius + 0.01f);
            velocity = Vector3.Reflect(velocity, hit.normal) * flight.Bounciness;
            spinSpeed *= 0.7f;
            if (velocity.magnitude > 2f && age - lastBounceSound > 0.1f)
            {
                lastBounceSound = age;
                CellSound.Post(EVENTS.FRAGGRENADEBOUNCE, hit.point);
            }
            // Settled on a floor: it stops and waits out its fuse.
            if (hit.normal.y > 0.7f && velocity.magnitude < RestSpeed)
            {
                resting = true;
                velocity = Vector3.zero;
            }
            return;
        }

        transform.position = position + step;
        transform.Rotate(spinAxis, spinSpeed * dt, Space.World);
    }

    // A red blink that speeds up from ~2 a second to ~16 as the fuse burns down, with a beep on
    // each flash while they're still far enough apart to hear.
    [HideFromIl2Cpp]
    private void Flash()
    {
        var progress = Mathf.Clamp01(age / flight.Fuse);
        var interval = Formulas.GrenadeFlashInterval(age, flight.Fuse);
        if (age >= nextFlash)
        {
            nextFlash = age + interval;
            lit = true;
            if (interval > 0.12f) CellSound.Post(EVENTS.DEPLOYER_BEEP_TARGET_ACQUIRED, transform.position);
        }
        else if (lit && age >= nextFlash - interval * 0.55f)
        {
            lit = false;
        }

        var position = transform.position;
        light.transform.position = position + Vector3.up * 0.1f;
        light.Intensity = lit ? 3f + 3f * progress : 0f;
        led.enabled = lit;
        if (lit)
        {
            led.SetPosition(0, position - Vector3.up * 0.02f);
            led.SetPosition(1, position + Vector3.up * 0.02f);
        }
    }

    [HideFromIl2Cpp]
    private void Explode(bool fromMessage = false)
    {
        if (exploded) return;
        exploded = true;
        live.Remove(id);
        var position = transform.position;
        Destroy(gameObject);

        if (owner == null)
        {
            // A teammate's copy: if this was its own fuse, the shooter's message is still coming.
            if (!fromMessage) selfDetonated.Add(id);
            Explosion.Show(position);
            return;
        }
        GrenadeLauncher.SendDetonate(id, position);
        Explosion.Show(position);
        AreaDamage.Apply(position, GrenadeLauncher.BlastRadius.Value, GrenadeLauncher.BlastDamage.Value, GrenadeLauncher.BlastEdgeFraction.Value,
            GrenadeLauncher.BlastStagger.Value, owner.Cast<Agents.Agent>(), null, gearCategoryId);
    }

    // The lock melter's meshes, copied onto the grenade - just the look, none of the prefab's
    // pickup or interaction scripts. Falls back to a plain capsule if the prefab isn't loaded.
    [HideFromIl2Cpp]
    internal static void BuildModel(Transform parent)
    {
        string source = "fallback capsule";
        try
        {
            foreach (var path in LockMelterPrefabs)
            {
                var prefab = AssetShardManager.GetLoadedAsset<GameObject>(path);
                if (prefab == null) continue;
                var root = prefab.transform;
                var copied = 0;
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null) continue;
                    var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var part = new GameObject("Part");
                    part.transform.SetParent(parent, false);
                    part.transform.localPosition = matrix.GetColumn(3);
                    part.transform.localRotation = matrix.rotation;
                    part.transform.localScale = matrix.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    copied++;
                }
                if (copied == 0) continue;
                source = $"{path} ({copied} meshes)";
                return;
            }

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.Destroy(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(parent, false);
            capsule.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        }
        finally
        {
            if (!modelLogged)
            {
                modelLogged = true;
                Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons launcher grenade model: {source}.");
            }
        }
    }

    [HideFromIl2Cpp]
    private static Material LedMaterial() =>
        ledMaterial != null ? ledMaterial // Unity's null: rebuilt if unloaded
            : ledMaterial = new Material(Shader.Find("Sprites/Default")) { color = FlashColor };
}

/// <summary>The blast's look and sound: the trip mine's explosion effect, a flash and the frag grenade bang.</summary>
internal static class Explosion
{
    private const string TripMineExplosion = "Assets/AssetPrefabs/FX_Effects/FX_Tripmine.prefab";
    private static FX_Pool? pool;
    private static bool effectFailed, effectLogged;

    internal static void Show(Vector3 position)
    {
        CellSound.Post(EVENTS.FRAGGRENADEEXPLODE, position);
        BurstFlash.Spawn(position + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.2f), 10f, 10f, 0.35f);
        if (effectFailed) return;
        try
        {
            if (pool == null) pool = MineDeployerInstance_Detonate_Explosive.s_explosionPool; // Unity null: destroyed at level cleanup
            if (pool == null)
            {
                var prefab = AssetShardManager.GetLoadedAsset<GameObject>(TripMineExplosion);
                if (prefab != null) pool = FX_Manager.GetEffectPool(prefab);
            }
            if (!effectLogged)
            {
                effectLogged = true;
                Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons grenade explosion effect: {(pool != null ? "trip mine explosion" : "none loaded (flash only)")}.");
            }
            pool?.AquireEffect()?.Play(null, position, Quaternion.identity);
        }
        catch (Exception e)
        {
            effectFailed = true;
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons grenade explosion effect unavailable: {e.Message}");
        }
    }
}
