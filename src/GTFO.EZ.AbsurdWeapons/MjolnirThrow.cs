using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using GameData;
using Gear;
using GTFO.API;
using MindAttic.GTFO.Shared;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Mjolnir's throw: release a full charge and the hammer flies from your hand, tumbling end over
/// end, out to 25 m (or the first wall), then comes back to you. Along the way it hits every
/// enemy it passes through, and whenever an enemy is within 3 m of it, it casts chain lightning
/// from the hammer to the nearest one, jumping on to two more. Damage happens on the thrower's game only, through the
/// bullet path; teammates are sent the throw and see the same hammer (and the bolts, which
/// ChainLightning sends).
/// </summary>
[HarmonyPatch]
internal static class MjolnirThrow
{
    internal static ConfigEntry<float> Speed = null!;
    internal static ConfigEntry<float> ReturnSpeed = null!;
    internal static ConfigEntry<float> Reach = null!;
    internal static ConfigEntry<float> SpinDegreesPerSecond = null!;
    internal static ConfigEntry<float> HitRadius = null!;
    internal static ConfigEntry<float> HitDamage = null!;
    internal static ConfigEntry<float> HitStagger = null!;
    internal static ConfigEntry<float> PulseSeconds = null!;
    internal static ConfigEntry<float> PulseRange = null!;
    internal static ConfigEntry<float> PulseDamage = null!;
    internal static ConfigEntry<int> PulseJumps = null!;
    private const float FullChargeLeeway = 0.95f;
    private const string ThrowEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.MjolnirThrow.v2";
    private const string LegacyThrowEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.MjolnirThrow";

    private static float chargeStarted = -1f;

    internal static void Bind(ConfigFile config)
    {
        Speed = config.Bind("Mjolnir", "ThrowSpeed", 26f, "The thrown hammer's outbound speed, m/s.");
        ReturnSpeed = config.Bind("Mjolnir", "ThrowReturnSpeed", 32f, "Its speed flying back to you, m/s.");
        Reach = config.Bind("Mjolnir", "ThrowReach", 25f, "Metres it flies before turning back (or the first wall).");
        SpinDegreesPerSecond = config.Bind("Mjolnir", "ThrowSpin", 1080f, "How fast it tumbles end over end, degrees/second.");
        HitRadius = config.Bind("Mjolnir", "ThrowHitRadius", 0.6f, "How wide a swath the flying hammer hits.");
        HitDamage = config.Bind("Mjolnir", "ThrowHitDamage", 20f, "Damage per enemy hit, each way.");
        HitStagger = config.Bind("Mjolnir", "ThrowHitStagger", 4f, "Stagger multiplier per hit.");
        PulseSeconds = config.Bind("Mjolnir", "ThrowPulseSeconds", 0.12f, "Seconds between the flying hammer's lightning pulses.");
        PulseRange = config.Bind("Mjolnir", "ThrowPulseRange", 3f, "The flying hammer casts chain lightning whenever an enemy is within this many metres of it.");
        PulseDamage = config.Bind("Mjolnir", "ThrowPulseDamage", 6f, "Damage of a pulse's first strike.");
        // Capped like the Chain Lightning Gun's Jumps: the bolt teammates are sent holds that many.
        PulseJumps = config.Bind("Mjolnir", "ThrowPulseJumps", 2,
            new ConfigDescription($"Chain-lightning jumps per pulse, after the first strike (at most {ChainLightning.MaxJumps}).",
                new AcceptableValueRange<int>(0, ChainLightning.MaxJumps)));
    }

