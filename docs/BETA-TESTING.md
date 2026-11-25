# DMCS beta: what to test and how to report

Thanks for testing. This is a beta. It loads and runs, and every mechanic passes automatic tests inside the running
game. Only one person has played it in VR so far, so the default values are still guesses.

## New in beta.2

- Stabs no longer show the clash star. The game labels one of its stabs a swing, so it flashed.
- This page now says how to trigger each feature.

## Install

1. Blade & Sorcery 1.3.2 (PC VR).
2. Copy the `DMCS` folder into `Blade & Sorcery/BladeAndSorcery_Data/StreamingAssets/Mods/`. If you have an older DMCS,
   delete its folder first.
3. In game: menu book → Mods → DMCS. If you played an older DMCS, press **Reset To Defaults** once. Saved settings keep
   their old values, and some defaults have changed.
4. Turn on **Debug Logging** (same page). It writes what DMCS decides into the game's log file, so I can see why
   something did or did not happen.

To uninstall, delete the `DMCS` folder.

## How to trigger each feature

With the default settings:

- **Charge Stagger:** run at 4 m/s or faster (a fast jog) and hit an enemy with your weapon, fist or kick. It
  staggers. Charge into it again while it is still staggered and it goes down.
- **Disarm Strike:** stagger the enemy first, for example with a charge or a counter parry. Then hit its weapon hand
  and it drops the weapon. A body hit on a staggered enemy may also make it drop the weapon: 25 % on the first hit,
  more with each further hit.
- **Twist Disarm:** stagger the enemy. Then strike its weapon with your blade while you twist your wrist fast, as if
  turning a key hard. The weapon flies out of its hand.
- **Counter Parry:** move your weapon into the enemy's swing just in time, at 1 m/s or faster, instead of holding a
  still guard. The enemy staggers.
- **Power Parry:** swing hard into the enemy's attack, at 3 m/s or faster. Its weapon flies away.
- **Clash of Might:** when an enemy winds up a big sword swing, a star flashes on its blade. Block that blade with both
  hands on your weapon, so the blades meet blade on blade.
  - *Simple* mode (default): its sword flies away.
  - *On* mode: the blades lock. Push by moving your hands away from you or to the side. Win every round, or lose your
    sword and take a hit.
  - A one-handed block, or a blow that lands on your hilt or handle, is a normal parry.
- **Adrenaline:** the meter fills as you fight: hits, blocks, staggers, disarms, dodges, catching a knocked-away weapon
  in the air, and clash wins. Hits you take drain it. When it is full, you hear a roar and the screen edges turn red.
  - Your next hard hit, at 3 m/s or faster, is a fatality: slow motion, the hit limb comes off, the enemy dies.
  - Land no hard hit for 2 s, or only 2 light hits, and an enemy near you (within 3 m) kneels. Hit it to execute it.
- **Adrenaline Magic:** with a full meter, hit an enemy with a spell. Fire burns it up and sets enemies near it on
  fire. Lightning jumps to nearby enemies. Gravity flings everyone around you away. Telekinesis lets you grab an enemy
  and throw it to its death.
- **Finisher:** knock an enemy to the ground, then stab it in the chest with a blade. It dies, no adrenaline needed.
- **Double Fatality** (off by default): a full meter gives two fatalities in a row.
- **Skill Tree** (off by default): turn on a feature's *Needs Skill* toggle and that feature only works once you have
  bought its skill in the Body tree.

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
2. The DMCS version (on the Mods page; this beta is `0.4.0-beta.2`).
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
