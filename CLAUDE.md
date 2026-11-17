# DMCS — Dark Messiah Combat Systems (Blade & Sorcery mod)

Implementation spec for this repo. One mod only; the Achilles Tendon mod lives in its own repo.

Blade & Sorcery (VR) mod built with the official B&S SDK (Unity + C#, `ThunderRoad` namespace).

> **Important for the implementer:** API names below (`ThunderScript`, `EventManager.onCreatureHit`, `CollisionInstance`, `RagdollPart.Type`, `Creature`, `Item`, `Handle`, etc.) are best-guess references. **Verify every API against the current SDK version before using it** (official B&S modding wiki / SDK docs / decompiled `ThunderRoad.dll`). If an API differs, adapt — don't invent.

---

## General requirements

- Target the current B&S version (check the SDK version first and note it in a README).
- Each mod is its own project/folder with its own manifest, so they can be installed independently.
- Mechanics apply **player vs. NPC only** (no NPC vs. NPC).
- Keep each mechanic in its own class so it can be enabled/disabled independently at runtime.

## In-game Mod Menu (required)

**Every feature must have an on/off toggle and all of its settings exposed in the in-game Mod Menu.** No magic numbers in code; every value listed under "Config" in each feature section below is a Mod Menu setting.

- Use the SDK's built-in mod options system (in recent versions: `[ModOption]` attributes on static fields/properties in a `ThunderScript` — **verify the exact attribute, parameters and supported types against the current SDK**).
- Group settings per feature (category = feature name), e.g. `Clash of Might`, `Disarm Strike`, `Charge Stagger`, `Adrenaline`.
- Each group starts with `Enabled` (bool toggle), followed by its settings.
- Use sliders with sensible min/max/step for numbers, dropdowns for enums, toggles for bools. Give every option a short tooltip/description.
- Changes must apply **live** without restarting the game or reloading the level. Toggling a feature off mid-fight must cleanly stop it (e.g. restore `Time.timeScale`, remove screen effects, stop bleed-out coroutines if desired).
- Settings must persist between sessions (via the mod options system's own saving, or a JSON fallback if not supported).
- Global extras per mod: `Debug Logging` toggle (logs clash start/end, stagger, disarm, meter changes to the console) and a `Reset to Defaults` option if the SDK supports buttons.
- Sub-effects get their own toggles too, e.g. Adrenaline: `Slow-Mo`, `Dismemberment`, `Knockback`, `Screen Distortion`, `Heartbeat Sound`, `Vignette`, `Weapon Glow` each individually switchable.

---

Dark Messiah-style combat mechanics for Blade & Sorcery. Four mechanics that feed into each other.

**Intended combo flow:**
Sprint → Charge attack → Stagger → Hit hand → Disarm → (Clash if enemy re-arms) → Win clash → Adrenaline critical

## 1. Clash of Might (blade lock)

**Redesigned with the user 2026-11-17** (replaces the first draft "any blade contact longer than 0.3 s + controller micro-movements"). **5a built 2026-11-17** (flow steps 1–3: big swing, flash, slow-motion; see docs/IMPLEMENTATION.md); 5b (block at the flash, freeze, weight, push measure + headset diagnostic log) and 5c (outcomes, Mod Menu rest) not built.

**Dark Messiah's own clash (read 2026-11-17):** both sides do a power attack at once → the blades lock (`sword_versus_sword`, a click counter `m_iVersusClick`, win/lose animations `ACT_VERSUS_*_OUT_WIN/_OUT_LOOSE` in DM's `server.dll`); the player mashes to push back, the loser is knocked back and briefly stunned ([mthec 2009](https://mthec.wordpress.com/2009/03/23/dark-messiah-might-and-magic-part-one/)). With the Disarming skill (melee level 2) the winner disarms the loser ([GameSpot walkthrough](https://www.gamespot.com/articles/dark-messiah-of-might-and-magic-walkthrough/1100-6160412/)). DMCS's lose outcome is harsher than DM's (user's choice).

**Flow**
1. **Big swing:** an NPC melee attack with a long wind-up. B&S has no power-attack class: at `AttackStage.WindUp` the running attack is `BrainModuleMelee.attack` (`AttackMotion`; `lastAttack` is the one before); its real wind-up in seconds is `animationClip.length × start / (animationSpeed × brain speed)` (`AttackMotion.GetRealTime`). A big swing = real wind-up ≥ `clashMinWindUp` (value from the probe that measures all of the game's attacks; the clip lengths are not in the catalog JSON). Thrusts do not count.
2. **Flash:** a short bright glint at the middle of the NPC's blade (middle of its slash damager) = the signal. `clashOnlySwords` (default on): only swords; off: every blade with a slash damager.
3. **Auto slow-motion** (`clashSlowMo`, default on, scale `clashSlowMoScale` 0.3) when, at the start of the strike (the first physics frames of `AttackStage.Attack`, see the probe), all hold: the NPC is within its swing reach of the player; the player's blade is within `clashBlockReach` (~0.6 m, guess) of the flash point; and the player's blade is roughly at a right angle to the swing: perpendicular to the swing plane (the plane of the NPC blade's axis and its motion), within `clashBlockAngleTolerance` (very generous, default 45°). Skipped when another slow-motion runs (as the critical). Ends when the clash starts, or at the end of the swing.
4. **Block at the flash point:** the game reports the NPC attack hitting the player's weapon (`EventManager.onCreatureAttackParry`, with the contact point). Contact within `clashPointTolerance` of the flash point and the same angle check → clash. Anything else = an ordinary parry (+10 Adrenaline as now), no clash.
5. **Clash:** DMCS holds the NPC in its swing pose (AI hold as the kneel in 4c; risk, see the probe) and makes the player's weapon heavy (mass ×`clashWeaponWeight` while it lasts; B&S's own weight handling makes it lag behind the controller), so the player cannot just push through.
6. **Push measure (a), decided:** how far the player's real controller is ahead of the lagging weapon towards the NPC = push strength; accumulated over time against `npcResistance`. Win when it is reached; lose after `clashMaxDuration`.
7. **Win:** `clashWinDisarms` (default on; later a buyable Body-tree power) disarms through the Disarm Strike path (fling, no-regrab lock, catch bonus); the NPC is staggered; +25 Adrenaline (clash win); with `clashWinKneelChance` (default 25 %) it also kneels and the execution window of 4c opens.
8. **Lose:** `clashLoseOutcome` dropdown `Disarm + Hit` (default), `Disarm`, `Hit`, `None`; the hit is `clashLoseDamage` (default 20; the player has 100 health, `Creature_PlayerDefault*.json`).
- Feedback during the clash: haptics, sparks and a grinding sound at the contact.
- Flash sound (user 2026-11-17, built): *Clash Flash Sound* (Off, DMCS 1; DMCS 1 modelled on Sekiro's perilous-attack cue, measured in a recording, Sekiro's files are encrypted and never read or shipped) and *Clash Flash Game Sound* (Off or one of three B&S catalog effects), any combination, plus a volume slider. User 2026-11-17: DMCS 1 is usable, the other three were dropped; make three new ones at the end of step 5.

**Harness probe 2026-11-17** (temporary probe code, not committed; NPC `HumanMale`/`HumanMedium` with `SwordShortCommon` attacking the standing player, 2 runs, 21 swings):
- Real wind-up (to `start`) of all 68 attack motions in the three melee stances: 0.13–0.77 s; time to the hit (`peak`) 0.33–1.37 s. In game the Attack stage came on time (e.g. 0.500 s computed, 0.489 s measured). A wind-up threshold of ~0.4 s splits the plain swings (0.13–0.37 s) from the overhead, counter, lunge and step swings and most dual-wield swings (0.40–0.77 s); the exact value is a slider.
- The wind-up does **not** move the blade in the strike plane: angle between the two planes 15–88° (mostly 45–88°, 14 swings). So the angle check reads the strike plane in the first physics frames of `AttackStage.Attack`; the flash and the reach checks run from the wind-up on.
- Freezing works: `creature.animator.speed = 0` mid-swing kept the sword in place for 3 s (it settled 4–28 cm in the first 0.25 s, then stayed), held, alive, still `isAttacking`; the game's End event still came on time; after `speed = 1` the NPC fought on normally. Whether the frozen blade pushes back against the player's blade is not tested (the harness player cannot press a blade).
- Weight: the player's sword at mass ×5/×20/×50 sagged at the tip by 1/5/11 cm while held still; how heavy that feels when pushing is a headset check. Alternative if it is too weak: the player's arm joint force (`Creature.AddJointForceMultiplier` < 1, as the Hyperfocus buff uses > 1).
- The controller-ahead-of-hand offset cannot be measured headless (the mock headset's hand target does not move); `PlayerHand.grip` (target, driven by the controller's `TrackedPoseDriver`) vs `RagdollHand` position is the candidate pair, to be checked in the headset with a diagnostic log.

**Config:** `clashOnlySwords`, `clashMinWindUp`, `clashSlowMo`, `clashSlowMoScale`, `clashBlockReach`, `clashBlockAngleTolerance`, `clashPointTolerance`, `clashWeaponWeight`, `npcResistance`, `clashMaxDuration`, `clashWinDisarms`, `clashWinKneelChance`, `clashLoseOutcome`, `clashLoseDamage`, `hapticsStrength`.

## 2. Disarm Strike

**Behavior**
- If the player hits an NPC who is currently **staggered**, and the hit `RagdollPart` is a hand (or forearm, configurable), the NPC drops its held weapon(s).
- Disarmed NPCs **just stand there** (no fleeing, no fist fighting, no picking weapons back up unless re-armed by game logic).

**Implementation hints**
- Hook the creature-hit event, check `targetCreature` stagger state and the hit part type (left/right hand).
- Release the held item from the corresponding hand.
- Prevent the AI from re-grabbing weapons for `disarmedNoRegrabTime` (or permanently, config).

**Config:** `includeForearm`, `disarmedNoRegrabTime`, `requireStagger` (default true).

### 2b. Disarm Strike extensions (designed 2026-11-16)

- **Body Hit Drop** (built, default on): a player hit anywhere but the hand (forearm counts as body while `includeForearm` is off) on a staggered NPC makes it drop its weapon(s) with `bodyHitDropChance` (default 25 %). Every further body hit in the same stagger that did not drop adds `dropChancePerExtraHit` (default 25 %), so 25 / 50 / 75 / 100 %. The count starts over with the next stagger.
- A body hit that does not drop signals the loosening grip: sparks at the held weapon (`loosenedGripSpark`) and a haptic pulse in the hitting hand (`loosenedGripHaptics`).
- A knocked-out weapon flies off in the hit's direction (`disarmFlingForce`, m/s, 0 = just drops). Applies to hand hits and body hits.
- Shields stay in the hand (`shieldsCanBeDisarmed`, default off), as the game's own hand-hit disarm keeps them.
- After every disarm path: the same no-regrab lock as Disarm Strike.
- Chances per NPC type for Body Hit Drop (built): `femaleChance` (`CreatureData.gender == Female`) and `smallerChance` (NPC shorter than the player by at least `smallerMargin` cm, default 0; `Creature.GetHeight`), both default 25 % = the base chance. The first-hit chance is the highest of base and the fitting type chances; the extra-hit bonus adds to it. Base 0 % + female 25 % = only female NPCs.
- Dropped: a Threat mod (HuJohner, Nexus 6830) surrender link, as it cannot be tested yet (2026-11-16).
- The existing hand-hit rule stays: a hit on a staggered NPC's hand disarms (`disarmChance`, default 100 %); forearm only with `includeForearm` (default off).
- Dropped design: "grab the handle to take the weapon while staggered". The base game already hands a weapon with a short handle (dagger, short sword) to whoever grabs it, staggered or not (`Handle.OnGrab` releases all other holders when `axisLength == 0`; seen in a BasHarness run 2026-11-16). A chance on top would need DMCS to block the game's own grab.

**Not built yet:**
- **Twist Disarm** (default on, chance default 50 %, no stagger needed): the player's weapon hits the NPC's **weapon** (blade on blade) while the blade rolls around its own long axis (wrist twist) faster than `twistMinRollSpeed` (deg/s). Total angular speed is not used, because every swing rotates around shoulder and elbow. Shares the weapon-on-weapon contact code with Clash of Might; build together with step 5.
- Maybe `Gamey` / `Realistic` / `Custom` presets for the disarm values (as Charge Stagger has); Realistic values need the headset.
- Adrenaline gain per disarm path and a catch bonus (catching a disarmed weapon in the air before it lands); with step 4.

**Config:** `bodyHitDrop`, `bodyHitDropChance`, `dropChancePerExtraHit`, `femaleChance`, `smallerChance`, `smallerMargin`, `loosenedGripSpark`, `loosenedGripHaptics`, `shieldsCanBeDisarmed`, `disarmFlingForce`; later `twistEnabled`, `twistChance`, `twistMinRollSpeed`, Adrenaline gains for disarm and catch.

## 3. Charge Stagger

**Behavior**
- If the player is running fast (locomotion speed ≥ `chargeSpeedThreshold`, top speed within `chargeSpeedWindow` before the impact) when a hit lands, the hit NPC is **staggered**.
- Counts for melee hits (held weapon, fist/kick) and — switchable, default on — for thrown weapons and spells that hit while the player runs.
- The hit itself must be hard enough: impact speed ≥ `minSwingSpeed` (swing, punch or projectile).
- A charge hit staggers with `staggerChance` (percent, default 100 %).
- Any number of NPCs can be charge-staggered. A **second** charge hit on an NPC that is still staggered does not stagger again: it **throws the NPC to the ground** (switchable, default on).
- A successful charge stagger adds to the Adrenaline meter.

**Presets:** a `Gamey` / `Realistic` choice sets predefined values for chance, speed threshold, speed window, min swing speed, strength and duration. Every value stays adjustable; changing one shows `Custom`. Default: Gamey.

**Implementation hints**
- Track player locomotion velocity each frame; on hit, compare against threshold (optionally use speed in a short window before impact).
- Trigger the game's built-in stagger/push on the NPC if available, otherwise apply a short ragdoll/unbalance.

**Config:** `preset`, `staggerChance`, `chargeSpeedThreshold`, `chargeSpeedWindow`, `minSwingSpeed`, `thrownAndSpellsCount`, `staggerStrength`, `staggerDuration`, `secondHitKnocksDown`.

## 4. Adrenaline System

**Meter fills from** (each with its own config value):
- Successful hits
- Blocks / parries
- Clash wins
- Taking damage
- Successful dodges
- Charge staggers

**When the meter is full**
- A **time-limited window** opens (`adrenalineWindowDuration`, e.g. 6 s). If unused, meter resets.
- Signal the window: heartbeat sound, haptic pulse pattern, screen vignette/desaturation, optional weapon glow/trail.
- The **next landed hit** during the window is a critical:
  - Guaranteed dismemberment of the hit part (if dismemberable)
  - Slow-mo (`Time.timeScale`, e.g. 0.3 for ~1.5 s, then smoothly restore)
  - Massive knockback / ragdoll force
  - Screen distortion effect
- Meter resets after the critical.

**Implementation hints**
- Single persistent component holding a float meter + state machine: `Filling → Ready(window timer) → Critical → Filling`.
- Always restore `Time.timeScale` (including on death, menu, level unload).
- Dodge detection: if no direct event exists, approximate (e.g. enemy attack misses while player moved quickly out of range).

**Config:** gain values per event, `meterMax`, `adrenalineWindowDuration`, `slowMoScale`, `slowMoDuration`, `knockbackForce`, toggles for each visual effect.

### 4b. Adrenaline design (2026-11-16; 4a–4e built, see docs/IMPLEMENTATION.md)

Read in the decompiled `ThunderRoad.dll` (game 1.3.2) and the game's catalog JSON.

**Own meter, not the game's Focus.** The base game has Focus (`Mana.currentFocus`, 30 without skills, regen 3/s, half while moving). It is spent on a manual slow-time (`SpellPowerSlowTime`, held on the right B button in the SteamVR bindings, scale 0.5, 6 focus/s, needs 10). Full Focus only plays a sound (`Effect_FocusFull`). The skill `SkillCombatFocus` refills it on kill, slice, parry, deflect and disarm. Focus has no automatic critical, so DMCS keeps its own meter and leaves Focus alone.

**State machine** (pure logic in `DMCS.Core`, unit-tested over every flow): `Filling → Ready (window timer) → Critical → Filling`; an unused window empties the meter. `meterMax` default 100, `adrenalineWindowDuration` default 6 s, `meterDecay` 2 per second after `meterDecayDelay` 0 s, switched off by default (see "Meter decay" below).

**Gains** (each its own slider; defaults confirmed by the user 2026-11-16, not tried in game yet):

| Source | Detection (as read in the code) | Default |
|---|---|---|
| Player hit on an NPC | `EventManager.onCreatureHit`, existing `HitInfo` | 5 |
| Block / parry | `EventManager.onCreatureAttackParry` (an NPC attack hits the player's weapon; raised by `BrainModuleMelee`; block and parry are one event) | 10 |
| Damage taken | `onCreatureHit` with the player as target | 5 |
| Charge stagger | existing `ChargeStagger` event | 15 |
| Disarm (all paths) | existing `DisarmStrike` event | 15 |
| Catch bonus | `EventManager.OnItemGrab` on a weapon DMCS knocked away, before it touched the ground (how "touched the ground" is read is not settled yet) | 10 |
| Dodge | no game event: an NPC melee attack on the player reaches `AttackStage.Attack` (`onCreatureAttacking`) and ends without a hit on the player and without a parry, while the player moved faster than a threshold (reuse `SpeedHistory`). An approximation; needs the headset | 10 |
| Clash win | step 5; leave the hook | 25 |

**Critical = Fatality** (the next landed player hit during the window; melee, fist and kick; thrown and spells behind a switch, default off, as Charge Stagger). As Dark Messiah's fatality (manual: a power strike with a full adrenaline bar slays the enemy; aimed at the head it beheads):
- `criticalNeedsHardSwing` (default on): only a hit at least as fast as `criticalMinSwingSpeed` (default 3 m/s, user 2026-11-16) is critical; lighter hits count towards the kneel (see "Kneel and execution"). VR stand-in for the power strike.
- `criticalKills` (default on): the critical kills the NPC (`Creature.Kill`), also when nothing could be cut off.
- Battle cry (`battleCry`, default on): the player shouts on the critical, with the game's own player voice (the player creature has voice `HumanMale1` / female counterpart; dialog `attackmelee` = `Bas.AudioGroup.Voice.Male1.Attack`). The exact call to play a creature dialog is to be read in the code at build time. Dark Messiah's own sounds are not shipped (copyright, see below).
- Dismember: `RagdollPart.TrySlice()` on the hit part. The game refuses it when dismemberment is off in its content settings and for the root part (torso); then the rest still happens.
- Slow-mo (see "Critical time mode"): the game's `TimeManager.SetSlowMotion` (it also sets `fixedDeltaTime` and the slow-mo audio snapshot), then `SetSlowMotion(false, …)`. If a slow-mo is already running (`TimeManager.slowMotionState` not `Disabled`, e.g. the player's Focus), DMCS skips its own and never stops the other one. Always restore on feature off, level unload and player death.
- Knockback: destabilize the ragdoll and push all parts along the hit (as the disarm fling).

**Ready sound (decided 2026-11-17, built):** Dark Messiah plays `Adrenaline_Reached` ("// Full Adrenaline", volume 0.6) when the bar fills. DMCS rebuilds it very close as its own synthesis (dark thunder roar at the start + rumble + hiss + metallic ring at ~1021 Hz, measured in DM's file; see docs/IMPLEMENTATION.md, 4d). DM's file is never shipped; since 2026-11-17 the player can pick "Dark Messiah" and DMCS reads the original from the player's own install at runtime (user's request).

**Dark Messiah's heartbeat (read in its `server.dll`, 2026-11-17):** not an adrenaline signal but a low-health warning. `Heartbeat_1` plays once when the player's health falls to 10 % or below, `Heartbeat_2` once at 50 % or below (above 10 %); both re-arm when health is back above 50 %. The convars `mm_player_heartbeat_fast` (0.6) and `mm_player_heartbeat_slow` (1) are registered but never read. B&S has its own low-health heartbeat (`CameraEffects`), so DMCS does not add one; its heartbeat while the window is open stays as designed.

**Signals while the window is open** (built in 4d; also while an execution window is open):
- Heartbeat: the game's clip `Bas.Audio.Misc.HeartBeatLoop` on DMCS's own AudioSource (the game's heartbeat source belongs to the health display and is reset by `CameraEffects.RefreshHealth`).
- Haptics: `RagdollHand.PlayHapticClipOver` with a heartbeat curve.
- Weapon glow: catalog particle `Bas.Particle.Spell.Hyperfocus.Hand` at the weapon hand (look needs the headset).
- No meter display while filling (a wrist display would need a UI asset; later at most).

**Adrenaline vignette (decided 2026-11-16): Dark Messiah style, own drawing.**
- Dark Messiah's effect (read in its own files, `materials/hud/fullscreen_adrenaline.vmt` in the VPKs): a fullscreen `UnlitGeneric` overlay, `$additive`, texture `hud/fullscreen_adrenaline2_` = 25 frames of 256×256 DXT1 played at 25 fps (1 s loop): red and orange streaks reaching in from the edges, black (= see-through) centre, the streaks sit elsewhere in every frame, which makes them flicker.
- DMCS rebuilds it very close in look and technique, but with its own frames: DMCS generates the flipbook at startup (no Unity SDK asset), draws it additively on a head-locked mesh in front of the eyes (both eyes see it right), plays it while the window is open and flares it once on the critical. Colours, streak length, density and frame rate are Mod Menu sliders.
- Never ship or trace Dark Messiah's files: copyrighted, and Nexus forbids shipping them without the rights holder's permission (File Submission Guidelines; wiki "Porting content"); a free mod changes nothing. Reading them at runtime from the player's own Dark Messiah install is allowed as an option (user, 2026-11-17): *Ready Sound Style*, *Vignette Style* and *Heartbeat Sound* each offer "Dark Messiah" next to every DMCS version (DMCS 1–3), and fall back to DMCS 3 / B&S when the install is not found.
- Shader (harness probe 2026-11-17): the game has `Universal Render Pipeline/Particles/Unlit` loaded; DMCS sets it additive (`_Surface` 1, `_Blend` 2, `_SrcBlend` SrcAlpha, `_DstBlend` One, `_ZWrite` 0, `_SURFACE_TYPE_TRANSPARENT`). The look was matched against DM's frames (patchy thin radial smears, mostly dim red); *Vignette Size* sets how far from the view centre the frame's edge sits (default 50°).
- The game's own vignette is not used: it belongs to the health display and `CameraEffects.RefreshHealth` overwrites it on every hit and heal.

**Meter decay (decided 2026-11-16):** switchable (`Adrenaline Decay`), off by default while testing (user, 2026-11-16); when on: delay 0 s, 2 per second (a full meter of 100 drains in 50 s). Both are sliders.

**Screen distortion: dropped (user, 2026-11-16).** Bullet time takes its place.

**Critical time mode (decided 2026-11-16):** a string dropdown `criticalTimeMode` (enum dropdowns get a wrong default in B&S), default `Hyperfocus`; each mode has its own scale and duration sliders:
- `Hyperfocus`: as the game's Hyperfocus skill: the world slows, the player keeps moving near normal speed (`SpellPowerSlowTime.BuffPlayer`: `Creature.AddJointForceMultiplier`, `Locomotion.SetSpeedModifier`, `Locomotion.SetPhysicModifier`; catalog `Skill_HyperFocus`: timeScale 0.1, joint ×10, speed ×20, drag ×10). DMCS makes the same calls with its own handler and removes them afterwards.
- `Slow Time`: as the game's plain slow time on the B button (`Spell_SlowTime`: scale 0.5); the player slows too.
- Both through `TimeManager.SetSlowMotion`; duration in real time (proposed 3 s).

**Chest-stab fatality (decided 2026-11-16, flow corrected by the user the same day):** a critical stab into the torso: the NPC sinks to its knees with the blade in it and dies; there is no slow-motion yet. Within `executionWindow` the player finishes it:
- a kick (a player body hit with a foot or leg): slow-motion starts and the NPC flies away (`criticalKnockback`);
- a cut to the neck or head (Slash): the head comes off (`RagdollPart.SafeSlice` on the head), slow-motion starts and the head flies away. The game's cut runs in a coroutine (`Ragdoll.SliceCoroutine` sets `isSliced` later), so the push on the head waits for it.
- Nothing in time: the dying animation ends and the body falls.
- Kneeling death: `BrainModuleDeath` picks its dying clip inside `Creature.Kill` (`TryPlayAnimation` → `DyingCoroutine` → `AnimationData.Pick` before its first yield); DMCS sets that NPC's `stabAnimationData` and `defaultAnimationData` to a kneeling-only copy of `HumanStabDeaths` (`Death_KneelBackward`/`KneelMid` from the front, `Death_KneelForward…` from behind, `KneelMid` from the side) for the kill and restores them right after. Harness 2026-11-16: `Death_KneelBackward`/`Death_KneelMid` played.

**Kneel and execution (decided 2026-11-16)** — what happens in the adrenaline window besides a hard hit:
1. The meter is full, the adrenaline window opens.
2. A hard hit (`criticalNeedsHardSwing`) is the critical as above.
3. The NPC goes down on its knees, alive, when either
   - no hard player hit lands for `kneelAfterNoHit` seconds (2 s, confirmed; must be shorter than the window), or
   - the player lands only short, light hits (below the hard-swing speed): after `kneelAfterLightHits` of them (2, confirmed).
   Which NPC: the one the player hit last; if none yet in this fight, the nearest enemy attacking the player.
4. The kneeling replaces the adrenaline window with an execution window (`executionWindow`, 5 s, confirmed; shared with the chest-stab finish). Any player hit on the kneeling NPC is the execution: it counts as the critical (time mode, battle cry, kill); a stab plays the kneeling death, anything else ragdolls it.
5. No execution in time: the NPC stands up again and fights on, or with `kneelDeathChance` (default 0 %) it dies and falls over. The meter is empty either way.
- Kneeling alive: the catalog animation `HumanIdleExamineKneel` (KneelingStart / ExamineKneel loop / KneelingEnd), held the way the game's `BrainModuleFear.Panic` holds a cowering NPC (`brain.isIncapacitated`, behaviour tree reset and `updateTree = false`, stance Idle, `StopAnimation(true)`, `PlayAnimation`); released as `StopPanic` does. A first try with a brain swap to `HumanDummy` left the NPC standing (harness screenshot 2026-11-16).

**API for other mods (approved by the user 2026-11-17, built):** `DMCS.Adrenaline.AddExternal(float amount, string source)`, public static, found by name through reflection (no hard dependency); same gain path as DMCS's own sources; false when Adrenaline is off, the amount is not positive or the window is open. Keep name and signature stable. Used by the Achilles Tendon mod (`AchillesHeelCut`, `AchillesCrippledKill`); details in docs/IMPLEMENTATION.md.

**Build order:** 4a meter, gains (hit, damage, parry, stagger, disarm), decay, window, Mod Menu, debug log; 4b critical (time mode, kill, dismember, knockback, battle cry, chest-stab fatality); 4c kneel and execution; 4d signals, ready sound and the vignette (built 2026-11-17); 4e dodge and catch bonus (marked "not verified in game" until a headset check; design below). Each: unit tests first, then a harness scenario (the harness can deal hits and damage and read `Time.timeScale` and `isSliced`; parry and dodge are hard to force there).

**4e Dodge and catch bonus (designed and built 2026-11-17, user go the same day):**
- Dodge: an NPC melee attack on the player (`EventManager.onCreatureAttacking`, `AttackType.Melee`, target = the player) that reached `AttackStage.Attack` and then ends with `AttackStage.End` (not `Cancel`: the game raises Cancel when an attack that could already damage is interrupted, `BrainModuleMelee`) without hitting the player (`onCreatureHit` on the player from that NPC; a hit whose attacker cannot be read voids every open attack) and without a block/parry (`onCreatureAttackParry` with that NPC as the parried side), while the player's **head** moved at least `dodgeMinSpeed` (m/s, horizontal, measured over 0.1 s, fastest value between Attack and End). Head instead of `Locomotion.horizontalSpeed`, because a VR dodge is mostly a real step or lean, which the stick speed does not show; head speed sees both. A jump of more than 1 m between two frames (teleport) starts the measurement over. An approximation; the default speed (2 m/s) is a guess for the headset.
- Catch: a weapon DMCS knocked out of an NPC's hand (every Disarm Strike path), grabbed by the player's hand (`EventManager.OnItemGrab`) before it touched the ground and within `catchMaxAirTime` (default 3 s). "Touched the ground" = the weapon's rigidbody collides with a collider that belongs to no `ColliderGroup` (no creature part, no item: level geometry and static props), read with an own `OnCollisionEnter` component on the weapon, because the game's `CollisionHandler.OnCollisionStartEvent` drops slow contacts and contacts without known physics materials. Telekinesis catches do not count (hand only). An NPC grabbing it first ends the chance.
- Mod Menu (in the gain block after `Per Disarm`): `Adrenaline Per Dodge` (10), `Dodge Min Speed` (0.5–6 m/s, 2), `Adrenaline Per Catch` (10), `Catch Max Air Time` (0.5–5 s, 3).
- Harness: BasHarness gets `npc-attack <stage>` (raises the game's own `EventManager.InvokeCreatureAttack` for the spawned NPC against the player; tests DMCS's listening, not a real NPC swing) and `player-grab-dropped` (the player's hand grabs the weapon the spawned NPC was given with `npc-grab`). A real NPC swing and a real dodge stay a headset check.

**Open work (listed 2026-11-17, in this order):**
1. Headset check of 4a–4d: meter, critical, kneel/execution, signals (vignette size, haptics, sounds, DMCS 1–4 against Dark Messiah). Cost: small for code, the user's time in the headset; brings back the values everything else rests on (hard-swing 3 m/s, vignette 50°, haptics strength, volumes).
2. ~~4e dodge and catch bonus~~ built 2026-11-17 (see above and docs/IMPLEMENTATION.md); the dodge speed needs the headset.
3. Step 5: Clash of Might (redesigned 2026-11-17, see section 1) + Twist Disarm (blade-on-blade contact). Cost: large; first a harness probe (wind-up times, freezing an NPC mid-swing, swing plane, controller offset), then build; push strength, weight and timings are tuned in the headset.
4. DMCS 4 vignette fine-tuning: Dark Messiah's streaks are a bit more brownish and blurrier. Cost: small (colour and blur in the generator plus a statistics test); only when the headset shows it matters.
5. End of step 5: three more clash flash sounds besides DMCS 1 (user 2026-11-17). Cost: small (the synth exists), the ear decides.
6. Later, not scheduled: `Gamey` / `Realistic` presets for the disarm values (Realistic needs headset values), and features as skill-tree powers (below). Cost: medium to large; whether a mod can add a skill to the Body tree is not checked yet.

**Working on Windows:** the harness (`tools/harness.sh`) needs Linux (Bash, own Proton prefix). A Windows build needs `BAS_GAME_DIR` or `-p:BasGameDir=…`, as the default path in `Directory.Build.props` is the Linux Steam folder.

---

## Later: features as skill-tree powers (not scheduled)

Instead of an on/off toggle, each feature's main setting becomes **Off / On / Power**:
- **Off** — feature disabled.
- **On** — always active (today's behaviour).
- **Power** — the feature becomes a power the player has to **buy in the Body crystal skill tree**; it only works once bought.
- First candidate (user, 2026-11-17): Clash of Might's win disarm (`clashWinDisarms`) as a buyable power, as Dark Messiah's Disarming skill.

To verify before building: how a mod adds a skill to an existing tree (the game's skills are `SkillData` catalog entries, e.g. `SkillCharger`; whether a mod can add one to the Body tree via its own catalog JSON is not checked yet), and how "bought" is read at runtime (e.g. `Creature.HasSkill`).

## Build order

1. Project setup + SDK version check + empty mod loading in game with debug logging + Mod Menu skeleton (all feature groups with their `Enabled` toggle). Add each feature's settings to the menu as that feature is built.
2. Charge Stagger (simplest, validates hit + stagger hooks).
3. Disarm Strike (builds on stagger).
4. Adrenaline System (meter + window + critical effects).
5. Clash of Might (hardest — collision duration + controller intensity tuning).

After each step: build, test in game, and note what was verified vs. assumed (in docs/IMPLEMENTATION.md; README.md stays player-facing).
