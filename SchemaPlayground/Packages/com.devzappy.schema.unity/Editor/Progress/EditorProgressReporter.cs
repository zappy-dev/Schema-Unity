using UnityEditor;
using System;
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public class EditorProgressReporter : IProgress<(float value, string message)>, IDisposable
    {
        private bool isDisposed = false;
        private string title;
        private string info;

        public EditorProgressReporter(string title, string info = null)
        {
            Logger.LogDbgVerbose($"EditorProgressReporter reporting");
            this.title = title;
            this.info = info;
        }

        public void Report((float value, string message) progress)
        {
            if (isDisposed) return; // attempting to report to a disposed reporter, will result is UI errors with clearing the display progress bar
            Logger.LogDbgVerbose($"EditorProgressReporter reporting progress: {progress.message}");
            // Display the progress bar in Unity Editor
            EditorUtility.DisplayProgressBar(title, (info != null) ? $"{info} - {progress.message}" : progress.message, Mathf.Clamp01(progress.value));
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                throw new InvalidOperationException($"{nameof(EditorProgressReporter)} already disposed");
            }
            isDisposed = true;
            Logger.LogDbgVerbose($"EditorProgressReporter disposed");
            // Clear the progress bar when finished
            EditorUtility.ClearProgressBar();
        }
    }
}