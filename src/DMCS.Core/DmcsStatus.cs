using System.Collections.Generic;
using System.Text;

namespace DMCS.Core
{
    /// <summary>One log line with the effective settings.</summary>
    public static class DmcsStatus
    {
        public static string Describe(bool debugLogging, IEnumerable<FeatureToggle> toggles)
        {
            var text = new StringBuilder("Settings: Debug Logging=").Append(debugLogging ? "on" : "off");
            if (toggles != null)
            {
                foreach (var toggle in toggles)
                {
                    if (toggle == null)
                    {
                        continue;
                    }
                    text.Append(" | ").Append(toggle.Name).Append('=').Append(toggle.MenuEnabled ? "Enabled" : "Disabled");
                    if (toggle.MenuEnabled && toggle.NeedsSkill)
                    {
                        text.Append(toggle.HasSkill ? ", needs its skill (bought)" : ", needs its skill (not bought)");
                    }
                }
            }
            return text.ToString();
        }
    }
}
