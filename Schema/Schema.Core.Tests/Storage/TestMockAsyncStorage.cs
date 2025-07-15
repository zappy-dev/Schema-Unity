using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Schema.Core.Data;
using Schema.Core.Storage;

namespace Schema.Core.Tests.Storage
{
    [TestFixture]
    public class TestMockAsyncStorage
    {
        private MockAsyncStorage _mockStorage;
        private DataScheme _testDataScheme;

        [SetUp]
        public void Setup()
        {
            _mockStorage = new MockAsyncStorage();
            _testDataScheme = new DataScheme("TestScheme");
        }

        [Test]
        public async Task FileExistsAsync_FileNotStored_ReturnsFalse()
        {
            // Act
            var result = await _mockStorage.FileExistsAsync("nonexistent.json");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public async Task FileExistsAsync_FileStored_ReturnsTrue()
        {
            // Arrange
            await _mockStorage.SerializeToFileAsync("test.json", _testDataScheme);

            // Act
            var result = await _mockStorage.FileExistsAsync("test.json");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task FileExistsAsync_TextFileStored_ReturnsTrue()
        {
            // Arrange
            await _mockStorage.WriteAllTextAsync("test.txt", "content");

            // Act
            var result = await _mockStorage.FileExistsAsync("test.txt");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void FileExistsAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.FileExistsAsync("test.json", cancellationTokenSource.Token));
        }

        [Test]
        public async Task DeserializeFromFileAsync_FileStored_ReturnsStoredData()
        {
            // Arrange
            await _mockStorage.SerializeToFileAsync("test.json", _testDataScheme);

            // Act
            var result = await _mockStorage.DeserializeFromFileAsync<DataScheme>("test.json");

            // Assert
            Assert.That(result, Is.EqualTo(_testDataScheme));
        }

        [Test]
        public void DeserializeFromFileAsync_FileNotStored_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _mockStorage.DeserializeFromFileAsync<DataScheme>("nonexistent.json"));
        }

