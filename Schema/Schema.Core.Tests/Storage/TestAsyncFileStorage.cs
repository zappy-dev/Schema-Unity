using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Core.Storage;

namespace Schema.Core.Tests.Storage
{
    [TestFixture]
    public class TestAsyncFileStorage
    {
        private Mock<IFileSystem> _mockFileSystem;
        private Mock<IStorageFormat<DataScheme>> _mockStorageFormat;
        private AsyncFileStorage _asyncFileStorage;
        private string _testFilePath;
        private string _testDirectoryPath;
        private DataScheme _testDataScheme;

        [SetUp]
        public void Setup()
        {
            _mockFileSystem = new Mock<IFileSystem>();
            _mockStorageFormat = new Mock<IStorageFormat<DataScheme>>();
            _asyncFileStorage = new AsyncFileStorage(_mockStorageFormat.Object, _mockFileSystem.Object);
            
            _testFilePath = Path.Combine(Path.GetTempPath(), "test_file.json");
            _testDirectoryPath = Path.Combine(Path.GetTempPath(), "test_directory");
            _testDataScheme = new DataScheme("TestScheme");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any test files that might have been created
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            
            if (Directory.Exists(_testDirectoryPath))
            {
                Directory.Delete(_testDirectoryPath, true);
            }
        }

        [Test]
        public async Task FileExistsAsync_FileExists_ReturnsTrue()
        {
            // Arrange
            _mockFileSystem.Setup(fs => fs.FileExists(_testFilePath)).Returns(true);

            // Act
            var result = await _asyncFileStorage.FileExistsAsync(_testFilePath);

            // Assert
            Assert.IsTrue(result);
            _mockFileSystem.Verify(fs => fs.FileExists(_testFilePath), Times.Once);
        }

        [Test]
        public async Task FileExistsAsync_FileDoesNotExist_ReturnsFalse()
        {
            // Arrange
            _mockFileSystem.Setup(fs => fs.FileExists(_testFilePath)).Returns(false);

            // Act
            var result = await _asyncFileStorage.FileExistsAsync(_testFilePath);

            // Assert
            Assert.IsFalse(result);
            _mockFileSystem.Verify(fs => fs.FileExists(_testFilePath), Times.Once);
        }

