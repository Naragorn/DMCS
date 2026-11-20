# DMCS — implementation notes

Everything about how DMCS is built, verified and tested: build steps, the game APIs it uses (read in the decompiled
`ThunderRoad.dll`), how each mechanic works inside, the headless harness and the test log. The spec is `CLAUDE.md`;
the player-facing description and the full Mod Menu list are in `README.md`.

## Status

| Build step | State |
|---|---|
| 1. Project setup, SDK check, mod loads, Debug Logging, Mod Menu skeleton | **Done. Unit tests + in-game harness PASS (2026-11-16).** Menu look in the headset not checked. |
| 2. Charge Stagger | **Done. Unit tests + in-game harness PASS (2026-11-16).** Feel in the headset (real sprint + real swing) not checked. |
| 3. Disarm Strike | **Built, unit tests PASS, in-game harness PASS (2026-11-16).** Not checked in the headset yet. |
| 3b. Body Hit Drop, chances per NPC type, shields, fling | **Built, unit tests + in-game harness PASS (2026-11-16, chance-per-type version).** Not checked in the headset; Twist Disarm not built (with step 5). |
| 4. Adrenaline | **4a (meter), 4b (critical / fatality, chest-stab finish), 4c (kneel and execution) and 4d (signals: ready sound, heartbeat, haptics, hand glow, Dark Messiah-style vignette) built, unit tests + in-game harness PASS (2026-11-16/17).** Block/parry and damage taken are unit-tested only. Nothing checked in the headset (sound, haptics and the vignette's look need it). 4e (dodge and catch bonus) built 2026-11-17, unit tests + harness PASS; the harness raises the game's attack event but no NPC really swings, so a real dodge is a headset check. |
| 5. Clash of Might | **5a (telegraph: big swing, flash, slow-motion) built 2026-11-17, 5b (block at the flash, lock) 2026-11-17, 5c (mode Off/Simple/On, rounds, push measure, outcomes) 2026-11-18; unit tests + harness PASS.** The slow-motion's "go" path, the flash's look, the timing and every push value are unit-tested / headset only. A real press against the frozen blade was never measured (see 5c). Twist Disarm and the three more flash sounds not built. |
| 6. Counter Parry and Power Parry | **Built 2026-11-20, unit tests + harness: see the test log.** Speeds are guesses; a real parry against a real NPC swing is a headset check. |

## Versions (checked 2026-11-16)

| What | Value | Source |
|---|---|---|
| Game build | **1.3.2.261012003** | `Player.log` line `Game version: 1.3.2.261012003` (last run 2026-10-23); Steam buildid `25901209`, last updated 2026-10-14 |
| Unity | **2021.3.38f1** | `Player.log`: `Initialize engine version: 2021.3.38f1 (7a2fa5d8d101)` |
| Official SDK | **1.3.0 SDK** (master, last commit 2026-11-06) | `https://dev.azure.com/Warpfrog/BasSDK/_git/BasSDK` README. The GitHub repo `KospY/BasSDK` is frozen at 1.1.0 and says it moved to Azure DevOps. |
| API ground truth | the game's own `BladeAndSorcery_Data/Managed/ThunderRoad.dll` | decompiled with ILSpy 9.1 (`ilspycmd`), read-only, not shipped |

The mod compiles directly against the installed game's `ThunderRoad.dll`, so the API it uses is exactly the one the game runs, not the SDK's copy.

## Build

Requirements: .NET SDK 8 (builds the `net472` mod via the `Microsoft.NETFramework.ReferenceAssemblies` package — no Mono/Windows needed) and a local Blade & Sorcery install.

```sh
dotnet test                                               # build + all tests
dotnet format --verify-no-changes                         # lint
dotnet build src/DMCS/DMCS.csproj -c Release -p:DeployToGame=true   # copies DMCS.dll + manifest.json to Mods/DMCS
tools/harness.sh                                          # in-game scenarios, headless (see below)
tools/deploy.sh                                           # committed build into BOTH game installs (Linux + Windows)
```

Game path defaults to `~/.local/share/Steam/steamapps/common/Blade & Sorcery`; override with `-p:BasGameDir=...` or env `BAS_GAME_DIR`.

### Layout

| Path | Purpose |
|---|---|
| `src/DMCS.Core` | Pure logic (no game references): logger gate, feature toggles, reset logic, category names, Charge Stagger rule, speed history, stagger windows, slider values. Compiled into `DMCS.dll`. |
| `src/DMCS` | The mod: `ThunderScript` entry point, Mod Menu options, one class per feature. Target `net472` (same as working B&S script mods, e.g. HealthRegen.dll). |
| `tests/DMCS.Tests` | xUnit. Core logic flows + a metadata test that inspects the built `DMCS.dll` the way the game's ModManager does (via `MetadataLoadContext`, no game code executed). Needs the game install. |

## Mod Menu (inside)

Option names, ranges and defaults: `README.md` → Mod Menu. Each option is a static property with `[ModOption]` in its
feature class; slider ranges and defaults live in `DMCS.Core` (`*Settings`, `SliderSpec`), so tests can check that every
default is one of its slider values. Clash of Might has only its toggle so far.

## Charge Stagger (step 2)

A hit by the player on a living NPC staggers it when all of these hold:
- the player's top speed in the last *Speed Window* seconds is at least *Speed Threshold* (the game's own
  `Locomotion.horizontalSpeed`; measured in the Arena: walking **2.76 m/s**, full sprint **6.0 m/s**);
- the hit is fast enough: `|CollisionInstance.impactVelocity|` ≥ *Min Swing Speed* (swing, punch or projectile);
- the *Chance* roll succeeds.

