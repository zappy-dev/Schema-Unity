# Async Command System Demonstration

## Overview
This document demonstrates the new async Command pattern implemented for the Schema system, showing the improvements over the synchronous approach.

## Before vs After Comparison

### Before: Synchronous Schema Operations

```csharp
// OLD: Synchronous pattern - blocking operations
public void CreateScheme()
{
    var newScheme = new DataScheme("MyScheme");
    
    // Blocking call - no cancellation, no progress reporting
    var result = Schema.LoadDataScheme(newScheme, overwriteExisting: false);
    
    if (result.Failed)
    {
        Debug.LogError($"Failed to create scheme: {result.Message}");
        return;
    }
    
    // No undo support - changes are permanent
    Debug.Log("Scheme created successfully");
}

// OLD: Direct data mutations - no undo support
public void UpdateEntry(DataScheme scheme, DataEntry entry, string attributeName, object value)
{
    var result = scheme.SetDataOnEntry(entry, attributeName, value);
    
    if (result.Failed)
    {
        Debug.LogError($"Failed to update entry: {result.Message}");
        return;
    }
    
    // Save changes immediately - no way to undo
    Schema.Save();
}
```

### After: Async Command Pattern

```csharp
// NEW: Async command pattern - non-blocking, cancellable, undoable
public async Task CreateSchemeAsync()
{
    var newScheme = new DataScheme("MyScheme");
    
    // Create progress reporter for Unity UI
    using var progressReporter = ProgressReporterFactory.CreateForSchemaLoad("MyScheme");
    
    // Create command with full async support
    var command = new LoadDataSchemeCommand(
        newScheme,
        overwriteExisting: false,
        progress: progressReporter,
        storage: new AsyncFileStorage(),
        logger: new UnityLogger()
    );
    
    // Execute with cancellation support
    var result = await _commandHistory.ExecuteAsync(command, cancellationToken);
    
    if (result.IsFailure)
    {
        Debug.LogError($"Failed to create scheme: {result.Message}");
        return;
    }
    
    // Command is automatically added to undo history
    Debug.Log($"Scheme created successfully in {result.Duration.TotalMilliseconds}ms");
    Debug.Log($"Can undo: {_commandHistory.CanUndo}");
}

// NEW: Command-based data updates with undo support
public async Task UpdateEntryAsync(DataScheme scheme, DataEntry entry, string attributeName, object value)
{
    var command = new SetDataOnEntryCommand(scheme, entry, attributeName, value);
    
    var result = await _commandHistory.ExecuteAsync(command, cancellationToken);
    
    if (result.IsFailure)
    {
        Debug.LogError($"Failed to update entry: {result.Message}");
        return;
    }
    
    Debug.Log($"Entry updated successfully - can undo: {_commandHistory.CanUndo}");
}

// NEW: Easy undo/redo operations
public async Task UndoLastOperationAsync()
{
    if (!_commandHistory.CanUndo)
    {
        Debug.Log("Nothing to undo");
        return;
    }
    
    var result = await _commandHistory.UndoAsync(cancellationToken);
    
    if (result.IsSuccess)
    {
        Debug.Log($"Successfully undone: {result.Message}");
    }
    else
    {
        Debug.LogError($"Failed to undo: {result.Message}");
    }
}
```

## Key Improvements

### 1. Async Operations with Cancellation
```csharp
// Operations can be cancelled mid-execution
var cancellationTokenSource = new CancellationTokenSource();

// Cancel after 5 seconds
cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    var result = await command.ExecuteAsync(cancellationTokenSource.Token);
}
catch (OperationCanceledException)
{
    Debug.Log("Operation was cancelled");
}
```

### 2. Progress Reporting
```csharp
// Commands report progress during execution
var progress = new Progress<CommandProgress>(progress =>
{
    Debug.Log($"Progress: {progress.Message} ({progress.Value:P0})");
    EditorUtility.DisplayProgressBar("Loading", progress.Message, progress.Value);
});

var command = new LoadDataSchemeCommand(scheme, false, progress: progress);
```

### 3. Comprehensive Undo/Redo System
```csharp
// Full command history with undo/redo
Debug.Log($"Command History: {_commandHistory.Count} commands");
Debug.Log($"Can Undo: {_commandHistory.CanUndo} ({_commandHistory.UndoHistory.Count} commands)");
Debug.Log($"Can Redo: {_commandHistory.CanRedo} ({_commandHistory.RedoHistory.Count} commands)");

// Undo last command
await _commandHistory.UndoAsync();

// Redo last undone command
await _commandHistory.RedoAsync();

// Clear all history
_commandHistory.ClearHistory();
```

