using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Logging;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Base class for schema commands providing common functionality
    /// </summary>
    /// <typeparam name="TResult">Type of the result returned by the command</typeparam>
    public abstract class SchemaCommandBase<TResult> : ISchemaCommand<TResult>
    {
        /// <summary>
        /// Unique identifier for this command instance
        /// </summary>
        public CommandId Id { get; }
        
        /// <summary>
        /// Human-readable description of the command
        /// </summary>
        public abstract string Description { get; }

        public async Task<CommandResult> RedoAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteAsync(cancellationToken);
            return result.ToCommandResult();
        }

        /// <summary>
        /// Indicates whether this command can be undone
        /// </summary>
        public virtual bool CanUndo => true;
        
        /// <summary>
        /// Timestamp when the command was created
        /// </summary>
        public DateTime CreatedAt { get; }
        
        protected SchemaCommandBase()
        {
            Id = CommandId.NewId();
            CreatedAt = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Executes the command asynchronously
        /// </summary>
        public async Task<CommandResult<TResult>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDbgVerbose($"Starting execution of command: {Description}", this);
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var result = await ExecuteInternalAsync(cancellationToken);
                
                stopwatch.Stop();
                Logger.LogDbgVerbose($"Command executed successfully: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                
                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Logger.LogDbgVerbose($"Command cancelled: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                return CommandResult<TResult>.Cancelled($"Command '{Description}' was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogDbgError($"Command failed: {Description} - {ex.Message} ({stopwatch.ElapsedMilliseconds}ms)", this);
                return CommandResult<TResult>.Failure($"Command '{Description}' failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        /// <summary>
        /// Undoes the command asynchronously
        /// </summary>
        public async Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default)
        {
            if (!CanUndo)
            {
                return CommandResult.Failure($"Command '{Description}' cannot be undone");
            }
            
            Logger.LogDbgVerbose($"Starting undo of command: {Description}", this);
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var result = await UndoInternalAsync(cancellationToken);
                
                stopwatch.Stop();
                Logger.LogDbgVerbose($"Command undone successfully: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                
                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Logger.LogDbgVerbose($"Command undo cancelled: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                return CommandResult.Cancelled($"Undo of command '{Description}' was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogDbgError($"Command undo failed: {Description} - {ex.Message} ({stopwatch.ElapsedMilliseconds}ms)", this);
                return CommandResult.Failure($"Undo of command '{Description}' failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        /// <summary>
        /// Derived classes implement the actual command execution logic
        /// </summary>
        protected abstract Task<CommandResult<TResult>> ExecuteInternalAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Derived classes implement the actual undo logic
        /// </summary>
        protected abstract Task<CommandResult> UndoInternalAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Helper method to check for cancellation and throw if requested
        /// </summary>
        protected void ThrowIfCancellationRequested(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        
        /// <summary>
        /// Helper method to report progress if progress reporter is available
        /// </summary>
        protected void ReportProgress(IProgress<CommandProgress> progress, float value, string message)
        {
            progress?.Report(new CommandProgress(value, message, Id, Description));
        }
        
        public override string ToString()
        {
            return $"{GetType().Name}[{Id}]: {Description}";
        }
    }
}