using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataType
{
    private Mock<IFileSystem> _mockFileSystem;
    private const string VALID_FILE_PATH = "valid.json";
    private const string INVALID_FILE_PATH = "invalid.json";
    private const string VALID_SCHEME_NAME = "ValidSchema";
    private const string VALID_SCHEME_NAME_NO_IDENTIFIER = "ValidSchemaNoIdentifier";
    private const string INVALID_SCHEME_NAME = "InvalidSchema";
    private const string VALID_REFERENCE_ATTRIBUTE = "ValidAttribute";
    private const string INVALID_REFERENCE_ATTRIBUTE = "InvalidAttribute";
    private const string VALID_REFERENCE_VALUE = "valid";
    private const string INVALID_REFERENCE_VALUE = "invalid";
    
    [SetUp]
    public void OnTestSetup()
    {
        Schema.Reset();
        _mockFileSystem = new Mock<IFileSystem>();
        Storage.SetFileSystem(_mockFileSystem.Object);

        _mockFileSystem.Setup(m => m.FileExists(VALID_FILE_PATH)).Returns(true);
        _mockFileSystem.Setup(m => m.FileExists(INVALID_FILE_PATH)).Returns(false);

        // pre-load data schemes
        var validScheme = new DataScheme(VALID_SCHEME_NAME);
        validScheme.AddAttribute(new AttributeDefinition(VALID_REFERENCE_ATTRIBUTE, DataType.Text, isIdentifier: true));
        validScheme.AddAttribute(new AttributeDefinition(INVALID_REFERENCE_ATTRIBUTE, DataType.Text));
        validScheme.AddEntry(new DataEntry(new Dictionary<string, object>()
        {
            { VALID_REFERENCE_ATTRIBUTE, VALID_REFERENCE_VALUE }
        }));
        Schema.LoadDataScheme(validScheme, true);
        
        var validSchemaNoIdentifier = new DataScheme(VALID_SCHEME_NAME_NO_IDENTIFIER);
        validSchemaNoIdentifier.AddAttribute(new AttributeDefinition(VALID_REFERENCE_ATTRIBUTE, DataType.Text));
        validSchemaNoIdentifier.AddEntry(new DataEntry(new Dictionary<string, object>()
        {
            { VALID_REFERENCE_ATTRIBUTE, VALID_REFERENCE_VALUE }
        }));
        Schema.LoadDataScheme(validSchemaNoIdentifier, true);
    }
    
    [Test, TestCaseSource(nameof(ConversionTestCases))]
    public void Test_TryToConvertData(object data, DataType fromType, DataType toType, bool expectSuccessResult, object expectedConvertedResult)
    {
        var conversion = DataType.ConvertData(data, fromType, toType);

        conversion.AssertCondition(expectSuccessResult, expectedConvertedResult);
    }

    private static IEnumerable ConversionTestCases
    {
        get
        {
            yield return new TestCaseData("1", DataType.Text, DataType.Integer, true, 1);
            yield return new TestCaseData("", DataType.Text, DataType.Integer, true, 0);
            yield return new TestCaseData("foo", DataType.Text, DataType.Integer, false, null);
            yield return new TestCaseData("foo", DataType.Text, DataType.DateTime, false, null);
            yield return new TestCaseData("", DataType.Text, DataType.DateTime, true, System.DateTime.Today);
            
            // weird unhandled type conversion case
            yield return new TestCaseData(1, DataType.Integer, DataType.DateTime, false, null);

            var testValidDateStr = "2024-01-01 01:02:03";
            var testDate = DateTime.Parse(testValidDateStr);
            yield return new TestCaseData(testValidDateStr, DataType.Text, DataType.DateTime, true, testDate);
            
            yield return new TestCaseData(VALID_FILE_PATH, DataType.Text, DataType.FilePath, true, VALID_FILE_PATH);
            yield return new TestCaseData(INVALID_FILE_PATH, DataType.Text, DataType.FilePath, false, null);
            yield return new TestCaseData(INVALID_FILE_PATH, DataType.Text, DataType.FilePath, false, null);
            
            // Reference type conversions
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                new ReferenceDataType(VALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE), true, VALID_REFERENCE_VALUE);
            
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                new ReferenceDataType(INVALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE), false, null);
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                new ReferenceDataType(VALID_SCHEME_NAME, INVALID_REFERENCE_ATTRIBUTE), false, null);
            yield return new TestCaseData(INVALID_REFERENCE_VALUE, DataType.Text, 
                new ReferenceDataType(VALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE), false, null);
            
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                new ReferenceDataType(VALID_SCHEME_NAME_NO_IDENTIFIER, VALID_REFERENCE_ATTRIBUTE), false, null);

            // Boolean conversions
            yield return new TestCaseData(true, DataType.Boolean, DataType.Boolean, true, true);
            yield return new TestCaseData(false, DataType.Boolean, DataType.Boolean, true, false);
            yield return new TestCaseData("true", DataType.Text, DataType.Boolean, true, true);
            yield return new TestCaseData("false", DataType.Text, DataType.Boolean, true, false);
            yield return new TestCaseData("notabool", DataType.Text, DataType.Boolean, false, null);
            yield return new TestCaseData(1, DataType.Integer, DataType.Boolean, true, true);
            yield return new TestCaseData(0, DataType.Integer, DataType.Boolean, true, false);
            yield return new TestCaseData(2, DataType.Integer, DataType.Boolean, true, true);
        }
    }

    [Test, TestCaseSource(nameof(TypeEqualityTestCases))]
    public void Test_Equality(DataType fromType, DataType toType, bool expectedResult)
    {
        Assert.That(fromType.Equals(toType), Is.EqualTo(expectedResult));
        Assert.That(fromType == toType, Is.EqualTo(expectedResult));
        Assert.That(fromType.GetHashCode().Equals(toType.GetHashCode()), Is.EqualTo(expectedResult));
    }

    private static IEnumerable TypeEqualityTestCases
    {
        get
        {
            yield return new TestCaseData(DataType.Text, new TextDataType(string.Empty), true);
            yield return new TestCaseData(DataType.Text, new TextDataType(null), true);
            yield return new TestCaseData(DataType.DateTime, new DateTimeDataType(), true);
            yield return new TestCaseData(DataType.Integer, new IntegerDataType(), true);
            yield return new TestCaseData(DataType.FilePath, new FilePathDataType(), true);
            yield return new TestCaseData(DataType.Text, DataType.Integer, false);
            yield return new TestCaseData(DataType.Text, new ReferenceDataType("Schema1", "Attribute1"), false);
            yield return new TestCaseData(new ReferenceDataType("Schema1", "Attribute1"), new ReferenceDataType("Schema1", "Attribute1"), true);
            yield return new TestCaseData(new ReferenceDataType("Schema1", "Attribute1"), new ReferenceDataType("Schema2", "Attribute1"), false);
            yield return new TestCaseData(new ReferenceDataType("Schema1", "Attribute1"), new ReferenceDataType("Schema1", "Attribute2"), false);
            yield return new TestCaseData(DataType.Boolean, new BooleanDataType(), true);
            yield return new TestCaseData(DataType.Boolean, DataType.Text, false);
        }
    }

    [Test]
    public void Test_BooleanDataType_CheckIfValidData()
    {
        var boolType = new BooleanDataType();
        Assert.That(boolType.CheckIfValidData(true).Passed, Is.True);
        Assert.That(boolType.CheckIfValidData(false).Passed, Is.True);
        Assert.That(boolType.CheckIfValidData("true").Passed, Is.False);
        Assert.That(boolType.CheckIfValidData(1).Passed, Is.False);
    }
}