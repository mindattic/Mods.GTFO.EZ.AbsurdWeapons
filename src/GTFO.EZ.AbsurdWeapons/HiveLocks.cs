using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Enemies;
using Gear;
using Il2CppInterop.Runtime.Attributes;
using Player;
using UnityEngine;

namespace GTFO.EZ.AbsurdWeapons;

/// <summary>
/// The Hive guns' lock-on markers, after Titanfall 1's Smart Pistol: while one is in hand, every
/// enemy in sight near the crosshair (the Hive kind's lock cone, range and max locks - the same
/// enemies its rounds curve onto) wears a camera-facing diamond per pip, building from orange to
/// red over a moment (tough enemies get up to three, one per <see cref="HealthPerPip"/> of health).
/// Purely the look: every round curves onto one of these enemies whatever the pips show
/// (SeekerGuns.NextLock). Local only - teammates see the rounds, not your markers.
/// </summary>
internal sealed class HiveLocks : MonoBehaviour
{
    internal static ConfigEntry<float> PipSeconds = null!;
    internal static ConfigEntry<float> HealthPerPip = null!;
    internal static ConfigEntry<int> MaxPips = null!;

    internal static void Bind(ConfigFile config)
    {
        PipSeconds = config.Bind("Hive Guns", "LockPipSeconds", 0.25f, "Seconds each lock pip takes to build while the enemy stays near the crosshair.");
        HealthPerPip = config.Bind("Hive Guns", "LockHealthPerPip", 40f, "An enemy needs one lock pip per this much health (at least one).");
        MaxPips = config.Bind("Hive Guns", "LockMaxPips", 3, "The most pips any enemy needs, however tough (Titanfall's pilots took three).");
    }

    private static readonly Color Acquiring = new(1f, 0.6f, 0.15f, 0.9f);
    private static readonly Color Locked = new(1f, 0.1f, 0.1f, 1f);
    private static Material? material;

    private sealed class Track
    {
        public EnemyAgent Enemy = null!;
        public float Progress; // seconds of lock built up
        public int Needed;     // pips this enemy takes
        public GameObject Marker = null!;
        public LineRenderer[] Rings = Array.Empty<LineRenderer>(); // one per pip
    }

    private static readonly Dictionary<IntPtr, Track> tracks = new();

    public HiveLocks(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        try
        {
            var player = PlayerManager.GetLocalPlayerAgent();
            var id = player?.Inventory?.WieldedItem?.TryCast<BulletWeapon>()?.ArchetypeData?.persistentID ?? 0;
            var camera = player?.FPSCamera;
            if (player == null || !player.Alive || camera == null || SeekerKind.ForGun(id) != SeekerKind.Hive
                || FocusStateManager.CurrentState != eFocusState.FPS)
            {
                ClearAll();
                return;
            }

            var hive = SeekerKind.Hive;
            var inCone = SeekerGuns.FindLocks(camera.Position, camera.Forward, hive.LockConeDegrees, hive.LockRange);
            var seen = new HashSet<IntPtr>();
            // Exactly the enemies NextLock cycles between: MaxLocks 0 (or less) locks none, so none is marked.
            for (var i = 0; i < inCone.Count && i < hive.MaxLocks; i++)
            {
                var enemy = inCone[i];
                seen.Add(enemy.Pointer);
                if (!tracks.TryGetValue(enemy.Pointer, out var track))
                    tracks[enemy.Pointer] = track = NewTrack(enemy);
                track.Progress = Mathf.Min(track.Progress + Time.deltaTime, track.Needed * PipSeconds.Value);
            }

            List<IntPtr>? lost = null;
            foreach (var (key, track) in tracks)
                if (!seen.Contains(key) || track.Enemy == null || !track.Enemy.Alive) (lost ??= new List<IntPtr>()).Add(key);
            if (lost != null)
                foreach (var key in lost) Drop(key);

            var right = camera.transform.right;
            var up = camera.transform.up;
            foreach (var track in tracks.Values) Draw(track, right, up);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"GTFO.EZ.AbsurdWeapons hive locks failed: {e.Message}");
            ClearAll();
        }
    }

    private static bool IsLocked(Track track) => track.Progress >= track.Needed * PipSeconds.Value - 0.0001f;

    [HideFromIl2Cpp]
    private static Track NewTrack(EnemyAgent enemy)
    {
        var health = 20f;
        try
        {
            if (enemy.Damage != null) health = enemy.Damage.HealthMax;
        }
        catch (Exception)
        {
            // Keep the default.
        }
        var needed = Mathf.Clamp(Mathf.CeilToInt(health / Mathf.Max(1f, HealthPerPip.Value)), 1, Math.Max(1, MaxPips.Value));

        var marker = new GameObject("GTFO.EZ.AbsurdWeapons Hive Lock");
        var rings = new LineRenderer[needed];
        for (var i = 0; i < needed; i++)
        {
            var ring = new GameObject($"Pip {i + 1}");
            ring.transform.SetParent(marker.transform, false);
            var line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.numCornerVertices = 1;
            if (material == null) material = new Material(Shader.Find("Sprites/Default")); // Unity's null: rebuilt if unloaded
            line.material = material;
            rings[i] = line;
        }
        return new Track { Enemy = enemy, Needed = needed, Marker = marker, Rings = rings };
    }

    // A diamond per pip around the enemy's chest, facing the camera: the pips built so far drawn,
    // orange while acquiring, all red once locked.
    [HideFromIl2Cpp]
    private static void Draw(Track track, Vector3 right, Vector3 up)
    {
        var center = track.Enemy.AimTarget.position;
        var pipSeconds = Mathf.Max(0.01f, PipSeconds.Value);
        var built = Mathf.Min(track.Needed, Mathf.FloorToInt(track.Progress / pipSeconds + 0.0001f));
        var locked = IsLocked(track);
        var partial = (track.Progress % pipSeconds) / pipSeconds;
        for (var i = 0; i < track.Rings.Length; i++)
        {
            var line = track.Rings[i];
            // The pip being built grows in; built ones sit at full size.
            var grow = i < built ? 1f : i == built ? partial : 0f;
            line.enabled = grow > 0.02f || locked;
            if (!line.enabled) continue;
            var size = (0.32f + 0.12f * i) * Mathf.Lerp(0.4f, 1f, locked ? 1f : grow);
            line.SetPosition(0, center + up * size);
            line.SetPosition(1, center + right * size);
            line.SetPosition(2, center - up * size);
            line.SetPosition(3, center - right * size);
            var color = locked ? Locked : Acquiring;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = locked ? 0.035f : 0.02f;
        }
    }

    private static void Drop(IntPtr key)
    {
        if (tracks.TryGetValue(key, out var track) && track.Marker != null) Destroy(track.Marker);
        tracks.Remove(key);
    }

    private static void ClearAll()
    {
        if (tracks.Count == 0) return;
        foreach (var key in new List<IntPtr>(tracks.Keys)) Drop(key);
    }
}
