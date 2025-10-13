using System;
using NUnit.Framework;
using Schema.Core.Data;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestColorDataType
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestColorDataType)
    };
    private ColorDataType _type;

    [SetUp]
    public void Setup()
    {
        _type = new ColorDataType();
    }

    [Test, TestCaseSource(nameof(ValidHexColorTestCases))]
    public void IsValidValue_ShouldPass_OnValidHexColors(string hexColor)
    {
        var result = _type.IsValidValue(Context, hexColor);
        Assert.That(result.Passed, Is.True, $"Expected '{hexColor}' to be valid");
    }

    [Test, TestCaseSource(nameof(InvalidHexColorTestCases))]
    public void IsValidValue_ShouldFail_OnInvalidHexColors(object invalidValue)
    {
        var result = _type.IsValidValue(Context, invalidValue);
        Assert.That(result.Passed, Is.False, $"Expected '{invalidValue}' to be invalid");
    }

    [Test, TestCaseSource(nameof(ConvertValueTestCases))]
    public void ConvertValue_ShouldSucceed_OnValidInputs(object input, string expectedOutput)
    {
        var conversion = _type.ConvertValue(Context, input);
        Assert.That(conversion.Passed, Is.True, $"Expected conversion of '{input}' to succeed");
        Assert.That(conversion.Result, Is.EqualTo(expectedOutput), $"Expected '{expectedOutput}', got '{conversion.Result}'");
    }

    [Test, TestCaseSource(nameof(ConvertValueFailureTestCases))]
    public void ConvertValue_ShouldFail_OnInvalidInputs(object input)
    {
        var conversion = _type.ConvertValue(Context, input);
        Assert.That(conversion.Passed, Is.False, $"Expected conversion of '{input}' to fail");
    }

    [Test]
    public void ConvertValue_ShouldHandleNullInput()
    {
        var conversion = _type.ConvertValue(Context, null);
        Assert.That(conversion.Passed, Is.True);
        Assert.That(conversion.Result, Is.EqualTo("#000000"));
    }

    [Test]
    public void ConvertValue_ShouldHandleEmptyString()
    {
        var conversion = _type.ConvertValue(Context, "");
        Assert.That(conversion.Passed, Is.True);
        Assert.That(conversion.Result, Is.EqualTo("#000000"));
    }

    [Test]
    public void ConvertValue_ShouldHandleWhitespaceString()
    {
        var conversion = _type.ConvertValue(Context, "   ");
        Assert.That(conversion.Passed, Is.True);
        Assert.That(conversion.Result, Is.EqualTo("#000000"));
    }

    [Test]
    public void Clone_ShouldCreateIdenticalInstance()
    {
        var original = new ColorDataType("#FF5733");
        var cloned = (ColorDataType)original.Clone();
        
        Assert.That(cloned.TypeName, Is.EqualTo(original.TypeName));
        Assert.That(cloned.DefaultValue, Is.EqualTo(original.DefaultValue));
    }

    [Test]
    public void TypeName_ShouldReturnColor()
    {
        Assert.That(_type.TypeName, Is.EqualTo("Color"));
    }

    [Test]
    public void CSDataType_ShouldReturnStringType()
    {
        Assert.That(_type.CSDataType, Is.EqualTo(typeof(string).ToString()));
    }

    [Test]
    public void DefaultValue_ShouldBeBlack()
    {
        Assert.That(_type.DefaultValue, Is.EqualTo("#000000"));
    }

    [Test]
    public void Constructor_WithCustomDefault_ShouldSetValue()
    {
        var customColor = new ColorDataType("#FF5733");
        Assert.That(customColor.DefaultValue, Is.EqualTo("#FF5733"));
    }

    [Test, TestCaseSource(nameof(StaticMethodTestCases))]
    public void IsValidHexColor_StaticMethod_ShouldWorkCorrectly(string hexColor, bool expected)
    {
        var result = ColorDataType.IsValidHexColor(hexColor);
        Assert.That(result, Is.EqualTo(expected), $"Expected IsValidHexColor('{hexColor}') to return {expected}");
    }

    [Test, TestCaseSource(nameof(NormalizeHexColorTestCases))]
    public void NormalizeHexColor_StaticMethod_ShouldWorkCorrectly(string input, string expected)
    {
        var result = ColorDataType.NormalizeHexColor(input);
        Assert.That(result, Is.EqualTo(expected), $"Expected NormalizeHexColor('{input}') to return '{expected}'");
    }

    [Test]
    public void Equality_ShouldWorkCorrectly()
    {
        var color1 = new ColorDataType();
        var color2 = new ColorDataType();
        var color3 = new ColorDataType("#FF5733");
        
        Assert.That(color1.Equals(color2), Is.True);
        Assert.That(color1 == color2, Is.True);
        Assert.That(color1.GetHashCode(), Is.EqualTo(color2.GetHashCode()));
        
        Assert.That(color1.Equals(color3), Is.True); // Same type, different default values should still be equal
        Assert.That(color1 == color3, Is.True);
    }

    private static object[] ValidHexColorTestCases =
    {
        "#000000",
        "#FFFFFF",
        "#FF0000",
        "#00FF00",
        "#0000FF",
        "#123456",
        "#ABCDEF",
        "#abcdef",
        "#AbCdEf",
        "#00000000", // 8-digit with alpha
        "#FFFFFFFF", // 8-digit with alpha
        "#FF5733AA", // 8-digit with alpha
    };

    private static object[] InvalidHexColorTestCases =
    {
        "",
        "   ",
        null,
        "not a color",
        "#GGGGGG",
        "#12345", // Too short
        "#1234567", // Too long
        "#123456789", // Too long
        "000000", // Missing #
        "#", // Just #
        "#12345G", // Invalid character
        "#12345g", // Invalid character
        123,
        true,
        new object(),
    };

    private static object[] ConvertValueTestCases =
    {
        new object[] { "#000000", "#000000" },
        new object[] { "#FFFFFF", "#FFFFFF" },
        new object[] { "#ff0000", "#FF0000" },
        new object[] { "#00ff00", "#00FF00" },
        new object[] { "#0000ff", "#0000FF" },
        new object[] { "#123456", "#123456" },
        new object[] { "#abcdef", "#ABCDEF" },
        new object[] { "#AbCdEf", "#ABCDEF" },
        new object[] { "#00000000", "#00000000" },
        new object[] { "#ffffffff", "#FFFFFFFF" },
        new object[] { "#ff5733aa", "#FF5733AA" },
        new object[] { "000000", "#000000" }, // Missing # prefix
        new object[] { "FFFFFF", "#FFFFFF" }, // Missing # prefix
        new object[] { "  #FF0000  ", "#FF0000" }, // With whitespace
        new object[] { "  FF0000  ", "#FF0000" }, // With whitespace and missing #
    };

    private static object[] ConvertValueFailureTestCases =
    {
        "not a color",
        "#GGGGGG",
        "#12345",
        "#1234567",
        "#123456789",
        "#12345G",
        "#12345g",
    };

    private static object[] StaticMethodTestCases =
    {
        new object[] { "#000000", true },
        new object[] { "#FFFFFF", true },
        new object[] { "#FF0000", true },
        new object[] { "#00000000", true },
        new object[] { "#FFFFFFFF", true },
        new object[] { "", false },
        new object[] { "   ", false },
        new object[] { null, false },
        new object[] { "not a color", false },
        new object[] { "#GGGGGG", false },
        new object[] { "#12345", false },
        new object[] { "#1234567", false },
        new object[] { "000000", false },
        new object[] { "#", false },
    };

    private static object[] NormalizeHexColorTestCases =
    {
        new object[] { "#000000", "#000000" },
        new object[] { "#ffffff", "#FFFFFF" },
        new object[] { "#FF0000", "#FF0000" },
        new object[] { "000000", "#000000" },
        new object[] { "FFFFFF", "#FFFFFF" },
        new object[] { "  #FF0000  ", "#FF0000" },
        new object[] { "  FF0000  ", "#FF0000" },
        new object[] { "", null },
        new object[] { "   ", null },
        new object[] { null, null },
        new object[] { "not a color", null },
        new object[] { "#GGGGGG", null },
        new object[] { "#12345", null },
    };
}