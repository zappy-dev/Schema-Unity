using System;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Status of a command execution
    /// </summary>
    public enum CommandStatus
    {
        /// <summary>
        /// Command executed successfully
        /// </summary>
        Success,
        
        /// <summary>
        /// Command failed to execute
        /// </summary>
        Failure,
        
        /// <summary>
        /// Command was cancelled before completion
        /// </summary>
        Cancelled
    }
    
    /// <summary>
    /// Result of a command execution with typed result
    /// </summary>
    /// <typeparam name="TResult">Type of the result value</typeparam>
    public class CommandResult<TResult>
    {
        /// <summary>
        /// Status of the command execution
        /// </summary>
        public CommandStatus Status { get; }
        
        /// <summary>
        /// Result value of the command (only valid when Status is Success)
        /// </summary>
        public TResult Result { get; }
        
        /// <summary>
        /// Human-readable message about the command execution
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Exception that occurred during execution (if any)
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// Time taken to execute the command
        /// </summary>
        public TimeSpan Duration { get; }
        
        /// <summary>
        /// Indicates whether the command can be undone
        /// </summary>
        public bool CanUndo { get; }
        
        /// <summary>
        /// Indicates whether the command executed successfully
        /// </summary>
        public bool IsSuccess => Status == CommandStatus.Success;
        
        /// <summary>
        /// Indicates whether the command failed
        /// </summary>
        public bool IsFailure => Status == CommandStatus.Failure;
        
        /// <summary>
        /// Indicates whether the command was cancelled
        /// </summary>
        public bool IsCancelled => Status == CommandStatus.Cancelled;
        
        private CommandResult(CommandStatus status, TResult result, string message, Exception exception, TimeSpan duration, bool canUndo)
        {
            Status = status;
            Result = result;
            Message = message ?? string.Empty;
            Exception = exception;
            Duration = duration;
            CanUndo = canUndo;
        }
        
        /// <summary>
        /// Creates a successful command result
        /// </summary>
        public static CommandResult<TResult> Success(TResult result, string message = null, TimeSpan duration = default, bool canUndo = true)
        {
            return new CommandResult<TResult>(CommandStatus.Success, result, message, null, duration, canUndo);
        }
        
        /// <summary>
        /// Creates a failed command result
        /// </summary>
        public static CommandResult<TResult> Failure(string message, Exception exception = null, TimeSpan duration = default)
        {
            return new CommandResult<TResult>(CommandStatus.Failure, default(TResult), message, exception, duration, false);
        }
        
        /// <summary>
        /// Creates a cancelled command result
        /// </summary>
        public static CommandResult<TResult> Cancelled(string message = null, TimeSpan duration = default)
        {
            return new CommandResult<TResult>(CommandStatus.Cancelled, default(TResult), message, null, duration, false);
        }
        
        /// <summary>
        /// Attempts to get the result value
        /// </summary>
        /// <param name="result">The result value if successful</param>
        /// <returns>True if the command was successful and result is available</returns>
        public bool TryGetResult(out TResult result)
        {
            result = Result;
            return IsSuccess;
        }
        
        /// <summary>
        /// Converts to a non-generic CommandResult
        /// </summary>
        public CommandResult ToCommandResult()
        {
            return new CommandResult(Status, Message, Exception, Duration, CanUndo);
        }
        
        public override string ToString()
        {
            return $"CommandResult[Status={Status}, Message={Message}, Duration={Duration.TotalMilliseconds}ms]";
        }
    }
    
    /// <summary>
    /// Non-generic command result for operations that don't return a value
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// Status of the command execution
        /// </summary>
        public CommandStatus Status { get; }
        
        /// <summary>
        /// Human-readable message about the command execution
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Exception that occurred during execution (if any)
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// Time taken to execute the command
        /// </summary>
        public TimeSpan Duration { get; }
        
        /// <summary>
        /// Indicates whether the command can be undone
        /// </summary>
        public bool CanUndo { get; }
        
        /// <summary>
        /// Indicates whether the command executed successfully
        /// </summary>
        public bool IsSuccess => Status == CommandStatus.Success;
        
        /// <summary>
        /// Indicates whether the command failed
        /// </summary>
        public bool IsFailure => Status == CommandStatus.Failure;
        
        /// <summary>
        /// Indicates whether the command was cancelled
        /// </summary>
        public bool IsCancelled => Status == CommandStatus.Cancelled;
        
        internal CommandResult(CommandStatus status, string message, Exception exception, TimeSpan duration, bool canUndo)
        {
            Status = status;
            Message = message ?? string.Empty;
            Exception = exception;
            Duration = duration;
            CanUndo = canUndo;
        }
        
        /// <summary>
        /// Creates a successful command result
        /// </summary>
        public static CommandResult Success(string message = null, TimeSpan duration = default, bool canUndo = true)
        {
            return new CommandResult(CommandStatus.Success, message, null, duration, canUndo);
        }
        
        /// <summary>
        /// Creates a failed command result
        /// </summary>
        public static CommandResult Failure(string message, Exception exception = null, TimeSpan duration = default)
        {
            return new CommandResult(CommandStatus.Failure, message, exception, duration, false);
        }
        
        /// <summary>
        /// Creates a cancelled command result
        /// </summary>
        public static CommandResult Cancelled(string message = null, TimeSpan duration = default)
        {
            return new CommandResult(CommandStatus.Cancelled, message, null, duration, false);
        }
        
        public override string ToString()
        {
            return $"CommandResult[Status={Status}, Message={Message}, Duration={Duration.TotalMilliseconds}ms]";
        }
    }
}