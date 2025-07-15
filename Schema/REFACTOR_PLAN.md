# Schema to Async Command Interface Refactor Plan

## Executive Summary
Convert the current synchronous Schema static interface to an async Command pattern with cancellation token support and robust undo functionality. This will enable better long-running operation support, cancellation capabilities, and comprehensive undo/redo operations.

## Current State Analysis

### Current Architecture
```csharp
// Current synchronous pattern
public static class Schema
{
    public static SchemaResult LoadDataScheme(DataScheme scheme, bool overwriteExisting);
    public static SchemaResult UpdateIdentifierValue(string schemeName, string identifierAttribute, object oldValue, object newValue);
    public static SchemaResult<DataScheme> GetScheme(string schemeName);
}

// Current result pattern
public struct SchemaResult
{
    public RequestStatus Status { get; }
    public string Message { get; }
    public bool Passed { get; }
    public bool Failed { get; }
}
```

### Identified Pain Points
1. **No Cancellation Support**: Long-running operations cannot be cancelled
2. **No Undo/Redo**: Operations are permanent with no rollback capability
3. **Synchronous I/O**: File operations block the calling thread
4. **Limited Observability**: No progress reporting for long operations
5. **Error Recovery**: Partial failures leave system in inconsistent state

## Critical Considerations from Codebase Review

### 1. File I/O and Persistence Layer
**Current Pattern:**
```csharp
// Schema.Persistence.cs - Synchronous file operations
public static SchemaResult<ManifestLoadStatus> LoadManifestFromPath(string manifestPath, IProgress<(float, string)> progress = null)
{
    if (!Storage.FileSystem.FileExists(manifestPath))
        return SchemaResult<ManifestLoadStatus>.Fail($"No Manifest scheme found.");
    
    if (!Storage.DefaultManifestStorageFormat.DeserializeFromFile(manifestPath).Try(out var loadedManifestSchema))
        return SchemaResult<ManifestLoadStatus>.Fail("Failed to load manifest schema.");
}
```

**Refactor Implications:**
- File operations need async/await conversion
- Progress reporting already exists but needs async integration
- Storage layer needs async interface
- Cancellation tokens must be threaded through I/O operations

### 2. Data Mutation and State Management
**Current Pattern:**
```csharp
// DataScheme.cs - Direct state mutation
public SchemaResult SetDataOnEntry(DataEntry entry, string attributeName, object value, bool allowIdentifierUpdate = false)
{
    var attrResult = GetAttribute(attributeName);
    if (!attrResult.Try(out var attr))
        return SchemaResult.Fail($"Attribute '{attributeName}' not found in scheme '{SchemeName}'.");
    
    IsDirty = true;  // Direct state mutation
    return entry.SetData(attributeName, value);
}
```

**Refactor Implications:**
- State mutations must be captured for undo operations
- IsDirty flag pattern needs preservation
- Validation logic must be async-compatible
- Rollback operations need to restore exact previous state

### 3. Thread Safety and Locking
**Current Pattern:**
```csharp
// Schema.Persistence.cs - Manual locking
lock (manifestOperationLock)
{
    var prevDataSchemes = dataSchemes;
    dataSchemes.Clear();
    // ... operation
}
```

**Refactor Implications:**
- Manual locks must be replaced with async-compatible synchronization
- SemaphoreSlim for async operations
- Command execution must be thread-safe
- Undo operations require careful synchronization

### 4. Test Patterns and Backward Compatibility
**Current Pattern:**
```csharp
// TestSchema.cs - Simple synchronous tests
[Test]
public void Test_LoadDataScheme(bool overwriteExisting)
{
    // Arrange
    var newScheme = new DataScheme("Foo");
    
    // Act
    var addResponse = Schema.LoadDataScheme(newScheme, overwriteExisting);
    
    // Assert
    Assert.IsTrue(addResponse.Passed);
    Assert.That(Schema.NumAvailableSchemes, Is.EqualTo(2));
}
```

