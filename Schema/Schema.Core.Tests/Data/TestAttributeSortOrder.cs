using System.Collections;
using Schema.Core.Data;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestAttributeSortOrder
{
    [Test, TestCaseSource(nameof(Equality_GoodCases))]
    public void Test_Equality_GoodCase(AttributeSortOrder a, AttributeSortOrder b)
    {
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }
    
    [Test, TestCaseSource(nameof(Equality_BadCases))]
    public void Test_Equality_BadCase(AttributeSortOrder a, AttributeSortOrder b)
    {
        Assert.That(a, Is.Not.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }

    private static IEnumerable Equality_GoodCases
    {
        get
        {
            yield return new TestCaseData(new AttributeSortOrder("Foo", SortOrder.Ascending),
            new AttributeSortOrder("Foo", SortOrder.Ascending));
        }
    }

    private static IEnumerable Equality_BadCases
    {
        get
        {
            yield return new TestCaseData(new AttributeSortOrder("Foo", SortOrder.Ascending),
            new AttributeSortOrder("Foo", SortOrder.Descending));
            yield return new TestCaseData(new AttributeSortOrder("Foo", SortOrder.Ascending),
            new AttributeSortOrder("Bar", SortOrder.Ascending));
            yield return new TestCaseData(new AttributeSortOrder("Foo", SortOrder.Ascending),
            new AttributeSortOrder("Foo", SortOrder.None));
        }
    }
}