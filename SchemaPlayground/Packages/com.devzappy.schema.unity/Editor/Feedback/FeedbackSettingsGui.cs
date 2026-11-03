using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>The per-machine setting, drawn in the dialog and under Preferences > Schema Feedback.</summary>
    internal static class FeedbackSettingsGui
    {
        private static bool? foldout;
        private static Task<string> pingTask;
        private static string pingResult;

        public static void Draw(FeedbackSettings settings, bool foldoutByDefault)
        {
            if (!foldout.HasValue) foldout = foldoutByDefault;
            foldout = EditorGUILayout.Foldout(foldout.Value, settings.IsConfigured ? "Daemon socket" : "Daemon socket: not set", true);
            if (!foldout.Value) return;

            DrawFields(settings);
        }

        /// <summary>Always-open form, for Preferences.</summary>
        public static void DrawFields(FeedbackSettings settings)
        {
            if (!settings.IsConfigured)
            {
                EditorGUILayout.HelpBox(FeedbackSettings.HowToConfigure, MessageType.Warning);
            }

            var edited = EditorGUILayout.DelayedTextField(
                new GUIContent("Socket path", "The repairkit daemon's Unix socket, e.g. /Users/you/.schema-unity/repairkit/rk.sock. Stored in this machine's EditorPrefs; the project is not touched."),
                settings.SocketPath);
            if (!string.Equals(edited, settings.SocketPath, StringComparison.Ordinal))
            {
                FeedbackSettings.SaveSocketPath(edited);
                settings.SocketPath = (edited ?? string.Empty).Trim();
                pingResult = null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!settings.IsConfigured || pingTask != null))
                {
                    if (GUILayout.Button("Test connection", GUILayout.Width(130)))
                    {
                        var path = settings.SocketPath;
                        pingResult = "Testing...";
                        pingTask = Task.Run(() => PingText(path));
                    }
                }

                if (pingTask != null && pingTask.IsCompleted)
                {
                    pingResult = pingTask.IsFaulted ? "The test failed unexpectedly." : pingTask.Result;
                    pingTask = null;
                }

                if (!string.IsNullOrEmpty(pingResult)) EditorGUILayout.LabelField(pingResult, EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var outbox = new FeedbackOutbox(settings.OutboxRoot);
                EditorGUILayout.LabelField(outbox.HasAnyEntries()
                    ? "Unsent reports are waiting in " + settings.OutboxRoot
                    : "Outbox: empty (" + settings.OutboxRoot + ")", EditorStyles.wordWrappedMiniLabel);
            }

            if (pingTask != null) EditorWindow.focusedWindow?.Repaint();
        }

        private static string PingText(string socketPath)
        {
            try
            {
                new RepairkitClient(socketPath, 2000).Ping();
                return "Connected: the daemon answered.";
            }
            catch (RepairkitException e)
            {
                return "Not connected: " + e.Message;
            }
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider() => new SettingsProvider("Preferences/Schema Feedback", SettingsScope.User)
        {
            label = "Schema Feedback",
            keywords = new[] { "schema", "feedback", "repairkit", "socket" },
            guiHandler = _ => DrawFields(FeedbackSettings.LoadFromEditorPrefs()),
        };
    }
}
