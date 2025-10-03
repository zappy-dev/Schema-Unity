using System;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Unique identifier for a command instance
    /// </summary>
    public readonly struct CommandId : IEquatable<CommandId>
    {
        private readonly Guid _value;
        
        private CommandId(Guid value)
        {
            _value = value;
        }
        
        /// <summary>
        /// Creates a new unique command identifier
        /// </summary>
        public static CommandId NewId() => new CommandId(Guid.NewGuid());
        
        /// <summary>
        /// Creates a command identifier from a GUID
        /// </summary>
        public static CommandId FromGuid(Guid guid) => new CommandId(guid);
        
        /// <summary>
        /// Gets the underlying GUID value
        /// </summary>
        public Guid ToGuid() => _value;
        
        public bool Equals(CommandId other)
        {
            return _value.Equals(other._value);
        }
        
        public override bool Equals(object obj)
        {
            return obj is CommandId other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
        
        public override string ToString()
        {
            return _value.ToString("N").Substring(0, 8); // Short 8-character representation
        }
        
        public string ToFullString()
        {
            return _value.ToString();
        }
        
        public static bool operator ==(CommandId left, CommandId right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(CommandId left, CommandId right)
        {
            return !left.Equals(right);
        }
        
        public static implicit operator Guid(CommandId commandId)
        {
            return commandId._value;
        }
        
        public static implicit operator CommandId(Guid guid)
        {
            return new CommandId(guid);
        }
    }
}