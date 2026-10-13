using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

/// <summary>
/// A context scope puts a value in the context for the length of an operation, and puts back what was there (finding 21 of the list in
/// docs/upstream-fixes.md). Until it does, a nested operation clears what the operation around it set.
/// </summary>
[TestFixture]
public class TestContextScopes
{
    [SetUp]
    public void Setup()
    {
        Schema.Reset();
        Schema.SetStorage(new Storage(new Mock<IFileSystem>().Object));
        Schema.InitializeTemplateManifestScheme(new SchemaContext { Driver = nameof(TestContextScopes) });
    }

    [TearDown]
    public void TearDown()
    {
        Schema.Reset();
    }

    [Test]
    public void B21_AScopeRestoresWhatTheContextHeldBefore_NotNull()
    {
        var outerScheme = new DataScheme("Outer");
        var innerScheme = new DataScheme("Inner");
        var outerEntry = new DataEntry();
        var innerEntry = new DataEntry();
        var outerType = DataType.Text;
        var innerType = DataType.Integer;
        var context = new SchemaContext { Scheme = outerScheme, AttributeName = "OuterColumn", DataType = outerType, Entry = outerEntry };

        using (new SchemeContextScope(ref context, innerScheme))
        using (new AttributeContextScope(ref context, "InnerColumn"))
        using (new DataTypeContextScope(ref context, innerType))
        using (new EntryContextScope(ref context, innerEntry))
        {
            Assert.That(context.Scheme, Is.SameAs(innerScheme));
            Assert.That(context.AttributeName, Is.EqualTo("InnerColumn"));
            Assert.That(context.DataType, Is.SameAs(innerType));
            Assert.That(context.Entry, Is.SameAs(innerEntry));
        }

        Assert.That(context.Scheme, Is.SameAs(outerScheme));
        Assert.That(context.AttributeName, Is.EqualTo("OuterColumn"));
        Assert.That(context.DataType, Is.SameAs(outerType));
        Assert.That(context.Entry, Is.SameAs(outerEntry));
    }

    [Test]
    public void B21_AScopeLeavesAnEmptyContextEmpty()
    {
        var context = new SchemaContext();

        using (new SchemeContextScope(ref context, new DataScheme("Inner")))
        {
        }

        Assert.That(context.Scheme, Is.Null);
    }

    [Test]
    public void B21_ALinkIntoATableThatIsNotLoadedYet_CanBeAddedToARowThatIsAlreadyThere()
    {
        var context = ScratchProject.Context;
        var chain = new DataScheme("Chain");
        chain.AddAttribute(context, "ID", DataType.Text, isIdentifier: true).AssertPassed();
        chain.AddAttribute(context, "Next", new ReferenceDataType("Chain", "ID", validateSchemeLoaded: false) { DefaultValue = "" }).AssertPassed();
        DataEntry Row(string id, string next) => new DataEntry { { "ID", id, context }, { "Next", next, context } };

        chain.AddEntry(context, Row("B", "")).AssertPassed();

        chain.AddEntry(context, Row("A", "B")).AssertPassed();
        chain.AddEntry(context, Row("C", "Nowhere")).AssertFailed();
    }
}
