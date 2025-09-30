using Schema.Unity.Editor.Ext;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class EditorIcon
    {
        public const string PLUS_ICON_NAME = "Toolbar Plus";
        public const string GEAR_ICON_NAME = "SettingsIcon@2x";
        
        public static readonly GUIContent Gear = EditorGUIUtility.IconContent(GEAR_ICON_NAME);
        public static readonly GUIContent DropdownArrow = EditorGUIUtility.IconContent("d_icon dropdown");
        public static readonly Texture2D UpArrow = EditorGUIUtility.IconContent("UpArrow").image as Texture2D;
        public static readonly Texture DownArrow = UpArrow.CloneTexture().FlipTextureVertically();
    }
}