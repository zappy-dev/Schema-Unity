using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Schemes;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Serialization;

/// <summary>
/// The wrapper that Schema generates for a table with a Reference column: compiled for real, and then used.
/// Acceptance criterion AC7 of the conversion and load safety fixes (docs/upstream-fixes.md): next to the getter that throws on a
/// blank or dangling reference there is one that does not.
/// </summary>
[TestFixture]
public class TestGeneratedReferenceAccessors
{
    private static readonly SchemaContext Context = new()
    {
        Driver = nameof(TestGeneratedReferenceAccessors),
    };

    private Assembly generated = null!;
    private Type zonesEntry = null!;
    private Type itemsEntry = null!;

    [OneTimeSetUp]
    public void GenerateAndCompile()
    {
        Schema.Reset();
        var mockFileSystem = new Mock<IFileSystem>();
        Schema.SetStorage(new Storage(mockFileSystem.Object));
        Schema.InitializeTemplateManifestScheme(Context);

        var zones = ScratchSchemes.Table("Zones", "Zone_A", "Zone_B");
        zones.AddAttribute(Context, "Next", new ReferenceDataType("Zones", "ID", validateSchemeLoaded: false) { DefaultValue = "" }).AssertPassed();
        var items = ScratchSchemes.Table("Items", "Item_1");
        items.AddAttribute(Context, "Home", new ReferenceDataType("Zones", "ID", validateSchemeLoaded: false) { DefaultValue = "" }).AssertPassed();

        // A table whose identifier is an integer, which the throwing getter reads with GetDataAsInt and the OrNull accessor reads as the raw cell
        var levels = new DataScheme("Levels");
        levels.AddAttribute(Context, "ID", DataType.Integer, isIdentifier: true).AssertPassed();
        foreach (var id in new[] { 1, 2 })
        {
            levels.AddEntry(Context, new DataEntry { { "ID", id, Context } }).AssertPassed();
        }
        items.AddAttribute(Context, "Level", new ReferenceDataType("Levels", "ID", validateSchemeLoaded: false) { DefaultValue = "" }).AssertPassed();

        Schema.LoadDataScheme(Context, zones, overwriteExisting: true, registerManifestEntry: false).AssertPassed();
        Schema.LoadDataScheme(Context, levels, overwriteExisting: true, registerManifestEntry: false).AssertPassed();
        Schema.LoadDataScheme(Context, items, overwriteExisting: true, registerManifestEntry: false).AssertPassed();

        var sources = new[] { zones, levels, items }.Select(Generate).ToList();
        generated = Compile(sources);
        zonesEntry = generated.GetType("ZonesEntry")!;
        itemsEntry = generated.GetType("ItemsEntry")!;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Schema.Reset();
    }

    private static string Generate(DataScheme scheme)
    {
        var manifest = Schema.GetManifestScheme(Context).Result;
        var manifestEntry = new DataEntry();
        manifestEntry.SetData(Context, nameof(ManifestEntry.SchemeName), scheme.SchemeName);
        manifestEntry.SetData(Context, nameof(ManifestEntry.CSharpNamespace), "");
        manifestEntry.SetData(Context, nameof(ManifestEntry.CSharpGenerateIds), false);

        var format = new CSharpSchemeStorageFormat(new Mock<IFileSystem>().Object)
        {
            ManifestEntry = new ManifestEntry(manifest._, manifestEntry),
        };
        return format.Serialize(Context, scheme).AssertPassed();
    }

