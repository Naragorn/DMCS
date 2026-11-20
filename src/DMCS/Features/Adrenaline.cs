using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DMCS.Core;
using ThunderRoad;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Adrenaline (steps 4a–4d): combat fills a meter; when it is full a window opens, and the next qualifying hit is
    /// the critical (<see cref="AdrenalineCritical"/>).
    /// Sources: player hits on NPCs and NPC damage on the player (EventManager.onCreatureHit), blocks and parries
    /// (EventManager.onCreatureAttackParry, raised by BrainModuleMelee/BrainModuleDefense), charge staggers and
    /// disarms (DMCS's own events). Clock: real time (Time.unscaledTime), so slow-mo does not stretch the window.
    /// </summary>
    public static class Adrenaline
    {
        private const string Channel = "Adrenaline";

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.Adrenaline, true, Dmcs.Log);

        private static readonly AdrenalineMeter Meter = new AdrenalineMeter();

        /// <summary>Hits judged critical at their start (before the game's damage), executed at their end.</summary>
        private static readonly HashSet<CollisionInstance> PendingCritical = new HashSet<CollisionInstance>();

        /// <summary>Step 4c: who kneels and when (the enemy hit last; no hit or only light hits in the window).</summary>
        private static readonly KneelTracker<int> Kneel = new KneelTracker<int>();

        private static readonly Dictionary<int, Creature> HitCreatures = new Dictionary<int, Creature>();
        private static bool _hooked;
        private static float _meterMax = AdrenalineSettings.MeterMax.Default;

        [ModOption(
            name = DmcsCategories.Adrenaline + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Combat fills a meter; when full, your next hit is a critical.",
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.MenuEnabled;
            set => Toggle.Set(value);
        }

        [ModOption(
            name = AdrenalineSettings.MeterMaxName,
            tooltip = "Points the meter needs to be full.",
            valueSourceName = nameof(MeterMaxValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 1)]
        [ModOptionSlider]
        public static float MeterMax
        {
            get => _meterMax;
            set
            {
                _meterMax = value;
                Meter.Cap(value);
            }
        }

        [ModOption(
            name = AdrenalineSettings.WindowDurationName,
            tooltip = "Seconds a full meter waits for your critical before it empties.",
            valueSourceName = nameof(WindowDurationValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 2)]
        [ModOptionSlider]
        public static float WindowDuration { get; set; } = AdrenalineSettings.WindowDuration.Default;

        [ModOption(
            name = AdrenalineSettings.DecayEnabledName,
            tooltip = "The meter drains over time while it is not full.",
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 3)]
        public static bool DecayEnabled { get; set; } = AdrenalineSettings.DefaultDecayEnabled;

        [ModOption(
            name = AdrenalineSettings.DecayPerSecondName,
            tooltip = "Points per second the meter loses while it is not full. 0 = it never drains.",
            valueSourceName = nameof(DecayPerSecondValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 4)]
        [ModOptionSlider]
        public static float DecayPerSecond { get; set; } = AdrenalineSettings.DecayPerSecond.Default;

        [ModOption(
            name = AdrenalineSettings.DecayDelayName,
            tooltip = "Seconds after the last gain before the meter starts to drain.",
            valueSourceName = nameof(DecayDelayValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 5)]
        [ModOptionSlider]
        public static float DecayDelay { get; set; } = AdrenalineSettings.DecayDelay.Default;

        [ModOption(
            name = AdrenalineSettings.GainHitName,
            tooltip = "Points for each hit you land on an enemy (weapon, fist, kick, thrown, spell).",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 6)]
        [ModOptionSlider]
        public static int GainHit { get; set; } = AdrenalineSettings.DefaultGainHit;

        [ModOption(
            name = AdrenalineSettings.GainBlockParryName,
            tooltip = "Points for each enemy attack you stop with your weapon or shield.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 7)]
        [ModOptionSlider]
        public static int GainBlockParry { get; set; } = AdrenalineSettings.DefaultGainBlockParry;

        [ModOption(
            name = AdrenalineSettings.GainDamageTakenName,
            tooltip = "Points for each hit an enemy lands on you (taken off or added, see the next option).",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 8)]
        [ModOptionSlider]
        public static int GainDamageTaken { get; set; } = AdrenalineSettings.DefaultGainDamageTaken;

        [ModOption(
            name = AdrenalineSettings.DamageTakenLowersName,
            tooltip = "Hits you take lower the meter by 'Per Damage Taken' instead of raising it.",
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 9)]
        public static bool DamageTakenLowers { get; set; } = AdrenalineSettings.DefaultDamageTakenLowers;

        [ModOption(
            name = AdrenalineSettings.GainChargeStaggerName,
            tooltip = "Points for each enemy you stagger with a charge.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 10)]
        [ModOptionSlider]
        public static int GainChargeStagger { get; set; } = AdrenalineSettings.DefaultGainChargeStagger;

        [ModOption(
            name = AdrenalineSettings.GainDisarmName,
            tooltip = "Points for each enemy you disarm.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 11)]
        [ModOptionSlider]
        public static int GainDisarm { get; set; } = AdrenalineSettings.DefaultGainDisarm;

        [ModOption(
            name = AdrenalineSettings.GainDodgeName,
            tooltip = "Points for each enemy attack that misses you while you move away fast (needs the headset to judge).",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 12)]
        [ModOptionSlider]
        public static int GainDodge { get; set; } = AdrenalineSettings.DefaultGainDodge;

        [ModOption(
            name = AdrenalineSettings.DodgeMinSpeedName,
            tooltip = "How fast your head must move sideways or back (m/s) while an attack misses, to count as a dodge.",
            valueSourceName = nameof(DodgeMinSpeedValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 13)]
        [ModOptionSlider]
        public static float DodgeMinSpeed { get; set; } = AdrenalineSettings.DodgeMinSpeed.Default;

        [ModOption(
            name = AdrenalineSettings.GainCatchName,
            tooltip = "Points for catching a weapon you knocked out of an enemy's hand before it hits the ground.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 14)]
        [ModOptionSlider]
        public static int GainCatch { get; set; } = AdrenalineSettings.DefaultGainCatch;

        [ModOption(
            name = AdrenalineSettings.CatchMaxAirTimeName,
            tooltip = "Seconds after the disarm a catch still counts (besides: it must not have touched the ground).",
            valueSourceName = nameof(CatchMaxAirTimeValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 15)]
        [ModOptionSlider]
        public static float CatchMaxAirTime { get; set; } = AdrenalineSettings.CatchMaxAirTime.Default;

        [ModOption(
            name = AdrenalineSettings.GainClashWinName,
            tooltip = "Points for winning a Clash of Might (also the simple one: blocking at the star).",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 16)]
        [ModOptionSlider]
        public static int GainClashWin { get; set; } = AdrenalineSettings.DefaultGainClashWin;

        public static ModOptionFloat[] DodgeMinSpeedValues() => Floats(AdrenalineSettings.DodgeMinSpeed, "0.00", " m/s");

        public static ModOptionFloat[] CatchMaxAirTimeValues() => Floats(AdrenalineSettings.CatchMaxAirTime, "0.0", " s");

        public static ModOptionFloat[] MeterMaxValues() => Floats(AdrenalineSettings.MeterMax, "0", "");

        public static ModOptionFloat[] WindowDurationValues() => Floats(AdrenalineSettings.WindowDuration, "0", " s");

        public static ModOptionFloat[] DecayPerSecondValues() => Floats(AdrenalineSettings.DecayPerSecond, "0.0", " /s");

        public static ModOptionFloat[] DecayDelayValues() => Floats(AdrenalineSettings.DecayDelay, "0", " s");

        public static ModOptionInt[] GainValues() =>
            AdrenalineSettings.GainValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture), v)).ToArray();

        private static AdrenalineGains Gains =>
            new AdrenalineGains(GainHit, GainBlockParry, GainDamageTaken, GainChargeStagger, GainDisarm, GainDodge, GainCatch, GainClashWin);

        /// <summary>Raised for each critical (later steps hook in here).</summary>
        public static event Action<Creature> Critical;

        /// <summary>The meter is full and waits for the critical.</summary>
        public static bool IsReady => Meter.State == AdrenalineState.Ready;

        public static void Start()
        {
            Toggle.Changed -= OnToggle;
            Toggle.Changed += OnToggle;
            if (_hooked)
            {
                return;
            }
            EventManager.onCreatureHit += OnCreatureHit;
            EventManager.onCreatureAttackParry += OnParry;
            ChargeStagger.Staggered += OnChargeStagger;
            DisarmStrike.Disarmed += OnDisarm;
            AdrenalineDodgeCatch.Hook();
            _hooked = true;
        }

        public static void Stop()
        {
            Toggle.Changed -= OnToggle;
            if (_hooked)
            {
                EventManager.onCreatureHit -= OnCreatureHit;
                EventManager.onCreatureAttackParry -= OnParry;
                ChargeStagger.Staggered -= OnChargeStagger;
                DisarmStrike.Disarmed -= OnDisarm;
                AdrenalineDodgeCatch.Unhook();
                _hooked = false;
            }
            ResetAll();
        }

        /// <summary>Level unloading or feature off: the meter starts empty.</summary>
        public static void ResetAll()
        {
            Meter.Reset();
            PendingCritical.Clear();
            Kneel.WindowClosed();
            HitCreatures.Clear();
            AdrenalineCritical.StopAll();
            AdrenalineSignals.StopAll();
            AdrenalineDodgeCatch.ResetAll();
        }

        public static void Update()
        {
            PendingCritical.Clear();
            AdrenalineCritical.Update();
            AdrenalineSignals.Update(Toggle.Enabled, IsReady, AdrenalineCritical.HasExecutionWindow);
            if (!Toggle.Enabled)
            {
                return;
            }
            AdrenalineDodgeCatch.Update();
            var e = Meter.Tick(Time.unscaledTime, Time.unscaledDeltaTime, DecayEnabled ? DecayPerSecond : 0f, DecayDelay);
            if (e == AdrenalineEvent.WindowExpired)
            {
                Kneel.WindowClosed();
                Dmcs.Log.Debug(Channel, "window over unused: meter emptied");
            }
            else if (Meter.State == AdrenalineState.Ready)
            {
                CheckKneel();
            }
            else if (e == AdrenalineEvent.Drained)
            {
                Dmcs.Log.Debug(Channel, "meter drained to 0");
            }
        }

        private static void OnToggle(bool enabled)
        {
            if (!enabled)
            {
                ResetAll();
            }
        }

        private static void OnCreatureHit(Creature target, CollisionInstance hit, EventTime eventTime)
        {
            try
            {
                if (target == null || hit == null)
                {
                    return;
                }
                var source = HitInfo.SourceOf(hit);
                if (target.isPlayer && source == HitSource.NotPlayer)
                {
                    AdrenalineDodgeCatch.PlayerHit(hit);
                }
                if (eventTime == EventTime.OnStart)
                {
                    if (AdrenalineCritical.IsKneeling(target))
                    {
                        AdrenalineCritical.Prepare(target, hit);
                        return;
                    }
                    var atStart = AdrenalineCritical.Decide(Toggle.Enabled, Meter.State == AdrenalineState.Ready, target, hit, source);
                    if (atStart == CriticalDecision.Critical)
                    {
                        PendingCritical.Add(hit);
                        AdrenalineCritical.Prepare(target, hit);
                    }
                    else if (atStart == CriticalDecision.TooLight || atStart == CriticalDecision.Ranged)
                    {
                        Dmcs.Log.Debug(Channel, "no critical on " + target.name + ": " + atStart + " (swing "
                            + hit.impactVelocity.magnitude.ToString("0.00", CultureInfo.InvariantCulture) + " m/s)");
                    }
                    return;
                }
                if (AdrenalineCritical.TryFinish(target, hit, source))
                {
                    return;
                }
                if (AdrenalineCritical.TryExecute(target, hit, source))
                {
                    AdrenalineSignals.Flare();
                    Critical?.Invoke(target);
                    return;
                }
                if (PendingCritical.Remove(hit) && Meter.Consume())
                {
                    Kneel.WindowClosed();
                    AdrenalineCritical.Execute(target, hit, source);
                    AdrenalineSignals.Flare();
                    Critical?.Invoke(target);
                    return;
                }
                if (source != HitSource.NotPlayer && !target.isPlayer && !target.isKilled)
                {
                    var id = target.GetInstanceID();
                    HitCreatures[id] = target;
                    var light = AdrenalineCritical.NeedsHardSwing && hit.impactVelocity.magnitude < AdrenalineCritical.MinSwingSpeed;
                    Kneel.PlayerHit(id, Time.unscaledTime, light);
                }
                var gain = AdrenalineSources.FromHit(true, source, target.isPlayer, hit.damageStruct.damage);
                if (gain == AdrenalineSource.DamageTaken && DamageTakenLowers)
                {
                    Lose(target.name);
                }
                else if (gain.HasValue)
                {
                    Add(gain.Value, target.isPlayer ? "you" : target.name);
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline error: " + ex);
            }
        }

        /// <summary>Step 4c: with the window open, no hit for a while or only light hits put the enemy on its knees.</summary>
        private static void CheckKneel()
        {
            var reason = Kneel.Check(Time.unscaledTime, AdrenalineCritical.KneelEnabled, AdrenalineCritical.KneelAfterNoHit,
                AdrenalineCritical.KneelAfterLightHits);
            if (reason == KneelReason.None)
            {
                return;
            }
            var target = KneelTarget();
            if (target == null || !AdrenalineCritical.KneelAlive(target, reason))
            {
                return;
            }
            Meter.Consume();
            Kneel.WindowClosed();
        }

        /// <summary>The enemy hit last, if alive; else the nearest living enemy that targets the player.</summary>
        private static Creature KneelTarget()
        {
            if (Kneel.HasLastHit && HitCreatures.TryGetValue(Kneel.LastHit, out var last))
            {
                if (last != null && !last.isKilled)
                {
                    return last;
                }
                Kneel.Forget(Kneel.LastHit);
            }
            var player = Player.currentCreature;
            if (player == null)
            {
                return null;
            }
            return Creature.allActive
                .Where(c => c != null && !c.isPlayer && !c.isKilled && c.brain != null && c.brain.currentTarget == player)
                .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude)
                .FirstOrDefault();
        }

        private static void Lose(string who)
        {
            if (!Toggle.Enabled)
            {
                return;
            }
            if (Meter.Lose(GainDamageTaken) == AdrenalineEvent.Lost)
            {
                Dmcs.Log.Debug(Channel, "-" + GainDamageTaken + " DamageTaken (" + who + "): "
                    + Meter.Value.ToString("0", CultureInfo.InvariantCulture) + "/" + MeterMax.ToString("0", CultureInfo.InvariantCulture));
            }
        }

        private static void OnParry(Creature parried, Item parriedItem, Creature parrying, Item parryingItem, CollisionInstance hit)
        {
            try
            {
                if (parried != null && parrying != null && parrying.isPlayer)
                {
                    AdrenalineDodgeCatch.Parried(parried);
                }
                var source = AdrenalineSources.FromParry(parried != null && parried.isPlayer, parrying != null && parrying.isPlayer);
                if (source.HasValue)
                {
                    Add(source.Value, parried != null ? parried.name : "?");
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline error: " + ex);
            }
        }

        private static void OnChargeStagger(Creature target) => Add(AdrenalineSource.ChargeStagger, target != null ? target.name : "?");

        private static void OnDisarm(Creature target) => Add(AdrenalineSource.Disarm, target != null ? target.name : "?");

        internal static void Add(AdrenalineSource source, string who) => Gain(Gains.For(source), source.ToString(), who);

        /// <summary>
        /// Public API for other mods (called by name through reflection, e.g. the Achilles Tendon mod for a heel cut or the
        /// kill of a crippled NPC): adds <paramref name="amount"/> to the meter as any DMCS source does, and opens the
        /// window when it fills. Keep the name and signature stable.
        /// </summary>
        /// <returns>false when Adrenaline is off, the amount is not positive, or the meter is already full (window open).</returns>
        public static bool AddExternal(float amount, string source)
        {
            if (!AdrenalineExternal.Accepts(Toggle.Enabled, amount))
            {
                return false;
            }
            return Gain(amount, AdrenalineExternal.Label(source), string.Empty);
        }

        /// <summary>The one place a gain goes into the meter (DMCS sources and other mods).</summary>
        private static bool Gain(float amount, string label, string who)
        {
            if (!Toggle.Enabled)
            {
                return false;
            }
            var e = Meter.Gain(amount, Time.unscaledTime, MeterMax, WindowDuration);
            if (e == AdrenalineEvent.None)
            {
                return false;
            }
            Dmcs.Log.Debug(Channel, "+" + amount.ToString("0.##", CultureInfo.InvariantCulture) + " " + label + " (" + who + "): "
                + Meter.Value.ToString("0", CultureInfo.InvariantCulture) + "/" + MeterMax.ToString("0", CultureInfo.InvariantCulture));
            if (e == AdrenalineEvent.Filled)
            {
                Kneel.WindowOpened(Time.unscaledTime);
                Dmcs.Log.Debug(Channel, "meter full: window open for " + WindowDuration.ToString("0", CultureInfo.InvariantCulture) + " s");
                AdrenalineSignals.WindowOpened();
            }
            return true;
        }

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();
    }
}
