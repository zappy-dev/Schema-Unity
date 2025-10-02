using Schema.Core.Logging;

namespace Schema.Core.Tests;

[SetUpFixture]
public class TestFixtureSetup
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Logger.SetLogger(new TestLogger());
        Logger.Level = Logger.LogLevel.ERROR;

        SchemaResultSettings.Instance.LogStackTrace = true;
    }
}