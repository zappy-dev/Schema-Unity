using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestFilePathDataType
{
    private Mock<IFileSystem> _mockFileSystem;
    
    [SetUp]
    public void Setup()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        
        Core.Serialization.Storage.SetFileSystem(_mockFileSystem.Object);
    }
    
    [Test]
    public void ConvertData_AbsoluteFilePath_To_RelativeFilePath()
    {
        string absolutePath = "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content/TODOs.json";
        _mockFileSystem.Setup(fs => fs.FileExists(absolutePath)).Returns(true);
        
        var fileType = new FilePathDataType(allowEmptyPath: true, useRelativePaths: true,
            basePath: "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content");

        fileType.CheckIfValidData(absolutePath, TestFixtureSetup.SchemaTestContext).AssertFailed();
        fileType.ConvertData(absolutePath, TestFixtureSetup.SchemaTestContext).TryAssert(out var result);
        
        Assert.That(result, Is.EqualTo("TODOs.json"));
    }
}