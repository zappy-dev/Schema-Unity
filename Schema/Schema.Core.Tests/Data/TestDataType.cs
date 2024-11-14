using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;

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
        validScheme.AddAttribute(new AttributeDefinition(VALID_REFERENCE_ATTRIBUTE, DataType.String, isIdentifier: true));
        validScheme.AddAttribute(new AttributeDefinition(INVALID_REFERENCE_ATTRIBUTE, DataType.String));
        validScheme.AddEntry(new DataEntry(new Dictionary<string, object>()
        {
            { VALID_REFERENCE_ATTRIBUTE, VALID_REFERENCE_VALUE }
        }));
        Schema.LoadDataScheme(validScheme, true);
        
        var validSchemaNoIdentifier = new DataScheme(VALID_SCHEME_NAME_NO_IDENTIFIER);
        validSchemaNoIdentifier.AddAttribute(new AttributeDefinition(VALID_REFERENCE_ATTRIBUTE, DataType.String));
        validSchemaNoIdentifier.AddEntry(new DataEntry(new Dictionary<string, object>()
        {
            { VALID_REFERENCE_ATTRIBUTE, VALID_REFERENCE_VALUE }
        }));
        Schema.LoadDataScheme(validSchemaNoIdentifier, true);
    }
    
    [Test, TestCaseSource(nameof(ConversionTestCases))]
    public bool Test_TryToConvertData(object data, DataType fromType, DataType toType, object expectedConvertedResult)
    {
        bool result = DataType.TryToConvertData(data, fromType, toType, out var convertedData);
        
        Assert.That(convertedData, Is.EqualTo(expectedConvertedResult));

        return result;
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
            yield return new TestCaseData(DataType.String, new TextDataType(string.Empty), true);
            yield return new TestCaseData(DataType.String, new TextDataType(null), true);
            yield return new TestCaseData(DataType.DateTime, new DateTimeDataType(), true);
            yield return new TestCaseData(DataType.Integer, new IntegerDataType(), true);
            yield return new TestCaseData(DataType.FilePath, new FilePathDataType(), true);
            yield return new TestCaseData(DataType.String, DataType.Integer, false);
            yield return new TestCaseData(DataType.String, new ReferenceDataType("Schema1", "Attribute1"), false);
            yield return new TestCaseData(new ReferenceDataType("Schema1", "Attribute1"), new ReferenceDataType("Schema1", "Attribute1"), true);
            yield return new TestCaseData(new ReferenceDataType("Schema1", "Attribute1"), new ReferenceDataType("Schema2", "Attribute1"), false);
            yield return new TestCaseData(new ReferenceDataType("Schema1", "Attribute1"), new ReferenceDataType("Schema1", "Attribute2"), false);
        }
    }

    private static IEnumerable ConversionTestCases
    {
        get
        {
            yield return new TestCaseData("1", DataType.String, DataType.Integer, 1).Returns(true);
            yield return new TestCaseData("", DataType.String, DataType.Integer, 0).Returns(true);
            yield return new TestCaseData("foo", DataType.String, DataType.Integer, null).Returns(false);
            yield return new TestCaseData("foo", DataType.String, DataType.DateTime, DateTime.MinValue).Returns(false);
            
            // weird unhandled type conversion case
            yield return new TestCaseData(1, DataType.Integer, DataType.DateTime, null).Returns(false);

            var testValidDateStr = "2024-01-01 01:02:03";
            var testDate = DateTime.Parse(testValidDateStr);
            yield return new TestCaseData(testValidDateStr, DataType.String, DataType.DateTime, testDate).Returns(true);
            
            yield return new TestCaseData(VALID_FILE_PATH, DataType.String, DataType.FilePath, VALID_FILE_PATH).Returns(true);
            yield return new TestCaseData(INVALID_FILE_PATH, DataType.String, DataType.FilePath, null).Returns(false);
            yield return new TestCaseData(INVALID_FILE_PATH, DataType.String, DataType.FilePath, null).Returns(false);
            
            // Reference type conversions
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.String, 
                new ReferenceDataType(VALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE), VALID_REFERENCE_VALUE).Returns(true);
            
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.String, 
                new ReferenceDataType(INVALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE), null).Returns(false);
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.String, 
                new ReferenceDataType(VALID_SCHEME_NAME, INVALID_REFERENCE_ATTRIBUTE), null).Returns(false);
            yield return new TestCaseData(INVALID_REFERENCE_VALUE, DataType.String, 
                new ReferenceDataType(VALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE), null).Returns(false);
            
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.String, 
                new ReferenceDataType(VALID_SCHEME_NAME_NO_IDENTIFIER, VALID_REFERENCE_ATTRIBUTE), null).Returns(false);
        }
    }
}