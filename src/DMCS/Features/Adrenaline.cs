using System;
using System.Globalization;
using System.Linq;
using DMCS.Core;
using ThunderRoad;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Adrenaline (step 4a): combat fills a meter; when it is full a window opens for the critical (built in 4b).
    /// Sources: player hits on NPCs and NPC damage on the player (EventManager.onCreatureHit), blocks and parries
    /// (EventManager.onCreatureAttackParry, raised by BrainModuleMelee/BrainModuleDefense), charge staggers and
    /// disarms (DMCS's own events). Clock: real time (Time.unscaledTime), so slow-mo does not stretch the window.
    /// </summary>
    public static class Adrenaline
    {
        private const string Channel = "Adrenaline";

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.Adrenaline, true, Dmcs.Log);

        private static readonly AdrenalineMeter Meter = new AdrenalineMeter();
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
            get => Toggle.Enabled;
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
            name = AdrenalineSettings.DecayPerSecondName,
            tooltip = "Points per second the meter loses while it is not full. 0 = it never drains.",
            valueSourceName = nameof(DecayPerSecondValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 3)]
        [ModOptionSlider]
        public static float DecayPerSecond { get; set; } = AdrenalineSettings.DecayPerSecond.Default;

        [ModOption(
            name = AdrenalineSettings.DecayDelayName,
            tooltip = "Seconds after the last gain before the meter starts to drain.",
            valueSourceName = nameof(DecayDelayValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 4)]
        [ModOptionSlider]
        public static float DecayDelay { get; set; } = AdrenalineSettings.DecayDelay.Default;

        [ModOption(
            name = AdrenalineSettings.GainHitName,
            tooltip = "Points for each hit you land on an enemy (weapon, fist, kick, thrown, spell).",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 5)]
        [ModOptionSlider]
        public static int GainHit { get; set; } = AdrenalineSettings.DefaultGainHit;

        [ModOption(
            name = AdrenalineSettings.GainBlockParryName,
            tooltip = "Points for each enemy attack you stop with your weapon or shield.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 6)]
        [ModOptionSlider]
        public static int GainBlockParry { get; set; } = AdrenalineSettings.DefaultGainBlockParry;

        [ModOption(
            name = AdrenalineSettings.GainDamageTakenName,
            tooltip = "Points for each hit an enemy lands on you.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 7)]
        [ModOptionSlider]
        public static int GainDamageTaken { get; set; } = AdrenalineSettings.DefaultGainDamageTaken;

        [ModOption(
            name = AdrenalineSettings.GainChargeStaggerName,
            tooltip = "Points for each enemy you stagger with a charge.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 8)]
        [ModOptionSlider]
        public static int GainChargeStagger { get; set; } = AdrenalineSettings.DefaultGainChargeStagger;

        [ModOption(
            name = AdrenalineSettings.GainDisarmName,
            tooltip = "Points for each enemy you disarm.",
            valueSourceName = nameof(GainValues),
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 9)]
        [ModOptionSlider]
        public static int GainDisarm { get; set; } = AdrenalineSettings.DefaultGainDisarm;

        public static ModOptionFloat[] MeterMaxValues() => Floats(AdrenalineSettings.MeterMax, "0", "");

        public static ModOptionFloat[] WindowDurationValues() => Floats(AdrenalineSettings.WindowDuration, "0", " s");

        public static ModOptionFloat[] DecayPerSecondValues() => Floats(AdrenalineSettings.DecayPerSecond, "0.0", " /s");

        public static ModOptionFloat[] DecayDelayValues() => Floats(AdrenalineSettings.DecayDelay, "0", " s");

        public static ModOptionInt[] GainValues() =>
            AdrenalineSettings.GainValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture), v)).ToArray();

        private static AdrenalineGains Gains =>
            new AdrenalineGains(GainHit, GainBlockParry, GainDamageTaken, GainChargeStagger, GainDisarm);

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
                _hooked = false;
            }
            ResetAll();
        }

        /// <summary>Level unloading or feature off: the meter starts empty.</summary>
        public static void ResetAll()
        {
            Meter.Reset();
        }

        public static void Update()
        {
            if (!Toggle.Enabled)
            {
                return;
            }
            var e = Meter.Tick(Time.unscaledTime, Time.unscaledDeltaTime, DecayPerSecond, DecayDelay);
            if (e == AdrenalineEvent.WindowExpired)
            {
                Dmcs.Log.Debug(Channel, "window over unused: meter emptied");
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
                var source = AdrenalineSources.FromHit(eventTime == EventTime.OnEnd, HitInfo.SourceOf(hit), target.isPlayer, hit.damageStruct.damage);
                if (source.HasValue)
                {
                    Add(source.Value, target.isPlayer ? "you" : target.name);
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline error: " + ex);
            }
        }

        private static void OnParry(Creature parried, Item parriedItem, Creature parrying, Item parryingItem, CollisionInstance hit)
        {
            try
            {
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

        private static void Add(AdrenalineSource source, string who)
        {
            if (!Toggle.Enabled)
            {
                return;
            }
            var gain = Gains.For(source);
            var e = Meter.Gain(gain, Time.unscaledTime, MeterMax, WindowDuration);
            if (e == AdrenalineEvent.None)
            {
                return;
            }
            Dmcs.Log.Debug(Channel, "+" + gain + " " + source + " (" + who + "): "
                + Meter.Value.ToString("0", CultureInfo.InvariantCulture) + "/" + MeterMax.ToString("0", CultureInfo.InvariantCulture));
            if (e == AdrenalineEvent.Filled)
            {
                Dmcs.Log.Debug(Channel, "meter full: window open for " + WindowDuration.ToString("0", CultureInfo.InvariantCulture) + " s");
            }
        }

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();
    }
}
