# GTFO.EZ.AbsurdWeapons

A GTFO mod that adds eighteen over-the-top weapons, from a dashing katana and a returning Mjolnir to tentacle, laser, rail and shrink-ray guns, built at load time from the game's own data.

![Language](https://img.shields.io/badge/language-C%23%20(.NET%206)-blue)
![Platform](https://img.shields.io/badge/platform-BepInEx%20IL2CPP-informational)
![Status](https://img.shields.io/badge/status-in%20development%20(v20)-yellow)

![Absurd Weapons cover art: GTFO prisoners firing a tentacle gun, a laser cannon and curving smart rounds, and swinging a giant spiked hammer](logo.png)

Not yet published on Thunderstore; the package name will be `GtfoEZAbsurdWeapons`. Until then, build
it yourself (see Quick start) and share the zip from `dist/`.

## Why

- Get weapons GTFO never had - a stealth dagger, a hammer you throw, a rocket you detonate by
  right-click, a beam that shrinks enemies until you can step on them - without losing any vanilla gear.
- Swap between them mid-level by pressing a slot's key again, so you can try the whole arsenal in one
  drop instead of going back to the lobby.
- Stay in sync with friends: the shooter's game deals damage through the game's own bullet path, and
  every shot, beam, tentacle, bolt and blast is sent to teammates so they see the same thing.
- Survive game patches: no data files are shipped or edited. Every weapon is cloned from the game's
  own loaded datablocks at startup, so it inherits vanilla balance changes.
- Make it yours: every weapon can be switched off, and 200+ config settings retune them.

## Features

Every added weapon shows "(!)" after its name, so it stands out from vanilla gear in the loadout
screen. The player-facing breakdown of each one, with numbers, is in
[package/README.md](package/README.md).

**Melee (4)**

- **Katana** - a ~1.85 m blade that cleaves through the arc; a full charge dashes you up to 10 m with
  the swing held and cuts the moment you reach an enemy or a wall.
- **Mjolnir** - every landed hit releases a thunderclap; a full charge throws it out to 25 m and back,
  crackling chain lightning at enemies it passes.
- **Warhammer** - a giant-headed sledgehammer: 500-damage charged blow, every hit flings enemies (walls
  hurt them), and a full charge slams the floor in a shockwave.
- **Shadow Dagger** - stabs 2.5x as fast; a fully charged hit makes you invisible to enemies for 3 s,
  and kills while hidden sharpen the blade and stretch the stealth.

**Guns (13)**

- **Plasma Pistol / Rifle** - Doom's plasma gun: rapid blue plasma balls with a small shock on impact.
- **Hive Pistol / Rifle** - Titanfall's Smart Pistol: rounds curve onto enemies locked near the crosshair.
- **Laser Pistol / Rifle** - one continuous 70 m beam while the trigger is held.
- **Tentacle Cannon** - striker tentacles lash out of the floor around you and bite what they reach.
- **Grenade Pistol / Rifle** - lobbed grenades that bounce and blow after 2.5 s, or at once on an enemy.
- **Railgun** - a hitscan slug through every enemy on the line, walls included, in a double helix.
- **Chain Lightning Gun** - a stream of lightning that jumps from enemy to enemy, up to five times.
- **Rocket Launcher** - Quake's rocket: dead straight, no drop; right-click detonates it mid-air.
- **Shrink Beam** - shrinks whatever it holds on until it gibs; step on small ones.

**Tool (1)**

- **Victory Banner** - plant a flag in front of you that heals everyone within 8 m; pick it back up
  like a sentry.

**Around the weapons**

- In-level swapping on a second press of the slot key, with each weapon keeping its own ammo.
- Insert / Delete toggle infinite health and infinite ammo, the shared toggles GTFO.EZ, its Absurd
  companions and GTFO.CrashTestDummies all carry.
- A master switch and a per-weapon `Enabled` switch in the config.

## Quick start

Requires the [.NET 6 SDK](https://dotnet.microsoft.com/) and `BepInExPack_GTFO` already installed
and launched at least once ("Modded" start) in a Thunderstore Mod Manager or r2modman profile.

```powershell
git clone --recurse-submodules https://github.com/mindattic/GTFO.EZ.AbsurdWeapons.git
cd GTFO.EZ.AbsurdWeapons
.\build.ps1
```

This builds against the Thunderstore Mod Manager profile named "Easy Mode DEV" by default
(`.\build.ps1 -Profile MyProfile` to use another one) and places the plugin straight into that
profile's `BepInEx/plugins/GTFO.EZ.AbsurdWeapons/` folder, so you can launch the game and test
immediately. It also produces `dist/GTFO.EZ.AbsurdWeapons-<version>.zip` for sharing.

If you're on the older r2modman app instead, its profiles live under a different path. Point the
build at it directly:

```powershell
dotnet build .\src\GTFO.EZ.AbsurdWeapons\GTFO.EZ.AbsurdWeapons.csproj -p:BepInEx="$env:AppData\r2modmanPlus-local\GTFO\profiles\Default\BepInEx"
```

Launch the game. `BepInEx/LogOutput.log` should show `GTFO.EZ.AbsurdWeapons v20.0.0 loaded.`, one
`GTFO.EZ.AbsurdWeapons added the <weapon> ...` line per weapon and, when a melee model is built, its
blade length before and after. The new weapons appear in the loadout screen in their normal slots.

## Usage

- **Pick weapons** in the loadout screen like any vanilla gear: they sit in the melee, main, special
  and tool slots, each marked "(!)".
- **Absurd Weapons is a toggle, off at the start of every level:** press **End** to turn it on - it
  snapshots your current loadout and health - and swapping mid-level (below) starts working. Press
  **End** again to turn it off: every slot you swapped goes back to that snapshot's gear and ammo
  (dropping you out of whatever Absurd weapon you're holding), and your health is set back to it too -
  so the run can go on as if you'd never touched them. Teammates see who switched it on or off in
  their event log.
- **Swap mid-level** (once Absurd Weapons is on): with a slot already in hand, press that slot's own
  select key again (1 to 4 by default - melee, main, special, tool - following rebinds) to move to its
  next weapon. The first swap of a slot adds the gear you dropped in with to the front of its cycle. A
  line under the crosshair names the new weapon, and teammates see the swap.
- **Full charges** (hold the melee charge to at least 95% of full) trigger the Katana dash, the
  Mjolnir throw, the Warhammer slam and the Shadow Dagger stealth.
- **Rocket Launcher:** right-click detonates your oldest rocket in flight, one per click.
- **Victory Banner:** fire to plant it; look at it within 2.5 m and press Use to pick it up.
- **Insert** toggles infinite health and **Delete** infinite ammo (for the whole team, whoever presses it).

Everyone in a lobby needs the identical mod version, with the same weapons switched on: weapons sync
by id.

## How it works

Like GTFO.EZ, this ships no copies of the game's data files. It patches `GameDataInit.Initialize`
and registers new datablocks cloned from the game's own loaded ones at runtime. Nothing vanilla is
edited.

```text
GameDataInit.Initialize (game boot)
        |
        v
  MeleeInjector / GunInjector postfixes  [HarmonyAfter("MindAttic.GTFO.EZ")]
        |
        +--> per weapon: skip if its id is already taken (registered whatever Enabled says)
        +--> clone archetype + gear category + offline gear at the weapon's fixed id
        +--> rewrite the gear JSON (category, name + "(!)", fire mode, hold)
        v
  in a level: Harmony patches and injected MonoBehaviours run each weapon's behaviour
  (projectiles, beams, tentacles, dashes, bursts), sent to teammates over GTFO-API
```

### Datablocks

`MeleeInjector` and `GunInjector` build each weapon as three cloned blocks (archetype, gear category,
offline gear) at a fixed id. Every block for a weapon uses that weapon's id. The offline gear's JSON is
rewritten to point at the new category and renamed; the name gets its "(!)" marker from
`DataBlocks.DisplayName`, while block names stay unmarked. The model's own gear entry also decides the
inventory slot.

| Weapon | Id | Archetype from | Category from | Model (gear) from | Changes |
|---|---|---|---|---|---|
| Katana | 1002 | `Bat` | `MeleeBat` | `Knife` (Mastaba Fixed Blade, blade x6.9 = ~1.85 m) | light 4 / charged 14, reach 3.5, cleaves; the dash is runtime (`KatanaDash`) |
| Mjolnir | 1003 | `Bat` | `MeleeBat` | `Mallet` (Santonian Mallet) | none beyond id and name; the thunderclap and throw are runtime (`ImpactBursts`, `MjolnirThrow`) |
| Warhammer | 1005 | `Hammer` | `MeleeSledgehammer` | `Sledgehammer` (Kovac) | hammer's own animation set and charge; charged 500 / light 6, stagger x55 charged / x11 light, environment x10, hit width 0.6, reach 2.4, cleaves, half speed charging; the shockwave, knockback and slam are runtime (`ImpactBursts`, `Knockback`, `WarhammerSlam`) |
| Shadow Dagger | 1006 | `Knife` | `MeleeKnife` | `Knife` (Mastaba Fixed Blade) | light attacks 2.5x as fast (`SwordTempo`); the stealth is runtime (`Stealth`) |
| Plasma Pistol | 1101 | the LRG's archetype (shared with the Plasma Rifle) | the Steigro's (`Pistol_Stagger`) | `Autopistol staggering` (Raptus Steigro, main, no sight) | `AddPlasma`: `FireMode` Auto, damage 22.5 (display; rolled per hit), shot delay 3/35 s, clip 100, own kickless `RecoilDataBlock` 1101, own low hold at 1101 (`AddLowHold`) |
| Hive Pistol | 1102 | the HXC's archetype (shared with the Hive Rifle) | the S49's (`Pistol`) | `PistolSemi` (Shelling S49, main, no sight) | `AddHive`: `FireMode` Auto, damage 1, shot delay 0.09 s, clip 30, own kickless `RecoilDataBlock` 1102 |
| Tentacle Cannon | 1103 | the XDist2's archetype | the XDist2's | `ShotgunRanged` (Buckland XDist2, special, iron sights) | `AddTentacle`: `FireMode` Auto, 1 pellet, no cone or spread, damage 25 (per tentacle bite), shot delay 0.235 s, `ShellCasingSize` 0, clip 60, no spin-up, own kickless `RecoilDataBlock` 1103, own first-person hold `ItemFPSSettingsDataBlock` 1103 (`AddFarLowHold`: vanilla `Shotgun` pushed lower and further out, aim FOV 34); the lashes are runtime (`TentacleCannon`) |
| Hive Rifle | 1104 | the HXC's archetype | the HXC's (`RifleHeavy`) | `RifleHeavyAuto` (Malatack HXC, special) | `AddHive`: same stats as the Hive Pistol, kickless recoil 1104 |
| Plasma Rifle | 1105 | the LRG's archetype | the LRG's (`Gauss_Rifle`) | `HELRifleSemi` (Omneco LRG, special) | `AddPlasma`: same stats as the Plasma Pistol, kickless recoil 1105 |
| Laser Pistol | 1106 | the DEL P1's archetype (shared with the Laser Rifle) | the Treffen 2's | `Pistolauto` (Raptus Treffen 2, main, no sight) | `AddLaser`: `FireMode` Auto, damage 2 per tick, beam 70 m, shot delay 0.05 s (20 ticks/s), clip 100, no spread, `ShellCasingSize` 0, kickless recoil 1106; its hold is a steady copy of `ItemFPSSettings` 48 (`Pistol_Chunky`, 10 cm lower, so the beam shows) at 1106 (`AddSteadyHold`); the beam is runtime (`LaserGuns`) |
| Laser Rifle | 1107 | the DEL P1's archetype | the ND6's | `SMGHeavyAuto` (Accrat ND6, no animated parts) | `AddLaser`: same stats as the Laser Pistol, kickless recoil 1107, a steady copy of the model's own hold at 1107; the ND6 is a main-slot SMG in vanilla, so its cloned gear category's `BaseItem` is forced to the special slot's item (`forceSpecialSlot: true`) |
| Railgun | 1108 | the PR 11's archetype for its fire mode | the PR 11's (`Sniper`) | `SniperSemi` (Koning PR 11, special) | `AddRailgun`: Semi, damage 9999 (to every enemy on the line), shot delay 1.5 s, clip 5, no spread, `ShellCasingSize` 0, kickless recoil 1108, the sniper's own scope; the slug is runtime (`Railgun`) |
| Chain Lightning Gun | 1109 | the exp1's archetype for its fire mode | the exp1's (`MachineGun_LowRPM`) | `HELGun` (Omneco exp1, special) | `AddChainLightning`: Auto, damage 5 (first strike), shot delay 0.15 s, clip 60, no spread or spin-up, no brass, kickless recoil 1109; the bolts are runtime (`ChainLightning`) |
| Grenade Pistol | 1115 | the Arid 5's archetype (shared with the Grenade Rifle) | the 3RB's | `RevolverHEL` (Bataldo 3RB, main) | `AddGrenadeGun`: Semi, 1 round, no spread, damage shown 75 (the blast), shot delay 0.6 s, clip 6, no brass, its own model's recoil; each round is a grenade (`GrenadeLauncher`) |
| Grenade Rifle | 1116 | same archetype as the Grenade Pistol | the Veruta XII's | `MachinegunAuto` (TechMan Veruta XII, special) | `AddGrenadeGun`: same stats as the Grenade Pistol, its own model's recoil |
| Rocket Launcher | 1118 | the XDist2's archetype for its fire mode | the XDist2's (`ShotgunRanged`) | `ShotgunRanged` (Buckland XDist2, special, iron sights) | `AddRocketLauncher`: Semi, 1 pellet, no cone or spread, damage shown 110 (the blast's center, for the loadout screen), shot delay 0.8 s, clip 5, no brass, keeps the model's own kick; each shot is a rocket (`RocketLauncher`) |
| Shrink Beam | 1119 | the exp1's archetype for its fire mode | the exp1's (`HELGun`) | `HELGun` (Omneco exp1, special) | `AddShrinkBeam`: Auto, 1 pellet, damage shown 0 (it shrinks, it doesn't deal a plain number), shot delay 0.08 s, clip 100, no brass or spin-up, kickless recoil 1119; the ray is runtime (`ShrinkBeam`) |
| Victory Banner | 1202 | (tools have none) | `GlueGun` | `Glue_distance` (Stalwart Flow G2, tool) | `AddVictoryBanner`: category and gear only; placing, picking up and healing are runtime (`VictoryBanner`) |

Ids are fixed (not "next free") so every player in a lobby agrees on them. Retired ids stay unused so
old loadouts can't pick up a different weapon: 1001 (Yari), 1004 (Rapier), 1007-1009 (Wakizashi,
Odachi, Tanto) and 1010 (Gravity Hammer). If another mod already uses an id, that weapon isn't added
and an error is logged; the rest still load. `SetClipSize` rescales `CostOfBullet` so a full magazine
uses the same share of the reserve as the source gun's. Vanilla names, ids and fields came from a
build-34855 datablock dump (github.com/igobyjama/devile, `Devile/BepInEx/GameData-Dump/34855`).

Both injectors are `[HarmonyAfter("MindAttic.GTFO.EZ")]`, so with GTFO.EZ installed they clone its
already-tuned vanilla blocks, and EZ (keyed on vanilla names and ids) never re-tunes ours.

### Blade stretching

`BladeStretcher` is a postfix on `GearPartHolder.OnAllPartsSpawned`, which runs for every model the
game builds: loadout preview, first person, and teammates' third person. For our categories it scales
`MeleeHeadPart` (the blade) along its length axis, Y by default (all of GTFO's melee parts run along Y;
Auto picks the longest mesh dimension instead). It then shifts the part so the end nearest
`MeleeHandlePart` stays put. Settings live in `WeaponSettings` (`BladeLength`, `BladeLengthAxis`,
`Reach`).

- A one-piece model with no head part (such as the Wox Compact knife) is stretched whole via its
  handle part, pinned at `m_aRightHand` so the grip stays in the hand.
- `BladeLengthAxis = All` scales evenly, anchored at the point nearest the shaft. That gives the
  Warhammer its giant head.
- Visual only: hit reach is the archetype's camera ray.

Original scale and position are cached per instance, so re-applying never compounds. Each build logs
the length in meters before and after, for tuning.

### Seeker guns

`SeekerGuns` and `SeekerProjectile` follow the approach of Dinorush's ExtraWeaponCustomization (EWC).
Vanilla `ProjectileTargeting` homes onto and damages players only, so it can't be used as-is.

- **Firing:** `BulletWeapon.Fire` and `Shotgun.Fire` prefixes (and their synced versions) record which
  archetype is firing. A prefix on `Weapon.CastWeaponRay` (4-arg overload) swallows our guns' hitscan
  rays (`maxRayDist = 0`, return false), so ammo, recoil, muzzle flash and sound stay vanilla. Plasma
  guns set `ShellCasingSize` 0, so no brass. Each ray, one per shotgun pellet, becomes a seeker with the
  pellet's spread applied, spawned at the gun's barrel as drawn on screen (`Barrel.Shot`).
- **Projectile:** `ProjectileManager.SpawnProjectileType` spawns the real enemy projectile for its
  model, glow and trail. Its `ProjectileBase` is fired with no target, silenced and disabled.
  `SeekerProjectile`, an injected MonoBehaviour, then flies it: it locks onto the enemy closest to the
  line of flight (`OverlapSphere` on `MASK_ENEMY_DAMAGABLE`, cone check, `Linecast` against
  `MASK_WORLD`), turns toward it at a capped rate, and sphere-casts each step against enemies,
  projectile blockers and the bullet-ray mask. A seeker that hits something other than an enemy limb
  damages a door lock there (`LockDamage`).
- **Plasma** (`SeekerKind`) is Doom's plasma gun on a `TargetingSmall` (the Shooter's) tinted blue
  (`Tint`: its light, trail, particle start colors and every `Color` slot of every material).
  `Homing = false`, so it flies straight from the barrel at the crosshair point at `BallSpeed` 57.2 m/s
  (twice Doom's). Each hit rolls `DamageScale` x 1d`DamageDie` (5 x 1d8), and every impact adds a
  `BlastRadius` 1.75 m `AreaDamage` shock (sparing the enemy hit directly) with a blue flash and
  `Impact_Electronics` sparks. Lifetime 6 s.
- **Hive** is Titanfall's Smart Pistol on a `SemiTargetingQuick` (the Hybrid's), full-auto.
  `SmartLock`: at the trigger, `NextLock` gathers living, visible enemies within `LockConeDegrees`
  (35) of the crosshair and `LockRange` (60 m), nearest the crosshair first, and successive shots cycle
  through up to `MaxLocks` (6). The round leaves the barrel and flies a quadratic Bezier (barrel, a
  control point 40% of the way along the aim, the target's `AimTarget` re-read every frame) at
  `RoundSpeed` 45 m/s. With no lock, or once its target dies, it homes normally. Lifetime 3 s.
- **Lock markers:** `HiveLocks` marks those same enemies with camera-facing diamonds that build from
  orange to red over `LockPipSeconds` (0.25 s), one per `LockHealthPerPip` (40) health up to
  `LockMaxPips` (3). The look only, local only, and only in first-person focus.
- `Shard` (split seekers) is still defined but nothing uses it right now.
- **Damage:** only the shooter's copy deals damage, through `Dam_EnemyDamageLimb.BulletDamage`, the
  normal bullet path, so it syncs host and client.
- **Multiplayer:** each shot is sent to teammates with GTFO-API's `NetworkAPI`
  (`MindAttic.GTFO.EZ.AbsurdWeapons.SeekerShot`). They spawn a visual-only copy that deals no damage; a
  smart round's message carries its target's position, which the copy curves to. GTFO-API ships inside
  BepInExPack_GTFO, so it isn't a separate dependency.

### Ammo

`InfiniteAmmo` holds the ammo rules.

- **Magazine guns** use ammo like vanilla ones: a magazine, a reserve in the slot's pool, reloads.
- **Held-trigger guns** (lasers, Chain Lightning, Shrink Beam, Tentacle Cannon) spend ammo by time:
  every frame the trigger is held, `InfiniteAmmo.Drain(player, Time.deltaTime)` spends that frame's
  share of a magazine (`ClipSize / [Ammo] MagazineSeconds` rounds a second, default 10 s per
  magazine, fractions carried to the next frame).
- **Stream guns** (lasers, Chain Lightning, Shrink Beam) never reach `Fire` and never reload: `Drain`
  refills an empty magazine from the reserve, and the beam stops once both are empty.
- **Tentacle Cannon:** its shots are refunded (a `BeginFire` prefix saves the magazine and an
  `EndFire` finalizer puts it back); `TentacleAmmoDriver` drains it by time held instead, and it
  reloads like any gun.
- **Infinite ammo** (`InfiniteAmmo.On`, the shared Delete toggle in `Shared/InfiniteToggles.cs`):
  the same save-and-restore refunds every shot of every gun, vanilla included, and the held-trigger
  guns skip their drain.

### Stream readiness

Lasers, Chain Lightning and the Shrink Beam are trigger-driven, so `StreamReadiness` (in
`LaserGuns.cs`) decides when they may run: 0.3 s after the gun is raised, never while it's lowered or
hidden, and not while sprinting unless GTFO.EZ.AbsurdMoves' "Fire While Running Enabled" is on. A
`FeedbackPlayer.DoPlay` prefix mutes their (and the Railgun's) vanilla tracers.

### Tentacle Cannon

`TentacleCannon`, `TentacleLash` and `TentaclePool`.

- **Firing:** the `CastWeaponRay` prefix swallows the tentacle gun's rays too, one per round. Shotgun
  pellet hit data has no `owner` (and its damage always arrives as 0 - the game splits a shotgun's
  damage across its own pellet count, not ours), so the shooter falls back to the firing weapon's
  `Owner` and the damage is read straight from `GunInjector.TentacleDamage`. For the local shooter
  each ray calls `TentacleCannon.FirePellet`, which finds the enemy nearest the crosshair within
  `EngageConeDegrees` (20) and in reach (`SeekerGuns.FindLocks`) and sends `TentaclesPerShot` (3)
  tentacles at it, each from its own free spawn spot and landing on its own small random offset on
  the body. With no enemy in reach, each instead reaches along its own random line within
  `GrabConeDegrees` (25) of the aim for the first surface it meets - a wall, the floor, a crate (and
  breaks a door lock it grabs) - out to the full reach, or a short wriggle out to `IdleReach` (4 m) if
  it meets nothing at all.
- **Spawn spots:** twelve fixed points in the camera's own right/up/forward frame - a half ring
  0.8-0.9 m out, swept from well below the left of the view round under the bottom to well below the
  right (-165 to -15 degrees) - so a tentacle's start is never seen. A spot isn't free again until the
  tentacle on it has fully retracted, so no more than twelve are ever out from one shooter at once.
- **Reach:** the strikers' own tentacle length,
  `EAB_MovingEnemeyTentacle.m_absoluteMaxDistanceToHitTarget`, read (longest found) from the level's
  strikers, retried every 5 s until one exists (`FallbackReach` 25 m until then) and logged.
- **The tentacle:** each lash rolls its type from the game's own GPU tentacle pool
  (`GPUCurvyManager.TryAllocateGPUCurvy`) - mostly strikers' (55%), some big strikers' (30%), the odd
  tank's (15%), falling back to a striker if that pool is empty. `TentacleLash`, an injected
  MonoBehaviour, lays the control points along a snake from its spot to the target every frame - a
  big side-to-side wave and a small vertical one, rippling and pulsing at random rates - and feeds them
  to the curvy (`FeedControlPositions`, `FeedSettings`, `CalculateEndPointsAndUpdateGPU`). It shoots out
  in 0.04 s, hangs 0.5 s thrashing and retracts over 0.2 s, then `DeallocateGPUCurvy`.
- **Damage:** the bite lands the moment it's out: `BulletDamage` (`TentacleDamage` 25, `StaggerMulti`
  x12) on the target's limb nearest the lash's end, on the shooter's game only. With no target, it
  bites any enemy within 0.6 m of the end, else melts a door lock (`LockDamage`).
- **Multiplayer:** teammates get `MindAttic.GTFO.EZ.AbsurdWeapons.TentacleLash` (start and end) per
  tentacle and draw visual-only lashes.
- **Pool size:** each tentacle type's GPU pool is a fixed size (`GPUC_Setup.m_globalLimit`). A prefix on
  `GPUC_Setup.Setup` multiplies every type's original size by `[Tentacles] PoolMultiplier` (default 4,
  range 1-16, never compounding across levels), so the cannon's volleys and enemy tentacles stop
  competing for slots. The log lists each type's before and after.

### Laser guns

`LaserGuns`, `LaserDriver` and `LaserBeam`.

- **No shots at all:** prefixes skip the laser guns' archetype trigger logic
  (`BulletWeaponArchetype.Update`, `OnStartFiring`, `OnFireShot`, `BWA_Auto.OnFireShot`) and
  `BulletWeapon.Fire` (and `BulletWeaponSynced.Fire`), so they never fire - no bullet, muzzle flash,
  brass or kick. Their steady hold never relaxes and has no `RecoilAnimation`, so they sit raised.
- **Trigger-driven:** `LaserDriver` reads `InputAction.Fire` while a laser gun is wielded and calls
  `LaserGuns.Tick` every frame to redraw the beam and drain ammo; every archetype `ShotDelay` (0.05 s)
  the tick also deals damage: a raycast from the eye (`MASK_BULLETWEAPON_RAY`, `BeamRange` 70 m) and
  `BulletDamage` on any enemy limb there, or lock damage on a door lock.
- **One beam:** each player has one `LaserBeam` (a `LineRenderer`, `BeamWidth` 0.025, using the trip
  mine's laser material) from the gun model's barrel. It goes off 0.25 s after the ticks stop.
- **Burn:** where the beam lands, a flickering white-hot spot, an orange `EffectLight`, and every 0.06 s
  `Impact_Metal` sparks (a decal every 6th) and, on enemies, every third, `Impact_EnemyBody`.
- **Multiplayer:** each tick is sent as `MindAttic.GTFO.EZ.AbsurdWeapons.LaserBeam` (start, end, hit
  kind, surface normal); teammates draw the same beam and burn.

### Railgun

The `CastWeaponRay` prefix swallows the Railgun's ray. For the local shooter `Railgun.Fire` runs
`RaycastAll` on `MASK_ENEMY_DAMAGABLE` along the aim out to `PenetrationRange` (1000 m) - walls don't
stop it - sorts the hits by distance and calls `BulletDamage` once per enemy on the first limb met (so
a head is a head). Door locks on the line are damaged up to the first wall. `RailBeam` draws two helix
strands around the first 80 m, faded out over 0.8 s, with an `EffectLight`; `Impact_Metal` sparks play
at every wall the line crosses. Teammates get `MindAttic.GTFO.EZ.AbsurdWeapons.RailShot`.

### Chain Lightning Gun

`ChainLightning`, `ChainLightningDriver` and `LightningBolt`. It never fires: the same
`BulletWeapon.Fire` prefix that stops the lasers skips it. `ChainLightningDriver` reads
`InputAction.Fire` while it's wielded and calls `ChainLightning.Fire` every 0.08 s, each strike's damage
the archetype's scaled by `0.08 / ShotDelay` (the same damage per second as one shot per `ShotDelay`).
Each tick takes the enemy a ray from the eye hits (`Range` 40 m) or, failing that, `SeekerGuns.FindLocks`
within `AssistConeDegrees` (6); a first ray that hits a door lock damages it and stops there. Each
strike is `BulletDamage` (x2 stagger); the next target is the nearest unstruck living enemy within
`JumpRange` (8 m) of the last strike with a clear `MASK_WORLD` line, up to `Jumps` (5) jumps at
`JumpFalloff` (75%) of the previous damage. `LightningBolt` re-jags a `LineRenderer` from the gun's
barrel through the strike points for 0.16 s, with a `BurstFlash` and `Impact_Electronics` sparks at
each strike, so successive ticks overlap into one continuous arc. The path goes to teammates as
`MindAttic.GTFO.EZ.AbsurdWeapons.ChainLightning`. `ChainLightning.Pulse` is shared with the thrown
Mjolnir. `ChainLightningGlow` hangs a soft violet `EffectLight` (0.6, 0.3, 1; range 1.2 m, intensity
about 0.2 with a flicker) on the gun while it's out in any player's hand - the Shadow Dagger's purple,
which used to carry over onto it by accident, now the gun's own fixed color, separate from the dagger's
light.

### Grenade Pistol and Rifle

`GrenadeLauncher`, `LauncherGrenade` and `Explosion`.

- **Firing:** plain semi-auto guns (Bataldo 3RB, main / TechMan Veruta XII, special) sharing the Arid
  5's stats, each with its own model's recoil. The `CastWeaponRay` prefix swallows their ray and, for
  the local shooter, calls `GrenadeLauncher.Launch`: a grenade from the barrel as drawn on screen,
  aimed at the crosshair point, at 30 m/s plus `LaunchLift` (5 m/s up). No brass.
- **Flight:** `LauncherGrenade` flies by hand - `Gravity` (18 m/s2), `SphereCast` against enemies and
  any player or bot but the shooter (`ProjectileHits`; on the shooter's game: explode) and `MASK_WORLD`
  (reflect, x0.5, settle on floors). The look is the lock melter's meshes copied from its loaded prefab
  (no scripts), else a capsule. A red `EffectLight` and dot blink from about 2 to 16 a second, beeping
  `DEPLOYER_BEEP_TARGET_ACQUIRED` while the blink is still slow; bounces play `FRAGGRENADEBOUNCE`.
- **Blast:** at 2.5 s (or on an enemy, teammate or bot) `AreaDamage.Apply` (5.2 m, 75 falling to 33%,
  x5 stagger) on the shooter's game, the trip mine's explosion effect (`FX_Tripmine` pool), a
  `BurstFlash` and `FRAGGRENADEEXPLODE`. Teammates get launch (`...GrenadeLaunch.v2`, carrying the
  shooter's `Gravity`, `GrenadeBounciness` and `FuseSeconds`) and detonate messages; their copies fly
  with the shooter's numbers, only bounce, and explode when told (or 0.75 s after the fuse).

### Rocket Launcher

`RocketLauncher`, `RocketProjectile` and `FallingSpark`, after Quake 1's own rocket code
(`W_FireRocket`, `T_MissileTouch`, `T_RadiusDamage`) with Doom's gravity-free flight.

- **Firing:** the gun fires normally (sound, recoil, ammo). The `CastWeaponRay` prefix swallows its
  ray and calls `RocketLauncher.Launch`: a rocket from the muzzle, dead straight along the aim at
  `LaunchSpeed` 32.7 m/s, no drop, no bounce, gone after `Lifetime` 10 s if it never hits anything.
- **Flight:** `RocketProjectile` flies it by hand as an orange fireball (an unlit see-through flame
  sphere around a white-hot core, flickering, with an orange `EffectLight`; no missile model), dropping a
  `FallingSpark` every 0.05 s and a trail of the game's concrete-impact dust that starts at the muzzle:
  a puff every 0.75 m for the first 6 m so it reads as one continuous trail, thinning out to the
  sparse far look (a puff every 0.4 s of flight, 1.5 m behind it) by 20 m. On the shooter's game it sphere-casts
  for the first enemy, wall, teammate or bot in its way (never the shooter; `ProjectileHits`).
- **Blast:** the Grenade Launcher's blast code (`AreaDamage`, `Explosion`) - `BlastDamage` 110 at the
  center, falling to `BlastEdgeFraction` (a third) at `BlastRadius` 5.2 m, `BlastStagger` x5 - plus the
  game's own `DamageUtil.DoExplosionDamage`, so the blast also catches nearby players, the shooter
  included. It runs on the shooter's game only, and that reaches every player: a player's
  `ExplosionDamage` is `Dam_SyncedDamageBase`'s (the player damage classes don't override it), which
  sends the explosion-damage packet the player's own game applies (`ReceiveExplosionDamage`), like
  bullet friendly fire. Running it on every game would hurt each player once per game.
- **Remote detonation:** it can't aim down sights. A `FirstPersonItemHolder.UpdateAimInput` prefix
  skips the aim input while it's held, and right-click (`InputAction.Aim`) instead detonates the
  shooter's oldest rocket in flight where it is, one per click.
- **Multiplayer:** `MindAttic.GTFO.EZ.AbsurdWeapons.RocketLaunch.v2` (with the shooter's `LaunchSpeed`
  and `Lifetime`) and `...RocketDetonate`. A teammate's copy is visual only and flies at the shooter's
  speed until the detonate message snaps it to the real impact point.

### Shrink Beam

`ShrinkBeam`, `ShrinkBeamDriver`, `ShrinkStream` and `ShrinkRegrowth`.

- **Trigger-driven:** like the lasers it never fires. `ShrinkBeamDriver` ticks every `GunShotDelay`
  (0.08 s) while Fire is held, draining ammo by time held, along a ray out to `Range` 40 m.
- **Shrink:** whatever the beam holds on loses `ShrinkPerSecond` (25%) of its full size, and the same
  share of its run speed, per second. At `MinScale` (45%) it gibs: `PopDamage` 99999 through the
  shooter's bullet path, so the kill syncs. At `StepOnScale` (70%) or smaller, getting within
  `StepOnRadius` (2 m) and looking down at it (within `StepOnLookConeDegrees` 35) steps on it - it gibs
  too. Door padlocks shrink the same way and break at `LockBreakScale` (50%).
- **Regrowth:** `ShrinkRegrowth` grows an enemy back at `RegrowPerSecond` (15%) once it's been off the
  beam for `RegrowDelaySeconds` (1 s), so a shrink has to be finished in one sustained hold.
- **Look:** `ShrinkStream` draws three thin green strands corkscrewing around the beam's line.
- **Multiplayer:** scale and speed aren't networked, so every client that gets the beam
  (`MindAttic.GTFO.EZ.AbsurdWeapons.ShrinkBeam.v2`, naming the shooter and carrying the shooter's
  shrink for that tick and the scale it stops at) runs the same shrink on its own copy of the enemy
  under it. The slowdown that matters is the host's copy; a client warns once per level when the host
  doesn't run the mod (`NetPresence`). Regrowth runs on each game's own settings.

### Katana dash

A postfix on `MeleeWeaponFirstPerson.ChangeState` times the local player's Katana charge. If it was
held at least 95% of the animation set's `MaxDamageChargeTime`, a `ChangeState` prefix holds the
release back (the swing waits) and `DashRunner` (an injected MonoBehaviour) moves the player up to
`DashDistance` (10 m) along the aim, flattened, over `DashSeconds` (0.6 s) through
`PlayerCharacterController.Move`, so collision stops it and position syncs like any movement. Each
frame a `SphereCast` (`DashSlashRadius` 1.2 m) ahead finds the first living enemy in the way and cuts the
step short to reach it. Contact - that enemy, any enemy within reach of the swept `OverlapCapsule`, or a
wall that stops most of a step - or the end of the distance releases the held swing, so the charged cut
lands right there. If the release won't take, a `BulletDamage` cut (`DashSlashStagger` x3) stands in.

### Attack tempo

The `MeleeSpeedModifier` getter is never called natively (IL2CPP inlined it), so patching it does
nothing. Instead a melee weapon whose light or charged attack speed isn't 1 (built in: Shadow Dagger
light x2.5, Katana charged x2) gets its own copy of its `MeleeAnimationSetDataBlock` (id = the weapon's
id) from `MeleeInjector.WithTempo`. Its light-attack `MeleeAttackData` and/or its charged release and
hit - copied, never edited in place - have every `*Time` divided by the speed, and `SwordTempo` (a
`ChangeState` postfix) sets `WeaponAnimator.speed` to match during those states. Charge-ups keep their
timing, which the full-charge checks use.

### Hammer impact bursts

`ImpactBursts` is a postfix on `MeleeWeaponFirstPerson.DoAttackDamage` for the local player's hits, not
pushes. Each `ImpactBurst` record says which weapon it belongs to and what it does:

| Burst | Weapon | Fires on | Radius | Damage | Stagger multi | Struck enemy |
|---|---|---|---|---|---|---|
| Thunderclap | Mjolnir | any hit that lands | 5 m | 8, falling to 40% at the edge | x3 | spared (it took the hammer) |
| Shockwave | Warhammer | any hit that lands | 6 m | 1 | x100 | included |

It finds every living enemy in the radius around the hit (`OverlapSphere` on `MASK_ENEMY_DAMAGABLE`,
one nearest limb per enemy, line of sight against `MASK_WORLD`). Each one gets `BulletDamage`, so it
syncs. Stagger is damage x stagger multiplier, so the shockwave's 1 x 100 knocks back anything that
isn't stagger-immune. The flash is a fading `EffectLight` (`BurstFlash`), sent to teammates over
`MindAttic.GTFO.EZ.AbsurdWeapons.ImpactBurst`. Mjolnir's thunderclap also plays `ELEVATOR_THUNDER`.

### Knockback

`Knockback` and `KnockbackSlides`. The Warhammer's shockwave also shoves enemies: the struck one
`ShockwaveKnockStruckLight` 4 m along the swing (`ShockwaveKnockStruckCharged` 12 m charged), the rest
`ShockwaveKnockArea` 6 m out from the impact. The slide eases out (`Sharpness` 14, cut at `MaxSeconds`
0.6 s) and is clipped by `NavMesh.Raycast`, so it stops at walls and ledges.

- **Wall slam:** `ShockwaveSlamDamagePerMeter` (5) turns the distance a slide didn't get to travel
  when it hit the edge (minus `SlamFreeMeters`, 1 m) into damage and a `SlamStagger` x3 stagger on the
  limb nearest the wall, dealt on the host but credited to the player who knocked it back, with an
  `Impact_Concrete` puff.
- **Host-owned:** enemy positions belong to the host. A client sends
  `MindAttic.GTFO.EZ.AbsurdWeapons.Knockback.v2` to the host (the attacking player, the enemy's network
  id, direction, distance, slam damage; its position only as a fallback) and the host slides it; the
  game's own position sync shows it to everyone. Without the mod on the host nothing moves, and the
  client says so once per level (`NetPresence`).

### Warhammer slam

`WarhammerSlam` times the local player's Warhammer charge; a release held at least 95% of
`MaxDamageChargeTime` schedules a slam `SlamDelaySeconds` (0.25 s) later - the swing's hit moment.
Unless the swing's own hit already burst since the release, it finds the floor `SlamReach` (1.8 m)
ahead (short of a wall) and releases the shockwave there - stagger and knockback all around, the flash
sent to teammates - plus `SpawnDebris`: the game's own concrete-impact effect (dust and a scorch decal)
at the slam point and `SlamDebrisCount` (8) more scattered across `SlamDebrisRadius` (1.5 m), each
dropped to the floor under it.

### Mjolnir throw

`MjolnirThrow` and `ThrownHammer`. A release held at least 95% of `MaxDamageChargeTime` spawns a
`ThrownHammer` - the held weapon's meshes copied onto the default layer (a primitive stand-in on
teammates' games) - flown by hand at `ThrowSpeed` 26 m/s along the camera, tumbling end over end
(`ThrowSpin` 1080 degrees/s), out to `ThrowReach` 25 m or the first `MASK_WORLD` hit, then home to the
thrower at `ThrowReturnSpeed` 32 m/s. On the thrower's game it `SphereCastAll`s (`ThrowHitRadius`
0.6 m) each step for enemies (`ThrowHitDamage` 20, x4 stagger, once each way) and every
`ThrowPulseSeconds` (0.12 s) calls `ChainLightning.Pulse`: the nearest visible enemy within
`ThrowPulseRange` (3 m) takes `ThrowPulseDamage` (6), then `ThrowPulseJumps` (2) jumps. Teammates get
`MindAttic.GTFO.EZ.AbsurdWeapons.MjolnirThrow.v2` (naming the thrower, with their speeds, reach and
spin) and fly a visual copy back to the thrower.

### Shadow Dagger stealth

`Stealth` and `StealthVisuals` (in `ShadowDagger.cs`).

- **Trigger:** a postfix on `MeleeWeaponFirstPerson.DoAttackDamage` fires when the local player's
  Shadow Dagger lands a fully charged hit (not a push) on an enemy. Fully charged means the state is a
  charge release or hit and the damage about to be dealt is at least 95% of the archetype's
  `ChargedAttackDamage`; a partial charge logs its numbers and is ignored. It hides that player for
  `StealthSeconds` (3 s), keyed by their `SNet_Player.Lookup`, up to the current cap.
- **The cap:** a stealth starts with a cap of `StealthMaxSeconds` (3 s). Every enemy hit while hidden,
  kill or not, drains `StealthCapDrainPerHit` (5%) of the cap, so a stealth always ends; the cap resets
  when it does.
- **Kills while hidden:** each confirmed kill (checked 0.4 s after the hit, since the host confirms
  them) adds `StealthBonusPerKill` (+25%) to a damage bonus on every Shadow Dagger hit for the rest of
  that stealth, up to `StealthMaxBonus` (+100%), and stretches the stealth by `StealthKillTimeBonus`
  (0.33 s), up to the cap.
- **Ends early:** wielding anything but the dagger ends the stealth at once, here and on every game.
- **Sync:** the hit is sent over GTFO-API (`MindAttic.GTFO.EZ.AbsurdWeapons.Stealth.v2`, naming the
  hidden player); every game keeps the same `hiddenUntil` table. That matters most on the host, where
  enemy AI runs: a client warns once per level when the host doesn't run the mod (`NetPresence`).
- **Enemies ignore hidden players:** `EnemyDetection.IsTargetValid` returns false for them, and
  prefixes on `EnemyAI.SetDetectedAgent` (sight and noise), `EnemyAI.SetTarget` and
  `EnemyAI.InjectPropagatedTarget` (alerts from other enemies) skip them.
- **Looks:** `StealthVisuals` runs on every game from the shared hidden table: every shown renderer of
  a hidden player switches to a see-through copy of its materials, tinted the dagger's purple glow,
  fading toward `StealthTransparency` (95%) and filling back in as the time runs down. Under
  `StealthWarningSeconds` (1 s) left it flickers, from 2 Hz up to 20 Hz, until it reverts. Even out of
  stealth, the dagger gives off a faint pulsing violet glow in anyone's hand.

### Victory Banner

`VictoryBanner` and `BannerPylon`. The C-Foam launcher's `WantToFireGlue` hook plants instead of
firing a projectile: a `BannerPylon` (a flag built from primitives) right in front of the owner - the
drop point is taken level from chest height, kept short of a wall, then raycast down to the floor - and
the owner switches to their main weapon. Firing again while it's up does nothing. It is sent to
teammates, and the owner picks it up like a sentry: look at it within `PickupRange` (2.5 m) and press
Use. It retires when the owner's tool slot no longer holds it or the owner leaves the level (checked
every half second on each client), and after `SafetyLifetimeSeconds` (600 s) as a safety net. While it's
down, every client heals its own local player `HealPercent` (1%) of max health every `HealSeconds`
(0.25 s) within `Radius` (8 m) - the planter's numbers, sent with `...BannerPlant.v2` (which names the
planter, as `...BannerPickUp.v2` does), not each client's own.

### In-level weapon swapping

`GearCycler`, an injected MonoBehaviour, reads the game's own `SelectMelee`, `SelectStandard`,
`SelectSpecial` and `SelectTool` actions while `FocusStateManager.CurrentState` is `FPS` and that slot
is already the wielded one (the game ignores that press itself), but only once Absurd Weapons is
switched on (below). The cycle is the slot's starting gear (recorded on the slot's first swap of the
level) followed by the `GearManager.GetAllGearForSlot` entries whose category is in
`DataBlocks.AddedCategories`. It equips the next with `PlayerBackpackManager.EquipLocalGear` - the
lobby's own path, which syncs - and shows a three-second line under the crosshair (`MessageHelper`,
`MessageType.Center`).

**Per-weapon ammo:** before swapping a slot away, `SaveAmmo` stores the gun's clip and the slot's
reserve under the outgoing gear's category. `FinishPending` waits until the new item `IsLoaded` (5 s at
most), then puts its saved ammo back - or, for one of ours not yet seen this level, a full magazine and
reserve - and re-wields the slot. Everything clears on leaving `InLevel`.

