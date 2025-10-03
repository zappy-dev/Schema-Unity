using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core;
using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Unity.Editor.Ext;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Logging.Logger;
using static Schema.Core.Schema;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    internal partial class SchemaEditorWindow
    {
        #region Fields and Properties
        
        private readonly ICommandProcessor commandProcessor = new CommandProcessor();
        private CancellationTokenSource _cancellationTokenSource;

        // Progress tracking
        private bool _operationInProgress;
        private string _currentOperationDescription;
        private float _currentProgress;
        private string _currentProgressMessage;

        internal struct CommandRequest
        {
            internal string Description;
            public ISchemaCommand Command;
            public Action<CommandResult> OnRequestComplete;
        }
        
        private Queue<CommandRequest> commandRequestQueue = new Queue<CommandRequest>();

        #endregion

        #region Lifecycle

        private void RegisterCommandHistoryCallbacks()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            // Subscribe to command history events for repainting
            commandProcessor.CommandExecuted += OnCommandExecuted;
            commandProcessor.CommandUndone += OnCommandUndone;
            commandProcessor.CommandRedone += OnCommandRedone;
            
            EditorApplication.update += ProcessCommandQueue;
        }

        private void UnregisterCommandHistoryCallbacks()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            commandProcessor.CommandExecuted -= OnCommandExecuted;
            commandProcessor.CommandUndone -= OnCommandUndone;
            commandProcessor.CommandRedone -= OnCommandRedone;
            
            EditorApplication.update -= ProcessCommandQueue;
        }

        private void OnCommandExecuted(object sender, CommandExecutedEventArgs commandExecutedEventArgs)
        {
            Repaint();
        }

        private void OnCommandUndone(object sender, CommandUndoneEventArgs commandUndoneEventArgs)
        {
            Repaint();
        }

        private void OnCommandRedone(object sender, CommandRedoneEventArgs commandRedoneEventArgs)
        {
            Repaint();
        }
        
        private void ProcessCommandQueue()
        {
            // Skip processing command queue if there are no commands
            if (commandRequestQueue.Count == 0) return;
            
            // Only execute one operation at a time
            if (_operationInProgress) return;

            var request = commandRequestQueue.Dequeue();
            
            ProcessCommandRequest(request).FireAndForget();
        }

        private async Task ProcessCommandRequest(CommandRequest request)
        {
            LogDbgVerbose($"Starting to process command: {request.Description}");
            _operationInProgress = true;
            _currentOperationDescription = request.Description;
            try
            {
                var result = await commandProcessor.ExecuteAsync(request.Command, _cancellationTokenSource.Token);
                request.OnRequestComplete(result);
                OnSelectedSchemeChanged?.Invoke();
                // Save();
            }
            catch (OperationCanceledException) { }
            finally
            {
                _operationInProgress = false;
                _currentProgress = 0f;
                _currentProgressMessage = string.Empty;
                Repaint();
            }
        }

        #endregion
        
        #region UI Rendering
        
        private void DrawUndoRedoPanel()
        {
            _showUndoRedoPanel = EditorGUILayout.Foldout(_showUndoRedoPanel, "Undo/Redo Controls");
            if (!_showUndoRedoPanel) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!commandProcessor.CanUndo || _operationInProgress);
            if (GUILayout.Button($"Undo ({commandProcessor.UndoHistory.Count})", GUILayout.Width(100)))
            {
                _ = ExecuteUndoAsync();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!commandProcessor.CanRedo || _operationInProgress);
            if (GUILayout.Button($"Redo ({commandProcessor.RedoHistory.Count})", GUILayout.Width(100)))
            {
                _ = ExecuteRedoAsync();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(commandProcessor.Count == 0 || _operationInProgress);
            if (GUILayout.Button("Clear History", GUILayout.Width(100)))
            {
                commandProcessor.ClearHistory();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void DrawProgressBar()
        {
            if (!_operationInProgress) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_currentOperationDescription ?? "Working...", GUILayout.Width(200));
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(rect, _currentProgress, _currentProgressMessage ?? string.Empty);
            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                CancelCurrentOperation();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        
        #endregion
        
        private void UpdateProgress(CommandProgress progress)
        {
            _currentProgress = progress.Value;
            _currentProgressMessage = progress.Message;
            EditorApplication.delayCall += Repaint;
        }

        private void CancelCurrentOperation()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _operationInProgress = false;
        }
        
        private void SubmitCommandRequest(CommandRequest commandRequest)
        {
            Logger.LogDbgVerbose($"Enqueuing command: {commandRequest.Description}");
            commandRequestQueue.Enqueue(commandRequest);
        }
        
        // Async version of adding a schema using command system
        internal Task SubmitAddSchemeRequest(SchemaContext context, DataScheme newSchema, string importFilePath = null)
        {
            // TODO: Replace this bad pattern, what if multiple operations need to queue up?
            // if (_operationInProgress) return;

            // Confirm overwrite if needed
            bool overwriteExisting = false;
            if (!IsSchemeLoaded(context, newSchema.SchemeName).Try(out bool doesExist, out var error))
            {
                var ct = new CancellationToken(true);
                return Task.FromCanceled(ct);
            }
            if (doesExist)
            {
                overwriteExisting = EditorUtility.DisplayDialog(
                    "Add Schema",
                    $"A Schema named {newSchema.SchemeName} already exists. Do you want to overwrite this scheme?",
                    "Yes",
                    "No");
                if (!overwriteExisting) return Task.CompletedTask;
            }

            var progress = new Progress<CommandProgress>(UpdateProgress);
            SubmitCommandRequest(new CommandRequest
            {
                Description = $"Adding schema '{newSchema.SchemeName}'",
                Command = new LoadDataSchemeCommand(context,
                    newSchema,
                    overwriteExisting: overwriteExisting,
                    importFilePath: importFilePath,
                    progress: progress),
                OnRequestComplete = (result) =>
                {
                    if (result.IsSuccess)
                    {
                        var ctx = new SchemaContext
                        {
                            Scheme = newSchema,
                            Driver = "User_Add_New_Scheme"
                        };
                        // persist data to file first
                        Save(ctx, true);
                        
                        // then select the new scheme
                        OnSelectScheme(newSchema.SchemeName, ctx);
                    }
                    else
                    {
                        LogError(result.Message);
                    }
                }
            });
            return Task.CompletedTask;
        }

        private Task ExecuteSetDataOnEntryAsync(SchemaContext context, DataScheme scheme, DataEntry entry, string attributeName, object value)
        {
            // if (_operationInProgress) return;
            // _operationInProgress = true;
            // _currentOperationDescription = $"Updating '{scheme.SchemeName}.{attributeName}'";
            
            SubmitCommandRequest(new CommandRequest
            {
                Description = $"Updating '{scheme.SchemeName}.{attributeName}'",
                Command = new SetDataOnEntryCommand(context, scheme, entry, attributeName, value),
                OnRequestComplete = (result) =>
                {
                    if (!result.IsSuccess)
                    {
                        Debug.LogError(result.Message);
                    }
                    else
                    {
                        // Persist change
                        // SaveDataScheme(scheme, false);
                    }
                }
                
            });
            return Task.CompletedTask;
            // try
            // {
            //     var progress = new Progress<CommandProgress>(UpdateProgress);
            //     var cmd = new SetDataOnEntryCommand(scheme, entry, attributeName, value);
            //     
            //     commandRequestQueue.Enqueue(new CommandRequest
            //     {
            //         Description = $"Updating '{scheme.SchemeName}.{attributeName}'",
            //         Command = cmd,
            //     });
            //     var result = await commandProcessor.ExecuteAsync(cmd, _cancellationTokenSource.Token);
            //     if (!result.IsSuccess)
            //     {
            //         Debug.LogError(result.Message);
            //     }
            //     else
            //     {
            //         // Persist change
            //         Save();
            //     }
            // }
            // catch (OperationCanceledException) { }
            // finally
            // {
            //     _operationInProgress = false;
            //     _currentProgress = 0f;
            //     _currentProgressMessage = string.Empty;
            //     Repaint();
            // }
        }
        
        private async Task ExecuteUndoAsync()
        {
            if (_operationInProgress) return;
            _operationInProgress = true;
            _currentOperationDescription = "Undoing last command";
            try
            {
                await commandProcessor.UndoAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _operationInProgress = false;
                OnSelectedSchemeChanged?.Invoke();
                ReleaseControlFocus();
                Repaint();
            }
        }

        private async Task ExecuteRedoAsync()
        {
            if (_operationInProgress) return;
            _operationInProgress = true;
            _currentOperationDescription = "Redoing last undone command";
            try
            {
                await commandProcessor.RedoAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _operationInProgress = false;
                OnSelectedSchemeChanged?.Invoke();
                ReleaseControlFocus();
                Repaint();
            }
        }
    }
}