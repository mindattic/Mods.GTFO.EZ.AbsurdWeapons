using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using FX_EffectSystem;
using GTFO.API;
using MindAttic.GTFO.Shared;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Chain Lightning Gun: a continuous stream of lightning for as long as the trigger is held
/// (<see cref="ChainLightningDriver"/>; the gun itself never fires). Every tick it strikes the
/// enemy under the crosshair (or, failing that, the enemy nearest the aim within a narrow cone),
/// then jumps to the nearest enemy it hasn't hit yet, again and again, up to five jumps, each one
/// weaker than the last. Ticks come faster than a bolt fades, so it reads as one unbroken arc. Jumps need line of sight. Damage goes through the bullet path on the
/// shooter's game, so it syncs; teammates are sent the bolt's path and see the same lightning.
/// </summary>
internal static class ChainLightning
{
    // The ceiling for the Jumps setting: the network message below has exactly 7 point fields
    // (X0..X6) sized for this many jumps plus the start and first strike.
    internal const int MaxJumps = 5;

    private static ConfigEntry<int> Jumps = null!;
    private static ConfigEntry<float> Range = null!;
    private static ConfigEntry<float> AssistConeDegrees = null!; // a near miss still finds its first target
    private static ConfigEntry<float> JumpRange = null!;
    private static ConfigEntry<float> JumpFalloff = null!;       // each jump does this share of the one before
    private static ConfigEntry<float> StaggerMulti = null!;
    private const string BoltEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.ChainLightning";

    // The bolt starts further out from the eye than the lasers' beam (LaserGuns.ScreenStart's
    // default) - the gun sits lower on screen, so the default offset left the bolt spawning inside it.
    private const float BoltForward = 0.9f;

    internal static void Bind(ConfigFile config)
    {
        Range = config.Bind("Chain Lightning Gun", "Range", 40f, "Metres the first strike reaches.");
        AssistConeDegrees = config.Bind("Chain Lightning Gun", "AssistConeDegrees", 6f, "A near-miss within this cone still finds a first target.");
        JumpRange = config.Bind("Chain Lightning Gun", "JumpRange", 8f, "Metres each jump can reach the next enemy.");
        JumpFalloff = config.Bind("Chain Lightning Gun", "JumpFalloff", 0.75f, "Share of the previous strike's damage each jump does.");
        StaggerMulti = config.Bind("Chain Lightning Gun", "StaggerMulti", 2f, "Stagger multiplier per strike.");
        Jumps = config.Bind("Chain Lightning Gun", "Jumps", MaxJumps,
            new ConfigDescription($"How many times a bolt leaps on to another enemy after the first strike (at most {MaxJumps}). 0 strikes one enemy only.",
                new AcceptableValueRange<int>(0, MaxJumps)));
    }

    internal static void Init()
    {
        RemoteEvents.Register<BoltMessage>(BoltEvent, (sender, message) => LightningBolt.Spawn(message.Points(), message.Struck != 0, sender));
    }

    // How often the stream strikes while the trigger is held. Each strike's damage is scaled from
    // the archetype's per-shot damage and shot delay, so damage per second is unchanged.
    internal const float TickSeconds = 0.08f;

    // One tick of the stream from the local shooter.
    // `muzzle`: the gun in hand, so the stream leaves that model's own barrel - hip fire or aimed -
    // the same way the lasers' beam does (else a fixed spot ahead of the eye).
    internal static void Fire(PlayerAgent owner, Vector3 eye, Vector3 aim, float damage, uint gearCategoryId, Transform? muzzle = null)
    {
        var path = new List<Vector3> { muzzle != null ? Barrel.Start(owner, muzzle, eye, aim) : LaserGuns.ScreenStart(eye, aim, BoltForward) };
        var struck = new HashSet<IntPtr>();

        // The first target: whatever enemy the shot hits, else the enemy nearest the aim.
        EnemyAgent? current = null;
        Dam_EnemyDamageLimb? limb = null;
        var point = eye + aim * Range.Value;
        if (Physics.Raycast(eye, aim, out var hit, Range.Value, LayerManager.MASK_BULLETWEAPON_RAY))
        {
            point = hit.point;
            limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
            current = limb?.m_base?.Owner;
            // A corpse in the way isn't a target: fall through to the enemy nearest the aim instead
            // of the whole stream fizzling on it as a miss.
            if (current != null && !current.Alive)
            {
                current = null;
                limb = null;
            }
            if (current == null && LockDamage.TryHit(hit.collider, damage, owner.Cast<Agent>(), hit.point, aim, hit.normal))
            {
                path.Add(point);
                Send(path, owner, struckEnemies: false);
                return;
            }
        }
        if (current == null)
        {
            var locks = SeekerGuns.FindLocks(eye, aim, AssistConeDegrees.Value, Range.Value);
            if (locks.Count > 0)
            {
                current = locks[0];
                limb = NearestLimb(current, current.AimTarget.position);
                point = current.AimTarget.position;
            }
        }

        if (current == null || limb == null || !current.Alive)
        {
            // A plain miss: the bolt crackles out to whatever it hit, harmlessly.
            path.Add(point);
            Send(path, owner, struckEnemies: false);
            return;
        }

        var strike = damage;
        var jumps = Mathf.Clamp(Jumps.Value, 0, MaxJumps);
        for (var jump = 0; jump <= jumps && current != null && limb != null; jump++)
        {
            struck.Add(current.Pointer);
            var from = path[path.Count - 1];
            var direction = (point - from).sqrMagnitude > 0.0001f ? (point - from).normalized : aim;
            limb.BulletDamage(strike, owner.Cast<Agent>(), point, direction, -direction,
                allowDirectionalBonus: false, StaggerMulti.Value, 1f, gearCategoryId);
            path.Add(point);

            strike *= JumpFalloff.Value;
            current = NextTarget(point, struck);
            if (current == null) break;
            point = current.AimTarget.position;
            limb = NearestLimb(current, point);
        }
        Send(path, owner, struckEnemies: true);
    }

