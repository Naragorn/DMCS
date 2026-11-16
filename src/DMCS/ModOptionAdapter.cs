using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Thin adapter so the pure reset logic can drive the game's ModOption.</summary>
    internal sealed class ModOptionAdapter : IResettableOption
    {
        private readonly ModOption _option;

        public ModOptionAdapter(ModOption option)
        {
            _option = option;
        }

        public string Name => _option.name;

        public int DefaultIndex => _option.defaultValueIndex;

        public int ValueCount => _option.parameterValues?.Length ?? 0;

        /// <summary>ModOption.Apply sets the member, saves the index and raises ValueChanged.</summary>
        public void ApplyIndex(int index) => _option.Apply(index);

        public void RefreshUi()
        {
            var ui = _option.uiComponent;
            // uiComponent is null until the mods page was opened; a destroyed MonoBehaviour is Unity-null.
            if (ui == null || (ui is UnityEngine.Object unityObject && unityObject == null))
            {
                return;
            }
            // UIList.Reload = InitValues + LoadValue(default) + Refresh -> row shows the default value.
            ui.Reload();
        }
    }
}
