using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public class CellStyle
    {
        public GUIStyle FieldStyle { get; } = new GUIStyle(EditorStyles.textField);
        public GUIStyle DropdownStyle { get; } = new GUIStyle("MiniPullDown");
        public GUIStyle ButtonStyle { get; } = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter
        };

        private Color backgroundColor;
        public Color BackgroundColor => backgroundColor;

        public void SetBackgroundColor(Color newBackgroundColor)
        {
            Texture2D backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixels(new[]
            {
                newBackgroundColor
            });
            backgroundTexture.Apply();
                
            // FieldStyle.normal.background = backgroundTexture;
                
            // DropdownStyle.normal.background = backgroundTexture;
                
            // DropdownStyle.active.background = backgroundTexture;
            // DropdownStyle.hover.background = backgroundTexture;
                
            // ButtonStyle.normal.background = backgroundTexture;
            this.backgroundColor = newBackgroundColor;
        }
    }
}