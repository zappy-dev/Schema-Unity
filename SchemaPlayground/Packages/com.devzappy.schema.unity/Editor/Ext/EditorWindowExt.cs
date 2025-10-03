using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public static class EditorWindowExt
    {
        public static T Open<T>() where T : EditorWindow
        {
            var w = EditorWindow.GetWindow<T>();
            w.Show();
            w.Repaint();
            return w;
        }

        public static void Close(EditorWindow w) { if (w) w.Close(); }


        private const int DEFAULT_DELAY = 1;

        public static IEnumerator PumpFrames(int frames = DEFAULT_DELAY)
        {
            for (int i = 0; i < frames; i++)
                yield return null; // allow OnGUI / layout / repaint cycles
        }

        public static void Send(this EditorWindow w, Event e)
        {
            Logger.Log($"Send event {e} to window: {w}");
            // Ensure the window can receive & process GUI events.
            w.Focus();
            w.SendEvent(e);
            w.Repaint();
        }

        public static Rect GetLastScreenRect(this EditorWindow window)
        {
            var r = GUILayoutUtility.GetLastRect();  // group-space    
            var screenTL = GUIUtility.GUIToScreenPoint(r.position);
            var winTL    = (Vector2)window.position.position;           // window top-left in screen coords
            return new Rect(screenTL - winTL, r.size);
        }

        public static IEnumerator WaitUntil(Func<bool> cond, float timeoutSec = 2f)
        {
            var t = Time.realtimeSinceStartup;
            while (!cond())
            {
                if (Time.realtimeSinceStartup - t > timeoutSec) Assert.Fail("WaitUntil timeout");
                yield return null;
            }
        }
    }
}