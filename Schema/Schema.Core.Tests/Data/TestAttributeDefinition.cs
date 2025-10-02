using System.Collections;
using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestAttributeDefinition
{
    private static readonly SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestAttributeDefinition)
    };
    
    [Test, TestCaseSource(nameof(CopyTestCases))]
    public void Test_Clone(AttributeDefinition definition)
    {
        var clone = definition.Clone() as AttributeDefinition;
        Assert.NotNull(clone);
        Assert.That(clone, Is.EqualTo(definition));
        Assert.That(clone.GetHashCode(), Is.EqualTo(definition.GetHashCode()));
    }

    private static IEnumerable CopyTestCases
    {
        get
        {
            foreach (var builtInType in DataType.BuiltInTypes)
            {
                yield return new AttributeDefinition(null, "Foo", builtInType);
            }
        }
    }

    [Test, TestCaseSource(nameof(CopyTestCases))]
    public void Test_Copy(AttributeDefinition copyFrom)
    {
        var definition = new AttributeDefinition(null, "Foo", DataType.Text);
        definition.Copy(copyFrom);
        Assert.That(definition, Is.EqualTo(copyFrom));
        Assert.That(definition.GetHashCode(), Is.EqualTo(copyFrom.GetHashCode()));
    }

    [Test, TestCaseSource(nameof(CreateReferenceType_TestCases))]
    public void Test_CreateReferenceType_BadCases(AttributeDefinition definition)
    {
        definition.CreateReferenceType(Context).AssertFailed();
    }

    private static IEnumerable CreateReferenceType_TestCases
    {
        get
        {
            yield return new TestCaseData(new AttributeDefinition(null, "Foo", DataType.Text));
            yield return new TestCaseData(new AttributeDefinition(null, "Foo", DataType.Text, isIdentifier: true));
        }
    }
}