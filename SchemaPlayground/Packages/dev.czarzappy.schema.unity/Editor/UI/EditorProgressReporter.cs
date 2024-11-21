using UnityEditor;
using System;
using UnityEngine;
using Logger = Schema.Core.Logger;

namespace Schema.Unity.Editor
{
    public class EditorProgressReporter : IProgress<(float value, string message)>, IDisposable
    {
        private string title;
        private string info;

        public EditorProgressReporter(string title, string info)
        {
            Logger.LogVerbose($"EditorProgressReporter reporting");
            this.title = title;
            this.info = info;
        }

        public void Report((float value, string message) progress)
        {
            Logger.LogVerbose($"EditorProgressReporter reporting progress: {progress.message}");
            // Display the progress bar in Unity Editor
            EditorUtility.DisplayProgressBar(title, $"{info} - {progress.message}", Mathf.Clamp01(progress.value));
        }

        public void Dispose()
        {
            Logger.LogVerbose($"EditorProgressReporter disposed");
            // Clear the progress bar when finished
            EditorUtility.ClearProgressBar();
        }
    }
}