using Moq;
using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Commands
{
    [TestFixture]
    public class TestLoadDataSchemeCommand
    {
        internal static SchemaContext Context = new SchemaContext
        {
            Driver = nameof(TestLoadDataSchemeCommand),
        };
        
        private Mock<IProgress<CommandProgress>> _mockProgress;
        private DataScheme _testScheme;
        private string _schemeName;
        private Mock<IFileSystem> _mockFileSystem;

        [SetUp]
        public void Setup()
        {
            Schema.Reset();
            _schemeName = "TestScheme";
            _testScheme = new DataScheme(_schemeName);
            _testScheme.AddAttribute(Context, "FieldA", DataType.Text).AssertPassed();
            _testScheme.AddEntry(Context, new DataEntry { { "FieldA", "Value1", Context } });
            _mockProgress = new Mock<IProgress<CommandProgress>>();
            
            _mockFileSystem = new Mock<IFileSystem>();
            Schema.SetStorage(new Storage(_mockFileSystem.Object));
            Schema.InitializeTemplateManifestScheme(Context);
        }

        [Test]
        public async Task ExecuteAsync_SuccessfulLoad_AddsScheme()
        {
            // Arrange
            var command = new LoadDataSchemeCommand(Context, _testScheme, overwriteExisting: true);

            // Act
            var result = await command.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsSuccess, result.Message);
            Assert.That(Schema.IsSchemeLoaded(Context, _schemeName).AssertPassed(), Is.True);
            Schema.GetScheme(Context, _schemeName).TryAssert(out var loadedScheme);
            Assert.That(loadedScheme, Is.EqualTo(_testScheme));
        }

        [Test]
        public async Task ExecuteAsync_InvalidSchemeName_Fails()
        {
            // Arrange
            var badScheme = new DataScheme("");
            var command = new LoadDataSchemeCommand(Context, badScheme, overwriteExisting: true);

            // Act
            var result = await command.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsFailure);
            Assert.That(result.Message, Does.Contain("Schema name is invalid"));
        }

        [Test]
        public async Task ExecuteAsync_ExistingSchemeWithoutOverwrite_Fails()
        {
            // Arrange
            Schema.LoadDataScheme(Context, _testScheme, true);
            var duplicateScheme = new DataScheme(_schemeName);
            var command = new LoadDataSchemeCommand(Context, duplicateScheme, overwriteExisting: false);

            // Act
            var result = await command.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsFailure);
            Assert.That(result.Message, Does.Contain("already exists"));
        }

        [Test]
        public async Task UndoAsync_AfterSuccessfulLoad_RemovesScheme()
        {
            Logger.Level = Logger.LogLevel.VERBOSE;
            // Arrange
            var command = new LoadDataSchemeCommand(Context, _testScheme, overwriteExisting: true);
            await command.ExecuteAsync(CancellationToken.None);
            Assert.That(Schema.IsSchemeLoaded(Context, _schemeName).AssertPassed(), Is.True);

            // Act
            var undoResult = await command.UndoAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(undoResult.IsSuccess, undoResult.Message);
            Assert.That(Schema.IsSchemeLoaded(Context, _schemeName).AssertPassed(), Is.False);
        }

        [Test]
        public async Task UndoAsync_WithoutExecution_Fails()
        {
            // Arrange
            var command = new LoadDataSchemeCommand(Context, _testScheme, overwriteExisting: true);

            // Act
            var undoResult = await command.UndoAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(undoResult.IsFailure);
            Assert.That(undoResult.Message, Does.Contain("Command 'Load data scheme 'TestScheme'' cannot be undone"));
        }
    }
} 