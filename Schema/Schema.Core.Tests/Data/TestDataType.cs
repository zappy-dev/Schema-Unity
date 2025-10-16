using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestDataType
{
    private static SchemaContext Context => new()
    {
        Driver = nameof(TestDataType)
    };
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
        TestFixtureSetup.Initialize(Context, out _mockFileSystem, out _);

        Logger.LogVerbose($"Adding mock for FileExists({Context}, {VALID_FILE_PATH}) => Pass");
        Logger.LogVerbose($"Adding mock for FileExists({Context.WithDataType(DataType.FilePath_RelativePaths)}, {VALID_FILE_PATH}) => Pass");
        _mockFileSystem.Setup(m => m.FileExists(Context, VALID_FILE_PATH)).Returns(SchemaResult.Pass());
        _mockFileSystem.Setup(m => m.FileExists(Context, Path.Combine(TestFixtureSetup.ProjectPath, VALID_FILE_PATH))).Returns(SchemaResult.Pass());
        _mockFileSystem.Setup(m => m.FileExists(Context.WithDataType(DataType.FilePath_RelativePaths), VALID_FILE_PATH)).Returns(SchemaResult.Pass());
        _mockFileSystem.Setup(m => m.FileExists(Context.WithDataType(DataType.FilePath_RelativePaths), Path.Combine(TestFixtureSetup.ProjectPath, VALID_FILE_PATH))).Returns(SchemaResult.Pass());
        _mockFileSystem.Setup(m => m.FileExists(Context, INVALID_FILE_PATH)).Returns(SchemaResult.Fail(Context, ""));

        // pre-load data schemes
        var validScheme = new DataScheme(VALID_SCHEME_NAME);
        validScheme.AddAttribute(Context, VALID_REFERENCE_ATTRIBUTE, DataType.Text, isIdentifier: true);
        validScheme.AddAttribute(Context, INVALID_REFERENCE_ATTRIBUTE, DataType.Text);
        validScheme.AddEntry(Context, new DataEntry(new Dictionary<string, object>()
        {
            { VALID_REFERENCE_ATTRIBUTE, VALID_REFERENCE_VALUE }
        }));
        Schema.LoadDataScheme(Context, validScheme, true);
        
        var validSchemaNoIdentifier = new DataScheme(VALID_SCHEME_NAME_NO_IDENTIFIER);
        validSchemaNoIdentifier.AddAttribute(Context, VALID_REFERENCE_ATTRIBUTE, DataType.Text);
        validSchemaNoIdentifier.AddEntry(Context, new DataEntry(new Dictionary<string, object>()
        {
            { VALID_REFERENCE_ATTRIBUTE, VALID_REFERENCE_VALUE }
        }));
        Schema.LoadDataScheme(Context, validSchemaNoIdentifier, true);
    }
    
    [Test, TestCaseSource(nameof(ConversionTestCases))]
    public void Test_TryToConvertData(object data, DataType fromType, DataType toType, bool expectSuccessResult, object expectedConvertedResult)
    {
        var conversion = DataType.ConvertValue(Context, data, fromType, toType);

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

            var testValidDateStr = "2024-01-01 01:02:03Z";
            var testDate = DateTime.Parse(testValidDateStr).ToUniversalTime();
            yield return new TestCaseData(testValidDateStr, DataType.Text, DataType.DateTime, true, testDate);
            
            var testValidDateStr2 = "2024-01-01T01:02:03Z";
            var testDate2 = DateTime.Parse(testValidDateStr2).ToUniversalTime();
            yield return new TestCaseData(testValidDateStr2, DataType.Text, DataType.DateTime, true, testDate2);
            
            yield return new TestCaseData(VALID_FILE_PATH, DataType.Text, DataType.FilePath_RelativePaths, true, VALID_FILE_PATH);
            yield return new TestCaseData(INVALID_FILE_PATH, DataType.Text, DataType.FilePath_RelativePaths, false, null);
            yield return new TestCaseData(INVALID_FILE_PATH, DataType.Text, DataType.FilePath_RelativePaths, false, null);
            
            // Reference type conversions
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, VALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE, validateSchemeLoaded: false).AssertPassed(), true, VALID_REFERENCE_VALUE);
            
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, INVALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE, validateSchemeLoaded: false).AssertPassed(), false, null);
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, VALID_SCHEME_NAME, INVALID_REFERENCE_ATTRIBUTE, validateSchemeLoaded: false).AssertPassed(), false, null);
            yield return new TestCaseData(INVALID_REFERENCE_VALUE, DataType.Text, 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, VALID_SCHEME_NAME, VALID_REFERENCE_ATTRIBUTE, validateSchemeLoaded: false).AssertPassed(), false, null);
            
            yield return new TestCaseData(VALID_REFERENCE_VALUE, DataType.Text, 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, VALID_SCHEME_NAME_NO_IDENTIFIER, VALID_REFERENCE_ATTRIBUTE, validateSchemeLoaded: false).AssertPassed(), false, null);

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
            yield return new TestCaseData(DataType.FilePath_RelativePaths, new FilePathDataType(), true);
            yield return new TestCaseData(DataType.Text, DataType.Integer, false);
            yield return new TestCaseData(DataType.Text, ReferenceDataTypeFactory.CreateReferenceDataType(Context, "Schema1", "Attribute1", validateSchemeLoaded: false).AssertPassed(), false);
            yield return new TestCaseData(ReferenceDataTypeFactory.CreateReferenceDataType(Context, "Schema1", "Attribute1", validateSchemeLoaded: false).AssertPassed(), 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, "Schema1", "Attribute1", validateSchemeLoaded: false).AssertPassed(), true);
            yield return new TestCaseData(ReferenceDataTypeFactory.CreateReferenceDataType(Context, "Schema1", "Attribute1", validateSchemeLoaded: false).AssertPassed(), 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, "Schema2", "Attribute1", validateSchemeLoaded: false).AssertPassed(), false);
            yield return new TestCaseData(ReferenceDataTypeFactory.CreateReferenceDataType(Context, "Schema1", "Attribute1", validateSchemeLoaded: false).AssertPassed(), 
                ReferenceDataTypeFactory.CreateReferenceDataType(Context, "Schema1", "Attribute2", validateSchemeLoaded: false).AssertPassed(), false);
            yield return new TestCaseData(DataType.Boolean, new BooleanDataType(), true);
            yield return new TestCaseData(DataType.Boolean, DataType.Text, false);
        }
    }

    [Test]
    public void Test_BooleanDataType_CheckIfValidData()
    {
        var boolType = new BooleanDataType();
        Assert.That(boolType.IsValidValue(Context, true).Passed, Is.True);
        Assert.That(boolType.IsValidValue(Context, false).Passed, Is.True);
        Assert.That(boolType.IsValidValue(Context, "true").Passed, Is.False);
        Assert.That(boolType.IsValidValue(Context, 1).Passed, Is.False);
    }
}