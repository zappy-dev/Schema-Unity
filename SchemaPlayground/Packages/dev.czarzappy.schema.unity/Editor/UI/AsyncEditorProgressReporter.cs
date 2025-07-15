using System;
using Schema.Core.Commands;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// Progress reporter that integrates with Unity's progress bar system for async commands
    /// </summary>
    public class AsyncEditorProgressReporter : IProgress<CommandProgress>, IDisposable
    {
        private readonly string _title;
        private readonly string _info;
        private bool _isDisposed;
        private float _lastValue;
        private string _lastMessage;
        
        public AsyncEditorProgressReporter(string title, string info)
        {
            _title = title ?? "Schema Operation";
            _info = info ?? "Processing...";
            
            Logger.LogDbgVerbose($"AsyncEditorProgressReporter created: {_title} - {_info}");
        }
        
        public void Report(CommandProgress progress)
        {
            if (_isDisposed) return;
            
            _lastValue = progress.Value;
            _lastMessage = progress.Message;
            
            var displayMessage = $"{_info} - {progress.Message}";
            var progressValue = Mathf.Clamp01(progress.Value);
            
            Logger.LogDbgVerbose($"AsyncEditorProgressReporter reporting: {displayMessage} ({progressValue:P0})");
            
            // Update Unity's progress bar
            EditorUtility.DisplayProgressBar(_title, displayMessage, progressValue);
        }
        
        public void UpdateMessage(string message)
        {
            if (_isDisposed) return;
            
            _lastMessage = message;
            var displayMessage = $"{_info} - {message}";
            
            EditorUtility.DisplayProgressBar(_title, displayMessage, _lastValue);
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            EditorUtility.ClearProgressBar();
            
            Logger.LogDbgVerbose($"AsyncEditorProgressReporter disposed: {_title}");
        }
    }
    
    /// <summary>
    /// Factory for creating progress reporters
    /// </summary>
    public static class ProgressReporterFactory
    {
        /// <summary>
        /// Creates a progress reporter for Unity editor operations
        /// </summary>
        public static IProgress<CommandProgress> CreateForOperation(string operationName, string description = null)
        {
            return new AsyncEditorProgressReporter(operationName, description);
        }
        
        /// <summary>
        /// Creates a progress reporter for schema loading operations
        /// </summary>
        public static IProgress<CommandProgress> CreateForSchemaLoad(string schemeName)
        {
            return new AsyncEditorProgressReporter("Schema Loading", $"Loading scheme '{schemeName}'");
        }
        
        /// <summary>
        /// Creates a progress reporter for schema save operations
        /// </summary>
        public static IProgress<CommandProgress> CreateForSchemaSave(string schemeName)
        {
            return new AsyncEditorProgressReporter("Schema Saving", $"Saving scheme '{schemeName}'");
        }
        
        /// <summary>
        /// Creates a progress reporter for identifier update operations
        /// </summary>
        public static IProgress<CommandProgress> CreateForIdentifierUpdate(string schemeName, string attributeName)
        {
            return new AsyncEditorProgressReporter("Identifier Update", $"Updating '{schemeName}.{attributeName}'");
        }
    }
}