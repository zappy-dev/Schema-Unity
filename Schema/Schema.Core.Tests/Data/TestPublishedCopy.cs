using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

/// <summary>
/// The copy of a table that publishing writes out. Acceptance criterion AC5 of the conversion and load safety fixes
/// (docs/upstream-fixes.md): a row that cannot be added fails the publish, it is not dropped.
/// </summary>
[TestFixture]
public class TestPublishedCopy
{
    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestPublishedCopy),
    };

    [SetUp]
    public void Setup()
    {
        Schema.Reset();
        Schema.SetStorage(new Storage(new Mock<IFileSystem>().Object));
        Schema.InitializeTemplateManifestScheme(Context);
    }

    [TearDown]
    public void TearDown()
    {
        Schema.Reset();
    }

    [Test]
    public void AC5_CreatePublishedCopy_FailsNamingTheRow_WhenARowCannotBeAdded()
    {
        var items = ScratchSchemes.Table("Items", "Item_1", "Item_2", "Item_3");
        items.AddAttribute(Context, "Score", DataType.Integer).AssertPassed();
        items.GetEntry(1).SetData(Context, "Score", "not a number");

        var copy = items.CreatePublishedCopy(Context);

        copy.AssertFailed();
        Assert.That(copy.Message, Does.Contain("Items").And.Contain("Item_2"), copy.Message);
    }

    [Test]
    public void AC5_CreatePublishedCopy_FailsOnADanglingReference()
    {
        var zones = ScratchSchemes.Table("Zones", "Zone_A");
        Schema.LoadDataScheme(Context, zones, overwriteExisting: true, registerManifestEntry: false).AssertPassed();
        var items = ScratchSchemes.Table("Items", "Item_1", "Item_2");
        items.AddAttribute(Context, "Home", new ReferenceDataType("Zones", "ID") { DefaultValue = "" }).AssertPassed();
        items.GetEntry(0).SetData(Context, "Home", "Zone_A");
        items.GetEntry(1).SetData(Context, "Home", "Zone_Gone");

        var copy = items.CreatePublishedCopy(Context);

        copy.AssertFailed();
        Assert.That(copy.Message, Does.Contain("Items").And.Contain("Item_2"), copy.Message);
    }

    [Test]
    public void AC5_CreatePublishedCopy_KeepsEveryRow_AndOnlyThePublishedColumns()
    {
        var items = ScratchSchemes.Table("Items", "Item_1", "Item_2", "Item_3");
        items.AddAttribute(Context, "Score", DataType.Integer).AssertPassed();
        items.AddAttribute(Context, "Notes", DataType.Text, shouldPublish: false).AssertPassed();
        items.GetEntry(2).SetData(Context, "Score", 30);
        items.GetEntry(2).SetData(Context, "Notes", "draft");

        var copy = items.CreatePublishedCopy(Context).AssertPassed();

        Assert.That(copy.SchemeName, Is.EqualTo("Items"));
        Assert.That(copy.EntryCount, Is.EqualTo(3));
        Assert.That(copy.AllEntries.Select(e => e.GetDataAsString("ID")), Is.EqualTo(new[] { "Item_1", "Item_2", "Item_3" }));
        Assert.That(copy.GetEntry(2).GetDataDirect("Score"), Is.EqualTo(30));
        Assert.That(copy.GetAttributes().Select(a => a.AttributeName), Is.EqualTo(new[] { "ID", "Score" }));
        Assert.That(copy.GetEntry(2).GetDataDirect("Notes"), Is.Null);
    }

    [Test]
    public void AC5_CreatePublishedCopy_DoesNotChangeTheOriginal()
    {
        var items = ScratchSchemes.Table("Items", "Item_1");
        items.AddAttribute(Context, "Notes", DataType.Text, shouldPublish: false).AssertPassed();
        items.SetDirty(Context, false);

        items.CreatePublishedCopy(Context).AssertPassed();

        Assert.That(items.AttributeCount, Is.EqualTo(2));
        Assert.That(items.GetEntry(0).GetDataDirect("Notes"), Is.EqualTo(""));
        Assert.That(items.IsDirty, Is.False);
    }
}
