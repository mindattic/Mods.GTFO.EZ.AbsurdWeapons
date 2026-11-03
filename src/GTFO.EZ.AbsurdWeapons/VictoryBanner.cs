using System;
using System.Collections.Generic;
using Agents;
using BepInEx.Configuration;
using Gear;
using GTFO.API;
using MindAttic.GTFO.Shared;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Player;
using SNetwork;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Victory Banner: a plantable tool, put down and picked up like a sentry. Firing it sets it
/// down right in front of you - no throw - and you pick it back up by walking up to it, looking at
/// it and pressing Use; firing again while yours is still up does nothing.
/// Switching your tool slot away from the Victory Banner retires it on every client (checked
/// against whichever gear is currently wielded, every half second).
/// While it's down it heals everyone standing within its radius 1% of max health every 250 ms
/// (each client heals its own local player). That's all it does; the numbers are config
/// (`[Victory Banner]`).
/// </summary>
[HarmonyPatch]
internal static class VictoryBanner
{
    internal static ConfigEntry<float> Radius = null!;
    internal static ConfigEntry<float> HealSeconds = null!;
    internal static ConfigEntry<float> HealPercent = null!;
    internal static ConfigEntry<float> SafetyLifetimeSeconds = null!;
    internal static ConfigEntry<float> PickupRange = null!;

    private const string PlantEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.BannerPlant";
    private const string PickUpEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.BannerPickUp";

    // Set while the launcher itself is firing, so glue from anything else stays glue.
    private static int launcherFiring;

