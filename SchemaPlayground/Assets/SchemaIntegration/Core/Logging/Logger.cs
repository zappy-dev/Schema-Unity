namespace Schema.Core
{
    public static class Logger
    {
        private static ILogger _logger;

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        private static string FormatMessage(string message)
        {
            return $"[SCHEMA] {message}";
        }

        public static void Log(string message)
        {
            _logger.Log(FormatMessage(message));
        }

        public static void LogWarning(string message)
        {
            _logger.LogWarning(FormatMessage(message));
        }

        public static void LogError(string message)
        {
            _logger.LogError(FormatMessage(message));
        }
    }

    public interface ILogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}