Melee (held weapon, fist) always counts; thrown weapons and spells count while *Thrown And Spells Count* is on. For
those, the player's speed is taken **when the hit lands** (with the speed window), not when the item was thrown.
NPC-vs-NPC hits never count. Any number of NPCs can be staggered. A **second** charge hit on an NPC that is already
staggered (DMCS window, the game's own stagger, or lying down) **knocks it down** instead (*Second Hit Knocks Down*).

**Presets.** *Preset* sets six values at once; each stays adjustable, and changing one shows *Custom*:

| | Chance | Speed Threshold | Speed Window | Min Swing Speed | Strength | Duration |
|---|---|---|---|---|---|---|
| **Gamey** (default) | 100 % | 4 m/s (fast jog) | 0.5 s | 1 m/s | Full | 2.5 s |
| **Realistic** | 75 % | 5.5 m/s (near full sprint) | 0.15 s | 3 m/s | Medium | 1.25 s |

The running speeds rest on the measured walk/sprint speeds. The swing speeds (1 and 3 m/s) and the realistic chance
are **our picks, not measured** — how fast a real swing hits needs the headset. The preset row is saved but not
applied on load (`[ModOptionApplyOnPress]` → `applyOnLoad = false`): saved values load as they are and the row
follows them, so a preset never overwrites a custom setting.

**Deviation from the spec (agreed 2026-11-16):** the spec's `staggerDuration` cannot set how long the game's stagger lasts —
`BrainModuleHitReaction.StaggerCoroutine` runs until the stagger animation ends (`IsAnimatorBusy()`), only the level is selectable.
So *Strength* picks the game's level (`ForceStagger` with `StaggerLight / StaggerMedium / StaggerFull / Destabilize`), and
*Duration* is how long DMCS counts the NPC as staggered (the window for the knock-down and for Disarm Strike). For a knock-down the
duration is real: DMCS holds the NPC on the ground with `Brain.AddNoStandUpModifier` (harness: up after 4.2 s with 4 s, 1.4 s with 0.5 s).

`ChargeStagger.Staggered` is raised per stagger and knock-down for the Adrenaline meter (step 4).

## Disarm Strike (step 3)

A player hit (any source: weapon, fist, thrown, spell) on a hand of an NPC that was **staggered before this hit** makes it
drop its weapon — both hands by default (*Drops Both Hands*), else only the hit side. "Staggered" = inside a Charge Stagger
window, staggered by the game itself (`Brain.currentStagger`), or lying down. It is judged at the hit's `OnStart`: the game's
own hit reaction pushes before `onCreatureHit` `OnEnd`, so without that the hit that *causes* a stagger would also disarm.
The same rule now applies to Charge Stagger's "second hit knocks down".

Release goes through `RagdollHand.TryRelease` and `EventManager.InvokeDisarm`, like the game's own hand-hit disarm
(`BrainModuleEquipment.OnDamage`, which needs `pushLevel == 2`). For *No Regrab Time* (or for good with *Stays Permanent*):
- any item that lands in its hands is released again (covers picking up and drawing from holsters);
- with *Stand Still* the NPC gets the game's `HumanDummy` brain (`Brain.Load`, which swaps the brain at runtime) — it stands,
  does not attack or flee — and gets its own brain back when the time ends.

Not yet verified in game: that `HumanDummy` never picks anything up, and how the release looks.
`DisarmStrike.Disarmed` is raised per disarm (for Adrenaline).

### Body Hit Drop, shields, fling (step 3b)

- **Body Hit Drop:** a player hit anywhere but the hand (forearm counts as body while *Include Forearm* is off) on an NPC
  that was staggered before the hit drops its weapons with *Body Hit Drop Chance* (25 %). Each body hit in the same stagger
  that did not drop adds *Drop Chance Per Extra Hit* (25 %): 25 / 50 / 75 / 100 %. The count is cleared once the NPC is no
  longer staggered. The defaults are a first guess, not tuned in the headset.
- A body hit that does not drop shows sparks at each held weapon (the game's `HitMaterialMetal` effect: metal sound +
  `Bas.Particle.Collision.Metal`) and gives a `RagdollHand.HapticTick` in the player's hitting hand.
- **Fling:** a weapon knocked out by a hand or body hit gets `PhysicBody.AddForce(direction * Fling Force, VelocityChange)`
  along the hit's `impactVelocity` (3 m/s, a first guess).
- **Shields** stay in the hand unless *Shields Can Be Disarmed*; a shield is what the game's own disarm calls one
  (`ItemModuleAI.primaryClass == Shield`).
- **Chances per NPC type** (Body Hit Drop): *Body Hit Drop Chance Female* (`CreatureData.gender == Female`) and
  *Body Hit Drop Chance Smaller* (NPC shorter than the player by at least *Smaller By* cm, default 0;
  `Creature.GetHeight()` = `morphology.height * lossyScale.y` for both). Both default to 25 % = the base chance, so they
  change nothing until set. The first-hit chance is the highest that fits the NPC; the extra-hit bonus adds to it.
  Base 0 % with a female chance = only female NPCs.
- Not built: a link to the Threat mod's surrender state (dropped 2026-11-16, not testable in the harness yet).
- Not built: "grab the handle while staggered to take the weapon". The base game already gives a dagger or short sword to
  whoever grabs its handle, staggered or not (`Handle.OnGrab` → `Release()` when `axisLength == 0`; seen in a harness probe
  2026-11-16 with `DaggerCommon` and `SwordShortCommon`).

### Twist Disarm (step 3c, built 2026-11-18 with step 5)

- **Contact:** the player's held weapons' own collision handlers (`CollisionHandler.OnCollisionStartEvent`, subscribed on
  `EventManager.OnItemGrab` by a player hand and dropped on `OnItemRelease` when no player hand holds it, as the game's own
  `BladeDancer` subscribes `item.mainCollisionHandler`); the other side must be an item of type Weapon held by a living
  non-player creature. Source and target are swapped when the event comes the other way round. The game's
  `onCreatureAttackParry` was not used: `BrainModuleDefense.OnWeaponCollision` raises it only while the NPC is defending
  (read 2026-11-18), so it misses most blade contacts.
- **Roll:** `HandSpeeds` samples each controller's rig-local rotation (`PlayerHand.transform.localRotation`) every frame;
  `RollMeter` turns it into an angular velocity over 0.03 s; at the contact the fastest component along the held blade's
  long axis (grip → furthest damager, turned into the rig's space) in the last 0.15 s is the roll. A swing turns the blade
  around shoulder and elbow, not around itself, so it adds next to nothing; only the wrist's roll counts (spec).
- **Rework 2026-11-20 (user): only a staggered enemy** while *Disarm Requires Stagger* is on (`HitInfo.IsStaggered`:
  Charge Stagger window, the game's own stagger, off balance), chance default 100 %. Order in the rule: … InLock,
  TooSlow, Cooldown, NotStaggered, chance; so the log names a fast twist on an unstaggered enemy, and a slow touch starts
  no cooldown (a touch on the way in had blocked the twist 0.3 s later; slow touches are only logged once per 0.5 s).
  Measured: the game's light stagger (`push=1`) lasts 0.5–0.9 s, push 2 knocks the NPC off balance for 0.9–1.3 s.
- **Rule** (`TwistRule`, Core, unit-tested): Disarm Strike and *Twist Disarm* on, an enemy weapon, not a kept shield,
  not during a Clash lock, one decision per enemy per 0.5 s (a single clash raises several collision events), roll ≥
  *Twist Min Roll Speed* (400°/s, guess), then *Twist Disarm Chance* (50 %). No stagger needed. The disarm goes through
  `DisarmStrike.DisarmAlong` (fling along the hit, no-regrab lock, stand still, Disarm adrenaline, catch bonus).
- Log: `blade on HumanMale0's SwordShortCommon: no twist disarm, TooSlow (roll 0°/s (min 400), hit 2.52 m/s)` and
  `twist disarm on HumanMale0's SwordShortCommon (roll 904°/s (min 400), hit 2.31 m/s)` (harness 2026-11-18).
- Harness after the rework: `step3c` (not staggered → `NotStaggered`; staggered but slow → `TooSlow`; Twist off →
  nothing) and `step3d-twist-disarm-staggered` (blade ready beside the NPC's with `player-strike-weapon phase=ready`, a
  `push=1` torso hit, then a longer, deeper strike: 0.5 s, 0.4 m past the blade → disarm), both at `frame-rate 90`. The
  disarm needed its own fresh game: after the hits and NPCs of step3c it missed in most runs (the NPC drew an axe, mace
  or buckler from its loadout into the free hand, the first NPC was knocked off balance, the mock hand lost its sword).
  Correction: the first `frame-rate` (Application.targetFrameRate) capped nothing, as XR platforms ignore it (Unity
  scripting reference; found by the Achilles session: deltaTime stayed 0.001–0.003 s). So the 4/4 and the 17/17 suite
  of c706503 ran uncapped; what made step3d pass was its fresh game and the longer, deeper strike. BasHarness now has
  its own frame limiter (measured 90 fps); step3d 3/3 and step3c pass with it (2026-11-20).
- Films after the rework (`harness/films/twist-unstaggered-film.txt` and `twist-disarm-film.txt`, views side, first, hand):
  before (65a542a) an unstaggered twist disarmed (`roll 981°/s`), after it does not (`NotStaggered, roll 993°/s`); a
  staggered twist disarms (`roll 1116°/s, hit 4.68 m/s`).
- First film (2026-11-20): `harness/films/twist-disarm-film.txt` (not in the suite) films section 2's strike with BasHarness
  `record-start`/`record-stop` (fab59c4) from the side and from the player's eyes, 60 fps; before (31f38bd, same steps
  minus the Twist chance option, which that build lacks) the NPC keeps its sword, after (65a542a) it drops it
  (`roll 910°/s, hit 2.10 m/s`).
- Not verified: how fast a real wrist twist is in the headset (400°/s is a guess), and whether real swings in a fight give
  contacts the game reports (CollisionHandler drops slow contacts; the harness strike was 2.2–2.5 m/s).

## Adrenaline (step 4a)

- Meter `AdrenalineMeter` (Core): Filling → Ready (window) → empty again when the window runs out unused. Real time
  (`Time.unscaledTime`), so slow-mo will not stretch the window. Gains while Ready are ignored.
- Gains: a player hit on an NPC (`onCreatureHit` OnEnd, any player source), an NPC hit with damage on the player,
  a block or parry (`onCreatureAttackParry`, parrying creature = player), a charge stagger and a disarm (DMCS events).
- Decay: a filling meter loses *Decay Per Second* after *Decay Delay* seconds without a gain.
- Debug log (channel `Adrenaline`): `+5 Hit (<npc>): 5/100`, `meter full: window open for 6 s`,
  `window over unused: meter emptied`, `meter drained to 0`.
- Damage taken lowers the meter by *Per Damage Taken* (*Damage Taken Lowers Adrenaline*, on); a full meter keeps its
  window. Decay is switched off by default for now.
- Harness `step4a-adrenaline-meter`: hit, disarm, full + expiry, decay, off. Not covered in game: block/parry and damage
  taken (no harness step for them yet).

### Critical / fatality (step 4b)

- The next player hit with a full meter that is melee (weapon, fist, kick; thrown and spells behind a switch) and at least
  *Critical Min Swing Speed* fast. It is judged at the hit's start, before the game's damage, and done at its end.
- Battle cry: the player's own voice module (`BrainModuleSpeak.Play("attackmelee")`, voice `HumanMale1`/`HumanFemale3`).
- Slow-motion: `TimeManager.SetSlowMotion` with the game's `Hyperfocus` or `SpellSlowTime` effect; in Hyperfocus mode the
  player gets the Hyperfocus skill's buffs scaled with 1/scale (joints, speed, drag), removed at the end. Skipped when
  another slow-motion runs; ended only if it is still ours (or forced on feature off / level unload).
- Otherwise: `RagdollPart.SafeSlice()` on the hit part, `Creature.Kill(hit)`, then `BrainModuleDeath.StopDying()` and a
  velocity change on every part along the hit.
- Chest stab (torso + penetration or Pierce): no slow-motion yet; the NPC kneels with the blade in it and dies. Within
  *Execution Window* a kick (player body hit by a foot or leg) starts the slow-motion and throws it; a slash on the neck or
  head cuts the head off (`SafeSlice`; the cut runs in the game's `SliceCoroutine`, so the head's push waits until
  `isSliced`), starts the slow-motion and throws the head. The kneeling death: that NPC's `BrainModuleDeath.stabAnimationData`/`defaultAnimationData` are
  set to a kneeling-only copy of `HumanStabDeaths` for the kill (the game picks the clip inside `Kill`), then restored.
- Harness `step4b-adrenaline-critical` (2026-11-16): light hit → `TooLight`, thrown → `Ranged`; arm hit → battle cry
  `Success (male_1_attack_11)`, Hyperfocus 0.1× for 3 s, `cut off LeftArm`, killed, thrown, time back after 3 s; chest stab
  → `kneels (Bas.Animation.Death_KneelBackward)`, then a kick (`hit-npc by=foot dead=true`) → `kicked … away` with
  slow-motion, and on a second NPC a neck slash → `beheaded`, `the head flies`; Slow Time 0.5× without player buffs.
  Look and feel: headset only.

### Kneel and execution (step 4c)

- With the window open, the NPC hit last (else the nearest NPC targeting the player) goes down on its knees, alive, when
  no hard hit lands for *Kneel After No Hit* or after *Kneel After Light Hits* light hits (below the hard-swing speed).
  The meter is spent; the execution window (*Execution Window*) replaces the adrenaline window.
- Held as the game's `BrainModuleFear.Panic` holds a cowering NPC: `brain.isIncapacitated`, behaviour tree reset and
  `updateTree = false`, stance Idle, `StopAnimation(true)`, `PlayAnimation("HumanIdleExamineKneel")` (KneelingStart 2.77 s,
  Examine_Kneeling loop, KneelingEnd); each frame the dynamic-animation flag is kept as `BrainModuleFear.OnUpdate` keeps it.
  A first try with a brain swap to `HumanDummy` left the NPC standing (harness screenshot).
- Any player hit on it is the execution (the critical; a stab kneels it into death with the kick/behead finish).
  Not executed: it stands up (`StopAnimation`, brain runs again) or dies with *Kneel Death Chance*.
- Harness `step4c-adrenaline-kneel` (2026-11-16): no hit → kneels, animator FullBody `KneelingStart` → `Examine_Kneeling`,
  head height 1.76 → 1.27 m (probe), side screenshot shows it crouched; executed; light hits → kneels → stands up; death
  chance 100 % → dies; a stab execution → kneeling death → kick.
- `step2`, `step3`, `step3b` switch Adrenaline off: its critical would kill their test NPC. Every combat scenario starts with *Reset To Defaults*, so one that failed before its own reset does not leave its settings to the next (seen 2026-11-17: a `step3` timeout left Adrenaline off and failed all of `step4`). `step3` (fighting
  `HumanMedium` brain) once failed to stagger on `push=1` and passed on the rerun (known flake of the fighting brain).

### Signals and vignette (step 4d)

While the adrenaline window or an execution window is open (`AdrenalineSignalRule`), each switchable on its own:
- **Ready sound** once when the meter fills. DMCS synthesises it at runtime (`ReadySound`), modelled on what Dark
  Messiah's own `Adrenaline_Reached.wav` does, measured in its file (2026-11-17): 2.29 s stereo; a thunder roar at the
  start (dense and dark, weighted to the low mids, swelling in over ~50 ms, loudest 0.05–0.4 s, then rolling off; no
  crackle); a low rumble (20–160 Hz) carrying
  the first 1.1 s; a bright hiss swelling in the first 0.2 s with a floor to 1.1 s; and a metallic ring at ~1021 Hz with
  partials near ×2, ×3, ×3.85 and sidebands ~60 Hz off that swells to ~1 s and rings out. The mix is soft-limited (tanh)
  to Dark Messiah's loudness at the same peak. Tuned until the same band measurements matched per 100 ms (e.g. 300–2000 Hz in
  0.1–0.2 s: 99–110 vs 98–111; loudness early 0.20–0.23 vs 0.17–0.27 RMS, body 0.14–0.17 vs 0.15–0.18).
  Dark Messiah's file is never loaded or shipped.
- **Heartbeat**: the game's own loop (`Catalog.gameData.heartBeatLoopAddress` = `Bas.Audio.Misc.HeartBeatLoop`) on
  DMCS's own 2D AudioSource, faded in and out.
- **Haptics**: a lub-dub pulse (`HeartbeatPulse`, 100 bpm) in both hands via `RagdollHand.HapticTick`.
- **Hand glow**: the game's `HyperfocusHand` effect on each hand that holds something, placed as `SkillHyperFocus` does.
- **Vignette**: 25 frames of 256×256 drawn at startup (`VignetteFlipbook`): thin radial red/orange smears from the edges,
  patchy, black centre, every frame different, looped at 25 fps. Technique as Dark Messiah's own overlay (25 frames of
  256², additive, 25 fps); the look was compared with its frames (lit share 6 % vs 4 %, G/R of lit pixels 0.16 vs 0.18)
  but the frames are DMCS's own drawing. Shown on a spherical cap 0.1 m around the eyes, child of the head camera (near
  plane 0.02 m), with the game's `Universal Render Pipeline/Particles/Unlit` set to additive (harness probe 2026-11-17:
  loaded and supported; `Universal Render Pipeline/Unlit` and the legacy particle shaders are not). *Vignette Size* is the
  angle from the view centre to the frame's edge; the cap fades out over its outer 15 % (vertex alpha), as the frames'
  corners are lit and the square border would otherwise show as a hard line (seen with DMCS 1 at 30°, 2026-11-17); it
  flares on the critical (*Vignette Flare*).