**Refactor Implications:**
- 143 existing tests must continue to pass
- Sync wrapper methods needed for backward compatibility
- Test framework may need async test patterns
- Assertion patterns need to work with CommandResult<T>

## Target Architecture

### Phase 1: Core Command Infrastructure

#### 1.1 Command Base Classes
```csharp
// Base command interface
public interface ISchemaCommand<TResult>
{
    Task<CommandResult<TResult>> ExecuteAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default);
    bool CanUndo { get; }
    string Description { get; }
    CommandId Id { get; }
}

// Enhanced result type
public class CommandResult<TResult>
{
    public CommandStatus Status { get; }
    public TResult Result { get; }
    public string Message { get; }
    public Exception Exception { get; }
    public TimeSpan Duration { get; }
    public bool CanUndo { get; }
    
    public static CommandResult<TResult> Success(TResult result, string message = null);
    public static CommandResult<TResult> Failure(string message, Exception exception = null);
    public static CommandResult<TResult> Cancelled(string message = null);
}

// Command execution context
public class CommandContext
{
    public CancellationToken CancellationToken { get; }
    public IProgress<CommandProgress> Progress { get; }
    public ICommandHistory History { get; }
    public ISchemaRepository Repository { get; }
}
```

#### 1.2 Command History & Undo System
```csharp
public interface ICommandHistory
{
    Task<CommandResult> ExecuteAsync<TResult>(ISchemaCommand<TResult> command, CancellationToken cancellationToken = default);
    Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> RedoAsync(CancellationToken cancellationToken = default);
    bool CanUndo { get; }
    bool CanRedo { get; }
    IReadOnlyList<ISchemaCommand> History { get; }
}

public class CommandHistory : ICommandHistory
{
    private readonly Stack<ISchemaCommand> _undoStack = new();
    private readonly Stack<ISchemaCommand> _redoStack = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger _logger;
    
    public async Task<CommandResult> ExecuteAsync<TResult>(ISchemaCommand<TResult> command, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var result = await command.ExecuteAsync(cancellationToken);
            if (result.Status == CommandStatus.Success && command.CanUndo)
            {
                _undoStack.Push(command);
                _redoStack.Clear(); // Clear redo stack on new command
            }
            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

#### 1.3 Async Storage Layer
```csharp
public interface IAsyncStorage
{
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<TResult> DeserializeFromFileAsync<TResult>(string path, CancellationToken cancellationToken = default);
    Task SerializeToFileAsync<TData>(string path, TData data, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
}

public class AsyncFileStorage : IAsyncStorage
{
    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => File.Exists(path), cancellationToken);
    }
    
    public async Task<TResult> DeserializeFromFileAsync<TResult>(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<TResult>(json);
    }
}
```

### Phase 2: Specific Command Implementations

#### 2.1 Load Data Scheme Command
```csharp
public class LoadDataSchemeCommand : ISchemaCommand<DataScheme>
{
    private readonly DataScheme _scheme;
    private readonly bool _overwriteExisting;
    private readonly string _importFilePath;
    private DataScheme _previousScheme; // For undo
    private bool _schemeExistedBefore;
    
    public LoadDataSchemeCommand(DataScheme scheme, bool overwriteExisting, string importFilePath = null)
    {
        _scheme = scheme;
        _overwriteExisting = overwriteExisting;
        _importFilePath = importFilePath;
        Id = CommandId.NewId();
    }
    
    public async Task<CommandResult<DataScheme>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Store previous state for undo
            _schemeExisteBefore = Schema.DoesSchemeExist(_scheme.SchemeName);
            if (_schemeExisteBefore)
            {
                _previousScheme = await Schema.GetSchemeAsync(_scheme.SchemeName, cancellationToken);
            }
            
            // Validate scheme data asynchronously
            await ValidateSchemeDataAsync(cancellationToken);
            
