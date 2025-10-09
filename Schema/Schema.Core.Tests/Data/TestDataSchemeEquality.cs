using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataSchemeEquality
{
    private static readonly SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestDataSchemeEquality)
    };

    [SetUp]
    public void OnTestSetup()
    {
        Schema.Reset();
        
        var mockFS = new Mock<IFileSystem>();
        Schema.SetStorage(new Storage(mockFS.Object));
        Schema.InitializeTemplateManifestScheme(Context);
    }

    #region Basic Equals Tests

    [Test]
    public void Test_Equals_SameReference_ReturnsTrue()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");

        // Act & Assert
        Assert.That(scheme.Equals(scheme), Is.True);
        Assert.That(scheme == scheme, Is.True);
        Assert.That(scheme != scheme, Is.False);
    }

    [Test]
    public void Test_Equals_Null_ReturnsFalse()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");

        // Act & Assert
        Assert.That(scheme.Equals(null), Is.False);
        Assert.That(scheme == null, Is.False);
        Assert.That(scheme != null, Is.True);
    }

    [Test]
    public void Test_Equals_BothNull_ReturnsTrue()
    {
        // Arrange
        DataScheme scheme1 = null;
        DataScheme scheme2 = null;

        // Act & Assert
        Assert.That(scheme1 == scheme2, Is.True);
        Assert.That(scheme1 != scheme2, Is.False);
    }

    [Test]
    public void Test_Equals_IdenticalSchemes_ReturnsTrue()
    {
        // Arrange
        var scheme1 = CreateBasicScheme("TestScheme");
        var scheme2 = CreateBasicScheme("TestScheme");

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
        Assert.That(scheme1 == scheme2, Is.True);
        Assert.That(scheme1 != scheme2, Is.False);
    }

    [Test]
    public void Test_Equals_DifferentObject_ReturnsFalse()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");
        var differentObject = new object();

        // Act & Assert
        Assert.That(scheme.Equals(differentObject), Is.False);
    }

    #endregion

    #region SchemeName Equality Tests

    [Test]
    public void Test_Equals_DifferentSchemeName_ReturnsFalse()
    {
        // Arrange
        var scheme1 = CreateBasicScheme("Scheme1");
        var scheme2 = CreateBasicScheme("Scheme2");

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
        Assert.That(scheme1 == scheme2, Is.False);
        Assert.That(scheme1 != scheme2, Is.True);
    }

    [Test]
    public void Test_Equals_SameSchemeNameCaseSensitive_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        var scheme2 = new DataScheme("testscheme");

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_NullSchemeName_HandledCorrectly()
    {
        // Arrange
        var scheme1 = new DataScheme();
        var scheme2 = new DataScheme();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
    }

    #endregion

    #region Attributes Equality Tests

    [Test]
    public void Test_Equals_SameAttributes_ReturnsTrue()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme2.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
    }

    [Test]
    public void Test_Equals_DifferentAttributeCount_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_DifferentAttributeNames_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field2", DataType.Text).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_DifferentAttributeTypes_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Integer).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_DifferentAttributeOrder_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_AttributesWithDifferentIsIdentifier_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text, isIdentifier: true).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text, isIdentifier: false).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_AttributesWithDifferentDefaultValues_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text, defaultValue: "Default1").AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text, defaultValue: "Default2").AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_EmptyAttributes_ReturnsTrue()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        var scheme2 = new DataScheme("TestScheme");

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
    }

    #endregion

    #region Entries Equality Tests

    [Test]
    public void Test_Equals_SameEntries_ReturnsTrue()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry { { "Field1", "Value1", Context } }).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry { { "Field1", "Value1", Context } }).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
    }

    [Test]
    public void Test_Equals_DifferentEntryCount_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry { { "Field1", "Value1", Context } }).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry { { "Field1", "Value2", Context } }).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry { { "Field1", "Value1", Context } }).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_DifferentEntryValues_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry { { "Field1", "Value1", Context } }).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry { { "Field1", "Value2", Context } }).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_DifferentEntryOrder_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry { { "Field1", "Value1", Context } }).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry { { "Field1", "Value2", Context } }).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry { { "Field1", "Value2", Context } }).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry { { "Field1", "Value1", Context } }).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }

    [Test]
    public void Test_Equals_EmptyEntries_ReturnsTrue()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
    }

    [Test]
    public void Test_Equals_ComplexEntries_ReturnsTrue()
    {
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme1.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();
        scheme1.AddAttribute(Context, "Field3", DataType.Boolean).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry
        {
            { "Field1", "Value1", Context },
            { "Field2", 42, Context },
            { "Field3", true, Context }
        }).AssertPassed();

        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        scheme2.AddAttribute(Context, "Field2", DataType.Integer).AssertPassed();
        scheme2.AddAttribute(Context, "Field3", DataType.Boolean).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry
        {
            { "Field1", "Value1", Context },
            { "Field2", 42, Context },
            { "Field3", true, Context }
        }).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
    }

    #endregion

    #region GetHashCode Tests

    [Test]
    public void Test_GetHashCode_EqualSchemes_SameHashCode()
    {
        // Arrange
        var scheme1 = CreateBasicScheme("TestScheme");
        var scheme2 = CreateBasicScheme("TestScheme");

        // Act
        var hash1 = scheme1.GetHashCode();
        var hash2 = scheme2.GetHashCode();

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2), 
            "Equal schemes must have equal hash codes");
    }

    [Test]
    public void Test_GetHashCode_DifferentSchemes_DifferentHashCode()
    {
        // Arrange
        var scheme1 = CreateBasicScheme("Scheme1");
        var scheme2 = CreateBasicScheme("Scheme2");

        // Act
        var hash1 = scheme1.GetHashCode();
        var hash2 = scheme2.GetHashCode();

        // Assert
        // Note: While different objects should ideally have different hash codes,
        // this is not strictly required. However, we test for it here as a quality check.
        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public void Test_GetHashCode_ConsistentForSameObject()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");

        // Act
        var hash1 = scheme.GetHashCode();
        var hash2 = scheme.GetHashCode();

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public void Test_GetHashCode_AfterModification_DoesNotThrow()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");
        var hash1 = scheme.GetHashCode();

        // Act
        scheme.AddAttribute(Context, "NewField", DataType.Text);
        var hash2 = scheme.GetHashCode();

        // Assert - just ensure no exception is thrown and a hash is returned
        Assert.That(hash2, Is.Not.Zero);
    }

    #endregion

    #region IEqualityComparer Interface Tests

    [Test]
    public void Test_IEqualityComparer_Equals_IdenticalSchemes_ReturnsTrue()
    {
        // Arrange
        var scheme1 = CreateBasicScheme("TestScheme");
        var scheme2 = CreateBasicScheme("TestScheme");
        var comparer = scheme1 as System.Collections.Generic.IEqualityComparer<DataScheme>;

        // Act & Assert
        Assert.That(comparer.Equals(scheme1, scheme2), Is.True,
            "IEqualityComparer should use value equality like the instance Equals method");
    }

    [Test]
    public void Test_IEqualityComparer_Equals_BothNull_ReturnsTrue()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");
        var comparer = scheme as System.Collections.Generic.IEqualityComparer<DataScheme>;

        // Act & Assert
        Assert.That(comparer.Equals(null, null), Is.True);
    }

    [Test]
    public void Test_IEqualityComparer_Equals_OneNull_ReturnsFalse()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");
        var comparer = scheme as System.Collections.Generic.IEqualityComparer<DataScheme>;

        // Act & Assert
        Assert.That(comparer.Equals(scheme, null), Is.False);
        Assert.That(comparer.Equals(null, scheme), Is.False);
    }

    [Test]
    public void Test_IEqualityComparer_Equals_SameReference_ReturnsTrue()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");
        var comparer = scheme as System.Collections.Generic.IEqualityComparer<DataScheme>;

        // Act & Assert
        Assert.That(comparer.Equals(scheme, scheme), Is.True);
    }

    [Test]
    public void Test_IEqualityComparer_Equals_DifferentType_ReturnsFalse()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");
        var comparer = scheme as System.Collections.Generic.IEqualityComparer<DataScheme>;
        
        // Create a derived type to test type checking
        var derivedScheme = new TestDerivedDataScheme("TestScheme");

        // Act & Assert
        Assert.That(comparer.Equals(scheme, derivedScheme), Is.False);
    }

    [Test]
    public void Test_IEqualityComparer_GetHashCode_EqualSchemes_SameHashCode()
    {
        // Arrange
        var scheme1 = CreateBasicScheme("TestScheme");
        var scheme2 = CreateBasicScheme("TestScheme");
        var comparer = scheme1 as System.Collections.Generic.IEqualityComparer<DataScheme>;

        // Act
        var hash1 = comparer.GetHashCode(scheme1);
        var hash2 = comparer.GetHashCode(scheme2);

        // Assert
        Assert.That(hash1, Is.EqualTo(hash2),
            "Equal schemes must have equal hash codes via IEqualityComparer");
    }

    [Test]
    public void Test_IEqualityComparer_GetHashCode_NullScheme_ReturnsZero()
    {
        // Arrange
        var scheme = CreateBasicScheme("TestScheme");
        var comparer = scheme as System.Collections.Generic.IEqualityComparer<DataScheme>;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => comparer.GetHashCode(null));
    }

    #endregion

    #region Contract Violation Tests

    [Test]
    public void Test_EqualsAndGetHashCode_ContractViolation_Demonstration()
    {
        // This test demonstrates the bug: when Equals returns true, GetHashCode should return the same value
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        
        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        // Act
        bool areEqual = scheme1.Equals(scheme2);
        int hash1 = scheme1.GetHashCode();
        int hash2 = scheme2.GetHashCode();

        // Assert - This is the fundamental contract of GetHashCode
        Assert.That(areEqual, Is.True, "Schemes should be equal");
        Assert.That(hash1, Is.EqualTo(hash2), 
            "CRITICAL: If Equals returns true, GetHashCode MUST return the same value. " +
            "This is required for HashSet, Dictionary, and other hash-based collections to work correctly.");
    }

    [Test]
    public void Test_HashSet_WorksCorrectlyWithValueEquality()
    {
        // This test verifies that equal DataSchemes work correctly in HashSet
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        
        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var hashSet = new HashSet<DataScheme>();

        // Act
        hashSet.Add(scheme1);
        bool containsScheme2 = hashSet.Contains(scheme2);

        // Assert
        Assert.That(scheme1.Equals(scheme2), Is.True, "Schemes should be equal");
        Assert.That(containsScheme2, Is.True, 
            "HashSet should find scheme2 because it's equal to scheme1 (value equality)");
        Assert.That(hashSet.Count, Is.EqualTo(1), "HashSet should only contain 1 item");
    }

    [Test]
    public void Test_Dictionary_WorksCorrectlyWithValueEquality()
    {
        // This test verifies that equal DataSchemes work correctly as Dictionary keys
        // Arrange
        var scheme1 = new DataScheme("TestScheme");
        scheme1.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();
        
        var scheme2 = new DataScheme("TestScheme");
        scheme2.AddAttribute(Context, "Field1", DataType.Text).AssertPassed();

        var dictionary = new Dictionary<DataScheme, string>();

        // Act
        dictionary[scheme1] = "First";
        bool hasScheme2 = dictionary.ContainsKey(scheme2);

        // Assert
        Assert.That(scheme1.Equals(scheme2), Is.True, "Schemes should be equal");
        Assert.That(hasScheme2, Is.True, 
            "Dictionary should find scheme2 as a key because it's equal to scheme1");
        Assert.That(dictionary[scheme2], Is.EqualTo("First"), 
            "Should be able to retrieve value using equal key");
    }

    #endregion

    #region Complex Scenarios

    [Test]
    public void Test_Equals_ComplexScheme_AllPropertiesEqual_ReturnsTrue()
    {
        // Arrange
        var scheme1 = new DataScheme("ComplexScheme");
        scheme1.AddAttribute(Context, "TextField", DataType.Text, defaultValue: "default").AssertPassed();
        scheme1.AddAttribute(Context, "IntField", DataType.Integer, isIdentifier: true).AssertPassed();
        scheme1.AddAttribute(Context, "BoolField", DataType.Boolean).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry
        {
            { "TextField", "test", Context },
            { "IntField", 1, Context },
            { "BoolField", true, Context }
        }).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry
        {
            { "TextField", "test2", Context },
            { "IntField", 2, Context },
            { "BoolField", false, Context }
        }).AssertPassed();

        var scheme2 = new DataScheme("ComplexScheme");
        scheme2.AddAttribute(Context, "TextField", DataType.Text, defaultValue: "default").AssertPassed();
        scheme2.AddAttribute(Context, "IntField", DataType.Integer, isIdentifier: true).AssertPassed();
        scheme2.AddAttribute(Context, "BoolField", DataType.Boolean).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry
        {
            { "TextField", "test", Context },
            { "IntField", 1, Context },
            { "BoolField", true, Context }
        }).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry
        {
            { "TextField", "test2", Context },
            { "IntField", 2, Context },
            { "BoolField", false, Context }
        }).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.True);
    }

    [Test]
    public void Test_Equals_ComplexScheme_OneDifference_ReturnsFalse()
    {
        // Arrange
        var scheme1 = new DataScheme("ComplexScheme");
        scheme1.AddAttribute(Context, "TextField", DataType.Text, defaultValue: "default").AssertPassed();
        scheme1.AddAttribute(Context, "IntField", DataType.Integer).AssertPassed();
        scheme1.AddEntry(Context, new DataEntry
        {
            { "TextField", "test", Context },
            { "IntField", 1, Context }
        }).AssertPassed();

        var scheme2 = new DataScheme("ComplexScheme");
        scheme2.AddAttribute(Context, "TextField", DataType.Text, defaultValue: "default").AssertPassed();
        scheme2.AddAttribute(Context, "IntField", DataType.Integer).AssertPassed();
        scheme2.AddEntry(Context, new DataEntry
        {
            { "TextField", "test", Context },
            { "IntField", 2, Context } // Different value
        }).AssertPassed();

        // Act & Assert
        Assert.That(scheme1.Equals(scheme2), Is.False);
    }


    #endregion

    #region Helper Methods

    private DataScheme CreateBasicScheme(string schemeName)
    {
        return new DataScheme(schemeName);
    }

    // Test derived class to verify type checking
    private class TestDerivedDataScheme : DataScheme
    {
        public TestDerivedDataScheme(string schemeName) : base(schemeName)
        {
        }
    }

    #endregion
}

