using Moq;
using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Commands;

[TestFixture]
public class TestLoadDataSchemeManifest
{
    internal static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestLoadDataSchemeManifest),
    };
    
    private const string SchemeName = "ManifestTestScheme";
    private const string AttrName = "Field";

    private DataScheme CreateScheme(string value)
    {
        var scheme = new DataScheme(SchemeName);
        scheme.AddAttribute(Context, AttrName, DataType.Text).AssertPassed();
        scheme.AddEntry(Context, new DataEntry { { AttrName, value, Context } });
        return scheme;
    }
    
    private IMock<IFileSystem> _mockFileSystem;

    [SetUp]
    public void Setup()
    {
        Schema.Reset();
        _mockFileSystem = new  Mock<IFileSystem>();
        Schema.SetStorage(new Storage(_mockFileSystem.Object));
        Schema.InitializeTemplateManifestScheme(Context);
    }

    [Test]
    public async Task UpdateManifestAsync_AddsNewEntry_WhenNotExisting()
    {
        var scheme = CreateScheme("Value1");
        const string filePath = "path/to/file1.json";
        var cmd = new LoadDataSchemeCommand(Context, scheme, overwriteExisting: true, importFilePath: filePath);

        var result = await cmd.ExecuteAsync(CancellationToken.None);
        Assert.IsTrue(result.IsSuccess, result.Message);

        // Verify manifest entry added
        var manifestEntryResult = Schema.GetManifestEntryForScheme(SchemeName);
        Assert.IsTrue(manifestEntryResult.Passed, manifestEntryResult.Message);
        Assert.That(manifestEntryResult.Result.FilePath, Is.EqualTo(filePath));
    }

    [Test]
    public async Task UpdateManifestAsync_UpdatesExistingEntry_FilePathChanged()
    {
        // Initial load
        var scheme1 = CreateScheme("Value1");
        const string filePath1 = "file_old.json";
        var cmd1 = new LoadDataSchemeCommand(Context, scheme1, overwriteExisting: true, importFilePath: filePath1);
        await cmd1.ExecuteAsync(CancellationToken.None);

        // Overwrite with new import path
        var scheme2 = CreateScheme("Value2");
        const string filePath2 = "file_new.json";
        var cmd2 = new LoadDataSchemeCommand(Context, scheme2, overwriteExisting: true, importFilePath: filePath2);
        await cmd2.ExecuteAsync(CancellationToken.None);

        var manifestEntryResult = Schema.GetManifestEntryForScheme(SchemeName);
        manifestEntryResult.AssertPassed();
        Assert.That(manifestEntryResult.Result.FilePath, Is.EqualTo(filePath2));
    }
} 