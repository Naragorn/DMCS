# DMCS — Dark Messiah Combat Systems

Dark Messiah-style combat for Blade & Sorcery. Sprint into an enemy to stagger it, knock the weapon out of its hand,
and build up adrenaline for a brutal finishing blow.

**Intended combo:** sprint → charge hit → stagger → hit the hand → disarm → catch the weapon → full adrenaline → fatality.

Every mechanic can be switched on and off and tuned in the in-game Mod Menu, and changes apply at once, mid-fight.

> **Early version (0.3.0).** Every mechanic is tested automatically inside the running game, but has not been played
> in a VR headset yet. The default values are first guesses. Feedback on how it feels is very welcome.

## Requirements and installation

- Blade & Sorcery **1.3.2** (PC VR). Built and tested against build 1.3.2.261012003; other versions are untested.
- Copy the `DMCS` folder (`DMCS.dll` + `manifest.json`) into
  `Blade & Sorcery/BladeAndSorcery_Data/StreamingAssets/Mods/`.
- The settings are on the mods page of the in-game menu book.

## What it does

### Charge Stagger

Hit an enemy while you run fast and it staggers. Weapons, fists and kicks count. Thrown weapons and spells count too if
they land while you are running. A second charge hit on an enemy that is still staggered throws it to the ground,
where it stays for a moment.

Pick a preset in the menu:
- **Gamey** (default): a fast jog and any real hit are enough, and the stagger always lands.
- **Realistic**: needs a near-full sprint and a committed swing, the stagger is shorter, and it does not always land.

Change any value and the preset shows *Custom*.

### Disarm Strike

- **Hand hit:** hit the hand of a staggered or knocked-down enemy and it drops its weapon. By default it drops
  everything it holds.
- **Body hit:** a hit anywhere else on a staggered enemy can knock the weapon loose too. The first hit has a 25 %
  chance, and every further hit in the same stagger adds 25 % (25 / 50 / 75 / 100 %). A hit that does not knock the
  weapon loose shows sparks on the enemy's weapon and gives a pulse in your hand.
- Separate chances for female enemies and for enemies shorter than you.
- **Twist (testing):** when the enemy is staggered, hit its weapon with yours while you twist your wrist, like turning a
  key, so your blade rolls around itself. It drops the weapon. Either direction counts; a plain swing does not. How fast
  the twist must be is a guess until tested in VR.
- **Fling:** a knocked-out weapon flies off in the direction of your hit.
- **Shields** stay in the hand, as in the base game, unless you allow disarming them.
- **After a disarm:** the enemy cannot pick up or draw a weapon for 10 s, or never with *Disarm Stays Permanent*. With
  *Disarmed Stand Still* it just stands there: no fleeing, no fist fighting.

### Adrenaline

Combat fills a hidden meter. It is filled by:
- hits you land
- blocks and parries
- charge staggers
- disarms
- dodges: an enemy swing misses while you step or lean away fast
- catches: you grab a weapon you knocked out of an enemy's hand before it hits the ground

Hits you take drain the meter. You can switch that so they fill it instead.

**When the meter is full** you get a short window, 6 s by default:
- **Signals:** a sound like Dark Messiah's "adrenaline reached", a heartbeat, a heartbeat pulse in both controllers,
  a glowing weapon hand, and flickering red streaks at the edge of your view.
- **Fatality:** your next **hard** hit is the critical. You shout, time slows down (Hyperfocus: the world slows and you
  keep your speed), the hit limb or head comes off, the enemy dies and is thrown back.
- **Chest stab:** a critical stab into the chest makes the enemy sink to its knees with your blade in it. Kick it away
  or cut off its head to finish it in slow motion, or let it fall.
- **Kneeling:** if you land no hard hit for 2 s, or only two light hits, the enemy goes down on its knees. Any hit then
  executes it. Wait too long and it stands up again and fights on.

The window closes unused after its time, and the meter starts over.

### Clash of Might (testing)

Dark Messiah's blade lock, made for VR. Three modes in the Mod Menu: **Off**, **Simple** (the default) and **On**.

