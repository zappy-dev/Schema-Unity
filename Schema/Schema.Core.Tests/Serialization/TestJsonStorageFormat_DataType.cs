using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Serialization;

[TestFixture]
public class TestJsonStorageFormat_DataType
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestJsonStorageFormat_DataType)
    };
    private JsonStorageFormat<DataType> dataTypeStorageFormat;
    
    [SetUp]
    public void OnTestSetup()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        dataTypeStorageFormat = new JsonStorageFormat<DataType>(mockFileSystem.Object);
    }
    
    [Test, TestCaseSource(nameof(JsonSerializationTestCases))]
    public void Test_TryDeserialize(string testJsonString, bool expectedSuccess, object expectedData, bool _, string _2)
    {
        // Test code here
        if (dataTypeStorageFormat.Deserialize(Context, testJsonString)
            .TryAssertCondition(expectedSuccess, out var data))
        {
            Assert.That(data, Is.EqualTo(expectedData));
        }
    }
    
    [Test, TestCaseSource(nameof(JsonSerializationTestCases))]
    public void Test_Serialize(string? expectedJsonString, bool _, DataType expectedData, bool expectedSuccess, string? altExpectedJsonString)
    {
        // Test code here
        if (dataTypeStorageFormat.Serialize(expectedData).TryAssertCondition(expectedSuccess, out var jsonString))
        {
            var realExpectedJsonString =
                altExpectedJsonString != null ? altExpectedJsonString : expectedJsonString;

            realExpectedJsonString = realExpectedJsonString.ReplaceLineEndings();
            Assert.That(jsonString, Is.EqualTo(realExpectedJsonString));
        }
    }

    private static IEnumerable JsonSerializationTestCases
    {
        get
        {
            yield return new TestCaseData("{\n  \"DefaultValue\": \"\"\n}",
                true,
                new TextDataType(),
                true,
                null);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": false,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: false),
                true,
                null);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": true,\n  \"UseRelativePaths\": false,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": false,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: false),
                true,
                null);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": true,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: false, useRelativePaths: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": true,\n  \"UseRelativePaths\": true,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: true, useRelativePaths: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": true,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(false, useRelativePaths: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"DefaultValue\": 0\n}",
                true,
                new IntegerDataType(),
                true,
                null);
            yield return new TestCaseData("{\n  \"DefaultValue\": 1\n}",
                true,
                new IntegerDataType(1),
                true,
                null);
            // TODO: Figure out testing for different time zones on different devices
            // yield return new TestCaseData("{\n  \"TypeName\": \"Date Time\",\n  \"DefaultValue\": \""\"\n}",
            //     true,
            //     new DateTimeDataType(DateTime.MinValue),
            //     true);
            // handle old serialization format
            yield return new TestCaseData("{\n  \"$type\": \"Schema.Core.Data.ReferenceDataType, Schema.Core\",\n  \"ReferenceSchemeName\": \"SpellStatus\",\n \"ReferenceAttributeName\": \"Status\",\n   \"SupportsEmptyReferences\": true,\n \"DefaultValue\": null\n}",
                true,
                new ReferenceDataType("SpellStatus", "Status"),
                true,
                "{\n  \"ReferenceSchemeName\": \"SpellStatus\",\n  \"ReferenceAttributeName\": \"Status\",\n  \"SupportsEmptyReferences\": true,\n  \"DefaultValue\": null\n}");
            yield return new TestCaseData("{\n  \"ReferenceSchemeName\": \"SpellStatus\",\n  \"ReferenceAttributeName\": \"Status\",\n  \"SupportsEmptyReferences\": true,\n  \"DefaultValue\": null\n}",
                true,
                new ReferenceDataType("SpellStatus", "Status"),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\": \"Schema.Runtime.Type.UnityAssetDataType, Schema.Runtime\",\n  \"ObjectType\": \"UnityEngine.Texture, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\",\n  \"DefaultValue\": null\n}",
                true,
                new PluginDataType("Schema.Runtime.Type.UnityAssetDataType, Schema.Runtime",
                new (
                )
                {
                    {"ObjectType", "UnityEngine.Texture, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"},
                    {"DefaultValue", null},
                }),
                true,
                null);
        }
    }
}