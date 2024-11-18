using System.Collections;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Serialization;

[TestFixture]
public class TestCSVStorageFormat
{
    private Mock<IFileSystem> mockFileSystem;
    private IStorageFormat<DataScheme> storageFormat;

    [SetUp]
    public void OnTestSetup()
    {
        mockFileSystem = new Mock<IFileSystem>();
        storageFormat = new CSVStorageFormat(mockFileSystem.Object);
    }

    [Test]
    [TestCaseSource(nameof(BadSchemeTestCases))]
    public void Test_SerializeToFile_BadCase(DataScheme scheme)
    {
        storageFormat.SerializeToFile("test.csv", scheme).AssertFailed();
    }

    private static IEnumerable BadSchemeTestCases
    {
        get
        {
            yield return new TestCaseData(null);
            yield return new TestCaseData(new DataScheme());
            var testScheme = new DataScheme("Test");
            yield return new TestCaseData(testScheme);

            testScheme.AddEntry(new DataEntry());
            yield return new TestCaseData(testScheme);
        }
    }

    [Test]
    public void Test_SerializeToFile_Small()
    {
        var schemeFilePath = "Test.csv";
        var dataScheme = new DataScheme("Test");
        dataScheme.AddAttribute(new AttributeDefinition("StringField", DataType.Text));
        dataScheme.AddAttribute(new AttributeDefinition("IntField", DataType.Integer));
        dataScheme.AddEntry(new DataEntry
        {
            {"StringField", "StringData"},
            {"IntField", 123},
        });
        
        storageFormat.SerializeToFile(schemeFilePath, dataScheme);

        var expectedCsvString = """
        StringField,IntField
        StringData,123
        
        """;
        
        mockFileSystem.Verify(fs => fs.WriteAllText(schemeFilePath, expectedCsvString), Times.Once);
        mockFileSystem.VerifyNoOtherCalls();
    }
}