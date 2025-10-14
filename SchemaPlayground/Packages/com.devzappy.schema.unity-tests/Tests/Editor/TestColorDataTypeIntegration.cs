using NUnit.Framework;
using Schema.Core;
using Schema.Core.Data;
using Schema.Runtime;
using Schema.Runtime.Type;
using UnityEngine;

namespace Schema.Unity.Editor.Tests
{
    [TestFixture]
    public class TestColorDataTypeIntegration
    {
        private static SchemaContext Context = new SchemaContext
        {
            Driver = nameof(TestColorDataTypeIntegration)
        };

        [Test]
        public void ColorDataType_ShouldBeRegisteredAsPluginType()
        {
            // Create a new Color data type instance
            var colorType = new ColorDataType();
            
            // Verify it can be added as a plugin type
            DataType.AddPluginType(colorType);
            
            // Verify it's a plugin type (not in core built-in types)
            CollectionAssert.DoesNotContain(DataType.CoreBuiltInTypes, colorType);
            
            // Verify it's now in the built-in types (which includes plugins)
            CollectionAssert.Contains(DataType.BuiltInTypes, colorType);
        }

        [Test]
        public void ColorDataType_ShouldHandleInvalidColors()
        {
            var dataEntry = new DataEntry(new System.Collections.Generic.Dictionary<string, object>
            {
                { "InvalidColor", "not a color" },
                { "EmptyColor", "" },
                { "NullColor", null }
            });
            
            // Invalid color should fail
            var invalidResult = dataEntry.GetDataAsColor("InvalidColor");
            Assert.That(invalidResult.Passed, Is.False);
            
            // Empty color should return black
            var emptyResult = dataEntry.GetDataAsColor("EmptyColor");
            Assert.That(emptyResult.Passed, Is.False);
            
            // Null color should return black
            var nullResult = dataEntry.GetDataAsColor("NullColor");
            Assert.That(nullResult.Passed, Is.False);
        }

        [Test]
        public void ColorDataType_StaticMethods_ShouldWorkCorrectly()
        {
            // Test HexToColor
            var redColor = ColorDataType.HexToColor("#FF0000");
            Assert.That(redColor, Is.EqualTo(Color.red));
            
            var greenColor = ColorDataType.HexToColor("00FF00"); // Without #
            Assert.That(greenColor, Is.EqualTo(Color.green));
            
            // Test ColorToHex
            var redHex = ColorDataType.ColorToHex(Color.red, false);
            Assert.That(redHex, Is.EqualTo("#FF0000"));
            
            var redHexWithAlpha = ColorDataType.ColorToHex(Color.red, true);
            Assert.That(redHexWithAlpha, Is.EqualTo("#FF0000FF"));
            
            // Test IsValidHexColor
            Assert.That(ColorDataType.IsValidHexColor("#FF0000"), Is.True);
            Assert.That(ColorDataType.IsValidHexColor("invalid"), Is.False);
            
            // Test NormalizeHexColor
            var normalized = ColorDataType.NormalizeHexColor("ff0000");
            Assert.That(normalized, Is.EqualTo("#FF0000FF"));
        }
    }
}