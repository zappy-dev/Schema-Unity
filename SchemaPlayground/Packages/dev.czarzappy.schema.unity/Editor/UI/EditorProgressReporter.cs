using UnityEditor;
using System;
using UnityEngine;

namespace Schema.Unity.Editor
{
    public class EditorProgressReporter : IProgress<(float value, string message)>, IDisposable
    {
        private string title;
        private string info;

        public EditorProgressReporter(string title, string info)
        {
            this.title = title;
            this.info = info;
        }

        public void Report((float value, string message) progress)
        {
            // Display the progress bar in Unity Editor
            EditorUtility.DisplayProgressBar(title, $"{info} - {progress.message}", Mathf.Clamp01(progress.value));
        }

        public void Dispose()
        {
            // Clear the progress bar when finished
            EditorUtility.ClearProgressBar();
        }
    }
}