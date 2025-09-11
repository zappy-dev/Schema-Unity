using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Data;

[TestFixture]
public class TestFolderDataType
{
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
		_mockFileSystem.Setup(fs => fs.DirectoryExists(absoluteDir)).Returns(SchemaResult.Pass());

		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content");

		folderType.CheckIfValidData(absoluteDir, TestFixtureSetup.SchemaTestContext).AssertFailed();
		folderType.ConvertData(absoluteDir, TestFixtureSetup.SchemaTestContext).TryAssert(out var result);

		Assert.That(result, Is.EqualTo("Configs"));
	}

	[Test]
	public void CheckIfValidData_Fails_On_Absolute_When_Relative_Required()
	{
		string absoluteDir = "/abs/path/Content/Configs";
		_mockFileSystem.Setup(fs => fs.DirectoryExists(absoluteDir)).Returns(SchemaResult.Pass());

		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/abs/path/Content");

		folderType.CheckIfValidData(absoluteDir, TestFixtureSetup.SchemaTestContext).AssertFailed();
	}

	[Test]
	public void ConvertData_Allows_Empty_When_Configured()
	{
		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/base");

		folderType.CheckIfValidData(string.Empty, TestFixtureSetup.SchemaTestContext).AssertPassed();
		folderType.ConvertData(string.Empty, TestFixtureSetup.SchemaTestContext).AssertPassed();
	}

	[Test]
	public void ConvertData_Fails_When_Directory_Does_Not_Exist()
	{
		string relDir = "MissingDir";
		string basePath = "/proj/Content";
		string resolved = PathUtility.MakeAbsolutePath(relDir, basePath);
		_mockFileSystem.Setup(fs => fs.DirectoryExists(resolved)).Returns(SchemaResult.Fail("nope"));

		var folderType = new FolderDataType(allowEmptyPath: false, useRelativePaths: true,
			basePath: basePath);

		folderType.ConvertData(relDir, TestFixtureSetup.SchemaTestContext).AssertFailed<object>();
	}

	[Test]
	public void ConvertData_Passes_When_Directory_Exists()
	{
		string relDir = "ExistingDir";
		string basePath = "/proj/Content";
		string resolved = PathUtility.MakeAbsolutePath(relDir, basePath);
		_mockFileSystem.Setup(fs => fs.DirectoryExists(resolved)).Returns(SchemaResult.Pass());

		var folderType = new FolderDataType(allowEmptyPath: false, useRelativePaths: true,
			basePath: basePath);

		folderType.ConvertData(relDir, TestFixtureSetup.SchemaTestContext).TryAssert(out var result);
		Assert.That(result, Is.EqualTo(relDir));
	}

	[Test]
	public void CheckIfValidData_Fails_For_NonString()
	{
		var folderType = new FolderDataType();
		folderType.CheckIfValidData(123, TestFixtureSetup.SchemaTestContext).AssertFailed();
	}
}


