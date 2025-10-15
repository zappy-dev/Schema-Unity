using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataEntry
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestDataEntry)
    };
    
    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   ")]
    public void Test_Add_BadCase(string? attributeName)
    {
        var res = new DataEntry();
        res.Add( attributeName, "data", Context).AssertFailed();
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

        entry.Add("int", 42, Context);
        Assert.That(entry.GetDataAsInt("int"), Is.EqualTo(42));

        entry.Add("long", 42L, Context);
        Assert.That(entry.GetDataAsInt("long"), Is.EqualTo(42));

        entry.Add("stringNumeric", "123", Context);
        Assert.That(entry.GetDataAsInt("stringNumeric"), Is.EqualTo(123));

        entry.Add("stringBad", "notAnInt", Context);
        Assert.That(entry.GetDataAsInt("stringBad"), Is.EqualTo(0));
    }

    [Test]
    public void Test_TryGetDataAsInt_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsInt("missing", out var missingResult), Is.False);
        Assert.That(missingResult, Is.EqualTo(0));

        entry.Add("int", 7, Context);
        Assert.That(entry.TryGetDataAsInt("int", out var intResult), Is.True);
        Assert.That(intResult, Is.EqualTo(7));

        entry.Add("stringNumeric", "99", Context);
        Assert.That(entry.TryGetDataAsInt("stringNumeric", out var strResult), Is.True);
        Assert.That(strResult, Is.EqualTo(99));

        entry.Add("bad", "foo", Context);
        Assert.That(entry.TryGetDataAsInt("bad", out var badResult), Is.False);
        Assert.That(badResult, Is.EqualTo(0));
    }

    [Test]
    public void Test_GetDataAsFloat_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.GetDataAsFloat("missing"), Is.EqualTo(0f));

        entry.Add("float", 3.14f, Context);
        Assert.That(entry.GetDataAsFloat("float"), Is.EqualTo(3.14f).Within(0.0001f));

        entry.Add("double", 2.5d, Context);
        Assert.That(entry.GetDataAsFloat("double"), Is.EqualTo(2.5f).Within(0.0001f));

        entry.Add("int", 2, Context);
        Assert.That(entry.GetDataAsFloat("int"), Is.EqualTo(2f).Within(0.0001f));

        entry.Add("stringNumeric", "1.25", Context);
        Assert.That(entry.GetDataAsFloat("stringNumeric"), Is.EqualTo(1.25f).Within(0.0001f));

        entry.Add("stringBad", "abc", Context);
        Assert.That(entry.GetDataAsFloat("stringBad"), Is.EqualTo(0f));
    }

    [Test]
    public void Test_TryGetDataAsFloat_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsFloat("missing", out var missing), Is.False);
        Assert.That(missing, Is.EqualTo(0f));

        entry.Add("float", 3f, Context);
        Assert.That(entry.TryGetDataAsFloat("float", out var floatVal), Is.True);
        Assert.That(floatVal, Is.EqualTo(3f));

        entry.Add("stringNumeric", "2.5", Context);
        Assert.That(entry.TryGetDataAsFloat("stringNumeric", out var floatParsed), Is.True);
        Assert.That(floatParsed, Is.EqualTo(2.5f).Within(0.0001f));

        entry.Add("bad", "bar", Context);
        Assert.That(entry.TryGetDataAsFloat("bad", out var badVal), Is.False);
        Assert.That(badVal, Is.EqualTo(0f));
    }

    [Test]
    public void Test_GetDataAsBool_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.GetDataAsBool("missing"), Is.False);

        entry.Add("bool", true, Context);
        Assert.That(entry.GetDataAsBool("bool"), Is.True);

        entry.Add("stringBool", "false", Context);
        Assert.That(entry.GetDataAsBool("stringBool"), Is.False);

        entry.Add("stringBad", "notBool", Context);
        Assert.That(entry.GetDataAsBool("stringBad"), Is.False);
    }

    [Test]
    public void Test_TryGetDataAsBool_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsBool("missing", out var missing), Is.False);
        Assert.That(missing, Is.False);

        entry.Add("bool", false, Context);
        Assert.That(entry.TryGetDataAsBool("bool", out var boolVal), Is.True);
        Assert.That(boolVal, Is.False);

        entry.Add("stringBool", "true", Context);
        Assert.That(entry.TryGetDataAsBool("stringBool", out var parsedBool), Is.True);
        Assert.That(parsedBool, Is.True);

        entry.Add("bad", "xyz", Context);
        Assert.That(entry.TryGetDataAsBool("bad", out var bad), Is.False);
        Assert.That(bad, Is.False);
    }

    [Test]
    public void Test_GetDataAsString_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.GetDataAsString("missing"), Is.Empty);

        entry.Add("string", "hello", Context);
        Assert.That(entry.GetDataAsString("string"), Is.EqualTo("hello"));

        entry.Add("int", 5, Context);
        Assert.That(entry.GetDataAsString("int"), Is.EqualTo("5"));
    }

    [Test]
    public void Test_TryGetDataAsString_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsString("missing", out var missing), Is.False);
        Assert.That(missing, Is.Null);

        entry.Add("string", "foo", Context);
        Assert.That(entry.TryGetDataAsString("string", out var str), Is.True);
        Assert.That(str, Is.EqualTo("foo"));

        entry.Add("nullValue", null, Context);
        Assert.That(entry.TryGetDataAsString("nullValue", out var nullStr), Is.False);
        Assert.That(nullStr, Is.Null);
    }

    [Test]
    public void Test_GetDataAsEnum_VariousCases()
    {
        var entry = new DataEntry();
        // Missing returns default (ValueA / 0)
        Assert.That(entry.GetDataAsEnum<SampleEnum>("missing"), Is.EqualTo(SampleEnum.ValueA));

        entry.Add("enum", SampleEnum.ValueB, Context);
        Assert.That(entry.GetDataAsEnum<SampleEnum>("enum"), Is.EqualTo(SampleEnum.ValueB));

        entry.Add("intEnum", 2, Context);
        Assert.That(entry.GetDataAsEnum<SampleEnum>("intEnum"), Is.EqualTo(SampleEnum.ValueC));

        entry.Add("stringEnum", "ValueA", Context);
        Assert.That(entry.GetDataAsEnum<SampleEnum>("stringEnum"), Is.EqualTo(SampleEnum.ValueA));

        entry.Add("bad", "NotAValue", Context);
        Assert.That(entry.GetDataAsEnum<SampleEnum>("bad"), Is.EqualTo(SampleEnum.ValueA));
    }

    [Test]
    public void Test_TryGetDataAsEnum_VariousCases()
    {
        var entry = new DataEntry();
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("missing", out var missing), Is.False);
        Assert.That(missing, Is.EqualTo(SampleEnum.ValueA));

        entry.Add("enum", SampleEnum.ValueC, Context);
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("enum", out var enumVal), Is.True);
        Assert.That(enumVal, Is.EqualTo(SampleEnum.ValueC));

        entry.Add("intEnum", 1, Context);
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("intEnum", out var intEnum), Is.True);
        Assert.That(intEnum, Is.EqualTo(SampleEnum.ValueB));

        entry.Add("stringEnum", "ValueA", Context);
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("stringEnum", out var parsedEnum), Is.True);
        Assert.That(parsedEnum, Is.EqualTo(SampleEnum.ValueA));

        entry.Add("bad", "Invalid", Context);
        Assert.That(entry.TryGetDataAsEnum<SampleEnum>("bad", out var badEnum), Is.False);
        Assert.That(badEnum, Is.EqualTo(SampleEnum.ValueA));
    }

    [Test]
    public void Test_GetDataAsList_VariousCases()
    {
        var entry = new DataEntry();

        // Missing key -> empty list
        var missing = entry.GetDataAsList<int>("missing");
        Assert.That(missing, Is.Empty);

        // Null value -> empty list
        entry.Add("null", null, Context);
        var nullList = entry.GetDataAsList<string>("null");
        Assert.That(nullList, Is.Empty);

        // Exact List<T>
        entry.Add("listInt", new System.Collections.Generic.List<int> { 1, 2, 3 }, Context);
        var listInt = entry.GetDataAsList<int>("listInt");
        Assert.That(listInt, Is.EquivalentTo(new[] { 1, 2, 3 }));

        // Array -> converted to List<T>
        entry.Add("arrayStr", new[] { "a", "b" }, Context);
        var arrayStr = entry.GetDataAsList<string>("arrayStr");
        Assert.That(arrayStr, Is.EquivalentTo(new[] { "a", "b" }));

        // IEnumerable<T>
        System.Collections.Generic.IEnumerable<float> enumFloat = new System.Collections.Generic.List<float> { 1.5f, 2.5f };
        entry.Add("ienumFloat", enumFloat, Context);
        var listFloat = entry.GetDataAsList<float>("ienumFloat");
        Assert.That(listFloat, Is.EquivalentTo(new[] { 1.5f, 2.5f }));

        // Non-generic IEnumerable with convertible items
        System.Collections.ArrayList arrayList = new System.Collections.ArrayList { "1", 2, 3.0 };
        entry.Add("nongenericConvertible", arrayList, Context);
        var converted = entry.GetDataAsList<int>("nongenericConvertible");
        Assert.That(converted, Is.EquivalentTo(new[] { 1, 2, 3 }));

        // Non-generic IEnumerable with some non-convertible items (should skip those)
        System.Collections.ArrayList mixed = new System.Collections.ArrayList { "x", "4", new object() };
        entry.Add("nongenericMixed", mixed, Context);
        var mixedResult = entry.GetDataAsList<int>("nongenericMixed");
        Assert.That(mixedResult, Is.EquivalentTo(new[] { 4 }));

        // Type mismatch not enumerable -> empty list
        entry.Add("scalar", 42, Context);
        var scalarResult = entry.GetDataAsList<int>("scalar");
        Assert.That(scalarResult, Is.Empty);
    }

    [Test]
    public void Test_GetDataAsGuid_VariousCases()
    {
        var entry = new DataEntry();

        // Missing attribute returns Guid.Empty
        Assert.That(entry.GetDataAsGuid("missing"), Is.EqualTo(System.Guid.Empty));

        // Guid value is returned as-is
        var g = System.Guid.NewGuid();
        entry.Add("guid", g, Context);
        Assert.That(entry.GetDataAsGuid("guid"), Is.EqualTo(g));

        // String representation of Guid is parsed
        entry.Add("stringGuid", g.ToString(), Context);
        Assert.That(entry.GetDataAsGuid("stringGuid"), Is.EqualTo(g));

        // Bad string returns Guid.Empty
        entry.Add("badString", "not-a-guid", Context);
        Assert.That(entry.GetDataAsGuid("badString"), Is.EqualTo(System.Guid.Empty));

        // Non-Guid type not parseable returns Guid.Empty
        entry.Add("intVal", 123, Context);
        Assert.That(entry.GetDataAsGuid("intVal"), Is.EqualTo(System.Guid.Empty));
    }

    [Test]
    public void Test_GetDataAsDateTime_VariousCases()
    {
        var entry = new DataEntry();

        // Missing key -> DateTime.MinValue
        Assert.That(entry.GetDataAsDateTime("missing"), Is.EqualTo(System.DateTime.MinValue));

        // DateTime value is returned as-is
        var dt = new System.DateTime(2024, 5, 4, 3, 2, 1, System.DateTimeKind.Utc);
        entry.Add("dt", dt, Context);
        Assert.That(entry.GetDataAsDateTime("dt"), Is.EqualTo(dt));

        // String date is not parsed -> DateTime.MinValue
        entry.Add("strDate", "2024-05-04T03:02:01Z", Context);
        Assert.That(entry.GetDataAsDateTime("strDate"), Is.EqualTo(System.DateTime.MinValue));

        // Null value -> DateTime.MinValue
        entry.Add("null", null, Context);
        Assert.That(entry.GetDataAsDateTime("null"), Is.EqualTo(System.DateTime.MinValue));

        // Ticks/long not parsed -> DateTime.MinValue
        entry.Add("ticksLong", 637812345678901234L, Context);
        Assert.That(entry.GetDataAsDateTime("ticksLong"), Is.EqualTo(System.DateTime.MinValue));

        // Non-DateTime type -> DateTime.MinValue
        entry.Add("intVal", 123, Context);
        Assert.That(entry.GetDataAsDateTime("intVal"), Is.EqualTo(System.DateTime.MinValue));
    }
}