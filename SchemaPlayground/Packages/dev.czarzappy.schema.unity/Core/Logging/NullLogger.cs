using Schema.Core.Logging;

namespace Schema.Core.Commands
{
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