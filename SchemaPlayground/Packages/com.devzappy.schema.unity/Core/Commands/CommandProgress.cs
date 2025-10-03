using System;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Progress information for a command execution
    /// </summary>
    public class CommandProgress
    {
        /// <summary>
        /// Progress value between 0.0 and 1.0
        /// </summary>
        public float Value { get; }
        
        /// <summary>
        /// Human-readable progress message
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// ID of the command reporting progress
        /// </summary>
        public CommandId CommandId { get; }
        
        /// <summary>
        /// Description of the command reporting progress
        /// </summary>
        public string CommandDescription { get; }
        
        public CommandProgress(float value, string message, CommandId commandId, string commandDescription)
        {
            Value = Math.Max(0f, Math.Min(1f, value)); // Clamp between 0 and 1
            Message = message ?? string.Empty;
            CommandId = commandId;
            CommandDescription = commandDescription ?? string.Empty;
        }
        
        public override string ToString()
        {
            return $"{CommandDescription}: {Message} ({Value:P0})";
        }
    }
}