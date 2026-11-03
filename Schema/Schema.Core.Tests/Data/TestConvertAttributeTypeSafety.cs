using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

/// <summary>
/// Converting a column's type must not change what a cell means, and must not half-finish.
/// Acceptance criteria AC1, AC3 and AC9 of the conversion and load safety fixes (docs/upstream-fixes.md).
/// </summary>
[TestFixture]
public class TestConvertAttributeTypeSafety
{
    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestConvertAttributeTypeSafety),
    };

    private DataScheme zones = null!;
    private DataScheme items = null!;

    [SetUp]
    public void Setup()
    {
        Schema.Reset();
        Schema.SetStorage(new Storage(new Mock<IFileSystem>().Object));
        Schema.InitializeTemplateManifestScheme(Context);

        // The table that a converted column will point at: two rows, the first of which is the one a wrong default would pick.
        zones = ScratchSchemes.Table("Zones", "Zone_A", "Zone_B");
        Schema.LoadDataScheme(Context, zones, overwriteExisting: true, registerManifestEntry: false).AssertPassed();

        items = ScratchSchemes.Table("Items", "Item_1", "Item_2", "Item_3", "Item_4", "Item_5");
    }

    [TearDown]
    public void TearDown()
    {
        Schema.Reset();
    }

    private static object? Cell(DataScheme scheme, int row, string column) => scheme.GetEntry(row).GetDataDirect(column);

    private static List<object?> Column(DataScheme scheme, string column) =>
        scheme.AllEntries.Select(e => e.GetDataDirect(column)).ToList();

    #region AC1: a blank cell is not the first row of the target

    [Test]
    public void AC1_BlankCellsConvertedToAReference_StayBlank_AndNeverBecomeTheFirstRow()
    {
        // A cell that is blank, whitespace only, null, or has no value at all. The type is built the way the editor's column menu builds it,
        // so its default is the first row of Zones.
        ScratchSchemes.AddTextColumn(items, "Home", "Zone_B", "", "   ", null, "Zone_A");
        var type = new ReferenceDataType("Zones", "ID");
        Assert.That(type.DefaultValue, Is.EqualTo("Zone_A"), "precondition: the constructor's default is the first row");

        var result = items.ConvertAttributeType(Context, "Home", type);

        result.AssertPassed();
        Assert.That(Column(items, "Home"), Is.EqualTo(new object?[] { "Zone_B", "", "", "", "Zone_A" }));
        Schema.IsValidScheme(Context, items).AssertPassed();
    }

    [Test]
    public void AC1_BlankEndOfASelfReferencingChain_StaysBlank()
    {
        // The pattern behind a "next level" or "next zone" column: the last row links to nothing.
        ScratchSchemes.AddTextColumn(zones, "Next", "Zone_B", "");
        var type = new ReferenceDataType("Zones", "ID");

        zones.ConvertAttributeType(Context, "Next", type).AssertPassed();

        Assert.That(Cell(zones, 0, "Next"), Is.EqualTo("Zone_B"));
        Assert.That(Cell(zones, 1, "Next"), Is.EqualTo(""), "the end of the chain must not become Zone_A");
        Schema.IsValidScheme(Context, zones).AssertPassed();
    }

    [Test]
    public void AC1_BlankCell_FailsNamingSchemeRowAndColumn_WhenEmptyReferencesAreNotAllowed()
    {
        ScratchSchemes.AddTextColumn(items, "Home", "Zone_B", "", "Zone_A", "Zone_A", "Zone_A");
        var type = new ReferenceDataType("Zones", "ID") { SupportsEmptyReferences = false };

        var result = items.ConvertAttributeType(Context, "Home", type);

        result.AssertFailed();
        Assert.That(result.Message, Does.Contain("Items").And.Contain("Home").And.Contain("Item_2"), result.Message);
        Assert.That(Column(items, "Home"), Is.EqualTo(new object?[] { "Zone_B", "", "Zone_A", "Zone_A", "Zone_A" }), "no cell is changed");
        Assert.That(items.GetAttribute("Home").Result.DataType, Is.InstanceOf<TextDataType>());
    }

    #endregion

    #region AC3: all or nothing, and a failure that says where

    [Test]
    public void AC3_FailedConversion_LeavesEveryCellTheColumnTypeAndTheDirtyStateAsBefore()
    {
        ScratchSchemes.AddTextColumn(items, "Score", "1", "2", "oops", "4", "5");
        var attribute = items.GetAttribute("Score").Result;
        var defaultBefore = attribute.DefaultValue;
        Assert.That(items.IsDirty, Is.False, "precondition: the scheme is clean");

        var result = items.ConvertAttributeType(Context, "Score", DataType.Integer);

        result.AssertFailed();
        Assert.That(Column(items, "Score"), Is.EqualTo(new object?[] { "1", "2", "oops", "4", "5" }),
            "the cells before the bad one must still be the text they were");
        Assert.That(attribute.DataType, Is.InstanceOf<TextDataType>());
        Assert.That(attribute.DefaultValue, Is.EqualTo(defaultBefore));
        Assert.That(items.IsDirty, Is.False, "nothing changed, so the scheme is not dirty");
        Schema.IsValidScheme(Context, items).AssertPassed();
    }

    [Test]
    public void AC3_FailedConversion_DoesNotClearAScheme_ThatWasAlreadyDirty()
    {
        ScratchSchemes.AddTextColumn(items, "Score", "1", "2", "oops", "4", "5");
        items.SetDirty(Context, true);

        items.ConvertAttributeType(Context, "Score", DataType.Integer).AssertFailed();

        Assert.That(items.IsDirty, Is.True);
    }

    [Test]
    public void AC3_SuccessfulConversion_ConvertsEveryCell_AndMarksTheSchemeDirty()
    {
        ScratchSchemes.AddTextColumn(items, "Score", "1", "2", "3", "4", "5");

        items.ConvertAttributeType(Context, "Score", DataType.Integer).AssertPassed();

        Assert.That(Column(items, "Score"), Is.EqualTo(new object?[] { 1, 2, 3, 4, 5 }));
        Assert.That(items.GetAttribute("Score").Result.DataType, Is.InstanceOf<IntegerDataType>());
        Assert.That(items.IsDirty, Is.True);
    }

    [Test]
    public void AC3_FailureMessage_NamesSchemeColumnRowAndValue_OfEveryBadCell()
    {
        ScratchSchemes.AddTextColumn(items, "Score", "1", "oops", "3", "bad", "5");

        var result = items.ConvertAttributeType(Context, "Score", DataType.Integer);

        result.AssertFailed();
        Assert.That(result.Message, Does.Contain("Items").And.Contain("Score"), result.Message);
        Assert.That(result.Message, Does.Contain("Item_2").And.Contain("oops"), result.Message);
        Assert.That(result.Message, Does.Contain("Item_4").And.Contain("bad"), "every bad cell is named, not only the first: " + result.Message);
        Assert.That(result.Message, Does.Not.Contain("Item_3"), "a cell that converts is not named: " + result.Message);
    }

    [Test]
    public void AC3_DanglingReference_FailsNamingTheRowAndTheValue()
    {
        ScratchSchemes.AddTextColumn(items, "Home", "Zone_A", "Zone_Typo", "Zone_B", "", "Zone_A");

        var result = items.ConvertAttributeType(Context, "Home", new ReferenceDataType("Zones", "ID"));

        result.AssertFailed();
        Assert.That(result.Message, Does.Contain("Items").And.Contain("Home").And.Contain("Item_2").And.Contain("Zone_Typo"), result.Message);
        Assert.That(Column(items, "Home"), Is.EqualTo(new object?[] { "Zone_A", "Zone_Typo", "Zone_B", "", "Zone_A" }));
        Assert.That(items.GetAttribute("Home").Result.DataType, Is.InstanceOf<TextDataType>());
    }

    [Test]
    public void AC3_FailureMessage_NamesTheRowNumber_WhenTheSchemeHasNoIdentifier()
    {
        var plain = new DataScheme("Plain");
        plain.AddAttribute(Context, "Score", DataType.Text).AssertPassed();
        foreach (var text in new[] { "1", "oops", "3" })
        {
            plain.AddEntry(Context, new DataEntry { { "Score", text, Context } }).AssertPassed();
        }

        var result = plain.ConvertAttributeType(Context, "Score", DataType.Integer);

        result.AssertFailed();
        Assert.That(result.Message, Does.Contain("Plain").And.Contain("Score").And.Contain("oops"), result.Message);
        Assert.That(result.Message, Does.Contain("row 2"), "a row with no identifier is named by its position, counting from 1: " + result.Message);
    }

    #endregion

    #region AC9: converting a column that is not there

    [Test]
    public void AC9_ConvertingAnUnknownColumn_ReturnsAFailedResult_InsteadOfThrowing()
    {
        SchemaResult result = default;

        Assert.DoesNotThrow(() => result = items.ConvertAttributeType(Context, "NoSuchColumn", DataType.Integer));

        result.AssertFailed();
        Assert.That(result.Message, Does.Contain("NoSuchColumn").And.Contain("Items"), result.Message);
    }

    [Test]
    public void AC9_ConvertingToANullType_ReturnsAFailedResult_InsteadOfThrowing()
    {
        ScratchSchemes.AddTextColumn(items, "Score", "1", "2", "3", "4", "5");
        SchemaResult result = default;

        Assert.DoesNotThrow(() => result = items.ConvertAttributeType(Context, "Score", null!));

        result.AssertFailed();
        Assert.That(items.GetAttribute("Score").Result.DataType, Is.InstanceOf<TextDataType>());
    }

    #endregion
}
