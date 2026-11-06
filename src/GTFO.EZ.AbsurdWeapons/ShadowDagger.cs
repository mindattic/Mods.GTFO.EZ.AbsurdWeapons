using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Enemies;
using Gear;
using GTFO.API;
using MindAttic.GTFO.Shared;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Shadow Dagger: lightning-fast stabs (its light attacks play 2.5x as fast; WeaponSettings),
/// and a fully charged hit that lands on an enemy hides its wielder for <see cref="Seconds"/>. While
/// hidden, every kill adds <see cref="KillTimeBonus"/> and every enemy hit drains the cap on how
/// high the time can grow (<see cref="CapDrainPerHit"/>), so it always ends; the cap resets to
/// <see cref="MaxSeconds"/> once it does. While hidden, enemies can't detect
/// them by sight or noise, can't lock on or be alerted to them, and drop them as a target if they
/// already had them. Enemy AI runs on the host, so the hit is sent to everyone over GTFO-API and
/// every game keeps the same list of who is hidden. Hidden players fade to <see cref="Transparency"/>, coming
/// back in as their time runs out (see <see cref="StealthVisuals"/>). Putting the dagger away - wielding anything else - ends
/// it at once.
/// </summary>
[HarmonyPatch]
internal static class Stealth
{
    internal static ConfigEntry<float> Seconds = null!;
    internal static ConfigEntry<float> KillTimeBonus = null!;
    internal static ConfigEntry<float> MaxSeconds = null!;
    internal static ConfigEntry<float> CapDrainPerHit = null!;
    internal static ConfigEntry<float> BonusPerKill = null!;
    internal static ConfigEntry<float> MaxBonus = null!;
    internal static ConfigEntry<float> Transparency = null!;
    internal static ConfigEntry<float> WarningSeconds = null!;

    // A charged hit counts as full when it deals at least this share of the full charged damage.
    private const float FullChargeShare = 0.95f;
    private const string StealthEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.Stealth.v2";
    private const string LegacyStealthEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.Stealth";

    // Keyed by the player's Steam id (SNet_Player.Lookup): when their stealth ends, in Time.time.
    private static readonly Dictionary<ulong, float> hiddenUntil = new();

    internal static void Bind(ConfigFile config)
    {
        Seconds = config.Bind("Shadow Dagger", "StealthSeconds", 3f, "Seconds a fully charged hit hides you for (up to the current cap while already hidden).");
        KillTimeBonus = config.Bind("Shadow Dagger", "StealthKillTimeBonus", 0.33f, "Seconds added to the stealth for every kill made while hidden, up to the current cap.");
        MaxSeconds = config.Bind("Shadow Dagger", "StealthMaxSeconds", 3f, "The cap on stealth time when a stealth starts. It drains with every enemy hit while hidden, and resets when the stealth ends.");
        CapDrainPerHit = config.Bind("Shadow Dagger", "StealthCapDrainPerHit", 0.05f,
            new ConfigDescription("Share of the current cap lost with every enemy hit while hidden, kill or not (0.05 = 5%), so a stealth always ends eventually.",
                new AcceptableValueRange<float>(0f, 1f)));
        BonusPerKill = config.Bind("Shadow Dagger", "StealthBonusPerKill", 0.25f, "Damage bonus (0.25 = +25%) added per kill made while hidden.");
        MaxBonus = config.Bind("Shadow Dagger", "StealthMaxBonus", 1f, "Cap on the stacked kill bonus (1 = +100%).");
        Transparency = config.Bind("Shadow Dagger", "StealthTransparency", 0.95f,
            new ConfigDescription("How see-through you are at full stealth (0.95 = 5% visible). Visual only - enemies can't see you either way.",
                new AcceptableValueRange<float>(0f, 1f)));
        WarningSeconds = config.Bind("Shadow Dagger", "StealthWarningSeconds", 1f,
            "With this many seconds of stealth left you start flickering back in, faster and faster, as a warning. 0 turns the flicker off.");
    }

