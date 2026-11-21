using ThunderRoad;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Plays a catalog effect once at a point: only its Start parts.
    /// Collision effects such as HitMaterialMetal also carry a Loop part (the metal rubbing,
    /// Bas.AudioGroup.Collision.Rubbing.Metal, full volume at the spawn's default speed 1) that the game's collision
    /// handling ends when the contact ends. Spawned and played by a mod, nothing ends it: EffectAudio.Play despawns only
    /// Start and End parts after their clip, a Loop part loops on until the level unloads, and every spawn added one more
    /// (read in ThunderRoad.dll and Effect_HitMaterialMetal.json, game 1.3.2; heard by the user 2026-11-21).
    /// End and Custom parts would never play here either, so they go too instead of holding a pooled audio source.
    /// </summary>
    internal static class OneShotEffect
    {
        /// <summary>False when the effect is not in the catalog.</summary>
        public static bool Play(string effectId, Vector3 position)
        {
            var instance = Catalog.GetData<EffectData>(effectId, false)?.Spawn(position, Quaternion.identity);
            if (instance == null)
            {
                return false;
            }
            DropAllButStart(instance);
            instance.Play();
            return true;
        }

        private static void DropAllButStart(EffectInstance instance)
        {
            // A despawned part leaves instance.effects (EffectInstance.OnEffectDespawn), and the list goes back to the pool
            // (null) with the last part, so walk it from the end and check it on every step.
            for (var i = (instance.effects?.Count ?? 0) - 1; i >= 0; i--)
            {
                if (instance.effects == null)
                {
                    return;
                }
                if (i >= instance.effects.Count)
                {
                    continue;
                }
                var part = instance.effects[i];
                if (part != null && part.step != Effect.Step.Start)
                {
                    part.Despawn();
                }
            }
        }
    }
}