    // A lightning pulse from a point (thrown Mjolnir): it strikes the nearest enemy within `range`
    // that it can see, then jumps on like a bolt, `jumps` times. Returns whether it struck anything.
    internal static bool Pulse(PlayerAgent owner, Vector3 from, float range, float damage, int jumps, uint gearCategoryId)
    {
        var struck = new HashSet<IntPtr>();
        var current = NextTarget(from, struck, range);
        if (current == null) return false;

        var path = new List<Vector3> { from };
        var strike = damage;
        for (var jump = 0; jump <= jumps && current != null; jump++)
        {
            var point = current.AimTarget.position;
            var limb = NearestLimb(current, point);
            if (limb == null) break;
            struck.Add(current.Pointer);
            var direction = (point - path[path.Count - 1]).normalized;
            limb.BulletDamage(strike, owner.Cast<Agent>(), point, direction, -direction,
                allowDirectionalBonus: false, StaggerMulti.Value, 1f, gearCategoryId);
            path.Add(point);
            strike *= JumpFalloff.Value;
            current = NextTarget(point, struck);
        }
        // Struck unless the first target had no limb to hit (the path is then just the start).
        Send(path, owner, struckEnemies: path.Count > 1);
        return true;
    }

    // `struckEnemies` is said outright rather than read off the path's length: a bolt that strikes
    // exactly one enemy has the same two points (gun, strike) as a plain miss.
    private static void Send(List<Vector3> path, PlayerAgent owner, bool struckEnemies)
    {
        LightningBolt.Spawn(path.ToArray(), struckEnemies, owner.Owner != null ? owner.Owner.Lookup : 0UL);
        NetworkAPI.InvokeEvent(BoltEvent, new BoltMessage(path, struckEnemies));
    }

