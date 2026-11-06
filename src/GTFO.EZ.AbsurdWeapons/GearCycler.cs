using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GameData;
using Gear;
using Il2CppInterop.Runtime.Attributes;
using MindAttic.GTFO.Shared;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Pressing a slot's own select key (melee, main, special, tool - 1 to 4 by default, following
/// rebinds) while that slot is already in hand swaps its weapon for the next one in that slot's
/// cycle, mid-level - but only once Absurd Weapons is switched on (see below). The cycle is the
/// gear you dropped in with (added the first time you swap that slot) followed by this mod's
/// weapons for the slot, so what you brought comes round again. It equips through the game's own
/// loadout path (PlayerBackpackManager.EquipLocalGear, which syncs to teammates), then re-wields
/// the slot, and a three-second line under the crosshair names what you switched to.
/// Every weapon keeps its own ammo for the rest of the level: what's in its magazine and the
/// slot's reserve are saved when you swap away from it and put back when you swap to it again. One
/// of this mod's weapons starts full the first time it's equipped in a level.
/// Absurd Weapons is a toggle (<see cref="toggleKey"/>, End by default), off at the start of
/// every level. Pressing it the first time turns it on and snapshots every slot's current gear and
/// the player's current health; cycling does nothing until it's on. Pressing it again turns it
/// off: every slot that got swapped goes back to its snapshotted gear and ammo - dropping you out
/// of whichever Absurd weapon you were holding, back into what you dove in with - and health is
/// set back to the snapshotted value.
/// </summary>
internal sealed class GearCycler : MonoBehaviour
{
    private static ConfigEntry<KeyCode> toggleKey = null!;

    // The game's own select-slot actions (so rebinds are followed): pressed again while that slot is
    // already in hand, it cycles the slot - the game itself does nothing with that press.
    private static readonly (InputAction Action, InventorySlot Slot)[] SlotKeys =
    {
        (InputAction.SelectMelee, InventorySlot.GearMelee),
        (InputAction.SelectStandard, InventorySlot.GearStandard),
        (InputAction.SelectSpecial, InventorySlot.GearSpecial),
        (InputAction.SelectTool, InventorySlot.GearClass),
    };

    private sealed class SavedAmmo
    {
        public int? Clip;
        public AmmoType? AmmoType;
        public float Reserve;
    }

    // Each swapped slot's gear as it was before this level's first swap of it.
    private readonly Dictionary<InventorySlot, GearIDRange> starting = new();

    // Each weapon's own ammo (by gear category), saved when it's swapped away.
    private readonly Dictionary<uint, SavedAmmo> ammo = new();

    // Slots whose newly equipped gear is still being built: the category expected there. Its ammo
    // goes in once it's loaded, then `wieldSlot` is wielded.
    private readonly Dictionary<InventorySlot, uint> pending = new();
    private InventorySlot wieldSlot = InventorySlot.None;
    private float pendingFrom, pendingGiveUpAt;

    // Whether Absurd Weapons is switched on for the current level; off by default on every dive.
    private bool active;
    private float? startingHealth;

    public GearCycler(IntPtr ptr) : base(ptr) { }

    internal static void Bind(ConfigFile config)
    {
        toggleKey = config.Bind("Gear Cycling", "ToggleAbsurdWeaponsKey", KeyCode.End,
            "Switches Absurd Weapons on (and snapshots your current gear and health) or, pressed again, off - putting every swapped slot and your health back to that snapshot. Off at the start of every level.");
    }

    // A teammate switching Absurd Weapons on or off shows in everyone's event log: their loadout
    // changes in front of the team, and switching off puts their health back to the snapshot.
    private const string ToggleEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.AbsurdToggle.v2";

    [HideFromIl2Cpp]
    internal static void Init() =>
        Wire.Register<ToggleMessage>(ToggleEvent, (sender, message) =>
        {
            var name = PlayerName(Wire.Actor(message.Player, sender));
            var text = message.On != 0 ? $"{name}: Absurd Weapons ON" : $"{name}: Absurd Weapons OFF - starting loadout and health restored";
            MessageHelper.Show(text, MessageType.EventLog);
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {text}");
        });

    [HideFromIl2Cpp]
    private static void BroadcastToggle(PlayerAgent player, bool on)
    {
        try
        {
            GTFO.API.NetworkAPI.InvokeEvent(ToggleEvent, new ToggleMessage(Wire.LookupOf(player), on));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't tell the team about the toggle: {e.Message}");
        }
    }

