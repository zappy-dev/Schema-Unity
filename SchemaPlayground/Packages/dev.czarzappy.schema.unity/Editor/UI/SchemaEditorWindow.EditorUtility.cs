using System;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        internal GUILayoutOption[] DoNotExpandWidthOptions = { GUILayout.ExpandWidth(false) };
        internal GUILayoutOption[] ExpandWidthOptions = { GUILayout.ExpandWidth(true) };
        internal object FastTextField(object value, GUIStyle style, params GUILayoutOption[] options)
        {
            return EditorGUILayout.TextField(
                value == null ? string.Empty : value.ToString(), style, options);
        }
        
        internal void Mark()
        {
            EditorGUILayout.LabelField($"Mark{debugIdx++}", GUILayout.ExpandWidth(false), GUILayout.Width(50));
        }
        
        internal void DrawVerticalLine(float thickness = 2)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(thickness), GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, Color.white);
        }

        internal static bool AddButton(string text, bool expandWidth = false, float? height = null) => 
            Button(text, EditorIcon.PLUS_ICON_NAME, expandWidth: expandWidth, height: height);

        internal static bool Button(string text, string iconName, bool expandWidth = false, float? height = null) =>
            Button(text, iconName, GUILayout.ExpandWidth(expandWidth),
                height != null ? GUILayout.Height(height.Value) : GUILayout.ExpandHeight(false));
        
        
        internal static bool Button(string text, string iconName, params GUILayoutOption[] options) =>
            GUILayout.Button(new GUIContent(text, EditorGUIUtility.IconContent(iconName).image), options);

    }
}