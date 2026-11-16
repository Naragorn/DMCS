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

**Behavior**
- When the player's weapon and an NPC's weapon stay in contact for longer than `clashMinContactTime` (e.g. 0.3 s), a clash begins. Short/glancing contacts are ignored.
- During the clash, measure the player's **push intensity**: the sum of small, rapid controller movements (micro-movements while holding the blade against the enemy) over time, from the weapon-holding hand's velocity/acceleration.
- The NPC has a fixed **resistance** value (config, optionally scaled by difficulty / NPC type).
- Clash resolves when one side's accumulated value wins or after `clashMaxDuration`:
  - Player wins → NPC is disarmed (drops weapon) → counts as a clash win for Adrenaline.
  - NPC wins → player's weapon is pushed away / player is briefly staggered or disarmed (configurable).
- Feedback during clash: controller haptics + sparks/grinding sound on the blades so the player knows the clash is active.

**Implementation hints**
- Detect sustained weapon-on-weapon collision (collision enter/stay/exit on both items, track contact duration).
- Each frame: read the holding hand's velocity, accumulate `intensity += |Δvelocity| * weight`, decay slightly over time so idle holding doesn't win.

**Config:** `clashMinContactTime`, `clashMaxDuration`, `npcResistanceBase`, `intensityWeight`, `intensityDecay`, `playerLoseOutcome` (`stagger` | `disarm` | `none`), `hapticsStrength`.

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

### 4b. Adrenaline design (2026-11-16, not built; open points at the end)

Read in the decompiled `ThunderRoad.dll` (game 1.3.2) and the game's catalog JSON.

**Own meter, not the game's Focus.** The base game has Focus (`Mana.currentFocus`, 30 without skills, regen 3/s, half while moving). It is spent on a manual slow-time (`SpellPowerSlowTime`, held on the right B button in the SteamVR bindings, scale 0.5, 6 focus/s, needs 10). Full Focus only plays a sound (`Effect_FocusFull`). The skill `SkillCombatFocus` refills it on kill, slice, parry, deflect and disarm. Focus has no automatic critical, so DMCS keeps its own meter and leaves Focus alone.

**State machine** (pure logic in `DMCS.Core`, unit-tested over every flow): `Filling → Ready (window timer) → Critical → Filling`; an unused window empties the meter. `meterMax` default 100, `adrenalineWindowDuration` default 6 s, `meterDecay` 2 per second after `meterDecayDelay` 0 s (see "Meter decay" below).

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
- `criticalNeedsHardSwing` (default on): only a hit at least as fast as `criticalMinSwingSpeed` (default = Charge Stagger's `minSwingSpeed`) is critical; a lighter hit is a normal hit and keeps the window open. VR stand-in for the power strike.
- `criticalKills` (default on): the critical kills the NPC (`Creature.Kill`), also when nothing could be cut off.
- Battle cry (`battleCry`, default on): the player shouts on the critical, with the game's own player voice (the player creature has voice `HumanMale1` / female counterpart; dialog `attackmelee` = `Bas.AudioGroup.Voice.Male1.Attack`). The exact call to play a creature dialog is to be read in the code at build time. Dark Messiah's own sounds are not shipped (copyright, see below).
- Dismember: `RagdollPart.TrySlice()` on the hit part. The game refuses it when dismemberment is off in its content settings and for the root part (torso); then the rest still happens.
- Slow-mo: the game's `TimeManager.SetSlowMotion` (it also sets `fixedDeltaTime` and the slow-mo audio snapshot), then `SetSlowMotion(false, …)`. If a slow-mo is already running (`TimeManager.slowMotionState` not `Disabled`, e.g. the player's Focus), DMCS skips its own and never stops the other one. Always restore on feature off, level unload and player death.
- Knockback: destabilize the ragdoll and push all parts along the hit (as the disarm fling).

