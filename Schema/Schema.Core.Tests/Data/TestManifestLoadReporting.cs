using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

/// <summary>
/// What loading a manifest reports about the tables it could not load, or loaded with a bad value in them.
/// Acceptance criteria AC6 and AC8 of the conversion and load safety fixes (docs/upstream-fixes.md).
/// </summary>
[TestFixture]
public class TestManifestLoadReporting
{
    private static readonly SchemaContext Context = ScratchProject.Context;

    /// <summary>Adds a Reference column to a table. Existing rows get a blank link.</summary>
    private static void AddLink(DataScheme scheme, string column, string toScheme)
    {
        var type = new ReferenceDataType(toScheme, "ID", validateSchemeLoaded: false) { DefaultValue = "" };
        scheme.AddAttribute(Context, column, type).AssertPassed();
    }

    private static bool IsFullyLoaded(SchemaResult<Schema.ManifestLoadStatus> load) =>
        load.Passed && load.Result == Schema.ManifestLoadStatus.FULLY_LOADED;

    #region AC6: tables that reference each other in a cycle

    [Test]
    public void AC6_TopologicalSort_FailsNamingTheSchemesOfACycle()
    {
        var items = ScratchSchemes.Table("Items", "Item_1");
        var zones = ScratchSchemes.Table("Zones", "Zone_A");
        var extras = ScratchSchemes.Table("Extras", "Extra_1");
        AddLink(items, "Zone", "Zones");
        AddLink(zones, "Item", "Items");

        var sorted = DataScheme.TopologicalSortByReferences(Context, new[] { items, zones, extras });

        sorted.AssertFailed();
        Assert.That(sorted.Message, Does.Contain("Items").And.Contain("Zones"), sorted.Message);
        Assert.That(sorted.Message, Does.Not.Contain("Extras"), "a table that is not part of the cycle is not blamed: " + sorted.Message);
    }

    [Test]
    public void AC6_TopologicalSort_StillSortsSelfReferencesAndChains()
    {
        var zones = ScratchSchemes.Table("Zones", "Zone_A");
        var items = ScratchSchemes.Table("Items", "Item_1");
        var bags = ScratchSchemes.Table("Bags", "Bag_1");
        AddLink(zones, "Next", "Zones"); // a table that links to itself is not a cycle between tables
        AddLink(items, "Zone", "Zones");
        AddLink(bags, "Item", "Items");

        var sorted = DataScheme.TopologicalSortByReferences(Context, new[] { bags, items, zones }).AssertPassed();

        Assert.That(sorted.Select(s => s.SchemeName).ToList(), Is.EqualTo(new[] { "Zones", "Items", "Bags" }));
    }

    [Test]
    public void AC6_LoadManifest_ReportsTheTablesItCouldNotLoadByName()
    {
        using var project = new ScratchProject();
        var items = project.Add(ScratchSchemes.Table("Items", "Item_1"));
        var zones = project.Add(ScratchSchemes.Table("Zones", "Zone_A"));
        project.Add(ScratchSchemes.Table("Extras", "Extra_1"));
        AddLink(items, "Zone", "Zones");
        AddLink(zones, "Item", "Items");
        project.Save();

        var load = project.Load();

        Assert.That(IsFullyLoaded(load), Is.False, "two tables are missing, so the load is not complete: " + load.Message);
        Assert.That(load.Message, Does.Contain("Items").And.Contain("Zones"), "the message names the tables it dropped: " + load.Message);
        Assert.That(load.Message, Does.Not.Contain("Loaded 3 schemes"), load.Message);
    }

    [Test]
    public void AC6_LoadManifest_StillLoadsTheTablesOutsideTheCycle()
    {
        using var project = new ScratchProject();
        var items = project.Add(ScratchSchemes.Table("Items", "Item_1"));
        var zones = project.Add(ScratchSchemes.Table("Zones", "Zone_A"));
        project.Add(ScratchSchemes.Table("Extras", "Extra_1"));
        AddLink(items, "Zone", "Zones");
        AddLink(zones, "Item", "Items");
        project.Save();

        project.Load();

        Assert.That(Schema.LoadedSchemes.ContainsKey("Extras"), Is.True);
        Assert.That(Schema.LoadedSchemes.ContainsKey("Items"), Is.False);
        Assert.That(Schema.LoadedSchemes.ContainsKey("Zones"), Is.False);
    }

