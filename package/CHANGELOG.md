# Changelog

## Unreleased

- **Bug fix (G.AWPN-T236, for GTFO.EZ's G.EZ-T125 config sync):** a weapon switched off with its
  `<Weapon> Enabled` setting used to skip registering its datablocks altogether, so two players with
  different switches had different gear categories - a teammate's Absurd weapon could be an id your
  game had never heard of. Every weapon is now always registered; `Enabled` only takes it out of the
  loadout screen and the weapon cycle.
- **Bug fix (G.AWPN-T235, from the G.EZ-T122 audit): teammates' copies now use the shooter's
  settings.** A teammate's game flew your grenades, rockets and thrown Mjolnir, ran your Shrink Beam
  and healed from your Victory Banner with *its own* config, so with different settings they bounced,
  fused, flew, shrank or healed differently from game to game. Every launch, throw, beam tick and
  plant now carries the shooter's numbers (grenade gravity/bounce/fuse, rocket speed/lifetime,
  Mjolnir speeds/reach/spin, shrink per tick and its floor, banner radius/heal rate/heal amount/
  lifetime), and every game uses those.
- **Stealth, thrown Mjolnir, Victory Banner, Shrink Beam and knockback messages name the player
  they're about**, instead of assuming it's whichever game sent them.
- **The host lacking the mod is no longer silent:** knockback, Shadow Dagger stealth and the Shrink
  Beam's slowdown only take effect on the host's game. A client now finds out whether the host runs
  the mod (a hello at the start of each level), and if it doesn't, says so once per level in the event
  log and the console the first time one of those comes up.
- **Switching Absurd Weapons on or off (End) now shows in teammates' event logs**, naming who did it,
  because it changes your loadout in front of them and switching off resets your health.
- Rocket splash damage to players was checked: it already hurts the shooter, bots and teammates
  alike, once each. The game's explosion damage to a player is sent to the game that owns that
  player, so it isn't limited to the shooter's own machine. Unchanged.
- **Network messages are versioned:** every message whose layout changed is sent under a new `.v2`
  name, followed by the old message for players still on an older version. Players on this version
  ignore the old copy from anyone they've heard the new one from, so nothing shows up twice. Mixing
  versions still works, but an older game uses its own settings for your projectiles, as before.

## v20

- Dependency bump: `BepInExPack_GTFO` 3.2.3 -> 3.2.4. Moves in step with the GTFO.EZ family's
  version number (GTFO.EZ, GTFO.EZ.AbsurdWeapons, GTFO.EZ.AbsurdMoves, GTFO.EZ.AbsurdItems,
  GTFO.CrashTestDummies and GTFO.SkipDrop share one). No change in this mod's own behavior.

## v19

- Moves in step with the GTFO.EZ family's version number (GTFO.EZ, GTFO.EZ.AbsurdWeapons,
  GTFO.EZ.AbsurdMoves, GTFO.EZ.AbsurdItems, GTFO.CrashTestDummies and GTFO.SkipDrop share one). No
  change in this mod's own behavior.

## v18

- **Bug fix:** `BladeStretcher`'s cache of each blade's unstretched transform was keyed by instance
  ID alone, with no way to tell a destroyed blade from a live one, so it grew for as long as the game
  process ran (every rebuilt model - the loadout screen, a re-equip, a teammate's model - added an
  entry that never came back out). It now prunes destroyed blades before each use.
- **The Absurd Weapons on/off toggle now also announces in the event log**, not just the short line
  under the crosshair and the console - same as the Insert/Delete infinite health and ammo toggles.
- Moves in step with the GTFO.EZ family's version number (GTFO.EZ, GTFO.EZ.AbsurdWeapons,
  GTFO.EZ.AbsurdMoves and GTFO.EZ.AbsurdItems share one).

## v17

- `GTFO.EZ.Shared` submodule bump: `SharedOwner.Order` no longer lists `GTFO.CrashTestDummies`,
  which dropped Insert/Delete (and this submodule) in its own v17. No change in this mod's own
  behavior.
- `GTFO.CrashTestDummies` and `GTFO.SkipDrop` join the `vN` scheme this release, so every GTFO.*
  mod now shares one version number.

## v16

- **The Absurd Weapons toggle moved from Home to End.** GTFO.EZ.AbsurdMoves' own toggle moves the
  other way, onto Home.
- **Insert and Delete swapped: Insert is now infinite health, Delete is now infinite ammo.** Both
  also always announce in the console now, not just the event log. Shared code (`GTFO.EZ.Shared`),
  so GTFO.EZ, its Absurd companions and GTFO.CrashTestDummies all pick it up.
- Moves in step with the GTFO.EZ family's version number (GTFO.EZ, GTFO.EZ.AbsurdWeapons,
  GTFO.EZ.AbsurdMoves and GTFO.EZ.AbsurdItems share one).

## v15

- **Absurd Weapons is a toggle again:** off at the start of every level. **Home** switches it on,
  snapshotting your current loadout and health, so swapping mid-level (below) starts working; **Home**
  again switches it off, putting every swapped slot and your health back to that snapshot - out of
  whatever Absurd weapon you're holding and back to a serious run - all at once. The on/off state is
  announced on-screen and always in the console too.