**Signals while the window is open:**
- Heartbeat: the game's clip `Bas.Audio.Misc.HeartBeatLoop` on DMCS's own AudioSource (the game's heartbeat source belongs to the health display and is reset by `CameraEffects.RefreshHealth`).
- Haptics: `RagdollHand.PlayHapticClipOver` with a heartbeat curve.
- Weapon glow: catalog particle `Bas.Particle.Spell.Hyperfocus.Hand` at the weapon hand (look needs the headset).
- No meter display while filling (a wrist display would need a UI asset; later at most).

**Adrenaline vignette (decided 2026-11-16): Dark Messiah style, own drawing.**
- Dark Messiah's effect (read in its own files, `materials/hud/fullscreen_adrenaline.vmt` in the VPKs): a fullscreen `UnlitGeneric` overlay, `$additive`, texture `hud/fullscreen_adrenaline2_` = 25 frames of 256×256 DXT1 played at 25 fps (1 s loop): red and orange streaks reaching in from the edges, black (= see-through) centre, the streaks sit elsewhere in every frame, which makes them flicker.
- DMCS rebuilds it very close in look and technique, but with its own frames: DMCS generates the flipbook at startup (no Unity SDK asset), draws it additively on a head-locked mesh in front of the eyes (both eyes see it right), plays it while the window is open and flares it once on the critical. Colours, streak length, density and frame rate are Mod Menu sliders.
- Never ship, trace or load Dark Messiah's files: copyrighted, and Nexus forbids it without the rights holder's permission (File Submission Guidelines; wiki "Porting content"); a free mod changes nothing.
- Risk: an additive shader the game ships (else borrow one from a catalog particle effect). First step before 4c: a harness probe that only logs which shader is found.
- The game's own vignette is not used: it belongs to the health display and `CameraEffects.RefreshHealth` overwrites it on every hit and heal.

**Meter decay (decided 2026-11-16):** the meter drains over time by default: delay 0 s, 2 per second (a full meter of 100 drains in 50 s). Both are sliders.

**Screen distortion: dropped (user, 2026-11-16).** Bullet time takes its place.

**Bullet time on the critical (decided 2026-11-16; details proposed, not confirmed):** the critical's slow-mo becomes bullet time: the world slows (`TimeManager.SetSlowMotion`), the player keeps moving at near normal speed. The game's own Hyperfocus skill does the same (`SpellPowerSlowTime.BuffPlayer`: `Creature.AddJointForceMultiplier`, `Locomotion.SetSpeedModifier`, `Locomotion.SetPhysicModifier`; catalog `Skill_HyperFocus`: timeScale 0.1, joint ×10, speed ×20, drag ×10); DMCS uses the same calls with its own handler and removes them afterwards. Proposed: `bulletTimeScale` 0.3, `bulletTimeDuration` 3 s (real time), `playerFullSpeed` on.

**Chest-stab fatality (decided 2026-11-16; details proposed):** a critical that stabs the torso (`damageStruct.penetration != None`, hit part Torso) makes the NPC sink to its knees and die; it then falls over, or the player kicks it away.
- The game already has this: `BrainModuleDeath` plays a dying animation when a penetrating hit kills a standing, slow NPC (`stabAnimationId` = `HumanStabDeaths`, which holds `Death_KneelBackward`, `Death_KneelMid`, `Death_KneelForward`, `…2`, `…3` among non-kneeling ones; picked by hit direction; a stab from the front is "Backward": 2 of 6 clips kneel). The velocity gate is off in the HumanMedium/HumanHard brains (`useDamagerDyingAnimMaxVelocity: false`), so a hard swing still gets it.
- Proposed: DMCS guarantees the kneel by setting that NPC's `BrainModuleDeath.stabAnimationData` (a public field) to a copy holding only the kneeling clips just before it kills it with `Creature.Kill(collisionInstance)` of the stab, then restores it. Not tried yet; a harness scenario logs the clip played.
- Kick away: the game already ends the dying animation when a hit faster than the damager's `dyingAnimationMaxVelocity` lands (kick: 8 m/s; `BrainModuleDeath.OnDamage` → `StopDying` → inert ragdoll). Proposed: `fatalityKickForce` adds a push along the kick (default 5 m/s, a first guess).
- No knockback and no dismember on this fatality (they would cut the kneel short).

**Still open:** the proposed details above (bullet time values, guaranteed kneel, kick force).

**Build order:** 4a meter, gains (hit, damage, parry, stagger, disarm), window, Mod Menu, debug log; 4b critical (dismember, slow-mo, knockback); 4c signals; 4d dodge and catch bonus (marked "not verified in game" until a headset check). Each: unit tests first, then a harness scenario (the harness can deal hits and damage and read `Time.timeScale` and `isSliced`; parry and dodge are hard to force there).

**Working on Windows:** the harness (`tools/harness.sh`) needs Linux (Bash, own Proton prefix). A Windows build needs `BAS_GAME_DIR` or `-p:BasGameDir=…`, as the default path in `Directory.Build.props` is the Linux Steam folder.

---

## Later: features as skill-tree powers (not scheduled)

Instead of an on/off toggle, each feature's main setting becomes **Off / On / Power**:
- **Off** — feature disabled.
- **On** — always active (today's behaviour).
- **Power** — the feature becomes a power the player has to **buy in the Body crystal skill tree**; it only works once bought.

To verify before building: how a mod adds a skill to an existing tree (the game's skills are `SkillData` catalog entries, e.g. `SkillCharger`; whether a mod can add one to the Body tree via its own catalog JSON is not checked yet), and how "bought" is read at runtime (e.g. `Creature.HasSkill`).

## Build order

1. Project setup + SDK version check + empty mod loading in game with debug logging + Mod Menu skeleton (all feature groups with their `Enabled` toggle). Add each feature's settings to the menu as that feature is built.
2. Charge Stagger (simplest, validates hit + stagger hooks).
3. Disarm Strike (builds on stagger).
4. Adrenaline System (meter + window + critical effects).
5. Clash of Might (hardest — collision duration + controller intensity tuning).

After each step: build, test in game, and note what was verified vs. assumed (in README).
