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
| 3. Disarm Strike | not started |
| 4. Adrenaline | not started |
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

Feature settings are added to their group as each feature is built (spec build order). Clash of Might, Disarm Strike and Adrenaline have no gameplay effect yet; their toggles only switch state live and log it.

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
| Disarm | V | `RagdollHand.grabbedHandle`, `UnGrab(bool throwing)`, `TryRelease()` |
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

The harness hits the NPC through `Creature.Damage` with the player's hand as damager and `pushLevel = -1`, so the
game's own hit reaction does not push and any stagger comes from DMCS.

Not covered by the harness: how the mods page *looks* in the headset (nobody opens the book), a real arm-swing sprint,
a real physical weapon hit (held-weapon attribution) and how the stagger *feels*.

## In-game test log

| Date | Step | Result |
|---|---|---|
| 2026-11-16 | 1 | Harness: `step1-a-menu`, `-b-persist-set`, `-c-persist-check` **PASS** (70 s). First run found that saved values apply after script load; the load banner claimed default states — fixed (Settings line at level load). |
| 2026-11-16 | 2b | Presets (Gamey/Realistic/Custom), chance, min swing speed, thrown/spells, second hit knocks down. Harness: all four scenarios **PASS** (2.5 min). |
| 2026-11-16 | 2 | Harness: all four scenarios **PASS** (2 min). Found on the way: a fighting NPC (`HumanMedium`) lands real hits whose own pushes blur the result → scenario uses `HumanDummy`; runs into a wall stop the player → runs go back and forth. |
