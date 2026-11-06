using System;
using System.Reflection;
using GameData;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>Helpers for building new weapons out of cloned vanilla datablocks.</summary>
internal static class DataBlocks
{
    internal static T Lookup<T>(string name) where T : GameDataBlockBase<T>
    {
        var id = GameDataBlockBase<T>.GetBlockID(name);
        return GameDataBlockBase<T>.GetBlock(id) ?? throw new InvalidOperationException($"vanilla {typeof(T).Name} '{name}' not found");
    }

    // Copies every writable instance property onto a new block, so fields this mod doesn't
    // explicitly set (stamina costs, hit timing, etc.) start from the source weapon's values.
    internal static T Clone<T>(T source) where T : GameDataBlockBase<T>, new() => CopyObject(source);

    // Copies every writable instance property onto a fresh object - for the plain data objects
    // inside datablocks (shared with vanilla, so never edited in place).
    internal static T CopyObject<T>(T source) where T : new()
    {
        var copy = new T();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0) continue;
            property.SetValue(copy, property.GetValue(source));
        }
        return copy;
    }

    // Every gear category this mod registered this session - the weapons it added, whatever their
    // ids or names. Filled as each one is registered; the in-level gear cycler only offers these.
    internal static readonly System.Collections.Generic.HashSet<uint> AddedCategories = new();

    // G.AWPN-T236 / G.EZ-T125: whether each weapon's own "... Enabled" setting offers it in the
    // loadout. Every weapon's blocks are registered whatever the setting, so the host and every
    // client hold the same datablocks; a disabled one is only left out of the loadout lists (LoadoutFilter).
    internal static readonly System.Collections.Generic.Dictionary<uint, Func<bool>> InLoadout = new();

    // Gear-JSON helpers (see GearJson, which is unit tested).
    internal static uint? CategoryIdOf(PlayerOfflineGearDataBlock gear) => GearJson.CategoryId(gear.GearJSON);
    internal static eWeaponFireMode FireModeOf(PlayerOfflineGearDataBlock gear) => (eWeaponFireMode)GearJson.FireModeId(gear.GearJSON);
    internal static string DisplayName(string name) => GearJson.DisplayName(name);
    internal static string SetGearComponent(string json, int component, uint value) => GearJson.SetComponent(json, component, value);
    internal static string? RewriteGearJson(string json, uint fromCategory, uint toCategory, string name) =>
        GearJson.Rewrite(json, fromCategory, toCategory, name);
}
