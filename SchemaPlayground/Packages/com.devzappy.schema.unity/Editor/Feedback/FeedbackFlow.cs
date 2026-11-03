using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>
    /// The two entry points (a Feedback button in the Schema Editor toolbar, and <c>Tools/Schema/Send Feedback...</c>)
    /// and the Editor-load flush of the outbox. Both entry points do the same thing in the same order:
    /// capture the Schema Editor window first, then open the dialog, so the dialog is never in the shot.
    /// </summary>
    [InitializeOnLoad]
    internal static class FeedbackFlow
    {
        static FeedbackFlow()
        {
            SchemaEditorWindow.DrawToolbarExtras -= DrawToolbarButton;
            SchemaEditorWindow.DrawToolbarExtras += DrawToolbarButton;
            NextTick(FlushOutboxOnLoad);
        }

        [MenuItem("Tools/Schema/Send Feedback...", false, 20)]
        private static void MenuSendFeedback() => Begin(FindOpenSchemaEditor());

        private static void DrawToolbarButton(SchemaEditorWindow window)
        {
            var content = new GUIContent("Feedback", "Report a problem with the Schema Editor. A screenshot of this window is taken first.");
            if (GUILayout.Button(content, GUILayout.ExpandWidth(false)))
            {
                // Not inside OnGUI: let this repaint finish, then capture the window as it is.
                NextTick(() => Begin(window));
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> once on the next Editor update. Used instead of <c>EditorApplication.delayCall</c>:
        /// in a GUI Editor started with <c>-executeMethod</c> and the screen locked, <c>update</c> ticked but a queued
        /// <c>delayCall</c> never ran, which would have left the button doing nothing.
        /// </summary>
        private static void NextTick(Action action)
        {
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                EditorApplication.update -= tick;
                action();
            };
            EditorApplication.update += tick;
        }

        private static SchemaEditorWindow FindOpenSchemaEditor() =>
            Resources.FindObjectsOfTypeAll<SchemaEditorWindow>().FirstOrDefault();

        /// <summary>Captures <paramref name="window"/> (when there is one), then opens the dialog. A null window means the dialog opens without a screenshot.</summary>
        internal static void Begin(SchemaEditorWindow window)
        {
            // Bring the tab to the front and ask it to repaint, then capture two ticks later: a window that has only
            // just been shown or switched to can still be half drawn, and the picture would show that.
            if (window != null)
            {
                window.Focus();
                window.Repaint();
            }

            NextTick(() => NextTick(() => CaptureThenOpen(window)));
        }

        private static void CaptureThenOpen(SchemaEditorWindow window)
        {
            Texture2D shot = null;
            string note;
            try
            {
                if (window == null)
                {
                    note = "No screenshot: the Schema Editor is not open. Open it first (Tools > Schema Editor) to include one.";
                }
                else
                {
                    var capture = WindowCapture.Capture(window);
                    shot = capture.Texture;
                    note = capture.Ok ? string.Empty : "No screenshot: " + capture.Problem;
                }

                // Read after the capture, before the dialog exists: the context describes this moment.
                var inputs = FeedbackContextCapture.Capture(window, includeSchemeData: true);
                FeedbackWindow.Open(inputs, shot, note);
            }
            finally
            {
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
            }
        }

        /// <summary>
        /// On Editor load, send what an earlier unreachable daemon left in the outbox. A strict no-op unless a socket
        /// path is set and something is waiting, so a machine that never configured feedback opens no socket.
        /// </summary>
        private static void FlushOutboxOnLoad()
        {
            if (Application.isBatchMode) return;

            var settings = FeedbackSettings.LoadFromEditorPrefs();
            if (!settings.IsConfigured || !new FeedbackOutbox(settings.OutboxRoot).HasAnyEntries()) return;

            var sender = new FeedbackSender(settings);
            Task.Run(() =>
            {
                try
                {
                    var delivered = sender.FlushOutbox();
                    if (delivered > 0) Debug.Log("Schema feedback: sent " + delivered + " report(s) that were waiting in the outbox.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Schema feedback: could not flush the outbox (" + e.Message + ").");
                }
            });
        }
    }
}
