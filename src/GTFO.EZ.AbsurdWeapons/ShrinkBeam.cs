using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using GTFO.API;
using MindAttic.GTFO.Shared;
using Il2CppInterop.Runtime.Attributes;
using LevelGeneration;
using Player;
using SNetwork;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Shrink Beam: a continuous ray (<see cref="ShrinkBeamDriver"/>; the gun itself never fires)
/// that holds on whatever's under the crosshair and steadily shrinks it - smaller and slower by the
/// same share of its own full size - until it shrinks to <see cref="MinScale"/> and gibs. Once
/// it's at <see cref="StepOnScale"/> or smaller, getting within <see cref="StepOnRadius"/> of it and
/// looking down at it gibs it too - you've stepped on it. Stop holding the beam on it
/// and, after <see cref="RegrowDelaySeconds"/> off it, it grows back to full size at its own pace: a
/// shrink has to be finished in one sustained hold, not nibbled away at across several bursts. The
/// shrink and regrow are cosmetic (scale and move speed aren't
/// networked, so every client that sees the beam's line - the shooter's own tick, or the message
/// everyone else gets - runs the same shrink math on its own copy of whatever that line is on);
/// the kill itself goes through the shooter's own bullet path, so it syncs like any other hit.
/// </summary>
internal static class ShrinkBeam
{
    private static ConfigEntry<float> Range = null!;
    internal static ConfigEntry<float> ShrinkPerSecond = null!;
    internal static ConfigEntry<float> RegrowDelaySeconds = null!;
    internal static ConfigEntry<float> RegrowPerSecond = null!;
    internal static ConfigEntry<float> MinScale = null!;
    internal static ConfigEntry<float> PopDamage = null!;
    internal static ConfigEntry<float> StepOnScale = null!;
    internal static ConfigEntry<float> LockBreakScale = null!;
    internal static ConfigEntry<float> StepOnRadius = null!;
    internal static ConfigEntry<float> StepOnLookConeDegrees = null!;
    internal static ConfigEntry<float> GunShotDelay = null!;
    internal static ConfigEntry<int> GunClipSize = null!;

    private const string BeamEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.ShrinkBeam.v2";
    private const string LegacyBeamEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.ShrinkBeam";
    // How far from the received beam's end point a receiving client looks for the enemy it's on.
    private const float RemoteFindRadius = 0.6f;
    private static readonly Color PopColor = new(0.3f, 1f, 0.4f);

    internal static void Bind(ConfigFile config)
    {
        Range = config.Bind("Shrink Beam", "Range", 40f, "Metres the beam reaches.");
        ShrinkPerSecond = config.Bind("Shrink Beam", "ShrinkPerSecond", 0.25f, "Share of full size (and speed) removed per second the beam holds on an enemy.");
        RegrowDelaySeconds = config.Bind("Shrink Beam", "RegrowDelaySeconds", 1f, "Seconds off the beam before a shrunk enemy starts growing back.");
        RegrowPerSecond = config.Bind("Shrink Beam", "RegrowPerSecond", 0.15f, "Share of full size regained per second once it starts growing back.");
        MinScale = config.Bind("Shrink Beam", "MinScale", 0.45f, "Share of full size at which it gibs.");
        PopDamage = config.Bind("Shrink Beam", "PopDamage", 99999f, "Damage dealt on the pop (enough to kill anything outright).");
        StepOnScale = config.Bind("Shrink Beam", "StepOnScale", 0.7f, "At or below this share of full size you can step on it: get within StepOnRadius and look down at it and it gibs.");
        LockBreakScale = config.Bind("Shrink Beam", "LockBreakScale", 0.5f, "Share of full size at which a shrunk door padlock breaks.");
        StepOnRadius = config.Bind("Shrink Beam", "StepOnRadius", 2f, "Metres from a small-enough enemy within which looking down at it steps on it.");
        StepOnLookConeDegrees = config.Bind("Shrink Beam", "StepOnLookConeDegrees", 35f, "How close to your crosshair a small-enough enemy has to be for you to count as looking down at it.");
        GunShotDelay = config.Bind("Shrink Beam", "GunShotDelay", 0.08f, "Seconds between beam ticks (it never actually fires - the stream is trigger-driven).");
        GunClipSize = config.Bind("Shrink Beam", "GunClipSize", 100, "Magazine size.");
    }