    internal static void Init()
    {
        // The hidden player the message names, not whichever game sent it.
        Wire.Register<StealthMessage>(StealthEvent, (sender, message) => Hide(Wire.Actor(message.Player, sender), message.Seconds));
        // An older version's: its sender is the hidden player.
        Wire.RegisterLegacy<LegacyStealthMessage>(LegacyStealthEvent, (sender, message) => Hide(sender, message.Seconds));
    }

    private static void Broadcast(ulong player, float seconds)
    {
        Wire.Send(StealthEvent, new StealthMessage(player, seconds), LegacyStealthEvent, new LegacyStealthMessage(seconds));
        // Enemies ignoring the hidden player is the host's enemy AI: without the mod there, nothing hides.
        if (seconds > 0f && !SNetwork.SNet.IsMaster) NetPresence.WarnIfHostLacksMod("Shadow Dagger stealth");
    }

    internal static bool IsHidden(Agent? agent) => Remaining(agent) > 0f;

    // Seconds of stealth left (0 when not hidden).
    internal static float Remaining(Agent? agent)
    {
        if (agent == null || hiddenUntil.Count == 0) return 0f;
        var player = agent.TryCast<PlayerAgent>();
        if (player == null || player.Owner == null || !hiddenUntil.TryGetValue(player.Owner.Lookup, out var until)) return 0f;
        var left = until - Time.time;
        if (left > 0f) return left;
        hiddenUntil.Remove(player.Owner.Lookup); // ran out: off the list
        return 0f;
    }

    private static void Hide(ulong player, float seconds)
    {
        if (seconds > 0f) hiddenUntil[player] = Time.time + seconds;
        else hiddenUntil.Remove(player);
        Plugin.Logger.LogInfo(seconds > 0f
            ? $"GTFO.EZ.AbsurdWeapons: player {player} is hidden from enemies for {seconds:0} s."
            : $"GTFO.EZ.AbsurdWeapons: player {player} is visible again.");
    }

    // The stealth lasts only while the dagger is in hand: the local player wielding anything else
    // ends it, here and (by message) on every other game.
    internal static void EndIfDaggerPutAway(PlayerAgent player)
    {
        if (!player.IsLocallyOwned || player.Owner == null || Remaining(player) <= 0f) return;
        var wielded = player.Inventory?.WieldedItem;
        if (wielded == null || wielded.GearCategoryData?.persistentID == MeleeInjector.ShadowDaggerId) return;
        Hide(player.Owner.Lookup, 0f);
        Broadcast(player.Owner.Lookup, 0f);
    }

    // (Fast stabs: the dagger's light attacks are 2.5x as fast - MeleeInjector / SwordTempo.)

    // ---- While hidden: every enemy hit drains the cap; every kill adds damage and time ----

    private const float KillCheckDelay = 0.4f; // the kill is confirmed by the host; look a moment later

    private static float damageBonus;      // the local player's, while hidden
    private static float cap = -1f;        // the local player's current stealth cap, while hidden (-1: not started)
    private static readonly List<(EnemyAgent Enemy, float CheckAt)> pendingKills = new();

    private static float Cap => cap >= 0f ? cap : MaxSeconds.Value;

    private static void SetLocal(PlayerAgent local, float seconds)
    {
        if (local.Owner == null) return;
        Hide(local.Owner.Lookup, seconds);
        Broadcast(local.Owner.Lookup, seconds);
    }

    // One check per enemy, however many times it was hit: a kill counts once.
    private static void QueueKillCheck(EnemyAgent enemy)
    {
        pendingKills.RemoveAll(p => p.Enemy != null && p.Enemy.Pointer == enemy.Pointer);
        pendingKills.Add((enemy, Time.time + KillCheckDelay));
    }

