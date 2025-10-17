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
	private CancellationTokenSource cts = new();

	[SetUp]
	public void Setup()
	{
		_mockFileSystem = new Mock<IFileSystem>();

		Schema.SetStorage(new Storage(_mockFileSystem.Object));
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		cts.Cancel();
		cts.Dispose();
	}

	[Test]
	public void ConvertData_AbsoluteDirectory_To_RelativeDirectory()
	{
		string absoluteDir = "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content/Configs";
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, absoluteDir, cts.Token)).Returns(Task.FromResult(true));

		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/Users/zappy/src/Schema-Unity/SchemaPlayground/Content");

		folderType.IsValidValue(Context, absoluteDir).AssertFailed();
		folderType.ConvertValue(Context, absoluteDir, cts.Token).TryAssert(out var result);

		Assert.That(result, Is.EqualTo("Configs"));
	}

	[Test]
	public void CheckIfValidData_Fails_On_Absolute_When_Relative_Required()
	{
		string absoluteDir = "/abs/path/Content/Configs";
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, absoluteDir, cts.Token)).Returns(Task.FromResult(true));

		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/abs/path/Content");

		folderType.IsValidValue(Context, absoluteDir).AssertFailed();
	}

	[Test]
	public void ConvertData_Allows_Empty_When_Configured()
	{
		var folderType = new FolderDataType(allowEmptyPath: true, useRelativePaths: true,
			basePath: "/base");

		folderType.IsValidValue(Context, string.Empty).AssertPassed();
		folderType.ConvertValue(Context, string.Empty).AssertPassed();
	}

	[Test]
	public void ConvertData_Fails_When_Directory_Does_Not_Exist()
	{
		string relDir = "MissingDir";
		string basePath = "/proj/Content";
		string resolved = PathUtility.MakeAbsolutePath(relDir, basePath);
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, resolved, cts.Token)).Returns(Task.FromResult(false));

		var folderType = new FolderDataType(allowEmptyPath: false, useRelativePaths: true,
			basePath: basePath);

		folderType.ConvertValue(Context, relDir).AssertFailed<object>();
	}

	[Test]
	public void ConvertData_Passes_When_Directory_Exists()
	{
		string relDir = "ExistingDir";
		string basePath = "/proj/Content";
		string resolved = PathUtility.MakeAbsolutePath(relDir, basePath);
		_mockFileSystem.Setup(fs => fs.DirectoryExists(Context, resolved, cts.Token)).Returns(Task.FromResult(true));

		var folderType = new FolderDataType(allowEmptyPath: false, useRelativePaths: true,
			basePath: basePath);

		folderType.ConvertValue(Context, relDir, cts.Token).TryAssert(out var result);
		Assert.That(result, Is.EqualTo(relDir));
	}

	[Test]
	public void CheckIfValidData_Fails_For_NonString()
	{
		var folderType = new FolderDataType();
		folderType.IsValidValue(Context, 123).AssertFailed();
	}
}