            // Load scheme with progress reporting
            var result = await LoadSchemeInternalAsync(cancellationToken);
            
            return CommandResult<DataScheme>.Success(result, $"Successfully loaded scheme '{_scheme.SchemeName}'");
        }
        catch (OperationCanceledException)
        {
            return CommandResult<DataScheme>.Cancelled("Load operation was cancelled");
        }
        catch (Exception ex)
        {
            return CommandResult<DataScheme>.Failure($"Failed to load scheme: {ex.Message}", ex);
        }
    }
    
    public async Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_schemeExisteBefore && _previousScheme != null)
            {
                // Restore previous scheme
                await Schema.LoadDataSchemeAsync(_previousScheme, true, cancellationToken);
            }
            else
            {
                // Remove scheme if it didn't exist before
                await Schema.RemoveSchemeAsync(_scheme.SchemeName, cancellationToken);
            }
            
            return CommandResult.Success($"Undone load of scheme '{_scheme.SchemeName}'");
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to undo load: {ex.Message}", ex);
        }
    }
    
    public bool CanUndo => true;
    public string Description => $"Load data scheme '{_scheme.SchemeName}'";
    public CommandId Id { get; }
}
```

#### 2.2 Update Identifier Value Command
```csharp
public class UpdateIdentifierValueCommand : ISchemaCommand<int>
{
    private readonly string _schemeName;
    private readonly string _identifierAttribute;
    private readonly object _oldValue;
    private readonly object _newValue;
    private readonly List<(string SchemeName, DataEntry Entry, string AttributeName, object OldValue)> _changedReferences = new();
    private (DataEntry Entry, object OldValue)? _originalEntry;
    
    public async Task<CommandResult<int>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Update identifier in target scheme
            var targetScheme = await Schema.GetSchemeAsync(_schemeName, cancellationToken);
            var entry = targetScheme.AllEntries.FirstOrDefault(e => 
                Equals(e.GetDataAsString(_identifierAttribute), _oldValue?.ToString()));
            
            if (entry == null)
                return CommandResult<int>.Failure($"Entry with {_identifierAttribute} == '{_oldValue}' not found");
            
            // Store original entry state for undo
            _originalEntry = (entry, entry.GetData(_identifierAttribute).Result);
            
            // Update the identifier
            await targetScheme.SetDataOnEntryAsync(entry, _identifierAttribute, _newValue, 
                allowIdentifierUpdate: true, cancellationToken);
            
            // 2. Update all references across all schemes
            int totalUpdated = 0;
            foreach (var scheme in Schema.GetSchemes())
            {
                if (scheme.SchemeName == _schemeName) continue;
                
                cancellationToken.ThrowIfCancellationRequested();
                
                var updated = await UpdateReferencesInSchemeAsync(scheme, cancellationToken);
                totalUpdated += updated;
            }
            
            return CommandResult<int>.Success(totalUpdated, $"Updated identifier and {totalUpdated} references");
        }
        catch (OperationCanceledException)
        {
            return CommandResult<int>.Cancelled("Update operation was cancelled");
        }
        catch (Exception ex)
        {
            return CommandResult<int>.Failure($"Failed to update identifier: {ex.Message}", ex);
        }
    }
    
    public async Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Restore original entry value
            if (_originalEntry.HasValue)
            {
                var targetScheme = await Schema.GetSchemeAsync(_schemeName, cancellationToken);
                await targetScheme.SetDataOnEntryAsync(_originalEntry.Value.Entry, _identifierAttribute, 
                    _originalEntry.Value.OldValue, allowIdentifierUpdate: true, cancellationToken);
            }
            
            // Restore all changed references
            foreach (var (schemeName, entry, attributeName, oldValue) in _changedReferences.AsEnumerable().Reverse())
            {
                var scheme = await Schema.GetSchemeAsync(schemeName, cancellationToken);
                await scheme.SetDataOnEntryAsync(entry, attributeName, oldValue, cancellationToken);
            }
            
            return CommandResult.Success($"Undone identifier update for '{_schemeName}.{_identifierAttribute}'");
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Failed to undo identifier update: {ex.Message}", ex);
        }
    }
    
    public bool CanUndo => true;
    public string Description => $"Update identifier '{_schemeName}.{_identifierAttribute}' from '{_oldValue}' to '{_newValue}'";
    public CommandId Id { get; }
}
```

### Phase 3: Async Schema Interface

#### 3.1 New Schema Interface
```csharp
public static class Schema
{
    private static readonly ICommandHistory _commandHistory = new CommandHistory();
    private static readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private static readonly IAsyncStorage _storage = new AsyncFileStorage();
    
