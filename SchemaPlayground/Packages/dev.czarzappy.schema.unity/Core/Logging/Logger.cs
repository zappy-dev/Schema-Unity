using System;
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

        private static string FormatMessage(string message, object context = null)
        {
            var sb = new StringBuilder();
            sb.Append("[Schema] ");
            if (context != null)
            {
                sb.Append("[Context=").Append(context).Append("] ");
            }

            sb.Append(message);
            
            return sb.ToString();
        }
        
        public static void LogVerbose(string message, object context = null)
        {
            _logger.Log(LogLevel.VERBOSE, FormatMessage(message, context));
        }

        public static void Log(string message, object context = null)
        {
            _logger.Log(LogLevel.INFO, FormatMessage(message, context));
        }

        public static void LogWarning(string message, object context = null)
        {
            _logger.Log(LogLevel.WARNING, FormatMessage(message, context));
        }

        public static void LogError(string message, object context = null)
        {
            _logger.Log(LogLevel.ERROR, FormatMessage(message, context));
        }
    }

    public interface ILogger
    {
        Logger.LogLevel LogLevel { get; set; }
        void Log(Logger.LogLevel logLevel, string message);
    }
}