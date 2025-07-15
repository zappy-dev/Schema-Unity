using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Logging;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Implementation of command history for managing undo/redo operations
    /// </summary>
    public class CommandHistory : ICommandHistory
    {
        private readonly Stack<ISchemaCommand> _undoStack = new Stack<ISchemaCommand>();
        private readonly Stack<ISchemaCommand> _redoStack = new Stack<ISchemaCommand>();
        private readonly List<ISchemaCommand> _allCommands = new List<ISchemaCommand>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ILogger _logger;
        
        /// <summary>
        /// Maximum number of commands to keep in history
        /// </summary>
        public int MaxHistorySize { get; set; } = 100;
        
        public bool CanUndo
        {
            get
            {
                Logger.LogVerbose($"CanUndo: {_undoStack.Count > 0} (count: {_undoStack.Count})");
                return _undoStack.Count > 0;
            }
        }

        public bool CanRedo => _redoStack.Count > 0;
        
        public IReadOnlyList<ISchemaCommand> History => _allCommands.AsReadOnly();
        public IReadOnlyList<ISchemaCommand> UndoHistory => _undoStack.ToArray();
        public IReadOnlyList<ISchemaCommand> RedoHistory => _redoStack.ToArray();
        
        public ISchemaCommand LastCommand => _allCommands.LastOrDefault();
        public int Count => _allCommands.Count;
        
        public event EventHandler<CommandExecutedEventArgs> CommandExecuted;
        public event EventHandler<CommandUndoneEventArgs> CommandUndone;
        public event EventHandler<CommandRedoneEventArgs> CommandRedone;
        public event EventHandler HistoryCleared;
        
        public CommandHistory(ILogger logger = null)
        {
            _logger = logger ?? new NullLogger();
        }
        
        public async Task<CommandResult<TResult>> ExecuteAsync<TResult>(ISchemaCommand<TResult> command, CancellationToken cancellationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                Logger.LogDbgVerbose($"Executing command: {command.Description}", this);
                
                var stopwatch = Stopwatch.StartNew();
                var result = await command.ExecuteAsync(cancellationToken);
                stopwatch.Stop();
                
                if (result.IsSuccess)
                {
                    // Add to history if command executed successfully
                    _allCommands.Add(command as ISchemaCommand);
                    
                    // Add to undo stack if command can be undone
                    if (command.CanUndo)
                    {
                        _undoStack.Push(command as ISchemaCommand);
                        _redoStack.Clear(); // Clear redo stack on new command
                    }
                    
                    // Maintain maximum history size
                    if (_allCommands.Count > MaxHistorySize)
                    {
                        var oldestCommand = _allCommands[0];
                        _allCommands.RemoveAt(0);
                        
                        // Remove from undo stack if it's there
                        if (_undoStack.Contains(oldestCommand))
                        {
                            var tempStack = new Stack<ISchemaCommand>();
                            while (_undoStack.Count > 0 && _undoStack.Peek() != oldestCommand)
                            {
                                tempStack.Push(_undoStack.Pop());
                            }
                            if (_undoStack.Count > 0)
                            {
                                _undoStack.Pop(); // Remove the oldest command
                            }
                            while (tempStack.Count > 0)
                            {
                                _undoStack.Push(tempStack.Pop());
                            }
                        }
                    }
                    
                    Logger.LogDbgVerbose($"Command executed successfully: {command.Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                    CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command as ISchemaCommand, result.ToCommandResult(), stopwatch.Elapsed));
                }
                else
                {
                    Logger.LogDbgError($"Command failed: {command.Description} - {result.Message}", this);
                }
                
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!CanUndo)
                {
                    return CommandResult.Failure("No commands available to undo");
                }
                
                var command = _undoStack.Pop();
                Logger.LogVerbose($"Undoing command: {command.Description}", this);
                
                var stopwatch = Stopwatch.StartNew();
                var result = await command.UndoAsync(cancellationToken);
                stopwatch.Stop();
                
                if (result.IsSuccess)
                {
                    _redoStack.Push(command);
                    Logger.LogDbgVerbose($"Command undone successfully: {command.Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                    CommandUndone?.Invoke(this, new CommandUndoneEventArgs(command, result, stopwatch.Elapsed));
                }
                else
                {
                    // Put command back on undo stack if undo failed
                    _undoStack.Push(command);
                    Logger.LogDbgError($"Undo failed: {command.Description} - {result.Message}", this);
                }
                
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<CommandResult> RedoAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!CanRedo)
                {
                    return CommandResult.Failure("No commands available to redo");
                }
                
                var command = _redoStack.Pop();
                Logger.LogDbgVerbose($"Redoing command: {command.Description}", this);
                
                var stopwatch = Stopwatch.StartNew();
                var result = await command.RedoAsync(cancellationToken);
                stopwatch.Stop();
                
                if (result.IsSuccess)
                {
                    _undoStack.Push(command);
                    Logger.LogDbgVerbose($"Command redone successfully: {command.Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                    CommandRedone?.Invoke(this, new CommandRedoneEventArgs(command, result, stopwatch.Elapsed));
                }
                else
                {
                    // Put command back on redo stack if redo failed
                    _redoStack.Push(command);
                    Logger.LogDbgError($"Redo failed: {command.Description} - {result.Message}", this);
                }
                
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public void ClearHistory()
        {
            _semaphore.Wait();
            try
            {
                _undoStack.Clear();
                _redoStack.Clear();
                _allCommands.Clear();
                
                Logger.LogDbgVerbose("Command history cleared", this);
                HistoryCleared?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
    
    /// <summary>
    /// Null logger implementation for when no logger is provided
    /// </summary>
    internal class NullLogger : ILogger
    {
        public Logger.LogLevel LogLevel { get; set; } = Logger.LogLevel.VERBOSE;
        
        public void Log(Logger.LogLevel logLevel, string message) { }
        public void LogDbgVerbose(string message, object context = null) { }
        public void LogDbgError(string message, object context = null) { }
        public void Log(string message, object context = null) { }
    }
}