    // Async command execution
    public static async Task<CommandResult<DataScheme>> LoadDataSchemeAsync(
        DataScheme scheme, 
        bool overwriteExisting, 
        string importFilePath = null,
        IProgress<(float, string)> progress = null,
        CancellationToken cancellationToken = default)
    {
        var command = new LoadDataSchemeCommand(scheme, overwriteExisting, importFilePath);
        return await _commandHistory.ExecuteAsync(command, cancellationToken);
    }
    
    public static async Task<CommandResult<int>> UpdateIdentifierValueAsync(
        string schemeName, 
        string identifierAttribute, 
        object oldValue, 
        object newValue,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateIdentifierValueCommand(schemeName, identifierAttribute, oldValue, newValue);
        return await _commandHistory.ExecuteAsync(command, cancellationToken);
    }
    
    // Undo/Redo operations
    public static async Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default)
    {
        return await _commandHistory.UndoAsync(cancellationToken);
    }
    
    public static async Task<CommandResult> RedoAsync(CancellationToken cancellationToken = default)
    {
        return await _commandHistory.RedoAsync(cancellationToken);
    }
    
    // Backward compatibility (deprecated)
    [Obsolete("Use LoadDataSchemeAsync instead")]
    public static SchemaResult LoadDataScheme(DataScheme scheme, bool overwriteExisting, string importFilePath = null)
    {
        var result = LoadDataSchemeAsync(scheme, overwriteExisting, importFilePath).GetAwaiter().GetResult();
        return new SchemaResult(
            result.Status == CommandStatus.Success ? SchemaResult.RequestStatus.Passed : SchemaResult.RequestStatus.Failed,
            result.Message
        );
    }
    
    [Obsolete("Use UpdateIdentifierValueAsync instead")]
    public static SchemaResult UpdateIdentifierValue(string schemeName, string identifierAttribute, object oldValue, object newValue)
    {
        var result = UpdateIdentifierValueAsync(schemeName, identifierAttribute, oldValue, newValue).GetAwaiter().GetResult();
        return new SchemaResult(
            result.Status == CommandStatus.Success ? SchemaResult.RequestStatus.Passed : SchemaResult.RequestStatus.Failed,
            result.Message
        );
    }
}
```

#### 3.2 Async DataScheme Operations
```csharp
public partial class DataScheme
{
    public async Task<CommandResult> SetDataOnEntryAsync(
        DataEntry entry, 
        string attributeName, 
        object value, 
        bool allowIdentifierUpdate = false,
        CancellationToken cancellationToken = default)
    {
        var command = new SetDataOnEntryCommand(this, entry, attributeName, value, allowIdentifierUpdate);
        return await Schema.ExecuteCommandAsync(command, cancellationToken);
    }
    
