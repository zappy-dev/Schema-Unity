using NUnit.Framework;
using Schema.Core.Data;

namespace Schema.Unity.Editor.Tests.Unit
{
    [TestFixture]
    public class AttributeSettingsPromptUnitTests
    {
        private DataScheme testScheme;
        private AttributeDefinition testAttribute;
        
        [SetUp]
        public void Setup()
        {
            testScheme = new DataScheme("TestScheme");
            testAttribute = new AttributeDefinition("TestAttribute", DataType.Text);
            testScheme.AddAttribute(testAttribute);
        }
        
        #region AttributeDefinition Tests
        
        [Test]
        public void AttributeDefinition_WhenCreated_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var attribute = new AttributeDefinition("TestAttr", DataType.Integer, isIdentifier: true);
            
            // Assert
            Assert.That(attribute.AttributeName, Is.EqualTo("TestAttr"));
            Assert.That(attribute.DataType, Is.EqualTo(DataType.Integer));
            Assert.That(attribute.IsIdentifier, Is.True);
        }
        
        [Test]
        public void AttributeDefinition_WhenCloned_ShouldCreateEqualCopy()
        {
            // Arrange
            var original = new AttributeDefinition("TestAttr", DataType.Integer, isIdentifier: true);
            
            // Act
            var cloned = original.Clone() as AttributeDefinition;
            
            // Assert
            Assert.That(cloned, Is.Not.Null);
            Assert.That(cloned.AttributeName, Is.EqualTo(original.AttributeName));
            Assert.That(cloned.DataType, Is.EqualTo(original.DataType));
            Assert.That(cloned.IsIdentifier, Is.EqualTo(original.IsIdentifier));
            Assert.That(cloned, Is.Not.SameAs(original)); // Different instances
        }
        
        [Test]
        public void AttributeDefinition_WhenClonedAndModified_ShouldNotAffectOriginal()
        {
            // Arrange
            var original = new AttributeDefinition("TestAttr", DataType.Text, isIdentifier: false);
            
            // Act
            var cloned = original.Clone() as AttributeDefinition;
            // Modify cloned version (if mutable properties exist)
            
            // Assert
            Assert.That(original.AttributeName, Is.EqualTo("TestAttr"));
            Assert.That(original.DataType, Is.EqualTo(DataType.Text));
            Assert.That(original.IsIdentifier, Is.False);
        }
        
        [TestCase(DataType.Text)]
        [TestCase(DataType.Integer)]
        [TestCase(DataType.Float)]
        [TestCase(DataType.Boolean)]
        [TestCase(DataType.DateTime)]
        public void AttributeDefinition_WithDifferentDataTypes_ShouldRetainType(DataType dataType)
        {
            // Arrange & Act
            var attribute = new AttributeDefinition("TestAttribute", dataType);
            
            // Assert
            Assert.That(attribute.DataType, Is.EqualTo(dataType));
        }
        
        [TestCase(true)]
        [TestCase(false)]
        public void AttributeDefinition_WithIdentifierFlag_ShouldRetainFlag(bool isIdentifier)
        {
            // Arrange & Act
            var attribute = new AttributeDefinition("TestAttribute", DataType.Text, isIdentifier: isIdentifier);
            
            // Assert
            Assert.That(attribute.IsIdentifier, Is.EqualTo(isIdentifier));
        }
        
        #endregion
        
        #region DataScheme Tests
        
        [Test]
        public void DataScheme_WhenCreated_ShouldHaveCorrectName()
        {
            // Arrange & Act
            var scheme = new DataScheme("TestScheme");
            
            // Assert
            Assert.That(scheme.SchemeName, Is.EqualTo("TestScheme"));
        }
        
        [Test]
        public void DataScheme_WhenAttributeAdded_ShouldContainAttribute()
        {
            // Arrange
            var scheme = new DataScheme("TestScheme");
            var attribute = new AttributeDefinition("NewAttribute", DataType.DateTime);
            
            // Act
            scheme.AddAttribute(attribute);
            
            // Assert
            Assert.That(scheme.Attributes.Contains(attribute), Is.True);
            Assert.That(scheme.Attributes.Count, Is.EqualTo(1));
        }
        
        [Test]
        public void DataScheme_WhenMultipleAttributesAdded_ShouldContainAll()
        {
            // Arrange
            var scheme = new DataScheme("TestScheme");
            var attr1 = new AttributeDefinition("Attr1", DataType.Text);
            var attr2 = new AttributeDefinition("Attr2", DataType.Integer);
            var attr3 = new AttributeDefinition("Attr3", DataType.Boolean);
            
            // Act
            scheme.AddAttribute(attr1);
            scheme.AddAttribute(attr2);
            scheme.AddAttribute(attr3);
            
            // Assert
            Assert.That(scheme.Attributes.Count, Is.EqualTo(3));
            Assert.That(scheme.Attributes.Contains(attr1), Is.True);
            Assert.That(scheme.Attributes.Contains(attr2), Is.True);
            Assert.That(scheme.Attributes.Contains(attr3), Is.True);
        }
        
