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

### 2b. Disarm Strike extensions (designed 2026-11-16, not built)

Three more ways to disarm, each switchable, each with its own success chance:

- **Twist Disarm** (default on, chance default **open**): the player's weapon hits the NPC's **weapon** (blade on blade, not the hand) while the blade rolls around its own long axis (wrist twist) faster than `twistMinRollSpeed` (deg/s). Total angular speed is not used, because every swing rotates around shoulder and elbow. On success the NPC drops the weapon. Whether it needs a stagger: **open**. Shares the weapon-on-weapon contact code with Clash of Might.
- **Grip Steal while staggered** (default on, chance default 50 %): the player grabs the handle of a weapon a staggered NPC holds (`Item.OnGrabEvent`); on success the NPC's hand lets go.
- **Grip Steal after hit** (default on, chance default 75 %): a hit of any kind on a staggered NPC loosens its grip; grabbing the handle then succeeds with the higher chance. Window: `loosenedGripDuration`.
- One roll per NPC per stagger / loosened window, so re-grabbing cannot retry the chance (proposal, **open**).
- After every disarm path: the same no-regrab lock as Disarm Strike.

Extras:
- Every disarm path adds to the Adrenaline meter (own gain value).
- Loosened grip is signalled with a spark and haptics.
- A disarm by twist or hit throws the weapon away with an impulse in swing direction (`disarmFlingForce`).
- Shields are excluded (`shieldsCanBeDisarmed`, default off), matching the game's own hand-hit disarm.
- Catch bonus: catching a disarmed weapon in the air before it lands adds extra Adrenaline.

Existing behaviour stays: a plain hit on a staggered NPC's hand disarms; forearm hits only with `includeForearm` (default off).

**Config:** `twistEnabled`, `twistChance`, `twistMinRollSpeed`, `gripStealStaggeredEnabled`, `gripStealStaggeredChance`, `gripStealAfterHitEnabled`, `gripStealAfterHitChance`, `loosenedGripDuration`, `disarmFlingForce`, `shieldsCanBeDisarmed`, Adrenaline gains for disarm and catch.

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
