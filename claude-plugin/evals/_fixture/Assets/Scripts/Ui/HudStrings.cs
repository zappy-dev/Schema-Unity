using UnityEngine;
using UnityEngine.Localization;

namespace Tidewater
{
    /// <summary>HUD text comes from the "HUD" string table in the Unity Localization package, one entry per key.</summary>
    public class HudStrings : MonoBehaviour
    {
        public LocalizedString CoinsLabel = new LocalizedString("HUD", "coins");
        public LocalizedString FuelLabel = new LocalizedString("HUD", "fuel");
        public LocalizedString DockedLabel = new LocalizedString("HUD", "docked");
    }
}