    internal static void Init()
    {
        // Each received tick shrinks by the shooter's own amount down to the shooter's own floor
        // (sent with it), not this game's config - the host's copy is the one whose slowdown counts.
        Wire.Register<BeamMessage>(BeamEvent, (sender, message) =>
            OnRemoteBeam(Wire.Actor(message.Shooter, sender), new Vector3(message.X0, message.Y0, message.Z0),
                new Vector3(message.X1, message.Y1, message.Z1), message.Hit, message.Shrink, message.Floor));
        // An older version's tick carries no numbers: this game's own stand in.
        Wire.RegisterLegacy<LegacyBeamMessage>(LegacyBeamEvent, (sender, message) =>
            OnRemoteBeam(sender, new Vector3(message.X0, message.Y0, message.Z0), new Vector3(message.X1, message.Y1, message.Z1),
                message.Hit, ShrinkPerSecond.Value * TickSeconds,
                message.Hit == BeamMessage.HitLock ? LockBreakScale.Value : MinScale.Value));
    }

    private static void OnRemoteBeam(ulong shooter, Vector3 start, Vector3 end, byte hit, float shrink, float floor)
    {
        ShrinkStream.Spawn(start, end, shooter);
        // Only what the shooter's beam is on: a beam on the floor beside an enemy shrinks nothing.
        if (hit == BeamMessage.HitEnemy)
        {
            var enemy = NearestEnemy(end, RemoteFindRadius);
            if (enemy != null && enemy.Alive) Shrink(enemy, shrink, floor);
        }
        else if (hit == BeamMessage.HitLock && NearestLock(end) is { } padlock)
        {
            ShrinkLock(padlock, shrink, floor);
        }
    }

    // Seconds of shrink in one beam tick - the shooter's tick interval and what each received beam
    // message stands for, so both sides shrink by the same amount (a GunShotDelay of 0 or less would
    // otherwise shrink teammates' copies by nothing, or grow them).
    internal static float TickSeconds => Mathf.Max(0.02f, GunShotDelay.Value);

    // Keyed by the enemy's own Il2Cpp pointer (its GameObject never changes while it's alive).
    private sealed class Shrunk
    {
        public EnemyAgent Enemy = null!;
        public float Scale = 1f;
        public Vector3 BaseScale = Vector3.one; // its own size before the beam touched it
        public float BaseRunSpeed;
        public bool CapturedBase;
        public float LastHit;
        public float PoppedAt = -1f; // waiting on the kill to land (a client waits on the host)
    }

    // A pop whose kill never landed (it missed, or was refused) lets the enemy grow back after this.
    private const float PopGraceSeconds = 2f;

    private static readonly Dictionary<IntPtr, Shrunk> shrunk = new();

    // Door padlocks shrink the same way and break at LockBreakScale.
    private sealed class ShrunkLock
    {
        public LG_WeakLockDamage Lock = null!;
        public Transform Body = null!;
        public Vector3 BaseScale;
        public float Scale = 1f;
        public float LastHit;
        public float PoppedAt = -1f; // waiting on the break to land (a client waits on the host)
    }

    private static readonly Dictionary<IntPtr, ShrunkLock> shrunkLocks = new();

