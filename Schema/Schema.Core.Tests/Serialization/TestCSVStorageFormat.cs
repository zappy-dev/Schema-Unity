using System.Collections;
using System.Text;
using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Serialization;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Serialization;

[TestFixture]
public class TestCSVStorageFormat
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestCSVStorageFormat)
    };
    private Mock<IFileSystem> mockFileSystem;
    private ISchemeStorageFormat storageFormat;
    private DataScheme testScheme;
    private CancellationTokenSource cts = new();

    [SetUp]
    public void OnTestSetup()
    {
        mockFileSystem = new Mock<IFileSystem>();
        storageFormat = new CsvSchemeStorageFormat(mockFileSystem.Object);

        testScheme = new DataScheme("Test");
        testScheme.AddAttribute(Context, "StringField", DataType.Text);
        testScheme.AddAttribute(Context, "IntField", DataType.Integer);
        testScheme.AddEntry(Context, new DataEntry
        {
            {"StringField", "StringData", Context},
            {"IntField", 123, Context},
        });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        cts.Cancel();
        cts.Dispose();
    }

    [Test]
    [TestCaseSource(nameof(BadSchemeTestCases))]
    public async Task Test_SerializeToFile_BadCase(DataScheme scheme)
    {
        (await storageFormat.SerializeToFile(Context, "test.csv", scheme, cts.Token)).AssertFailed();
    }

    private static IEnumerable BadSchemeTestCases
    {
        get
        {
            yield return new TestCaseData(null);
            yield return new TestCaseData(new DataScheme());
            var testScheme = new DataScheme("Test");
            yield return new TestCaseData(testScheme);

            testScheme.AddEntry(Context, new DataEntry());
            yield return new TestCaseData(testScheme);
        }
    }

    [Test]
    public async Task Test_SerializeToFile_Small()
    {
        var schemeFilePath = "Test.csv";
        (await storageFormat.SerializeToFile(Context, schemeFilePath, testScheme, cts.Token)).AssertPassed();

        var expectedCsvString = """
        StringField,IntField
        StringData,123
        
        """;
        
        mockFileSystem.Verify(fs => fs.WriteAllText(Context, schemeFilePath, expectedCsvString, cts.Token), Times.Once);
        mockFileSystem.VerifyAll();
        mockFileSystem.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Test_LoadFromRows()
    {
        var schemeFilePath = "Test.csv";

        var csvContent = storageFormat.Serialize(Context, testScheme).AssertPassed();
        Logger.Log(csvContent);
        var csvLines = CsvSchemeStorageFormat.SplitToRows(csvContent);
        mockFileSystem.Setup(fs => fs.ReadAllLines(Context, schemeFilePath, cts.Token))
            .Returns(Task.FromResult(SchemaResult<string[]>.Pass(csvLines))).Verifiable();

        (await storageFormat.DeserializeFromFile(Context, schemeFilePath, cts.Token))
            .TryAssert(out DataScheme loadedScheme);
        
        Assert.That(loadedScheme, Is.EqualTo(testScheme), () =>
        {
            var diffReport = new StringBuilder();
            DataScheme.BuildAttributeDiffReport(Context, diffReport, testScheme, loadedScheme);
            return diffReport.ToString();
        });
        mockFileSystem.VerifyAll();
        mockFileSystem.VerifyNoOtherCalls();
    }
}