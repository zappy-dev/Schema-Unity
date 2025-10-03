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
    public abstract class SchemaCommandBase : ISchemaCommand
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
            return result;
        }

        private bool wasExecuted = false;
        
        /// <summary>
        /// Indicates whether this command can be undone
        /// </summary>
        public virtual bool CanUndo => wasExecuted; // cannot undo something that wasn't executed
        
        /// <summary>
        /// Timestamp when the command was created
        /// </summary>
        public DateTime CreatedAt { get; }
        
        public SchemaContext Context { get; }
        
        protected SchemaCommandBase(SchemaContext context)
        {
            Id = CommandId.NewId();
            CreatedAt = DateTime.UtcNow;
            Context = context;
        }
        
        /// <summary>
        /// Executes the command asynchronously
        /// </summary>
        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDbgVerbose($"Starting execution of command: {Description}", this);
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ExecuteInternalAsync(cancellationToken);
                wasExecuted = true;

                stopwatch.Stop();
                Logger.LogDbgVerbose(
                    $"Command executed successfully: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);

                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Logger.LogDbgVerbose($"Command cancelled: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                return CommandResult.Cancel($"Command '{Description}' was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogDbgError($"Command failed: {Description} - {ex.Message} ({stopwatch.ElapsedMilliseconds}ms)",
                    this);
                return CommandResult.Fail($"Command '{Description}' failed: {ex.Message}", ex,
                    stopwatch.Elapsed);
            }
        }
        
        /// <summary>
        /// Undoes the command asynchronously
        /// </summary>
        public async Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default)
        {
            if (!CanUndo)
            {
                return CommandResult.Fail($"Command '{Description}' cannot be undone");
            }
            
            Logger.LogDbgVerbose($"Starting undo of command: {Description}", this);
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var result = await UndoInternalAsync(cancellationToken);
                wasExecuted = false;
                
                stopwatch.Stop();
                Logger.LogDbgVerbose($"Command undone successfully: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                
                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Logger.LogDbgVerbose($"Command undo cancelled: {Description} ({stopwatch.ElapsedMilliseconds}ms)", this);
                return CommandResult.Cancel($"Undo of command '{Description}' was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogDbgError($"Command undo failed: {Description} - {ex.Message} ({stopwatch.ElapsedMilliseconds}ms)", this);
                return CommandResult.Fail($"Undo of command '{Description}' failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }
        
        /// <summary>
        /// Derived classes implement the actual command execution logic
        /// </summary>
        protected abstract Task<CommandResult> ExecuteInternalAsync(CancellationToken cancellationToken);
        
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