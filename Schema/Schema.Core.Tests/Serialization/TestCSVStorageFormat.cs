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
    private DataScheme testScheme;

    [SetUp]
    public void OnTestSetup()
    {
        mockFileSystem = new Mock<IFileSystem>();
        storageFormat = new CSVStorageFormat(mockFileSystem.Object);

        testScheme = new DataScheme("Test");
        testScheme.AddAttribute("StringField", DataType.Text);
        testScheme.AddAttribute("IntField", DataType.Integer);
        testScheme.AddEntry(new DataEntry
        {
            {"StringField", "StringData"},
            {"IntField", 123},
        });
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
        storageFormat.SerializeToFile(schemeFilePath, testScheme);

        var expectedCsvString = """
        StringField,IntField
        StringData,123
        
        """;
        
        mockFileSystem.Verify(fs => fs.WriteAllText(schemeFilePath, expectedCsvString), Times.Once);
        mockFileSystem.VerifyAll();
        mockFileSystem.VerifyNoOtherCalls();
    }

    [Test]
    public void Test_LoadFromRows()
    {
        var schemeFilePath = "Test.csv";

        var csvContent = storageFormat.Serialize(testScheme).AssertPassed();
        Console.WriteLine(csvContent);
        var csvLines = CSVStorageFormat.SplitToRows(csvContent);
        mockFileSystem.Setup(fs => fs.ReadAllLines(schemeFilePath))
            .Returns(SchemaResult<string[]>.Pass(csvLines)).Verifiable();

        storageFormat.DeserializeFromFile(schemeFilePath).TryAssert(out DataScheme loadedScheme);
        
        Assert.That(loadedScheme, Is.EqualTo(testScheme));
        mockFileSystem.VerifyAll();
        mockFileSystem.VerifyNoOtherCalls();
    }
}