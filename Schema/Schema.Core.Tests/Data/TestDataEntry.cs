using Schema.Core.Data;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataEntry
{
    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   ")]
    public void Test_Add_BadCase(string? attributeName)
    {
        Assert.Throws<ArgumentException>(() => new DataEntry
        {
            { attributeName, "data" }
        });
    }
}