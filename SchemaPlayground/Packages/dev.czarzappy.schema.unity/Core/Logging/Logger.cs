using System;
using System.Diagnostics;
using System.Text;

namespace Schema.Core
{
    public static class Logger
    {
        private static ILogger _logger;

        public enum LogLevel
        {
            VERBOSE,
            INFO,
            WARNING,
            ERROR
        }

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        private static string FormatMessage(string message, LogLevel msgSeverity, object context = null)
        {
            var sb = new StringBuilder();
            sb.Append("[Schema] ");
            if (context != null)
            {
                sb.Append("[Context=").Append(context).Append("] ");
            }

            sb.AppendLine(message);

            if (_logger.LogLevel == LogLevel.VERBOSE && msgSeverity >= LogLevel.WARNING)
            {
#if !UNITY_64 // Don't log stack trace in environment that already logs out stack trace
                StackTrace stackTrace = new StackTrace(4);


                sb.AppendLine(stackTrace.ToString());
#endif
            }
            
            return sb.ToString();
        }

        private static void Log(string message, LogLevel msgSeverity, object context = null)
        {
            _logger?.Log(msgSeverity, FormatMessage(message, msgSeverity, context));
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
            Log(message, LogLevel.WARNING, context);
        }

        public static void LogError(string message, object context = null)
        {
            Log(message, LogLevel.ERROR, context);
        }
    }

    public interface ILogger
    {
        Logger.LogLevel LogLevel { get; set; }
        void Log(Logger.LogLevel logLevel, string message);
    }
}