- Moves in step with the GTFO.EZ family's version number (GTFO.EZ, GTFO.EZ.AbsurdWeapons,
  GTFO.EZ.AbsurdMoves and GTFO.EZ.AbsurdItems share one).

## v14

- **Bug fix:** the lasers, Chain Lightning, Shrink Beam and Tentacle Cannon emptied in about two
  seconds. Their ammo now goes by how long you hold the trigger: a full magazine lasts 10 seconds of
  sustained fire (new `[Ammo] MagazineSeconds`, replacing `StreamPercentPerSecond`). The beam guns
  refill an empty magazine from the reserve instead of reloading; the Tentacle Cannon no longer
  charges a round per bite and reloads like any gun.
- **Rocket Launcher:** the rocket is now an orange-glowing fireball instead of a little missile, and
  its dust trail starts right at the muzzle - dense near the gun so it reads as one continuous trail,
  thinning to the old sparse puffs further out - instead of only beginning some 20 m away.
- **Bug fix:** the Plasma Pistol and Rifle no longer charge up before firing.
- **Bug fix:** a Chain Lightning or Mjolnir bolt that strikes exactly one enemy flashes again.
- **Bug fix:** the Shadow Dagger's purple glow no longer sticks to the weapon swapped in after it.
- **Chain Lightning** glows a soft violet of its own.
- **Bug fixes - walls and doors:** grenades no longer go off on an enemy or player just behind a
  wall or door; rockets, grenades and plasma rounds fired up against a wall no longer start past it;
  rocket and plasma blasts no longer reach through the wall or ceiling they hit; a point-blank
  Mjolnir throw no longer starts on the far side of a wall, and its hit no longer reaches through
  doors; the Warhammer slam no longer lands past a wall.
- **Bug fixes - where shots start:** rocket, grenade, plasma and hive shots aimed at the floor ahead
  (even far down a corridor) leave the barrel again instead of your eye, and a shot at an enemy
  closer than the barrel no longer starts inside or past it.
- **Bug fixes - bots:** on the host, a bot's shots no longer change your ammo display, play your
  Railgun or Tentacle Cannon sounds, use up your tentacle spots or get infinite Tentacle Cannon ammo,
  and your right-click no longer sets off a bot's rockets. The Victory Banner heals bots too.
- **Bug fixes - multiplayer:** teammates hear the Railgun; a teammate's dropped Shrink Beam no longer
  shrinks enemies on everyone else's game; a rocket or grenade fired point-blank into a teammate no
  longer explodes at the map origin.
- **Bug fixes - other:** swapping a weapon back in never refills its magazine; a slow swap can't move
  the new weapon's ammo onto the old one; the Warhammer slam fires one shockwave, not two; the
  Katana's charge-up keeps its normal pace during a dash; plasma rounds no longer leak a material
  copy per shot; cached materials are rebuilt if the game unloads them.
- Moves in step with the GTFO.EZ family's version number (GTFO.EZ, GTFO.EZ.AbsurdWeapons,
  GTFO.EZ.AbsurdMoves and GTFO.EZ.AbsurdItems share one).

## v13

- **Versioning:** the GTFO.EZ family - GTFO.EZ, GTFO.EZ.AbsurdWeapons, GTFO.EZ.AbsurdMoves and
  GTFO.EZ.AbsurdItems - now shares one version number, so this first release is **v13**, in step
  with GTFO.EZ, rather than v1.
- Renamed from GTFO.AbsurdWeapons to **GTFO.EZ.AbsurdWeapons** before release, as a GTFO.EZ companion mod built
  on GTFO.EZ's core (plugin GUID `MindAttic.GTFO.EZ.AbsurdWeapons`, config file
  `MindAttic.GTFO.EZ.AbsurdWeapons.cfg`, Thunderstore package `GtfoEZAbsurdWeapons`).
- Master switch: `[0. Mod] Enabled` (default `true`) is the config file's first entry; set it to
  `false` and the whole mod is off - nothing is patched, no weapon is added, no hotkey.
