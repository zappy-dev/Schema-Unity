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

    private enum SampleEnum
    {
        ValueA = 0,
        ValueB = 1,
        ValueC = 2
    }

    [Test]
    public void Test_GetDataAsInt_VariousCases()
    {
        var entry = new DataEntry();
        // Missing attribute returns default 0
        Assert.That(entry.GetDataAsInt("missing"), Is.EqualTo(0));

        entry.Add("int", 42);
        Assert.That(entry.GetDataAsInt("int"), Is.EqualTo(42));

        entry.Add("long", 42L);
        Assert.That(entry.GetDataAsInt("long"), Is.EqualTo(42));

        entry.Add("stringNumeric", "123");
        Assert.That(entry.GetDataAsInt("stringNumeric"), Is.EqualTo(123));

        entry.Add("stringBad", "notAnInt");
        Assert.That(entry.GetDataAsInt("stringBad"), Is.EqualTo(0));
    }

    [Test]
    public void Test_TryGetDataAsInt_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsInt("missing", out var missingResult), Is.False);
        Assert.That(missingResult, Is.EqualTo(0));

        entry.Add("int", 7);
        Assert.That(entry.TryGetDataAsInt("int", out var intResult), Is.True);
        Assert.That(intResult, Is.EqualTo(7));

        entry.Add("stringNumeric", "99");
        Assert.That(entry.TryGetDataAsInt("stringNumeric", out var strResult), Is.True);
        Assert.That(strResult, Is.EqualTo(99));

        entry.Add("bad", "foo");
        Assert.That(entry.TryGetDataAsInt("bad", out var badResult), Is.False);
        Assert.That(badResult, Is.EqualTo(0));
    }

    [Test]
    public void Test_GetDataAsFloat_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.GetDataAsFloat("missing"), Is.EqualTo(0f));

        entry.Add("float", 3.14f);
        Assert.That(entry.GetDataAsFloat("float"), Is.EqualTo(3.14f).Within(0.0001f));

        entry.Add("double", 2.5d);
        Assert.That(entry.GetDataAsFloat("double"), Is.EqualTo(2.5f).Within(0.0001f));

        entry.Add("int", 2);
        Assert.That(entry.GetDataAsFloat("int"), Is.EqualTo(2f).Within(0.0001f));

        entry.Add("stringNumeric", "1.25");
        Assert.That(entry.GetDataAsFloat("stringNumeric"), Is.EqualTo(1.25f).Within(0.0001f));

        entry.Add("stringBad", "abc");
        Assert.That(entry.GetDataAsFloat("stringBad"), Is.EqualTo(0f));
    }

    [Test]
    public void Test_TryGetDataAsFloat_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsFloat("missing", out var missing), Is.False);
        Assert.That(missing, Is.EqualTo(0f));

        entry.Add("float", 3f);
        Assert.That(entry.TryGetDataAsFloat("float", out var floatVal), Is.True);
        Assert.That(floatVal, Is.EqualTo(3f));

        entry.Add("stringNumeric", "2.5");
        Assert.That(entry.TryGetDataAsFloat("stringNumeric", out var floatParsed), Is.True);
        Assert.That(floatParsed, Is.EqualTo(2.5f).Within(0.0001f));

        entry.Add("bad", "bar");
        Assert.That(entry.TryGetDataAsFloat("bad", out var badVal), Is.False);
        Assert.That(badVal, Is.EqualTo(0f));
    }

    [Test]
    public void Test_GetDataAsBool_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.GetDataAsBool("missing"), Is.False);

        entry.Add("bool", true);
        Assert.That(entry.GetDataAsBool("bool"), Is.True);

        entry.Add("stringBool", "false");
        Assert.That(entry.GetDataAsBool("stringBool"), Is.False);

        entry.Add("stringBad", "notBool");
        Assert.That(entry.GetDataAsBool("stringBad"), Is.False);
    }

    [Test]
    public void Test_TryGetDataAsBool_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsBool("missing", out var missing), Is.False);
        Assert.That(missing, Is.False);

        entry.Add("bool", false);
        Assert.That(entry.TryGetDataAsBool("bool", out var boolVal), Is.True);
        Assert.That(boolVal, Is.False);

        entry.Add("stringBool", "true");
        Assert.That(entry.TryGetDataAsBool("stringBool", out var parsedBool), Is.True);
        Assert.That(parsedBool, Is.True);

        entry.Add("bad", "xyz");
        Assert.That(entry.TryGetDataAsBool("bad", out var bad), Is.False);
        Assert.That(bad, Is.False);
    }

    [Test]
    public void Test_GetDataAsString_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.GetDataAsString("missing"), Is.Null);

        entry.Add("string", "hello");
        Assert.That(entry.GetDataAsString("string"), Is.EqualTo("hello"));

        entry.Add("int", 5);
        Assert.That(entry.GetDataAsString("int"), Is.EqualTo("5"));
    }

    [Test]
    public void Test_TryGetDataAsString_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsString("missing", out var missing), Is.False);
        Assert.That(missing, Is.Null);

        entry.Add("string", "foo");
        Assert.That(entry.TryGetDataAsString("string", out var str), Is.True);
        Assert.That(str, Is.EqualTo("foo"));

        entry.Add("nullValue", null);
        Assert.That(entry.TryGetDataAsString("nullValue", out var nullStr), Is.False);
        Assert.That(nullStr, Is.Null);
    }

    [Test]
    public void Test_GetDataAsEnum_VariousCases()
    {
        var entry = new DataEntry();
        // Missing returns default (ValueA / 0)
        Assert.That(entry.GetDataAsEnum<SampleEnum>("missing"), Is.EqualTo(SampleEnum.ValueA));

        entry.Add("enum", SampleEnum.ValueB);
        Assert.That(entry.GetDataAsEnum<SampleEnum>("enum"), Is.EqualTo(SampleEnum.ValueB));

        entry.Add("intEnum", 2);
        Assert.That(entry.GetDataAsEnum<SampleEnum>("intEnum"), Is.EqualTo(SampleEnum.ValueC));

        entry.Add("stringEnum", "ValueA");
        Assert.That(entry.GetDataAsEnum<SampleEnum>("stringEnum"), Is.EqualTo(SampleEnum.ValueA));

        entry.Add("bad", "NotAValue");
        Assert.That(entry.GetDataAsEnum<SampleEnum>("bad"), Is.EqualTo(SampleEnum.ValueA));
    }

    [Test]
    public void Test_TryGetDataAsEnum_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("missing", out var missing), Is.False);
        Assert.That(missing, Is.EqualTo(SampleEnum.ValueA));

        entry.Add("enum", SampleEnum.ValueC);
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("enum", out var enumVal), Is.True);
        Assert.That(enumVal, Is.EqualTo(SampleEnum.ValueC));

        entry.Add("intEnum", 1);
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("intEnum", out var intEnum), Is.True);
        Assert.That(intEnum, Is.EqualTo(SampleEnum.ValueB));

        entry.Add("stringEnum", "ValueA");
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("stringEnum", out var parsedEnum), Is.True);
        Assert.That(parsedEnum, Is.EqualTo(SampleEnum.ValueA));

        entry.Add("bad", "Invalid");
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("bad", out var badEnum), Is.False);
        Assert.That(badEnum, Is.EqualTo(SampleEnum.ValueA));
    }
}