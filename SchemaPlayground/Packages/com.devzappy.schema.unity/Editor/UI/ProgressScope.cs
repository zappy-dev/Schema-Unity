using System;
using UnityEditor;
using UnityEngine;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    public class ProgressScope : IDisposable
    {
        private readonly string title;
        public ProgressScope(string title)
        {
            this.title = title;
        }

        public void Progress(string step, float progress)
        {
            Logger.LogDbgVerbose($"[{title}] {step}");
            EditorUtility.DisplayProgressBar(title, step, progress);
        }

        public void Dispose()
        {
            EditorUtility.ClearProgressBar();
        }
    }
}