- **Versions to compare** (*Ready Sound Style*, *Vignette Style*, *Heartbeat Sound*): every version DMCS built stays
  selectable, byte for byte as built (checked against the old commits): sound DMCS 1 (rumble, hiss, ring), DMCS 2 (+ a
  crackling thunder crack), DMCS 3 (+ a dark thunder roar, default); vignette DMCS 1 (flames), DMCS 2 (even radial
  streaks), DMCS 3 (patchy radial streaks, default), DMCS 4 (tuned to Dark Messiah's frames by their measured
  statistics: fans of fine radial lines that glow up and fade in place, light rising into the corners with a dim red
  corner glow; DM vs DMCS 4: mean red 6.3 / 6.2, lit pixels 4.0 % / 4.2 %, G/R 0.18 / 0.17, neighbouring frames
  correlate 0.89 / 0.95, five apart 0.59 / 0.62, mean red by radius 0.8–1.3: 4 / 7.5 / 12 / 24 / 35 / 36 vs
  6 / 7 / 9 / 21 / 36 / 41; own drawing, nothing traced). Picking a DMCS vignette sets *Streaks*, *Length* and *Hue* to how
  that version was built (only when picked; saved values win on load).
- **Dark Messiah** in any of the three reads the original files from the player's own Dark Messiah install at runtime
  (`DarkMessiahAssets`; nothing is shipped): `Adrenaline_Reached.wav`, `Player_Heartbeat01.wav` (the fast one, looped)
  and the 25 frames of `fullscreen_adrenaline2_.vtf`, out of its VPK archives (`VpkDirectory`, `WavData`, `VtfData` with
  a DXT1 decoder). The install is searched in: Blade & Sorcery's own steamapps\common, every library in Steam's
  libraryfolders.vdf (the Linux Steam under Proton through Wine's Z: drive, the Windows Steam from the registry), and
  the usual library folder names (SteamLibrary, Steam, Program Files (x86)\Steam, …) on every drive and, under
  Proton, every mount below /run/media, /media, /mnt and /home. Not found → DMCS 3 / Blade & Sorcery, logged once.
- Frames and sound are built at level load (frames in a few ms: each streak only touches its own pixel box).
- Harness `step4d-adrenaline-signals` (2026-11-17): meter full → ready sound (2.30 s), signals on, shader found, cap on the
  `Head` camera, heartbeat clip loaded and playing; screenshots at 50° and 30° show the streaks (the desktop mirror shows
  less than an eye, so at 50° only the edge shows); critical → flare, signals off; Adrenaline off mid-window → stopped.
  Not checked: how loud, how strong and how it looks in the headset; the glow (the harness player holds nothing).

### Dodge and catch bonus (step 4e)

- **Dodge** (*Adrenaline Per Dodge* 10, *Dodge Min Speed* 2 m/s): an NPC melee attack on the player
  (`EventManager.onCreatureAttacking`, raised by `BrainModuleMelee`) that reached `AttackStage.Attack` and ends with
  `End` (not `Cancel` = interrupted), with no hit on the player from that NPC (a hit whose attacker cannot be read voids
  every open attack) and no block/parry, while the player's **head** reached the min speed between Attack and End.
  Head speed instead of the stick speed, because a VR dodge is mostly a real step or lean. It is measured over 0.1 s
  (`HeadSpeedMeter`): the rig moves in physics steps, so per-frame speeds jump; the first harness run read 19.85 m/s
  from a 2.5 m/s run that way. A jump above 1 m between two frames is a teleport and starts over.
  Log: `dodged <npc>'s attack (head 5.05 m/s)` / `no dodge of <npc>'s attack: TooSlow (head 0.00 m/s)`.
- **Catch** (*Adrenaline Per Catch* 10, *Catch Max Air Time* 3 s): a weapon a DMCS disarm released from an NPC's hand
  (`DisarmStrike.KnockedAway`), grabbed by the player's hand (`EventManager.OnItemGrab`) before it touched the ground and
  within the air time. Ground = a contact with a collider that belongs to no `ColliderGroup` (level geometry, static
  props), read by an own `OnCollisionEnter` component on the weapon (the game's `CollisionHandler` drops slow contacts).
  An NPC grabbing it first ends the chance; telekinesis does not count. Log: `caught <item> in the air`,
  `<item> touched the ground: no catch bonus`.
- Pure logic in Core (`DodgeTracker`, `HeadSpeedMeter`, `CatchTracker`), unit-tested over every flow.
- Harness `step4e-adrenaline-dodge-catch` (new BasHarness verbs `npc-attack <stage>` and `player-grab-dropped`): catch at
  once → bonus; after landing → none; attack missed while running → dodge; standing → TooSlow; cancelled → none;
  Adrenaline off → nothing. Not covered in game: a hit or a parry during the attack (unit tests only), a real NPC swing,
  and the speed a real dodge reaches in the headset (the 2 m/s default is a guess).

### API for other mods

`DMCS.Adrenaline.AddExternal(float amount, string source)` (public static, in `DMCS.dll`; found by name through
reflection, so other mods need no hard dependency; keep it stable): adds `amount` to the meter like any DMCS source and
opens the window when the meter fills (ready sound, signals, kneel timer). Returns false when Adrenaline is off, the
amount is not positive, or the window is already open. The debug log shows `+<amount> External:<source> ()`. Used by
the Achilles Tendon mod (`AchillesHeelCut`, `AchillesCrippledKill`). Unit-tested rule and meter flows; the call itself
is not exercised by the harness.

## Clash of Might, step 5a: telegraph

Design and probe results: `CLAUDE.md`, section 1.
- **Big swing:** at `AttackStage.WindUp` (raised by `BrainModuleMelee.AttackWindow`) the running attack is
  `BrainModuleMelee.attack`; its wind-up = `AttackMotion.GetRealStartTime(GetBrainSpeedMultiplier(side))`. Big when it is no
  thrust and the wind-up is at least *Clash Min Wind Up* (0.4 s). Logs: `<npc> winds up <attack> (0.49 s) with <item>: flash`,
  `<npc> swings <attack> (wind-up 0.26 s): not a big swing`, `… no weapon in the Right hand`, `… with <item>: no slashing blade (<damager>/<type>, …)`, `… not a sword`.
- **Sword:** catalog category `Swords`/`Greatswords` or an id starting with `Sword` (the faction swords); with *Only Swords* off any
  weapon with a damager whose `damageModifierData.damageType` is `Slash`.
- **Flash point:** the slash damagers' colliders (`colliderOnly` or the collider group), their bounds' corners projected on handle →
  tip (`GetFurthestDamagerCollider`); the middle of that extent, stored local to the weapon so it follows the blade.
- **Flash:** an own 128² star (`Glint`, white, alpha = shape: bright core, soft halo, four long rays along the axes, four shorter
  fainter diagonal rays), warm white, additive URP `Particles/Unlit` (as the vignette), a quad of 0.3 m facing the head camera.
  Over time: burst from 0.5× to 1.5× size and full brightness in 0.08 s, settling within 0.25 s to 1× and 80 %, turning 40°/s so the
  rays glitter, until the attack ends. The first version (64², thin rays, 0.14 m, 60 % glow) was hard to see from the player's
  eye (picture probe 2026-11-17, user: too small and pale).
- **Flash at the swing top (user 2026-11-17, *Clash Flash At Swing Top*, default on):** the flash (and its sound) waits for the
  top of the wind-up (`SwingTop`: the blade tip's height above the NPC's feet has fallen 2 cm below its highest point, not before
  40 % of the wind-up, at the latest when the strike starts). Probe (11 big swings): the tip is highest at 79–100 % of the wind-up
  (0.03–0.08 s before the strike); a lunge starts high and dips first, so it flashes at 40 %. In `step5a` the flashes came at
  0.20–0.44 s of 0.44–0.50 s. This shortens the warning: off brings back the flash at the wind-up start. Log: `<npc>'s blade
  flashes at the swing top (0.26 of 0.44 s), sound DMCS 1 (3.60 s)`.
- **Two blades on the first pictures:** a test setup artefact. The container `CivilianMeleeSword` puts a sword on the NPC's back,
  and an extra `npc-grab` gave it a second one; it drew the back sword into its other hand. The clash scenarios no longer grab.
- **Flash sound (2026-11-17, user: "one should sound 99 % like Sekiro's"):** *Clash Flash Sound* (Off, DMCS 1, synthesised by
  `FlashSound`, built in the background at level load, 2D on DMCS's own AudioSource) and *Clash Flash Game Sound* (Off, Metal Hit =
  `HitMaterialMetal` with sparks, Focus Ready = `FocusReady`, Gravity Pulse = `ImpactPulse`, spawned at the flash point); both play
  together, so every combination works. DMCS 1 is modelled on Sekiro's perilous-attack cue, measured in a recording (YouTube
  `5YzN2bPgjB0`, 3.67 s, whose uploader says it was "pitched and slowed to try and match its in-game appearance"; Sekiro's own sound
  banks are encrypted, their first bytes `80 C6 90 86` instead of `FSB4`/`FSB5`, and were not read). The reference: a sub boom
  (~31 Hz plus 60–115 Hz) that carries the sound, loud to ~0.9 s, a fifth at 2 s, gone at 3.4 s; a broadband strike in the first
  50 ms; a ring at ~1863 Hz (beating with 1670/1790 Hz) with 3670 Hz and short 5460/7345 Hz partials. DMCS 1 against it (scratchpad
  scripts, per 0.25 s, seven bands): band levels 2.35 dB off on average (max 6.9), loudness curve 1.48 dB, the 1863 Hz ring within
  ~2 dB to 2 s. "99 %" cannot be measured; the user listened and called DMCS 1 usable (2026-11-17). Three first other
  versions (blade ring, gong, ping) were dropped on the user's word; three new ones come at the end of step 5. Nothing of Sekiro's is shipped.
- **Slow-motion:** from `AttackStage.Attack` on, for 0.1 s each physics step (`ScriptFixedUpdate`): NPC within *Enemy Reach*,
  the player's held blade (handle → tip of either hand) within *Block Reach* of the flash point, and the angle between the
  player's blade and the swing plane's normal (blade axis × velocity at the flash point, `GetItemPointVelocity`) within
  *Block Angle Tolerance*. Go → `TimeManager.SetSlowMotion(scale)` with the `SpellSlowTime` effect, until the attack's End/Cancel
  (cap 2.5 s real); skipped when another slow-motion runs, ended only if it is still ours: while the game still eases the scale
  down (`TimeManager.slowMotionState == Starting`) it counts as ours (`ClashRules.StillOurSlowMotion`). Without that, a swing that
  ended 0.14 s after the start (scale still 0.68) left the slow-motion running for good (picture probe 2026-11-17). Log: `block ready against <npc>'s swing
  (enemy 1.40 m, blade 0.30 m, angle 12°): slow-motion 0.30x` or `no slow-motion for <npc>'s swing: BladeTooFar`.
- Pure logic in Core (`ClashRules`, `ClashGeometry`, `ClashTelegraph`, `Glint`), unit-tested over every flow.
- Harness `step5a-clash-telegraph` (new BasHarness options `spawn-npc … container=<id>` and `player-invincible`): a real
  `HumanMedium` NPC with only a sword (container `CivilianMeleeSword`; it drew its second sword too) fights the standing,
  invincible player for 60 s: big swings flash (e.g. `MidLeftSwing` 0.51 s, `ChainHighRightSwing` 0.60 s), every one refused with
  `BladeTooFar` (the harness player holds nothing); *Min Wind Up* 0.8 → nothing flashes; feature off → no clash lines.
  Found on the way: the default loadout is random (a mace in the left hand); the player dies after about a minute without
  invincibility; the NPC's swings into the standing player counted as player body hits and Body Hit Drop disarmed it (see
  "Open findings").
