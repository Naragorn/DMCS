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
- Each group starts with `Enabled` (bool toggle), followed by its settings. Exception (user 2026-11-18): Clash of Might starts with its mode dropdown `Off` / `Simple` / `On`.
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

**Redesigned with the user 2026-11-17** (replaces the first draft "any blade contact longer than 0.3 s + controller micro-movements"). **5a built 2026-11-17** (flow steps 1–3: big swing, flash, slow-motion; see docs/IMPLEMENTATION.md); 5b (block at the flash, freeze, weight, diagnostic log for the push measure) built 2026-11-17; 5c (mode, rounds, push measure, outcomes, Mod Menu rest) built 2026-11-18 (docs/IMPLEMENTATION.md); feel and values: headset.

**Dark Messiah's own clash (read 2026-11-17):** both sides do a power attack at once → the blades lock (`sword_versus_sword`, a click counter `m_iVersusClick`, win/lose animations `ACT_VERSUS_*_OUT_WIN/_OUT_LOOSE` in DM's `server.dll`); the player mashes to push back, the loser is knocked back and briefly stunned ([mthec 2009](https://mthec.wordpress.com/2009/03/23/dark-messiah-might-and-magic-part-one/)). With the Disarming skill (melee level 2) the winner disarms the loser ([GameSpot walkthrough](https://www.gamespot.com/articles/dark-messiah-of-might-and-magic-walkthrough/1100-6160412/)). DMCS's lose outcome is harsher than DM's (user's choice).

**Flow**
1. **Big swing:** an NPC melee attack with a long wind-up. B&S has no power-attack class: at `AttackStage.WindUp` the running attack is `BrainModuleMelee.attack` (`AttackMotion`; `lastAttack` is the one before); its real wind-up in seconds is `animationClip.length × start / (animationSpeed × brain speed)` (`AttackMotion.GetRealTime`). A big swing = real wind-up ≥ `clashMinWindUp` (value from the probe that measures all of the game's attacks; the clip lengths are not in the catalog JSON). Thrusts do not count.
2. **Flash:** a short bright glint at the middle of the NPC's blade (middle of its slash damager) = the signal. `clashOnlySwords` (default on): only swords; off: every blade with a slash damager.
**Two hands, the only condition (user 2026-11-24, built):** a block of the flashing blade with the weapon in both hands (the game's `Item.IsTwoHanded`) is the clash, in Simple and On; a one-handed block is an ordinary parry. Angle, block reach, max block speed and the power-parry precedence are gone (sliders removed). The slow-motion asks the same: the enemy in reach and a weapon in both hands. Why (Player.log 2026-11-23, headset with the old saved values 45°/1.5 m/s): of 56 star blocks 29 were refused for the angle and 21 for the speed, 6 locked; the slow-motion was refused 50 of 57 times for the blade distance. Steps 3 and 4 below are the history before this.
3. **Auto slow-motion** (`clashSlowMo`, default on, scale `clashSlowMoScale` 0.3) when, at the start of the strike (the first physics frames of `AttackStage.Attack`, see the probe), all hold: the NPC is within its swing reach of the player; the player's blade is within `clashBlockReach` (default 1.0 m since 2026-11-22, first 0.6 m; headset log 2026-11-22: at 0.6 m 54 of 63 judged swings were refused as BladeTooFar and the user saw no slow-motion; 1.0 m is a guess, the refusal log now names the distances) of the flash point; and the player's blade is roughly at a right angle to the swing: perpendicular to the swing plane (the plane of the NPC blade's axis and its motion), within `clashBlockAngleTolerance` (very generous, default 45°). Skipped when another slow-motion runs (as the critical). Ends when the clash starts, or at the end of the swing.
4. **Block of the flashing blade:** the game reports the NPC attack hitting the player's weapon (`EventManager.onCreatureAttackParry`, with the contact point). **No exact point any more (user 2026-11-22: "far too rarely hit"):** any block of the flashing blade counts; `clashPointTolerance` is gone. The angle check stays as a slider, default 90° (= any angle). Otherwise an ordinary parry (+10 Adrenaline as now), no clash. **Too much swing (user 2026-11-21, built):** a block whose hand is at or above `clashMaxBlockSpeed` (default 2.5 m/s since 2026-11-22, first 1.5; the hand speed the Parry group reads) is no clash either: the swing goes on, the blades bounce off, an ordinary parry (the Parry group still applies its own counter and power rules to it). Applies to Simple and On. Less swing → the clash triggers. Player.log 2026-11-22: 6 of 23 blocked star swings locked with point 0.25 m, 45° and 1.5 m/s; with the new rules 18 of 23 would have (docs/IMPLEMENTATION.md).
5. **Clash:** DMCS holds the NPC in its swing pose (AI hold as the kneel in 4c; risk, see the probe) and makes the player's weapon heavy (mass ×`clashWeaponWeight` while it lasts; B&S's own weight handling makes it lag behind the controller), so the player cannot just push through. **Hold Blade (user 2026-11-21, built):** the player's blade stays where the blades met (`clashHoldBlade`, default on; `clashHoldSlack` 2 cm, a guess); before, a swing into the block carried it up to 0.27–1.14 m past the lock point (seven locks in the Player.log of 2026-11-20). **Hold options (user 2026-11-22, built):** `clashHoldStyle` `Spring` (default) / `Hard`; spring `clashHoldSpringStrength` (Hz, 3) and `clashHoldSpringDamping` (0.5), sized on the blade's lock mass; `clashHoldRotation` (off: the blade turns around the lock point); `clashHoldBriefly` (off) with `clashHoldTime` (0.3 s): hold only the swing-through, then the weight alone. All values guesses for the headset.
6. **Push measure (a), decided:** how far the player's real controller is ahead of the held weapon, along the line from the player's head to the lock point (since 2026-11-22: pushing the crossed blades away from oneself; before, only "level towards the NPC's body" counted and most pushes were lost) = push strength. **Sideways (user 2026-11-22, built):** with `clashSidePush` (on) the push to the player's right or left (head's right, square to that line) counts too (forward and side together, pulling back adds nothing); a duel won mostly sideways (over all its rounds) always disarms, flinging the sword to that side; the star turns gold while the player is ahead pushing sideways. `clashWinDisarms` (now only the forward win) defaults to off, so the direction decides: forward = stagger or kneel, sideways = disarm. A disarmed winner's enemy gets the disarm reaction, no stagger on top; per push round against the enemy's push wave (2026-11-18: rounds replace the first draft "accumulate against `npcResistance`, lose after `clashMaxDuration`"; see "Decisions for 5c").
7. **Win:** `clashWinDisarms` (default on; later a buyable Body-tree power) disarms through the Disarm Strike path (fling, no-regrab lock, catch bonus); the NPC is staggered; +25 Adrenaline (clash win); with `clashWinKneelChance` (default 25 %) it also kneels and the execution window of 4c opens.
8. **Lose:** `clashLoseOutcome` dropdown `Disarm + Hit` (default), `Disarm`, `Hit`, `None`; the hit is `clashLoseDamage` (default 20; the player has 100 health, `Creature_PlayerDefault*.json`).
- Feedback during the clash: haptics and sparks at the contact. No grinding sound (removed by the user 2026-11-21; docs/IMPLEMENTATION.md "Metal rubbing loop"). Maybe back later, only while the blades are locked (user 2026-11-22: "später erst"; open work 8).
- Flash sound (user 2026-11-17, built): *Clash Flash Sound* (Off, DMCS 1; DMCS 1 modelled on Sekiro's perilous-attack cue, measured in a recording, Sekiro's files are encrypted and never read or shipped) and *Clash Flash Game Sound* (Off or one of three B&S catalog effects), any combination, plus a volume slider. User 2026-11-17: DMCS 1 is usable, the other three were dropped; make three new ones at the end of step 5. **Built 2026-11-20** (user: "bau drei weitere flash sounds"): DMCS 2 = DMCS 1 made short (1.6 s), DMCS 3 = darker and longer (24 Hz boom, ring an octave lower at 932 Hz, 4.5 s), DMCS 4 = bright (the ring at 2794 Hz carries it, light boom, 3 s); the ear decides.

**Decisions for 5c (user 2026-11-18):**
- **Feature mode** `clashMode` dropdown `Off` / `On` / `Simple` (replaces the plain `Enabled` toggle of this group; default `Simple`, user 2026-11-18).
  - **Simple** = the easier variant of this feature: the star comes, and a block exactly where the star is (same point and angle checks as the clash) knocks the enemy's sword away (Disarm Strike path: fling, no-regrab lock). No lock, no pushing.
  - **On** = the full lock below.
- **Difficulty** `clashDifficulty` slider 0–200, default 50: how hard the enemy pushes back. 0 = it barely pushes, 50 = some, 100 = twice as hard as 50, 200 = the player hardly has a chance (linear in the enemy's force).
- **Difficulty scales with height** `clashDifficultyScalesWithHeight` (toggle): taller enemies than the player push harder, smaller ones weaker (`Creature.GetHeight`).
- **No progress bar** (user 2026-11-18): every round counts on its own; the player just has to engage. **Clear signals (user 2026-11-22, built: "unclear whether you win or lose"):** `clashDuelStar` (on): the star moves to the crossed blades and shows who is ahead in the round (warm white even, green and bigger for the player, red and smaller for the enemy); `clashOutcomeCues` (on): win = sparks burst + the arena melee-kill stinger + three strong beats, lose = a heavy body-fall thud + a long rumble.
- **Rounds:** random between `clashMinRounds` and `clashMaxRounds` (default 3–5, each 1–10); `clashFewerRounds` (x, default 1) fewer against a smaller enemy (height scaling on) and/or a female one (gender scaling on).
- **Difficulty scales with gender** `clashDifficultyScalesWithGender` (default on): female enemies push `clashFemaleStrength` (default 50 %) weaker; literature: women have 40–60 % of men's upper-body strength.
- **Lose hit default on** (Lose Outcome `Disarm + Hit`).
- Mode default **Simple** (user 2026-11-18).
- **Push measure:** the player's real controller ahead of the weapon towards the enemy, minus a dead zone (`clashPushDeadZone`, ~3 cm guess) for the heavy weapon's natural lag; against `npcResistance`. All values guessed until the headset log.
- **Enemy pushes back periodically** (user: so the player can "play" at measuring strength). The lock runs in a few rounds, the enemy's push swelling and ebbing in each. **The return phase is gone (user 2026-11-22):** the next round starts as soon as one is held (`clashReturnWindow` and `clashReturnTolerance` removed). Before, the player had to bring the controller back to the blades between rounds; in the 2026-11-20/22 logs that lost two locks the player had pushed well.
- **Letting go** of the own weapon during the lock = lose (hit only; the weapon is already gone). Enemy dead, gone or letting go = the lock just ends.
- **Lose hit:** the enemy moves again (its swing animation runs on) and DMCS deals `clashLoseDamage` directly (deterministic, testable).
- **Feedback during the lock:** haptics that follow the enemy's push, sparks at the contact (the grinding sound was removed by the user 2026-11-21). A toggle uses Dark Messiah's own sounds from the player's install when found (as Ready Sound): `Sword_Versus_Sword` (`mm/sound/arkane/weapons/sword/sword_vs_sword01–03.wav`, volume 0.5–0.7, pitch 95–105, read in `mm/scripts/game_sounds_sword.txt` 2026-11-18) for the lock, `Sword_Disarm` (`sword_disarm.wav`, volume 0.6) for the disarm. Never shipped.

**Harness probe 2026-11-17** (temporary probe code, not committed; NPC `HumanMale`/`HumanMedium` with `SwordShortCommon` attacking the standing player, 2 runs, 21 swings):
- Real wind-up (to `start`) of all 68 attack motions in the three melee stances: 0.13–0.77 s; time to the hit (`peak`) 0.33–1.37 s. In game the Attack stage came on time (e.g. 0.500 s computed, 0.489 s measured). A wind-up threshold of ~0.4 s splits the plain swings (0.13–0.37 s) from the overhead, counter, lunge and step swings and most dual-wield swings (0.40–0.77 s); the exact value is a slider.
- The wind-up does **not** move the blade in the strike plane: angle between the two planes 15–88° (mostly 45–88°, 14 swings). So the angle check reads the strike plane in the first physics frames of `AttackStage.Attack`; the flash and the reach checks run from the wind-up on.
- Freezing works: `creature.animator.speed = 0` mid-swing kept the sword in place for 3 s (it settled 4–28 cm in the first 0.25 s, then stayed), held, alive, still `isAttacking`; the game's End event still came on time; after `speed = 1` the NPC fought on normally. Whether the frozen blade pushes back against the player's blade is not tested (the harness player cannot press a blade).
- Weight: the player's sword at mass ×5/×20/×50 sagged at the tip by 1/5/11 cm while held still; how heavy that feels when pushing is a headset check. Alternative if it is too weak: the player's arm joint force (`Creature.AddJointForceMultiplier` < 1, as the Hyperfocus buff uses > 1).
- The controller-ahead-of-hand offset cannot be measured headless (the mock headset's hand target does not move); `PlayerHand.grip` (target, driven by the controller's `TrackedPoseDriver`) vs `RagdollHand` position is the candidate pair, to be checked in the headset with a diagnostic log.

**Config:** `clashMode`, `clashDifficulty`, `clashDifficultyScalesWithHeight`, `clashDifficultyScalesWithGender`, `clashFemaleStrength`, `clashMinRounds`, `clashMaxRounds`, `clashFewerRounds`, `clashPushPhase`, `clashPushDeadZone`, `clashDarkMessiahSounds`, `clashOnlySwords`, `clashMinWindUp`, `clashSlowMo`, `clashSlowMoScale`, `clashBlockReach`, `clashBlockAngleTolerance`, `clashMaxBlockSpeed`, `clashWeaponWeight`, `clashHoldBlade`, `clashHoldStyle`, `clashHoldSlack`, `clashHoldSpringStrength`, `clashHoldSpringDamping`, `clashHoldRotation`, `clashHoldBriefly`, `clashHoldTime`, `npcResistance` (built as *Clash Enemy Push*),  `clashWinDisarms`, `clashWinKneelChance`, `clashLoseOutcome`, `clashLoseDamage`, `hapticsStrength`, `clashDuelStar`, `clashOutcomeCues`, `clashSidePush`.

## 2. Disarm Strike

**Behavior**
- If the player hits an NPC who is currently **staggered**, and the hit `RagdollPart` is a hand (or forearm, configurable), the NPC drops its held weapon(s).
- ~~Disarmed NPCs **just stand there**~~ (first spec; user 2026-11-22: "they just stand around dumb"). **Since 2026-11-22, by dice:** a short shock (the passive HumanDummy brain, `disarmedShockTime` 2 s), backing off (own brain, tree paused, `BrainModuleMove.UpdateMoveCycleDirection` backwards while turned to the player, `disarmedBackOffTime` 2 s) or cowering (the game's `BrainModuleFear.Panic`, `disarmedCowerTime` 5 s, a hit ends it); chances `disarmedShockChance` / `disarmedBackOffChance` / `disarmedCowerChance` (50 each, weighed against each other). Then the own brain is back and the game's `BehaviorTree_HumanUnarmed` makes it find and pick up a weapon. With `stayDisarmed` it stands still for good after the reaction. Replaces `disarmedNoRegrabTime` and `Disarmed Stand Still`. The reaction waits for a running stagger to end (at most 3 s): most disarms hit a staggered enemy, a stagger push ends the game's cowering at once, and a move would cut the stagger animation.

**Implementation hints**
- Hook the creature-hit event, check `targetCreature` stagger state and the hit part type (left/right hand).
- Release the held item from the corresponding hand.
- Prevent the AI from re-grabbing weapons while the reaction runs (or permanently, config).

**Config:** `includeForearm`, `disarmedShockChance`, `disarmedShockTime`, `disarmedBackOffChance`, `disarmedBackOffTime`, `disarmedCowerChance`, `disarmedCowerTime`, `stayDisarmed`, `requireStagger` (default true).

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

**Twist Disarm, built 2026-11-18** (docs/IMPLEMENTATION.md, step 3c; roll speed default 400°/s is a guess):
- **Reworked 2026-11-20 (user): only against a staggered enemy, as Dark Messiah's Disarming skill** (a stunned enemy, a power strike at its weapon or hand; GameSpot walkthrough, Steam discussion 2024). Gate: `Disarm Requires Stagger` (shared with the hand hit). Chance default 100 %. The twist stays required (the VR stand-in for the power strike; either direction). Kept apart from Clash of Might: the clash enemy is swinging, the twist enemy staggered; after a won clash with `clashWinDisarms` off the stagger opens a twist window (DM's flow). A slow touch starts no cooldown (bind, then twist counts).
- **Twist Disarm** (default on; first built 2026-11-18 with chance 50 % and no stagger): the player's weapon hits the NPC's **weapon** (blade on blade) while the blade rolls around its own long axis (wrist twist) faster than `twistMinRollSpeed` (deg/s). Total angular speed is not used, because every swing rotates around shoulder and elbow. Contact from the player's weapons' own collision events (not `onCreatureAttackParry`, which the game raises only while the NPC defends).

**Not built yet:**
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

**State machine** (pure logic in `DMCS.Core`, unit-tested over every flow): `Filling → Ready (window timer) → Critical → Filling`; an unused window empties the meter. `meterMax` default 100, `adrenalineWindowDuration` default 12 s (headset 2026-11-22: "6 s ist zu wenig"; 12 s is a guess, range 2–30 s), `meterDecay` 2 per second after `meterDecayDelay` 0 s, switched off by default (see "Meter decay" below).

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
   Which NPC: the one the player hit last; if none yet in this fight, the nearest enemy attacking the player. Since 2026-11-22 only within `kneelMaxDistance` (default 3 m, a guess; headset: far enemies knelt too); nobody that near = no kneel, the window stays open (`KneelTargetRule`).
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
5. ~~End of step 5: three more clash flash sounds besides DMCS 1~~ built 2026-11-20 (DMCS 2–4, section 1); the ear decides.
6. Perfect Block (section 5, user idea 2026-11-20): design with the user first (what a short swing is, how the block shows, the guard-down exceptions). Cost: medium; the threshold needs the headset.
7. Later, not scheduled: `Gamey` / `Realistic` presets for the disarm values (Realistic needs headset values); ~~features as skill-tree powers~~ built 2026-11-20 as the skill tree gate (below).
8. Later, not scheduled (user 2026-11-22: "später erst"): maybe bring the blade grinding sound back, only while the blades are locked in a clash. It was removed 2026-11-21 because a leak left it running after every clash (cause and fix in docs/IMPLEMENTATION.md "Metal rubbing loop"). If it comes back: it must start with the lock and end with it on every path (win, lose, break, feature off, level unload), behind its own Mod Menu toggle. The old `RubbingMetalSound` loop (removed in `8fd58c8`) already ended correctly at the lock's end and can be the starting point; check it in the headset that nothing keeps sounding afterwards. Cost: small.

**Deploy (rule, user 2026-11-18): always both installs.** Every committed, tested build goes into the Linux Steam copy **and** the Windows Steam copy of Blade & Sorcery (`…/BladeAndSorcery_Data/StreamingAssets/Mods/DMCS`; on Windows Vortex manages the Mods folder, DMCS lies next to the others), never only one. Use `tools/deploy.sh`: it refuses uncommitted `src` changes and builds with probe code, and checks the md5 of all three copies. Tell the Achilles session the deployed commit (it runs its compat check against DMCS). **From Windows (user 2026-11-22: "ja immer"):** after every committed, tested build, copy it straight into the Windows install (back up the old folder, DMCS.dll + manifest.json + Skills/*.json with LF endings, check the md5) without asking; the Linux copy follows with `tools/deploy.sh` there. New slider defaults do not reach options a player already saved (the game stores option indexes in `Saves/Default/Options.opt`).

**Working on Windows:** the harness (`tools/harness.sh`) needs Linux (Bash, own Proton prefix). A Windows build needs `BAS_GAME_DIR` or `-p:BasGameDir=…`, as the default path in `Directory.Build.props` is the Linux Steam folder.

---

## Dark Messiah combat features: what DMCS has, what is missing (listed 2026-11-20)

Sources for Dark Messiah: the game's manual (https://www.mocagh.org/nwc/darkmessiah-manual.pdf), the GameSpot walkthrough
(https://www.gamespot.com/articles/dark-messiah-of-might-and-magic-walkthrough/1100-6160412/), the GameSpy guide
(http://pc.gamespy.com/pc/might-and-magic/guide/page_2.html) and the HeroesWorld skill list
(https://heroesworld.net/wiki/might-and-magic/dark-messiah/dark-messiah-skills/).

**In DMCS already:**

| Dark Messiah | DMCS |
|---|---|
| Charge: sprint + power strike stuns | Charge Stagger (section 3); a second charge hit knocks the enemy down |
| Disarming (melee level 2): power strike at a stunned enemy's weapon or hand | Disarm Strike hand hit, Body Hit Drop (extra), Twist Disarm on the weapon (section 2) |
| Locked blades: both power-strike at once | Clash of Might, Simple or On mode (section 1) |
| A won locked-blades contest disarms (with Disarming) | `clashWinDisarms` |
| Adrenaline bar; full: the next power strike is a fatality | Adrenaline meter; a hard hit is the critical: kill, the hit part cut off, slow-motion, battle cry (section 4) |
| Fatality depends on the aim (head: beheading) | the hit part is cut off; chest stab with head-off or kick finish |
| DM's signals (ready sound, red vignette) | DMCS's own rebuilds, or DM's original files from the player's install |

DMCS's own additions (not in DM): kneel and execution, catch bonus, dodge bonus, Clash of Might's star warning.

**Missing (read in the sources above):**

| Dark Messiah | What it does | Cost for DMCS |
|---|---|---|
| Counter parry | Parrying right before the enemy's strike destabilizes it and opens it to a counter (manual: "Counter") | built 2026-11-20 with the Power Parry (section 6) |
| Kick | DM's trademark: enemies fly far, into spikes, fire, water or off ledges | **not built (user 2026-11-23: "the game has it already")**: B&S's kicks and its Mighty Kick skill (`SkillMightyKick`) |
| Double fatality (Adrenaline skill) | Store more adrenaline, two fatalities in a row | built 2026-11-23: option *Double Fatality* (default off) and a Body-tree skill on the last tier (section 8) |
| Whirlwind at full adrenaline | Crouch + power strike: a spin hitting everyone in reach | medium: VR needs a gesture (e.g. a fast turn with the weapon drawn), to be designed |
| Leap attack | Jump + power strike | **not built (user 2026-11-23: another mod does it)**: the game's Dive Strike skill plus High Dive Skill (FNIIM, Nexus 10264), listed under "Recommended mods" in the README |
| Adrenaline-enhanced magic | With a full meter: Telekinesis grabs and throws enemies, Weaken shrinks them for good | Fire, Lightning and Gravity built 2026-11-22, Telekinesis 2026-11-23 (section 7); Weaken left out (B&S has no such spell) |
| Finisher on a downed enemy | Stab a knocked-down enemy in the chest, without adrenaline | built 2026-11-23: group *Finisher* (default on), section 8 |
| Stealth backstab | A dagger power strike on an unaware enemy kills at once | **not built (user 2026-11-23: "won't do")**: in B&S the player does it by hand, a blade into the throat of an unaware enemy |
| Skill-tree odds and ends | Flurry of Blows, Strength +2 damage, Critical Hit +2 % double damage | hardly useful in VR; B&S's damage comes from physics |

B&S itself already has throwing daggers, bow headshots, shields and weapon blocks; how close they are to DM's was not
compared.

**Recommendation (2026-11-20):** the counter parry first (with the Power Parry, section 6) (small, reuses the parry event, closes DM's chain: parry in
time → the enemy staggers → Twist Disarm or a hard hit), together with Perfect Block (section 5); then the kick (DM's
signature; its values need the headset).

## 6. Counter Parry and Power Parry (user 2026-11-20; built 2026-11-20)

**Built (user go 2026-11-20: "bau beide"; power parry without a stagger gate, chance 100 %):** own Mod Menu group
`Parry` (Enabled, Counter Parry, Counter Guard Speed 1 m/s, Counter Stagger Duration 1.5 s, Power Parry, Power Parry Min
Speed 3 m/s, Power Parry Chance 100 %, Parry Speed Window 0.2 s; speeds are guesses for the headset). Rule
`DMCS.Core.ParryRule`, feature `Features/Parry.cs`, harness `step6-parry`, docs/IMPLEMENTATION.md step 6.
- One measure decides both: the parrying hand's real controller speed, fastest value in the window before the parry
  event (`HandSpeeds`). Held guard (below the counter speed) = the game's own parry; counter speed = stagger; power speed
  = disarm (`DisarmAlong`, flung away from the player) plus the stagger while Counter Parry is on.
- Deviation from the idea below ("timing from the attack's own stages"): in VR the contact time follows from where the
  guard is held, so attack timing would reward a guard held close to the body, not timing. Dark Messiah's counter is
  pressing block just before the hit; the VR equivalent is bringing the weapon into the swing just in time, which the
  hand speed shows. A player who waggles the sword constantly also gets counters (headset check; Perfect Block's
  short-swing measure may help later).
- Not the weapon's speed: at the event the collision has already slowed the blade, and the impact speed contains the
  enemy's swing.
- The counter's stagger is DMCS's own window (`HitInfo.IsStaggered` counts it, as a Charge Stagger window) plus the
  game's `ForceStagger` (StaggerMedium) one frame later, because right after the event `BrainModuleMelee` pushes the NPC
  itself (`TryPush(Parry)`: recoil or a riposte, read in the code 1.3.2) and would override an earlier stagger. The
  vanilla parry push leaves `brain.currentStagger` at None (StaggerCoroutine without SetStagger), so before this a parry
  never opened Disarm Strike.
- Clash of Might: `Parry.Start` subscribes after `ClashOfMight.Start`, so Clash decides first; Clash skips a parry at
  or above the power speed ("a power parry, not a clash"); a parry Clash took (lock or Simple disarm) is left alone
  (`TakenByClash`).
- Adrenaline: parry +10 as before (Adrenaline's own handler); a power parry is a disarm (+15). No extra gain for a
  counter.


**Counter Parry (Dark Messiah's "Counter", manual):** parrying right before the enemy's strike lands destabilizes it
and opens it to a counter. In DMCS: a parry in time (the game's `onCreatureAttackParry`, timing from the attack's own
stages) staggers the enemy, which opens the Disarm Strike hand hit and Twist Disarm.

**Power Parry (user 2026-11-20):** parrying an enemy attack with a lot of force, swinging hard into it (a power strike
as a parry) knocks the enemy's weapon out of its hand, without a twist.
- **Vanilla does not have this (read in the game code 1.3.2, 2026-11-20):** when an NPC's attack hits the player's
  weapon, `BrainModuleMelee` raises the parry event (impact > ~1.4 m/s) and pushes the NPC with `PushType.Parry`
  (impact > `recoilMinWeaponSpeed`, 1 m/s): the NPC recoils, or by chance ripostes (`recoilNothingRiposteChance`).
  How hard the player parried does not matter, and nothing disarms. The game's only block disarm is the other way
  round: an NPC blocking a player's heavy weapon (`ItemModuleAI.transferPushToBlocker`) that gets destabilized drops
  its own weapon (`BrainModuleDefense.disarmOnBlockDestabilize`).
- **Proposal:** on the game's parry event, the player's weapon speed at the contact (and/or the hand's swing, as Perfect
  Block measures it) at or above `powerParryMinSpeed` (guess: the hard-swing 3 m/s of the critical) → the enemy drops
  the attacking weapon (`DisarmAlong`, flung along the parry), with `powerParryChance` (default 100 %?); slower: the
  game's own recoil as now.
- **Apart from Clash of Might:** the clash is a held block at the star (slow blade); a power parry is a hard swing into
  the attack. So a parry at or above `powerParryMinSpeed` is never a clash; below it, the clash rules apply as now.
- Open: whether a power parry needs the stagger gate too (as Twist Disarm, user 2026-11-20) or is its own skill move
  without one; Adrenaline gain (Disarm +15 as every disarm path, plus parry +10).

**Config (proposal):** `counterParry`, `counterParryWindow`, `powerParry`, `powerParryMinSpeed`, `powerParryChance`.

## 7. Adrenaline Magic (user go 2026-11-20 "ja go für die magie"; Fire, Lightning, Gravity built 2026-11-22)

With a full meter the next player spell that hits an enemy is the critical, with a big version per element instead of
the melee fatality (battle cry and time mode as the critical; no cut). Mod Menu group `Adrenaline Magic` (category
order 5): Enabled, per element a toggle and its values. Part of Adrenaline: its toggle and skill gate count. Rule
`DMCS.Core.MagicRule`, feature `Features/AdrenalineMagic.cs`, harness `step8-adrenaline-magic` (BasHarness `player-cast`).
- **Fire:** the struck enemy gets the game's "Burning" at full heat (100: it ignites and dies the fire death); enemies
  within `magicFireRadius` (3 m) of it get `magicFireSpreadHeat` (50). Read in the game code 1.3.2: the fireball is an
  item with `ItemMagicProjectile`, its hit raises `onCreatureHit`, `imbueSpellCastCharge.id` is "Fire"; the game's own
  burn on a fireball comes from the default skill Ignition (heat 40).
- **Lightning:** the bolt (`SpellCastLightning.Hit`, `casterHand` set, DamageType Lightning) jumps to every enemy within
  `magicLightningRadius` (5 m): `PlayBolt` and "Electrocute" for `magicLightningShock` (4 s); the struck one dies
  (with Critical Kills). Lightning merges and skills hit with Energy and do not count.
- **Gravity:** the push (`SpellCastGravity.Throw`) damages nothing, so DMCS listens to the player's gravity spell's
  `OnPostPushEvent` (found through `Mana.OnSpellLoadEvent`): every enemy within `magicGravityRadius` (4 m) of the
  player is flung away at `magicGravityForce` (12 m/s); the nearest one the push hit dies.
- **Telekinesis (built 2026-11-23):** while the window is open the player's TK may grab a standing enemy
  (`grabRagdoll`, as the game's Marionette skill sets it) and throw it (`allowThrow`, as Telethrow); DMCS opens both
  only while the window is open and restores what was there before. A throw of an enemy
  (`SpellTelekinesis.onTelekinesisUngrabEvent`, thrown = true) is the critical: every part's velocity ×`magicTelekinesisThrow`
  (3), and the creature's next hit not from the player within `magicTelekinesisImpactTime` (3 s; its own impact) kills it.
  Harness: BasHarness `npc-tk-throw` flings the NPC and raises the game's release event; a real grab and swing: headset.
- Weaken (DM shrinks enemies): left out; B&S has no such spell.
- All values are guesses for the headset.

## 8. Finisher on a downed enemy (user 2026-11-23, default on; built 2026-11-23)

Dark Messiah: a knocked-down enemy is finished with a stab into the chest, no adrenaline needed. Group `Finisher`
(category order 7; Skill Tree moved to 8), one option `Enabled`. A stab (penetration or Pierce) with a weapon in the
player's hand into the torso of a living enemy whose ragdoll is Destabilized or Inert kills it. A kneeling enemy (the
adrenaline kneel or the chest-stab death) is left to the execution. Rule `FinisherRule`, `Features/Finisher.cs`, harness
`step9-finisher`. No skill (not asked for).

**Double Fatality (user 2026-11-23):** option `Double Fatality` in the Adrenaline group, default off, plus a Body-tree
skill on the last tier (`DMCS_DoubleFatality`, tier 3, *Double Fatality Needs Skill* in the Skill Tree group, default
off). On: the first critical of a full meter (melee or magic) leaves the meter full and opens a fresh window for a
second critical; the second empties it (`AdrenalineMeter.Spend`). The kneel's execution still uses up the whole meter.
With it the Adrenaline skill moved from tier 3 to tier 2 (user 2026-11-23).

## 5. Perfect Block (idea, user 2026-11-20; not designed, not built)

**Idea (user):** enemies block 100 % of the player's attacks while the player only makes short swings. Waggling the
controller must not win fights; the player has to bring finesse and skill into the fight.

**Why it fits:** B&S's damage depends on blade speed, so quick short flicks of the wrist are the cheapest way to hit.
Dark Messiah's melee rewards the opposite: power strikes break blocks, quick strikes mostly meet a raised guard (GameSpot:
"most melee enemies will have their parries up, making these strikes useless"), and a counter after a parry opens the
enemy.

**To decide with the user before building:**
- **What a short swing is.** Candidates: the hand's travel in the last ~0.3 s before the hit (a big swing travels far,
  a flick barely), the swing's wind-up (the hand moved back before it came forward), the arc of the blade tip, or the
  hit speed alone. The hand's travel and wind-up are read from the real controller (as HandSpeeds); the threshold is
  a Mod Menu slider and needs the headset.
- **What the block does.** (a) DMCS deflects the hit: no damage, sparks and a clang, the player's weapon is pushed back
  (cheap, deterministic, works on every NPC whatever its AI does). (b) The NPC's own AI really raises its weapon
  (BrainModuleDefense; whether DMCS can force a block in time is not checked). Recommendation: (a) first, (b) later if
  the look matters.
- **When the guard is down** (attacks that always get through): a staggered, kneeling, knocked-down or disarmed enemy;
  an enemy in the middle of its own attack; a hit from behind or the side (outside its view); the counter window right
  after the player's parry; thrown weapons and spells (switch).
- **What skill gets through instead:** a big swing with wind-up (the VR power strike), a feint (a swing that changes
  direction), a counter after a parry, Charge Stagger, Twist Disarm, Clash of Might.
- **Gradual or all-or-nothing:** 100 % block below the threshold (user's words), or a block chance that falls as the
  swing grows. A setting `perfectBlockChance` (default 100 %) keeps both possible.
- **Which enemies:** only armed ones (a weapon or shield to block with); per difficulty or enemy type later.
- **Interplay:** a blocked hit gives no Adrenaline for hits, but a block by the enemy could count as "parried" for
  Dark Messiah's counter idea; Disarm Strike's hand hit and Body Hit Drop need an enemy whose guard is down anyway
  (staggered), so they do not conflict.

**Config (proposal):** `perfectBlock` (on/off), `perfectBlockChance`, `minSwingTravel` (m), `minWindUp`, `blockDeflect`
(sparks, sound, push-back), guard-down exceptions as toggles.

## Skill tree gate (built 2026-11-20)

**Built (user 2026-11-20: "skill baum statt schalter (default off)"):** every feature has one skill in the game's
**Body** tree, and a Mod Menu group `Skill Tree` holds one toggle per feature, *<Feature> Needs Skill* (default off). On,
the feature works only while the player has that skill; off, it works as before. Deviation from the first idea below
(one dropdown Off / On / Power replacing each Enabled): a separate toggle per feature does the same (Enabled × Needs
Skill), but keeps every saved Enabled value and every harness scenario as it is.
- Skills (catalog JSON `src/DMCS/Skills/Skill_<id>.json`, `$type` `ThunderRoad.Skill.CustomSkillData`, shipped next to
  DMCS.dll; the game reads every `*.json` of a mod folder and its subfolders): Charge Stagger tier 1, Counter Parry
  (the Parry group) tier 1, Disarm Strike tier 2, Clash of Might tier 2, Adrenaline tier 2 (tier 3 until 2026-11-23),
  Double Fatality tier 3 (catalog tiers 0/0/1/1/1/2).
  The game adds each tier's core skill as a requirement and prices them like its own skills of that tier
  (`SkillData.OnCatalogRefresh`, `SkillData.Cost`, read in the game code 1.3.2).
- Icons: game icons as stand-ins (Juggernaut, TemporalRiposte, WeaponTheft, BruteForce, HyperFocus); an empty address
  would fall back to `Icons[<id>]`, which the game's sprite sheet does not have. Own icons need a Unity asset bundle.
- Bought or not: the game's own events. Buying at the tree adds the skill to the player's container
  (`SkillTreeOrb`: `container.AddDataContent`), refunding removes it; the game's `Mana` answers with
  `Creature.ForceLoadSkill` / `ForceUnloadSkill`, which raise `SkillData.OnSkillLoadedEvent` / `OnSkillUnloadedEvent`.
  At possession and level load DMCS reads `Creature.HasSkill` itself.
- `FeatureToggle.Enabled` = menu Enabled and (no gate or skill bought); the menu shows and saves `MenuEnabled`.
- Not checked: how the five orbs look in the tree, buying them with crystals, a skill without a video in the orb's
  popup (headset). Sandbox's skill menu should list them too (not checked).
- Note: the game's own Juggernaut skill (`SkillCharger`, Body tier 3) is a charge skill too; how it and Charge Stagger
  add up is not compared.

**First idea (2026-11-17, kept for reference):** instead of an on/off toggle, each feature's main setting becomes **Off / On / Power**:
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
