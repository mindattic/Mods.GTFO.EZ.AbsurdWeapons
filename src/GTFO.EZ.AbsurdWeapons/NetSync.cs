using System;
using System.Collections.Generic;
using GTFO.API;
using Il2CppInterop.Runtime.Attributes;
using MindAttic.GTFO.Shared;
using Player;
using SNetwork;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// Versioned network events (G.AWPN-T235). A message whose layout changed goes out under a new
/// ".v2" event name carrying the shooter's own numbers (speeds, fuse, blast, heal...) and the acting
/// player, followed by the old event for games still on an older version of the mod. A game on this
/// version handles the old event only from a sender it has never heard speak the new one, so nothing
/// is shown twice; the new event goes first and every game says hello at the start of each level
/// (<see cref="NetPresence"/>), so a teammate on this version is known before anything is fired.
/// The old events keep their old layouts byte for byte, so older games still read them.
/// </summary>
internal static class Wire
{
    // Senders (player lookups) seen sending a current-version event.
    private static readonly HashSet<ulong> current = new();

    internal static bool IsCurrent(ulong sender) => current.Contains(sender);

    internal static void Register<T>(string eventName, Action<ulong, T> onReceive) where T : struct =>
        RemoteEvents.Register<T>(eventName, (sender, message) =>
        {
            current.Add(sender);
            NetPresence.Seen(sender);
            onReceive(sender, message);
        });

    // An older version's event: handled only from a sender that hasn't spoken the current one.
    internal static void RegisterLegacy<T>(string eventName, Action<ulong, T> onReceive) where T : struct =>
        RemoteEvents.Register<T>(eventName, (sender, message) =>
        {
            NetPresence.Seen(sender);
            if (current.Contains(sender)) return;
            onReceive(sender, message);
        });

    // To everyone: the current event, then the old one for older games.
    internal static void Send<TCurrent, TLegacy>(string eventName, TCurrent message, string legacyEventName, TLegacy legacy)
        where TCurrent : struct where TLegacy : struct
    {
        NetworkAPI.InvokeEvent(eventName, message);
        NetworkAPI.InvokeEvent(legacyEventName, legacy);
    }

    // To one player (the host, for requests only it acts on): the current event, then the old one.
    internal static void SendTo<TCurrent, TLegacy>(SNet_Player target, string eventName, TCurrent message, string legacyEventName, TLegacy legacy)
        where TCurrent : struct where TLegacy : struct
    {
        NetworkAPI.InvokeEvent(eventName, message, target);
        NetworkAPI.InvokeEvent(legacyEventName, legacy, target);
    }

    // The player lookup a message names, or the GTFO-API sender when it names none (0).
    internal static ulong Actor(ulong named, ulong sender) => named != 0 ? named : sender;

    internal static ulong LookupOf(PlayerAgent? player) => player != null && player.Owner != null ? player.Owner.Lookup : 0UL;
}

/// <summary>
/// Who else in the lobby runs this mod, and a once-per-level warning when the host doesn't. Several
/// things only take effect on the host's game - enemies are the host's: a knockback a client lands is
/// a request the host carries out, Shadow Dagger stealth works by the host's enemies ignoring the
/// hidden player, and the Shrink Beam's slowdown is the host's copy of the enemy slowing down. Without
/// the mod on the host those silently did nothing; now the client says so, once, in the event log.
/// Every game says hello to the rest at the start of each level (and keeps asking while the host
/// hasn't answered); any game hearing a hello answers it, and any event from a game counts too.
/// </summary>
internal sealed class NetPresence : MonoBehaviour
{
    private const string HelloEvent = "MindAttic.GTFO.EZ.AbsurdWeapons.Hello.v2";
    private const float HelloEverySeconds = 5f;
    private const float LevelStartHelloSeconds = 30f;
    // A client waits this long into a level for the host's answer before warning.
    private const float WarnGraceSeconds = 10f;

    private static readonly HashSet<ulong> withMod = new();
    private static bool inLevel, warned;
    private static float levelStartedAt, nextHello;

    public NetPresence(IntPtr ptr) : base(ptr) { }

    [HideFromIl2Cpp]
    internal static void Init() =>
        Wire.Register<Hello>(HelloEvent, (sender, message) =>
        {
            if (message.Reply != 0) return;
            var player = SNet.TryGetPlayer(sender, out var found) ? found : null;
            if (player != null) NetworkAPI.InvokeEvent(HelloEvent, new Hello(1), player);
        });

    [HideFromIl2Cpp]
    internal static void Seen(ulong sender) => withMod.Add(sender);

    // Whether the host's game is known to run this mod (any version). True on the host itself.
    [HideFromIl2Cpp]
    internal static bool HostHasMod
    {
        get
        {
            if (SNet.IsMaster) return true;
            var master = SNet.Master;
            return master != null && withMod.Contains(master.Lookup);
        }
    }

    // Called by a client wherever something it just did needs the host's copy of the mod.
    [HideFromIl2Cpp]
    internal static void WarnIfHostLacksMod(string what)
    {
        try
        {
            if (warned || !inLevel || HostHasMod || Time.time - levelStartedAt < WarnGraceSeconds) return;
            warned = true;
            var message = $"GTFO.EZ.AbsurdWeapons: the host isn't running this mod - {what} won't reach enemies (knockback, Shadow Dagger stealth and Shrink Beam slowdown all need it on the host).";
            MessageHelper.Show(message, MessageType.EventLog);
            Plugin.Logger.LogWarning(message);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons host check failed: {e.Message}");
        }
    }

    private void Update()
    {
        try
        {
            var now = GameStateManager.CurrentStateName == eGameStateName.InLevel;
            if (now != inLevel)
            {
                inLevel = now;
                warned = false;
                levelStartedAt = Time.time;
                nextHello = Time.time + 1f; // a moment for the level's players to settle in
            }
            if (!inLevel || Time.time < nextHello) return;
            nextHello = Time.time + HelloEverySeconds;
            if (Time.time - levelStartedAt < LevelStartHelloSeconds || !HostHasMod)
                NetworkAPI.InvokeEvent(HelloEvent, new Hello(0));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"GTFO.EZ.AbsurdWeapons hello failed: {e.Message}");
            nextHello = Time.time + 30f;
        }
    }

    // Plain fields so GTFO-API can copy it as raw bytes.
    private readonly struct Hello
    {
        public readonly byte Reply; // 0: hello, 1: an answer to one (never answered again)
        public Hello(byte reply) => Reply = reply;
    }
}