    internal static void Init()
    {
        // The thrower the message names (not whichever game sent it), so the hammer flies back to
        // them, at the thrower's own speeds, reach and spin.
        Wire.Register<ThrowMessage>(ThrowEvent, (sender, message) =>
            ThrownHammer.Spawn(null, Knockback.PlayerFor(Wire.Actor(message.Thrower, sender)), new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.DirX, message.DirY, message.DirZ), null,
                new HammerFlight(message.Speed, message.ReturnSpeed, message.Reach, message.Spin)));
        // An older version's throw: its sender is the thrower, and this game's own numbers stand in.
        Wire.RegisterLegacy<LegacyThrowMessage>(LegacyThrowEvent, (sender, message) =>
            ThrownHammer.Spawn(null, Knockback.PlayerFor(sender), new Vector3(message.X, message.Y, message.Z),
                new Vector3(message.DirX, message.DirY, message.DirZ), null, HammerFlight.FromConfig()));
    }

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
    [HarmonyPostfix]
    private static void AfterChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
    {
        try
        {
            var archetype = __instance.MeleeArchetypeData;
            if (archetype == null || archetype.persistentID != MeleeInjector.MjolnirId) return;
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;

            if (newState is eMeleeWeaponState.AttackChargeUpLeft or eMeleeWeaponState.AttackChargeUpRight)
            {
                chargeStarted = Time.time;
                return;
            }
            if (newState is not (eMeleeWeaponState.AttackChargeReleaseLeft or eMeleeWeaponState.AttackChargeReleaseRight)) return;
            if (chargeStarted < 0f) return;
            var held = Time.time - chargeStarted;
            chargeStarted = -1f;

            var animations = GameDataBlockBase<MeleeAnimationSetDataBlock>.GetBlock(archetype.MeleeAnimationSet);
            var full = animations != null ? animations.MaxDamageChargeTime : 1f;
            if (!Formulas.IsFullCharge(held, full, FullChargeLeeway)) return;

            var camera = owner.FPSCamera;
            if (camera == null || ThrownHammer.LocalInFlight) return; // one throw at a time
            var direction = camera.Forward;
            var start = camera.Position + direction * 0.8f + Vector3.down * 0.15f;
            // Up against a wall or door, that point is already through it: start short of it instead
            // (the flight's own wall check then turns it straight back).
            if (Physics.Linecast(camera.Position, start, out var blocked, LayerManager.MASK_WORLD))
                start = blocked.point - (start - camera.Position).normalized * 0.2f;
            var flight = HammerFlight.FromConfig();
            ThrownHammer.Spawn(owner, owner, start, direction, __instance.gameObject, flight);
            Wire.Send(ThrowEvent, new ThrowMessage(start, direction, Wire.LookupOf(owner), flight),
                LegacyThrowEvent, new LegacyThrowMessage(start, direction));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Mjolnir throw failed: {e}");
        }
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct ThrowMessage
    {
        public readonly ulong Thrower; // the throwing player's lookup
        public readonly float X, Y, Z, DirX, DirY, DirZ;
        public readonly float Speed, ReturnSpeed, Reach, Spin; // the thrower's

        public ThrowMessage(Vector3 start, Vector3 direction, ulong thrower, HammerFlight flight)
        {
            Thrower = thrower;
            (X, Y, Z) = (start.x, start.y, start.z);
            (DirX, DirY, DirZ) = (direction.x, direction.y, direction.z);
            (Speed, ReturnSpeed, Reach, Spin) = (flight.Speed, flight.ReturnSpeed, flight.Reach, flight.Spin);
        }
    }

    // The pre-v2 layout, unchanged, for games on an older version.
    private readonly struct LegacyThrowMessage
    {
        public readonly float X, Y, Z, DirX, DirY, DirZ;

        public LegacyThrowMessage(Vector3 start, Vector3 direction)
        {
            (X, Y, Z) = (start.x, start.y, start.z);
            (DirX, DirY, DirZ) = (direction.x, direction.y, direction.z);
        }
    }
}

/// <summary>How one thrown hammer flies: the thrower's speeds, reach and spin, fixed at the throw.</summary>
internal readonly struct HammerFlight
{
    public readonly float Speed, ReturnSpeed, Reach, Spin;

    public HammerFlight(float speed, float returnSpeed, float reach, float spin) =>
        (Speed, ReturnSpeed, Reach, Spin) = (speed, returnSpeed, reach, spin);

    internal static HammerFlight FromConfig() =>
        new(MjolnirThrow.Speed.Value, MjolnirThrow.ReturnSpeed.Value, MjolnirThrow.Reach.Value, MjolnirThrow.SpinDegreesPerSecond.Value);
}

/// <summary>
/// One thrown Mjolnir: the hammer's meshes, copied from the held weapon (or a stand-in), flown out
/// and back by hand, spinning end over end with a blue glow.
/// </summary>
internal sealed class ThrownHammer : MonoBehaviour
{
    private static readonly Color GlowColor = new(0.45f, 0.75f, 1f);

    private PlayerAgent? owner;   // the thrower on their own game (deals damage); null on teammates' copies
    private PlayerAgent? thrower; // whose hand it returns to
    private HammerFlight flight;  // the thrower's speeds, reach and spin, on every copy
    private Vector3 origin, direction, spinAxis;
    private bool returning;
    private float travelled, nextPulse, age;
    private readonly HashSet<IntPtr> hitOut = new(), hitBack = new();
    private EffectLight glow = null!;

    // The local player's own throw is out (the next can't start until it's back).
    internal static bool LocalInFlight => localThrow != null;
    private static ThrownHammer? localThrow;

