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
    }
    
    internal static readonly SchemaContext SchemaTestContext = new()
    {
        Driver = "UnitTests",
    };
}