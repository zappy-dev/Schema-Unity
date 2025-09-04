using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Serialization;

[TestFixture]
public class TestJsonStorageFormat_DataEntry
{
    private IStorageFormat<DataEntry> storageFormat;
    
    [SetUp]
    public void OnTestSetup()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        storageFormat = new JsonStorageFormat<DataEntry>(mockFileSystem.Object);
    }
    
    [Test, TestCaseSource(nameof(DataEntryTestCases))]
    public void Test_TryDeserialize_DataEntry(string testJsonString, bool expectedSuccess, object expectedData, bool _, string _2)
    {
        // Test code here
        if (storageFormat.Deserialize(testJsonString)
            .TryAssertCondition(expectedSuccess, out var data))
        {
            Assert.That(data, Is.EqualTo(expectedData));
        }
    }
    
    [Test, TestCaseSource(nameof(DataEntryTestCases))]
    public void Test_Serialize_DataEntry(string? expectedJsonString, bool _, DataEntry expectedData, bool expectedSuccess, string? altExpectedJsonString)
    {
        // Test code here

        if (storageFormat.Serialize(expectedData).TryAssertCondition(expectedSuccess, out var jsonString))
        {
            var realExpectedJsonString =
                altExpectedJsonString != null ? altExpectedJsonString : expectedJsonString;

            realExpectedJsonString = realExpectedJsonString.ReplaceLineEndings();
            Assert.That(jsonString, Is.EqualTo(realExpectedJsonString));
        }
    }

    private static IEnumerable DataEntryTestCases
    {
        get
        {
            yield return new TestCaseData("{\n  \"ID\": 1,\n  \"Name\": \"Test Entry 0001\",\n  \"Description\": \"This is a test description for entry 1. It contains some text to make it longer and more realistic.\",\n  \"IsActive\": true,\n  \"CreatedDate\": \"2025-09-02T00:14:49.4347425-04:00\",\n  \"Value\": 550\n}",
                true,
                new DataEntry(new Dictionary<string, object>
                {
                    {"ID", 1},
                    {"Name", "Test Entry 0001"},
                    {"Description", "This is a test description for entry 1. It contains some text to make it longer and more realistic."},
                    {"IsActive", true},
                    {"CreatedDate", DateTime.Parse("2025-09-02T00:14:49.4347425-04:00")},
                    {"Value", 550}
                }),
                true,
                null);
        }
    }
}