    public ThrownHammer(IntPtr ptr) : base(ptr) { }

    private void OnDestroy()
    {
        if (localThrow == this) localThrow = null;
    }

    [HideFromIl2Cpp]
    internal static void Spawn(PlayerAgent? owner, PlayerAgent? thrower, Vector3 start, Vector3 direction, GameObject? heldModel, HammerFlight flight)
    {
        var go = new GameObject("GTFO.EZ.AbsurdWeapons Thrown Mjolnir");
        go.transform.position = start;
        go.transform.rotation = Quaternion.LookRotation(direction);
        CopyModel(heldModel, go.transform);

        var hammer = go.AddComponent<ThrownHammer>();
        hammer.owner = owner;
        hammer.thrower = thrower;
        hammer.flight = flight;
        if (owner != null) localThrow = hammer;
        hammer.origin = start;
        hammer.direction = direction.normalized;
        var right = Vector3.Cross(Vector3.up, hammer.direction);
        hammer.spinAxis = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right; // end over end

        var lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(go.transform, false);
        hammer.glow = lightObject.AddComponent<EffectLight>();
        hammer.glow.Color = GlowColor;
        hammer.glow.Range = 4f;
        hammer.glow.Intensity = 4f;
        hammer.glow.enabled = true;
    }

    private void Update()
    {
        try
        {
            var dt = Time.deltaTime;
            age += dt;
            var position = transform.position;

            Vector3 next;
            if (!returning)
            {
                var step = flight.Speed * dt;
                next = position + direction * step;
                travelled += step;
                if (Physics.Linecast(position, next, out var wall, LayerManager.MASK_WORLD))
                {
                    next = wall.point - direction * 0.2f;
                    returning = true;
                }
                else if (travelled >= flight.Reach) returning = true;
            }
            else
            {
                // Home back to the thrower's hand (or where it was thrown from).
                var hand = thrower != null && thrower.Alive ? thrower.Position + Vector3.up * 1.3f : origin;
                var toHand = hand - position;
                var step = flight.ReturnSpeed * dt;
                if (toHand.magnitude <= Mathf.Max(step, 1f) || age > 6f)
                {
                    Destroy(gameObject);
                    return;
                }
                next = position + toHand.normalized * step;
            }

            if (owner != null) Strike(position, next);
            transform.position = next;
            transform.Rotate(spinAxis, flight.Spin * dt, Space.World);
            glow.Intensity = 3f + Mathf.PerlinNoise(age * 20f, 0f) * 4f;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons thrown Mjolnir failed: {e.Message}");
            Destroy(gameObject);
        }
    }

