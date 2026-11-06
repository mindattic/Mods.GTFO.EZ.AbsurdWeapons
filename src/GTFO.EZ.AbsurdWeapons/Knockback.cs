using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using GTFO.API;
using MindAttic.GTFO.Shared;
using Il2CppInterop.Runtime.Attributes;
using Player;
using SNetwork;
using UnityEngine;
using UnityEngine.AI;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Physical knockback: slides an enemy across its walkable ground, fast then easing off, and stops
/// at walls and ledges (a NavMesh raycast) so nothing is shoved through geometry. Enemy positions
/// belong to the host, so the host does the sliding: a client's knockback is sent to the host,
/// which finds the enemy at that spot and pushes it. Everyone then sees it through the game's own
/// enemy position sync.
/// A slide can also slam: an enemy flung into a wall with distance still to go takes damage for
/// the distance it didn't get to travel - fall damage, sideways.
/// </summary>
internal static class Knockback
{
    private const string KnockEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.Knockback.v2";
    private const string LegacyKnockEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.Knockback";

    internal static ConfigEntry<float> Sharpness = null!;
    internal static ConfigEntry<float> MaxSeconds = null!;
    internal static ConfigEntry<float> SlamFreeMeters = null!;
    internal static ConfigEntry<float> SlamStagger = null!;

    internal static void Bind(ConfigFile config)
    {
        Sharpness = config.Bind("Knockback", "Sharpness", 14f, "How quickly a slide covers its distance (ease-out rate).");
        MaxSeconds = config.Bind("Knockback", "MaxSeconds", 0.6f, "A slide is cut short after this many seconds regardless.");
        SlamFreeMeters = config.Bind("Knockback", "SlamFreeMeters", 1f, "Slides ending within this of their full distance take no wall-slam damage.");
        SlamStagger = config.Bind("Knockback", "SlamStagger", 3f, "Stagger multiplier of a wall slam.");
    }

    internal static void Init()
    {
        // The attacker the request names gets the credit for any wall slam.
        Wire.Register<KnockMessage>(KnockEvent, (sender, message) =>
            OnRequest(message.Knock, PlayerFor(Wire.Actor(message.Attacker, sender))));
        // An older version's request: its sender is the attacker.
        Wire.RegisterLegacy<LegacyKnockMessage>(LegacyKnockEvent, (sender, message) => OnRequest(message, PlayerFor(sender)));
    }

    // On the host: a client's knockback, carried out.
    private static void OnRequest(LegacyKnockMessage message, PlayerAgent? attacker)
    {
        if (!SNet.IsMaster) return;
        var enemy = message.Enemy.TryGet(out var named) && named != null ? named : EnemyNear(new Vector3(message.X, message.Y, message.Z));
        if (enemy != null)
            KnockbackSlides.Start(enemy, new Vector3(message.DirX, 0f, message.DirZ), message.Distance, message.SlamDamagePerMeter,
                attacker, message.GearCategoryId);
    }

    // Pushes `enemy` `distance` metres along `direction` (flattened to the ground). With a slam
    // damage, hitting a wall early hurts: that much per metre of the slide left over.
    // `attacker` and `gearCategoryId` are credited with any wall slam.
    internal static void Push(EnemyAgent enemy, Vector3 direction, float distance, float slamDamagePerMeter = 0f,
        PlayerAgent? attacker = null, uint gearCategoryId = 0)
    {
        if (enemy == null || !enemy.Alive || distance <= 0f) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        if (SNet.IsMaster)
        {
            KnockbackSlides.Start(enemy, direction, distance, slamDamagePerMeter, attacker, gearCategoryId);
            return;
        }
        // Only the host moves enemies, so only the host is asked.
        var master = SNet.Master;
        if (master == null) return;
        var knock = new LegacyKnockMessage(enemy, direction, distance, slamDamagePerMeter, gearCategoryId);
        Wire.SendTo(master, KnockEvent, new KnockMessage(knock, Wire.LookupOf(attacker)), LegacyKnockEvent, knock);
        NetPresence.WarnIfHostLacksMod("knockback");
    }

    // The player agent of the player with this Steam id (SNet_Player.Lookup).
    internal static PlayerAgent? PlayerFor(ulong lookup)
    {
        foreach (var player in PlayerManager.PlayerAgentsInLevel)
            if (player != null && player.Owner != null && player.Owner.Lookup == lookup) return player;
        return null;
    }

    private static EnemyAgent? EnemyNear(Vector3 position)
    {
        EnemyAgent? best = null;
        var bestDistance = float.MaxValue;
        foreach (var collider in Physics.OverlapSphere(position, 2f, LayerManager.MASK_ENEMY_DAMAGABLE))
        {
            var enemy = collider.GetComponent<Dam_EnemyDamageLimb>()?.m_base?.Owner;
            if (enemy == null || !enemy.Alive) continue;
            var distance = (enemy.Position - position).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = enemy; }
        }
        return best;
    }

    // Plain fields so GTFO-API can copy it as raw bytes: the old request, plus who knocked it back.
    private readonly struct KnockMessage
    {
        public readonly ulong Attacker; // the attacking player's lookup
        public readonly LegacyKnockMessage Knock;

        public KnockMessage(LegacyKnockMessage knock, ulong attacker) => (Knock, Attacker) = (knock, attacker);
    }

    // The pre-v2 layout, unchanged, for games on an older version (the sender is the attacker). The
    // enemy goes by its network id; its position is a fallback for when the host can't resolve the id.
    private readonly struct LegacyKnockMessage
    {
        public readonly pEnemyAgent Enemy;
        public readonly float X, Y, Z, DirX, DirZ, Distance, SlamDamagePerMeter;
        public readonly uint GearCategoryId;

        public LegacyKnockMessage(EnemyAgent enemy, Vector3 direction, float distance, float slamDamagePerMeter, uint gearCategoryId)
        {
            SlamDamagePerMeter = slamDamagePerMeter;
            GearCategoryId = gearCategoryId;
            var named = new pEnemyAgent();
            named.Set(enemy);
            Enemy = named;
            var position = enemy.Position;
            (X, Y, Z) = (position.x, position.y, position.z);
            (DirX, DirZ) = (direction.x, direction.z);
            Distance = distance;
        }
    }
}

