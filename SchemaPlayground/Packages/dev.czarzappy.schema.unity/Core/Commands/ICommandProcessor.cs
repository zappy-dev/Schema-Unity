using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Interface for managing command execution history and undo/redo operations
    /// </summary>
    public interface ICommandProcessor
    {
        /// <summary>
        /// Executes a command and adds it to the history if successful
        /// </summary>
        /// <param name="command">Command to execute</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Result of the command execution</returns>
        Task<CommandResult> ExecuteAsync(ISchemaCommand command, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Undoes the last executed command
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the undo operation</param>
        /// <returns>Result of the undo operation</returns>
        Task<CommandResult> UndoAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Redoes the last undone command
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the redo operation</param>
        /// <returns>Result of the redo operation</returns>
        Task<CommandResult> RedoAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Indicates whether there are commands available to undo
        /// </summary>
        bool CanUndo { get; }
        
        /// <summary>
        /// Indicates whether there are commands available to redo
        /// </summary>
        bool CanRedo { get; }
        
        /// <summary>
        /// Gets the command history (read-only)
        /// </summary>
        IReadOnlyList<ISchemaCommand> History { get; }
        
        /// <summary>
        /// Gets the undo history (read-only)
        /// </summary>
        IReadOnlyList<ISchemaCommand> UndoHistory { get; }
        
        /// <summary>
        /// Gets the redo history (read-only)
        /// </summary>
        IReadOnlyList<ISchemaCommand> RedoHistory { get; }
        
        /// <summary>
        /// Clears all command history
        /// </summary>
        void ClearHistory();
        
        /// <summary>
        /// Gets the last executed command
        /// </summary>
        ISchemaCommand LastCommand { get; }
        
        /// <summary>
        /// Gets the total number of commands in history
        /// </summary>
        int Count { get; }
        
        /// <summary>
        /// Event raised when a command is executed
        /// </summary>
        event EventHandler<CommandExecutedEventArgs> CommandExecuted;
        
        /// <summary>
        /// Event raised when a command is undone
        /// </summary>
        event EventHandler<CommandUndoneEventArgs> CommandUndone;
        
        /// <summary>
        /// Event raised when a command is redone
        /// </summary>
        event EventHandler<CommandRedoneEventArgs> CommandRedone;
        
        /// <summary>
        /// Event raised when the command history is cleared
        /// </summary>
        event EventHandler HistoryCleared;
    }
    
    /// <summary>
    /// Event arguments for command executed events
    /// </summary>
    public class CommandExecutedEventArgs : EventArgs
    {
        public ISchemaCommand Command { get; }
        public CommandResult Result { get; }
        public TimeSpan Duration { get; }
        
        public CommandExecutedEventArgs(ISchemaCommand command, CommandResult result, TimeSpan duration)
        {
            Command = command;
            Result = result;
            Duration = duration;
        }
    }
    
    /// <summary>
    /// Event arguments for command undone events
    /// </summary>
    public class CommandUndoneEventArgs : EventArgs
    {
        public ISchemaCommand Command { get; }
        public CommandResult Result { get; }
        public TimeSpan Duration { get; }
        
        public CommandUndoneEventArgs(ISchemaCommand command, CommandResult result, TimeSpan duration)
        {
            Command = command;
            Result = result;
            Duration = duration;
        }
    }
    
    /// <summary>
    /// Event arguments for command redone events
    /// </summary>
    public class CommandRedoneEventArgs : EventArgs
    {
        public ISchemaCommand Command { get; }
        public CommandResult Result { get; }
        public TimeSpan Duration { get; }
        
        public CommandRedoneEventArgs(ISchemaCommand command, CommandResult result, TimeSpan duration)
        {
            Command = command;
            Result = result;
            Duration = duration;
        }
    }
}