    // One beam update from the local shooter, called every frame so the beam's start always sits
    // right at the gun's own barrel on screen, not just wherever it was at the last shrink tick.
    // `applyShrink` false: just the beam kept lit and following the gun (every frame between
    // ticks) - no shrink math, no network message (same split as the laser beam's).
    internal static void Tick(PlayerAgent owner, Vector3 eye, Vector3 aim, Transform? muzzle, float dt, uint gearCategoryId, bool applyShrink = true)
    {
        var start = Barrel.Start(owner, muzzle, eye, aim);
        var source = owner.Owner != null ? owner.Owner.Lookup : 0UL;
        var end = eye + aim * Range.Value;
        EnemyAgent? hitEnemy = null;
        LG_WeakLockDamage? hitLock = null;
        if (Physics.Raycast(eye, aim, out var hit, Range.Value, LayerManager.MASK_BULLETWEAPON_RAY))
        {
            end = hit.point;
            hitEnemy = hit.collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (hitEnemy == null) hitLock = IntactLock(hit.collider);
        }

        if (!applyShrink)
        {
            ShrinkStream.Spawn(start, end, source);
            return;
        }

        var shrink = ShrinkPerSecond.Value * dt;
        if (hitEnemy != null && hitEnemy.Alive)
        {
            var state = Shrink(hitEnemy, shrink, MinScale.Value);
            if (state.Scale <= MinScale.Value) Pop(owner, state, gearCategoryId);
            // Its slowdown is the host's copy of it slowing down: without the mod there, it doesn't.
            if (!SNet.IsMaster) NetPresence.WarnIfHostLacksMod("the Shrink Beam's slowdown");
        }
        else if (hitLock != null)
        {
            var state = ShrinkLock(hitLock, shrink, LockBreakScale.Value);
            if (state.Scale <= LockBreakScale.Value) PopLock(owner, state, hit.point, aim);
        }

        ShrinkStream.Spawn(start, end, source);
        var hitKind = hitEnemy != null && hitEnemy.Alive ? BeamMessage.HitEnemy : hitLock != null ? BeamMessage.HitLock : BeamMessage.HitNothing;
        var floor = hitKind == BeamMessage.HitLock ? LockBreakScale.Value : MinScale.Value;
        Wire.Send(BeamEvent, new BeamMessage(start, end, hitKind, source, shrink, floor),
            LegacyBeamEvent, new LegacyBeamMessage(start, end, hitKind));
    }

    // Shrinks `enemy` by one tick's worth (`amount` of its full size, no smaller than `floor`) and
    // applies it (scale and, scaled the same, its own top speed). The base speed is read once, the
    // first time this enemy is ever touched, so repeated ticks scale from the same baseline instead
    // of compounding.
    [HideFromIl2Cpp]
    private static Shrunk Shrink(EnemyAgent enemy, float amount, float floor)
    {
        if (!shrunk.TryGetValue(enemy.Pointer, out var state))
        {
            state = new Shrunk { Enemy = enemy, BaseScale = enemy.transform.localScale };
            shrunk[enemy.Pointer] = state;
        }
        state.LastHit = Time.time;
        if (state.PoppedAt >= 0f) return state;
        if (!state.CapturedBase)
        {
            try
            {
                var locomotion = enemy.Locomotion;
                state.BaseRunSpeed = locomotion != null ? locomotion.m_maxMovementSpeed : 0f;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons shrink beam couldn't read {enemy.name}'s base speed: {e.Message}");
            }
            state.CapturedBase = true;
        }

        state.Scale = Mathf.Max(floor, state.Scale - amount);
        Apply(state);
        return state;
    }

    [HideFromIl2Cpp]
    private static void Apply(Shrunk state)
    {
        var enemy = state.Enemy;
        if (enemy == null) return;
        try
        {
            enemy.transform.localScale = state.BaseScale * state.Scale;
            var locomotion = enemy.Locomotion;
            if (locomotion != null && state.BaseRunSpeed > 0f) locomotion.m_maxMovementSpeed = state.BaseRunSpeed * state.Scale;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons shrink beam couldn't resize {enemy.name}: {e.Message}");
        }
    }

