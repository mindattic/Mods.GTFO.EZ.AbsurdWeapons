using System;
using System.Collections.Generic;
using Gear;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using static GTFO.EZ.AbsurdWeapons.DataBlocks;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// G.AWPN-T236 (G.EZ-T125): every weapon this mod adds is registered whatever its "... Enabled"
/// setting, so the host and every client always hold the same datablocks - a teammate's gun is
/// never a category id this game doesn't know. A disabled weapon is only taken out of the lists
/// the loadout screen (and the in-level gear cycler) offer, here. Gear already chosen keeps working.
/// </summary>
[HarmonyPatch(typeof(GearManager), nameof(GearManager.GetAllGearForSlot))]
internal static class LoadoutFilter
{
    private static int _warned;

    [HarmonyPostfix]
    private static void Postfix(ref Il2CppReferenceArray<GearIDRange> __result)
    {
        try
        {
            if (__result == null || InLoadout.Count == 0) return;
            List<GearIDRange>? kept = null;
            for (var i = 0; i < __result.Length; i++)
            {
                var gear = __result[i];
                var hidden = gear != null && InLoadout.TryGetValue(gear.GetCompID(eGearComponent.Category), out var enabled) && !enabled();
                if (hidden && kept == null)
                {
                    kept = new List<GearIDRange>(__result.Length);
                    for (var j = 0; j < i; j++) kept.Add(__result[j]);
                }
                else if (!hidden) kept?.Add(gear!);
            }
            if (kept != null) __result = new Il2CppReferenceArray<GearIDRange>(kept.ToArray());
        }
        catch (Exception e)
        {
            if (_warned++ == 0) Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons couldn't hide disabled weapons from the loadout: {e.Message}");
        }
    }
}