    // On the thrower's game: every enemy the hammer passes through (once each way), and a
    // lightning pulse every so often.
    [HideFromIl2Cpp]
    private void Strike(Vector3 from, Vector3 to)
    {
        var hitSet = returning ? hitBack : hitOut;
        var line = to - from;
        var length = line.magnitude;
        if (length > 0.0001f)
        {
            foreach (var hit in Physics.SphereCastAll(from, MjolnirThrow.HitRadius.Value, line / length, length, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                var limb = hit.collider.GetComponent<Dam_EnemyDamageLimb>();
                var enemy = limb?.m_base?.Owner;
                if (limb == null || enemy == null || !enemy.Alive || hitSet.Contains(enemy.Pointer)) continue;
                var point = hit.point == Vector3.zero ? limb.transform.position : hit.point;
                // The hit sphere is wide: an enemy it brushes on the far side of a door or thin wall
                // isn't struck (seen from the nearest point of the hammer's path).
                var along = Mathf.Clamp(Vector3.Dot(point - from, line / length), 0f, length);
                if (Physics.Linecast(from + line / length * along, point, LayerManager.MASK_WORLD)) continue;
                if (!hitSet.Add(enemy.Pointer)) continue;
                limb.BulletDamage(MjolnirThrow.HitDamage.Value, owner!.Cast<Agent>(), point, line / length, -line / length,
                    allowDirectionalBonus: false, MjolnirThrow.HitStagger.Value, 1f, MeleeInjector.MjolnirId);
            }
            LockDamage.Line(from, line / length, length, MjolnirThrow.HitDamage.Value, owner!.Cast<Agent>());
        }

        if (age < nextPulse) return;
        nextPulse = age + MjolnirThrow.PulseSeconds.Value;
        ChainLightning.Pulse(owner!, to, MjolnirThrow.PulseRange.Value, MjolnirThrow.PulseDamage.Value, MjolnirThrow.PulseJumps.Value, MeleeInjector.MjolnirId);
    }

    // The held hammer's meshes (positioned relative to the weapon's root), drawn on the default
    // layer; a stand-in block if there's nothing to copy (a teammate's game). They go under a
    // holder of their own, turned so the hammer tumbles end over end (OrientEndOverEnd).
    [HideFromIl2Cpp]
    private static void CopyModel(GameObject? source, Transform parent)
    {
        var model = new GameObject("Model").transform;
        model.SetParent(parent, false);
        CopyParts(source, model);
        OrientEndOverEnd(model);
    }

    [HideFromIl2Cpp]
    private static void CopyParts(GameObject? source, Transform parent)
    {
        var copied = 0;
        if (source != null)
        {
            var root = source.transform;
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
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
        }
        if (copied > 0) return;

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.Object.Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(parent, false);
        head.transform.localScale = new Vector3(0.18f, 0.18f, 0.32f);
        var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        UnityEngine.Object.Destroy(handle.GetComponent<Collider>());
        handle.transform.SetParent(parent, false);
        handle.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        handle.transform.localScale = new Vector3(0.05f, 0.2f, 0.05f);
    }

    // The hammer spins about its own right (local X) while flying along its forward. For a clean
    // end-over-end tumble its handle has to lie along local up and its head face the flight
    // (local forward), so it's seen side-on as it turns. The held weapon's root guarantees neither,
    // and a box around the whole model in that root's axes is skewed when the handle sits at an
    // angle - so the axes come from the meshes' own: the handle is the longest extent of any single
    // mesh, the head the longest extent across it (of the other meshes, or the same one's next
    // longest). Then the whole thing is centered, so it tumbles about its middle, not the grip.
    [HideFromIl2Cpp]
    private static void OrientEndOverEnd(Transform model)
    {
        var filters = model.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter? handleMesh = null;
        var handle = Vector3.zero;
        var handleLength = 0f;
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            var (axis, length) = LongestAxis(model, filter, null);
            if (length > handleLength) { handleLength = length; handle = axis; handleMesh = filter; }
        }
        if (handleMesh == null) return;

        // The head: the longest extent across the handle, among the other meshes; a one-piece model
        // uses its own next-longest axis.
        var head = Vector3.zero;
        var headLength = 0f;
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null || filter == handleMesh) continue;
            var (axis, length) = LongestAxis(model, filter, handle);
            if (length > headLength) { headLength = length; head = axis; }
        }
        if (headLength <= 0f) (head, _) = LongestAxis(model, handleMesh, handle);
        head = Vector3.ProjectOnPlane(head, handle);
        if (head.sqrMagnitude < 0.0001f) head = Vector3.Cross(handle, Vector3.right).sqrMagnitude > 0.0001f ? Vector3.Cross(handle, Vector3.right) : Vector3.forward;

        // Handle onto up, head onto forward.
        var turn = Quaternion.Inverse(Quaternion.LookRotation(head.normalized, handle.normalized));
        model.localRotation = turn;

        // Center it: the box around every mesh, now that it's turned.
        var found = false;
        var bounds = default(Bounds);
        foreach (var filter in filters)
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            var meshBounds = mesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                var local = model.parent.InverseTransformPoint(filter.transform.TransformPoint(point));
                if (!found) bounds = new Bounds(local, Vector3.zero);
                else bounds.Encapsulate(local);
                found = true;
            }
        }
        if (found) model.localPosition -= bounds.center;
        Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons thrown Mjolnir oriented: handle {handle.normalized} ({handleLength:0.00} m), head {head.normalized} ({headLength:0.00} m), {filters.Length} meshes.");
    }

    // A mesh's longest axis in its own space (scaled to real size), as a direction in `model`'s
    // space, and its length. With `across`, only axes not lined up with it count.
    [HideFromIl2Cpp]
    private static (Vector3 Axis, float Length) LongestAxis(Transform model, MeshFilter filter, Vector3? across)
    {
        var size = filter.sharedMesh.bounds.size;
        var best = (Axis: Vector3.zero, Length: 0f);
        foreach (var local in new[] { Vector3.right * size.x, Vector3.up * size.y, Vector3.forward * size.z })
        {
            var inModel = model.InverseTransformVector(filter.transform.TransformVector(local));
            if (across.HasValue && Mathf.Abs(Vector3.Dot(inModel.normalized, across.Value.normalized)) > 0.7f) continue;
            if (inModel.magnitude > best.Length) best = (inModel, inModel.magnitude);
        }
        return best;
    }
}
