using System;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public class BackgroundColorScope : IDisposable
    {
        private Color originalBackgroundColor;

        public BackgroundColorScope(Color backgroundColor)
        {
            originalBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;
        }
        
        public void Dispose()
        {
            GUI.backgroundColor = originalBackgroundColor;
        }
    }
}