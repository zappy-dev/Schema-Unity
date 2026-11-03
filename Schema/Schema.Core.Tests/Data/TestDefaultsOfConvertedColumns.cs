using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

/// <summary>
/// The default value of a column: left alone when a conversion is run again (finding 17), and valid for the column when it
/// fills a cell (finding 8). See docs/upstream-fixes.md.
/// </summary>
[TestFixture]
public class TestDefaultsOfConvertedColumns
{
    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestDefaultsOfConvertedColumns),
    };

    /// <summary>
    /// A type like Color: its values are integers, but its default is kept as text, so the default alone is not a valid value.
    /// </summary>
    private sealed class TextDefaultIntegerType : DataType
    {
        public TextDefaultIntegerType() : base("7")
        {
        }

        public override string TypeName => "TextDefaultInteger";
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) =>
            SchemaResult<string>.Pass($"DataEntry.GetDataAsInt(\"{attribute.AttributeName}\")");
        public override string CSDataType => typeof(int).ToString();
        public override object Clone() => new TextDefaultIntegerType { DefaultValue = DefaultValue };
        public override SchemaResult IsValidValue(SchemaContext context, object value) =>
            CheckIf(value is int, "Value is not an integer", "Value is an integer", context);
        public override SchemaResult<object> ConvertValue(SchemaContext context, object value) =>
            int.TryParse(value?.ToString(), out var number)
                ? Pass<object>(number, "Converted", context)
                : Fail<object>($"'{value}' is not an integer", context);
    }

    private DataScheme zones = null!;
    private DataScheme items = null!;

    [SetUp]
    public void Setup()
    {
        Schema.Reset();
        Schema.SetStorage(new Storage(new Mock<IFileSystem>().Object));
        Schema.InitializeTemplateManifestScheme(Context);
        zones = ScratchSchemes.Table("Zones", "Zone_A", "Zone_B");
        Schema.LoadDataScheme(Context, zones, overwriteExisting: true, registerManifestEntry: false).AssertPassed();
        items = ScratchSchemes.Table("Items", "Item_1", "Item_2");
    }

    [TearDown]
    public void TearDown()
    {
        Schema.Reset();
    }

    #region 17: running a conversion again

    [Test]
    public void B17_ConvertingToTheTypeTheColumnAlreadyHas_KeepsTheDefaultSetInTheColumnSettings()
    {
        ScratchSchemes.AddTextColumn(items, "Home", "Zone_A", "");
        items.ConvertAttributeType(Context, "Home", new ReferenceDataType("Zones", "ID")).AssertPassed();
        var attribute = items.GetAttribute("Home").Result;
        attribute.DefaultValue = "Zone_B"; // set in the column settings

        items.ConvertAttributeType(Context, "Home", new ReferenceDataType("Zones", "ID")).AssertPassed();

        Assert.That(attribute.DefaultValue, Is.EqualTo("Zone_B"));
    }

    [Test]
    public void B17_ConvertingToAnotherType_StillSetsTheDefaultOfTheNewType()
    {
        ScratchSchemes.AddTextColumn(items, "Score", "1", "2");
        var attribute = items.GetAttribute("Score").Result;
        attribute.DefaultValue = "five";

        items.ConvertAttributeType(Context, "Score", DataType.Integer).AssertPassed();

        Assert.That(attribute.DefaultValue, Is.EqualTo(0));
    }

    #endregion

    #region 8: a default that is valid for the column

    [Test]
    public void B8_ANewRow_GetsADefaultThatIsValidForTheColumn()
    {
        items.AddAttribute(Context, "Number", new TextDefaultIntegerType()).AssertPassed();

        var added = items.CreateNewEmptyEntry(Context).AssertPassed();

        Assert.That(added.GetDataDirect("Number"), Is.EqualTo(7), "the default, converted: not the text it is kept as");
        Schema.IsValidScheme(Context, items).AssertPassed();
    }

    [Test]
    public void B8_AColumnAddedToATableWithRows_FillsTheExistingRowsWithAValidDefault()
    {
        items.AddAttribute(Context, "Number", new TextDefaultIntegerType()).AssertPassed();

        Assert.That(items.AllEntries.Select(e => e.GetDataDirect("Number")), Is.All.EqualTo(7));
        Schema.IsValidScheme(Context, items).AssertPassed();
    }

    [Test]
    public void B8_AddEntryFillsAMissingValueWithAValidDefault()
    {
        items.AddAttribute(Context, "Number", new TextDefaultIntegerType()).AssertPassed();
        var entry = new DataEntry { { "ID", "Item_3", Context } };

        items.AddEntry(Context, entry).AssertPassed();

        Assert.That(entry.GetDataDirect("Number"), Is.EqualTo(7));
    }

    [Test]
    public void B8_ADefaultThatIsAlreadyValid_IsUsedAsItIs()
    {
        items.AddAttribute(Context, "Score", DataType.Integer).AssertPassed();

        var added = items.CreateNewEmptyEntry(Context).AssertPassed();

        Assert.That(added.GetDataDirect("Score"), Is.EqualTo(0));
    }

    #endregion
}
