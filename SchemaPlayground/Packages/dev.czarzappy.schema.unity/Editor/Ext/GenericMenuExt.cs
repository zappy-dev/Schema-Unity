using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Ext
{
    public static class GenericMenuExt
    {
        public static void AddItem(this GenericMenu menu, GUIContent content, bool isDisabled, GenericMenu.MenuFunction function)
        {
            if (isDisabled)
            {
                menu.AddDisabledItem(content);
            }
            else
            {
                menu.AddItem(content, false, function);
            }
        }
    }
}