    // Kills confirmed since they were struck: the damage bonus grows and the stealth stretches by
    // KillTimeBonus, up to the cap. Called every frame (StealthVisuals); a stealth that's ended
    // resets everything, the cap included.
    internal static void ProcessKills(PlayerAgent local)
    {
        if (Remaining(local) <= 0f)
        {
            damageBonus = 0f;
            cap = -1f;
            pendingKills.Clear();
            return;
        }
        for (var i = pendingKills.Count - 1; i >= 0; i--)
        {
            var (enemy, checkAt) = pendingKills[i];
            if (Time.time < checkAt) continue;
            pendingKills.RemoveAt(i);
            if (enemy != null && enemy.Alive) continue;

            damageBonus = Formulas.StealthBonusAfterKill(damageBonus, BonusPerKill.Value, MaxBonus.Value);
            var left = Remaining(local);
            var stretched = Mathf.Max(left, Mathf.Min(left + KillTimeBonus.Value, Cap));
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons Shadow Dagger: kill while hidden - damage +{damageBonus * 100f:0}%, {stretched:0.##} s left (cap {Cap:0.##} s).");
            SetLocal(local, stretched);
        }
    }

    // ---- The trigger: a fully charged Shadow Dagger hit that lands on an enemy ----

    // The damage the hit is about to deal, read before the hit too, in case the game spends it.
    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.DoAttackDamage))]
    [HarmonyPrefix]
    private static void BeforeAttackDamage(MeleeWeaponFirstPerson __instance, out float __state)
    {
        __state = __instance.m_damageToDeal;
        // Kills while hidden sharpen the dagger: the bonus applies to every hit until it ends.
        if (damageBonus > 0f && __instance.MeleeArchetypeData?.persistentID == MeleeInjector.ShadowDaggerId
            && __instance.Owner != null && __instance.Owner.IsLocallyOwned && IsHidden(__instance.Owner))
            __instance.m_damageToDeal = __state * (1f + damageBonus);
    }

    [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.DoAttackDamage))]
    [HarmonyPostfix]
    private static void AfterAttackDamage(MeleeWeaponFirstPerson __instance, MeleeWeaponDamageData data, bool isPush, float __state)
    {
        try
        {
            if (isPush || data == null || __instance.MeleeArchetypeData?.persistentID != MeleeInjector.ShadowDaggerId) return;
            var struck = data.damageGO != null ? data.damageGO.GetComponentInParent<EnemyAgent>() : null;
            var owner = __instance.Owner;
            if (struck == null || owner == null || !owner.IsLocallyOwned || owner.Owner == null) return;

            // A hit while hidden drains the cap (the time already left is kept - it just can't grow
            // past the new cap) and is checked for a kill.
            var hidden = IsHidden(owner);
            if (hidden)
            {
                cap = Cap * (1f - Mathf.Clamp01(CapDrainPerHit.Value));
                QueueKillCheck(struck);
            }

            // Full charge: the damage about to be dealt (before any bonus) is (nearly) the
            // archetype's full charged damage.
            if (!ImpactBursts.IsChargedAttack(__instance.CurrentStateName)) return;
            var full = __instance.MeleeArchetypeData.ChargedAttackDamage;
            if (__state < full * FullChargeShare)
            {
                Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons Shadow Dagger: charged hit at {__state:0.##} of {full:0.##} - not a full charge, no stealth.");
                return;
            }

            // Starts the stealth, or tops it back up - never past the drained cap, never cutting it short.
            var left = Remaining(owner);
            SetLocal(owner, Mathf.Max(left, Mathf.Min(Seconds.Value, Cap)));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Shadow Dagger stealth failed: {e}");
        }
    }

    // ---- Enemies ignore hidden players (these decide on the host) ----

    // A target that's hidden no longer counts - enemies already chasing drop it.
    [HarmonyPatch(typeof(EnemyDetection), nameof(EnemyDetection.IsTargetValid))]
    [HarmonyPostfix]
    private static void TargetValid(AgentTarget agentTarget, ref bool __result)
    {
        if (__result && IsHidden(agentTarget?.m_agent)) __result = false;
    }

    // Noticing them by sight or noise.
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetDetectedAgent))]
    [HarmonyPrefix]
    private static bool DetectedAgent(Agent agent) => !IsHidden(agent);

    // Locking on.
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetTarget))]
    [HarmonyPrefix]
    private static bool Target(Agent agent) => !IsHidden(agent);

    // Being alerted to them by other enemies and noise propagation.
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.InjectPropagatedTarget))]
    [HarmonyPrefix]
    private static bool PropagatedTarget(Agent agent) => !IsHidden(agent);

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct StealthMessage
    {
        public readonly ulong Player; // the hidden player's lookup
        public readonly float Seconds;
        public StealthMessage(ulong player, float seconds) => (Player, Seconds) = (player, seconds);
    }

    // The pre-v2 layout, unchanged, for games on an older version. The sender is the hidden player.
    private readonly struct LegacyStealthMessage
    {
        public readonly float Seconds;
        public LegacyStealthMessage(float seconds) => Seconds = seconds;
    }
}