    internal static void Bind(ConfigFile config)
    {
        Radius = config.Bind("Victory Banner", "Radius", 8f, "Metres out to which it heals.");
        HealSeconds = config.Bind("Victory Banner", "HealSeconds", 0.25f, "Seconds between heals.");
        HealPercent = config.Bind("Victory Banner", "HealPercent", 1f, "Percent of max health restored per heal to whoever is inside the radius.");
        SafetyLifetimeSeconds = config.Bind("Victory Banner", "SafetyLifetimeSeconds", 600f,
            "A safety-net despawn in case it's never picked back up and its owner's tool never changes.");
        PickupRange = config.Bind("Victory Banner", "PickupRange", 2.5f, "Metres from your planted banner within which looking at it and pressing Use picks it back up.");
    }

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireBurst))]
    [HarmonyPrefix]
    private static void BeforeFireBurst() => launcherFiring++;

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireBurst))]
    [HarmonyFinalizer]
    private static void AfterFireBurst() => launcherFiring = Math.Max(0, launcherFiring - 1);

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireSingle))]
    [HarmonyPrefix]
    private static void BeforeFireSingle() => launcherFiring++;

    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.FireSingle))]
    [HarmonyFinalizer]
    private static void AfterFireSingle() => launcherFiring = Math.Max(0, launcherFiring - 1);

    internal static void Init()
    {
        RemoteEvents.Register<PlantMessage>(PlantEvent, (sender, message) =>
            BannerPylon.Spawn(sender, new Vector3(message.X, message.Y, message.Z), new Vector3(message.NormalX, message.NormalY, message.NormalZ)));
        RemoteEvents.Register<PickUpMessage>(PickUpEvent, (sender, _) => BannerPylon.Retire(sender));
    }

    // Called by the owner's own game (BannerPylon.StillInToolSlot) when their tool slot - PageUp/
    // PageDown, Home's restore, or just re-equipping something else - no longer holds the Victory
    // Banner: tells every client (itself included) to drop their copy, same as picking it up by hand.
    internal static void BroadcastPickUp() => NetworkAPI.InvokeEvent(PickUpEvent, new PickUpMessage());

    // Every glob the C-Foam launcher would fire goes through here (the local player's only). Ours
    // plants the banner instead (if you don't have one up already).
    [HarmonyPatch(typeof(ProjectileManager), nameof(ProjectileManager.WantToFireGlue))]
    [HarmonyPrefix]
    private static bool BeforeFireGlue(PlayerAgent owner, Vector3 pos, Vector3 velocity)
    {
        try
        {
            if (owner == null || !owner.IsLocallyOwned) return true;
            var wielded = owner.Inventory?.WieldedItem;
            if (wielded == null || wielded.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return true;
            if (launcherFiring == 0) return true;

            Toggle(owner, velocity);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Victory Banner failed: {e}");
            return false;
        }
    }

    // No charge-up: while the Victory Banner is in hand the launcher's own update (pressure, bursts)
    // doesn't run at all - a press of fire plants it right away (picking it up is Use, at the banner).
    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.Update))]
    [HarmonyPrefix]
    private static bool InsteadOfCharging(GlueGun __instance)
    {
        try
        {
            if (__instance.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return true;
            var owner = __instance.Owner;
            if (owner != null && owner.IsLocallyOwned && owner.Inventory?.WieldedItem?.Pointer == __instance.Pointer
                && InputMapper.GetButtonDownKeyMouse(InputAction.Fire, eFocusState.FPS))
                Toggle(owner, owner.FPSCamera != null ? owner.FPSCamera.Forward : owner.transform.forward);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Victory Banner input failed: {e.Message}");
            return true;
        }
    }

    // In hand it's the flag itself: the launcher's model hidden, a smaller copy of the flag in its place.
    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.OnWield))]
    [HarmonyPostfix]
    private static void HoldTheFlag(GlueGun __instance)
    {
        try
        {
            if (__instance.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return;
            BannerPylon.HoldModel(__instance.gameObject);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't show the held Victory Banner: {e.Message}");
        }
    }

    // Puts your banner down in front of you, like a sentry, and switches you to your main weapon.
    // Already down: nothing - it's picked back up by walking to it (BannerPylon.UpdatePickup).
    private static void Toggle(PlayerAgent owner, Vector3 forward)
    {
        var lookup = owner.Owner != null ? owner.Owner.Lookup : 0UL;
        if (BannerPylon.HasActive(lookup)) return;

        // Level, so looking down doesn't push the drop point under the floor; short of a wall in
        // front; then straight down from chest height onto the floor.
        var aim = new Vector3(forward.x, 0f, forward.z);
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : owner.transform.forward;
        var chest = owner.Position + Vector3.up * 1.2f;
        var reach = 1.5f;
        if (Physics.Raycast(chest, aim, out var wall, reach + 0.4f, LayerManager.MASK_WORLD)) reach = Mathf.Max(0f, wall.distance - 0.4f);
        var origin = chest + aim * reach;
        var position = owner.Position + aim * reach;
        var normal = Vector3.up;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 4f, LayerManager.MASK_WORLD))
        {
            position = hit.point;
            normal = hit.normal;
        }

        BannerPylon.Spawn(lookup, position, normal);
        Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons Victory Banner: planted at {position}.");
        NetworkAPI.InvokeEvent(PlantEvent, new PlantMessage(position, normal));
        owner.Sync?.WantsToWieldSlot(InventorySlot.GearStandard); // hands free of it, like a placed sentry
    }

    // Picking it back up (the owner, at the banner): gone on every game, and back in hand.
    internal static void PickUp(PlayerAgent owner)
    {
        var lookup = owner.Owner != null ? owner.Owner.Lookup : 0UL;
        BannerPylon.Retire(lookup);
        NetworkAPI.InvokeEvent(PickUpEvent, new PickUpMessage());
        Plugin.Logger.LogInfo("GTFO.EZ.AbsurdWeapons Victory Banner: picked back up.");
        owner.Sync?.WantsToWieldSlot(InventorySlot.GearClass);
    }

    // Infinite ammo: while the local player holds the banner, the tool ammo is kept full.
    [HarmonyPatch(typeof(GlueGun), nameof(GlueGun.Update))]
    [HarmonyPostfix]
    private static void KeepToolAmmoFull(GlueGun __instance)
    {
        try
        {
            if (__instance.GearCategoryData?.persistentID != GunInjector.VictoryBannerId) return;
            if (__instance.Owner == null || !__instance.Owner.IsLocallyOwned) return;
            var storage = PlayerBackpackManager.LocalBackpack?.AmmoStorage;
            var ammo = storage?.ClassAmmo;
            if (storage == null || ammo == null || ammo.IsFull) return;
            storage.SetAmmo(AmmoType.Class, ammo.AmmoMaxCap);
            storage.UpdateAllAmmoUI();
            storage.NeedsSync = true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons couldn't refill the Victory Banner: {e.Message}");
        }
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct PlantMessage
    {
        public readonly float X, Y, Z, NormalX, NormalY, NormalZ;

        public PlantMessage(Vector3 position, Vector3 normal)
        {
            (X, Y, Z) = (position.x, position.y, position.z);
            (NormalX, NormalY, NormalZ) = (normal.x, normal.y, normal.z);
        }
    }

    // A zero-field struct would throw from GTFO-API's Marshal.SizeOf<T>() (no meaningful size can be
    // computed for a type with no fields), so this carries one dummy byte just to have a layout.
    private readonly struct PickUpMessage
    {
        public readonly byte Dummy;
        public PickUpMessage() => Dummy = 0;
    }
}