/// <summary>Runs the knockback slides in progress, a little each frame.</summary>
internal sealed class KnockbackSlides : MonoBehaviour
{
    private const int AllAreas = -1; // NavMesh.AllAreas, missing from the interop build

    private sealed class Slide
    {
        public EnemyAgent Enemy = null!;
        public Vector3 Direction;
        public float Remaining, Age, SlamDamagePerMeter;
        public PlayerAgent? Attacker;
        public uint GearCategoryId;
    }

    private static readonly Dictionary<IntPtr, Slide> slides = new();

    public KnockbackSlides(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Start(EnemyAgent enemy, Vector3 direction, float distance, float slamDamagePerMeter = 0f,
        PlayerAgent? attacker = null, uint gearCategoryId = 0)
    {
        // A new hit replaces a slide still running on the same enemy.
        slides[enemy.Pointer] = new Slide
        {
            Enemy = enemy, Direction = direction.normalized, Remaining = distance, SlamDamagePerMeter = slamDamagePerMeter,
            Attacker = attacker, GearCategoryId = gearCategoryId,
        };
    }

    private void Update()
    {
        if (slides.Count == 0) return;
        var finished = new List<IntPtr>();
        foreach (var (key, slide) in slides)
        {
            try
            {
                var enemy = slide.Enemy;
                slide.Age += Time.deltaTime;
                if (enemy == null || !enemy.Alive || slide.Remaining < 0.02f || slide.Age > Knockback.MaxSeconds.Value)
                {
                    finished.Add(key);
                    continue;
                }

                var step = slide.Remaining * (1f - Mathf.Exp(-Knockback.Sharpness.Value * Time.deltaTime));
                var from = enemy.Position;
                var to = from + slide.Direction * step;
                if (!NavMesh.SamplePosition(from, out var fromHit, 1f, AllAreas)) { finished.Add(key); continue; }
                // Stop at the edge of walkable ground (walls, ledges).
                if (NavMesh.Raycast(fromHit.position, to, out var blocked, AllAreas))
                {
                    to = blocked.position;
                    var unspent = slide.Remaining - Vector3.Distance(fromHit.position, blocked.position);
                    var slam = Formulas.SlamDamage(unspent, slide.SlamDamagePerMeter, Knockback.SlamFreeMeters.Value);
                    if (slam > 0f && WallAhead(blocked.position, slide.Direction))
                        Slam(enemy, blocked.position, slide.Direction, slam, slide.Attacker, slide.GearCategoryId);
                    slide.Remaining = 0f;
                }
                else
                {
                    slide.Remaining -= step;
                }
                if (NavMesh.SamplePosition(to, out var toHit, 1f, AllAreas))
                    enemy.AI.NavmeshAgentWarp(toHit.position);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons knockback slide failed: {e.Message}");
                finished.Add(key);
            }
        }
        foreach (var key in finished) slides.Remove(key);
    }

    // A solid wall just past where the slide stopped, at body height (a ledge or drop has none).
    private static bool WallAhead(Vector3 stop, Vector3 direction) =>
        Physics.Raycast(stop + Vector3.up * 1f, direction, 1.2f, LayerManager.MASK_WORLD);

    // The wall hit: damage and a stagger through the enemy's limb nearest the wall (the bullet
    // path, so it syncs - this runs on the host), a thud of dust and a flash.
    [HideFromIl2Cpp]
    private static void Slam(EnemyAgent enemy, Vector3 wallPoint, Vector3 direction, float damage,
        PlayerAgent? attacker, uint gearCategoryId)
    {
        // Credited to whoever knocked it back (the host's own player if they've left).
        var source = attacker != null ? attacker : PlayerManager.GetLocalPlayerAgent();
        if (source == null) return;
        var nearest = ChainLightning.NearestLimb(enemy, wallPoint);
        if (nearest == null) return;
        var point = nearest.transform.position;
        nearest.BulletDamage(damage, source.Cast<Agents.Agent>(), point, direction, -direction,
            allowDirectionalBonus: false, Knockback.SlamStagger.Value, 1f, gearCategoryId);
        try
        {
            FX_EffectSystem.FX_Manager.PlayEffect(false, FX_EffectSystem.FX_GroupName.Impact_Concrete, point + direction * 0.3f,
                Quaternion.LookRotation(-direction), false);
        }
        catch (Exception)
        {
            // The dust is a nicety.
        }
        Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {enemy.name} slammed into a wall for {damage:0} damage.");
    }
}
