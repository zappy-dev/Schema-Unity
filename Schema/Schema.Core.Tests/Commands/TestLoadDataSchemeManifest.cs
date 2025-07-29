using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Commands;

[TestFixture]
public class TestLoadDataSchemeManifest
{
    private const string SchemeName = "ManifestTestScheme";
    private const string AttrName = "Field";

    private DataScheme CreateScheme(string value)
    {
        var scheme = new DataScheme(SchemeName);
        scheme.AddAttribute(new AttributeDefinition(AttrName, DataType.Text)).AssertPassed();
        scheme.AddEntry(new DataEntry { { AttrName, value } });
        return scheme;
    }

    [SetUp]
    public void Setup()
    {
        Schema.Reset();
    }

    [Test]
    public async Task UpdateManifestAsync_AddsNewEntry_WhenNotExisting()
    {
        var scheme = CreateScheme("Value1");
        const string filePath = "path/to/file1.json";
        var cmd = new LoadDataSchemeCommand(scheme, overwriteExisting: true, importFilePath: filePath);

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
        var cmd1 = new LoadDataSchemeCommand(scheme1, overwriteExisting: true, importFilePath: filePath1);
        await cmd1.ExecuteAsync(CancellationToken.None);

        // Overwrite with new import path
        var scheme2 = CreateScheme("Value2");
        const string filePath2 = "file_new.json";
        var cmd2 = new LoadDataSchemeCommand(scheme2, overwriteExisting: true, importFilePath: filePath2);
        await cmd2.ExecuteAsync(CancellationToken.None);

        var manifestEntryResult = Schema.GetManifestEntryForScheme(SchemeName);
        Assert.IsTrue(manifestEntryResult.Passed, manifestEntryResult.Message);
        Assert.That(manifestEntryResult.Result.FilePath, Is.EqualTo(filePath2));
    }
} 