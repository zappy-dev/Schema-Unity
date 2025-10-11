#define SCHEMA_DEBUG
using System.Diagnostics;
using System.Text;

namespace Schema.Core.Logging
{
    public static class Logger
    {
        private static ILogger _logger;

        public enum LogLevel
        {
            VERBOSE,
            INFO,
            WARN,
            ERROR
        }

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public static LogLevel Level
        {
            get => _logger.LogLevel;
            set => _logger.LogLevel = value;
        }

        private static string FormatMessage(string message, LogLevel msgSeverity, object context = null)
        {
            var sb = new StringBuilder();
            sb.Append("[Schema] ");
            sb.Append(message);
            if (context != null)
            {
                sb.AppendLine();
                sb.AppendLine("Context:");
                sb.Append(context);
            }

            if (_logger.LogLevel == LogLevel.VERBOSE && msgSeverity >= LogLevel.WARN)
            {
#if !UNITY_64 && INCLUDE_STACK_TRACE // Don't log stack trace in environment that already logs out stack trace
                StackTrace stackTrace = new StackTrace(4);
                sb.Append(stackTrace);
#endif
            }
            
            return sb.ToString();
        }

        private static void Log(string message, LogLevel msgSeverity, object context = null)
        {
            _logger?.Log(msgSeverity, FormatMessage(message, msgSeverity, context));
        }
        
        [Conditional("SCHEMA_DEBUG")]
        public static void LogDbgVerbose(string message, object context = null)
        {
            Log(message, LogLevel.VERBOSE, context);
        }
        
        public static void LogVerbose(string message, object context = null)
        {
            Log(message, LogLevel.VERBOSE, context);
        }

        public static void Log(string message, object context = null)
        {
            Log(message, LogLevel.INFO, context);
        }

        public static void LogWarning(string message, object context = null)
        {
            Log(message, LogLevel.WARN, context);
        }

        [Conditional("SCHEMA_DEBUG")]
        public static void LogDbgWarning(string message, object context = null)
        {
            Log(message, LogLevel.WARN, context);
        }

        public static void LogError(string message, object context = null)
        {
            Log(message, LogLevel.ERROR, context);
        }

        [Conditional("SCHEMA_DEBUG")]
        public static void LogDbgError(string message, object context = null)
        {
            Log(message, LogLevel.ERROR, context);
        }
    }
}