namespace Schema.Core.Tests;

[SetUpFixture]
public class TestFixtureSetup
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Logger.SetLogger(new TestLogger());
    }
}