### 4. Enhanced Error Handling
```csharp
// Rich error information with timing and context
var result = await command.ExecuteAsync();

if (result.IsFailure)
{
    Debug.LogError($"Command failed: {result.Message}");
    Debug.LogError($"Duration: {result.Duration.TotalMilliseconds}ms");
    
    if (result.Exception != null)
    {
        Debug.LogException(result.Exception);
    }
}
```

### 5. Event-Driven Architecture
```csharp
// Subscribe to command events for UI updates
_commandHistory.CommandExecuted += (sender, e) =>
{
    Debug.Log($"Command executed: {e.Command.Description} in {e.Duration.TotalMilliseconds}ms");
    UpdateUI();
};

_commandHistory.CommandUndone += (sender, e) =>
{
    Debug.Log($"Command undone: {e.Command.Description}");
    UpdateUI();
};
```

## Unity Integration

### Updated SchemaEditorWindow Features

```csharp
// NEW: Async Schema Editor Window with full command support
public class AsyncSchemaEditorWindow : EditorWindow
{
    private readonly ICommandHistory _commandHistory = new CommandHistory();
    private CancellationTokenSource _cancellationTokenSource;
    
    // Real-time progress display
    private void DrawProgressBar()
    {
        if (_operationInProgress)
        {
            EditorGUILayout.LabelField($"Operation: {_currentOperationDescription}");
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(rect, _currentProgress, _currentProgressMessage);
            
            if (GUILayout.Button("Cancel"))
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }
    
    // Undo/Redo UI controls
    private void DrawUndoRedoPanel()
    {
        EditorGUILayout.BeginHorizontal();
        
        EditorGUI.BeginDisabledGroup(!_commandHistory.CanUndo);
        if (GUILayout.Button($"Undo ({_commandHistory.UndoHistory.Count})"))
        {
            _ = _commandHistory.UndoAsync();
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUI.BeginDisabledGroup(!_commandHistory.CanRedo);
        if (GUILayout.Button($"Redo ({_commandHistory.RedoHistory.Count})"))
        {
            _ = _commandHistory.RedoAsync();
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
    }
}
```

### Progress Integration
```csharp
// Unity-specific progress reporter
using var progressReporter = ProgressReporterFactory.CreateForSchemaLoad(schemeName);

var command = new LoadDataSchemeCommand(
    scheme,
    overwriteExisting: false,
    progress: progressReporter
);

// Progress automatically shown in Unity's progress bar
var result = await _commandHistory.ExecuteAsync(command);
```

## Performance Benefits

### Before: Synchronous Blocking
- **UI Freezing**: Long operations block the Unity editor
- **No Cancellation**: Operations must complete or fail completely
- **No Progress**: Users don't know operation status
- **Memory Pressure**: All operations hold resources until complete

### After: Async Non-Blocking
- **Responsive UI**: Unity editor remains responsive during operations
- **Cancellable**: Operations can be cancelled at any point
- **Progress Reporting**: Real-time feedback to users
- **Resource Efficient**: Async operations don't block threads

## Testing Support

### Mock Storage for Testing
```csharp
// Easy testing with mock storage
var mockStorage = new MockAsyncStorage();
var command = new LoadDataSchemeCommand(scheme, false, storage: mockStorage);

// Test cancellation
var cts = new CancellationTokenSource();
cts.Cancel();

var result = await command.ExecuteAsync(cts.Token);
Assert.IsTrue(result.IsCancelled);
```

### Command History Testing
```csharp
// Test undo/redo functionality
var history = new CommandHistory();
var command = new LoadDataSchemeCommand(scheme, false);

// Execute command
var result = await history.ExecuteAsync(command);
Assert.IsTrue(result.IsSuccess);
Assert.IsTrue(history.CanUndo);

// Undo command
var undoResult = await history.UndoAsync();
Assert.IsTrue(undoResult.IsSuccess);
Assert.IsTrue(history.CanRedo);
```

## Migration Path

### Backward Compatibility
```csharp
// Old synchronous methods still work during transition
[Obsolete("Use LoadDataSchemeAsync instead")]
public static SchemaResult LoadDataScheme(DataScheme scheme, bool overwriteExisting)
{
    // Wrapper that calls async version
    var result = LoadDataSchemeAsync(scheme, overwriteExisting).GetAwaiter().GetResult();
    return ConvertToSchemaResult(result);
}
```

### Gradual Migration
1. **Phase 1**: New async methods available alongside old ones
2. **Phase 2**: Old methods marked as `[Obsolete]` with migration guidance
3. **Phase 3**: Old methods removed after full migration

This async Command system provides a robust foundation for modern, responsive Schema operations with full undo/redo support and excellent user experience.