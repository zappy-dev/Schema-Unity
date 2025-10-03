using Schema.Core.IO;
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

        SchemaResultSettings.Instance.LogFailure = true;
        SchemaResultSettings.Instance.LogStackTrace = true;
        // Establish a deterministic project root for path resolution in tests
        if (PathUtility.IsWindowsSystem)
        {
            Schema.ProjectPath = "C:\\Users\\TestUser\\src\\TestProject";
        } else if (PathUtility.IsUnixSystem)
        {
            Schema.ProjectPath = "/usr/local/bin";
        }
    }
}