**The Absurd Weapons toggle:** `[Gear Cycling] ToggleAbsurdWeaponsKey` (`End` by default) is read
straight off `Input.GetKeyDown`, not a game `InputAction`. Off at the start of every level. The first
press turns it on, records every one of the four slots' current `GearIDRange` and the local player's
`Damage.Health`, and lets swapping (above) run. The second press turns it off: every recorded slot
still holding something else is swapped back the same way a cycle swap is (saving and restoring its
own ammo), the slot that was in hand is re-wielded once the swaps finish loading, and
`Damage.SendSetHealth` puts health back to its recorded value - so a run can go back to serious with
none of the absurdity left behind. The on/off state (`Announce`) goes in the event log and the
console, not just the short line under the crosshair - same as the Insert/Delete infinite health and
ammo toggles - so it's never just text that scrolled past.

### Sounds

`Sounds` and `LoopSounds`. Every weapon's sounds are the game's own Wwise events, each a `[Sounds]`
config entry holding the event name (empty = silent, or the gun's own sound for a shot). Guns with a
shot override have their vanilla fire audio suppressed (prefixes on `BulletWeapon`'s
`Trigger*FireAudio` and `TriggerSynced*PerShotAudio`) and play the override once per shot from the ray
hook, on every client.

| Entry | Default event |
|---|---|
| `PlasmaShot` | `GAUSSPISTOLBODYA3D` |
| `RailgunShot` | `GAUSSPISTOLFIRE2D_RANDOM` |
| `GrenadeShot` | `FLAREGUNFIRE` |
| `TentacleShot` / `TentacleBite` | `SNATCHERTENTACLESHOOT` / `SNATCHERTENTACLEHIT` |
| `LaserLoopStart` / `LaserLoopStop` | `FREEZERLOOP` / `FREEZERLOOPEND` |
| `LightningLoopStart` / `LightningLoopStop` | `TERMINAL_CREEPY_STATIC_LOOP` / `TERMINAL_CREEPY_STATIC_STOP` |
| `LightningCrackle` | `FLUORESCENT_LIGHT_CRACKLE` |
| `ShrinkLoopStart` / `ShrinkLoopStop` | `BIOSCAN_TUBE_EMITTER` / `BIOSCAN_TUBE_EMITTER_STOP` |
| `ShrinkPop` | `ENEMYWEAKSPOTEXPLODE` |
| `Thunderclap` | `ELEVATOR_THUNDER` |
| `BannerPlant` | `SENTRYGUN_DEPLOY` |

The trigger-driven guns never fire, so they have no gunshot: their loops run while the stream is fed,
and `LoopSounds` stops a loop 0.25 s after its last tick. The Rocket Launcher keeps the XDist2's own
shot.

### Moved out

The Hand Grenade (item 1203), the Stim Pack (item 1204) and the loot mix live in
[GTFO.EZ.AbsurdItems](https://github.com/mindattic/GTFO.EZ.AbsurdItems) now, with the same config keys.
This mod no longer touches consumables or loot.

## Configuration

`BepInEx/config/MindAttic.GTFO.EZ.AbsurdWeapons.cfg`, written on first launch: first `[0. Mod]` (the
master switch), then one section per weapon (a pistol/rifle pair shares one, since they share stats),
plus `[Ammo]`, `[Knockback]`, `[Tentacles]`, `[Sounds]` and `[Internal]` (the migration version - don't
edit). **The full reference - every setting, its default and what it does - is in
[package/README.md](package/README.md#config).**

| Setting | Default | What it does |
|---|---|---|
| `[0. Mod] Enabled` | `true` | Turn the whole mod on or off. When false, the mod loads nothing: no patches, no weapons, no hotkeys. |

Conventions:

- **`[0. Mod] Enabled`** is bound first in `Plugin.Load` (BepInEx writes sections in ordinal order, so
  `0. Mod` is the file's first entry). Off, `Load` logs one line and returns before any other `Bind`,
  `ConfigMigrations.Apply`, `ClassInjector` registration, `Init`, `PatchAll` or `AddComponent`. The
  unbound settings stay in the file, and migrations still due run the next time the mod is on.
- **`<Weapon> Enabled`** (default `true`), in each weapon's own section, no longer decides whether the
  weapon's datablocks are registered (G.AWPN-T236, for GTFO.EZ-T125's host/client config sync): every
  weapon is registered on every machine, so a lobby always agrees on which category ids exist.
  `MeleeInjector.TryAdd`/`GunInjector.TryAdd` record the switch in `DataBlocks.InLoadout`, and
  `LoadoutFilter` (a `GearManager.GetAllGearForSlot` postfix) leaves a disabled weapon out of the
  loadout lists and the weapon cycle. It's read live, so it no longer needs a restart.
- **Restart vs live:** anything that goes into a datablock - every `Enabled`, gun damage, shot delay
  and clip size, the melee archetype overrides, the seeker and impact-burst kinds built in their
  `Bind` - is read once at `GameDataInit.Initialize`. Everything else reads `.Value` at use, and
  `BladeStretcher` calls `ConfigFile.Reload()` whenever it builds a melee model, so those pick up edits
  without a restart.
- **Melee overrides:** the `Add*` methods set each weapon's built-in values;
  `MeleeInjector.ApplyOverrides` then replaces any whose config entry is non-zero.
- **Changed defaults** get a `ConfigMigrations` entry, so an existing install that still holds the old
  default moves to the new one once.

## Project layout

```text
src/GTFO.EZ.AbsurdWeapons/       the plugin project (BepInEx/Il2Cpp - needs the game installed to build)
  Pure/GearJson.cs      gear-JSON reading/rewriting (no game types; unit tested)
  Pure/Formulas.cs      the weapons' numbers (no game types; unit tested)
  Plugin.cs             entry point: config, Il2Cpp type registration, network events, Harmony
  DataBlocks.cs         shared clone / lookup / gear-JSON rewrite helpers
  MeleeInjector.cs      melee datablocks (Katana, Mjolnir, Warhammer, Shadow Dagger)
  GunInjector.cs        gun and tool datablocks, holds, kickless recoil
  BladeStretcher.cs     blade / head scaling on model spawn
  WeaponSettings.cs     per-weapon melee config (BladeLength, BladeLengthAxis, Reach, overrides)
  SeekerGuns.cs         fire hooks, seeker kinds, shot refunds, network message
  SeekerProjectile.cs   the player-fired homing projectile
  HiveLocks.cs          Hive guns' Titanfall-style lock-on markers
  ShadowDagger.cs       Shadow Dagger stealth (enemy AI patches, sync) and its shimmer
  SwordTempo.cs         the melee weapons' light- and charged-attack speeds
  TentacleCannon.cs     Tentacle Cannon targeting, surface grab and the GPU tentacle lash
  TentaclePool.cs       raises every tentacle type's GPU pool size (PoolMultiplier)
  LaserGuns.cs          trigger-driven laser beam, its ticks and burn, stream readiness
  Railgun.cs            Railgun slug and its double-helix beam
  ChainLightning.cs     Chain Lightning Gun's stream, strikes, jumps and bolt
  GrenadeLauncher.cs    Grenade Pistol/Rifle launch, grenade flight, fuse, blast
  RocketLauncher.cs     Rocket Launcher flight, sparks, dust, glow, right-click detonation, blast
  ShrinkBeam.cs         Shrink Beam ray, shrink, regrowth, step-on pop, its beam visual
  VictoryBanner.cs      Victory Banner placement, pickup and healing
  KatanaDash.cs         the Katana's full-charge dash-slash
  ImpactBursts.cs       hammer impact bursts (Mjolnir thunderclap, Warhammer shockwave) and their flash
  Knockback.cs          host-side physical knockback slides (Warhammer)
  WarhammerSlam.cs      the Warhammer's full-charge ground slam and its scattered debris
  MjolnirThrow.cs       Mjolnir's full-charge throw, flight and return
  GearCycler.cs         in-level weapon swapping on a second press of the slot keys
  InfiniteAmmo.cs       ammo settings, stream-gun drain (infinite ammo is the shared Delete toggle)
  LockDamage.cs         lets this mod's weapons (which only look for enemy limbs) also melt door locks
  ProjectileHits.cs     what a hand-flown rocket or grenade can hit besides enemies (other players and bots)
  NetSync.cs            versioned network events (Wire: .v2 first, then the old event for older games)
                        and NetPresence (who runs the mod; the once-per-level host-lacks-mod warning)
  Sounds.cs             per-weapon sound choices, vanilla shot audio suppression, loop sounds
  ConfigMigrations.cs   one-time moves of a changed default onto configs still sitting on the old one
  Shared/               linked from external/GTFO.EZ.Shared (a git submodule; change it there): SharedOwner,
                        the Insert/Delete toggles, MessageHelper and RemoteEvents (throw-safe network events)
package/                exactly what ships to Thunderstore / gets zipped for sharing
  manifest.json         Thunderstore package name is "GtfoEZAbsurdWeapons" (no period allowed there)
  README.md             becomes the Thunderstore page
  CHANGELOG.md
  icon.png
tests/GTFO.EZ.AbsurdWeapons.Tests/  NUnit tests for Pure/ - plain dotnet test, no game needed
docs/                   user stories, weapon design references
build.ps1               builds the plugin and packages dist/GTFO.EZ.AbsurdWeapons-<version>.zip
logo.png                cover art
```

## Testing

```powershell
dotnet test tests/GTFO.EZ.AbsurdWeapons.Tests/GTFO.EZ.AbsurdWeapons.Tests.csproj
```

`tests/GTFO.EZ.AbsurdWeapons.Tests` (NUnit, .NET 6, no game needed) covers the game-free code in
`src/GTFO.EZ.AbsurdWeapons/Pure/`, which the test project compiles straight in (the mod still ships as
one DLL):

- `GearJson`: reading and rewriting gear JSON (category, fire mode, hold), the "(!)" names, names with
  special characters, and not matching component or category numbers that only start alike.
- `Formulas`: full-charge checks, area falloff, chain-lightning damage, wall-slam damage, the gear
  cycler's wrap-around, tentacle pool sizing (never compounding across levels), the grenade blink, the
  Shadow Dagger bonus cap and the light-attack timing scale.

Everything else runs against the game's loaded data, Unity objects or Il2Cpp types, so verify it by
launching the game after `build.ps1` (see Quick start for what the log should show).

## Versioning

v1, v2, v3... - major-only bumps, matching the convention across other MindAttic projects.
Thunderstore requires `manifest.json`'s `version_number` to be strict `x.y.z`, so "v1" is `1.0.0`, "v2"
is `2.0.0`, and so on (minor and patch stay 0). `package/CHANGELOG.md` headers use the short `vN` form.

The GTFO.EZ family - GTFO.EZ, GTFO.EZ.AbsurdWeapons, GTFO.EZ.AbsurdMoves, GTFO.EZ.AbsurdItems,
GTFO.CrashTestDummies and GTFO.SkipDrop - shares one version number, so a matching set is easy to
spot: the companions' first release was **v13**, in step with GTFO.EZ (not v1); GTFO.CrashTestDummies
and GTFO.SkipDrop joined the shared scheme at **v17**; every release since bumps all six together.
Current: v20 (`20.0.0` in `package/manifest.json` and `Plugin.VERSION`).

## Sharing with friends

- **Private:** import the zip from `dist/` as a local mod in your mod manager, then
  `Settings -> Profile -> Export profile as code` and send friends the code.
- **Public:** not yet on Thunderstore.
- Everyone in a lobby needs the identical mod version; weapons sync by id.

## Limitations

- Not yet released: no Thunderstore package, and balance is tuned for fun first.
- Only the code in `Pure/` has automated tests; everything else is verified by playing.
- Every weapon is confirmed in play: loadout slots, models in hand, blade reach and stretching,
  each weapon's effects and sync as host and client, slot-key swapping and performance.

## Documentation

- [package/README.md](package/README.md) - the Thunderstore page: every weapon for players, the full
  config reference, and multiplayer and compatibility notes.
- [package/CHANGELOG.md](package/CHANGELOG.md) - the version-by-version changelog.
- [docs/USER_STORIES.md](docs/USER_STORIES.md) - the living feature and ticket log, generated from the
  shared workflow database (change it with the workflow CLI, not by hand).
- [docs/design/weapon-references.md](docs/design/weapon-references.md) - the game each weapon is
  modelled on and the numbers taken from it.
- [AGENTS.md](AGENTS.md) - entrypoint for LLM agents working in this repo.

GTFO.EZ.AbsurdWeapons is a GTFO.EZ companion mod, built on
[GTFO.EZ](https://github.com/mindattic/GTFO.EZ)'s core: it carries GTFO.EZ's shared code (the
Insert/Delete toggles, MessageHelper and RemoteEvents) and works with or without GTFO.EZ installed. It
was called GTFO.AbsurdWeapons before its first release.

## License

No LICENSE file is included in this repository; all rights are reserved. The seeker-projectile approach
follows Dinorush's ExtraWeaponCustomization (EWC), and the weapons' designs borrow from Doom, Quake and
Titanfall (see [docs/design/weapon-references.md](docs/design/weapon-references.md)).

---

Part of [MindAttic](https://mindattic.com) - see more projects at
[github.com/mindattic](https://github.com/mindattic). Sibling GTFO mods:
[GTFO.EZ](https://github.com/mindattic/GTFO.EZ),
[GTFO.EZ.AbsurdMoves](https://github.com/mindattic/GTFO.EZ.AbsurdMoves),
[GTFO.EZ.AbsurdItems](https://github.com/mindattic/GTFO.EZ.AbsurdItems),
[GTFO.CrashTestDummies](https://github.com/mindattic/GTFO.CrashTestDummies) and
[GTFO.SkipDrop](https://github.com/mindattic/GTFO.SkipDrop).