    [Test]
    public void AC6_LoadManifest_LoadsSelfReferencingTablesFully()
    {
        using var project = new ScratchProject();
        var zones = project.Add(ScratchSchemes.Table("Zones", "Zone_A", "Zone_B"));
        var items = project.Add(ScratchSchemes.Table("Items", "Item_1"));
        AddLink(zones, "Next", "Zones");
        zones.GetEntry(0).SetData(Context, "Next", "Zone_B");
        AddLink(items, "Zone", "Zones");
        project.Save();

        var load = project.Load();

        load.AssertPassed();
        Assert.That(IsFullyLoaded(load), Is.True, load.Message);
        Assert.That(Schema.LoadedSchemes.Keys, Is.SupersetOf(new[] { "Zones", "Items" }));
    }

    #endregion

    #region AC8: a bad value met at load

    [Test]
    public void AC8_LoadManifest_WithADanglingReference_IsNotFullyLoaded_AndNamesTheRow()
    {
        using var project = new ScratchProject();
        var zones = project.Add(ScratchSchemes.Table("Zones", "Zone_A", "Zone_B"));
        var items = project.Add(ScratchSchemes.Table("Items", "Item_1", "Item_2", "Item_3"));
        AddLink(items, "Home", "Zones");
        items.GetEntry(0).SetData(Context, "Home", "Zone_A");
        items.GetEntry(1).SetData(Context, "Home", "Zone_Typo");
        project.Save();

        var load = project.Load();

        Assert.That(IsFullyLoaded(load), Is.False, "a dangling reference is not a complete load: " + load.Message);
        Assert.That(load.Message, Does.Contain("Items").And.Contain("Item_2").And.Contain("Home").And.Contain("Zone_Typo"), load.Message);
        Assert.That(load.Message, Does.Not.Contain("Item_1"), "the rows that are fine are not named: " + load.Message);
    }

    [Test]
    public void AC8_LoadManifest_WithAValueOfTheWrongType_IsNotFullyLoaded_AndNamesTheRow()
    {
        using var project = new ScratchProject();
        var items = project.Add(ScratchSchemes.Table("Items", "Item_1", "Item_2"));
        items.AddAttribute(Context, "Score", DataType.Integer).AssertPassed();
        items.GetEntry(0).SetData(Context, "Score", 7);
        items.GetEntry(1).SetData(Context, "Score", "seven");
        project.Save();

        var load = project.Load();

        Assert.That(IsFullyLoaded(load), Is.False, load.Message);
        Assert.That(load.Message, Does.Contain("Items").And.Contain("Item_2").And.Contain("Score").And.Contain("seven"), load.Message);
    }

    [Test]
    public void AC8_ATableWithABadValueIsStillLoaded_SoThatItCanBeFixedInTheEditor()
    {
        using var project = new ScratchProject();
        project.Add(ScratchSchemes.Table("Zones", "Zone_A"));
        var items = project.Add(ScratchSchemes.Table("Items", "Item_1"));
        AddLink(items, "Home", "Zones");
        items.GetEntry(0).SetData(Context, "Home", "Zone_Typo");
        project.Save();

        project.Load();

        Assert.That(Schema.GetScheme(Context, "Items").Try(out var loaded), Is.True, "the table must not vanish");
        Assert.That(loaded.GetEntry(0).GetDataDirect("Home"), Is.EqualTo("Zone_Typo"), "and the bad value is kept, not replaced");
    }

    [Test]
    public void AC8_LoadManifest_WithOnlyValidValues_IsFullyLoaded_EvenWithBlankLinks()
    {
        using var project = new ScratchProject();
        project.Add(ScratchSchemes.Table("Zones", "Zone_A", "Zone_B"));
        var items = project.Add(ScratchSchemes.Table("Items", "Item_1", "Item_2"));
        AddLink(items, "Home", "Zones");
        items.GetEntry(0).SetData(Context, "Home", "Zone_B");
        // Item_2 keeps a blank link, which the type allows
        project.Save();

        var load = project.Load();

        load.AssertPassed();
        Assert.That(IsFullyLoaded(load), Is.True, load.Message);
    }

    #endregion
}
