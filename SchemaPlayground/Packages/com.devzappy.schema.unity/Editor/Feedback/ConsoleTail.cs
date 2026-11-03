using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>
    /// The last <see cref="Capacity"/> Console lines that mention "Schema". Fed by
    /// <see cref="Application.logMessageReceivedThreaded"/> from the moment the Editor loads this assembly, so
    /// it covers the time since the last domain reload and not older Console history: the Console's own entries
    /// are reachable only through internal APIs, and a report does not justify reflecting into them.
    /// </summary>
    internal sealed class ConsoleTail
    {
        public const int Capacity = 50;
        public const int MaxLineChars = 400;
        public const string Mention = "Schema";

        public const string Note = "Schema lines logged since the last domain reload (the Console's older entries are not read)";

        private readonly object gate = new object();
        private readonly Queue<string> lines = new Queue<string>(Capacity + 1);

        public void Add(LogType type, string message)
        {
            if (string.IsNullOrEmpty(message) || message.IndexOf(Mention, System.StringComparison.Ordinal) < 0) return;

            var text = message.Length > MaxLineChars ? message.Substring(0, MaxLineChars) + "..." : message;
            var line = type == LogType.Log ? text : "[" + type + "] " + text;
            lock (gate)
            {
                lines.Enqueue(line);
                while (lines.Count > Capacity) lines.Dequeue();
            }
        }

        public List<string> Snapshot()
        {
            lock (gate) return new List<string>(lines);
        }

        /// <summary>The shared tail, subscribed once per domain load.</summary>
        public static ConsoleTail Shared { get; } = new ConsoleTail();

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
        }

        private static void OnLog(string condition, string stackTrace, LogType type) => Shared.Add(type, condition);
    }
}