- Picture probe 2026-11-17 (temporary code, not committed): with the player holding a sword and loosened values (block reach
  1.5 m, 80°, 4 m) the slow-motion started in game (`Time.timeScale` 0.30) and ended with the swing. The flash is hard to see
  from the player's eye (0.14 m, 60 % glow on bright sand): size, colour and brightness need sliders and the headset.
- Not covered in game: a block with the default values (the harness cannot move the player's arm), whether 0.1 s at the strike
  start leaves enough time.

## Clash of Might, step 5b: the lock

- **Block:** the game's own block event (`EventManager.onCreatureAttackParry`, raised by `BrainModuleMelee` when an NPC's attack
  hits a held weapon at more than ~1.4 m/s) with the NPC as the parried side and the player as the parrying side. The NPC must be
  flashing and the parried item must be the flashing weapon (else: `you blocked <npc>'s other weapon`). `ClashRules.JudgeBlock`:
  contact (`CollisionInstance.contactPoint`) within *Clash Point Tolerance* (0.25 m) of the flash point, and the player's blade
  within *Block Angle Tolerance* of the swing plane's normal (read now, else the one stored at the strike start; unknown = the
  contact decides). Else `you blocked <npc>'s swing: OffPoint|WrongAngle (contact 0.48 m from the flash, angle 87°), an ordinary parry`.
