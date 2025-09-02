using Schema.Core.Data;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDateTimeDataType
{
    private DateTimeDataType _type;

    [SetUp]
    public void Setup()
    {
        _type = new DateTimeDataType();
    }

    [Test]
    public void CheckIfValidData_ShouldPass_OnDateTime()
    {
        var dt = DateTime.Now;
        var result = _type.CheckIfValidData(dt, TestFixtureSetup.SchemaTestContext);
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void CheckIfValidData_ShouldFail_OnNonDateTime()
    {
        var result = _type.CheckIfValidData("not a date", TestFixtureSetup.SchemaTestContext);
        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void ConvertData_ShouldSucceed_OnValidString()
    {
        var dateStr = "2025-01-02 03:04:05";
        var conversion = _type.ConvertData(dateStr, TestFixtureSetup.SchemaTestContext);
        Assert.That(conversion.Passed, Is.True);
        Assert.That(conversion.Result, Is.TypeOf<DateTime>());
        Assert.That(((DateTime)conversion.Result), Is.EqualTo(DateTime.Parse(dateStr)));
    }

    [Test]
    public void ConvertData_ShouldFail_OnInvalidString()
    {
        var conversion = _type.ConvertData("invalid date", TestFixtureSetup.SchemaTestContext);
        Assert.That(conversion.Passed, Is.False);
    }
} 