    // Shrunk past MinScale: the shooter's own game finishes it off (through the bullet path, so it
    // syncs) and a green pop marks it. It stays tracked, small, until it's actually dead - on a client
    // the kill only lands once the host confirms it, and dropping it sooner let the next beam tick
    // pick it back up at full size.
    [HideFromIl2Cpp]
    private static void Pop(PlayerAgent owner, Shrunk state, uint gearCategoryId)
    {
        if (state.PoppedAt >= 0f) return;
        state.PoppedAt = Time.time;
        var enemy = state.Enemy;
        try
        {
            var point = enemy.AimTarget.position;
            PopEffect(point);
            var limb = ChainLightning.NearestLimb(enemy, point);
            limb?.BulletDamage(PopDamage.Value, owner.Cast<Agent>(), point, Vector3.up, Vector3.down,
                allowDirectionalBonus: false, 10f, 1f, gearCategoryId);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons shrink beam pop failed: {e.Message}");
        }
    }

    // The green pop's flash and sound - for a shrunk enemy or padlock.
    private static void PopEffect(Vector3 point)
    {
        BurstFlash.Spawn(point, PopColor, 5f, 8f, 0.3f);
        Sounds.Play(Sounds.ShrinkPop.Value, point);
    }

    [HideFromIl2Cpp]
    private static LG_WeakLockDamage? IntactLock(Collider collider)
    {
        var padlock = collider.GetComponentInParent<LG_WeakLockDamage>();
        return padlock != null && padlock.Health > 0f ? padlock : null;
    }

    // The intact padlock at the received beam's end point, if that's what it's on.
    [HideFromIl2Cpp]
    private static LG_WeakLockDamage? NearestLock(Vector3 point)
    {
        foreach (var collider in Physics.OverlapSphere(point, 0.3f, ~0, QueryTriggerInteraction.Collide))
            if (IntactLock(collider) is { } padlock) return padlock;
        return null;
    }

    [HideFromIl2Cpp]
    private static ShrunkLock ShrinkLock(LG_WeakLockDamage padlock, float amount, float floor)
    {
        if (!shrunkLocks.TryGetValue(padlock.Pointer, out var state))
        {
            var body = padlock.m_lock != null ? padlock.m_lock.transform : padlock.transform;
            state = new ShrunkLock { Lock = padlock, Body = body, BaseScale = body.localScale };
            shrunkLocks[padlock.Pointer] = state;
        }
        state.LastHit = Time.time;
        if (state.PoppedAt >= 0f) return state;
        state.Scale = Mathf.Max(floor, state.Scale - amount);
        state.Body.localScale = state.BaseScale * state.Scale;
        return state;
    }

    // Shrunk to LockBreakScale: back to its own size, a green pop, and the shooter's game breaks it
    // through the lock's own damage path, so it syncs. It stays tracked until it's actually broken -
    // on a client the break only lands once the host confirms it, and dropping it sooner let the beam
    // shrink it and pop it a second time.
    [HideFromIl2Cpp]
    private static void PopLock(PlayerAgent owner, ShrunkLock state, Vector3 point, Vector3 direction)
    {
        if (state.PoppedAt >= 0f) return;
        state.PoppedAt = Time.time;
        try
        {
            state.Body.localScale = state.BaseScale;
            PopEffect(point);
            LockDamage.Hit(state.Lock, PopDamage.Value, owner.Cast<Agent>(), point, direction, -direction);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons shrink beam lock pop failed: {e.Message}");
        }
    }