- **Lock:** our slow-motion ends; `creature.animator.speed = 0` (harness probe: the NPC holds its swing pose); the player's weapon
  `physicBody.mass ×` *Clash Weapon Weight* (20); the flash stays at the crossing. Every 0.1 s a diagnostic for the push measure of
  5c: `lock t=0.51 s: hand ahead 0.04 m (towards the enemy 0.00 m), blades 0.24 m apart` (`PlayerHand.grip`, the controller's target,
  minus `RagdollHand.grip`; towards the enemy = projected on the horizontal direction to it). Until the rounds decide it (step 5c; first: after *Clash Max Duration*, 4 s, removed in 5c) or
  when a fighter or weapon is gone, feature off or level unload: animator speed and mass back, flash off. Outcomes: step 5c.
- Pure logic in Core (`ClashRules.JudgeBlock`, `ClashLock`), unit-tested.
- Harness `step5b-clash-lock` (new BasHarness verbs `player-grab`, `player-parry [offset=]` and `await-log`): the game's block event
  raised at the flashing weapon's middle locks (contact 0.02 m, angle 56°), diagnostics, released after 2 s; with a 10° angle
  tolerance it is `WrongAngle`; 0.5 m along the blade `OffPoint`; no flash or feature off → nothing. The player's sword in the mock
  hand also met real swings: the game raised its block event by itself and one locked for real (contact 0.06 m, angle 70.9°,
  tolerance 80°). In the harness the diagnostic shows the heavy sword sagging (hand ahead 0.04–0.07 m, towards the enemy 0.00 m),
  since the mock controller does not move. A real push: headset.
- Picture probe 2026-11-17 (temporary code): five real-fight locks through `player-parry` (angles 5–54°; one refused at 81.9°,
  one `OffPoint` at 0.30 m). Moving the player's hand target slowly towards the enemy during a lock made the diagnostic react
  (`hand ahead 0.11 m (towards the enemy 0.09 m)`), and after 0.46 s the player let go of the 20× heavy sword, which ended the
  lock. Cause, read in the game code: `PlayerHand` drops a held item that is colliding when its grip is further from the
  shoulder centre than `morphology.GetArmCenterToFingerTipLenght()` (`StuckBehaviour.Drop`, the default); it happened with
  1× weight too, whenever the probe led the hand out of reach. For 5c: overstretching while pushing loses the weapon through
  the game itself. Crossed-blade pictures were taken by stepping the player 0.65–1.96 m closer first (blade middle then
  1–3 cm from the flash point).

## Clash of Might, step 5c: modes, rounds, outcomes (2026-11-18)

Decided with the user 2026-11-18 (spec, CLAUDE.md section 1).
- **Mode** `Clash of Might Mode` (string dropdown Off / Simple / On, default **Simple**) replaces the group's `Enabled`
  toggle; `FeatureToggle` stays on for Simple and On. The only group without an `Enabled` toggle (metadata test).
- **Simple:** a block that passes `JudgeBlock` (point and angle as for the lock) knocks the sword away:
  `DisarmStrike.DisarmAlong` (new; release, `EventManager.InvokeDisarm` with the parry's `CollisionInstance`, fling
  away from the player at *Fling Force*, the no-regrab lock and stand-still of every disarm path; runs while Disarm Strike
  itself is off too). Adrenaline: `+10 BlockParry` (the game's block event, as before), `+15 Disarm` (Disarm Strike's
  event) and the new `+25 ClashWin`, so one simple block is worth 50 by default (measured in the harness).
- **On:** the lock of 5b, then `ClashRounds` (pure, Core). Each push phase (*Clash Push Phase*, 2 s) the enemy's push is a
  half sine with peak *Clash Enemy Push* (0.06 m) × `ClashStrength.Factor` (difficulty/50 × npc height/player height ×
  (1 − female reduction), each scaling switchable). The player's push = lead of the controller towards the enemy minus
  *Clash Push Dead Zone* (0.03 m); the round is held when its integral reaches the enemy's (exact half-sine integral).
  Then the return phase: `|PlayerHand.grip − RagdollHand.grip|` ≤ *Clash Return Tolerance* (0.08 m) within *Clash Return
  Window* (1 s) starts the next round; else lost. Rounds per lock: random between min and max (3–5), minus *Fewer Rounds*
  (1) against a smaller or female enemy, at least 1. No progress bar (user): every round counts on its own; the last held
  round wins.
- **Win:** `DisarmAlong` (*Clash Win Disarms*), then kneel with *Clash Win Kneel Chance* (25 %, `AdrenalineCritical.KneelAlive`,
  `KneelReason.ClashWin`) or `Creature.ForceStagger(away, StaggerMedium)`; `+25 ClashWin`.
- **Lose:** `ClashLoseOutcomes` (Disarm + Hit default): the player's hand lets go (`RagdollHand.TryRelease`) and the weapon
  flies off away from the enemy; `Creature.Damage(LoseDamage, Blunt)` on the player (read: `Damage(float, DamageType)` builds
  a `CollisionInstance` and raises `onCreatureHit`, so DMCS counts it as damage taken too). Letting go of the weapon during the
  lock = lost, hit only. The enemy's animator runs on: its swing finishes.
- **Feedback:** `HapticTick` in the weapon hand every 0.05 s following the enemy's wave (× *Clash Haptics Strength*), a full
  tick when a round is held; sparks + clang `HitMaterialMetal` at the lock and each held round; the game's metal rubbing loop
  `RubbingMetalSound` (`Bas.AudioGroup.Collision.Rubbing.Metal`, intensity-driven) for the whole lock. *Clash Dark Messiah Sounds*:
  DM's `Sword_Versus_Sword` (three takes, volume 0.5–0.7, pitch 95–105 %) at the lock and each held round, `Sword_Disarm` (0.6) at
  a disarm, read through `DarkMessiahAssets` from the player's install (16-bit PCM 22050 Hz mono, read 2026-11-18), on its own
  AudioSource (a source's pitch bends everything it plays).
- **Female strength:** default 50 % weaker as asked; literature: women 40–60 % of men's upper-body strength (Roberts et al. 2016,
  doi 10.1139/apnm-2015-0540, after Wilmore 2008), men ~90 % more (Lassek & Gaulin 2009, cited in Morris et al. 2020, J Exp Biol 223).
- *Clash Block Angle Tolerance* now goes to 90° (= any angle), so the harness can lock whatever angle the mock-held sword has.
- **Harness** `step5c-clash-fight` (new BasHarness verbs `player-push <m> <s> [back=]` and `player-release [hand=]`): Simple
  disarms (`+10 BlockParry`, `+15 Disarm`, `+25 ClashWin`); On without push loses round 1 (you 0.000 vs the enemy 0.076 m·s) and
  costs real health (100 → 80, invincibility off for that section); On with pushes and returns at difficulty 0 wins after 2
  rounds (sword flies, NPC staggers); letting go during the lock loses with the hit only. PASS three times in a row; the full suite
  ran 14/15 before that change (step5c failed in its then section 4, see below).