- **Moved out:** the Hand Grenade, the Stim Pack and the loot mix now live in
  [GTFO.EZ.AbsurdItems](https://github.com/mindattic/GTFO.EZ.AbsurdItems), with the same `[Hand Grenade]`
  and `[Stim Pack]` settings and `HandGrenadeThrow`/`HandGrenadeBounce` sounds; its kit loot
  (*Consumables Are Kits*) runs them as one loot run, under GTFO.EZ's kit setting names
  (`Kit Loot (%)`, `Health Kit Weight`, ...) in place of `KitLootPercent`, `HealthKitWeight` and the
  rest. Install it to keep them. This mod no longer changes consumables or loot; its `[Loot]`,
  `[Hand Grenade]` and `[Stim Pack]` sections stay in an existing `.cfg` as leftover lines that do
  nothing. Eighteen weapons now.
- Loot uses GTFO.EZ's kit loot, shared between the two mods: a few small kits of 1-2 charges
  adding about 20% to the zone's resource packs (`KitLootPercent`, `KitChargesMin/Max`), with Hand
  Grenades at 4 to the kits' 3 and Stim Packs alongside. `KitSize` and `SpawnsPerZoneMultiplier`
  are gone.
- Insert (infinite ammo) and Delete (infinite health) are the shared toggles every GTFO.* mod
  has; `[Ammo] InfiniteAmmo` and `InfiniteAmmoToggleKey` are gone.
- Artifacts stay: Hand Grenades now join artifacts instead of replacing them - artifacts are as rare
  as vanilla. Grenades are common instead: half the loot mix (`HandGrenadeWeight` 4) and half as
  much loot again per zone (`SpawnsPerZoneMultiplier` 1.5). The `ArtifactsBecomeHandGrenades` setting is
  gone.
- Loot: where it clashes with GTFO.EZ, this mod's loot wins - GTFO.EZ's kit-only tables (its
  *Consumables Are Kits*) get this mod's mix too, Hand Grenades and Stim Packs alongside kits at
  this mod's own kit odds and size.
- Every added gun fires the moment you pull the trigger: none charges up first or cools down
  after. The Plasma Pistol/Rifle had picked up a charge-up from their stat source, the Omneco LRG HEL.
- Initial release with nineteen weapons, each marked **(!)**:
  - **Melee:** Katana (the Mastaba blade at twice a real katana's length; a full charge dashes up to
    10 m with the swing held, cutting the moment you reach an enemy or a wall), Mjolnir (a thunderclap on
    every hit; a full charge throws it end over end, throwing chain lightning as it flies out and
    back), Warhammer (500-damage blow, shockwave, a crater of scattered debris on a full-charge slam,
    flings enemies up to 12 m, and walls hurt them), Shadow Dagger
    (a fully charged hit makes you unseen for 3 s; kills while hidden add +25% damage up to +100%,
    every kill while hidden also adds +0.33 s, up to a 3 s cap that drains 5% with every enemy hit, resetting when the stealth ends; putting it away ends it).
  - **Guns:** Plasma Pistol/Rifle (Doom plasma), Hive Pistol/Rifle (Titanfall smart
    rounds, with lock-on markers), Laser Pistol/Rifle (continuous burning beams), Tentacle Cannon (bursts of striker tentacles
    from spots around the edge of your view, biting a nearby enemy or reaching for walls and floors otherwise), Railgun (a slow, devastating hitscan slug with infinite penetration and a double-helix beam),
    Chain Lightning Gun (bolts that jump between up to six enemies), Grenade Pistol/Rifle (lobbed lock-melter grenades that blow on
    an enemy or bounce and blow after 2.5 s, flashing red faster and faster), Rocket Launcher (Quake 1's
    rocket: dead straight, no drop, trailing sparks and dust, blowing up the first thing it touches - and
    nearby players too), Shrink Beam (a continuous ray that shrinks whatever it's held on until it gibs
    at a third of its size; once one's half-size or smaller, look down at it within 1 m to step on it).
  - **Tool:** Victory
    Banner (plant a flag with a glowing healing aura, pick it back up; heals everyone standing in it).
  - **Consumable:** Hand Grenade (a lock melter thrown like a glowstick, lobbed twice as hard, that bounces and blows after
    3 s; nobody starts with any; each one found is 1 more, up to 6) and Stim Pack (an overheal
    like Doom's: +50% health, up to 200%, draining back to 100%).
- Guns use ammo like vanilla guns; each starts full the first time it's equipped in a level and keeps
  its own ammo after that (`[Ammo] InfiniteAmmo` brings back bottomless guns).
- Loot consumables are only Hand Grenades and health, tool and ammo kits (`[Loot]`), plus Stim
  Packs when switched on - the Stim Pack is off by default for now.
  Artifacts stay as rare as vanilla.
- Swap weapons in a level by pressing a slot's own key again (1 to 4 by default, following rebinds): the
  gear you dropped in with joins the cycle the first time you swap. No keys of its own - the Page Up /
  Page Down / Home keys and the `[Gear Cycling]` settings are gone. The weapon's name shows for three
  seconds on the line under the crosshair.
- Config: every weapon has its own on/off switch (`<Weapon> Enabled`, pistol and rifle versions
  separately), and 200+ settings retune them - damage, rate of fire, ranges, blasts, the Katana's
  dash, Mjolnir's throw, the Warhammer's shockwave and slam, the Shadow Dagger's stealth, Chain
  Lightning's jump count, every sound, and more. `[Tentacles] PoolMultiplier`
  raises how many tentacles the game can draw at once. Full reference in the README.
- Fix: the Shadow Dagger's purple no longer sticks to the weapon you swap to. Its glow goes out the
  moment the dagger leaves your hand, and the stealth tint gives each weapon back its own look
  when it ends, even if one part fails to restore.
- The Chain Lightning Gun now glows a soft violet while it's in your hand - the purple the Shadow
  Dagger used to leave on it by mistake, kept on purpose as the gun's own glow.