    // Every tracked enemy and padlock not touched in a while grows back toward full size on its
    // own - each client runs this independently off its own shrunk lists, so there's nothing to sync.
    internal static void Regrow(float dt)
    {
        RegrowLocks(dt);
        if (shrunk.Count == 0) return;
        List<IntPtr>? done = null;
        foreach (var (pointer, state) in shrunk)
        {
            if (state.Enemy == null || !state.Enemy.Alive)
            {
                (done ??= new List<IntPtr>()).Add(pointer);
                continue;
            }
            if (state.PoppedAt >= 0f)
            {
                if (Time.time - state.PoppedAt < PopGraceSeconds) continue;
                state.PoppedAt = -1f;
            }
            if (Time.time - state.LastHit < RegrowDelaySeconds.Value) continue;

            state.Scale = Mathf.Min(1f, state.Scale + RegrowPerSecond.Value * dt);
            Apply(state);
            if (state.Scale >= 1f) (done ??= new List<IntPtr>()).Add(pointer);
        }
        if (done == null) return;
        foreach (var pointer in done) shrunk.Remove(pointer);
    }

    private static void RegrowLocks(float dt)
    {
        if (shrunkLocks.Count == 0) return;
        List<IntPtr>? done = null;
        foreach (var (pointer, state) in shrunkLocks)
        {
            if (state.Lock == null || state.Body == null)
            {
                (done ??= new List<IntPtr>()).Add(pointer);
                continue;
            }
            // Popped here: done once the break lands, or after the grace if it never does (it's
            // already back at its own size).
            if (state.PoppedAt >= 0f)
            {
                if (state.Lock.Health <= 0f || Time.time - state.PoppedAt >= PopGraceSeconds)
                    (done ??= new List<IntPtr>()).Add(pointer);
                continue;
            }
            // A teammate's copy: the shooter broke it (the break syncs, the shrink doesn't), so it
            // pops here too, back at its own size, instead of the broken lock regrowing.
            if (state.Lock.Health <= 0f)
            {
                try
                {
                    state.Body.localScale = state.BaseScale;
                    PopEffect(state.Body.position);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons shrink beam couldn't pop a teammate's lock: {e.Message}");
                }
                (done ??= new List<IntPtr>()).Add(pointer);
                continue;
            }
            if (Time.time - state.LastHit < RegrowDelaySeconds.Value) continue;
            state.Scale = Mathf.Min(1f, state.Scale + RegrowPerSecond.Value * dt);
            state.Body.localScale = state.BaseScale * state.Scale;
            if (state.Scale >= 1f) (done ??= new List<IntPtr>()).Add(pointer);
        }
        if (done == null) return;
        foreach (var pointer in done) shrunkLocks.Remove(pointer);
    }

    // Once an enemy's shrunk to StepOnScale, getting within StepOnRadius and looking down at it gibs
    // it - the local player stepped on it. Each client checks this for its own local player only, same
    // as the regrow above: no network message needed, the kill still goes through the bullet path.
    internal static void CheckStepOn()
    {
        if (shrunk.Count == 0) return;
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local == null || !local.Alive || FocusStateManager.CurrentState != eFocusState.FPS) return;
        var camera = local.FPSCamera;
        if (camera == null) return;

