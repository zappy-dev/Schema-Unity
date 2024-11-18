using System.Collections;
using Schema.Core.Data;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestAttributeDefinition
{
    [Test, TestCaseSource(nameof(CopyTestCases))]
    public void Test_Clone(AttributeDefinition definition)
    {
        var clone = definition.Clone() as AttributeDefinition;
        Assert.NotNull(clone);
        Assert.That(clone, Is.EqualTo(definition));
    }

    private static IEnumerable CopyTestCases
    {
        get
        {
            yield return new AttributeDefinition("Foo", DataType.Text);
            yield return new AttributeDefinition("Foo", DataType.Integer);
            yield return new AttributeDefinition("Foo", DataType.DateTime);
            yield return new AttributeDefinition("Foo", DataType.FilePath);
        }
    }

    [Test, TestCaseSource(nameof(CopyTestCases))]
    public void Test_Copy(AttributeDefinition copyFrom)
    {
        var definition = new AttributeDefinition("Foo", DataType.Text);
        definition.Copy(copyFrom);
        Assert.That(definition, Is.EqualTo(copyFrom));
    }
}