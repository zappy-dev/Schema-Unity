using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public static class SchemaLayout
    {
        public const float SETTINGS_WIDTH = 50;
        
        private const float EVEN_ODDS_BASE = 0.4f;
        private const float EVEN_ODDS_OFFSET = 0.3f;
        private static readonly Color cellEventBackgroundColor = new Color(EVEN_ODDS_BASE, EVEN_ODDS_BASE, EVEN_ODDS_BASE);

        private static readonly Color cellOddBackgroundColor = new Color(EVEN_ODDS_BASE + EVEN_ODDS_OFFSET, EVEN_ODDS_BASE + EVEN_ODDS_OFFSET, EVEN_ODDS_BASE + EVEN_ODDS_OFFSET);
        
        public static GUIStyle LeftAlignedButtonStyle;
        public static GUIStyle RightAlignedLabelStyle;
        private static GUIStyle _defaultDropdownButtonStyle;

        private static CellStyle _cellEvenStyle;
        private static CellStyle _cellOddStyle;
        
        public static void InitializeStyles()
        {
            LeftAlignedButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 5, 5)
            };

            RightAlignedLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight
            };

            _defaultDropdownButtonStyle = new GUIStyle("MiniPullDown")
            {
                alignment = TextAnchor.MiddleCenter
            };

            _cellEvenStyle = new CellStyle();
            _cellOddStyle = new CellStyle();
            
            _cellEvenStyle.SetBackgroundColor(cellEventBackgroundColor);
            _cellOddStyle.SetBackgroundColor(cellOddBackgroundColor);
        }

        public static bool SettingsButton(string text = "", float width = SETTINGS_WIDTH)
        {
            SingleLayoutOption[0] = GUILayout.MaxWidth(width);
            return EditorGUILayout.DropdownButton(
                new GUIContent(text, EditorIcon.Gear.image),
                FocusType.Keyboard, SingleLayoutOption);
        }


        public static bool DropdownButton(string text = "", float width = SETTINGS_WIDTH, GUIStyle style = null) =>
            DropdownButton(new GUIContent(text), width, style);

        public static GUILayoutOption[] SingleLayoutOption = new GUILayoutOption[1];
        
        public static bool DropdownButton(GUIContent content, float width = SETTINGS_WIDTH, GUIStyle style = null)
        {
            
            SingleLayoutOption[0] = GUILayout.Width(width);
            var buttonStyle = style ?? _defaultDropdownButtonStyle;
            return EditorGUILayout.DropdownButton(
                content,
                FocusType.Keyboard, buttonStyle, SingleLayoutOption);
        }
        
        public static CellStyle GetRowCellStyle(int rowIdx)
        {
            switch (rowIdx % 2)
            {
                case 0:
                    return _cellEvenStyle;
                default:
                    return _cellOddStyle;
            }
        }
    }
}