        List<Shrunk>? stepped = null;
        foreach (var state in shrunk.Values)
        {
            if (state.PoppedAt >= 0f || state.Scale > StepOnScale.Value || state.Enemy == null || !state.Enemy.Alive) continue;
            var feet = state.Enemy.transform.position;
            if (Vector3.Distance(local.Position, feet) > StepOnRadius.Value) continue;
            if (Vector3.Angle(camera.Forward, feet - camera.Position) > StepOnLookConeDegrees.Value) continue;
            (stepped ??= new List<Shrunk>()).Add(state);
        }
        if (stepped == null) return;
        foreach (var state in stepped) Pop(local, state, GunInjector.ShrinkBeamId);
    }

    // The living enemy nearest `point` within `radius` - how a receiving client finds the same
    // enemy the shooter's beam is on, since scale isn't networked but world position is.
    [HideFromIl2Cpp]
    private static EnemyAgent? NearestEnemy(Vector3 point, float radius)
    {
        EnemyAgent? best = null;
        var bestDistance = float.MaxValue;
        foreach (var collider in Physics.OverlapSphere(point, radius, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (enemy == null || !enemy.Alive) continue;
            var distance = (enemy.AimTarget.position - point).sqrMagnitude;
            if (distance >= bestDistance) continue;
            best = enemy;
            bestDistance = distance;
        }
        return best;
    }

    // Plain fields so GTFO-API can copy them as raw bytes.
    private readonly struct BeamMessage
    {
        public const byte HitNothing = 0, HitEnemy = 1, HitLock = 2;
        public readonly ulong Shooter; // the shooting player's lookup (whose beam this is)
        public readonly float X0, Y0, Z0, X1, Y1, Z1;
        public readonly float Shrink, Floor; // the shooter's share of full size taken this tick, and the scale it stops at
        public readonly byte Hit; // what the shooter's beam is actually on: only that shrinks on teammates' games

        public BeamMessage(Vector3 start, Vector3 end, byte hit, ulong shooter, float shrink, float floor)
        {
            Shooter = shooter;
            (X0, Y0, Z0) = (start.x, start.y, start.z);
            (X1, Y1, Z1) = (end.x, end.y, end.z);
            (Shrink, Floor) = (shrink, floor);
            Hit = hit;
        }
    }

    // The pre-v2 layout, unchanged, for games on an older version.
    private readonly struct LegacyBeamMessage
    {
        public readonly float X0, Y0, Z0, X1, Y1, Z1;
        public readonly byte Hit;

        public LegacyBeamMessage(Vector3 start, Vector3 end, byte hit)
        {
            (X0, Y0, Z0) = (start.x, start.y, start.z);
            (X1, Y1, Z1) = (end.x, end.y, end.z);
            Hit = hit;
        }
    }
}

/// <summary>
/// The beam's look and sound: Chain Lightning's old persistent-stream tether, recolored green and
/// drawn as a single swaying line (no jumps - the Shrink Beam only ever holds on one target) - one
/// unbroken ray per shooter for as long as it's fed, re-swayed every frame so it crackles, lingering
/// only a moment past the last tick so it ends when the trigger is released. A continuous hum plays
/// at the gun for as long as it's fed, same as the laser beams' and Chain Lightning's own loops.
/// </summary>
internal sealed class ShrinkStream : MonoBehaviour
{
    private const float LingerSeconds = 0.2f;
    private const int Strands = 3;
    private const int PointsPerStrand = 24;
    private const float Wiggle = 0.012f;
    private const float StartRadius = 0.035f; // around the barrel
    private const float EndRadius = 0.01f;    // drawn together at the target
    private const float SpinDegreesPerSecond = 540f;
    private const float TwistDegreesPerMetre = 90f;
    private static readonly Color BeamColor = new(0.35f, 1f, 0.3f, 0.85f);
    private static Material? material;

    private static readonly Dictionary<ulong, ShrinkStream> streams = new();

    private LineRenderer[] lines = null!;
    private Vector3 start, end;
    private ulong source;
    private float lastFed;

