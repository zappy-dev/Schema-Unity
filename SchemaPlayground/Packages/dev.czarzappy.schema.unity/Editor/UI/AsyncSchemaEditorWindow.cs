using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core;
using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Core.Storage;
using UnityEditor;
using UnityEngine;
using static Schema.Core.Schema;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// Updated SchemaEditorWindow that uses the new async Command pattern with undo/redo support
    /// </summary>
    public class AsyncSchemaEditorWindow : EditorWindow
    {
        #region Fields and Properties
        
        private readonly ICommandHistory _commandHistory = new CommandHistory();
        private readonly IAsyncStorage _storage = new AsyncFileStorage();
        private CancellationTokenSource _cancellationTokenSource;
        
        private bool isInitialized;
        private string newSchemeName = string.Empty;
        private string selectedSchemeName = string.Empty;
        private string manifestFilePath = string.Empty;
        
        private Vector2 explorerScrollPosition;
        private Vector2 tableViewScrollPosition;
        private List<CommandResult> responseHistory = new List<CommandResult>();
        
        // Progress tracking
        private bool _operationInProgress;
        private string _currentOperationDescription;
        private float _currentProgress;
        private string _currentProgressMessage;
        
        // Undo/Redo UI
        private bool _showUndoRedoPanel = true;
        
        #endregion
        
        #region Unity Lifecycle
        
        [MenuItem("Tools/Async Schema Editor")]
        public static void ShowWindow()
        {
            GetWindow<AsyncSchemaEditorWindow>("Async Schema Editor");
        }
        
        private void OnEnable()
        {
            Logger.LogDbgVerbose("Async Schema Editor enabled", this);
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Subscribe to command history events
            _commandHistory.CommandExecuted += OnCommandExecuted;
            _commandHistory.CommandUndone += OnCommandUndone;
            _commandHistory.CommandRedone += OnCommandRedone;
            
            isInitialized = false;
            EditorApplication.update += InitializeSafely;
        }
        
        private void OnDisable()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            // Unsubscribe from command history events
            _commandHistory.CommandExecuted -= OnCommandExecuted;
            _commandHistory.CommandUndone -= OnCommandUndone;
            _commandHistory.CommandRedone -= OnCommandRedone;
            
            EditorApplication.update -= InitializeSafely;
        }
        
        private async void InitializeSafely()
        {
            if (isInitialized) return;
            
            EditorApplication.update -= InitializeSafely;
            
            try
            {
                await InitializeAsync();
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogDbgError($"Failed to initialize async schema editor: {ex.Message}", this);
            }
        }
        
        private async Task InitializeAsync()
        {
            var defaultContentPath = Path.Combine(Path.GetFullPath(Application.dataPath + "/.."), "Content");
            var defaultManifestLoadPath = Path.Combine(defaultContentPath, "Manifest.json");
            
            manifestFilePath = defaultManifestLoadPath;
            
            // Load manifest using the new async system
            if (await _storage.FileExistsAsync(manifestFilePath))
            {
                // TODO: Implement async manifest loading command
                // For now, use the existing synchronous method
                var manifestResult = LoadManifestFromPath(manifestFilePath);
                if (manifestResult.Passed)
                {
                    Logger.LogDbgVerbose("Manifest loaded successfully", this);
                }
            }
        }
        
        #endregion
        
        #region Command Event Handlers
        
        private void OnCommandExecuted(object sender, CommandExecutedEventArgs e)
        {
            responseHistory.Add(e.Result);
            Logger.LogDbgVerbose($"Command executed: {e.Command.Description} ({e.Duration.TotalMilliseconds}ms)", this);
            Repaint();
        }
        
        private void OnCommandUndone(object sender, CommandUndoneEventArgs e)
        {
            responseHistory.Add(e.Result);
            Logger.LogDbgVerbose($"Command undone: {e.Command.Description} ({e.Duration.TotalMilliseconds}ms)", this);
            Repaint();
        }
        
        private void OnCommandRedone(object sender, CommandRedoneEventArgs e)
        {
            responseHistory.Add(e.Result);
            Logger.LogDbgVerbose($"Command redone: {e.Command.Description} ({e.Duration.TotalMilliseconds}ms)", this);
            Repaint();
        }
        
        #endregion
        
        #region GUI Rendering
        
        private void OnGUI()
        {
            if (!isInitialized)
            {
                EditorGUILayout.LabelField("Initializing...", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            DrawHeader();
            DrawUndoRedoPanel();
            DrawProgressBar();
            DrawSchemeManagement();
            DrawSchemeExplorer();
            DrawResponseHistory();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Async Schema Editor", EditorStyles.largeLabel);
            EditorGUILayout.Space();
        }
        
        private void DrawUndoRedoPanel()
        {
            _showUndoRedoPanel = EditorGUILayout.Foldout(_showUndoRedoPanel, "Undo/Redo Controls");
            
            if (_showUndoRedoPanel)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUI.BeginDisabledGroup(!_commandHistory.CanUndo || _operationInProgress);
                if (GUILayout.Button($"Undo ({_commandHistory.UndoHistory.Count})", GUILayout.Width(100)))
                {
                    _ = ExecuteUndoAsync();
                }
                EditorGUI.EndDisabledGroup();
                
                EditorGUI.BeginDisabledGroup(!_commandHistory.CanRedo || _operationInProgress);
                if (GUILayout.Button($"Redo ({_commandHistory.RedoHistory.Count})", GUILayout.Width(100)))
                {
                    _ = ExecuteRedoAsync();
                }
                EditorGUI.EndDisabledGroup();
                
                EditorGUI.BeginDisabledGroup(_commandHistory.Count == 0 || _operationInProgress);
                if (GUILayout.Button("Clear History", GUILayout.Width(100)))
                {
                    _commandHistory.ClearHistory();
                }
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndHorizontal();
                
                // Show last command
                if (_commandHistory.LastCommand != null)
                {
                    EditorGUILayout.LabelField($"Last Command: {_commandHistory.LastCommand.Description}", EditorStyles.miniLabel);
                }
                
                EditorGUILayout.Space();
            }
        }
        
        private void DrawProgressBar()
        {
            if (_operationInProgress)
            {
                EditorGUILayout.LabelField($"Operation: {_currentOperationDescription}");
                EditorGUILayout.LabelField($"Progress: {_currentProgressMessage}");
                
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(rect, _currentProgress, $"{_currentProgress:P0}");
                
                if (GUILayout.Button("Cancel Operation", GUILayout.Width(120)))
                {
                    CancelCurrentOperation();
                }
                
                EditorGUILayout.Space();
            }
        }
        
        private void DrawSchemeManagement()
        {
            EditorGUILayout.LabelField("Scheme Management", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            newSchemeName = EditorGUILayout.TextField("New Scheme Name:", newSchemeName);
            
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(newSchemeName) || _operationInProgress);
            if (GUILayout.Button("Create Scheme", GUILayout.Width(120)))
            {
                _ = CreateNewSchemeAsync(newSchemeName);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        private void DrawSchemeExplorer()
        {
            EditorGUILayout.LabelField("Available Schemes", EditorStyles.boldLabel);
            
            explorerScrollPosition = EditorGUILayout.BeginScrollView(explorerScrollPosition, GUILayout.Height(200));
            
            foreach (var schemeName in AllSchemes)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool isSelected = schemeName == selectedSchemeName;
                if (GUILayout.Toggle(isSelected, schemeName, EditorStyles.radioButton))
                {
                    if (!isSelected)
                    {
                        selectedSchemeName = schemeName;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();
        }
        
        private void DrawResponseHistory()
        {
            EditorGUILayout.LabelField("Response History", EditorStyles.boldLabel);
            
            if (responseHistory.Count > 0)
            {
                var lastResponse = responseHistory.Last();
                var color = lastResponse.IsSuccess ? Color.green : Color.red;
                
                GUI.contentColor = color;
                EditorGUILayout.LabelField($"Last Result: {lastResponse.Status} - {lastResponse.Message}");
                GUI.contentColor = Color.white;
            }
            
            if (GUILayout.Button("Clear History"))
            {
                responseHistory.Clear();
            }
        }
        
        #endregion
        
        #region Async Operations
        
        private async Task CreateNewSchemeAsync(string schemeName)
        {
            if (_operationInProgress) return;
            
            _operationInProgress = true;
            _currentOperationDescription = $"Creating scheme '{schemeName}'";
            
            try
            {
                var progress = new Progress<CommandProgress>(UpdateProgress);
                var newScheme = new DataScheme(schemeName);
                
                var command = new LoadDataSchemeCommand(
                    newScheme,
                    overwriteExisting: false,
                    progress: progress,
                    storage: _storage
                );
                
                var result = await _commandHistory.ExecuteAsync(command, _cancellationTokenSource.Token);
                
                if (result.IsSuccess)
                {
                    selectedSchemeName = schemeName;
                    newSchemeName = string.Empty;
                    
                    // Save the changes
                    await SaveAsync();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogDbgVerbose("Create scheme operation was cancelled", this);
            }
            catch (Exception ex)
            {
                Logger.LogDbgError($"Failed to create scheme: {ex.Message}", this);
            }
            finally
            {
                _operationInProgress = false;
                Repaint();
            }
        }
        
        private async Task ExecuteUndoAsync()
        {
            if (_operationInProgress) return;
            
            _operationInProgress = true;
            _currentOperationDescription = "Undoing last command";
            
            try
            {
                var result = await _commandHistory.UndoAsync(_cancellationTokenSource.Token);
                
                if (result.IsSuccess)
                {
                    await SaveAsync();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogDbgVerbose("Undo operation was cancelled", this);
            }
            catch (Exception ex)
            {
                Logger.LogDbgError($"Failed to undo: {ex.Message}", this);
            }
            finally
            {
                _operationInProgress = false;
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
                var result = await _commandHistory.RedoAsync(_cancellationTokenSource.Token);
                
                if (result.IsSuccess)
                {
                    await SaveAsync();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogDbgVerbose("Redo operation was cancelled", this);
            }
            catch (Exception ex)
            {
                Logger.LogDbgError($"Failed to redo: {ex.Message}", this);
            }
            finally
            {
                _operationInProgress = false;
                Repaint();
            }
        }
        
        private async Task SaveAsync()
        {
            try
            {
                // Use existing save functionality for now
                await Task.Run(() => Core.Schema.Save());
                Logger.LogDbgVerbose("Schema saved successfully", this);
            }
            catch (Exception ex)
            {
                Logger.LogDbgError($"Failed to save schema: {ex.Message}", this);
            }
        }
        
        #endregion
        
        #region Progress and Cancellation
        
        private void UpdateProgress(CommandProgress progress)
        {
            _currentProgress = progress.Value;
            _currentProgressMessage = progress.Message;
            
            // Force repaint on main thread
            if (EditorApplication.isCompiling || EditorApplication.isPlaying)
                return;
            
            EditorApplication.delayCall += Repaint;
        }
        
        private void CancelCurrentOperation()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            
            _operationInProgress = false;
            Logger.LogDbgVerbose("Current operation cancelled", this);
            
            Repaint();
        }
        
        #endregion
    }
}