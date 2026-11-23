using System;
using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>
    /// Finisher on a downed enemy (user 2026-11-23, default on; rule FinisherRule): a stab with a weapon in the player's
    /// hand into the torso of a living enemy lying on the ground (its ragdoll destabilized or inert, as after Charge
    /// Stagger's knock-down, a gravity push or a fall) kills it, without adrenaline. A kneeling enemy is the adrenaline
    /// execution's (AdrenalineCritical) and is left alone.
    /// </summary>
    public static class Finisher
    {
        private const string Channel = "Finisher";
        private static bool _hooked;

        [ModOption(name = FinisherSettings.EnabledName, nameLocalizationId = DmcsCategories.EnabledLocalizationId, tooltip = "Stab an enemy lying on the ground in the chest and it dies, no adrenaline needed.", category = DmcsCategories.Finisher, categoryOrder = DmcsCategories.FinisherOrder, order = 0)]
        public static bool Enabled { get; set; } = FinisherSettings.DefaultEnabled;

        public static void Start()
        {
            if (_hooked)
            {
                return;
            }
            EventManager.onCreatureHit += OnCreatureHit;
            _hooked = true;
        }

        public static void Stop()
        {
            if (!_hooked)
            {
                return;
            }
            EventManager.onCreatureHit -= OnCreatureHit;
            _hooked = false;
        }

        private static void OnCreatureHit(Creature target, CollisionInstance hit, EventTime eventTime)
        {
            try
            {
                if (eventTime != EventTime.OnEnd || target == null || hit == null || target.ragdoll == null)
                {
                    return;
                }
                var part = hit.damageStruct.hitRagdollPart;
                var state = target.ragdoll.state;
                var decision = FinisherRule.Decide(new FinisherHit(
                    Enabled,
                    HitInfo.SourceOf(hit),
                    target.isPlayer,
                    target.isKilled,
                    state == Ragdoll.State.Destabilized || state == Ragdoll.State.Inert,
                    AdrenalineCritical.IsKneeling(target) || AdrenalineCritical.IsFinishable(target),
                    part != null && part.type == RagdollPart.Type.Torso,
                    hit.damageStruct.penetration != DamageStruct.Penetration.None || hit.damageStruct.damageType == DamageType.Pierce));
                if (decision == FinisherDecision.Finish)
                {
                    target.Kill(hit);
                    Dmcs.Log.Debug(Channel, "finished " + target.name + " on the ground with a stab into the chest");
                }
                else if (decision == FinisherDecision.NotTorso || decision == FinisherDecision.NotAStab || decision == FinisherDecision.Kneeling)
                {
                    Dmcs.Log.Debug(Channel, "no finisher on " + target.name + " (on the ground): " + decision);
                }
            }
            catch (Exception e)
            {
                Dmcs.Log.Info("Finisher error: " + e);
            }
        }
    }
}