/// <summary>
/// Shows stealth on every game, from the shared hidden list, so everyone sees the same thing: the
/// hidden player tints the same purple as the dagger's own glow and fades out to
/// <see cref="Stealth.Transparency"/> transparent, tied straight to how much stealth is left - filling
/// back in as the time runs down - and under <see cref="Stealth.WarningSeconds"/> left, flickers between
/// that tint and fully visible, faster and faster, until it reverts. Anything that adds time
/// (another full hit, a full-windup kill) makes them more see-through again. Every part of them
/// shown (each renderer) is switched to a tinted, see-through copy of its own materials for the
/// duration, and back to its own when it ends. Teammates see the whole body; the hidden player sees
/// their first-person view (arms and the dagger itself).
/// Even out of stealth, the Shadow Dagger in anyone's hand gives off a faint pulsing violet glow -
/// a whisper of the Gravity Hammer's old light, not the thing itself.
/// </summary>
internal sealed class StealthVisuals : MonoBehaviour
{
    private const float FadeRate = 6f;          // how quickly the shown transparency follows the time left
    private const float RescanSeconds = 0.25f;  // how often to pick up renderers that appeared (a swapped-in weapon)
    private const float FlickerMinHz = 2f;     // flicker rate at the start of the warning
    private const float FlickerMaxHz = 20f;     // flicker rate right before it reverts

    // One part of a hidden player: its own materials, and the tinted see-through copies shown meanwhile.
    private sealed class Part
    {
        public Renderer Renderer = null!;
        public Il2CppReferenceArray<Material> Original = null!;
        public Material[] Ghosts = Array.Empty<Material>();
    }

    private sealed class Hidden
    {
        public readonly List<Part> Parts = new();
        public readonly HashSet<IntPtr> Known = new();
        public float NextScan, Transparency;
    }

    private readonly Dictionary<IntPtr, Hidden> hidden = new();

    // Every see-through copy currently shown, so one is never mistaken for a renderer's own material.
    private static readonly HashSet<IntPtr> ghostMaterials = new();

    private static readonly Color GlowColor = new(0.6f, 0.3f, 1f);
    private static Shader? ghostShader;

    // Per player wielding the dagger: its glow.
    private sealed class Held
    {
        public IntPtr Item;
        public EffectLight Glow = null!;
    }

    private readonly Dictionary<IntPtr, Held> held = new();

