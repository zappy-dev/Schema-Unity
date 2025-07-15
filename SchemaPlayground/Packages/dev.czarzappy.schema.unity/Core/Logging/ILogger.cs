namespace Schema.Core.Logging
{
    public interface ILogger
    {
        Logger.LogLevel LogLevel { get; set; }
        void Log(Logger.LogLevel logLevel, string message);
    }
}