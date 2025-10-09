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
    private JsonSchemeStorageFormat _storageFormat;
    
    [SetUp]
    public void OnTestSetup()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        _storageFormat = new JsonSchemeStorageFormat(mockFileSystem.Object);
    }
    
    [Test, TestCaseSource(nameof(JsonSerializationTestCases))]
    public void Test_TryDeserialize(string testDataTypeJsonString, bool expectedSuccess, DataType expectedDataType, bool _, string _2)
    {
        // HACK: For jamming the test data type json string into a Scheme's Attribute
        var schemeJsonStr =
            "{\n  \"SchemeName\" : \"test\",\n  \"Attributes\" : [ {\n    \"AttributeName\" : \"testAttribute\",\n    \"DataType\" : " +
            // "{\n      \"$type\" : \"Schema.Core.Data.TextDataType,Schema.Core\",\n      \"DefaultValue\" : \"\"\n    },\n    " +
            testDataTypeJsonString +
            ",\"IsIdentifier\" : false,\n    \"ShouldPublish\" : true,\n    \"AttributeToolTip\" : \"\",\n    \"ColumnWidth\" : 150,\n    \"DefaultValue\" : \"\"\n  } ],\n  \"Entries\" : [ ]\n}";
        // Test code here
        if (_storageFormat.Deserialize(Context, schemeJsonStr)
            .TryAssertCondition(expectedSuccess, out var data))
        {
            var attribute = data.GetAttribute(0).AssertPassed();
            Assert.That(attribute.DataType, Is.EqualTo(expectedDataType));
        }
    }
    
    [Test, TestCaseSource(nameof(JsonSerializationTestCases))]
    public void Test_Serialize(string? expectedJsonString, bool _, DataType expectedData, bool expectedSuccess, string? altExpectedJsonString)
    {
        var testScheme = new DataScheme("test");
        testScheme.AddAttribute(Context, "testAttribute", expectedData);
        
        // Test code here
        if (_storageFormat.Serialize(Context, testScheme).TryAssertCondition(expectedSuccess, out var jsonString))
        {
            var realExpectedJsonString =
                altExpectedJsonString != null ? altExpectedJsonString : expectedJsonString;

            realExpectedJsonString = realExpectedJsonString.SanitizeWhitespace();
            jsonString = jsonString.SanitizeWhitespace();
            // bool isContained = jsonString.Contains(realExpectedJsonString);
            StringAssert.Contains(realExpectedJsonString, jsonString);
        }
    }

    private static IEnumerable JsonSerializationTestCases
    {
        get
        {
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.TextDataType,Schema.Core\", \"DefaultValue\": \"\"\n}",
                true,
                new TextDataType(),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.FilePathDataType,Schema.Core\", \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": false,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: false),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.FilePathDataType,Schema.Core\", \"AllowEmptyPath\": true,\n  \"UseRelativePaths\": false,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.FilePathDataType,Schema.Core\", \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": false,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: false),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.FilePathDataType,Schema.Core\", \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": true,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: false, useRelativePaths: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.FilePathDataType,Schema.Core\", \"AllowEmptyPath\": true,\n  \"UseRelativePaths\": true,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(allowEmptyPath: true, useRelativePaths: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.FilePathDataType,Schema.Core\", \"AllowEmptyPath\": false,\n  \"UseRelativePaths\": true,\n  \"BasePath\": null,\n  \"DefaultValue\": \"\"\n}",
                true,
                new FilePathDataType(false, useRelativePaths: true),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.IntegerDataType,Schema.Core\", \"DefaultValue\": 0\n}",
                true,
                new IntegerDataType(),
                true,
                null);
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.IntegerDataType,Schema.Core\", \"DefaultValue\": 1\n}",
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
                "{\n  \"$type\": \"Schema.Core.Data.ReferenceDataType, Schema.Core\",\n  \"ReferenceSchemeName\": \"SpellStatus\",\n  \"ReferenceAttributeName\": \"Status\",\n  \"SupportsEmptyReferences\": true,\n  \"DefaultValue\": null\n}");
            yield return new TestCaseData("{\n  \"$type\":\"Schema.Core.Data.ReferenceDataType,Schema.Core\", \"ReferenceSchemeName\": \"SpellStatus\",\n  \"ReferenceAttributeName\": \"Status\",\n  \"SupportsEmptyReferences\": true,\n  \"DefaultValue\": null\n}",
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