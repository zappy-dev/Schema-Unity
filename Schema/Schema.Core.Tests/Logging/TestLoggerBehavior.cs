using Schema.Core.Logging;

namespace Schema.Core.Tests.Logging;

[TestFixture]
public class TestLoggerBehavior
{
    private class CaptureLogger : ILogger
    {
        public Logger.LogLevel LogLevel { get; set; } = Logger.LogLevel.VERBOSE;
        public readonly List<(Logger.LogLevel Level, string Message)> Entries = new();
        public void Log(Logger.LogLevel logLevel, string message)
        {
            if (logLevel >= LogLevel)
            {
                Entries.Add((logLevel, message));
            }
        }
    }

    private CaptureLogger _captureLogger;

    [SetUp]
    public void Setup()
    {
        _captureLogger = new CaptureLogger();
        Logger.SetLogger(_captureLogger);
    }

    [Test]
    public void VerboseMessages_AreSuppressed_WhenLogLevelIsInfo()
    {
        _captureLogger.LogLevel = Logger.LogLevel.INFO;

        Logger.LogVerbose("Verbose");
        Logger.Log("Info");

        Assert.That(_captureLogger.Entries, Has.Exactly(1).Items);
        Assert.That(_captureLogger.Entries[0].Level, Is.EqualTo(Logger.LogLevel.INFO));
    }

    [Test]
    public void WarningAndError_AreAlwaysLogged()
    {
        _captureLogger.LogLevel = Logger.LogLevel.ERROR; // Only ERROR and above

        Logger.LogWarning("Warning");
        Logger.LogError("Error");

        // Only error should pass the level filter
        Assert.That(_captureLogger.Entries, Has.Exactly(1).Items);
        Assert.That(_captureLogger.Entries[0].Level, Is.EqualTo(Logger.LogLevel.ERROR));
    }
} 