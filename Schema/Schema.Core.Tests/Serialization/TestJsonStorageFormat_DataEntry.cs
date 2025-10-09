using System.Collections;
using System.Text;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Serialization;

[TestFixture]
public class TestJsonStorageFormat_DataEntry
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestJsonStorageFormat_DataEntry)
    };
    private ISchemeStorageFormat storageFormat;
    
    [SetUp]
    public void OnTestSetup()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        storageFormat = new JsonSchemeStorageFormat(mockFileSystem.Object);
    }
    
    [Test, TestCaseSource(nameof(DataEntryTestCases))]
    public void Test_SerializeDeserialize_DataEntry(string testDataEntryJsonString, bool expectedSuccess, DataEntry expectedData, bool _, string _2)
    {
        var testScheme = new DataScheme("test");
        foreach (var (attr, value) in expectedData)
        {
            var inferredDataType = DataType.InferDataTypeForValues(Context, value).AssertPassed();
            
            testScheme.AddAttribute(Context, attr, inferredDataType).AssertPassed();
            TestContext.WriteLine($"Inferred data type: {inferredDataType} for attribute: {attr}");
        }
        testScheme.AddEntry(Context, expectedData).AssertPassed();
        var expectedSchemeJsonString = storageFormat.Serialize(testScheme).AssertPassed();
        
        StringAssert.Contains(testDataEntryJsonString.SanitizeWhitespace(), expectedSchemeJsonString.SanitizeWhitespace());
        
        // Test code here
        var outputScheme = storageFormat.Deserialize(Context, expectedSchemeJsonString).AssertPassed();
        var outputEntry = outputScheme.GetEntry(0);
        Assert.That(outputEntry, Is.EqualTo(expectedData), () =>
        {
            var diffReport = new StringBuilder();
            DataEntry.BuildDiffReport(Context, diffReport, expectedData, outputEntry);
            return diffReport.ToString();
        });
    }
    
    [Test, TestCaseSource(nameof(DataEntryTestCases))]
    public void Test_Serialize_DataEntry(string? expectedJsonString, bool _, DataEntry expectedData, bool expectedSuccess, string? altExpectedJsonString)
    {
        // Test code here
        var testScheme = new DataScheme("test");
        foreach (var (attr, value) in expectedData)
        {
            var inferredDataType = DataType.InferDataTypeForValues(Context, value).AssertPassed();
            
            testScheme.AddAttribute(Context, attr, inferredDataType).AssertPassed();
            TestContext.WriteLine($"Inferred data type: {inferredDataType} for attribute: {attr}");
        }

        testScheme.AddEntry(Context, expectedData).AssertPassed();

        if (storageFormat.Serialize(testScheme).TryAssertCondition(expectedSuccess, out var jsonString))
        {
            var realExpectedJsonString =
                altExpectedJsonString != null ? altExpectedJsonString : expectedJsonString;

            realExpectedJsonString = realExpectedJsonString.SanitizeWhitespace();
            jsonString = jsonString.SanitizeWhitespace();
            StringAssert.Contains(realExpectedJsonString, jsonString);
        }
    }

    private static IEnumerable DataEntryTestCases
    {
        get
        {
            yield return new TestCaseData("{\n  \"ID\": 1,\n  " +
                                          "\"Name\": \"Test Entry 0001\",\n  " +
                                          "\"Description\": \"This is a test description for entry 1. It contains some text to make it longer and more realistic.\",\n  " +
                                          "\"IsActive\": true,\n  " +
                                          "\"CreatedDate\": \"2025-09-02T04:14:49.4347425Z\",\n  " +
                                          "\"Value\": 550\n}",
                true,
                new DataEntry(new Dictionary<string, object>
                {
                    {"ID", 1},
                    {"Name", "Test Entry 0001"},
                    {"Description", "This is a test description for entry 1. It contains some text to make it longer and more realistic."},
                    {"IsActive", true},
                    {"CreatedDate", DateTime.Parse("2025-09-02T00:14:49.4347425-04:00").ToUniversalTime()},
                    {"Value", 550}
                }),
                true,
                null);
        }
    }
}