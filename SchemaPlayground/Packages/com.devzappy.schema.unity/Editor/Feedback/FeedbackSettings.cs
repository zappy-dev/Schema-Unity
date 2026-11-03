using System;
using System.IO;
using UnityEditor;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>
    /// Where feedback goes, on this machine. Stored in <see cref="EditorPrefs"/>, so a host project gains no
    /// files to commit. EditorPrefs is per machine, shared by every Unity project, which is why only the UI
    /// layer reads it: the sender takes a settings object, and tests build their own and never touch the key.
    /// </summary>
    internal sealed class FeedbackSettings
    {
        public const string SocketPathPrefsKey = "Schema.Feedback.SocketPath";
        public const int DefaultTimeoutMs = 5000;

        /// <summary>The repairkit daemon's Unix socket. Empty means feedback is switched off on this machine.</summary>
        public string SocketPath = string.Empty;

        /// <summary>Where unsent reports wait. Defaults to <see cref="DefaultOutboxRoot"/>.</summary>
        public string OutboxRoot = DefaultOutboxRoot;

        public int TimeoutMs = DefaultTimeoutMs;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(SocketPath);

        /// <summary><c>~/.schema-unity/feedback-outbox</c>: outside every project, so nothing to commit.</summary>
        public static string DefaultOutboxRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".schema-unity", "feedback-outbox");

        public static FeedbackSettings LoadFromEditorPrefs() => new FeedbackSettings
        {
            SocketPath = (EditorPrefs.GetString(SocketPathPrefsKey, string.Empty) ?? string.Empty).Trim(),
        };

        public static void SaveSocketPath(string socketPath)
        {
            var trimmed = (socketPath ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                EditorPrefs.DeleteKey(SocketPathPrefsKey);
            }
            else
            {
                EditorPrefs.SetString(SocketPathPrefsKey, trimmed);
            }
        }

        /// <summary>The sentence shown when nothing is set: how to turn feedback on.</summary>
        public const string HowToConfigure =
            "Feedback is not set up on this machine. Paste the path of your repairkit daemon's Unix socket " +
            "(a file ending in .sock) into the socket field, then Send again. Nothing was sent.";
    }
}