        [Test]
        public void FileExistsAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.FileExistsAsync(_testFilePath, cancellationTokenSource.Token));
        }

        [Test]
        public async Task DeserializeFromFileAsync_ValidData_ReturnsDeserializedData()
        {
            // Arrange
            var expectedScheme = new DataScheme("ExpectedScheme");
            var mockResult = SchemaResult<DataScheme>.Pass(expectedScheme, "Success", this);
            
            _mockStorageFormat.Setup(sf => sf.DeserializeFromFile(_testFilePath))
                .Returns(mockResult);

            // Act
            var result = await _asyncFileStorage.DeserializeFromFileAsync<DataScheme>(_testFilePath);

            // Assert
            Assert.That(result, Is.EqualTo(expectedScheme));
            _mockStorageFormat.Verify(sf => sf.DeserializeFromFile(_testFilePath), Times.Once);
        }

        [Test]
        public void DeserializeFromFileAsync_DeserializationFails_ThrowsInvalidOperationException()
        {
            // Arrange
            var mockResult = SchemaResult<DataScheme>.Fail("Deserialization failed", this);
            
            _mockStorageFormat.Setup(sf => sf.DeserializeFromFile(_testFilePath))
                .Returns(mockResult);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _asyncFileStorage.DeserializeFromFileAsync<DataScheme>(_testFilePath));
        }

        [Test]
        public void DeserializeFromFileAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.DeserializeFromFileAsync<DataScheme>(_testFilePath, cancellationTokenSource.Token));
        }

        [Test]
        public async Task SerializeToFileAsync_ValidDataScheme_Succeeds()
        {
            // Arrange
            var mockResult = SchemaResult.Pass("Serialization successful");
            
            _mockStorageFormat.Setup(sf => sf.SerializeToFile(_testFilePath, _testDataScheme))
                .Returns(mockResult);

            // Act
            await _asyncFileStorage.SerializeToFileAsync(_testFilePath, _testDataScheme);

            // Assert
            _mockStorageFormat.Verify(sf => sf.SerializeToFile(_testFilePath, _testDataScheme), Times.Once);
        }

        [Test]
        public void SerializeToFileAsync_SerializationFails_ThrowsInvalidOperationException()
        {
            // Arrange
            var mockResult = SchemaResult.Fail("Serialization failed");
            
            _mockStorageFormat.Setup(sf => sf.SerializeToFile(_testFilePath, _testDataScheme))
                .Returns(mockResult);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _asyncFileStorage.SerializeToFileAsync(_testFilePath, _testDataScheme));
        }

        [Test]
        public void SerializeToFileAsync_NonDataSchemeType_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonDataSchemeData = "not a DataScheme";

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _asyncFileStorage.SerializeToFileAsync(_testFilePath, nonDataSchemeData));
        }

        [Test]
        public void SerializeToFileAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.SerializeToFileAsync(_testFilePath, _testDataScheme, cancellationTokenSource.Token));
        }

        [Test]
        public async Task DeleteFileAsync_FileExists_DeletesFile()
        {
            // Arrange
            _mockFileSystem.Setup(fs => fs.FileExists(_testFilePath)).Returns(true);
            
            // Create a temporary file to actually delete
            File.WriteAllText(_testFilePath, "test content");

            // Act
            await _asyncFileStorage.DeleteFileAsync(_testFilePath);

            // Assert
            Assert.IsFalse(File.Exists(_testFilePath));
            _mockFileSystem.Verify(fs => fs.FileExists(_testFilePath), Times.Once);
        }

        [Test]
        public async Task DeleteFileAsync_FileDoesNotExist_DoesNothing()
        {
            // Arrange
            _mockFileSystem.Setup(fs => fs.FileExists(_testFilePath)).Returns(false);

            // Act
            await _asyncFileStorage.DeleteFileAsync(_testFilePath);

            // Assert
            _mockFileSystem.Verify(fs => fs.FileExists(_testFilePath), Times.Once);
        }

        [Test]
        public void DeleteFileAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.DeleteFileAsync(_testFilePath, cancellationTokenSource.Token));
        }

        [Test]
        public async Task ReadAllTextAsync_FileExists_ReturnsContent()
        {
            // Arrange
            var expectedContent = "test file content";
            File.WriteAllText(_testFilePath, expectedContent);

            // Act
            var result = await _asyncFileStorage.ReadAllTextAsync(_testFilePath);

            // Assert
            Assert.That(result, Is.EqualTo(expectedContent));
        }

        [Test]
        public void ReadAllTextAsync_FileDoesNotExist_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _asyncFileStorage.ReadAllTextAsync(_testFilePath));
        }

        [Test]
        public void ReadAllTextAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.ReadAllTextAsync(_testFilePath, cancellationTokenSource.Token));
        }

        [Test]
        public async Task WriteAllTextAsync_ValidContent_WritesToFile()
        {
            // Arrange
            var content = "test content to write";

            // Act
            await _asyncFileStorage.WriteAllTextAsync(_testFilePath, content);

            // Assert
            Assert.IsTrue(File.Exists(_testFilePath));
            var writtenContent = File.ReadAllText(_testFilePath);
            Assert.That(writtenContent, Is.EqualTo(content));
        }

        [Test]
        public async Task WriteAllTextAsync_DirectoryDoesNotExist_CreatesDirectoryAndWritesFile()
        {
            // Arrange
            var directoryPath = Path.GetDirectoryName(_testFilePath);
            var content = "test content";

            // Act
            await _asyncFileStorage.WriteAllTextAsync(_testFilePath, content);

            // Assert
            Assert.IsTrue(Directory.Exists(directoryPath));
            Assert.IsTrue(File.Exists(_testFilePath));
            var writtenContent = File.ReadAllText(_testFilePath);
            Assert.That(writtenContent, Is.EqualTo(content));
        }

        [Test]
        public void WriteAllTextAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.WriteAllTextAsync(_testFilePath, "content", cancellationTokenSource.Token));
        }

        [Test]
        public async Task GetFileInfoAsync_FileExists_ReturnsFileInfo()
        {
            // Arrange
            File.WriteAllText(_testFilePath, "test content");

            // Act
            var result = await _asyncFileStorage.GetFileInfoAsync(_testFilePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.FullName, Is.EqualTo(Path.GetFullPath(_testFilePath)));
            Assert.IsTrue(result.Exists);
        }

        [Test]
        public async Task GetFileInfoAsync_FileDoesNotExist_ReturnsFileInfoForNonExistentFile()
        {
            // Act
            var result = await _asyncFileStorage.GetFileInfoAsync(_testFilePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.FullName, Is.EqualTo(Path.GetFullPath(_testFilePath)));
            Assert.IsFalse(result.Exists);
        }

        [Test]
        public void GetFileInfoAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.GetFileInfoAsync(_testFilePath, cancellationTokenSource.Token));
        }

        [Test]
        public async Task CreateDirectoryAsync_DirectoryDoesNotExist_CreatesDirectory()
        {
            // Act
            await _asyncFileStorage.CreateDirectoryAsync(_testDirectoryPath);

            // Assert
            Assert.IsTrue(Directory.Exists(_testDirectoryPath));
        }

        [Test]
        public async Task CreateDirectoryAsync_DirectoryExists_DoesNothing()
        {
            // Arrange
            Directory.CreateDirectory(_testDirectoryPath);

            // Act
            await _asyncFileStorage.CreateDirectoryAsync(_testDirectoryPath);

            // Assert
            Assert.IsTrue(Directory.Exists(_testDirectoryPath));
        }

        [Test]
        public void CreateDirectoryAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.CreateDirectoryAsync(_testDirectoryPath, cancellationTokenSource.Token));
        }

        [Test]
        public async Task DirectoryExistsAsync_DirectoryExists_ReturnsTrue()
        {
            // Arrange
            Directory.CreateDirectory(_testDirectoryPath);

            // Act
            var result = await _asyncFileStorage.DirectoryExistsAsync(_testDirectoryPath);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task DirectoryExistsAsync_DirectoryDoesNotExist_ReturnsFalse()
        {
            // Act
            var result = await _asyncFileStorage.DirectoryExistsAsync(_testDirectoryPath);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void DirectoryExistsAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.DirectoryExistsAsync(_testDirectoryPath, cancellationTokenSource.Token));
        }

        [Test]
        public async Task CopyFileAsync_FileExists_CopiesFile()
        {
            // Arrange
            var sourcePath = _testFilePath;
            var destinationPath = Path.Combine(Path.GetTempPath(), "copied_file.json");
            var content = "test content";
            File.WriteAllText(sourcePath, content);

            try
            {
                // Act
                await _asyncFileStorage.CopyFileAsync(sourcePath, destinationPath);

                // Assert
                Assert.IsTrue(File.Exists(destinationPath));
                var copiedContent = File.ReadAllText(destinationPath);
                Assert.That(copiedContent, Is.EqualTo(content));
            }
            finally
            {
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
            }
        }

        [Test]
        public async Task CopyFileAsync_DestinationDirectoryDoesNotExist_CreatesDirectoryAndCopiesFile()
        {
            // Arrange
            var sourcePath = _testFilePath;
            var destinationPath = Path.Combine(_testDirectoryPath, "copied_file.json");
            var content = "test content";
            File.WriteAllText(sourcePath, content);

            try
            {
                // Act
                await _asyncFileStorage.CopyFileAsync(sourcePath, destinationPath);

                // Assert
                Assert.IsTrue(Directory.Exists(_testDirectoryPath));
                Assert.IsTrue(File.Exists(destinationPath));
                var copiedContent = File.ReadAllText(destinationPath);
                Assert.That(copiedContent, Is.EqualTo(content));
            }
            finally
            {
                if (Directory.Exists(_testDirectoryPath))
                    Directory.Delete(_testDirectoryPath, true);
            }
        }

        [Test]
        public void CopyFileAsync_SourceFileDoesNotExist_ThrowsFileNotFoundException()
        {
            // Arrange
            var sourcePath = _testFilePath;
            var destinationPath = Path.Combine(Path.GetTempPath(), "copied_file.json");

            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _asyncFileStorage.CopyFileAsync(sourcePath, destinationPath));
        }

        [Test]
        public void CopyFileAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.CopyFileAsync(_testFilePath, "destination", false, cancellationTokenSource.Token));
        }

        [Test]
        public async Task MoveFileAsync_FileExists_MovesFile()
        {
            // Arrange
            var sourcePath = _testFilePath;
            var destinationPath = Path.Combine(Path.GetTempPath(), "moved_file.json");
            var content = "test content";
            File.WriteAllText(sourcePath, content);

            try
            {
                // Act
                await _asyncFileStorage.MoveFileAsync(sourcePath, destinationPath);

                // Assert
                Assert.IsFalse(File.Exists(sourcePath));
                Assert.IsTrue(File.Exists(destinationPath));
                var movedContent = File.ReadAllText(destinationPath);
                Assert.That(movedContent, Is.EqualTo(content));
            }
            finally
            {
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
            }
        }

        [Test]
        public void MoveFileAsync_SourceFileDoesNotExist_ThrowsFileNotFoundException()
        {
            // Arrange
            var sourcePath = _testFilePath;
            var destinationPath = Path.Combine(Path.GetTempPath(), "moved_file.json");

            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _asyncFileStorage.MoveFileAsync(sourcePath, destinationPath));
        }

        [Test]
        public void MoveFileAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await _asyncFileStorage.MoveFileAsync(_testFilePath, "destination", cancellationTokenSource.Token));
        }

        [Test]
        public void Constructor_NoParameters_UsesDefaultDependencies()
        {
            // Act
            var storage = new AsyncFileStorage();

            // Assert
            Assert.IsNotNull(storage);
        }

        [Test]
        public void Constructor_WithParameters_UsesProvidedDependencies()
        {
            // Arrange
            var mockStorageFormat = new Mock<IStorageFormat<DataScheme>>();
            var mockFileSystem = new Mock<IFileSystem>();

            // Act
            var storage = new AsyncFileStorage(mockStorageFormat.Object, mockFileSystem.Object);

            // Assert
            Assert.IsNotNull(storage);
        }
    }
} 