    public StealthVisuals(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            var players = PlayerManager.PlayerAgentsInLevel;
            HashSet<IntPtr>? present = hidden.Count > 0 || held.Count > 0 ? new() : null;
            if (players != null) foreach (var player in players)
            {
                if (player == null) continue;
                present?.Add(player.Pointer);
                // The glow first and on its own: nothing failing below may leave the dagger's
                // purple light lit on whatever is swapped into the hand next.
                try
                {
                    Wielding(player);
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Shadow Dagger glow failed: {e.Message}");
                    PutOutGlow(player.Pointer);
                }

                try
                {
                    Stealth.EndIfDaggerPutAway(player);
                    // The host's bots are locally owned too, and would reset the player's kill bonuses.
                    if (player.IsLocallyOwned && player.Owner != null && !player.Owner.IsBot) Stealth.ProcessKills(player);
                }
                catch (Exception e)
                {
                    // The look below is still shown (or restored) even if this bookkeeping failed.
                    Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons stealth bookkeeping failed: {e.Message}");
                }

                var remaining = Stealth.Remaining(player);
                var tracked = hidden.TryGetValue(player.Pointer, out var state);
                if (remaining > 0f)
                {
                    if (!tracked) hidden[player.Pointer] = state = new Hidden();
                    // A weapon swapped in while hidden fades with the rest.
                    if (Time.time >= state!.NextScan) Collect(player, state);
                    Fade(state, remaining);
                }
                else if (tracked) Reappear(player.Pointer, state!);
            }

            // Someone who left (or a level that ended) while hidden is never visited above again:
            // their see-through copies are dropped here instead of leaking, and a dagger's glow
            // goes out rather than staying lit where they last stood.
            if (present == null) return;
            List<IntPtr>? leftGlow = null;
            foreach (var pointer in held.Keys)
                if (!present.Contains(pointer)) (leftGlow ??= new List<IntPtr>()).Add(pointer);
            if (leftGlow != null)
                foreach (var pointer in leftGlow) PutOutGlow(pointer);
            List<IntPtr>? gone = null;
            foreach (var pointer in hidden.Keys)
                if (!present.Contains(pointer)) (gone ??= new List<IntPtr>()).Add(pointer);
            if (gone == null) return;
            foreach (var pointer in gone) Reappear(pointer, hidden[pointer]);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons stealth visuals failed: {e.Message}");
        }
    }

    // The dagger's glow while it's in `player`'s hand.
    [HideFromIl2Cpp]
    private void Wielding(PlayerAgent player)
    {
        var item = player.Inventory?.WieldedItem;
        var isDagger = item != null && item.GearCategoryData?.persistentID == MeleeInjector.ShadowDaggerId;
        // Only while the dagger is actually out in the hand: mid-swap, or once anything else is
        // wielded, the light goes out at once, so it never tints the weapon that replaces it.
        if (isDagger && !item!.gameObject.activeInHierarchy) isDagger = false;
        held.TryGetValue(player.Pointer, out var state);
        if (state != null && (!isDagger || state.Item != item!.Pointer || state.Glow == null))
        {
            PutOutGlow(player.Pointer);
            state = null;
        }
        if (!isDagger) return;

        if (state == null)
        {
            state = new Held { Item = item!.Pointer };
            var go = new GameObject("GTFO.EZ.AbsurdWeapons Shadow Dagger Glow");
            state.Glow = go.AddComponent<EffectLight>();
            state.Glow.Color = GlowColor;
            state.Glow.Range = 1.2f;
            state.Glow.enabled = true;
            held[player.Pointer] = state;
        }

        state.Glow.transform.position = item!.transform.position;
        // A whisper of light, not a beacon: a tenth of what it was.
        state.Glow.Intensity = 0.2f * (0.75f + 0.25f * Mathf.Sin(Time.time * 12f));
    }

    // The dagger's light for `player` off now (dark this frame, not only once it's destroyed) and gone.
    [HideFromIl2Cpp]
    private void PutOutGlow(IntPtr player)
    {
        if (!held.TryGetValue(player, out var state)) return;
        held.Remove(player);
        if (state.Glow == null) return;
        state.Glow.Intensity = 0f;
        state.Glow.enabled = false;
        Destroy(state.Glow.gameObject);
    }

    // Switches the player's showing renderers not tracked yet to see-through copies of their
    // materials: your own first-person view (arms and the item in hand), or a teammate's whole body.
    [HideFromIl2Cpp]
    private static void Collect(PlayerAgent player, Hidden state)
    {
        state.NextScan = Time.time + RescanSeconds;
        Component? root = player.IsLocallyOwned ? player.FPItemHolder : player;
        if (root == null) return;
        ghostShader ??= Shader.Find("Sprites/Default");
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || !state.Known.Add(renderer.Pointer)) continue;
            var original = renderer.sharedMaterials;
            if (original == null || original.Length == 0) continue;
            // Already showing a see-through copy: its own materials aren't known here, and taking
            // the copy for them would leave it tinted for good once the stealth ends.
            if (ShowsGhost(original)) continue;
            var ghosts = new Material[original.Length];
            for (var i = 0; i < original.Length; i++)
            {
                ghosts[i] = new Material(ghostShader);
                ghostMaterials.Add(ghosts[i].Pointer);
                var source = original[i];
                if (source != null && source.HasProperty("_MainTex")) ghosts[i].mainTexture = source.mainTexture;
            }
            renderer.materials = new Il2CppReferenceArray<Material>(ghosts);
            state.Parts.Add(new Part { Renderer = renderer, Original = original, Ghosts = ghosts });
        }
    }

    // Transparency follows the stealth time left (Stealth.Transparency at a full stealth or more, back
    // to solid as it runs out), eased so it doesn't snap when time is added; the tint is the
    // dagger's own purple glow, not white.
    [HideFromIl2Cpp]
    private static void Fade(Hidden state, float remaining)
    {
        var target = Stealth.Transparency.Value * Mathf.Clamp01(remaining / Mathf.Max(0.1f, Stealth.Seconds.Value));
        state.Transparency = Mathf.Lerp(state.Transparency, target, 1f - Mathf.Exp(-FadeRate * Time.deltaTime));
        var tint = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 1f - state.Transparency);

        // Near the end: flickers back toward fully visible, faster and faster as it runs out, so it
        // reads as the stealth glitching off rather than just quietly ending.
        var warning = Stealth.WarningSeconds.Value;
        if (remaining < warning)
        {
            var urgency = 1f - Mathf.Clamp01(remaining / warning); // 0 entering the warning, 1 at zero
            var hz = Mathf.Lerp(FlickerMinHz, FlickerMaxHz, urgency);
            if (Mathf.PingPong(Time.time * hz, 1f) < 0.5f) tint = Color.white;
        }

        foreach (var part in state.Parts)
        {
            foreach (var ghost in part.Ghosts)
                if (ghost != null) ghost.color = tint;
        }
    }

    [HideFromIl2Cpp]
    private static bool ShowsGhost(Il2CppReferenceArray<Material>? materials)
    {
        if (materials == null) return false;
        foreach (var material in materials)
            if (material != null && ghostMaterials.Contains(material.Pointer)) return true;
        return false;
    }

    // Every part back to its own materials. Off the hidden list first, and each part on its own,
    // so one that fails can't leave the rest - or every weapon swapped in after - stuck tinted.
    // A part the game has since given other materials (a weapon swapped or rebuilt) keeps those:
    // writing the old ones back would put one weapon's look onto another.
    [HideFromIl2Cpp]
    private void Reappear(IntPtr player, Hidden state)
    {
        hidden.Remove(player);
        foreach (var part in state.Parts)
        {
            try
            {
                if (part.Renderer != null && ShowsGhost(part.Renderer.sharedMaterials))
                    part.Renderer.sharedMaterials = part.Original;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons stealth visuals: a part couldn't get its own look back: {e.Message}");
            }
            foreach (var ghost in part.Ghosts)
            {
                if (ghost == null) continue;
                ghostMaterials.Remove(ghost.Pointer);
                Destroy(ghost);
            }
        }
    }
}
