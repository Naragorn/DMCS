using System;
using System.Collections.Generic;
using System.Reflection;
using ThunderRoad;

namespace DMCS
{
    /// <summary>
    /// Optional link to the Threat mod (HuJohner, Nexus bladeandsorcery/mods/6830, 3.0.0): an NPC held at weapon
    /// point is in its public static set <c>Threat.ThreatBehaviour.threatenedCreatures</c> (HashSet&lt;Creature&gt;;
    /// added in OnTriggerEnter, removed in ResetThreat; read from the decompiled Threat.dll 3.0.0). Found by reflection,
    /// so DMCS needs no reference to Threat and runs without it. Looked up again until found, because mods may load
    /// after DMCS.
    /// </summary>
    internal static class ThreatSupport
    {
        private const string TypeName = "Threat.ThreatBehaviour";
        private const string FieldName = "threatenedCreatures";

        private static FieldInfo _field;
        private static bool _reportedMissing;

        /// <summary>True when the Threat mod is loaded and holds this NPC threatened.</summary>
        public static bool IsSurrendered(Creature creature)
        {
            if (creature == null)
            {
                return false;
            }
            var field = Field();
            return field != null && field.GetValue(null) is HashSet<Creature> set && set.Contains(creature);
        }

        private static FieldInfo Field()
        {
            if (_field != null)
            {
                return _field;
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try
                {
                    type = assembly.GetType(TypeName, false);
                }
                catch (Exception)
                {
                    continue;
                }
                var field = type?.GetField(FieldName, BindingFlags.Public | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(HashSet<Creature>))
                {
                    _field = field;
                    Dmcs.Log.Debug("Disarm Strike", "Threat mod found (" + assembly.GetName().Name + " " + assembly.GetName().Version + "): threatened enemies count as surrendered");
                    return _field;
                }
            }
            if (!_reportedMissing)
            {
                _reportedMissing = true;
                Dmcs.Log.Debug("Disarm Strike", "Threat mod not loaded: no surrender state");
            }
            return null;
        }
    }
}
