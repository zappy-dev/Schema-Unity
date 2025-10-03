using System;
using System.Threading;
using System.Threading.Tasks;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Non-generic base interface for command operations
    /// </summary>
    public interface ISchemaCommand
    {
        Task<CommandResult> ExecuteAsync(CancellationToken ct = default);
        
        /// <summary>
        /// Undoes the command asynchronously
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the undo operation</param>
        /// <returns>Command result indicating undo success</returns>
        Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default);
        
        Task<CommandResult> RedoAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Indicates whether this command can be undone
        /// </summary>
        bool CanUndo { get; }
        
        /// <summary>
        /// Human-readable description of the command
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Unique identifier for this command instance
        /// </summary>
        CommandId Id { get; }
        
        /// <summary>
        /// Timestamp when the command was created
        /// </summary>
        DateTime CreatedAt { get; }
    }
}