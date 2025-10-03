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
    
    public readonly struct Unit
    {
        public static readonly Unit Value = new Unit();
        public override string ToString() => "()";
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
        
        public Type ResultType { get; }
        
        #pragma warning disable CS8632 // Disable warning around object? type
        public object? Value { get; }
        
        internal CommandResult(Type resultType, object? value, CommandStatus status, string message, Exception exception, TimeSpan duration, bool canUndo)
        {
            ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
            Value = value;
            Status = status;
            Message = message ?? string.Empty;
            Exception = exception;
            Duration = duration;
            CanUndo = canUndo;
        }
        #pragma warning restore CS8632
        
        public bool TryGet<T>(out T value)
        {
            if (Value is T t) { value = t; return true; }
            value = default!;
            return false;
        }
        
        /// <summary>
        /// Creates a successful command result
        /// </summary>
        public static CommandResult Pass<T>(T result, string message = null, TimeSpan duration = default, bool canUndo = true)
        {
            return new CommandResult(typeof(T), result, CommandStatus.Success, message, null, duration, canUndo);
        }
        
        /// <summary>
        /// Creates a failed command result
        /// </summary>
        public static CommandResult Fail(string message, Exception exception = null, TimeSpan duration = default)
        {
            return new CommandResult(typeof(Unit), Unit.Value, CommandStatus.Failure, message, exception, duration, false);
        }
        
        /// <summary>
        /// Creates a cancelled command result
        /// </summary>
        public static CommandResult Cancel(string message = null, TimeSpan duration = default)
        {
            return new CommandResult(typeof(Unit), Unit.Value, CommandStatus.Cancelled, message, null, duration, false);
        }
        
        public override string ToString()
        {
            return $"CommandResult[Status={Status}, Message={Message}, Duration={Duration.TotalMilliseconds}ms]";
        }
    }
}