- **Not coming back** is unit-tested only. In the harness a controller moved 0.8 m out and left there gave three different
  results in four runs (the game's arm's-length drop, the return window running out with the hand stuck 0.10 m off, once a
  win). **Resolved 2026-11-18 (probe):** the hand follows even a 0.8 m jump within ~0.7 s (DMCS: `hand ahead 0.69 → 0.05 m`),
  so which of the three comes depends on whether the sword gets stuck on something. The 0.65 m "lead" BasHarness reported was
  its own misreading: same `RagdollHand` object (checked by reference), but read in its coroutine `RagdollHand.grip` sat up to
  0.6 m away from the held sword, while DMCS read it 3 cm from the sword in the same frames. Why the bone has another pose at
  that point of the frame was not looked into. BasHarness `player-push` now measures against the held handle (a physics body)
  and agrees with DMCS (`mean 0.002 m` after the catch-up). DMCS's own push measure was right all along.
- **Measured, important for the headset:** in the harness the player's sword never touches the frozen enemy blade (the block is
  a raised event), and then nothing holds the 20× heavy weapon back: the hand catches up with a moved controller within
  ~0.3–0.4 s (`hand ahead 0.13 → 0.05 m`). The push measure only works if the frozen enemy blade really resists a press.
  Not measured. If it does not, the fallback is to make the enemy's weapon kinematic during the lock (cheap, not built).

### Open findings

- 2026-11-17, harness `step5a`: a fighting NPC swinging into the standing (invincible) player was disarmed by DMCS Body Hit
  Drop (`body hit RightArm, 25 %`). Diagnosed with a temporary log on every hit DMCS reads as a player body hit: the game
  reports the NPC's arm/hand running into the player's motionless left hand as a hit *by* the player's `LeftHand` part
  (5 contacts in 60 s, impact 2–4.8 m/s, the player's hand part moving 1.8–5.5 m/s only because it was knocked). The NPC was
  staggered by the game's own recoil (`LightAndMedium`), so the next contact rolled the 25 % body-hit chance. Cause: `HitInfo.SourceOf`
  takes any contact of a player body part as a player strike; the part's physics speed cannot tell a punch from being hit.
  **Fixed 2026-11-17:** `PlayerStrikeRule` + `HandSpeeds`: a hand or arm contact counts only when that hand's controller
  (the `PlayerHand` pose, local to the rig, measured over 0.05 s) reached *Body Hit Min Hand Speed* (General, 1.5 m/s, a guess)
  within 0.2 s before; feet and legs count as kicks; torso and head never. Log: `[Hits] contact by your RightHand not counted as a
  strike (hand 0.00 m/s)`. BasHarness `hit-npc by=hand` now swings the right controller pose at `swing=` m/s (default 3) for 0.12 s
  before the hit. Harness: all scenarios with hand hits PASS; `step3b` checks `swing=0` (no strike) and threshold 0 (counts);
  `step5a` now runs with Disarm Strike on and expects no disarm from the NPC's own swings.

## Parry, step 6: Counter Parry and Power Parry (2026-11-20)

Spec section 6. Own Mod Menu group `Parry` (category order 5), feature class `Features/Parry.cs`, rule `ParryRule` (Core).
- **Event:** the game's `EventManager.onCreatureAttackParry` (raised by `BrainModuleMelee.OnWeaponCollision` when an NPC
  attack hits a held weapon with impact² > 2; read 1.3.2). Only NPC (parried) vs player (parrying, item held by a player
  hand).
- **Measure:** `HandSpeeds.Peak(side of the parrying item's main hand, Parry Speed Window 0.2 s)`, the real controller's
  rig-local speed. Below *Counter Guard Speed* (1 m/s): `TooSlow`, the game's own parry. From it: counter. From *Power
  Parry Min Speed* (3 m/s): power parry. Both speeds are guesses. Not the weapon's speed: at the event the collision has
  already slowed the blade, and `impactVelocity` contains the NPC's swing.
- **Rule order** (`ParryRule.Decide`, unit-tested): Disabled (group off, or both parts off) → NotPlayerParry → TakenByClash
  → power (KeepsShield, NoWeapon, chance) → Counter / TooSlow. A power parry also staggers while Counter Parry is on; one
  that cannot disarm (shield kept, weapon gone, chance failed) still counters.
- **Counter stagger:** DMCS's own window (*Counter Stagger Duration* 1.5 s, `StaggerWindows`; `HitInfo.IsStaggered` counts
  it, so the hand hit and Twist Disarm work) plus `Creature.ForceStagger(away from the player, StaggerMedium)` in the next
  `ScriptUpdate`. Why the next frame: right after raising the event, `BrainModuleMelee` pushes the NPC itself
  (`TryPush(PushType.Parry)`: `BrainModuleHitReaction.StaggerCoroutine` with the Parry recoil or, by chance, a Riposte that
  attacks back at once). That push never sets `brain.currentStagger` (no `SetStagger` on the Parry path), so before step
  6 a parry never opened Disarm Strike. A stagger set before the push would be overridden by a riposte.
- **Power parry:** `DisarmStrike.DisarmAlong(npc, its hand, away from the player and up)`, at once in the event (fling,
  no-regrab lock, stand still, Disarm +15 adrenaline, catch bonus).
- **Clash of Might:** `Parry.Start` runs after `ClashOfMight.Start`, so the clash's handler sees the event first. The clash
  returns early on a parry at or above the power speed (`a power parry, not a clash`); a parry the clash took (lock with
  that NPC, or its Simple disarm) is `TakenByClash`.
- **Log:** `parry of HumanMale0's SwordShortCommon: an ordinary parry, TooSlow (hand 0.00 m/s (counter from 1.00, power
  from 3.00))`, `counter parry on HumanMale0: staggered for 1.50 s (…)`, `power parry on HumanMale0's SwordShortCommon:
  knocked away (…)`.
- **Harness** `step6-parry`: BasHarness `player-parry swing=<m/s>` sweeps the right controller sideways for 0.12 s before
  the event. It raises only the event, so the game's own parry push does not happen there.
- **Not verified:** real guard and hard-parry speeds in the headset; whether a player who waggles the sword gets counters
  all the time; how the counter's stagger meets the game's own parry push and riposte in a real fight.

## API verification

Legend: **V** = verified in the decompiled `ThunderRoad.dll` of game 1.3.2. **V+T** = verified and covered by a test. **A** = assumed / behaviour only provable in game. Line refs point into the decompiled source.

### Used in step 1

| API | Status | Evidence / note |
|---|---|---|
| `ThunderScript` (`ScriptLoaded(ModManager.ModData)`, `ScriptEnable/Disable/Update/FixedUpdate/LateUpdate/Unload`) | V | `ThunderScript.cs`. Created by `ModManager` via `Activator.CreateInstance(type, nonPublic:true)` → `ScriptLoaded(mod)` → `Enable()` (`ModManager.cs:1020-1040`). |
| `[ModOption]` named args `name, nameLocalizationId, tooltip, category, categoryOrder, order, valueSourceName` | V+T | `ModOption.cs`. |
| `[ModOptionButton]`, `[ModOptionDontSave]`, `[ModOptionApplyOnPress]` | V+T | `ModOptionButton.cs`, `ModOptionDontSave.cs`, `ModOptionApplyOnPress.cs` (sets `applyOnLoad=false`). |
| Static property as option target, default = getter value at load | V | `ModOption.ReadModOptionDefaultValue` reads the static field/property and matches it against the value list. |
| Persistence | V + **harness** | `ModOption.Apply` → `GameManager.options.SetModOption(modData.folderName, name, index, saveValue)`. Saved as **value index**, keyed by **(mod folder, option name)**. Saved values are applied **after** the ThunderScripts load (`ModManager.LoadAndRefreshCatalog` → `Options.ApplyModOptions`), so DMCS logs the effective settings at level load, not at script load. Harness: set + save, restart, values back. |
| Live apply | V + **harness** | Menu change → `UIArrowListModOption.OnIndexChanged` → `ModOption.Apply` → property setter. Harness applies the same call in a running level; the toggle and its log line follow at once. |
| Group label "Enabled" | V+T + **harness** | Option names must be unique (save key), so each toggle has a unique name (`Clash of Might Enabled`, …) and `nameLocalizationId = "Default.Enabled"`, which the menu displays instead (`UIArrowListModOption.InitValues`). That text exists in the game: `bas.jsondb → Texts/Text_English.json` group `Default`, id `Enabled` = "Enabled" (German: "Aktiviert"). Falls back to the unique name if missing. |
| Button = re-apply on every press | V + **harness** | `UIList.NextValue`: for `UIButtonList` subclasses with one value, the index wraps to 0 and `OnIndexChanged` → `Apply(0)` runs on each press. |
| Reset: `ModManager.ModData.modOptions`, `ModOption.defaultValueIndex`, `parameterValues`, `Apply(int)`, `uiComponent.Reload()` | V + **harness** (values) / **A** (redraw) | `ModManager.cs:150`, `ModOption.cs`, `UIList.Reload` = `InitValues + LoadValue(default) + Refresh`. Harness: "Reset 9 options to defaults." (step 2), values and members back at default. Whether an **open** menu page redraws is still a headset check. |
| `UnityEngine.Debug.Log` | V | `UnityEngine.CoreModule`. |
| Manifest format (`Name, Description, Author, ModVersion, GameVersion`) | V | Official wiki JSON-modding page and installed working mods (all use `"GameVersion": "1.0.0.0"`). |

