using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestFilePathDataType
{
    private static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestFilePathDataType)
    };
    private Mock<IFileSystem> _mockFileSystem;
    
    [SetUp]
    public void Setup()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        
        Schema.SetStorage(new Storage(_mockFileSystem.Object));
    }
    
    [Test]
    public void ConvertData_AbsoluteFilePath_To_RelativeFilePath()
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        
        string absolutePath = "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content/TODOs.json";
        _mockFileSystem.Setup(fs => fs.FileExists(Context, absolutePath, cts.Token)).Returns(Task.FromResult(SchemaResult.Pass()));
        
        var fileType = new FilePathDataType(allowEmptyPath: true, useRelativePaths: true,
            basePath: "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content");

        fileType.IsValidValue(Context, absolutePath).AssertFailed();
        fileType.ConvertValue(Context, absolutePath, cts.Token).TryAssert(out var result);
        
        Assert.That(result, Is.EqualTo("TODOs.json"));
    }
}