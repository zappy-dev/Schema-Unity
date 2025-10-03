using UnityEngine;

namespace Schema.Unity.Editor
{
    internal static class LayoutUtils
    {
        internal static GUILayoutOption[] DoNotExpandWidthOptions = { GUILayout.ExpandWidth(false) };
        internal static GUILayoutOption[] ExpandWidthOptions = { GUILayout.ExpandWidth(true) };
    }
}