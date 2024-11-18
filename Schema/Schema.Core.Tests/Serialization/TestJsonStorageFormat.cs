using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;

namespace Schema.Core.Tests.Serialization;

[TestFixture]
public class TestJsonStorageFormat
{
    private JsonStorageFormat<DataType> dataTypeStorageFormat;
    
    [SetUp]
    public void OnTestSetup()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        dataTypeStorageFormat = new JsonStorageFormat<DataType>(mockFileSystem.Object);
    }
    
    [Test, TestCaseSource(nameof(JsonSerializationTestCases))]
    public void Test_TryDeserialize(string testJsonString, bool expectedSuccess, object expectedData, bool _)
    {
        // Test code here
        var result = dataTypeStorageFormat.TryDeserialize(testJsonString, out var data);
        Assert.That(result, Is.EqualTo(expectedSuccess));
        Assert.That(data, Is.EqualTo(expectedData));
    }
    
    [Test, TestCaseSource(nameof(JsonSerializationTestCases))]
    public void Test_Serialize(string expectedJsonString, bool _, DataType expectedData, bool expectedSuccess)
    {
        // Test code here
        var jsonString = dataTypeStorageFormat.Serialize(expectedData);
        // Assert.That(result, Is.EqualTo(expectedSuccess));
        if (expectedSuccess)
        {
            Assert.That(jsonString, Is.EqualTo(expectedJsonString));
        }
        else
        {
            Assert.That(jsonString, Is.Not.EqualTo(expectedJsonString));
        }
    }

    private static IEnumerable JsonSerializationTestCases
    {
        get
        {
            yield return new TestCaseData("{\n  \"TypeName\": \"String\",\n  \"DefaultValue\": \"\"\n}",
                true,
                new TextDataType(),
                true);
            yield return new TestCaseData("{\n  \"TypeName\": \"FilePath\",\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(false),
                false);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": true,\"TypeName\": \"FilePath\",\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(true),
                false);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": false,\"TypeName\": \"FilePath\",\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(false),
                false);
            yield return new TestCaseData("{\n  \"TypeName\": \"Integer\",\n  \"DefaultValue\": 0\n}",
                true,
                new IntegerDataType(),
                true);
            yield return new TestCaseData("{\n  \"TypeName\": \"Integer\",\n  \"DefaultValue\": 1\n}",
                true,
                new IntegerDataType(1),
                true);
            // TODO: Figure out testing for different time zones on different devices
            // yield return new TestCaseData("{\n  \"TypeName\": \"Date Time\",\n  \"DefaultValue\": \""\"\n}",
            //     true,
            //     new DateTimeDataType(DateTime.MinValue),
            //     true);
            // handle old serialization format
            yield return new TestCaseData("{\n  \"$type\": \"Schema.Core.Data.ReferenceDataType, Schema.Core\",\n  \"ReferenceSchemeName\": \"SpellStatus\",\n \"ReferenceAttributeName\": \"Status\",\n   \"SupportsEmptyReferences\": true,\n    \"TypeName\": \"Reference/SpellStatus - Status\",\n \"DefaultValue\": null\n}",
                true,
                new ReferenceDataType("SpellStatus", "Status"),
                false);
            yield return new TestCaseData("{\n  \"ReferenceSchemeName\": \"SpellStatus\",\n  \"ReferenceAttributeName\": \"Status\",\n  \"SupportsEmptyReferences\": true,\n  \"TypeName\": \"Reference/SpellStatus - Status\",\n  \"DefaultValue\": null\n}",
                true,
                new ReferenceDataType("SpellStatus", "Status"),
                true);
        }
    }
}