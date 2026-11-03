using Moq;
using Schema.Core.IO;
using Schema.Core.Publish;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Publish;

/// <summary>
/// The project's publish switches: which of the built-in outputs Publish writes. They live in the project's content
/// folder beside the Manifest, so they travel with the project rather than with the machine.
/// </summary>
[TestFixture]
public class TestSchemaPublishSettings
{
    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestSchemaPublishSettings),
    };

    private string root = string.Empty;
    private string settingsPath = string.Empty;
    private readonly LocalFileSystem fileSystem = new();

    [SetUp]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "schema-publish-settings-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, Schema.DefaultContentDirectory));
        settingsPath = SchemaPublishSettings.PathFor(root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Test]
    public void Defaults_WriteBothBuiltInOutputs()
    {
        var defaults = SchemaPublishSettings.Defaults();

        Assert.That(defaults.PublishResourcesJson, Is.True);
        Assert.That(defaults.GenerateCSharp, Is.True);
        Assert.That(defaults.IsEveryBuiltInOutputOff, Is.False);
    }

    [Test]
    public void PathFor_IsBesideTheManifest_InTheProjectsContentFolder()
    {
        Assert.That(settingsPath, Is.EqualTo(Path.Combine(root, "Content", "PublishSettings.json")));
    }

    [Test]
    public void Load_GivesTheDefaults_WhenTheProjectHasNoSettingsFile()
    {
        var res = SchemaPublishSettings.Load(Context, fileSystem, settingsPath);

        res.AssertPassed();
        Assert.That(res.Result.PublishResourcesJson, Is.True);
        Assert.That(res.Result.GenerateCSharp, Is.True);
    }

    [Test]
    public void SaveThenLoad_KeepsTheSwitches()
    {
        var saved = new SchemaPublishSettings { PublishResourcesJson = false, GenerateCSharp = false };
        saved.Save(Context, fileSystem, settingsPath).AssertPassed();

        var res = SchemaPublishSettings.Load(Context, fileSystem, settingsPath);

        res.AssertPassed();
        Assert.That(res.Result.PublishResourcesJson, Is.False);
        Assert.That(res.Result.GenerateCSharp, Is.False);
        Assert.That(res.Result.IsEveryBuiltInOutputOff, Is.True);
    }

    [Test]
    public void Load_KeepsTheDefault_ForASwitchTheFileDoesNotMention()
    {
        File.WriteAllText(settingsPath, "{ \"GenerateCSharp\": false }");

        var res = SchemaPublishSettings.Load(Context, fileSystem, settingsPath);

        res.AssertPassed();
        Assert.That(res.Result.GenerateCSharp, Is.False);
        Assert.That(res.Result.PublishResourcesJson, Is.True, "a switch the file does not name keeps its default");
    }

    [Test]
    public void Load_FailsNamingThePath_WhenTheFileCannotBeParsed()
    {
        File.WriteAllText(settingsPath, "{ this is not json");

        var res = SchemaPublishSettings.Load(Context, fileSystem, settingsPath);

        res.AssertFailed();
        Assert.That(res.Message, Does.Contain(settingsPath), res.Message);
    }

    [Test]
    public void Load_FailsWhenTheFileIsThereButCannotBeRead()
    {
        var fs = new Mock<IFileSystem>();
        fs.Setup(f => f.FileExists(It.IsAny<SchemaContext>(), settingsPath)).Returns(SchemaResult.Pass());
        fs.Setup(f => f.ReadAllText(It.IsAny<SchemaContext>(), settingsPath))
            .Returns(SchemaResult<string>.Fail("the file is locked", Context));

        var res = SchemaPublishSettings.Load(Context, fs.Object, settingsPath);

        res.AssertFailed();
        Assert.That(res.Message, Does.Contain("locked"), res.Message);
    }

    [Test]
    public void Save_FailsWhenTheFileSystemCannotWrite()
    {
        var fs = new Mock<IFileSystem>();
        fs.Setup(f => f.WriteAllText(It.IsAny<SchemaContext>(), settingsPath, It.IsAny<string>()))
            .Returns(SchemaResult.Fail(Context, "the content folder is read-only"));

        var res = new SchemaPublishSettings().Save(Context, fs.Object, settingsPath);

        res.AssertFailed();
        Assert.That(res.Message, Does.Contain("read-only"), res.Message);
    }
}