/// <summary>
/// One banner, set down (not thrown) and stuck where it landed. Every
/// <see cref="VictoryBanner.HealSeconds"/> each client heals its own local player if they're within
/// <see cref="VictoryBanner.Radius"/>, so nothing has to cross the network; an expanding ring shows
/// every couple of seconds. Picking it back up, planting a new one for the same owner, or the
/// owner switching away from the Victory Banner in their tool slot all retire it, on every client;
/// so does the owner leaving the game (each client notices that on its own).
/// </summary>
internal sealed class BannerPylon : MonoBehaviour
{
    private const float RingSeconds = 0.6f;
    private const float RingEverySeconds = 2f; // the expanding ring is only a look
    private const float ToolCheckSeconds = 0.5f;
    private static readonly Color BannerColor = new(0.25f, 0.95f, 0.35f, 1f);

    private static readonly Dictionary<ulong, BannerPylon> activeByOwner = new();

    private ulong ownerLookup;
    private bool retired;
    private float placedAt, nextHealAt, nextRingAt, nextToolCheckAt, ringProgress = -1f;
    private LineRenderer ring = null!, aura = null!;
    private EffectLight glow = null!;
    private Transform? flag;
    private float flash; // 1 right after a heal pulse, fading

    public BannerPylon(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static bool HasActive(ulong ownerLookup) => activeByOwner.TryGetValue(ownerLookup, out var pylon) && pylon != null && !pylon.retired;

    [HideFromIl2Cpp]
    internal static void Spawn(ulong ownerLookup, Vector3 position, Vector3 normal)
    {
        if (activeByOwner.TryGetValue(ownerLookup, out var old) && old != null) old.Retire();

        var go = new GameObject("GTFO.EZ.AbsurdWeapons Victory Banner");
        go.transform.position = position;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        var flag = BuildModel(go.transform);
        SitOnFloor(go.transform, position);

        var pylon = go.AddComponent<BannerPylon>();
        pylon.flag = flag;
        pylon.ownerLookup = ownerLookup;
        pylon.placedAt = Time.time;
        pylon.nextHealAt = pylon.nextRingAt = Time.time; // heals right away

        var ringObject = new GameObject("Ring");
        ringObject.transform.SetParent(go.transform, false);
        pylon.ring = ringObject.AddComponent<LineRenderer>();
        pylon.ring.loop = true;
        pylon.ring.useWorldSpace = true;
        pylon.ring.positionCount = 48;
        pylon.ring.material = RingMaterial();
        pylon.ring.enabled = false;

        // The aura: a glowing ring on the ground at the healing radius, always there while it's up.
        var auraObject = new GameObject("Aura");
        auraObject.transform.SetParent(go.transform, false);
        pylon.aura = auraObject.AddComponent<LineRenderer>();
        pylon.aura.loop = true;
        pylon.aura.useWorldSpace = true;
        pylon.aura.positionCount = 64;
        pylon.aura.material = RingMaterial();

        // A green glow filling the aura, brightening with each heal.
        var glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(go.transform, false);
        glowObject.transform.localPosition = Vector3.up * 1.6f;
        pylon.glow = glowObject.AddComponent<EffectLight>();
        pylon.glow.Color = BannerColor;
        pylon.glow.Range = VictoryBanner.Radius.Value * 1.2f;
        pylon.glow.Intensity = 2f;
        pylon.glow.enabled = true;

        activeByOwner[ownerLookup] = pylon;
        Sounds.Play(Sounds.BannerPlant.Value, position); // every game plants its copy, so every game hears it
    }

    [HideFromIl2Cpp]
    internal static void Retire(ulong ownerLookup)
    {
        if (activeByOwner.TryGetValue(ownerLookup, out var pylon) && pylon != null) pylon.Retire();
    }

    [HideFromIl2Cpp]
    private void Retire()
    {
        if (retired) return;
        retired = true;
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local?.Owner != null && local.Owner.Lookup == ownerLookup) HidePrompt();
        if (activeByOwner.TryGetValue(ownerLookup, out var current) && current == this) activeByOwner.Remove(ownerLookup);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (retired) return;
        try
        {
            // The level ended (won, lost or left): the banner goes with it, or the next drop would
            // see it as already down and it would keep healing at its old spot.
            if (GameStateManager.CurrentStateName != eGameStateName.InLevel
                || Time.time - placedAt >= VictoryBanner.SafetyLifetimeSeconds.Value)
            {
                Retire();
                return;
            }

            if (Time.time >= nextToolCheckAt)
            {
                nextToolCheckAt = Time.time + ToolCheckSeconds;
                var local = PlayerManager.GetLocalPlayerAgent();
                if (local != null && local.Owner != null && local.Owner.Lookup == ownerLookup)
                {
                    if (!StillInToolSlot())
                    {
                        Retire();
                        VictoryBanner.BroadcastPickUp();
                        return;
                    }
                }
                else if (local != null && !OwnerInLevel())
                {
                    // A teammate's banner whose owner left: their game will never send the pick-up,
                    // so it goes here instead of healing on at its spot until the safety net.
                    Retire();
                    return;
                }
            }

            UpdatePickup();
            if (retired) return;

            if (Time.time >= nextHealAt)
            {
                nextHealAt = Time.time + Mathf.Max(0.05f, VictoryBanner.HealSeconds.Value);
                Heal();
            }
            if (Time.time >= nextRingAt)
            {
                nextRingAt = Time.time + RingEverySeconds;
                ringProgress = 0f;
                flash = 1f;
            }
            AnimateRing(Time.deltaTime);
            AnimateAura(Time.deltaTime);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons Victory Banner failed: {e.Message}");
            Retire();
        }
    }