**The warning (both modes):**
- When an enemy with a sword winds up a big swing (a long wind-up: overhead, lunging, stepping or counter swings),
  its blade flashes at the middle when it is raised all the way (like sunlight caught on it): a warm star with rays
  bursts up and keeps glittering until the swing comes, with a
  warning sound (DMCS's own, modelled on Sekiro's perilous-attack cue, and/or one of Blade & Sorcery's own).
  Plain quick swings and thrusts do not flash.
- If the enemy is close, your blade is near the flash and you hold it roughly across the swing when the strike
  comes, time slows down (0.3×) until the swing is over.

**Simple:** block the flashing swing right at the star, with your blade roughly across it, and the enemy's sword flies
out of its hand.

**On, the blade lock:** the same block locks the blades. The enemy freezes in its swing and your weapon turns heavy.
Then it is strength against strength, over a few rounds (3 to 5 by default):
- **Push:** press your weapon towards the enemy with your real hand. The enemy pushes back in a wave, swelling and
  easing off again; you hold the round if you pushed at least as hard as it did.
- **Come back:** after each round the enemy shoves back (a jolt in your hand). Bring your hand back to where the blades
  meet within a second, or you lose. Only then does the next round start.
- **Win** every round and the enemy's sword flies away; it staggers, or (25 %) drops to its knees for you to execute.
- **Lose** a round, miss the way back or let go of your weapon, and you lose: your weapon flies away and you take a
  hit (20 health), as set.
- How hard enemies push is the **Difficulty** (0–200, default 50). Taller enemies push harder and smaller ones weaker;
  female enemies push 50 % weaker (women have about 40–60 % of men's upper-body strength). Smaller and female enemies
  also last one round fewer. Each of these can be switched off or changed.
- You feel the enemy's push in the controller, sparks fly at the blades, and the blades grind (Blade & Sorcery's own
  sounds; or, with *Clash Dark Messiah Sounds*, Dark Messiah's blade lock and disarm sounds from your own install).

How hard the push should be is not tuned in the headset yet; the values may change.

## Dark Messiah's own sounds and vignette (optional)

DMCS ships only its own sounds and graphics, made to sound and look close to Dark Messiah's. Under *Ready Sound Style*,
*Heartbeat Sound* and *Vignette Style* you can instead pick **Dark Messiah**. DMCS then reads the original files from
**your own** Dark Messiah of Might and Magic install while the game runs. Nothing of Dark Messiah is included in this mod.

The install is found automatically in your Steam libraries and in the usual Steam library folders on every drive
(on Linux under Proton also on mounted drives).

Clash of Might's *Clash Dark Messiah Sounds* switch works the same way and plays Dark Messiah's own blade lock and
disarm sounds.

If it is not found, DMCS falls back to its own versions (DMCS 3, and Blade & Sorcery's heartbeat; for the clash,
Blade & Sorcery's own sounds).

## Mod Menu

All options with their defaults. Every slider shows its unit in the menu.

**General**

| Option | Default | What it does |
|---|---|---|
| Debug Logging | off | Writes stagger, disarm and meter events to `Player.log` (for bug reports) |
| Reset To Defaults | – | Sets every DMCS option back to its default |
| Body Hit Min Hand Speed | 1.5 m/s | How fast your real hand must move for a touch of your hand or arm to count as a punch (0 = every touch). Keeps enemies from disarming themselves by running into your hand |

**Clash of Might**

| Option | Default | Range | What it does |
|---|---|---|---|
| Clash of Might Mode | Simple | Off, Simple, On | Simple: a block at the star knocks the enemy's sword away. On: the blades lock and you push over a few rounds |
| Clash Only Swords | on | | Only enemies with a sword flash; off: any blade that cuts |
| Clash Min Wind Up | 0.4 s | 0.2–0.8 s | How long an enemy must wind up for its swing to flash |
| Clash Slow Motion | on | | Time slows when your blade is ready to block a flashing swing |
| Clash Slow Motion Scale | 0.3 | 0.1–0.8 | World speed during it |
| Clash Enemy Reach | 2.5 m | 1–4 m | How close the enemy must be |
| Clash Block Reach | 0.6 m | 0.2–1.5 m | How close your blade must be to the flash |
| Clash Block Angle Tolerance | 45° | 10–90° | How far your blade may be off a right angle to the swing (90°: any angle) |
| Clash Flash At Swing Top | on | | The flash comes when the blade is raised all the way, just before the strike; off: as soon as the wind-up starts (more warning) |
| Clash Point Tolerance | 0.25 m | 0.05–0.6 m | How close to the flash your block must hit |
| Clash Weapon Weight | 20× | 1–50× | How much heavier your weapon gets while the blades are locked |
| Clash Flash Sound | DMCS 1 | Off, DMCS 1 | The warning sound: a deep boom with a metallic ring, after Sekiro's perilous-attack cue |
| Clash Flash Game Sound | Off | Off, Metal Hit, Focus Ready, Gravity Pulse | One of the game's own sounds on top (Metal Hit also sparks) |
| Clash Flash Sound Volume | 0.8 | 0–1 | Volume of the DMCS sound |
| Clash Difficulty | 50 | 0–200 | How hard enemies push back in a lock: 0 barely, 100 twice as hard as 50, 200 you hardly have a chance |
| Clash Difficulty Scales With Height | on | | Taller enemies push harder, smaller ones weaker (in proportion), and smaller ones last fewer rounds |
| Clash Difficulty Scales With Gender | on | | Female enemies push weaker and last fewer rounds |
| Clash Female Strength Reduction | 50 % | 0–90 % | How much weaker a female enemy pushes |
| Clash Min Rounds | 3 | 1–10 | Fewest rounds of a lock (each lock picks a random number between min and max) |
| Clash Max Rounds | 5 | 1–10 | Most rounds of a lock |
| Clash Fewer Rounds Against Weaker | 1 | 0–5 | Rounds fewer against a smaller or female enemy (at least one stays) |
| Clash Enemy Push | 0.06 m | 0.01–0.2 m | How far your hand must lead your heavy weapon towards the enemy to match its push at its strongest (at difficulty 50) |
| Clash Push Dead Zone | 0.03 m | 0–0.1 m | Lead that does not count as pushing (the heavy weapon's natural lag) |
| Clash Push Phase | 2 s | 0.5–5 s | Length of one push round |
| Clash Return Window | 1 s | 0.25–3 s | Time to bring your hand back to the blades after a round |
| Clash Return Tolerance | 0.08 m | 0.02–0.3 m | How close your hand must come back to your weapon |
| Clash Win Disarms | on | | Winning a lock knocks the enemy's sword away |
| Clash Win Kneel Chance | 25 % | 0–100 % | Chance a beaten enemy kneels for an execution instead of staggering |
| Clash Lose Outcome | Disarm + Hit | Disarm + Hit, Disarm, Hit, None | What a lost lock costs you |
| Clash Lose Damage | 20 | 0–100 | Health the hit costs (you have 100) |
| Clash Haptics Strength | 0.6 | 0–1 | Vibration in your weapon hand during a lock |
| Clash Dark Messiah Sounds | off | | Dark Messiah's own blade lock and disarm sounds from your install |

**Disarm Strike**

| Option | Default | Range | What it does |
|---|---|---|---|
| Enabled | on | | Hand hits on staggered enemies disarm them |
| Disarm Requires Stagger | on | | Off: any hand hit can disarm |
| Disarm Include Forearm | off | | Forearm hits count as hand hits |
| Disarm Drops Both Hands | on | | Off: only the hit hand lets go |
| Disarm Chance | 100 % | 0–100 % | Chance that a hand hit disarms |
| Disarm No Regrab Time | 10 s | 2–60 s | How long a disarmed enemy cannot re-arm |
| Disarm Stays Permanent | off | | A disarmed enemy never re-arms |
| Disarmed Stand Still | on | | A disarmed enemy just stands there |
| Body Hit Drop | on | | Body hits on staggered enemies can disarm too |
| Body Hit Drop Chance | 25 % | 0–100 % | Chance of the first body hit in a stagger |
| Drop Chance Per Extra Hit | 25 % | 0–100 % | Added for each further body hit |
| Loosened Grip Spark | on | | Sparks when a body hit does not disarm |
| Loosened Grip Haptics | on | | Pulse in your hand when a body hit does not disarm |
| Shields Can Be Disarmed | off | | Shields can be knocked away too |
| Disarm Fling Force | 3 m/s | 0–10 m/s | How far a knocked-out weapon flies (0 = it just drops) |
| Body Hit Drop Chance Female | 25 % | 0–100 % | First body hit chance for female enemies |
| Body Hit Drop Chance Smaller | 25 % | 0–100 % | First body hit chance for enemies shorter than you |
| Smaller By | 0 cm | 0–50 cm | How much shorter an enemy must be for the chance above |
| Twist Disarm | on | | A wrist twist on a staggered enemy's weapon knocks it out (with *Disarm Requires Stagger* off: any enemy) |
| Twist Disarm Chance | 100 % | 0–100 % | Chance a fast enough twist disarms |
| Twist Min Roll Speed | 400°/s | 100–1500°/s | How fast your blade must roll around itself when it meets the enemy's weapon |

The highest chance that fits the enemy applies. Base 0 % with a female chance of 25 % means only female enemies drop
on body hits.

**Charge Stagger**

| Option | Default (Gamey) | Realistic | Range | What it does |
|---|---|---|---|---|
| Enabled | on | | | Hits while running stagger |
| Charge Stagger Preset | Gamey | | Gamey, Realistic, Custom | Sets the six values below |
| Charge Stagger Chance | 100 % | 75 % | 0–100 % | Chance that a charge hit staggers |
| Charge Speed Threshold | 4 m/s | 5.5 m/s | 1–8 m/s | How fast you must run (walking ≈ 2.8, full sprint ≈ 6) |
| Charge Speed Window | 0.5 s | 0.15 s | 0–1 s | How long before the hit your top speed still counts |
| Charge Min Swing Speed | 1 m/s | 3 m/s | 0–10 m/s | How fast the hit itself must be |
| Charge Thrown And Spells Count | on | | | Thrown weapons and spells stagger too |
| Charge Stagger Strength | Full | Medium | Light, Medium, Full, Knock Down | How hard the enemy is staggered |
| Charge Stagger Duration | 2.5 s | 1.25 s | 0.5–5 s | How long the enemy counts as staggered (for disarms); how long a knocked-down enemy stays down |
| Charge Second Hit Knocks Down | on | | | A charge hit on a staggered enemy throws it down |

**Adrenaline**

| Option | Default | Range | What it does |
|---|---|---|---|
| Enabled | on | | The meter and everything below |
| Adrenaline Meter Max | 100 | 50–300 | Points for a full meter |
| Adrenaline Window | 6 s | 2–20 s | How long a full meter waits for your critical |
| Adrenaline Decay | off | | The meter drains over time |
| Adrenaline Decay Per Second | 2 | 0–20 | How fast it drains |
| Adrenaline Decay Delay | 0 s | 0–30 s | Seconds after the last gain before it drains |
| Adrenaline Per Hit | 5 | 0–50 | Points per hit you land |
| Adrenaline Per Block Or Parry | 10 | 0–50 | Points per enemy attack you stop |
| Adrenaline Per Damage Taken | 5 | 0–50 | Points per hit you take |
| Damage Taken Lowers Adrenaline | on | | Hits you take drain instead of fill |
| Adrenaline Per Charge Stagger | 15 | 0–50 | Points per charge stagger |
| Adrenaline Per Disarm | 15 | 0–50 | Points per disarm |
| Adrenaline Per Dodge | 10 | 0–50 | Points per enemy swing you dodge |
| Dodge Min Speed | 2 m/s | 0.5–6 m/s | How fast your head must move for a dodge |
| Adrenaline Per Catch | 10 | 0–50 | Points per knocked-out weapon caught in the air |
| Catch Max Air Time | 3 s | 0.5–5 s | How long after the disarm a catch still counts |
| Adrenaline Per Clash Win | 25 | 0–50 | Points for a won Clash of Might (also the simple one) |
| Critical Time Mode | Hyperfocus | Hyperfocus, Slow Time | Hyperfocus: the world slows, you don't. Slow Time: you slow too |
| Critical Hyperfocus Time Scale | 0.1 | 0.05–0.5 | World speed in Hyperfocus |
| Critical Hyperfocus Duration | 3 s | 0.5–10 s | How long Hyperfocus lasts |
| Critical Slow Time Scale | 0.5 | 0.25–0.75 | Speed in Slow Time |
| Critical Slow Time Duration | 3 s | 0.5–10 s | How long Slow Time lasts |
| Critical Needs Hard Swing | on | | Only a hard hit is the critical |
| Critical Min Swing Speed | 3 m/s | 0–10 m/s | How hard "hard" is |
| Critical Thrown And Spells | off | | Thrown weapons and spells can be the critical |
| Critical Kills | on | | The critical always kills |
| Critical Dismember | on | | The critical cuts off the hit head, arm or leg |
| Critical Knockback | 8 m/s | 0–20 m/s | How hard the critical throws the enemy |
| Critical Battle Cry | on | | You shout on the critical |
| Chest Stab Kneels | on | | A critical chest stab makes the enemy kneel and die |
| Execution Window | 5 s | 1–15 s | How long a kneeling enemy waits for the finish |
| Kneel And Execution | on | | A full meter without hard hits makes the enemy kneel |
| Kneel After No Hit | 2 s | 0.5–5 s | Seconds without a hit before it kneels |
| Kneel After Light Hits | 2 | 1–10 | Light hits before it kneels |
| Kneel Death Chance | 0 % | 0–100 % | Chance it dies instead of standing up again |
| Adrenaline Ready Sound | on | | Sound when the meter is full |
| Adrenaline Ready Sound Style | DMCS 3 | DMCS 1–3, Dark Messiah | Which sound (DMCS 1–3 are DMCS's own versions) |
| Adrenaline Ready Sound Volume | 0.6 | 0–1 | |
| Adrenaline Heartbeat | on | | Heartbeat while the meter is full |
| Adrenaline Heartbeat Sound | Blade & Sorcery | Blade & Sorcery, Dark Messiah | Which heartbeat |
| Adrenaline Heartbeat Volume | 0.5 | 0–1 | |
| Adrenaline Haptics | on | | Heartbeat pulse in both controllers |
| Adrenaline Haptics Strength | 0.5 | 0–1 | |
| Adrenaline Weapon Glow | on | | Your weapon hand glows |
| Adrenaline Vignette | on | | Red streaks at the edge of your view |
| Adrenaline Vignette Style | DMCS 3 | DMCS 1–4, Dark Messiah | 1 flames, 2 streaks, 3 patchy streaks, 4 closest to Dark Messiah |
| Adrenaline Vignette Intensity | 1 | 0–1 | Brightness |
| Adrenaline Vignette Hue | 6° | 0–40° | 0 = red, 40 = orange |
| Adrenaline Vignette Streaks | 70 | 2–100 | Streaks per frame |
| Adrenaline Vignette Streak Length | 0.4 | 0.1–0.6 | How far they reach in |
| Adrenaline Vignette Size | 50° | 30–70° | Smaller = further into view |
| Adrenaline Vignette Frame Rate | 25 fps | 5–60 fps | How fast they flicker |
| Adrenaline Vignette Flare | on | | The streaks flare up on the critical |

Picking a DMCS vignette style sets *Streaks*, *Streak Length* and *Hue* to how that version was made.

## Reporting bugs

Turn on *Debug Logging*, play until the problem shows, and send the
`Player.log`. On Windows it is in
`%USERPROFILE%\AppData\LocalLow\Warpfrog\BladeAndSorcery\`.

## For mod authors

Other mods can add to the Adrenaline meter without a hard dependency, by finding this method through reflection:

```csharp
// DMCS.Adrenaline in DMCS.dll; returns false when Adrenaline is off, amount <= 0 or the window is already open.
public static bool AddExternal(float amount, string source)
```

This name and signature stay stable.

## Development

Build steps, the game APIs used, how each mechanic works inside, the automated in-game tests and the test log are in
[`docs/IMPLEMENTATION.md`](docs/IMPLEMENTATION.md). The full spec is in `CLAUDE.md`.