        [Test]
        public async Task DeserializeFromFileAsync_WrongType_ThrowsFileNotFoundException()
        {
            // Arrange
            await _mockStorage.SerializeToFileAsync("test.json", _testDataScheme);

            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _mockStorage.DeserializeFromFileAsync<string>("test.json"));
        }

        [Test]
        public void DeserializeFromFileAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.DeserializeFromFileAsync<DataScheme>("test.json", cancellationTokenSource.Token));
        }

        [Test]
        public async Task SerializeToFileAsync_ValidData_StoresData()
        {
            // Act
            await _mockStorage.SerializeToFileAsync("test.json", _testDataScheme);

            // Assert
            var result = await _mockStorage.DeserializeFromFileAsync<DataScheme>("test.json");
            Assert.That(result, Is.EqualTo(_testDataScheme));
        }

        [Test]
        public void SerializeToFileAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.SerializeToFileAsync("test.json", _testDataScheme, cancellationTokenSource.Token));
        }

        [Test]
        public async Task DeleteFileAsync_FileStored_RemovesFile()
        {
            // Arrange
            await _mockStorage.SerializeToFileAsync("test.json", _testDataScheme);
            await _mockStorage.WriteAllTextAsync("test.txt", "content");

            // Act
            await _mockStorage.DeleteFileAsync("test.json");
            await _mockStorage.DeleteFileAsync("test.txt");

            // Assert
            Assert.IsFalse(await _mockStorage.FileExistsAsync("test.json"));
            Assert.IsFalse(await _mockStorage.FileExistsAsync("test.txt"));
        }

        [Test]
        public async Task DeleteFileAsync_FileNotStored_DoesNothing()
        {
            // Act
            await _mockStorage.DeleteFileAsync("nonexistent.json");

            // Assert
            Assert.IsFalse(await _mockStorage.FileExistsAsync("nonexistent.json"));
        }

        [Test]
        public void DeleteFileAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.DeleteFileAsync("test.json", cancellationTokenSource.Token));
        }

        [Test]
        public async Task ReadAllTextAsync_TextFileStored_ReturnsContent()
        {
            // Arrange
            var expectedContent = "test content";
            await _mockStorage.WriteAllTextAsync("test.txt", expectedContent);

            // Act
            var result = await _mockStorage.ReadAllTextAsync("test.txt");

            // Assert
            Assert.That(result, Is.EqualTo(expectedContent));
        }

        [Test]
        public void ReadAllTextAsync_FileNotStored_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _mockStorage.ReadAllTextAsync("nonexistent.txt"));
        }

        [Test]
        public void ReadAllTextAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.ReadAllTextAsync("test.txt", cancellationTokenSource.Token));
        }

        [Test]
        public async Task WriteAllTextAsync_ValidContent_StoresContent()
        {
            // Arrange
            var content = "test content";

            // Act
            await _mockStorage.WriteAllTextAsync("test.txt", content);

            // Assert
            var result = await _mockStorage.ReadAllTextAsync("test.txt");
            Assert.That(result, Is.EqualTo(content));
        }

        [Test]
        public void WriteAllTextAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.WriteAllTextAsync("test.txt", "content", cancellationTokenSource.Token));
        }

        [Test]
        public async Task GetFileInfoAsync_FileStored_ReturnsFileInfo()
        {
            // Arrange
            await _mockStorage.SerializeToFileAsync("test.json", _testDataScheme);

            // Act
            var result = await _mockStorage.GetFileInfoAsync("test.json");

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.Name, Is.EqualTo("test.json"));
        }

        [Test]
        public void GetFileInfoAsync_FileNotStored_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _mockStorage.GetFileInfoAsync("nonexistent.json"));
        }

        [Test]
        public void GetFileInfoAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.GetFileInfoAsync("test.json", cancellationTokenSource.Token));
        }

        [Test]
        public async Task CreateDirectoryAsync_AlwaysSucceeds()
        {
            // Act
            await _mockStorage.CreateDirectoryAsync("test_directory");

            // Assert
            // No exception should be thrown
        }

        [Test]
        public void CreateDirectoryAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.CreateDirectoryAsync("test_directory", cancellationTokenSource.Token));
        }

        [Test]
        public async Task DirectoryExistsAsync_AlwaysReturnsTrue()
        {
            // Act
            var result = await _mockStorage.DirectoryExistsAsync("any_directory");

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void DirectoryExistsAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.DirectoryExistsAsync("test_directory", cancellationTokenSource.Token));
        }

        [Test]
        public async Task CopyFileAsync_FileStored_CopiesFile()
        {
            // Arrange
            await _mockStorage.SerializeToFileAsync("source.json", _testDataScheme);

            // Act
            await _mockStorage.CopyFileAsync("source.json", "destination.json");

            // Assert
            Assert.IsTrue(await _mockStorage.FileExistsAsync("source.json"));
            Assert.IsTrue(await _mockStorage.FileExistsAsync("destination.json"));
            
            var copiedData = await _mockStorage.DeserializeFromFileAsync<DataScheme>("destination.json");
            Assert.That(copiedData, Is.EqualTo(_testDataScheme));
        }

        [Test]
        public async Task CopyFileAsync_TextFileStored_CopiesTextFile()
        {
            // Arrange
            await _mockStorage.WriteAllTextAsync("source.txt", "content");

            // Act
            await _mockStorage.CopyFileAsync("source.txt", "destination.txt");

            // Assert
            Assert.IsTrue(await _mockStorage.FileExistsAsync("source.txt"));
            Assert.IsTrue(await _mockStorage.FileExistsAsync("destination.txt"));
            
            var copiedContent = await _mockStorage.ReadAllTextAsync("destination.txt");
            Assert.That(copiedContent, Is.EqualTo("content"));
        }

        [Test]
        public void CopyFileAsync_SourceFileNotStored_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _mockStorage.CopyFileAsync("source.json", "destination.json"));
        }

        [Test]
        public async Task CopyFileAsync_OverwriteFalse_DestinationExists_DoesNotOverwrite()
        {
            // Arrange
            var originalData = new DataScheme("Original");
            var newData = new DataScheme("New");
            
            await _mockStorage.SerializeToFileAsync("source.json", originalData);
            await _mockStorage.SerializeToFileAsync("destination.json", newData);

            // Act
            await _mockStorage.CopyFileAsync("source.json", "destination.json", overwrite: false);

            // Assert
            var destinationData = await _mockStorage.DeserializeFromFileAsync<DataScheme>("destination.json");
            Assert.That(destinationData, Is.EqualTo(newData)); // Should not be overwritten
        }

        [Test]
        public async Task CopyFileAsync_OverwriteTrue_DestinationExists_Overwrites()
        {
            // Arrange
            var originalData = new DataScheme("Original");
            var newData = new DataScheme("New");
            
            await _mockStorage.SerializeToFileAsync("source.json", originalData);
            await _mockStorage.SerializeToFileAsync("destination.json", newData);

            // Act
            await _mockStorage.CopyFileAsync("source.json", "destination.json", overwrite: true);

            // Assert
            var destinationData = await _mockStorage.DeserializeFromFileAsync<DataScheme>("destination.json");
            Assert.That(destinationData, Is.EqualTo(originalData)); // Should be overwritten
        }

        [Test]
        public void CopyFileAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.CopyFileAsync("source.json", "destination.json", cancellationToken: cancellationTokenSource.Token));
        }

        [Test]
        public async Task MoveFileAsync_FileStored_MovesFile()
        {
            // Arrange
            await _mockStorage.SerializeToFileAsync("source.json", _testDataScheme);

            // Act
            await _mockStorage.MoveFileAsync("source.json", "destination.json");

            // Assert
            Assert.IsFalse(await _mockStorage.FileExistsAsync("source.json"));
            Assert.IsTrue(await _mockStorage.FileExistsAsync("destination.json"));
            
            var movedData = await _mockStorage.DeserializeFromFileAsync<DataScheme>("destination.json");
            Assert.That(movedData, Is.EqualTo(_testDataScheme));
        }

        [Test]
        public void MoveFileAsync_SourceFileNotStored_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _mockStorage.MoveFileAsync("source.json", "destination.json"));
        }

        [Test]
        public void MoveFileAsync_CancellationRequested_TaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _mockStorage.MoveFileAsync("source.json", "destination.json", cancellationTokenSource.Token));
        }

        [Test]
        public async Task IntegrationTest_MultipleOperations_WorkCorrectly()
        {
            // Arrange
            var dataScheme1 = new DataScheme("Scheme1");
            var dataScheme2 = new DataScheme("Scheme2");

            // Act & Assert
            // Write files
            await _mockStorage.SerializeToFileAsync("file1.json", dataScheme1);
            await _mockStorage.WriteAllTextAsync("file2.txt", "content");

            // Verify files exist
            Assert.IsTrue(await _mockStorage.FileExistsAsync("file1.json"));
            Assert.IsTrue(await _mockStorage.FileExistsAsync("file2.txt"));

            // Read files
            var readData1 = await _mockStorage.DeserializeFromFileAsync<DataScheme>("file1.json");
            var readData2 = await _mockStorage.ReadAllTextAsync("file2.txt");
            
            Assert.That(readData1, Is.EqualTo(dataScheme1));
            Assert.That(readData2, Is.EqualTo("content"));

            // Copy files
            await _mockStorage.CopyFileAsync("file1.json", "file1_copy.json");
            Assert.IsTrue(await _mockStorage.FileExistsAsync("file1_copy.json"));

            // Move files
            await _mockStorage.MoveFileAsync("file2.txt", "file2_moved.txt");
            Assert.IsFalse(await _mockStorage.FileExistsAsync("file2.txt"));
            Assert.IsTrue(await _mockStorage.FileExistsAsync("file2_moved.txt"));

            // Delete files
            await _mockStorage.DeleteFileAsync("file1_copy.json");
            Assert.IsFalse(await _mockStorage.FileExistsAsync("file1_copy.json"));
        }
    }
} 