    private const float PickupConeDegrees = 40f;
    private static bool promptShown;

    // Like a sentry: the owner walks up to their banner, looks at it, and presses Use to pick it up.
    // A prompt shows while they can.
    [HideFromIl2Cpp]
    private void UpdatePickup()
    {
        var local = PlayerManager.GetLocalPlayerAgent();
        if (local == null || local.Owner == null || local.Owner.Lookup != ownerLookup) return;
        var camera = local.FPSCamera;
        var pole = transform.position + transform.up * 1.1f;
        var canPick = local.Alive && camera != null && FocusStateManager.CurrentState == eFocusState.FPS
            && Vector3.Distance(local.Position, transform.position) <= VictoryBanner.PickupRange.Value
            && Vector3.Angle(camera.Forward, pole - camera.Position) <= PickupConeDegrees;
        var layer = GuiManager.InteractionLayer;
        if (canPick)
        {
            if (layer != null)
            {
                layer.SetInteractPrompt("Pick up Victory Banner", "Use");
                layer.InteractPromptVisible = true;
                promptShown = true;
            }
            if (InputMapper.GetButtonDownKeyMouse(InputAction.Use, eFocusState.FPS))
            {
                HidePrompt();
                VictoryBanner.PickUp(local);
            }
        }
        else if (promptShown)
        {
            HidePrompt();
        }
    }