    // Backward compatibility
    [Obsolete("Use SetDataOnEntryAsync instead")]
    public SchemaResult SetDataOnEntry(DataEntry entry, string attributeName, object value, bool allowIdentifierUpdate = false)
    {
        var result = SetDataOnEntryAsync(entry, attributeName, value, allowIdentifierUpdate).GetAwaiter().GetResult();
        return new SchemaResult(
            result.Status == CommandStatus.Success ? SchemaResult.RequestStatus.Passed : SchemaResult.RequestStatus.Failed,
            result.Message
        );
    }
}
```

## Implementation Phases

### Phase 1: Infrastructure (Weeks 1-2)
- [ ] Create command base classes and interfaces
- [ ] Implement CommandResult<T> and CommandContext
- [ ] Build CommandHistory with undo/redo support
- [ ] Create async storage abstraction layer
- [ ] Set up cancellation token plumbing
- [ ] Replace manual locks with async synchronization

### Phase 2: Core Commands (Weeks 3-4)
- [ ] Implement LoadDataSchemeCommand with full undo support
- [ ] Implement UpdateIdentifierValueCommand with reference tracking
- [ ] Implement SetDataOnEntryCommand
- [ ] Convert file I/O operations to async
- [ ] Create command composition support
- [ ] Preserve IsDirty flag semantics

### Phase 3: Schema Interface Conversion (Weeks 5-6)
- [ ] Convert Schema static methods to async
- [ ] Add command history integration
- [ ] Implement backward compatibility layer
- [ ] Add progress reporting to long operations
- [ ] Thread cancellation tokens through all operations

### Phase 4: Advanced Features (Weeks 7-8)
- [ ] Implement command batching/transactions
- [ ] Add command serialization for persistence
- [ ] Create command validation pipeline
- [ ] Add operation timeouts and retry logic
- [ ] Implement command dependencies and sequencing

### Phase 5: Testing & Migration (Weeks 9-10)
- [ ] Create async test extension methods
- [ ] Update all 143 existing tests to use compatibility layer
- [ ] Add comprehensive async test suite
- [ ] Add cancellation token stress tests
- [ ] Test undo/redo functionality thoroughly
- [ ] Performance testing for async operations
- [ ] Migration guide for existing code

## Migration Strategy

### Backward Compatibility
1. **Deprecation Path**: Mark old synchronous methods as `[Obsolete]` with clear migration messages
2. **Adapter Pattern**: Provide sync wrappers that call async methods using `GetAwaiter().GetResult()`
3. **Gradual Migration**: Allow incremental adoption across codebase
4. **Result Type Mapping**: Convert `CommandResult<T>` to `SchemaResult` for compatibility

### Breaking Changes
1. **Method Signatures**: All operations now return `Task<CommandResult<T>>`
2. **Exception Handling**: Exceptions wrapped in CommandResult instead of thrown
3. **State Management**: Operations are now stateful for undo support
4. **Thread Safety**: Manual locks replaced with async synchronization primitives

### Testing Strategy
1. **Preserve Existing Tests**: Keep current 143 tests passing with compatibility layer
2. **New Async Tests**: Create comprehensive async test suite
3. **Cancellation Tests**: Test all cancellation scenarios
4. **Undo/Redo Tests**: Verify undo functionality for all commands
5. **Performance Tests**: Ensure async operations don't degrade performance
6. **Memory Tests**: Monitor for async-related memory leaks

## Success Metrics
- [ ] All 143 existing tests pass with compatibility layer
- [ ] >95% code coverage maintained
- [ ] All operations support cancellation within 100ms
- [ ] Undo/redo works for all supported commands
- [ ] Performance maintains or improves over current sync implementation
- [ ] Memory usage remains stable during long operations
- [ ] Progress reporting works for all long-running operations

## Risk Mitigation
1. **Gradual Rollout**: Implement behind feature flags
2. **Rollback Plan**: Keep sync methods until full migration complete
3. **Performance Monitoring**: Track async operation performance
4. **Memory Monitoring**: Watch for async-related memory leaks
5. **User Feedback**: Gather feedback on cancellation and undo UX
6. **Deadlock Prevention**: Careful async/await usage to prevent deadlocks
7. **State Consistency**: Ensure undo operations restore exact previous state