    /// <summary>
    /// Compiles generated wrappers against the Schema.Core that is under test. The wrappers import the namespace Schema.Runtime, which
    /// belongs to the Unity-only assembly, so an empty namespace of that name stands in for it: these wrappers use nothing from it.
    /// </summary>
    private static Assembly Compile(IEnumerable<string> sources)
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = trusted.Select(path => MetadataReference.CreateFromFile(path)).ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(DataScheme).Assembly.Location));

        // C# 8 is what Unity 2020.3, the oldest Unity this package supports, compiles with.
        var options = new CSharpParseOptions(LanguageVersion.CSharp8);
        var trees = sources.Append("namespace Schema.Runtime { }")
            .Select(code => CSharpSyntaxTree.ParseText(code, options))
            .ToList();
        var compilation = CSharpCompilation.Create("GeneratedWrappers_" + Guid.NewGuid().ToString("N"), trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        var errors = emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();
        Assert.That(emitted.Success, Is.True, "the generated wrappers must compile:\n" + string.Join("\n", errors));

        stream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }

    private static object Wrap(Type entryType, DataScheme scheme, int row) =>
        Activator.CreateInstance(entryType, scheme, scheme.GetEntry(row))!;

    private static object? Read(object entry, string property)
    {
        try
        {
            return entry.GetType().GetProperty(property)?.GetValue(entry);
        }
        catch (TargetInvocationException e)
        {
            throw e.InnerException!;
        }
    }

    private static DataScheme Loaded(string name) => Schema.GetScheme(Context, name).Result;

    private void SetLink(string scheme, int row, string column, string value) =>
        Loaded(scheme).GetEntry(row).SetData(Context, column, value);

    [Test]
    public void AC7_TheGeneratedWrappersCompile()
    {
        Assert.That(zonesEntry, Is.Not.Null);
        Assert.That(itemsEntry, Is.Not.Null);
        Assert.That(itemsEntry.GetProperty("Home"), Is.Not.Null, "the getter that exists today is still generated");
        Assert.That(zonesEntry.GetProperty("Next"), Is.Not.Null);
    }

    [Test]
    public void AC7_TheExistingGetter_StillThrowsOnABlankReference()
    {
        SetLink("Items", 0, "Home", "");
        var item = Wrap(itemsEntry, Loaded("Items"), 0);

        Assert.Throws<InvalidOperationException>(() => Read(item, "Home"), "the existing getter keeps its documented behaviour");
    }

    [Test]
    public void AC7_AReferenceColumnHasAnAccessorThatReturnsNullForABlankReference()
    {
        SetLink("Items", 0, "Home", "");
        var item = Wrap(itemsEntry, Loaded("Items"), 0);
        var accessor = itemsEntry.GetProperty("HomeOrNull");

        Assert.That(accessor, Is.Not.Null, "a reference column needs a read path that does not throw, next to the getter");
        Assert.That(Read(item, "HomeOrNull"), Is.Null);
    }

    [Test]
    public void AC7_AReferenceColumnHasAnAccessorThatReturnsNullForADanglingReference()
    {
        SetLink("Items", 0, "Home", "Zone_Gone");
        var item = Wrap(itemsEntry, Loaded("Items"), 0);

        Assert.That(itemsEntry.GetProperty("HomeOrNull"), Is.Not.Null);
        Assert.That(Read(item, "HomeOrNull"), Is.Null);
        Assert.Throws<InvalidOperationException>(() => Read(item, "Home"), "the existing getter still throws");
    }

    [Test]
    public void AC7_TheAccessorReturnsTheRowWhenTheReferenceIsGood()
    {
        SetLink("Items", 0, "Home", "Zone_B");
        var item = Wrap(itemsEntry, Loaded("Items"), 0);

        var home = Read(item, "HomeOrNull");

        Assert.That(home, Is.Not.Null);
        Assert.That(home, Is.InstanceOf(zonesEntry));
        Assert.That(((EntryWrapper)home!)._.GetDataAsString("ID"), Is.EqualTo("Zone_B"));
        Assert.That(((EntryWrapper)Read(item, "Home")!)._.GetDataAsString("ID"), Is.EqualTo("Zone_B"), "both read paths agree on a good reference");
    }

    [Test]
    public void AC7_AChainOfSelfReferencesCanBeWalkedToItsEnd()
    {
        SetLink("Zones", 0, "Next", "Zone_B");
        SetLink("Zones", 1, "Next", "");
        Assert.That(zonesEntry.GetProperty("NextOrNull"), Is.Not.Null, "a self-referencing column needs the accessor too");
        var zone = Wrap(zonesEntry, Loaded("Zones"), 0);

        var ids = new List<string>();
        for (var hop = 0; zone != null && hop < 10; hop++)
        {
            ids.Add(((EntryWrapper)zone)._.GetDataAsString("ID"));
            zone = Read(zone, "NextOrNull")!;
        }

        Assert.That(ids, Is.EqualTo(new[] { "Zone_A", "Zone_B" }), "the chain ends at the blank link without throwing");
    }

    [Test]
    public void AC7_AReferenceToAnIntegerIdentifier_ReadsTheSameWayThroughBothMembers()
    {
        var items = Loaded("Items");
        var item = Wrap(itemsEntry, items, 0);

        items.GetEntry(0).SetData(Context, "Level", 2);
        var level = (EntryWrapper)Read(item, "LevelOrNull")!;
        Assert.That(level, Is.Not.Null);
        Assert.That(level._.GetDataDirect("ID"), Is.EqualTo(2));
        Assert.That(((EntryWrapper)Read(item, "Level")!)._.GetDataDirect("ID"), Is.EqualTo(2), "both members agree on a good reference");

        items.GetEntry(0).SetData(Context, "Level", "");
        Assert.That(Read(item, "LevelOrNull"), Is.Null, "a blank reference to an integer identifier is null, not the entry with id 0");
        Assert.Throws<InvalidOperationException>(() => Read(item, "Level"));

        items.GetEntry(0).SetData(Context, "Level", 99);
        Assert.That(Read(item, "LevelOrNull"), Is.Null, "a dangling one too");
    }

    [Test]
    public void AC7_TheSchemeClassOffersALookupThatReturnsNullInsteadOfAFailedResult()
    {
        var zonesScheme = generated.GetType("ZonesScheme")!;
        var lookup = zonesScheme.GetMethod("GetEntryOrNull", BindingFlags.Public | BindingFlags.Static);

        Assert.That(lookup, Is.Not.Null);
        Assert.That(lookup!.Invoke(null, new object?[] { "Zone_B" }), Is.InstanceOf(zonesEntry));
        Assert.That(lookup.Invoke(null, new object?[] { "Zone_Gone" }), Is.Null);
        Assert.That(lookup.Invoke(null, new object?[] { "" }), Is.Null);
        Assert.That(lookup.Invoke(null, new object?[] { null }), Is.Null);
    }
}
