using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        #region Editor Testing

        public const string CONTROL_NAME_NEW_SCHEME_NAME_FIELD = "NewSchemeNameField";
        public Rect CreateNewSchemeButtonWinRect { get; private set; }
        public Rect ExplorerWinRect { get; private set; }
        private List<(int frameCount, Event)> eventHistory = new List<(int frameCount, Event)>();
        public IEnumerable<(int frameCount, Event)> EventHistory => eventHistory;

        public string ReportEvents()
        {
            var sb = new StringBuilder();
            foreach (var @event in EventHistory
                         .OrderByDescending((@event) => @event.frameCount))
            {
                sb.AppendLine(@event.ToString());
            }

            return sb.ToString();
        }

        #endregion
        
        private string nextControlToFocus = null;
        internal bool releaseControl = false;
        internal void SetFocus(string controlName)
        {
            Logger.LogDbgVerbose($"SetFocus={controlName}");
            this.Focus();
            // allows for external windows to set the control focus
            nextControlToFocus = controlName;
            this.Repaint();
        }
        
        /// <summary>
        /// Utility method for release focus from a selected control.
        /// Selecting a control can prevent it from updating with new values. By forcing a release of the focus, these controls can repaint with new values
        /// </summary>
        internal void ReleaseControlFocus()
        {
            Logger.LogDbgVerbose("ReleaseControlFocus");
            GUI.FocusControl(null);
        }

        internal IEnumerator SimClick_CreateNewSchema()
        {
            var rect = CreateNewSchemeButtonWinRect;

            // Make sure to focus Editor Window
            Logger.LogDbgVerbose($"click rect: {rect}");
            // yield return ClickRect(window, rect);

            yield return SimClick_ButtonRect(rect);
            // var pos = rect.center;
            //
            // // [804] [Schema][804] Event.current: Event: mouseDown   Position: (272.0, 341.0) Modifiers: None, isKey: False
            // // [805] [Schema][805] Event.current: Event: mouseUp   Position: (272.0, 341.0) Modifiers: None, isKey: False
            // Send(this, new Event { type = EventType.MouseMove,  mousePosition = pos });
            // yield return EditorWindowExt.PumpFrames();
            // // PumpEditorFrame(); // let OnGUI run
            //
            // Send(this, new Event { type = EventType.MouseDown, button = 0, mousePosition = pos });
            // yield return EditorWindowExt.PumpFrames();
            // // PumpEditorFrame();
            //
            // Send(this, new Event { type = EventType.MouseUp,   button = 0, mousePosition = pos });
            // yield return EditorWindowExt.PumpFrames();
            // PumpEditorFrame();
        }

        internal IEnumerator SimClick_ButtonRect(Rect buttonRect)
        {
            Focus();
            var pos = buttonRect.center;
            
            // [804] [Schema][804] Event.current: Event: mouseDown   Position: (272.0, 341.0) Modifiers: None, isKey: False
            // [805] [Schema][805] Event.current: Event: mouseUp   Position: (272.0, 341.0) Modifiers: None, isKey: False
            Send(this, new Event { type = EventType.MouseMove,  mousePosition = pos });
            yield return EditorWindowExt.PumpFrames();
            // PumpEditorFrame(); // let OnGUI run
            
            Send(this, new Event { type = EventType.MouseDown, button = 0, mousePosition = pos });
            yield return EditorWindowExt.PumpFrames();
            // PumpEditorFrame();
            
            Send(this, new Event { type = EventType.MouseUp,   button = 0, mousePosition = pos });
            yield return EditorWindowExt.PumpFrames();
        }

        internal IEnumerator SimClick_ExplorerSelectSchemeByIndex(int schemeIdx)
        {
            var numAvailableSchemes = Schema.Core.Schema.NumAvailableSchemes;
            if (schemeIdx >= numAvailableSchemes || schemeIdx < 0)
            {
                Logger.LogWarning($"Attempting to click on out-of-range scheme idx: {schemeIdx}");
                yield break;
            }
            
            var groupRect = ExplorerWinRect;

            var buttonHeight = groupRect.height / numAvailableSchemes;

            var buttonRect = new Rect(groupRect.x, buttonHeight * schemeIdx, groupRect.width, buttonHeight);
            yield return SimClick_ButtonRect(buttonRect);
        }
        
        static void PumpEditorFrame()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            // If you're in a test coroutine, prefer: yield return null;
        }
        
        public static void Send(EditorWindow w, Event e)
        {
            Logger.LogDbgVerbose($"Send event {e} to window: {w}");
            // Ensure the window can receive & process GUI events.
            w.Focus();
            w.SendEvent(e);
            w.Repaint();
        }

        private void OverlapChecker(string context, Rect rect)
        {
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp)
            {
                bool over = rect.Contains(Event.current.mousePosition);
                Debug.Log($"[{context}] OnGUI saw {Event.current.type} at {Event.current.mousePosition}, " +
                          $"overBtn={over}, hot={GUIUtility.hotControl}, btnRect: {rect}");
            }
        }
    }
}