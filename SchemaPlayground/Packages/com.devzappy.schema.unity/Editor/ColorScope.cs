using System;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public class ColorScope : IDisposable
    {
        private Color originalColor;

        public ColorScope(Color color)
        {
            originalColor = GUI.color;
            GUI.color = color;
        }
        
        public void Dispose()
        {
            GUI.color = originalColor;
        }
    }
}