Game quirk found: `UIButtonListModOption` feeds `nameLocalizationId` into the **tooltip** localisation, so button options must not set `nameLocalizationId` (enforced by a test).

### Used in step 2

| API | Status | Evidence / note |
|---|---|---|
| `EventManager.onCreatureHit`, `EventTime.OnEnd` | V + **harness** | `Creature.Damage(CollisionInstance)` raises it with `OnStart` before and `OnEnd` after damage (`Creature.cs:3558-3650`); a killing hit gets no `OnEnd`. DMCS reacts on `OnEnd`. |
| Attacker of a hit | V + **harness** | Held item: `damager.collisionHandler.item.handlers` (hands holding it). Thrown: no hand on it, `Item.lastHandler` = player hand. Spell projectile: an item with `ItemMagicProjectile` whose `lastHandler` is the casting hand (`SpellCastProjectile.cs:290`); other spells: `CollisionInstance.casterHand`. Body: `damager.collisionHandler.ragdollPart.ragdoll.creature`. Harness builds each of these four hits (held = a real `RagdollHand.Grab`); real physical contacts and real spell casts are headset checks. |
| `Locomotion.horizontalSpeed` | V + **harness** | `Locomotion.cs:445-454`: XZ magnitude of the physics velocity, updated each frame. Harness drives the player through the game's own `Locomotion.MoveWeighted` (the gamepad's call). |
| `Creature.ForceStagger(Vector3, PushBehaviour.Effect, RagdollPart.Type)` | V + **harness** | → `BrainModuleHitReaction.ForceStaggerBehaviour` → `SetStagger` (`StaggerFull` = 1 → `Brain.Stagger.Full`; `Destabilize` = 2 → ragdoll `Destabilized`). Harness: `Brain.currentStagger == Full`, resp. state `Destabilized`. |
| `Brain.currentStagger` = "is staggered" | V + **harness** | `None` / `LightAndMedium` / `Full`; back to `None` when the stagger coroutine ends. A knocked-down NPC has `None` + state `Destabilized`. |
| `Brain.AddNoStandUpModifier / RemoveNoStandUpModifier` | V + **harness** | `BrainModuleHitReaction.OnCycle` only calls `StandUp()` when `noStandupModifiers.Count == 0`. |
| `EventManager.onCreatureDespawn`, `onLevelUnload` | V | Release held NPCs (creatures are pooled) and clear windows. |
| Float slider values / default lookup | V+T | `ModOptionFloat.CreateArray` (`start + i*step`) and `ModOption.ReadModOptionDefaultValue` (`Equals`, falls back to index 0): a test checks every default is exactly one of its slider values. |
| Enum dropdowns | V (avoided) | `ReadModOptionDefaultValue` compares the boxed `int` values with the boxed enum default → never equal → default falls to the first entry. Strength is therefore a `string` option. |

Related game feature: the game has a skill `SkillCharger` (`ChargerBehaviour`) that, once the player has it, already pushes NPCs
on a hit while sprinting (`MaxPush`). DMCS does not depend on it. With both active a sprint hit would get the skill's push and
DMCS's stagger — derived from the code, not tested.

### Spec APIs for later steps (existence checked now, behaviour not yet)

| Spec reference | Status | What exists in 1.3.2 |
|---|---|---|
| `EventManager.onCreatureHit` | V | `CreatureHitEvent(Creature creature, CollisionInstance collisionInstance, EventTime eventTime)` |
| `EventManager.onCreatureAttackParry` (blocks/parries) | V | `CreatureParryEvent(Creature parriedCreature, Item parriedItem, Creature parryingCreature, Item parryingItem, CollisionInstance)` |
| `EventManager.onCreatureDisarm`, `onDeflect`, `onCreatureAttacking`, `onLevelLoad/Unload` | V | signatures in `EventManager.cs:24-58` |
| `CollisionInstance` | V | `impactVelocity`, `intensity`, `damageStruct` (`hitRagdollPart`, `damager`), `sourceColliderGroup`, `targetColliderGroup` |
| `RagdollPart.Type` | V | `Head, Neck, Torso, LeftArm, RightArm, LeftHand, RightHand, LeftLeg, …`. **No separate forearm type** — "include forearm" will map to `LeftArm/RightArm` (to verify in step 3 which bones that covers). |
| `RagdollPart.TrySlice()`, `sliceAllowed`, `isSliced` | V | dismemberment for step 4 |
| `Creature` | V | `isPlayer`, `brain`, `ragdoll`, `handLeft/handRight`, `isKilled`, `TryPush(PushType, Vector3, int, RagdollPart.Type)`, `ForceStagger(Vector3, BrainModuleHitReaction.PushBehaviour.Effect, RagdollPart.Type)` |
| Disarm | V (used in step 3, not yet in game) | `RagdollHand.grabbedHandle`, `TryRelease()` (= `UnGrab(false)`), `EventManager.InvokeDisarm(Creature, RagdollHand, Handle, CollisionInstance)`; `Brain.Load(string)` swaps brains at runtime (`Brain.cs:289`); AI picks items up via the behaviour-tree node `AI.Action.PickupItem` and draws via `DrawItem` — both end in a hand grab, which DMCS releases. |
| `Item`, `Handle` | V | `Item.mainHandler`, `Item.handlers`, `Item.collisionHandlers` → `CollisionHandler.OnCollisionStartEvent/OnCollisionStopEvent/OnCollidingEvent` (blade-lock contact timing for step 5) |
| Haptics | V | `RagdollHand.HapticTick(float, bool)`, `PlayHapticClipOver(AnimationCurve, float)` |
| Player locomotion | V | `Player.local`, `Player.locomotion`, `Locomotion.velocity`, `Locomotion.IsRunning` |
| `Time.timeScale` | A | Unity API; restore paths tested in step 4 |
| Dodge event | **A** | No dodge event found; spec allows approximation (step 4). |

## In-game tests (headless harness)

