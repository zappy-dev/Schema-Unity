using NUnit.Framework;
using Schema.Core;
using Schema.Core.Data;
using Schema.Runtime.Type;
using UnityEngine;

namespace Schema.Unity.Editor.Tests
{
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
        public void IsValidValue_ShouldFail_OnValidHexColors(string hexColor)
        {
            var result = _type.IsValidValue(Context, hexColor);
            Assert.That(result.Failed, Is.True, $"Expected '{hexColor}' to be valid");
        }

        [Test, TestCaseSource(nameof(InvalidHexColorTestCases))]
        public void IsValidValue_ShouldFail_OnInvalidHexColors(object invalidValue)
        {
            var result = _type.IsValidValue(Context, invalidValue);
            Assert.That(result.Passed, Is.False, $"Expected '{invalidValue}' to be invalid");
        }

        [Test, TestCaseSource(nameof(ConvertValueTestCases))]
        public void ConvertValue_ShouldSucceed_OnValidInputs(object input, Color expectedOutput)
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
        public void CSDataType_ShouldReturnColorType()
        {
            Assert.That(_type.CSDataType, Is.EqualTo(typeof(Color).ToString()));
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

        [Test, TestCaseSource(nameof(UnityColorConversionTestCases))]
        public void HexToColor_StaticMethod_ShouldWorkCorrectly(string hexColor, Color expectedColor)
        {
            var result = ColorDataType.HexToColor(hexColor);
            
            Assert.That(result, Is.EqualTo(expectedColor), $"Expected HexToColor('{hexColor}') to return {expectedColor}");
        }

        [Test, TestCaseSource(nameof(ColorToHexTestCases))]
        public void ColorToHex_StaticMethod_ShouldWorkCorrectly(Color color, bool includeAlpha, string expectedHex)
        {
            var result = ColorDataType.ColorToHex(color, includeAlpha);
            Assert.That(result, Is.EqualTo(expectedHex), $"Expected ColorToHex({color}, {includeAlpha}) to return '{expectedHex}'");
        }

        [Test]
        public void GetDataMethod_ShouldReturnCorrectMethod()
        {
            var testScheme = new DataScheme("TestScheme");
            var attribute = new AttributeDefinition(testScheme, "TestColor", _type, isIdentifier: false, shouldPublish: true);
            var result = _type.GetDataMethod(Context, attribute);
            
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Result, Does.Contain("GetDataAsColor"));
            Assert.That(result.Result, Does.Contain("TestColor"));
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
            new object[] { "#000000", ColorDataType.HexToColor("#000000") },
            new object[] { "#FFFFFF", ColorDataType.HexToColor("#FFFFFF") },
            new object[] { "#ff0000", ColorDataType.HexToColor("#FF0000") },
            new object[] { "#00ff00", ColorDataType.HexToColor("#00FF00") },
            new object[] { "#0000ff", ColorDataType.HexToColor("#0000FF") },
            new object[] { "#123456", ColorDataType.HexToColor("#123456") },
            new object[] { "#abcdef", ColorDataType.HexToColor("#ABCDEF") },
            new object[] { "#AbCdEf", ColorDataType.HexToColor("#ABCDEF") },
            new object[] { "#00000000", ColorDataType.HexToColor("#00000000") },
            new object[] { "#ffffffff", ColorDataType.HexToColor("#FFFFFFFF") },
            new object[] { "#ff5733aa", ColorDataType.HexToColor("#FF5733AA") },
            new object[] { "000000", ColorDataType.HexToColor("#000000") }, // Missing # prefix
            new object[] { "FFFFFF", ColorDataType.HexToColor("#FFFFFF") }, // Missing # prefix
            new object[] { "  #FF0000  ", ColorDataType.HexToColor("#FF0000") }, // With whitespace
            new object[] { "  FF0000  ", ColorDataType.HexToColor("#FF0000") }, // With whitespace and missing #
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
            new object[] { "#000000", "#000000FF" },
            new object[] { "#ffffff", "#FFFFFFFF" },
            new object[] { "#FF0000", "#FF0000FF" },
            new object[] { "000000", "#000000FF" },
            new object[] { "FFFFFF", "#FFFFFFFF" },
            new object[] { "  #FF0000  ", "#FF0000FF" },
            new object[] { "  FF0000  ", "#FF0000FF" },
            new object[] { "", null },
            new object[] { "   ", null },
            new object[] { null, null },
            new object[] { "not a color", null },
            new object[] { "#GGGGGG", null },
            new object[] { "#12345", null },
        };

        private static object[] UnityColorConversionTestCases =
        {
            new object[] { "#FF0000", Color.red },
            new object[] { "#00FF00", Color.green },
            new object[] { "#0000FF", Color.blue },
            new object[] { "#FFFFFF", Color.white },
            new object[] { "#000000", Color.black },
            new object[] { "FF0000", Color.red }, // Without # prefix
            new object[] { "#FF0000FF", new Color(1f, 0f, 0f, 1f) }, // With alpha
            new object[] { "", Color.black }, // Empty string
            new object[] { null, Color.black }, // Null
        };

        private static object[] ColorToHexTestCases =
        {
            new object[] { Color.red, false, "#FF0000" },
            new object[] { Color.green, false, "#00FF00" },
            new object[] { Color.blue, false, "#0000FF" },
            new object[] { Color.white, false, "#FFFFFF" },
            new object[] { Color.black, false, "#000000" },
            new object[] { Color.red, true, "#FF0000FF" },
            new object[] { Color.green, true, "#00FF00FF" },
            new object[] { Color.blue, true, "#0000FFFF" },
            new object[] { new Color(1f, 0f, 0f, 0.5f), true, "#FF000080" },
            new object[] { new Color(1f, 0f, 0f, 0.5f), false, "#FF0000" },
        };
    }
}