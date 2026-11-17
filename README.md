# DMCS — Dark Messiah Combat Systems

Dark Messiah-style combat mechanics for Blade & Sorcery.

- **Clash of Might** — blade lock decided by how hard you push back with controller micro-movements; loser gets disarmed.
- **Disarm Strike** — hit a staggered enemy's hand to make them drop their weapon.
- **Charge Stagger** — attacks while sprinting stagger enemies.
- **Adrenaline** — meter builds through combat; when full, a short window for a guaranteed critical (dismemberment, slow-mo, knockback, screen effect).

Every feature can be toggled and tuned in the in-game Mod Menu. Full spec: `CLAUDE.md`.

## Status

| Build step | State |
|---|---|
| 1. Project setup, SDK check, mod loads, Debug Logging, Mod Menu skeleton | **Done. Unit tests + in-game harness PASS (2026-11-16).** Menu look in the headset not checked. |
| 2. Charge Stagger | **Done. Unit tests + in-game harness PASS (2026-11-16).** Feel in the headset (real sprint + real swing) not checked. |
| 3. Disarm Strike | **Built, unit tests PASS, in-game harness PASS (2026-11-16).** Not checked in the headset yet. |
| 3b. Body Hit Drop, chances per NPC type, shields, fling | **Built, unit tests + in-game harness PASS (2026-11-16, chance-per-type version).** Not checked in the headset; Twist Disarm not built (with step 5). |
| 4. Adrenaline | **4a (meter), 4b (critical / fatality, chest-stab finish), 4c (kneel and execution) and 4d (signals: ready sound, heartbeat, haptics, hand glow, Dark Messiah-style vignette) built, unit tests + in-game harness PASS (2026-11-16/17).** Block/parry and damage taken are unit-tested only. Nothing checked in the headset (sound, haptics and the vignette's look need it). 4e (dodge/catch) not built; design in `CLAUDE.md` 4b. |
| 5. Clash of Might | not started |

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
```

Game path defaults to `~/.local/share/Steam/steamapps/common/Blade & Sorcery`; override with `-p:BasGameDir=...` or env `BAS_GAME_DIR`.

### Layout

| Path | Purpose |
|---|---|
| `src/DMCS.Core` | Pure logic (no game references): logger gate, feature toggles, reset logic, category names, Charge Stagger rule, speed history, stagger windows, slider values. Compiled into `DMCS.dll`. |
| `src/DMCS` | The mod: `ThunderScript` entry point, Mod Menu options, one class per feature. Target `net472` (same as working B&S script mods, e.g. HealthRegen.dll). |
| `tests/DMCS.Tests` | xUnit. Core logic flows + a metadata test that inspects the built `DMCS.dll` the way the game's ModManager does (via `MetadataLoadContext`, no game code executed). Needs the game install. |

## Mod Menu

| Category | Option | Type | Range | Default |
|---|---|---|---|---|
| General | Debug Logging | toggle | | off |
| General | Reset To Defaults | button | | – |
| Clash of Might | Enabled | toggle | | on |
| Disarm Strike | Enabled | toggle | | on |
| Disarm Strike | Disarm Requires Stagger | toggle | | on |
| Disarm Strike | Disarm Include Forearm | toggle | | off |
| Disarm Strike | Disarm Drops Both Hands | toggle | | on |
| Disarm Strike | Disarm Chance | slider | 0–100 %, step 5 | 100 % |
| Disarm Strike | Disarm No Regrab Time | slider | 2–60 s, step 2 | 10 s |
| Disarm Strike | Disarm Stays Permanent | toggle | | off |
| Disarm Strike | Disarmed Stand Still | toggle | | on |
| Charge Stagger | Enabled | toggle | | on |
| Charge Stagger | Charge Stagger Preset | dropdown | Gamey, Realistic, Custom | Gamey |
| Charge Stagger | Charge Stagger Chance | slider | 0–100 %, step 5 | 100 % |
| Charge Stagger | Charge Speed Threshold | slider | 1–8 m/s, step 0.25 | 4 m/s |
| Charge Stagger | Charge Speed Window | slider | 0–1 s, step 0.05 | 0.5 s |
| Charge Stagger | Charge Min Swing Speed | slider | 0–10 m/s, step 0.25 | 1 m/s |
| Charge Stagger | Charge Thrown And Spells Count | toggle | | on |
| Charge Stagger | Charge Stagger Strength | dropdown | Light, Medium, Full, Knock Down | Full |
| Charge Stagger | Charge Stagger Duration | slider | 0.5–5 s, step 0.25 | 2.5 s |
| Charge Stagger | Charge Second Hit Knocks Down | toggle | | on |
| Adrenaline | Enabled | toggle | | on |
| Adrenaline | Adrenaline Meter Max | slider | 50–300, step 10 | 100 |
| Adrenaline | Adrenaline Window | slider | 2–20 s, step 1 | 6 s |
| Adrenaline | Adrenaline Decay | toggle | | off (for testing) |
| Adrenaline | Adrenaline Decay Per Second | slider | 0–20, step 0.5 | 2 |
| Adrenaline | Adrenaline Decay Delay | slider | 0–30 s, step 1 | 0 s |
| Adrenaline | Adrenaline Per Hit | slider | 0–50 | 5 |
| Adrenaline | Adrenaline Per Block Or Parry | slider | 0–50 | 10 |
| Adrenaline | Adrenaline Per Damage Taken | slider | 0–50 | 5 |
| Adrenaline | Damage Taken Lowers Adrenaline | toggle | | on |
| Adrenaline | Adrenaline Per Charge Stagger | slider | 0–50 | 15 |
| Adrenaline | Adrenaline Per Disarm | slider | 0–50 | 15 |
| Adrenaline | Critical Time Mode | dropdown | Hyperfocus, Slow Time | Hyperfocus |
| Adrenaline | Critical Hyperfocus Time Scale | slider | 0.05–0.5, step 0.05 | 0.1 |
| Adrenaline | Critical Hyperfocus Duration | slider | 0.5–10 s, step 0.5 | 3 s |
| Adrenaline | Critical Slow Time Scale | slider | 0.25–0.75, step 0.25 | 0.5 |
| Adrenaline | Critical Slow Time Duration | slider | 0.5–10 s, step 0.5 | 3 s |
| Adrenaline | Critical Needs Hard Swing | toggle | | on |
| Adrenaline | Critical Min Swing Speed | slider | 0–10 m/s, step 0.25 | 3 m/s |
| Adrenaline | Critical Thrown And Spells | toggle | | off |
| Adrenaline | Critical Kills | toggle | | on |
| Adrenaline | Critical Dismember | toggle | | on |
| Adrenaline | Critical Knockback | slider | 0–20 m/s, step 1 | 8 m/s |
| Adrenaline | Critical Battle Cry | toggle | | on |
| Adrenaline | Chest Stab Kneels | toggle | | on |
| Adrenaline | Execution Window | slider | 1–15 s, step 1 | 5 s |
| Adrenaline | Kneel And Execution | toggle | | on |
| Adrenaline | Kneel After No Hit | slider | 0.5–5 s, step 0.5 | 2 s |
| Adrenaline | Kneel After Light Hits | slider | 1–10 | 2 |
| Adrenaline | Kneel Death Chance | slider | 0–100 %, step 5 | 0 % |
| Adrenaline | Adrenaline Ready Sound | toggle | | on |
| Adrenaline | Adrenaline Ready Sound Style | dropdown | DMCS 1, DMCS 2, DMCS 3, Dark Messiah | DMCS 3 |
| Adrenaline | Adrenaline Ready Sound Volume | slider | 0–1, step 0.05 | 0.6 (Dark Messiah's) |
| Adrenaline | Adrenaline Heartbeat | toggle | | on |
| Adrenaline | Adrenaline Heartbeat Sound | dropdown | Blade & Sorcery, Dark Messiah | Blade & Sorcery |
| Adrenaline | Adrenaline Heartbeat Volume | slider | 0–1, step 0.05 | 0.5 (Dark Messiah's) |
| Adrenaline | Adrenaline Haptics | toggle | | on |
| Adrenaline | Adrenaline Haptics Strength | slider | 0–1, step 0.05 | 0.5 |
| Adrenaline | Adrenaline Weapon Glow | toggle | | on |
| Adrenaline | Adrenaline Vignette | toggle | | on |
| Adrenaline | Adrenaline Vignette Style | dropdown | DMCS 1, DMCS 2, DMCS 3, Dark Messiah (picking a DMCS version sets Streaks, Length, Hue) | DMCS 3 |
| Adrenaline | Adrenaline Vignette Intensity | slider | 0–1, step 0.05 | 1 |
| Adrenaline | Adrenaline Vignette Hue | slider | 0–40°, step 2 | 6° (red) |
| Adrenaline | Adrenaline Vignette Streaks | slider | 2–100 | 70 |
| Adrenaline | Adrenaline Vignette Streak Length | slider | 0.1–0.6, step 0.05 | 0.4 |
| Adrenaline | Adrenaline Vignette Size | slider | 30–70°, step 5 | 50° |
| Adrenaline | Adrenaline Vignette Frame Rate | slider | 5–60 fps, step 5 | 25 fps (Dark Messiah's) |
| Adrenaline | Adrenaline Vignette Flare | toggle | | on |

Feature settings are added to their group as each feature is built (spec build order). Clash of Might has no gameplay effect yet. Adrenaline fills its meter, opens the window, signals it (4d), does the critical (4b) and the kneel/execution (4c); dodge and catch gains are not built yet.

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
  angle from the view centre to the frame's edge; it flares on the critical (*Vignette Flare*).
- **Versions to compare** (*Ready Sound Style*, *Vignette Style*, *Heartbeat Sound*): every version DMCS built stays
  selectable, byte for byte as built (checked against the old commits): sound DMCS 1 (rumble, hiss, ring), DMCS 2 (+ a
  crackling thunder crack), DMCS 3 (+ a dark thunder roar, default); vignette DMCS 1 (flames), DMCS 2 (even radial
  streaks), DMCS 3 (patchy radial streaks, default). Picking a DMCS vignette sets *Streaks*, *Length* and *Hue* to how
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
| `step3b-body-hit-drop` | Settings and defaults; body hit without stagger → nothing even at 100 %; 0 % + 100 % per extra hit → first body hit fails (sparks, haptic pulse logged), the second drops the dagger (flung 3 m/s); the count starts over with the next stagger; a buckler in the other hand stays while the dagger drops; base 0 % + female 100 % on a male NPC → 0 %, on a female NPC → drops; smaller 100 % with Smaller By 50 cm on an adult NPC → 0 %; Body Hit Drop off / Disarm Strike off → nothing drops (live) |

The harness hits the NPC through `Creature.Damage` with the player's hand as damager and `pushLevel = -1`, so the
game's own hit reaction does not push and any stagger comes from DMCS.

Not covered by the harness: how the mods page *looks* in the headset (nobody opens the book), a real arm-swing sprint,
a real physical weapon hit (held-weapon attribution) and how the stagger *feels*.

## In-game test log

| Date | Step | Result |
|---|---|---|
| 2026-11-17 | 4d | Style switches: every built sound and vignette version plus Dark Messiah's own files (from the player's install). Unit tests (incl. the readers on the real Dark Messiah files of this machine): 390 **PASS**. Harness `step4d-b-adrenaline-styles`: see the next row. |
| 2026-11-17 | 4d | Ready sound, second pass (user: the thunder was too harsh and not like DM): the crackle bursts are gone; the roar is now dense and dark (falls off above ~700 Hz), swells in over 60 ms, holds to 0.4 s; the rumble is quieter. Unit tests only. |
| 2026-11-17 | 4d | Ready sound: thunder crack at the start added (the first version had rumble, hiss and ring only; Dark Messiah's file has a roar across the mids in the first 0.3 s). Unit tests only; the game-side code did not change. The sound tests now window their Goertzel measurement (without it the rumble leaked into every frequency, so the hiss test passed by luck). |
| 2026-11-17 | 4d | Signals and vignette. Shader probe first (URP Particles/Unlit found). Harness: all ten scenarios **PASS** (step3 on a rerun: one timeout on "may re-arm" after the combo disarm, passed on the rerun). First runs found: frames took 2 s to build (fixed: per-streak pixel boxes, same output, built at level load); at 60° to the edge the streaks sat outside the mirror view → *Vignette Size* slider, 50°. |
| 2026-11-16 | 1 | Harness: `step1-a-menu`, `-b-persist-set`, `-c-persist-check` **PASS** (70 s). First run found that saved values apply after script load; the load banner claimed default states — fixed (Settings line at level load). |
| 2026-11-16 | 3b | Body Hit Drop, shields, fling. Harness: all six scenarios **PASS** (with the earlier female/smaller toggles; the chance-per-type version that replaced them is unit-tested, its scenario changes **not run**). Found on the way: a "grab the handle" steal is pointless, because the game itself gives a dagger or short sword to whoever grabs it; an NPC holding a buckler did not stagger from the push=1 test hit; a fighting NPC picks up shields and maces lying around during waits → step3b uses `HumanDummy`, step3 hands the left dagger over right before the hits. Not checked: the height check on a really smaller NPC (unit-tested only), sparks and pulse in the headset. |
| 2026-11-16 | 3 | Harness: all five scenarios **PASS** (3 min). First run found: with Drops Both Hands off, the no-regrab check also emptied the hand that was not hit → only the disarmed hands are kept empty now. Also: a fighting brain may put its left-hand item away for a rock throw (`BrainModuleThrow`), so the scenario gives the left dagger right before the hit. |
| 2026-11-16 | 2b | Presets (Gamey/Realistic/Custom), chance, min swing speed, thrown/spells, second hit knocks down. Harness: all four scenarios **PASS** (2.5 min). |
| 2026-11-16 | 2 | Harness: all four scenarios **PASS** (2 min). Found on the way: a fighting NPC (`HumanMedium`) lands real hits whose own pushes blur the result → scenario uses `HumanDummy`; runs into a wall stop the player → runs go back and forth. |