`tools/harness.sh` builds DMCS and runs `harness/*.txt` in the game, headless in the background, through
[BasHarness](https://github.com/Naragorn/BasHarness) (cloned as sibling folder `../BasHarness`; `BAS_HARNESS_DIR` overrides): Unity's mock headset instead of
SteamVR, straight into the Arena, only DMCS + the harness mod loaded, in the harness's own Proton prefix —
your install, mods, saves and options are not touched. Every run starts from a fresh prefix.
Results land in `artifacts/harness/<time>/<scenario>/summary.md` (with Player.log and screenshots).

| Scenario | Checks |
|---|---|
| `step1-a-menu` | mod + script load; effective settings line; the step-1 options with category, displayed title (`Enabled` from the game's localisation), type, interaction, save flag, tooltip; defaults; Debug Logging on → toggle change logged live; Debug Logging off → toggle still switches, no debug line; Reset To Defaults restores values and members; screenshot |
| `step1-b-persist-set` | set Debug Logging on, Disarm Strike off, Charge Stagger preset Realistic then Speed Threshold 6 and Strength Knock Down |
| `step1-c-persist-check` | new game start: all values back (menu value and live member), preset row shows Custom and the rest of Realistic (chance 75 %, window 0.15 s) survived — the preset row did not reset them on load; Reset To Defaults → Gamey |
| `step2-charge-stagger` | all nine settings with type/slider/Gamey defaults; Realistic sets its six values, one change → Custom, back → Realistic, Gamey restores; NPC (`HumanDummy` brain) beside the player; standing / walking (2.8 m/s) → TooSlow; sprint (6 m/s) → `Brain.currentStagger = Full`; second charge hit → knocked down and held, then up; hit 0.2 s after stopping (2.3 m/s) counts through the 0.5 s window; threshold 8 → TooSlow; min swing 6 → 5 m/s SwingTooWeak, 7 m/s staggers; chance 0 % → ChanceFailed; thrown, spell, held weapon → staggered (source logged); thrown/spells off → RangedNotCounted; second-hit switch off → AlreadyStaggered; Knock Down 4 s → down 4.2 s, control 0.5 s → up after 1.4 s; feature off → none (all live) |
| `step3-disarm-strike` | Settings and defaults (Body Hit Drop switched off: this scenario checks the hand rule); fighting NPC with a dagger in each hand; not staggered → NotStaggered; forearm off → no disarm; staggered + hand hit → both drop, brain `HumanDummy`, a re-grabbed dagger is released, after 4 s the own brain is back and it can hold again; drop-both off → only the hit hand drops, the other keeps its dagger; charge hit then hand hit → the charge hit staggers without disarming, the next one disarms; feature off → none |
| `step4e-adrenaline-dodge-catch` | 4e settings and defaults; a disarmed dagger grabbed at once → `+10 Catch`; grabbed after it landed → none; the game's attack event (Attack → End) while running → `+10 Dodge`; standing → TooSlow; Attack → Cancel → none; Adrenaline off → nothing |
| `step5a-clash-telegraph` | Clash settings and defaults; a real sword NPC: big swings flash, plain ones and thrusts do not, no slow-motion without a blade in hand (`BladeTooFar`); Min Wind Up 0.8 → no flash; feature off → nothing |
| `step3b-body-hit-drop` | Settings and defaults; body hit without stagger → nothing even at 100 %; 0 % + 100 % per extra hit → first body hit fails (sparks, haptic pulse logged), the second drops the dagger (flung 3 m/s); the count starts over with the next stagger; a buckler in the other hand stays while the dagger drops; base 0 % + female 100 % on a male NPC → 0 %, on a female NPC → drops; smaller 100 % with Smaller By 50 cm on an adult NPC → 0 %; Body Hit Drop off / Disarm Strike off → nothing drops (live) |
| `step6-parry` | Parry settings and defaults; a held guard → `TooSlow`, nothing; the controller swept at 1.5 m/s → counter, NPC staggered, keeps its sword; Power Parry off + 5 m/s → only a counter; Parry off → nothing; 5 m/s → power parry, sword knocked away, staggered; Clash On: a slow block at the star locks (`TakenByClash`), a 5 m/s block at the star is a power parry, no lock |

The harness hits the NPC through `Creature.Damage` with the player's hand as damager and `pushLevel = -1`, so the
game's own hit reaction does not push and any stagger comes from DMCS.

Not covered by the harness: how the mods page *looks* in the headset (nobody opens the book), a real arm-swing sprint,
a real physical weapon hit (held-weapon attribution) and how the stagger *feels*.

## In-game test log

| Date | Step | Result |
|---|---|---|
| 2026-11-20 | 3c | Twist Disarm reworked: only a staggered enemy (Disarm Requires Stagger), chance 100 %, a slow touch starts no cooldown. Unit tests 615 **PASS**, BasHarness 149 **PASS**. Harness: full suite 17/17 **PASS** (new `step3d-twist-disarm-staggered`, 4 of 4 before that). Before (red, 65a542a): an unstaggered 981°/s twist disarmed; after: `NotStaggered`. Films with side, eye and hand views. BasHarness gained `frame-rate`, `player-strike-weapon phase=ready/strike` and `depth`, a gentle return after a strike, and the hand view. |
| 2026-11-18 | 3c | Twist Disarm. Unit tests 611 **PASS**, BasHarness 127 **PASS**. Harness `step3c-twist-disarm` (new BasHarness verb `player-strike-weapon [twist=]`: real blade-on-blade physics against a standing `HumanDummy` with a sword) **PASS** three times in a row and in the full suite (16/16): no twist → `TooSlow (roll 0°/s, hit 2.49–2.52 m/s)`, NPC keeps its sword; 900°/s → measured `roll 904–926°/s`, disarmed; Twist off → nothing. Before (red): the same 900°/s strike on 31f38bd leaves the sword in the NPC's hand (whether the blades touched there cannot be read: that build logs no contacts). First runs found: a straight sweep from the player's blade to the NPC's often hit its body first → the verb now comes at the blade from outside the body; the sweep can knock the player's own sword out of the mock hand → each strike grabs a fresh one. |
| 2026-11-18 | 5c | Clash modes (Off/Simple/On, default Simple), rounds, push measure, win/lose, sounds. Unit tests 591 **PASS**, BasHarness 125 **PASS**. Harness: full suite 14/15 **PASS**; the 15th, step5c, failed there in its first section 4 (controller left 0.8 m out, see 5c), which was then replaced by `player-release`; step5c then **PASS** three times in a row (DMCS code unchanged since the suite run). Before (red): the same step5c on ff931db fails at its first check (no `Clash of Might Mode`); the old lock just ended (`TimeUp`) with the enemy still armed. Found on the way: in the harness nothing holds the heavy weapon back (the blades never touch), so the hand catches up with a moved controller within ~0.4 s; a controller beyond arm's length is read differently by DMCS and BasHarness (open). |
| 2026-11-17 | 5a | Clash telegraph (big swing, flash, slow-motion). Harness: all thirteen scenarios **PASS** (`step3` on a rerun: the known "may re-arm" timeout, passed twice after). Unit tests 499 **PASS**. Before: a probe measured all 68 attacks' wind-ups and found that the wind-up moves in another plane than the strike (so the angle is read at the strike start) and that an NPC can be frozen mid-swing (`animator.speed = 0`). First 5a runs found: the NPC's random loadout (a mace) and the player dying after a minute made the scenario flaky → BasHarness `container=` and `player-invincible`; Disarm Strike disarmed the NPC on its own swings (open finding). |
| 2026-11-17 | 4e | Dodge and catch bonus. Harness: all twelve scenarios **PASS**; unit tests 445 **PASS**. The first 4e run found two bugs: dodges were still judged with Adrenaline off (all handlers now check the toggle), and the per-frame head speed read 19.85 m/s from a 2.5 m/s run (now measured over 0.1 s, then 5.05 m/s while sprinting up). Also: running into the test NPCs charge-staggered them and filled the meter, so the scenario turns away and switches Charge Stagger off. |
| 2026-11-17 | 4d | DMCS 4 vignette (tuned to Dark Messiah's frame statistics), style switches in game (frames, sounds and Dark Messiah's files built/read in the background: the first in-game build took 0.4 s on the main thread), `Adrenaline.AddExternal` for other mods. Harness: all eleven scenarios **PASS**, `step4d-b` switches DMCS 1–4 and Dark Messiah (found on this machine under Proton at `Z:\run\media\…\Programs\Steam`, read in 115 ms). Unit tests 406 **PASS**. |
| 2026-11-17 | 4d | Style switches: every built sound and vignette version plus Dark Messiah's own files (from the player's install). Unit tests (incl. the readers on the real Dark Messiah files of this machine): 390 **PASS**. Harness `step4d-b-adrenaline-styles`: see the next row. |
| 2026-11-17 | 4d | Ready sound, second pass (user: the thunder was too harsh and not like DM): the crackle bursts are gone; the roar is now dense and dark (falls off above ~700 Hz), swells in over 60 ms, holds to 0.4 s; the rumble is quieter. Unit tests only. |
| 2026-11-17 | 4d | Ready sound: thunder crack at the start added (the first version had rumble, hiss and ring only; Dark Messiah's file has a roar across the mids in the first 0.3 s). Unit tests only; the game-side code did not change. The sound tests now window their Goertzel measurement (without it the rumble leaked into every frequency, so the hiss test passed by luck). |
| 2026-11-17 | 4d | Signals and vignette. Shader probe first (URP Particles/Unlit found). Harness: all ten scenarios **PASS** (step3 on a rerun: one timeout on "may re-arm" after the combo disarm, passed on the rerun). First runs found: frames took 2 s to build (fixed: per-streak pixel boxes, same output, built at level load); at 60° to the edge the streaks sat outside the mirror view → *Vignette Size* slider, 50°. |
| 2026-11-16 | 1 | Harness: `step1-a-menu`, `-b-persist-set`, `-c-persist-check` **PASS** (70 s). First run found that saved values apply after script load; the load banner claimed default states — fixed (Settings line at level load). |
| 2026-11-16 | 3b | Body Hit Drop, shields, fling. Harness: all six scenarios **PASS** (with the earlier female/smaller toggles; the chance-per-type version that replaced them is unit-tested, its scenario changes **not run**). Found on the way: a "grab the handle" steal is pointless, because the game itself gives a dagger or short sword to whoever grabs it; an NPC holding a buckler did not stagger from the push=1 test hit; a fighting NPC picks up shields and maces lying around during waits → step3b uses `HumanDummy`, step3 hands the left dagger over right before the hits. Not checked: the height check on a really smaller NPC (unit-tested only), sparks and pulse in the headset. |
| 2026-11-16 | 3 | Harness: all five scenarios **PASS** (3 min). First run found: with Drops Both Hands off, the no-regrab check also emptied the hand that was not hit → only the disarmed hands are kept empty now. Also: a fighting brain may put its left-hand item away for a rock throw (`BrainModuleThrow`), so the scenario gives the left dagger right before the hit. |
| 2026-11-16 | 2b | Presets (Gamey/Realistic/Custom), chance, min swing speed, thrown/spells, second hit knocks down. Harness: all four scenarios **PASS** (2.5 min). |
| 2026-11-16 | 2 | Harness: all four scenarios **PASS** (2 min). Found on the way: a fighting NPC (`HumanMedium`) lands real hits whose own pushes blur the result → scenario uses `HumanDummy`; runs into a wall stop the player → runs go back and forth. |