    // The nearest living enemy within jump range of `from` that hasn't been struck, in sight.
    private static EnemyAgent? NextTarget(Vector3 from, HashSet<IntPtr> struck, float? range = null)
    {
        var searchRange = range ?? JumpRange.Value;
        EnemyAgent? best = null;
        var bestDistance = float.MaxValue;
        foreach (var collider in Physics.OverlapSphere(from, searchRange, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (enemy == null || !enemy.Alive || struck.Contains(enemy.Pointer)) continue;
            var chest = enemy.AimTarget.position;
            var distance = Vector3.Distance(from, chest);
            if (distance >= bestDistance) continue;
            if (Physics.Linecast(from, chest, LayerManager.MASK_WORLD)) continue;
            best = enemy;
            bestDistance = distance;
        }
        return best;
    }

    // The limb of `enemy` nearest `point`.
    internal static Dam_EnemyDamageLimb? NearestLimb(EnemyAgent enemy, Vector3 point)
    {
        Dam_EnemyDamageLimb? nearest = null;
        var best = float.MaxValue;
        foreach (var limb in enemy.GetComponentsInChildren<Dam_EnemyDamageLimb>())
        {
            var distance = (limb.transform.position - point).sqrMagnitude;
            if (distance < best) { best = distance; nearest = limb; }
        }
        return nearest;
    }

    // Plain fields so GTFO-API can copy it as raw bytes: the gun plus up to six strikes, and
    // whether those points are enemies struck (flash) or a miss / lock hit (sparks only).
    private readonly struct BoltMessage
    {
        public readonly byte Count;
        public readonly byte Struck;
        public readonly float X0, Y0, Z0, X1, Y1, Z1, X2, Y2, Z2, X3, Y3, Z3, X4, Y4, Z4, X5, Y5, Z5, X6, Y6, Z6;

        public BoltMessage(List<Vector3> path, bool struckEnemies)
        {
            Count = (byte)Math.Min(path.Count, MaxJumps + 2);
            Struck = struckEnemies ? (byte)1 : (byte)0;
            Vector3 P(int i) => i < path.Count ? path[i] : Vector3.zero;
            (X0, Y0, Z0) = (P(0).x, P(0).y, P(0).z);
            (X1, Y1, Z1) = (P(1).x, P(1).y, P(1).z);
            (X2, Y2, Z2) = (P(2).x, P(2).y, P(2).z);
            (X3, Y3, Z3) = (P(3).x, P(3).y, P(3).z);
            (X4, Y4, Z4) = (P(4).x, P(4).y, P(4).z);
            (X5, Y5, Z5) = (P(5).x, P(5).y, P(5).z);
            (X6, Y6, Z6) = (P(6).x, P(6).y, P(6).z);
        }

        public Vector3[] Points()
        {
            var all = new[]
            {
                new Vector3(X0, Y0, Z0), new Vector3(X1, Y1, Z1), new Vector3(X2, Y2, Z2), new Vector3(X3, Y3, Z3),
                new Vector3(X4, Y4, Z4), new Vector3(X5, Y5, Z5), new Vector3(X6, Y6, Z6),
            };
            var points = new Vector3[Math.Min((int)Count, all.Length)];
            Array.Copy(all, points, points.Length);
            return points;
        }
    }
}

/// <summary>
/// One bolt's look: a jagged line through every strike point, re-jagged every frame so it
/// crackles, with a flash and sparks at each enemy struck, gone in a fraction of a second.
/// </summary>
internal sealed class LightningBolt : MonoBehaviour
{
    private const float Seconds = 0.16f;
    private const float SegmentLength = 0.35f;
    private const float Jitter = 0.18f;
    private static readonly Color BoltColor = new(0.7f, 0.85f, 1f, 1f);
    private static readonly Color FlashColor = new(0.45f, 0.75f, 1f);
    private static Material? material;

    private LineRenderer line = null!;
    private Vector3[] points = Array.Empty<Vector3>();
    private float age;

    public LightningBolt(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3[] points, bool struckEnemies, ulong source = 0)
    {
        if (points.Length < 2) return;

        // A crackling loop while the stream keeps coming, and a crackle where it strikes.
        LoopSounds.Touch($"lightning:{source}", Sounds.LightningLoopStart.Value, Sounds.LightningLoopStop.Value, points[1]);
        Sounds.PlayLimited($"crackle:{source}", 0.25f, Sounds.LightningCrackle.Value, points[points.Length - 1]);
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Lightning Bolt");
        var bolt = go.AddComponent<LightningBolt>();
        bolt.points = points;
        bolt.line = go.AddComponent<LineRenderer>();
        bolt.line.useWorldSpace = true;
        bolt.line.numCapVertices = 2;
        if (material == null) material = new Material(Shader.Find("Sprites/Default")); // Unity's null: rebuilt if unloaded
        bolt.line.material = material;
        bolt.Jag();

        // Every strike point after the gun flashes and sparks (a plain miss just sparks).
        for (var i = 1; i < points.Length; i++)
        {
            if (struckEnemies) BurstFlash.Spawn(points[i], FlashColor, 4f, 4f, 0.15f);
            try
            {
                FX_Manager.PlayEffect(false, FX_GroupName.Impact_Electronics, points[i], Quaternion.LookRotation(points[i] - points[i - 1]), false);
            }
            catch (Exception)
            {
                // Sparks are a nicety; the bolt and flash still show.
            }
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Seconds)
        {
            Destroy(gameObject);
            return;
        }
        Jag();
    }

    // Lays a fresh jagged path through the strike points: each stretch split into short segments
    // nudged sideways at random, pinned at the strike points themselves.
    [HideFromIl2Cpp]
    private void Jag()
    {
        var jagged = new List<Vector3> { points[0] };
        for (var i = 1; i < points.Length; i++)
        {
            var from = points[i - 1];
            var to = points[i];
            var segments = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) / SegmentLength), 1, 120);
            for (var s = 1; s < segments; s++)
                jagged.Add(Vector3.Lerp(from, to, s / (float)segments) + UnityEngine.Random.insideUnitSphere * Jitter);
            jagged.Add(to);
        }

        line.positionCount = jagged.Count;
        for (var i = 0; i < jagged.Count; i++) line.SetPosition(i, jagged[i]);
        var fade = 1f - age / Seconds;
        var width = 0.05f * (0.6f + 0.4f * UnityEngine.Random.value);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = new Color(BoltColor.r, BoltColor.g, BoltColor.b, fade);
    }
}

