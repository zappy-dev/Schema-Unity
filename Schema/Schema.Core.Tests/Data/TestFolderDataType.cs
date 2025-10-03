using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestFolderDataType
{
	private static SchemaContext Context = new SchemaContext
	{
		Driver = nameof(TestFolderDataType)
	};
	private Mock<IFileSystem> _mockFileSystem;

	[SetUp]
	public void Setup()
	{
		_mockFileSystem = new Mock<IFileSystem>();

		Schema.SetStorage(new Storage(_mockFileSystem.Object));
	}

	[Test]
	public void ConvertData_AbsoluteDirectory_To_RelativeDirectory()
	{
		string absoluteDir = "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content/Configs";
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, absoluteDir)).Returns(true);

		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content");

		folderType.CheckIfValidData(Context, absoluteDir).AssertFailed();
		folderType.ConvertData(Context, absoluteDir).TryAssert(out var result);

		Assert.That(result, Is.EqualTo("Configs"));
	}

	[Test]
	public void CheckIfValidData_Fails_On_Absolute_When_Relative_Required()
	{
		string absoluteDir = "/abs/path/Content/Configs";
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, absoluteDir)).Returns(true);

		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/abs/path/Content");

		folderType.CheckIfValidData(Context, absoluteDir).AssertFailed();
	}

	[Test]
	public void ConvertData_Allows_Empty_When_Configured()
	{
		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/base");

		folderType.CheckIfValidData(Context, string.Empty).AssertPassed();
		folderType.ConvertData(Context, string.Empty).AssertPassed();
	}

	[Test]
	public void ConvertData_Fails_When_Directory_Does_Not_Exist()
	{
		string relDir = "MissingDir";
		string basePath = "/proj/Content";
		string resolved = PathUtility.MakeAbsolutePath(relDir, basePath);
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, resolved)).Returns(false);

		var folderType = new FolderDataType(allowEmptyPath: false, useRelativePaths: true,
			basePath: basePath);

		folderType.ConvertData(Context, relDir).AssertFailed<object>();
	}

	[Test]
	public void ConvertData_Passes_When_Directory_Exists()
	{
		string relDir = "ExistingDir";
		string basePath = "/proj/Content";
		string resolved = PathUtility.MakeAbsolutePath(relDir, basePath);
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, resolved)).Returns(true);

		var folderType = new FolderDataType(allowEmptyPath: false, useRelativePaths: true,
			basePath: basePath);

		folderType.ConvertData(Context, relDir).TryAssert(out var result);
		Assert.That(result, Is.EqualTo(relDir));
	}

	[Test]
	public void CheckIfValidData_Fails_For_NonString()
	{
		var folderType = new FolderDataType();
		folderType.CheckIfValidData(Context, 123).AssertFailed();
	}
}


