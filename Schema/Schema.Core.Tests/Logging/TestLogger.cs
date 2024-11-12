namespace Schema.Core.Tests;

public class TestLogger : ILogger
{
    public Logger.LogLevel LogLevel { get; set; } = Logger.LogLevel.VERBOSE;
    public void Log(Logger.LogLevel logLevel, string message)
    {
        if (logLevel >= LogLevel)
        {
            TestContext.WriteLine($"[{logLevel}] {message}");
        }
    }
}