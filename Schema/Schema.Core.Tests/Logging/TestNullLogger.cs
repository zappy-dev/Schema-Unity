using Schema.Core.Logging;

namespace Schema.Core.Tests.Logging;

[TestFixture]
public class TestNullLogger
{
    [Test]
    public void NullLogger_ShouldIgnoreAllMessages()
    {
        // Use reflection to create internal NullLogger
        var nullLoggerType = typeof(Logger).Assembly.GetType("Schema.Core.Commands.NullLogger", throwOnError: true);
        var logger = (ILogger)Activator.CreateInstance(nullLoggerType);

        // Should not throw when logging any level
        Assert.DoesNotThrow(() =>
        {
            logger.Log(Logger.LogLevel.INFO, "Info message");
            logger.Log(Logger.LogLevel.ERROR, "Error message");
        });

        // Ensure level property is mutable and does not throw
        Assert.DoesNotThrow(() => logger.LogLevel = Logger.LogLevel.WARNING);
    }
} 