/// <summary>
/// Drives the local player's Chain Lightning stream from the trigger: while the gun is in hand and
/// Fire is held, a strike every <see cref="ChainLightning.TickSeconds"/>, its damage the archetype's
/// per-shot damage scaled to the tick (same damage per second as when it fired shots).
/// </summary>
internal sealed class ChainLightningDriver : MonoBehaviour
{
    private float nextTick;

    public ChainLightningDriver(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            var player = PlayerManager.GetLocalPlayerAgent();
            if (player == null || !player.Alive || FocusStateManager.CurrentState != eFocusState.FPS) return;
            if (StreamReadiness.Blocked(player)) return;
            if (!StreamReadiness.IsSettled(player)) return; // not yet raised into its aim position
            var gun = player.Inventory?.WieldedItem?.TryCast<Gear.BulletWeapon>();
            var archetype = gun?.ArchetypeData;
            if (gun == null || archetype == null || archetype.persistentID != GunInjector.ChainLightningId) return;
            if (!InputMapper.GetButtonKeyMouse(InputAction.Fire, eFocusState.FPS)) return;
            var camera = player.FPSCamera;
            if (camera == null) return;

            // Ammo goes by the time the trigger is held, every frame; the strikes come on the ticks.
            if (!InfiniteAmmo.HasStreamAmmo(player) || !InfiniteAmmo.Drain(player, Time.deltaTime)) return; // out of ammo
            if (Time.time < nextTick) return;
            nextTick = Time.time + ChainLightning.TickSeconds;
            var damage = archetype.Damage * ChainLightning.TickSeconds / Mathf.Max(0.02f, archetype.ShotDelay);
            ChainLightning.Fire(player, camera.Position, camera.Forward, damage, GunInjector.ChainLightningId, gun.MuzzleAlign);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons chain lightning driver failed: {e.Message}");
            nextTick = Time.time + 1f;
        }
    }
}

/// <summary>
/// The Chain Lightning Gun's soft purple glow while it's out in anyone's hand: the same violet
/// (0.6, 0.3, 1) the Shadow Dagger once left lit on it by accident, kept on purpose. Its own color
/// and its own light, nothing shared with the dagger, so the dagger's glow going out on a swap
/// (StealthVisuals) leaves this one be.
/// </summary>
internal sealed class ChainLightningGlow : MonoBehaviour
{
    internal static readonly Color GlowColor = new(0.6f, 0.3f, 1f);

    // Per player holding the gun: the item it was lit for, and the light.
    private readonly Dictionary<IntPtr, (IntPtr Item, EffectLight Light)> lit = new();

    public ChainLightningGlow(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            HashSet<IntPtr>? present = lit.Count > 0 ? new() : null;
            var players = PlayerManager.PlayerAgentsInLevel;
            if (players != null) foreach (var player in players)
            {
                if (player == null) continue;
                present?.Add(player.Pointer);
                try
                {
                    Light(player);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Chain Lightning glow failed: {e.Message}");
                    PutOut(player.Pointer);
                }
            }

            // A player gone (or a level ended) is never visited above: their light goes out here.
            if (present == null) return;
            List<IntPtr>? gone = null;
            foreach (var pointer in lit.Keys)
                if (!present.Contains(pointer)) (gone ??= new List<IntPtr>()).Add(pointer);
            if (gone != null) foreach (var pointer in gone) PutOut(pointer);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Chain Lightning glow failed: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private void Light(PlayerAgent player)
    {
        var item = player.Inventory?.WieldedItem;
        var isGun = item != null && item.gameObject.activeInHierarchy
            && item.TryCast<Gear.BulletWeapon>()?.ArchetypeData?.persistentID == GunInjector.ChainLightningId;
        var has = lit.TryGetValue(player.Pointer, out var state);
        if (has && (!isGun || state.Item != item!.Pointer || state.Light == null))
        {
            PutOut(player.Pointer);
            has = false;
        }
        if (!isGun) return;

        if (!has)
        {
            var go = new GameObject("GTFO.EZ.AbsurdWeapons Chain Lightning Glow");
            var light = go.AddComponent<EffectLight>();
            light.Color = GlowColor;
            light.Range = 1.2f;
            light.enabled = true;
            state = (item!.Pointer, light);
            lit[player.Pointer] = state;
        }

        state.Light!.transform.position = item!.transform.position;
        // The soft flicker the dagger's light had when it carried over.
        state.Light.Intensity = 0.2f * (0.75f + 0.25f * Mathf.Sin(Time.time * 12f));
    }

    [HideFromIl2Cpp]
    private void PutOut(IntPtr player)
    {
        if (!lit.TryGetValue(player, out var state)) return;
        lit.Remove(player);
        if (state.Light == null) return;
        state.Light.Intensity = 0f;
        state.Light.enabled = false;
        Destroy(state.Light.gameObject);
    }
}