    private static void HidePrompt()
    {
        if (!promptShown) return;
        promptShown = false;
        var layer = GuiManager.InteractionLayer;
        if (layer != null) layer.InteractPromptVisible = false;
    }

    // Whether the Victory Banner is still what's actually equipped in the owner's tool slot - not
    // just "currently drawn" (drawing your gun to fight shouldn't retire the banner you just
    // planted). Only checkable on the owner's own game, the same way GearCycler reads a slot's
    // gear; PageUp/PageDown, re-equipping another tool, or Home's restore all show up here.
    [HideFromIl2Cpp]
    private static bool StillInToolSlot()
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null || !backpack.TryGetBackpackItem(InventorySlot.GearClass, out var item) || item?.GearIDRange == null) return true;
        return item.GearIDRange.GetCompID(eGearComponent.Category) == GunInjector.VictoryBannerId;
    }

    // Whether the player who planted this banner is still in the level (alive or not).
    [HideFromIl2Cpp]
    private bool OwnerInLevel()
    {
        var players = PlayerManager.PlayerAgentsInLevel;
        if (players == null) return true; // can't tell: keep it
        foreach (var player in players)
            if (player != null && player.Owner != null && player.Owner.Lookup == ownerLookup) return true;
        return false;
    }

    // Heals the local player if they're standing within the radius - and, on the host, the bots
    // standing in it: they have no game of their own to heal them.
    [HideFromIl2Cpp]
    private void Heal()
    {
        HealIfInside(PlayerManager.GetLocalPlayerAgent());
        if (!SNet.IsMaster) return;
        var players = PlayerManager.PlayerAgentsInLevel;
        if (players == null) return;
        foreach (var player in players)
            if (player != null && player.Owner != null && player.Owner.IsBot) HealIfInside(player);
    }

    [HideFromIl2Cpp]
    private void HealIfInside(PlayerAgent? player)
    {
        if (player == null || !player.Alive) return;
        if (Vector3.Distance(player.Position, transform.position) > VictoryBanner.Radius.Value) return;

        try
        {
            var damage = player.Damage;
            if (damage == null) return;
            // The game's own heal (synced), a share of max health.
            if (damage.Health >= damage.HealthMax) return;
            damage.AddHealth(damage.HealthMax * VictoryBanner.HealPercent.Value / 100f, player.Cast<Agent>());
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons Victory Banner couldn't heal: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private void AnimateRing(float dt)
    {
        if (ringProgress < 0f)
        {
            ring.enabled = false;
            return;
        }

        ringProgress += dt / RingSeconds;
        if (ringProgress >= 1f)
        {
            ringProgress = -1f;
            ring.enabled = false;
            return;
        }

        var fade = 1f - ringProgress;
        ring.startWidth = ring.endWidth = 0.08f;
        ring.startColor = ring.endColor = new Color(BannerColor.r, BannerColor.g, BannerColor.b, fade);
        ring.enabled = true;
        Circle(ring, VictoryBanner.Radius.Value * ringProgress, 0.05f);
    }

    // The aura ring stays at the healing radius, breathing, and flares on each heal; the flag waves.
    [HideFromIl2Cpp]
    private void AnimateAura(float dt)
    {
        flash = Mathf.Max(0f, flash - dt * 1.5f);
        var breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f);
        aura.startWidth = aura.endWidth = 0.1f + 0.06f * flash;
        aura.startColor = aura.endColor = new Color(BannerColor.r, BannerColor.g, BannerColor.b, 0.45f + 0.25f * breathe + 0.3f * flash);
        Circle(aura, VictoryBanner.Radius.Value, 0.04f);
        glow.Range = VictoryBanner.Radius.Value * 1.2f;
        glow.Intensity = 1.5f + 0.8f * breathe + 5f * flash;
        if (flag != null) flag.localRotation = Quaternion.Euler(0f, 12f * Mathf.Sin(Time.time * 3.1f), 4f * Mathf.Sin(Time.time * 5.3f));
    }

    // Lays a ground-level circle of `radius` around the banner.
    [HideFromIl2Cpp]
    private void Circle(LineRenderer line, float radius, float lift)
    {
        var center = transform.position;
        var up = transform.up;
        var right = Vector3.Cross(up, Vector3.forward).sqrMagnitude > 0.001f ? Vector3.Cross(up, Vector3.forward).normalized : Vector3.right;
        var forward = Vector3.Cross(right, up).normalized;
        for (var i = 0; i < line.positionCount; i++)
        {
            var angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, center + up * lift + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius);
        }
    }

    // Lifts the banner so the lowest vertex of its meshes sits on `floor` (along its own up), not
    // into it - measured from the meshes themselves rather than the numbers they were built with.
    [HideFromIl2Cpp]
    private static void SitOnFloor(Transform root, Vector3 floor)
    {
        var up = root.up;
        var lowest = float.MaxValue;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) continue;
            foreach (var vertex in mesh.vertices)
                lowest = Mathf.Min(lowest, Vector3.Dot(filter.transform.TransformPoint(vertex) - floor, up));
        }
        if (lowest != float.MaxValue) root.position -= up * lowest;
    }

    // A flag: a dark pole on a small base with a green banner flying from its top - primitives,
    // no new assets shipped (same style as the grenade's fallback). Returns the banner, which waves.
    [HideFromIl2Cpp]
    private static Transform BuildModel(Transform parent)
    {
        var dark = new Color(0.15f, 0.15f, 0.17f);
        Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.04f, 0f), new Vector3(0.3f, 0.04f, 0.3f), dark);   // base
        Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.05f, 0f), new Vector3(0.05f, 1.05f, 0.05f), dark); // 2.1 m pole
        Part(PrimitiveType.Sphere, parent, new Vector3(0f, 2.12f, 0f), Vector3.one * 0.09f, BannerColor);         // finial

        // The banner hangs off a pivot at the pole, so it swings from there.
        var pivot = new GameObject("Flag").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = new Vector3(0f, 1.7f, 0f);
        Part(PrimitiveType.Cube, pivot, new Vector3(0.38f, 0f, 0f), new Vector3(0.72f, 0.5f, 0.02f), BannerColor);
        Part(PrimitiveType.Cube, pivot, new Vector3(0.38f, 0f, 0f), new Vector3(0.2f, 0.2f, 0.03f), Color.white);  // emblem
        return pivot;
    }

    private const string HeldModelName = "GTFO.EZ.AbsurdWeapons Held Flag";

    // The held Victory Banner: the launcher's renderers hidden and a third-size flag in their place,
    // on the item's own layer (the first-person item draws with its own camera).
    [HideFromIl2Cpp]
    internal static void HoldModel(GameObject item)
    {
        if (item.transform.Find(HeldModelName) != null) return;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        var holder = new GameObject(HeldModelName).transform;
        holder.SetParent(item.transform, false);
        holder.localPosition = new Vector3(0f, -0.25f, 0.15f);
        holder.localScale = Vector3.one * 0.35f;
        BuildModel(holder);
        foreach (var child in holder.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = item.layer;
    }

    [HideFromIl2Cpp]
    private static void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var part = GameObject.CreatePrimitive(type);
        UnityEngine.Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = PartMaterial(color);
    }

    // One shared material per part color. A new one per part was never freed with the banner (Unity
    // doesn't destroy a renderer's material with its object), so every plant - on every client -
    // leaked one per part.
    private static readonly Dictionary<(float, float, float, float), Material> partMaterials = new();

    [HideFromIl2Cpp]
    private static Material PartMaterial(Color color)
    {
        var key = (color.r, color.g, color.b, color.a);
        if (partMaterials.TryGetValue(key, out var material) && material != null) return material; // Unity's null: rebuilt if unloaded
        material = new Material(Shader.Find("Unlit/Color")) { color = color };
        partMaterials[key] = material;
        return material;
    }

    private static Material? ringMaterial;

    [HideFromIl2Cpp]
    private static Material RingMaterial() => ringMaterial != null ? ringMaterial // Unity's null: rebuilt if unloaded
        : ringMaterial = new Material(Shader.Find("Sprites/Default")) { color = BannerColor };
}
