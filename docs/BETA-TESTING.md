# DMCS beta: what to test and how to report

Thanks for testing. This is a beta. It loads and runs, and every mechanic passes automatic tests inside the running
game. Only one person has played it in VR so far, so the default values are still guesses.

## Install

1. Blade & Sorcery 1.3.2 (PC VR).
2. Copy the `DMCS` folder into `Blade & Sorcery/BladeAndSorcery_Data/StreamingAssets/Mods/`. If you have an older DMCS,
   delete its folder first.
3. In game: menu book → Mods → DMCS. If you played an older DMCS, press **Reset To Defaults** once. Saved settings keep
   their old values, and some defaults have changed.
4. Turn on **Debug Logging** (same page). It writes what DMCS decides into the game's log file, so I can see why
   something did or did not happen.

To uninstall, delete the `DMCS` folder.

## Please test these most

**Clash of Might** (the flashing blade):
- How often do you see the star? Too often, too rarely, about right?
- Block the flashing blade with **both hands** on your weapon, **blade on blade**. In *Simple* mode the enemy's sword
  should fly away. A one-handed block, or a hit on your hilt or handle, is a normal parry.
- In *On* mode the blades lock and you push in rounds. Pushing means moving your hands away from you, or to the side,
  from where they were when the blades locked. Does that feel right? Do you lose when you hold still?

**Everything else**, whatever you run into: charge stagger, disarming, parries, adrenaline and the fatality, magic with
full adrenaline, finishing a downed enemy.

## How to report

Please post in the Discord thread:
1. What you did, what you expected, and what happened.
2. The DMCS version (on the Mods page; this beta is `0.4.0-beta.1`).
3. Your log file, if something went wrong. Send it right after playing, because the game replaces it on the next start:
   `C:\Users\<you>\AppData\LocalLow\Warpfrog\BladeAndSorcery\Player.log`
4. Roughly when it happened (the log has timestamps).

## Known issues

- Clash push strength, weapon weight, star frequency and most other values are still guesses. Telling me which feel
  wrong helps the most.
- A hard enemy blow may knock one hand off a two-handed grip (seen in tests, not confirmed in VR). The next block then
  counts as one-handed.
- If you push your controller about half a metre ahead of the heavy locked sword, your hand lets go, and you lose the
  clash.
- Dodging and catching a knocked-away weapon in the air (adrenaline bonuses) have not been tested in VR at all.