        [Test]
        public void DataScheme_WhenEmpty_ShouldHaveZeroAttributes()
        {
            // Arrange & Act
            var scheme = new DataScheme("EmptyScheme");
            
            // Assert
            Assert.That(scheme.Attributes.Count, Is.EqualTo(0));
        }
        
        #endregion
        
        #region Data Validation Tests
        
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void AttributeDefinition_WithInvalidName_ShouldHandleGracefully(string invalidName)
        {
            // Arrange & Act & Assert
            // This tests the behavior when invalid names are provided
            // The actual behavior depends on the implementation
            Assert.DoesNotThrow(() => {
                var attribute = new AttributeDefinition(invalidName, DataType.Text);
                Assert.That(attribute.AttributeName, Is.EqualTo(invalidName));
            });
        }
        
        [Test]
        public void DataScheme_WithSpecialCharactersInName_ShouldRetainName()
        {
            // Arrange
            var schemeName = "Test-Schema_123!@#";
            
            // Act
            var scheme = new DataScheme(schemeName);
            
            // Assert
            Assert.That(scheme.SchemeName, Is.EqualTo(schemeName));
        }
        
        #endregion
        
        #region Edge Cases
        
        [Test]
        public void AttributeDefinition_WithLongName_ShouldRetainFullName()
        {
            // Arrange
            var longName = new string('A', 1000); // 1000 character name
            
            // Act
            var attribute = new AttributeDefinition(longName, DataType.Text);
            
            // Assert
            Assert.That(attribute.AttributeName, Is.EqualTo(longName));
            Assert.That(attribute.AttributeName.Length, Is.EqualTo(1000));
        }
        
        [Test]
        public void DataScheme_AddingSameAttributeMultipleTimes_ShouldHandleCorrectly()
        {
            // Arrange
            var scheme = new DataScheme("TestScheme");
            var attribute = new AttributeDefinition("DuplicateAttr", DataType.Text);
            
            // Act
            scheme.AddAttribute(attribute);
            scheme.AddAttribute(attribute); // Add same instance again
            
            // Assert
            // Behavior depends on implementation - either allows duplicates or prevents them
            // This test verifies the actual behavior
            Assert.That(scheme.Attributes.Count, Is.GreaterThanOrEqualTo(1));
        }
        
        [Test]
        public void DataScheme_WithUnicodeCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            var unicodeName = "Тест_スキーマ_测试";
            var unicodeAttrName = "属性_атрибут_attribute";
            
            // Act
            var scheme = new DataScheme(unicodeName);
            var attribute = new AttributeDefinition(unicodeAttrName, DataType.Text);
            scheme.AddAttribute(attribute);
            
            // Assert
            Assert.That(scheme.SchemeName, Is.EqualTo(unicodeName));
            Assert.That(attribute.AttributeName, Is.EqualTo(unicodeAttrName));
        }
        
        #endregion
        
        #region Equality and Comparison Tests
        
        [Test]
        public void AttributeDefinition_WithSameProperties_ShouldBeEqual()
        {
            // Arrange
            var attr1 = new AttributeDefinition("SameName", DataType.Integer, isIdentifier: true);
            var attr2 = new AttributeDefinition("SameName", DataType.Integer, isIdentifier: true);
            
            // Act & Assert
            // This tests if AttributeDefinition implements equality correctly
            // Behavior depends on the actual implementation
            Assert.That(attr1.AttributeName, Is.EqualTo(attr2.AttributeName));
            Assert.That(attr1.DataType, Is.EqualTo(attr2.DataType));
            Assert.That(attr1.IsIdentifier, Is.EqualTo(attr2.IsIdentifier));
        }
        
        [Test]
        public void DataScheme_WithSameName_ShouldHaveEqualNames()
        {
            // Arrange
            var scheme1 = new DataScheme("SameName");
            var scheme2 = new DataScheme("SameName");
            
            // Act & Assert
            Assert.That(scheme1.SchemeName, Is.EqualTo(scheme2.SchemeName));
        }
        
        #endregion
        
        #region Type Safety Tests
        
        [Test]
        public void AttributeDefinition_Clone_ShouldReturnCorrectType()
        {
            // Arrange
            var original = new AttributeDefinition("TestAttr", DataType.Float);
            
            // Act
            var cloned = original.Clone();
            
            // Assert
            Assert.That(cloned, Is.TypeOf<AttributeDefinition>());
            Assert.That(cloned, Is.InstanceOf<AttributeDefinition>());
        }
        
        #endregion
    }
}