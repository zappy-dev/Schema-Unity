using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;
using static Schema.Core.Data.ReferenceDataTypeFactory;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestReferenceDataType
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestReferenceDataType)
    };

    private const string REFERENCE_SCHEME_NAME = "TestScheme";
    private const string REFERENCE_ATTRIBUTE_NAME = "ID";
    private const string NON_EXISTENT_SCHEME = "NonExistentScheme";
    private const string INVALID_ATTRIBUTE = "InvalidAttribute";

    private DataScheme _testScheme;
    private Mock<IFileSystem> _mockFileSystem;

    [SetUp]
    public async Task Setup()
    {
        (_mockFileSystem, _) = await TestFixtureSetup.Initialize(Context);

        // Create a test scheme with string identifiers
        _testScheme = new DataScheme(REFERENCE_SCHEME_NAME);
        _testScheme.AddAttribute(Context, REFERENCE_ATTRIBUTE_NAME, DataType.Text, isIdentifier: true).AssertPassed();
        _testScheme.AddAttribute(Context, "Name", DataType.Text).AssertPassed();
        
        // Add some test entries
        _testScheme.AddEntry(Context, new DataEntry 
        { 
            { REFERENCE_ATTRIBUTE_NAME, "item1", Context },
            { "Name", "First Item", Context }
        }).AssertPassed();
        _testScheme.AddEntry(Context, new DataEntry
        {
            { REFERENCE_ATTRIBUTE_NAME, "item2", Context },
            { "Name", "Second Item", Context }
        }).AssertPassed();
        _testScheme.AddEntry(Context, new DataEntry
        {
            { REFERENCE_ATTRIBUTE_NAME, "item3", Context },
            { "Name", "Third Item", Context }
        }).AssertPassed();

        Schema.LoadDataScheme(Context, _testScheme, overwriteExisting: true).AssertPassed();
    }

    [TearDown]
    public void TearDown()
    {
        Schema.Reset();
    }

    #region Constructor Tests

    [Test]
    public void Constructor_Default_SetsEmptyValues()
    {
        var refType = CreateReferenceDataType(Context, null, null, validateSchemeLoaded: false).AssertPassed();
        
        Assert.That(refType.ReferenceSchemeName, Is.Null);
        Assert.That(refType.ReferenceAttributeName, Is.Null);
        Assert.That(refType.SupportsEmptyReferences, Is.True);
        Assert.That(refType.DefaultValue, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Constructor_WithValidScheme_SetsProperties()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.ReferenceSchemeName, Is.EqualTo(REFERENCE_SCHEME_NAME));
        Assert.That(refType.ReferenceAttributeName, Is.EqualTo(REFERENCE_ATTRIBUTE_NAME));
        Assert.That(refType.SupportsEmptyReferences, Is.True);
    }

    [Test]
    public void Constructor_WithValidScheme_SetsDefaultValueFromFirstEntry()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.DefaultValue, Is.EqualTo("item1"));
    }

    [Test]
    public void Constructor_WithNonExistentScheme_Fails()
    {
        CreateReferenceDataType(Context, NON_EXISTENT_SCHEME, REFERENCE_ATTRIBUTE_NAME).AssertFailed();
    }

    #endregion

    #region TypeName Tests

    [Test]
    public void TypeName_ReturnsCorrectFormat()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.TypeName, Is.EqualTo($"Reference/{REFERENCE_SCHEME_NAME} - {REFERENCE_ATTRIBUTE_NAME}"));
    }

    [Test]
    public void TypeName_WithNullValues_HandlesGracefully()
    {
        var refType = CreateReferenceDataType(Context, null, null, validateSchemeLoaded: false).AssertPassed();
        
        Assert.That(refType.TypeName, Does.Contain("Reference"));
    }

    [Test]
    public void ToString_ReturnsCorrectFormat()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.ToString(), Does.Contain(REFERENCE_SCHEME_NAME));
        Assert.That(refType.ToString(), Does.Contain(REFERENCE_ATTRIBUTE_NAME));
    }

    #endregion

    #region Clone Tests

    [Test]
    public void Clone_CreatesNewInstance()
    {
        var original = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var cloned = (ReferenceDataType)original.Clone();
        
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned, Is.Not.SameAs(original));
    }

    [Test]
    public void Clone_CopiesAllProperties()
    {
        var original = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        original.SupportsEmptyReferences = false;
        var cloned = (ReferenceDataType)original.Clone();
        
        Assert.That(cloned.ReferenceSchemeName, Is.EqualTo(original.ReferenceSchemeName));
        Assert.That(cloned.ReferenceAttributeName, Is.EqualTo(original.ReferenceAttributeName));
        Assert.That(cloned.SupportsEmptyReferences, Is.EqualTo(original.SupportsEmptyReferences));
        Assert.That(cloned.DefaultValue, Is.EqualTo(original.DefaultValue));
    }

    [Test]
    public void Clone_WithDefaultConstructor_Works()
    {
        var original = CreateReferenceDataType(Context, null, null, validateSchemeLoaded: false).AssertPassed();
        var cloned = (ReferenceDataType)original.Clone();
        
        Assert.That(cloned, Is.Not.Null);
    }

    #endregion

    #region Equality Tests

    [Test]
    public void Equals_SameSchemeAndAttribute_ReturnsTrue()
    {
        var ref1 = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var ref2 = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(ref1.Equals(ref2), Is.True);
        Assert.That(ref1 == ref2, Is.True);
    }

    [Test]
    public void Equals_DifferentScheme_ReturnsFalse()
    {
        var otherScheme = new DataScheme("OtherScheme");
        otherScheme.AddAttribute(Context, REFERENCE_ATTRIBUTE_NAME, DataType.Text, isIdentifier: true).AssertPassed();
        otherScheme.Load(Context).AssertPassed();
        
        var ref1 = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var ref2 = CreateReferenceDataType(Context, "OtherScheme", REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(ref1.Equals(ref2), Is.False);
        Assert.That(ref1 == ref2, Is.False);
    }

    [Test]
    public void Equals_DifferentAttribute_ReturnsFalse()
    {
        CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, "OtherAttribute").AssertFailed();
    }

    [Test]
    public void Equals_Null_ReturnsFalse()
    {
        var ref1 = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(ref1.Equals(null), Is.False);
    }

    [Test]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var ref1 = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(ref1.Equals(DataType.Text), Is.False);
    }

    [Test]
    public void GetHashCode_SameSchemeAndAttribute_SameHashCode()
    {
        var ref1 = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var ref2 = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(ref1.GetHashCode(), Is.EqualTo(ref2.GetHashCode()));
    }

    #endregion

    #region IsValidValue Tests

    [Test]
    public void IsValidValue_ValidIdentifier_ReturnsTrue()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var result = refType.IsValidValue(Context, "item1");
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Message, Does.Contain("exists"));
    }

    [Test]
    public void IsValidValue_AllValidIdentifiers_ReturnTrue()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.IsValidValue(Context, "item1").Passed, Is.True);
        Assert.That(refType.IsValidValue(Context, "item2").Passed, Is.True);
        Assert.That(refType.IsValidValue(Context, "item3").Passed, Is.True);
    }

    [Test]
    public void IsValidValue_InvalidIdentifier_ReturnsFalse()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var result = refType.IsValidValue(Context, "nonexistent");
        
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void IsValidValue_NullWithEmptyReferencesAllowed_ReturnsTrue()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        refType.SupportsEmptyReferences = true;
        var result = refType.IsValidValue(Context, null);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Message, Does.Contain("allowed"));
    }

    [Test]
    public void IsValidValue_NullWithEmptyReferencesDisallowed_ReturnsFalse()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        refType.SupportsEmptyReferences = false;
        var result = refType.IsValidValue(Context, null);
        
        Assert.That(result.Passed, Is.False);
        Assert.That(result.Message, Does.Contain("not allowed"));
    }

    [Test]
    public void IsValidValue_NonExistentScheme_ReturnsFalse()
    {
        CreateReferenceDataType(Context, NON_EXISTENT_SCHEME, REFERENCE_ATTRIBUTE_NAME).AssertFailed();
    }

    [Test]
    public void IsValidValue_WrongAttributeName_ReturnsFalse()
    {
        CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, INVALID_ATTRIBUTE).AssertFailed();
    }

    #endregion

    #region ConvertValue Tests

    [Test]
    public void ConvertValue_ValidString_ConvertsSuccessfully()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var result = refType.ConvertValue(Context, "item1");
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.EqualTo("item1"));
    }

    [Test]
    public void ConvertValue_InvalidIdentifier_ReturnsFalse()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var result = refType.ConvertValue(Context, "nonexistent");
        
        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void ConvertValue_NonExistentScheme_ReturnsFalse()
    {
        CreateReferenceDataType(Context, NON_EXISTENT_SCHEME, REFERENCE_ATTRIBUTE_NAME).AssertFailed();
    }

    [Test]
    public void ConvertValue_AllValidItems_Converts()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.ConvertValue(Context, "item1").Passed, Is.True);
        Assert.That(refType.ConvertValue(Context, "item2").Passed, Is.True);
        Assert.That(refType.ConvertValue(Context, "item3").Passed, Is.True);
    }

    #endregion

    #region GetReferencedIdentifierAttribute Tests

    [Test]
    public void GetReferencedIdentifierAttribute_ValidReference_ReturnsAttribute()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var result = refType.GetReferencedIdentifierAttribute(Context);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result.AttributeName, Is.EqualTo(REFERENCE_ATTRIBUTE_NAME));
        Assert.That(result.Result.IsIdentifier, Is.True);
    }

    [Test]
    public void GetReferencedIdentifierAttribute_NonExistentScheme_ReturnsFalse()
    {
        CreateReferenceDataType(Context, NON_EXISTENT_SCHEME, REFERENCE_ATTRIBUTE_NAME).AssertFailed();
    }

    [Test]
    public void GetReferencedIdentifierAttribute_WrongAttributeName_ReturnsFalse()
    {
        CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, INVALID_ATTRIBUTE).AssertFailed();
    }

    #endregion

    #region SupportsEmptyReferences Tests

    [Test]
    public void SupportsEmptyReferences_DefaultIsTrue()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.SupportsEmptyReferences, Is.True);
    }

    [Test]
    public void SupportsEmptyReferences_CanBeSetToFalse()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        refType.SupportsEmptyReferences = false;
        
        Assert.That(refType.SupportsEmptyReferences, Is.False);
    }

    [Test]
    public void SupportsEmptyReferences_AffectsNullValidation()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        // With support enabled
        refType.SupportsEmptyReferences = true;
        Assert.That(refType.IsValidValue(Context, null).Passed, Is.True);
        
        // With support disabled
        refType.SupportsEmptyReferences = false;
        Assert.That(refType.IsValidValue(Context, null).Passed, Is.False);
    }

    #endregion

    #region CSDataType Tests

    [Test]
    public void CSDataType_ReturnsSchemeEntryClassName()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.CSDataType, Does.Contain(REFERENCE_SCHEME_NAME));
        Assert.That(refType.CSDataType, Does.Contain("Entry"));
    }

    #endregion

    #region GetDataMethod Tests

    [Test]
    public void GetDataMethod_ValidReference_ReturnsGetEntryCode()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var attribute = new AttributeDefinition(null, "TestAttr", refType);
        var result = refType.GetDataMethod(Context, attribute);
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Does.Contain("GetEntry"));
        Assert.That(result.Result, Does.Contain(REFERENCE_SCHEME_NAME));
    }

    [Test]
    public void GetDataMethod_NonExistentScheme_ReturnsFalse()
    {
        CreateReferenceDataType(Context, NON_EXISTENT_SCHEME, REFERENCE_ATTRIBUTE_NAME).AssertFailed();
    }

    #endregion

    #region Integer Identifier Tests

    [Test]
    public void IntegerIdentifier_IsValidValue_AcceptsIntegers()
    {
        // Create scheme with integer identifier
        var intScheme = new DataScheme("IntScheme");
        intScheme.AddAttribute(Context, "IntID", DataType.Integer, isIdentifier: true);
        intScheme.AddEntry(Context, new DataEntry { { "IntID", 1, Context } });
        intScheme.AddEntry(Context, new DataEntry { { "IntID", 2, Context } });
        Schema.LoadDataScheme(Context, intScheme, overwriteExisting: true);

        var refType = CreateReferenceDataType(Context, "IntScheme", "IntID").AssertPassed();
        
        Assert.That(refType.IsValidValue(Context, 1).Passed, Is.True);
        Assert.That(refType.IsValidValue(Context, 2).Passed, Is.True);
        Assert.That(refType.IsValidValue(Context, 99).Passed, Is.False);
    }

    [Test]
    public void IntegerIdentifier_ConvertValue_ConvertsStringsToIntegers()
    {
        // Create scheme with integer identifier
        var intScheme = new DataScheme("IntScheme");
        intScheme.AddAttribute(Context, "IntID", DataType.Integer, isIdentifier: true);
        intScheme.AddEntry(Context, new DataEntry { { "IntID", 1, Context } });
        intScheme.AddEntry(Context, new DataEntry { { "IntID", 2, Context } });
        Schema.LoadDataScheme(Context, intScheme, overwriteExisting: true);

        var refType = CreateReferenceDataType(Context, "IntScheme", "IntID").AssertPassed();
        var result = refType.ConvertValue(Context, "1");
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.EqualTo(1));
    }

    [Test]
    public void IntegerIdentifier_ConvertValue_InvalidString_ReturnsFalse()
    {
        // Create scheme with integer identifier
        var intScheme = new DataScheme("IntScheme");
        intScheme.AddAttribute(Context, "IntID", DataType.Integer, isIdentifier: true);
        intScheme.AddEntry(Context, new DataEntry { { "IntID", 1, Context } });
        Schema.LoadDataScheme(Context, intScheme, overwriteExisting: true);

        var refType = CreateReferenceDataType(Context, "IntScheme", "IntID").AssertPassed();
        var result = refType.ConvertValue(Context, "not a number");
        
        Assert.That(result.Passed, Is.False);
    }

    #endregion

    #region Guid Identifier Tests

    [Test]
    public void GuidIdentifier_IsValidValue_AcceptsGuids()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        // Create scheme with Guid identifier
        var guidScheme = new DataScheme("GuidScheme");
        guidScheme.AddAttribute(Context, "GuidID", DataType.Guid, isIdentifier: true);
        guidScheme.AddEntry(Context, new DataEntry { { "GuidID", guid1, Context } });
        guidScheme.AddEntry(Context, new DataEntry { { "GuidID", guid2, Context } });
        Schema.LoadDataScheme(Context, guidScheme, overwriteExisting: true);

        var refType = CreateReferenceDataType(Context, "GuidScheme", "GuidID").AssertPassed();
        
        Assert.That(refType.IsValidValue(Context, guid1).Passed, Is.True);
        Assert.That(refType.IsValidValue(Context, guid2).Passed, Is.True);
        Assert.That(refType.IsValidValue(Context, Guid.NewGuid()).Passed, Is.False);
    }

    [Test]
    public void GuidIdentifier_ConvertValue_ConvertsStringsToGuids()
    {
        var guid1 = Guid.NewGuid();

        // Create scheme with Guid identifier
        var guidScheme = new DataScheme("GuidScheme");
        guidScheme.AddAttribute(Context, "GuidID", DataType.Guid, isIdentifier: true);
        guidScheme.AddEntry(Context, new DataEntry { { "GuidID", guid1, Context } });
        Schema.LoadDataScheme(Context, guidScheme, overwriteExisting: true);

        var refType = CreateReferenceDataType(Context, "GuidScheme", "GuidID").AssertPassed();
        var result = refType.ConvertValue(Context, guid1.ToString());
        
        Assert.That(result.Passed, Is.True);
        Assert.That(result.Result, Is.EqualTo(guid1));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void IsValidValue_EmptyString_WithEmptyReferencesAllowed_ValidatesAsIdentifier()
    {
        // Add an empty string as an identifier
        _testScheme.AddEntry(Context, new DataEntry
        {
            { REFERENCE_ATTRIBUTE_NAME, "", Context },
            { "Name", "Empty Item", Context }
        });

        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        refType.SupportsEmptyReferences = true;
        
        var result = refType.IsValidValue(Context, "");
        Assert.That(result.Passed, Is.True);
    }

    [Test]
    public void ConvertValue_CaseSensitive_DifferentCase_ReturnsFalse()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        var result = refType.ConvertValue(Context, "ITEM1");
        
        // Assuming case-sensitive comparison
        Assert.That(result.Passed, Is.False);
    }

    [Test]
    public void IsValidValue_SchemeWithNoIdentifier_ReturnsFalse()
    {
        // Create scheme without identifier
        var noIdScheme = new DataScheme("NoIdScheme");
        noIdScheme.AddAttribute(Context, "Name", DataType.Text);
        Schema.LoadDataScheme(Context, noIdScheme, overwriteExisting: true);

        CreateReferenceDataType(Context, "NoIdScheme", "Name").AssertFailed();
    }

    [Test]
    public void IsValidValue_SchemeWithMultipleAttributes_OnlyValidatesIdentifier()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        // "First Item" is a Name value, not an ID value
        var result = refType.IsValidValue(Context, "First Item");
        Assert.That(result.Passed, Is.False);
    }

    #endregion

    #region Default Value Tests

    [Test]
    public void DefaultValue_EmptyScheme_SetsEmptyString()
    {
        // Create empty scheme
        var emptyScheme = new DataScheme("EmptyScheme");
        emptyScheme.AddAttribute(Context, "ID", DataType.Text, isIdentifier: true);
        Schema.LoadDataScheme(Context, emptyScheme, overwriteExisting: true);

        var refType = CreateReferenceDataType(Context, "EmptyScheme", "ID").AssertPassed();
        
        Assert.That(refType.DefaultValue, Is.EqualTo(""));
    }

    [Test]
    public void DefaultValue_WithEntries_SetsToFirstIdentifier()
    {
        var refType = CreateReferenceDataType(Context, REFERENCE_SCHEME_NAME, REFERENCE_ATTRIBUTE_NAME).AssertPassed();
        
        Assert.That(refType.DefaultValue, Is.EqualTo("item1"));
    }

    #endregion
}

