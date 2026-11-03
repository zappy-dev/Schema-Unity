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

        /// <summary>
        /// The repairkit daemon's Unix socket, as an absolute path: everything that sets this runs the value
        /// through <see cref="ExpandUserPath"/> first. Empty means feedback is switched off on this machine.
        /// </summary>
        public string SocketPath = string.Empty;

        /// <summary>Where unsent reports wait. Defaults to <see cref="DefaultOutboxRoot"/>.</summary>
        public string OutboxRoot = DefaultOutboxRoot;

        public int TimeoutMs = DefaultTimeoutMs;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(SocketPath);

        /// <summary><c>~/.schema-unity/feedback-outbox</c>: outside every project, so nothing to commit.</summary>
        public static string DefaultOutboxRoot =>
            Path.Combine(HomeDirectory, ".schema-unity", "feedback-outbox");

        /// <summary>
        /// <c>~/.schema-unity/repairkit/repairkit.sock</c>: where <c>tools/repairkit.sh start</c> leaves the
        /// daemon's socket (docs/RepairKit-Daemon.md). Filled in for you when a socket is there, so the usual
        /// setup needs nothing pasted.
        /// </summary>
        public static string DefaultSocketPath =>
            Path.Combine(HomeDirectory, ".schema-unity", "repairkit", "repairkit.sock");

        private static string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        /// <summary>
        /// Makes a typed path one the OS can connect to: trims it, and expands a leading <c>~</c>. A shell
        /// expands <c>~</c>; the socket API does not, so a pasted <c>~/...</c> would be looked for inside a
        /// directory literally named <c>~</c> and never reach the daemon. <c>~user</c> (another account's
        /// home) and a <c>~</c> anywhere but the front are left alone, as is everything else.
        /// </summary>
        public static string ExpandUserPath(string path)
        {
            var trimmed = (path ?? string.Empty).Trim();
            if (trimmed.Length == 0) return string.Empty;

            var isHome = trimmed.Length == 1 && trimmed[0] == '~';
            var isUnderHome = trimmed.Length > 1 && trimmed[0] == '~' && (trimmed[1] == '/' || trimmed[1] == '\\');
            if (!isHome && !isUnderHome) return trimmed;

            var home = HomeDirectory;
            if (string.IsNullOrEmpty(home)) return trimmed;

            return isHome ? home : Path.Combine(home, trimmed.Substring(2));
        }

        /// <summary>
        /// The socket path to work with. <paramref name="stored"/> is null when this machine has never been
        /// asked (no EditorPrefs key): then <see cref="DefaultSocketPath"/> is used if a socket is sitting
        /// there, so a daemon in its usual place needs no pasting. A stored value always wins, including an
        /// empty one -- that is "switched off" chosen by hand, and is not quietly filled in again.
        /// </summary>
        public static string ResolveSocketPath(string stored, Func<string, bool> socketExists)
        {
            if (stored != null) return ExpandUserPath(stored);
            return socketExists(DefaultSocketPath) ? DefaultSocketPath : string.Empty;
        }

        /// <summary>True when something is at <paramref name="path"/>: to the filesystem a Unix socket is a file.</summary>
        internal static bool SocketFileExists(string path) => !string.IsNullOrEmpty(path) && File.Exists(path);

        public static FeedbackSettings LoadFromEditorPrefs() => new FeedbackSettings
        {
            SocketPath = ResolveSocketPath(
                EditorPrefs.HasKey(SocketPathPrefsKey) ? EditorPrefs.GetString(SocketPathPrefsKey, string.Empty) : null,
                SocketFileExists),
        };

        public static void SaveSocketPath(string socketPath)
        {
            // An empty value is written, not deleted: the key's presence is how "off, chosen by hand" is told
            // apart from "never asked", which is what lets the standard socket be filled in only the once.
            EditorPrefs.SetString(SocketPathPrefsKey, ExpandUserPath(socketPath));
        }

        /// <summary>The sentence shown when nothing is set: how to turn feedback on.</summary>
        public const string HowToConfigure =
            "Feedback is not set up on this machine. Start the repairkit daemon (tools/repairkit.sh start) and " +
            "its socket at ~/.schema-unity/repairkit/repairkit.sock is filled in for you; otherwise paste the " +
            "path of your daemon's Unix socket (a file ending in .sock) into the socket field, then Send again. " +
            "Nothing was sent.";
    }
}
