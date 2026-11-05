# GTFO.EZ.AbsurdWeapons

*(Listed on Thunderstore as `GtfoEZAbsurdWeapons` — package names there are restricted to letters, numbers,
and underscores, so they can't contain a period.)*

GTFO.EZ.AbsurdWeapons is a GTFO.EZ companion mod, built on [GTFO.EZ](https://github.com/mindattic/GTFO.EZ)'s core: it carries GTFO.EZ's shared code (the Insert/Delete infinite health and ammo toggles, and MessageHelper, which puts on-screen messages wherever they fit) and works with or without GTFO.EZ installed. It was called GTFO.AbsurdWeapons before its first release.

A BepInEx plugin for GTFO that adds **eighteen new weapons**: four melee weapons, thirteen guns
and a tool. No game files are shipped or edited. Every weapon is built at load time from the game's own
data, so they pick up vanilla balance changes, and nothing vanilla is modified.

They show up in the loadout screen next to the vanilla weapons, in their normal slots. Every one of
them has **(!)** after its name, so you can tell them from vanilla gear at a glance. In a level you
can swap between them without going back to the lobby (see [Swapping weapons in a level](#swapping-weapons-in-a-level)).

**Every weapon can be switched off, and nearly every number below can be retuned** (200+ settings),
in the config file BepInEx creates on first launch — see [Config](#config).

## The weapons

### Melee

| Weapon | Model | Handles like | In one line |
|---|---|---|---|
| **Katana** | Mastaba Fixed Blade, real katana size | Bat | Wide cuts through everything in the arc; a full charge dashes you up to 10 m with the swing held - it cuts the moment you reach an enemy or a wall |
| **Mjolnir** | Santonian Mallet | Bat | Every hit releases a thunderclap; a full charge throws it, crackling chain lightning |
| **Warhammer** | Kovac Sledgehammer, 3x head | Sledgehammer | 500-damage blow; every hit flings enemies, and walls hurt them |
| **Shadow Dagger** | Mastaba Fixed Blade | Knife, faster | A fully charged hit makes you unseen for 3 seconds; kills stretch it, every hit shortens how far |

### Guns

The guns use ammo like any vanilla gun - a magazine, a reserve in the slot's ammo pool, reloads,
and ammo packs top them up. Each one starts **completely full** the first time you equip it in a
level, and keeps its own ammo from then on (see [Swapping weapons in a level](#swapping-weapons-in-a-level)).
The held-trigger guns (lasers, Chain Lightning, Shrink Beam and the Tentacle Cannon) spend ammo by
**how long you hold the trigger**: a full magazine lasts **10 seconds** of sustained fire
(`[Ammo] MagazineSeconds`). The beam guns (all but the Tentacle Cannon) **never reload** - an empty
magazine refills straight from the reserve - and stop when the reserve is empty too. (Infinite ammo, on **Delete**, brings back bottomless guns.) They're tuned
to be fun first; balance comes later.

| Weapon | Slot | Model | In one line |
|---|---|---|---|
| **Plasma Pistol** | Main | Raptus Steigro | Doom's plasma gun: rapid blue plasma balls with a small shock on impact |
| **Plasma Rifle** | Special | Omneco LRG | The same plasma |
| **Hive Pistol** | Main | Shelling S49 | Titanfall's Smart Pistol: rounds curve onto locked enemies, 1 damage each |
| **Hive Rifle** | Special | Malatack HXC | The same curving smart rounds |
| **Laser Pistol** | Main | Raptus Treffen 2 | One continuous beam out to 70 m while the trigger is held; burns and sparks where it lands |
| **Laser Rifle** | Special | Accrat ND6 | The same beam |
| **Tentacle Cannon** | Special | Buckland XDist2 | A stream of striker tentacles that bite whatever they reach |
| **Grenade Pistol** | Main | Bataldo 3RB | Quake-style lobbed lock-melter grenades: instant on an enemy, else bounce and blow after 2.5 s |
| **Grenade Rifle** | Special | TechMan Veruta XII | The same grenades, from its own barrel with its own kick |
| **Railgun** | Special | Köning PR 11 | A hitscan slug with infinite penetration, wrapped in a double helix |
| **Chain Lightning Gun** | Special | Omneco exp1 | A continuous stream of lightning that leaps from enemy to enemy, up to five jumps |
| **Rocket Launcher** | Special | Buckland XDist2 | Quake 1's rocket: an orange fireball that flies dead straight with no drop, trailing falling sparks and a dust trail from the muzzle, glowing so you can track it, and blows up the first thing it touches - an enemy, a wall, a teammate or a bot, never you |
| **Shrink Beam** | Special | Omneco exp1 | A continuous ray that shrinks whatever it's held on until it gibs; look down at a small one up close and you'll step on it |

### Tool

| Weapon | Model | In one line |
|---|---|---|
| **Victory Banner** | Stalwart Flow G2 (C-Foam launcher) | Put a flag down in front of you, like a sentry, with a glowing healing aura that heals everyone inside; walk up and press Use to pick it up |

## Melee weapons

"Handles like" means everything except the model comes from that weapon: animations, sounds,
reach, stamina costs, and the swing teammates see, unless a stat below says otherwise.

### Katana

The Mastaba Fixed Blade with a ~1.85 m blade (a real katana's is ~70 cm; the grip stays knife-sized),
on the bat's moves: 4 / 14 damage, 3.5 m reach, cutting every enemy in the arc.

**Dash-slash:** release a fully charged swing and, instead of swinging, you dash up to
**10 m** forward along your aim (kept level) in 0.6 s **with the swing held**. The moment you make
contact - an enemy within reach, or a wall or prop that stops you - the charged cut is released
right there, twice as fast as a normal charged swing. If you reach nothing, it swings at the end of the dash.

### Mjolnir

A short-hafted war hammer with the bat's stats and moveset.

- **Thunderclap:** every hit that lands releases an electric shock with a blue flash, hitting every
  other enemy within 5 m for up to 8 damage (falling to 40% at the edge) with a x3 stagger. Walls
  block it.
- **Throw:** release a full charge and a Mjolnir flies from your hand, tumbling end over end, out
  to 25 m (or the first wall), then comes back to you (one throw at a time; the one in your hand
  stays put). It hits every enemy it passes through (20 damage, x4 stagger, once each way), and
  whenever an enemy is within 3 m of it, it casts chain lightning at the nearest one (every 0.12 s
  while one's that close), which jumps on to two more (6 damage, 75% per jump).

### Warhammer

A sledgehammer with a head the size of a crate. Charged 500 / light 6 damage, x55 / x11 stagger,
x10 to doors and locks on a charged hit, 2.4 m reach, hits several enemies.

- **Shockwave:** every hit that lands staggers everything within 6 m (1 damage, x100 stagger).
- **Slam:** a fully charged swing always comes down on the ground in front of you with the
  shockwave and a crater's worth of scattered debris and scorch marks - even if it hits nothing.
- **Knockback:** enemies are flung across the floor: the one you hit 4 m (**12 m** on a charged
  hit), the rest 6 m out from the impact. Walls and ledges stop the slide.
- **Wall slam:** an enemy that hits a wall with fling still to go takes "sideways fall damage": 5
  per metre it didn't get to travel (the first metre is free), with a stagger and a puff of dust.
  A charged hit into a nearby wall does about 50.

### Shadow Dagger

Quick stabs play **2.5x as fast** as the knife's, and **a fully charged hit on an enemy makes you
unseen for 3 seconds**: enemies can't see or hear you, won't lock on, and lose you if they were
chasing. You fade to a faint purple tint while hidden - the dagger's own glow - filling back in as
the time runs out, and flickering faster and faster over the last second as a warning before it
reverts. Another full-charge hit tops the stealth back up (never cut short).
**Put the dagger away and it ends at once:** wielding any other item makes you visible again.

- **Kills while hidden** sharpen the dagger: each adds **+25% damage**, up to **+100%**, for the
  rest of that stealth.
- **Every kill while hidden** adds **+0.33 seconds** to your stealth.
- **But it can't last forever:** your stealth can only grow up to a cap, which starts at **3 seconds**
  and **drains 5% with every enemy you hit** while hidden, kill or not. Draining the cap never takes
  away time you already have - you just can't stretch it back above the lowered cap. Once the
  stealth ends the cap resets to 3 seconds.

## Guns

### Plasma Pistol (main) and Plasma Rifle (special)

Doom's plasma gun: a rapid stream of blue plasma balls (real enemy projectiles, tinted) that fly
dead straight at twice Doom's speed, with no spread at all - every ball goes exactly where the
crosshair is - and stop at walls and doors. Each does **5 × 1d8 (5–40)** and a small 1.75 m electric
splash (6 damage, x3 stagger). One every 3 tics, ~11.7 a second, like Doom. No recoil, no brass.

### Hive Pistol (main) and Hive Rifle (special)

Titanfall's Smart Pistol: each locks onto up to 6 enemies within 35° of your crosshair (60 m) -
you see a lock marker build up on each - and every round bends out of the barrel onto one of them,
taking turns. Every round lands but does only **1 damage**. ~11 a second. No recoil.

### Laser Pistol (main) and Laser Rifle (special)

One continuous red beam from the barrel to whatever your crosshair is on, for as long as you hold
the trigger. Where it lands it **burns**: a white-hot spot, an orange glow, sparks and scorch marks.
It burns whatever enemy it's on for 40 damage a second, out to 70 m. No bullets, no muzzle flash,
no kickback.

### Tentacle Cannon (special)

Every round is a burst of three striker tentacles, mouths and all, spewing out from spots just below
your eyeline around the view and thrashing in from off-screen. **While an enemy is near your
crosshair and in reach**, every tentacle in the burst reaches for it, each landing its own bite;
with none in reach they reach out along their own random lines for the nearest wall, floor or crate
(and melt a lock they grab). It's carried very low at the waist, like a minigun. Each bite does 25
and staggers hard (x12); ~4.3 bursts a second. Ammo goes by **how long you hold the trigger** - a
full magazine is 10 seconds of fire, whatever the tentacles hit - and it reloads like any gun.

### Grenade Pistol (main) and Grenade Rifle (special)

Semi-auto launchers on heavy guns - the Bataldo 3RB revolver (main) and the TechMan Veruta XII
(special). A shot does exactly the same from either; each just looks like its own gun firing. Every
shot is a **lock melter rigged to blow**.

- **Hits an enemy, a teammate or a bot:** explodes instantly (never on you, the shooter).
- **Misses:** bounces off walls and floors, losing half its speed each time, and explodes **2.5 seconds** after firing.
- While it's out it **flashes red faster and faster** (with a beep each flash) as the fuse burns.
- It's lobbed: launched fast with a little upward lift, so it arcs out a long way.
- The blast does 75 damage at the center, falling to a third at 5.2 m, with a heavy stagger (x5).
  It never hurts players.

### Railgun (special)

The Köning PR 11, semi-auto, one shot every 1.5 seconds. Each shot is an instant slug with **infinite
penetration**: it goes through every enemy and every wall out to 1000 m, doing **9999 damage** to every
enemy on the line - it kills anything it touches. It's drawn as a teal (green-blue) double helix along the shot
that fades away to nothing over 0.8 s, with sparks wherever it punches through a wall. Keeps
the sniper's scope, with no kickback.

### Chain Lightning Gun (special)

The Omneco exp1, a continuous stream of lightning while you hold the trigger: 12.5 strikes a second.
Each strike hits the enemy under your crosshair (or the nearest within 6° if you just miss), then
**jumps** to the nearest enemy within 8 m it hasn't hit, up to 5 jumps. The first enemy takes about
2.7 a strike (33 a second: `Damage` 5 every `ShotDelay` 0.15 s), then 75% of the last on each jump. Jumps need
a clear line. It glows a soft violet while it's in your hand. No recoil, no brass.

### Rocket Launcher (special)

Quake 1's own rocket launcher. Every shot is a rocket that flies dead straight out of the barrel -
no drop, no bounce - to the first enemy, wall, teammate or bot in its way (never you), trailing falling
sparks and a light dust trail well behind it, and glowing orange so you can see where it is,
then explodes: **110 damage** at the center, falling to a third at **5.2 m**, with a heavy stagger
(x5). **The blast reaches nearby players too** (shooter, bots and teammates alike), Quake style.
Semi-auto, one shot every **0.8 seconds**.

It doesn't aim down sights: **right-click detonates your oldest rocket** still in flight - it stops
dead and blows up right where it is. One per click, so fire a volley and click through it to set off
a chain of mid-air explosions, or burst one over a crowd or around a corner.

### Shrink Beam (special)

A continuous green ray: three thin, jittering strands corkscrewing around the barrel and drawing
together on the target. Hold it on an enemy and it shrinks - smaller and slower by the same share -
the longer you keep the beam on it; let go and, after a second off the beam, it starts growing back,
so a shrink has to finish in one sustained hold. Shrink it to **45%** of its size and it gibs
outright. Once it's at **70%** size or smaller, get within **2 m** of it and look down at it and you
step on it - it gibs too. **Door padlocks shrink the same way** and break at 50%.

## Tool

### Victory Banner

You carry the flag itself. Press fire and it's **planted right in front of you** at once - a pole
with a green banner waving from it - no charging up - and you switch to your main weapon. Like a
sentry, you **pick it back up** by walking up to it, looking at it and pressing **Use** (a prompt
shows). Switching your tool slot away from it - cycling the slot or equipping something else -
**retires it on its own**, and it goes with you if you leave the game.

While it's up, a **glowing green aura** lies on the ground around it out to **8 m**. Every quarter
second it pulses, **healing** everyone standing inside it 1% of their max health - stay in it and
you're back to full well within a minute.

## Hand Grenade, Stim Pack and loot

The Hand Grenade, the Stim Pack and the loot mix (kits, grenades and stims as consumable loot) moved to [GTFO.EZ.AbsurdItems](https://github.com/mindattic/GTFO.EZ.AbsurdItems), with the same settings. This mod no longer changes consumables or loot.

## Swapping weapons in a level

Absurd Weapons is **off at the start of every level**. **Press End** to turn it on; press it again to
turn it off.

- **Turning it on** snapshots your current loadout (melee, main, special and tool) and your current
  health, then lets swapping (below) start working.
- **Turning it off** puts every slot you swapped back to that snapshot's gear and its own ammo -
  dropping you out of whatever Absurd weapon you're holding and back into what you dove in with - and
  sets your health back to the snapshotted value. A serious run can carry on as if you'd never touched
  the weapons.

Hold a main, special, tool or melee weapon and **press that slot's own key again** - 1 to 4 by
default (melee, main, special, tool), or whatever you've rebound them to: with the slot already in
hand, each press moves on to its next weapon. A short line under the crosshair names what you switched to.
Teammates get the swap too.

The first time you swap a slot, **the gear you dropped in with joins that slot's cycle**, ahead of
this mod's weapons - so cycling on brings your own main, special, tool and melee back round. (Weapons
you've switched off in the config aren't in it.)

**Every weapon keeps its own ammo** for the rest of the level: swap away and its magazine and
reserve are put aside; swap back and you get exactly that back. One of this mod's weapons starts
completely full the first time you equip it in a level.

## Config

`BepInEx/config/MindAttic.GTFO.EZ.AbsurdWeapons.cfg` is created on first launch. Every number above is
in it, grouped one section per weapon (pistol/rifle pairs share one section, since they share one
set of stats).

- Settings marked **†** are baked into the weapon when the game loads its data, so they **take effect
  on the next game restart**.
- Everything else is read while you play. The file is re-read whenever a melee weapon's model is
  built (the loadout screen, equipping it, or cycling to it), so edits to those land without
  restarting.
- In the melee sections, **0 means "keep the built-in value"** listed in the weapon's description
  above.

If you're upgrading from an older version: BepInEx keeps whatever value is already in your `.cfg`,
so when a *default* changes between versions (see `CHANGELOG.md`), the mod corrects that once per
affected setting the first time you launch the version that changes it.

### Turning the whole mod off

The file's first entry switches off everything at once, for a mod manager profile that doesn't list
the mod:

| Setting | Default | What it does |
|---|---|---|
| `[0. Mod] Enabled` † | true | Turn the whole mod on or off. When false, the mod loads nothing: no patches, no weapons, no hotkeys. Your other settings stay in the file as they are. |

### Turning weapons off

Every weapon has its own on/off switch, `<Weapon> Enabled` †, in its section. All are on by default.
A weapon that's off simply isn't added: it's not in the loadout screen or the weapon cycle,
and nothing else about the game changes. Pistol and rifle versions of a gun are switched separately.

| Section | Switches |
|---|---|
| `[Katana]` | `Katana Enabled` |
| `[Mjolnir]` | `Mjolnir Enabled` |
| `[Warhammer]` | `Warhammer Enabled` |
| `[Shadow Dagger]` | `Shadow Dagger Enabled` |
| `[Plasma Guns]` | `Plasma Pistol Enabled`, `Plasma Rifle Enabled` |
| `[Hive Guns]` | `Hive Pistol Enabled`, `Hive Rifle Enabled` |
| `[Laser Guns]` | `Laser Pistol Enabled`, `Laser Rifle Enabled` |
| `[Tentacle Guns]` | `Tentacle Cannon Enabled` |
| `[Grenade Launcher]` | `Grenade Pistol Enabled`, `Grenade Rifle Enabled` |
| `[Railgun]` | `Railgun Enabled` |
| `[Chain Lightning Gun]` | `Chain Lightning Gun Enabled` |
| `[Rocket Launcher]` | `Rocket Launcher Enabled` |
| `[Shrink Beam]` | `Shrink Beam Enabled` |
| `[Victory Banner]` | `Victory Banner Enabled` |

### Melee weapons (shared settings)

Each of `[Katana]`, `[Mjolnir]`, `[Warhammer]` and `[Shadow Dagger]` has these:

| Setting | Default | What it does |
|---|---|---|
| `BladeLength` | Katana 6.9, Warhammer 3, others 1 | How much the blade (or hammer head) is stretched; 1 is the model's own size. Visual only - raise `Reach` to match. |
| `BladeLengthAxis` | Y (Warhammer: All) | Which of the model's axes is its length. `Auto` picks the longest; `All` scales evenly, so a head gets bigger instead of longer. |
| `Reach` † | 0 (Katana 3.5, Mjolnir 1.75, Warhammer 2.4, Shadow Dagger 1.75) | How far hits land, in metres (0-10). |
| `LightAttackDamage` † | 0 (Katana 4, Mjolnir 3, Warhammer 6, Shadow Dagger: the knife's) | Light-attack damage. |
| `ChargedAttackDamage` † | 0 (Katana 14, Mjolnir 12, Warhammer 500, Shadow Dagger: the knife's) | Fully charged attack damage. |
| `LightStaggerMulti` † | 0 (Warhammer 11, others vanilla) | Light-attack stagger multiplier. |
| `ChargedStaggerMulti` † | 0 (Warhammer 55, others vanilla) | Charged-attack stagger multiplier. |
| `ChargedEnvironmentMulti` † | 0 (Warhammer 10, others vanilla) | Charged-attack damage multiplier against doors and locks. |
| `AttackSphereRadius` † | 0 (Warhammer 0.6, others vanilla) | How wide a swing's hit sphere is, in metres. |
| `ChargeSpeedMulti` † | 0 (Warhammer 0.5, others vanilla) | Your move speed while holding a charge (1 = normal). |
| `LightAttackSpeed` † | 0 (Shadow Dagger 2.5, others 1) | Light-attack pace: 2 swings twice as fast. |
| `ChargedAttackSpeed` † | 0 (Katana 2, others 1) | Pace of a charged swing's release and hit. The charge-up itself keeps its timing. |

### `[Katana]`

| Setting | Default | What it does |
|---|---|---|
| `DashDistance` | 10 | Metres the full-charge dash moves you (it stops sooner on contact with an enemy or a wall). |
| `DashSeconds` | 0.6 | Seconds the full dash takes. |
| `DashSlashRadius` | 1.2 | How close an enemy has to be to count as contact (the held swing releases). |
| `DashSlashStagger` | 3 | Stagger multiplier of the fallback cut, if the held swing can't be released. |

### `[Mjolnir]`

| Setting | Default | What it does |
|---|---|---|
| `ThunderclapRadius` † | 5 | Metres the thunderclap reaches. |
| `ThunderclapDamage` † | 8 | Damage at the thunderclap's center. |
| `ThunderclapEdgeFraction` † | 0.4 | Share of damage left at its edge. |
| `ThunderclapStagger` † | 3 | Thunderclap stagger multiplier. |
| `ThrowSpeed` | 26 | The thrown hammer's outbound speed, m/s. |
| `ThrowReturnSpeed` | 32 | Its speed flying back to you, m/s. |
| `ThrowReach` | 25 | Metres it flies before turning back (or the first wall). |
| `ThrowSpin` | 1080 | How fast it tumbles end over end, degrees/second. |
| `ThrowHitRadius` | 0.6 | How wide a swath the flying hammer hits. |
| `ThrowHitDamage` | 20 | Damage per enemy hit, each way. |
| `ThrowHitStagger` | 4 | Stagger multiplier per hit. |
| `ThrowPulseSeconds` | 0.12 | Seconds between the flying hammer's lightning pulses. |
| `ThrowPulseRange` | 3 | It casts chain lightning whenever an enemy is within this many metres. |
| `ThrowPulseDamage` | 6 | Damage of a pulse's first strike. |
| `ThrowPulseJumps` | 2 | Chain-lightning jumps per pulse, after the first strike (0-5). |

### `[Warhammer]`

| Setting | Default | What it does |
|---|---|---|
| `ShockwaveRadius` † | 6 | Metres the shockwave reaches. |
| `ShockwaveStagger` † | 100 | Shockwave stagger multiplier. |
| `ShockwaveKnockStruckLight` † | 4 | Metres the struck enemy slides on a light hit. |
| `ShockwaveKnockStruckCharged` † | 12 | Metres the struck enemy slides on a charged hit. |
| `ShockwaveKnockArea` † | 6 | Metres everyone else in the shockwave slides. |
| `ShockwaveSlamDamagePerMeter` † | 5 | Wall-slam damage per metre of unspent slide. |
| `SlamDelaySeconds` | 0.25 | Seconds after a full charge is released that the hammer hits the ground. |
| `SlamReach` | 1.8 | Metres in front of you the slam lands. |
| `SlamDebrisRadius` | 1.5 | Metres out from the slam point the debris and scorch marks scatter. |
| `SlamDebrisCount` | 8 | How many debris bursts scatter across that radius. |

### `[Shadow Dagger]`

| Setting | Default | What it does |
|---|---|---|
| `StealthSeconds` | 3 | Seconds a fully charged hit hides you for (up to the current cap while already hidden). |
| `StealthKillTimeBonus` | 0.33 | Seconds added for every kill made while hidden, up to the current cap. |
| `StealthMaxSeconds` | 3 | The cap on stealth time when a stealth starts; resets to this when it ends. |
| `StealthCapDrainPerHit` | 0.05 | Share of the current cap lost with every enemy hit while hidden, kill or not (0-1). Time already left is kept. |
| `StealthBonusPerKill` | 0.25 | Damage bonus added per kill while hidden (0.25 = +25%). |
| `StealthMaxBonus` | 1 | Cap on the stacked kill bonus (1 = +100%). |
| `StealthTransparency` | 0.95 | How see-through you are at full stealth (0-1). Visual only. |
| `StealthWarningSeconds` | 1 | With this much stealth left you start flickering back in as a warning. 0 turns it off. |

### `[Knockback]`

Shared by everything that flings enemies (the Warhammer).

| Setting | Default | What it does |
|---|---|---|
| `Sharpness` | 14 | How quickly a slide covers its distance (ease-out rate). |
| `MaxSeconds` | 0.6 | A slide is cut short after this many seconds regardless. |
| `SlamFreeMeters` | 1 | Slides ending within this of their full distance take no wall-slam damage. |
| `SlamStagger` | 3 | Stagger multiplier of a wall slam. |

### `[Plasma Guns]`

| Setting | Default | What it does |
|---|---|---|
| `Damage` † | 22.5 | Damage shown in the loadout screen (each ball actually rolls `DamageScale` x 1d`DamageDie`). |
| `ShotDelay` † | 0.0857 | Seconds between balls (~11.7 a second). |
| `ClipSize` † | 100 | Magazine size. |
| `BallSpeed` † | 57.2 | Ball speed, m/s. |
| `DamageDie` † | 8 | Each hit rolls `DamageScale` x 1d`DamageDie`. |
| `DamageScale` † | 5 | Each hit rolls `DamageScale` x 1d`DamageDie`. |
| `BlastRadius` † | 1.75 | Metres of the small shock on impact. |
| `BlastDamage` † | 6 | Damage at the shock's center. |
| `BlastStagger` † | 3 | Shock stagger multiplier. |

### `[Hive Guns]`

| Setting | Default | What it does |
|---|---|---|
| `Damage` † | 1 | Damage per round. |
| `ShotDelay` † | 0.09 | Seconds between rounds. |
| `ClipSize` † | 30 | Magazine size. |
| `RoundSpeed` † | 45 | Round speed, m/s. |
| `TurnRate` † | 1500 | How fast a homing round can turn, degrees/second. |
| `LockConeDegrees` † | 35 | Cone around the crosshair that locks count as nearby. |
| `LockRange` † | 60 | Metres a lock can be picked up from. |
| `MaxLocks` † | 6 | How many enemies it cycles locks between. |
| `LockPipSeconds` | 0.25 | Seconds each lock pip takes to build while the enemy stays near the crosshair. |
| `LockHealthPerPip` | 40 | An enemy needs one lock pip per this much health (at least one). |
| `LockMaxPips` | 3 | The most pips any enemy needs, however tough. |

### `[Laser Guns]`

| Setting | Default | What it does |
|---|---|---|
| `Damage` † | 2 | Damage per tick (20 ticks a second). |
| `ShotDelay` † | 0.05 | Seconds between ticks. |
| `ClipSize` † | 100 | Magazine size. |
| `BeamRange` † | 70 | Metres the beam reaches. |
| `BeamWidth` † | 0.025 | The beam's visual width. |

### `[Tentacle Guns]` (the Tentacle Cannon)

| Setting | Default | What it does |
|---|---|---|
| `Damage` † | 25 | Damage per bite (enemies' own tentacles are untouched). |
| `ShotDelay` † | 0.235 | Seconds between bursts. |
| `ClipSize` † | 60 | Magazine size. |
| `TentaclesPerShot` | 3 | Tentacles each burst sends out (each needs a free one of twelve spawn spots). |
| `EngageConeDegrees` | 20 | The enemy nearest the crosshair within this cone (and in reach) draws every tentacle. |
| `GrabConeDegrees` | 25 | With no enemy in reach, each tentacle reaches along its own random line within this cone. |
| `StaggerMulti` | 12 | Stagger multiplier per bite. |
| `IdleReach` | 4 | Metres a tentacle wriggles out when its line meets nothing in reach. |
| `FallbackReach` | 25 | Reach used until a real striker's is found in the level. |

### `[Tentacles]`

| Setting | Default | What it does |
|---|---|---|
| `PoolMultiplier` † | 4 | How many times more tentacles the game can draw at once (1-16; every tentacle type, enemies' included). |

### `[Grenade Launcher]` (the Grenade Pistol and Rifle)

| Setting | Default | What it does |
|---|---|---|
| `GunShotDelay` † | 0.6 | Seconds between shots. |
| `GunClipSize` † | 6 | Magazine size. |
| `LaunchSpeed` | 30 | Launch speed along the aim, m/s. |
| `LaunchLift` | 5 | Extra upward speed every grenade is thrown with, m/s - the lob. |
| `Gravity` | 18 | Gravity on a grenade in flight, m/s². |
| `FuseSeconds` | 2.5 | Seconds before a missed grenade explodes on its own. |
| `GrenadeBounciness` | 0.5 | Speed kept per bounce. |
| `BlastRadius` | 5.2 | Metres the blast reaches. |
| `BlastDamage` | 75 | Damage at the blast's center. |
| `BlastEdgeFraction` | 0.33 | Share of damage left at the blast's edge. |
| `BlastStagger` | 5 | Blast stagger multiplier. |

### `[Railgun]`

| Setting | Default | What it does |
|---|---|---|
| `Damage` † | 9999 | Damage to every enemy on the line. |
| `ShotDelay` † | 1.5 | Seconds between shots. |
| `ClipSize` † | 5 | Magazine size. |
| `PenetrationRange` | 1000 | Metres the piercing slug reaches. |

### `[Chain Lightning Gun]`

| Setting | Default | What it does |
|---|---|---|
| `Damage` † | 5 | Damage to the first enemy per `ShotDelay` of held trigger (each strike, every 0.08 s, is this scaled to the tick). |
| `ShotDelay` † | 0.15 | Seconds of held trigger that deal `Damage` once (it never fires shots). |
| `ClipSize` † | 60 | Magazine size. |
| `Range` | 40 | Metres the first strike reaches. |
| `AssistConeDegrees` | 6 | A near-miss within this cone still finds a first target. |
| `Jumps` | 5 | How many times a bolt leaps on to another enemy (0-5). |
| `JumpRange` | 8 | Metres each jump can reach. |
| `JumpFalloff` | 0.75 | Share of the previous strike's damage each jump does. |
| `StaggerMulti` | 2 | Stagger multiplier per strike. |

### `[Rocket Launcher]`

| Setting | Default | What it does |
|---|---|---|
| `GunShotDelay` † | 0.8 | Seconds between shots. |
| `GunClipSize` † | 5 | Magazine size. |
| `LaunchSpeed` | 32.7 | Rocket speed, m/s. |
| `Lifetime` | 10 | Seconds a rocket flies before it disappears if it never hits anything. |
| `BlastRadius` | 5.2 | Metres the blast reaches. |
| `BlastDamage` | 110 | Damage at the blast's center. |
| `BlastEdgeFraction` | 0.33 | Share of damage left at the blast's edge. |
| `BlastStagger` | 5 | Blast stagger multiplier. |

### `[Shrink Beam]`

| Setting | Default | What it does |
|---|---|---|
| `GunShotDelay` † | 0.08 | Seconds between beam ticks. |
| `GunClipSize` † | 100 | Magazine size. |
| `Range` | 40 | Metres the beam reaches. |
| `ShrinkPerSecond` | 0.25 | Share of full size (and speed) removed per second on the beam. |
| `RegrowDelaySeconds` | 1 | Seconds off the beam before a shrunk enemy starts growing back. |
| `RegrowPerSecond` | 0.15 | Share of full size regained per second while growing back. |
| `MinScale` | 0.45 | Share of full size at which it gibs. |
| `LockBreakScale` | 0.5 | Share of full size at which a shrunk door padlock breaks. |
| `PopDamage` | 99999 | Damage dealt when it gibs. |
| `StepOnScale` | 0.7 | At or below this share of full size you can step on it. |
| `StepOnRadius` | 2 | Metres within which looking down at a small-enough enemy steps on it. |
| `StepOnLookConeDegrees` | 35 | How close to your crosshair it has to be to count as looking down at it. |

### `[Victory Banner]`

| Setting | Default | What it does |
|---|---|---|
| `Radius` | 8 | Metres out to which it heals. |
| `HealSeconds` | 0.25 | Seconds between heals. |
| `HealPercent` | 1 | Percent of max health restored per heal. |
| `PickupRange` | 2.5 | Metres from the banner within which Use picks it back up. |
| `SafetyLifetimeSeconds` | 600 | A safety-net despawn in case it's never picked back up. |

### `[Ammo]`

| Setting | Default | What it does |
|---|---|---|
| `MagazineSeconds` | 10 | Seconds of held trigger a full magazine lasts, for the lasers, Chain Lightning, Shrink Beam and Tentacle Cannon (their ammo goes by time held, not shots). The beam guns refill an empty magazine from the reserve instead of reloading. |

**Insert** toggles infinite health and **Delete** infinite ammo (every gun, this mod's and vanilla,
never runs out or reloads), each announced in the event log and always in the console too, off at
the start. They're the same two keys GTFO.EZ, its Absurd companions and GTFO.CrashTestDummies all
have, from one shared copy of the code; with several installed only one answers, so a press never
toggles twice.

### `[Gear Cycling]`

| Setting | Default | What it does |
|---|---|---|
| `ToggleAbsurdWeaponsKey` | End | Switches Absurd Weapons on (snapshotting your current gear and health) or, pressed again, off (putting every swapped slot and your health back to that snapshot). Off at the start of every level. |

Swapping itself, once Absurd Weapons is on, uses the game's own slot keys and has no setting of its
own (see [Swapping weapons in a level](#swapping-weapons-in-a-level)).

### `[Sounds]`

Every weapon sound is one of the game's own Wwise events, picked by name. Swap any of them for
another game sound by name; an empty value is silent (or, for a gun's shot, the gun's own sound).

| Setting | Default | Plays |
|---|---|---|
| `PlasmaShot` | GAUSSPISTOLBODYA3D | Plasma Pistol/Rifle shot |
| `RailgunShot` | GAUSSPISTOLFIRE2D_RANDOM | Railgun shot |
| `GrenadeShot` | FLAREGUNFIRE | Grenade Pistol/Rifle shot |
| `TentacleShot` | SNATCHERTENTACLESHOOT | Each tentacle lashing out |
| `TentacleBite` | SNATCHERTENTACLEHIT | A tentacle landing its bite |
| `LaserLoopStart` / `LaserLoopStop` | FREEZERLOOP / FREEZERLOOPEND | The laser beam's hum |
| `LightningLoopStart` / `LightningLoopStop` | TERMINAL_CREEPY_STATIC_LOOP / _STOP | Chain Lightning's crackle loop |
| `ShrinkLoopStart` / `ShrinkLoopStop` | BIOSCAN_TUBE_EMITTER / _STOP | The Shrink Beam's emitter hum |
| `ShrinkPop` | ENEMYWEAKSPOTEXPLODE | A shrunk enemy gibbing |
| `LightningCrackle` | FLUORESCENT_LIGHT_CRACKLE | Where lightning strikes |
| `Thunderclap` | ELEVATOR_THUNDER | Mjolnir's thunderclap |
| `BannerPlant` | SENTRYGUN_DEPLOY | Planting the Victory Banner |

## Multiplayer

Everyone in the lobby needs this mod, with the same weapons switched on: weapons sync between
players by an id that only exists for players who have it. The player who fires deals the damage
through the normal bullet path, so health, stagger and kills stay in sync. Teammates are sent every
shot, beam, tentacle, bolt, grenade, flash and stealth, and see the same thing. Knockback is done by
the host, who owns enemy positions.

## Compatibility

- **GTFO.EZ:** compatible, in either load order; where the two clash, this mod wins. Everything it
  changes in the game's data is applied after GTFO.EZ's. Weapons are built after EZ's tuning, so
  each starts from EZ's version of the weapon it's copied from; stats this mod sets itself stay
  fixed. Keys don't
  overlap (this mod only adds a second press of the game's slot keys; GTFO.EZ uses E, X and Z), and Insert/Delete are the shared
  toggles both mods carry - only one answers.
- **GTFO.EZ.AbsurdItems:** compatible - the Hand Grenade, Stim Pack and loot mix that used to be in
  this mod live there now. A GTFO.* mod switched off in its config (`[0. Mod] Enabled`) counts as
  not installed for the others.
- **GTFO.EZ.AbsurdMoves:** optional - without it (or with it switched off), the held-trigger guns
  (lasers, Chain Lightning Gun, Shrink Beam) hold off firing while you're sprinting, like vanilla
  guns. With it installed and its `[Fire On The Move] Fire While Running Enabled` switched on, they
  can fire while you sprint too.
- **Other mods that add weapons** at the same ids (1002-1006 melee, 1101-1119 guns, 1202
  tool): if one already uses an id, that weapon isn't added and `LogOutput.log` says so. The
  rest still load. A melee weapon whose attack speed isn't 1 also adds a melee animation set at its
  own id, and the guns add recoil profiles at 1101-1109 and 1119 and a first-person hold at 1103; a
  clash there leaves that weapon its model's animation timing or recoil.

## Requirements

BepInExPack_GTFO, which includes GTFO-API (used to send shots and effects to teammates).

## Changelog

See `CHANGELOG.md`.