    public ShrinkStream(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Spawn(Vector3 start, Vector3 end, ulong source = 0)
    {
        // A continuous hum at the gun (where the player hears it) while the beam keeps coming.
        LoopSounds.Touch($"shrink:{source}", Sounds.ShrinkLoopStart.Value, Sounds.ShrinkLoopStop.Value, start);

        if (!streams.TryGetValue(source, out var stream) || stream == null)
        {
            stream = new GameObject("GTFO.EZ.AbsurdWeapons Shrink Beam").AddComponent<ShrinkStream>();
            stream.source = source;
            stream.lines = new LineRenderer[Strands];
            for (var i = 0; i < Strands; i++)
            {
                var strand = new GameObject("strand");
                strand.transform.SetParent(stream.transform, false);
                var line = strand.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = PointsPerStrand;
                if (material == null) material = new Material(Shader.Find("Sprites/Default")); // Unity's null: rebuilt if unloaded
                line.material = material;
                line.startWidth = 0.012f;
                line.endWidth = 0.018f;
                line.startColor = line.endColor = BeamColor;
                stream.lines[i] = line;
            }
            streams[source] = stream;
        }
        stream.start = start;
        stream.end = end;
        stream.lastFed = Time.time;
        stream.Draw();
    }

    private void Update()
    {
        if (Time.time - lastFed > LingerSeconds)
        {
            Destroy(gameObject);
            return;
        }
        try
        {
            Draw();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons shrink beam stream failed: {e.Message}");
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (streams.TryGetValue(source, out var current) && current == this) streams.Remove(source);
    }

    // Three thin strands corkscrewing around the beam's line, spinning around the barrel and
    // drawn together at the target, each jittering on its own.
    [HideFromIl2Cpp]
    private void Draw()
    {
        var axis = end - start;
        var length = axis.magnitude;
        if (length < 0.001f) return;
        var forward = axis / length;
        var right = Vector3.Cross(forward, Mathf.Abs(forward.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
        var up = Vector3.Cross(right, forward);
        var spin = Time.time * SpinDegreesPerSecond;
        for (var s = 0; s < Strands; s++)
        {
            var phase = spin + s * 360f / Strands;
            for (var i = 0; i < PointsPerStrand; i++)
            {
                var t = i / (float)(PointsPerStrand - 1);
                var angle = (phase + t * length * TwistDegreesPerMetre) * Mathf.Deg2Rad;
                var radius = Mathf.Lerp(StartRadius, EndRadius, t);
                var offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                var jitter = i == 0 || i == PointsPerStrand - 1 ? Vector3.zero : UnityEngine.Random.insideUnitSphere * Wiggle;
                lines[s].SetPosition(i, start + axis * t + offset + jitter);
            }
        }
    }
}

/// <summary>
/// Drives the local player's Shrink Beam from the trigger: while the gun is in hand and Fire is
/// held, a tick every <see cref="ShrinkBeam.GunShotDelay"/>.
/// </summary>
internal sealed class ShrinkBeamDriver : MonoBehaviour
{
    private float nextTick;

    public ShrinkBeamDriver(IntPtr ptr) : base(ptr) { }

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
            if (gun == null || archetype == null || archetype.persistentID != GunInjector.ShrinkBeamId) return;
            if (!InputMapper.GetButtonKeyMouse(InputAction.Fire, eFocusState.FPS)) return;
            var camera = player.FPSCamera;
            if (camera == null) return;

            // Lit (and locked to the barrel) every frame the trigger is held; the shrink itself
            // only lands on the throttled ticks.
            // Ammo goes by the time the trigger is held, every frame.
            if (!InfiniteAmmo.HasStreamAmmo(player) || !InfiniteAmmo.Drain(player, Time.deltaTime)) return; // out of ammo: no beam
            var tick = ShrinkBeam.TickSeconds;
            var applyShrink = Time.time >= nextTick;
            if (applyShrink) nextTick = Time.time + tick;
            ShrinkBeam.Tick(player, camera.Position, camera.Forward, gun.MuzzleAlign, tick, GunInjector.ShrinkBeamId, applyShrink);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons shrink beam driver failed: {e.Message}");
            nextTick = Time.time + 1f;
        }
    }
}

/// <summary>
/// Always-on: regrows every tracked shrunk enemy and padlock that's fallen off the beam, and pops any enemies
/// small enough for the local player to have just stepped on.
/// </summary>
internal sealed class ShrinkRegrowth : MonoBehaviour
{
    public ShrinkRegrowth(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            ShrinkBeam.Regrow(Time.deltaTime);
            ShrinkBeam.CheckStepOn();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons shrink regrowth failed: {e.Message}");
        }
    }
}
