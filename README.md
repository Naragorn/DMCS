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
| 2. Charge Stagger | not started |
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
| `src/DMCS.Core` | Pure logic (no game references): logger gate, feature toggles, reset logic, category names. Compiled into `DMCS.dll`. |
| `src/DMCS` | The mod: `ThunderScript` entry point, Mod Menu options, one class per feature. Target `net472` (same as working B&S script mods, e.g. HealthRegen.dll). |
| `tests/DMCS.Tests` | xUnit. Core logic flows + a metadata test that inspects the built `DMCS.dll` the way the game's ModManager does (via `MetadataLoadContext`, no game code executed). Needs the game install. |

## Mod Menu (step 1)

| Category | Option | Type | Default |
|---|---|---|---|
| General | Debug Logging | toggle | off |
| General | Reset To Defaults | button | – |
| Clash of Might | Enabled | toggle | on |
| Disarm Strike | Enabled | toggle | on |
| Charge Stagger | Enabled | toggle | on |
| Adrenaline | Enabled | toggle | on |

Feature settings are added to their group as each feature is built (spec build order). The feature toggles have no gameplay effect yet; they only switch state live and log it.

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
| Reset: `ModManager.ModData.modOptions`, `ModOption.defaultValueIndex`, `parameterValues`, `Apply(int)`, `uiComponent.Reload()` | V + **harness** (values) / **A** (redraw) | `ModManager.cs:150`, `ModOption.cs`, `UIList.Reload` = `InitValues + LoadValue(default) + Refresh`. Harness: "Reset 5 options to defaults.", values and members back at default. Whether an **open** menu page redraws is still a headset check. |
| `UnityEngine.Debug.Log` | V | `UnityEngine.CoreModule`. |
| Manifest format (`Name, Description, Author, ModVersion, GameVersion`) | V | Official wiki JSON-modding page and installed working mods (all use `"GameVersion": "1.0.0.0"`). |

Game quirk found: `UIButtonListModOption` feeds `nameLocalizationId` into the **tooltip** localisation, so button options must not set `nameLocalizationId` (enforced by a test).

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
| Stagger state | V (exists) / **A** (semantics) | `Brain.currentStagger` (enum `Brain.Stagger`), `BrainModuleHitReaction.SetStagger/StopStagger`. Which value means "is staggered" is decided in step 2. |
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
| `step1-a-menu` | mod + script load; effective settings line; all 6 options with category, displayed title (`Enabled` from the game's localisation), type, interaction, save flag, tooltip; defaults; Debug Logging on → toggle change logged live; Debug Logging off → toggle still switches, no debug line; Reset To Defaults restores values and members; screenshot |
| `step1-b-persist-set` | set Debug Logging on + Disarm Strike off, save as closing the menu does |
| `step1-c-persist-check` | new game start: both values back (menu value and the live member) |

Not covered by the harness: how the mods page *looks* in the headset (nobody opens the book) and anything
needing controller input.

## In-game test log

| Date | Step | Result |
|---|---|---|
| 2026-11-16 | 1 | Harness: `step1-a-menu`, `-b-persist-set`, `-c-persist-check` **PASS** (70 s). First run found that saved values apply after script load; the load banner claimed default states — fixed (Settings line at level load). |