    [HideFromIl2Cpp]
    private static string PlayerName(ulong lookup)
    {
        try
        {
            if (SNetwork.SNet.TryGetPlayer(lookup, out var player) && player != null && !string.IsNullOrWhiteSpace(player.NickName))
                return player.NickName;
        }
        catch (Exception)
        {
            // An unknown name is just "A teammate".
        }
        return "A teammate";
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct ToggleMessage
    {
        public readonly ulong Player; // whose Absurd Weapons it is
        public readonly byte On;
        public ToggleMessage(ulong player, bool on) => (Player, On) = (player, (byte)(on ? 1 : 0));
    }

    private void Update()
    {
        try
        {
            if (GameStateManager.CurrentStateName != eGameStateName.InLevel)
            {
                starting.Clear();
                ammo.Clear();
                pending.Clear();
                wieldSlot = InventorySlot.None;
                active = false;
                startingHealth = null;
                return;
            }

            if (pending.Count > 0) FinishPending();

            if (FocusStateManager.CurrentState != eFocusState.FPS) return;
            if (Input.GetKeyDown(toggleKey.Value))
            {
                ToggleActive();
                return;
            }
            if (!active) return;
            if (SlotKeyPressedAgain()) Cycle(1);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons gear cycling failed: {e}");
        }
    }

    // End (by default): first press turns Absurd Weapons on and snapshots the current loadout and
    // health; a second press turns it off, putting every swapped slot and health back to that
    // snapshot - whatever Absurd weapon you're holding is swapped out as part of its slot restoring.
    private void ToggleActive()
    {
        var player = PlayerManager.GetLocalPlayerAgent();
        if (player == null || !player.Alive) return;

        if (!active)
        {
            active = true;
            starting.Clear();
            foreach (var (_, slot) in SlotKeys)
            {
                var gear = GearIn(slot);
                if (gear != null) starting[slot] = gear;
            }
            startingHealth = player.Damage?.Health;
            Announce("Absurd Weapons: ON");
            BroadcastToggle(player, true);
            return;
        }

        Restore(player);
        active = false;
        starting.Clear();
        startingHealth = null;
        Announce("Absurd Weapons: OFF - starting loadout restored");
        BroadcastToggle(player, false);
    }

    // Puts every snapshotted slot back to the gear (and that gear's own ammo) it held when Absurd
    // Weapons was switched on, then re-wields whatever slot was in hand, and restores health.
    private void Restore(PlayerAgent player)
    {
        var hand = player.Inventory.WieldedSlot;
        foreach (var (slot, gear) in starting)
        {
            var category = gear.GetCompID(eGearComponent.Category);
            var current = pending.TryGetValue(slot, out var building) ? building : CategoryIn(slot);
            if (current == category) continue;
            if (!pending.ContainsKey(slot)) SaveAmmo(slot);
            Equip(slot, gear);
        }
        if (hand != InventorySlot.None) wieldSlot = hand;

        if (startingHealth.HasValue && player.Damage != null) player.Damage.SendSetHealth(startingHealth.Value);
    }

    private void Cycle(int step)
    {
        var player = PlayerManager.GetLocalPlayerAgent();
        if (player == null || !player.Alive) return;
        var slot = player.Inventory.WieldedSlot;
        if (slot is not (InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass or InventorySlot.GearMelee))
        {
            Show("Hold a main, special, tool or melee weapon to cycle it");
            return;
        }

        var ours = new List<GearIDRange>();
        foreach (var gear in GearManager.GetAllGearForSlot(slot))
            if (IsOurs(gear.GetCompID(eGearComponent.Category))) ours.Add(gear);
        if (ours.Count == 0)
        {
            Show("No GTFO.EZ.AbsurdWeapons weapons for this slot");
            return;
        }

        var current = pending.TryGetValue(slot, out var building) ? building : CategoryIn(slot);
        if (!starting.ContainsKey(slot))
        {
            var held = GearIn(slot);
            if (held != null) starting[slot] = held;
        }

        // The gear you dropped in with first (unless it's one of ours already), then ours.
        var cycle = new List<GearIDRange>();
        if (starting.TryGetValue(slot, out var start) && !IsOurs(start.GetCompID(eGearComponent.Category))) cycle.Add(start);
        cycle.AddRange(ours);

        var index = cycle.FindIndex(g => g.GetCompID(eGearComponent.Category) == current);
        var next = Formulas.CycleIndex(index, step, cycle.Count);
        var chosen = cycle[next];
        var chosenCategory = chosen.GetCompID(eGearComponent.Category);

        if (!pending.ContainsKey(slot)) SaveAmmo(slot);
        Equip(slot, chosen);
        var name = GameDataBlockBase<GearCategoryDataBlock>.GetBlock(chosenCategory)?.PublicName?.UntranslatedText;
        Show($"{name ?? "weapon"}  [{next + 1}/{cycle.Count}]");
    }

    // A slot's select key pressed while that slot is already the one in hand.
    private static bool SlotKeyPressedAgain()
    {
        var wielded = PlayerManager.GetLocalPlayerAgent()?.Inventory?.WieldedSlot ?? InventorySlot.None;
        foreach (var (action, slot) in SlotKeys)
            if (slot == wielded && InputMapper.GetButtonDownKeyMouse(action, eFocusState.FPS)) return true;
        return false;
    }

    private void Equip(InventorySlot slot, GearIDRange gear)
    {
        PlayerBackpackManager.EquipLocalGear(gear);
        pending[slot] = gear.GetCompID(eGearComponent.Category);
        wieldSlot = slot;
        pendingFrom = Time.time + 0.4f; // the new item is built asynchronously
        pendingGiveUpAt = Time.time + 5f;
    }

    // The weapon now in `slot`: its magazine and the slot's reserve, kept under its category.
    private void SaveAmmo(InventorySlot slot)
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null || !backpack.TryGetBackpackItem(slot, out var item) || item?.GearIDRange == null) return;
        var entry = new SavedAmmo();
        var gun = item.Instance?.TryCast<BulletWeapon>();
        if (gun != null) entry.Clip = gun.GetCurrentClip();
        if (slot is InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass)
        {
            var type = PlayerAmmoStorage.GetAmmoTypeFromSlot(slot);
            entry.AmmoType = type;
            entry.Reserve = backpack.AmmoStorage.GetAmmoInPack(type);
        }
        ammo[item.GearIDRange.GetCompID(eGearComponent.Category)] = entry;
    }

    // Once every newly equipped item is built and loaded: each gets its own ammo back (one of
    // ours seen for the first time this level starts full), and the slot that was held is wielded.
    private void FinishPending()
    {
        if (Time.time < pendingFrom) return;
        var backpack = PlayerBackpackManager.LocalBackpack;
        if (backpack == null) return;

        var ready = true;
        foreach (var (slot, category) in pending)
        {
            if (!backpack.TryGetBackpackItem(slot, out var item) || item == null || !item.IsLoaded || item.Instance == null
                || item.GearIDRange?.GetCompID(eGearComponent.Category) != category)
                ready = false;
        }
        if (!ready && Time.time < pendingGiveUpAt) return;
        if (!ready) Plugin.Logger.LogWarning("GTFO.EZ.AbsurdWeapons: swapped gear took too long to build; setting its ammo anyway.");

        var storage = backpack.AmmoStorage;
        foreach (var (slot, category) in pending)
        {
            backpack.TryGetBackpackItem(slot, out var item);
            // Gave up with the old weapon still in the slot: its ammo is already its own, and the
            // chosen weapon's saved ammo mustn't land on it.
            if (item?.GearIDRange != null && item.GearIDRange.GetCompID(eGearComponent.Category) != category) continue;
            var gun = item?.Instance?.TryCast<BulletWeapon>();
            var hasReserve = slot is InventorySlot.GearStandard or InventorySlot.GearSpecial or InventorySlot.GearClass;
            if (ammo.TryGetValue(category, out var saved))
            {
                if (saved.AmmoType.HasValue) storage.SetAmmo(saved.AmmoType.Value, saved.Reserve);
                if (saved.Clip.HasValue) gun?.SetCurrentClip(saved.Clip.Value);
            }
            else if (IsOurs(category))
            {
                if (hasReserve)
                {
                    var slotAmmo = storage.GetInventorySlotAmmo(slot);
                    if (slotAmmo != null) storage.SetAmmo(slotAmmo.AmmoType, slotAmmo.AmmoMaxCap);
                }
                if (gun != null) gun.SetCurrentClip(gun.ClipSize);
            }
            // Only the HUD is told: the game's SetClipAmmoInSlot loads the magazine from the reserve,
            // which would make every swap a free reload (the same call InfiniteAmmo dropped, G.AWPN-T176).
            if (hasReserve)
            {
                var slotAmmo = storage.GetInventorySlotAmmo(slot);
                var equippable = item?.Instance?.TryCast<ItemEquippable>();
                if (slotAmmo != null && equippable != null) storage.UpdateSlotAmmoUI(slotAmmo, equippable.GetCurrentClip());
            }
        }
        storage.UpdateAllAmmoUI();
        storage.NeedsSync = true;
        pending.Clear();

        if (wieldSlot != InventorySlot.None) PlayerManager.GetLocalPlayerAgent()?.Sync?.WantsToWieldSlot(wieldSlot);
        wieldSlot = InventorySlot.None;
    }

    private static GearIDRange? GearIn(InventorySlot slot)
    {
        var backpack = PlayerBackpackManager.LocalBackpack;
        return backpack != null && backpack.TryGetBackpackItem(slot, out var item) ? item?.GearIDRange : null;
    }

    private static uint CategoryIn(InventorySlot slot) => GearIn(slot)?.GetCompID(eGearComponent.Category) ?? 0;

    private static bool IsOurs(uint categoryId) => DataBlocks.AddedCategories.Contains(categoryId);

    // On the line under the crosshair, where your eyes already are when you swap
    // (MindAttic.GTFO.Shared.MessageHelper; the game's own interaction messages still take priority).
    internal static void Show(string message)
    {
        if (!MessageHelper.Show(message, MessageType.Center, 3f))
            Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {message}");
    }

    // The Absurd Weapons on/off state specifically: in the event log and the console too (not just a
    // fallback for when the on-screen message fails), so it's never just text that scrolled past -
    // same as InfiniteToggles' infinite health/ammo announcements.
    private static void Announce(string message)
    {
        Show(message);
        MessageHelper.Show(message, MessageType.EventLog);
        Plugin.Logger.LogInfo($"GTFO.EZ.AbsurdWeapons: {message}");
    }
}
