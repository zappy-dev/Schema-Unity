using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Core.Logging;
using Schema.Core.Storage;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Commands
{
    [TestFixture]
    public class TestLoadDataSchemeCommand
    {
        private Mock<IAsyncStorage> _mockStorage;
        private Mock<IProgress<CommandProgress>> _mockProgress;
        private DataScheme _testScheme;
        private string _schemeName;

        [SetUp]
        public void Setup()
        {
            Schema.Reset();
            _schemeName = "TestScheme";
            _testScheme = new DataScheme(_schemeName);
            _testScheme.AddAttribute(new AttributeDefinition("FieldA", DataType.Text)).AssertPassed();
            _testScheme.AddEntry(new DataEntry { { "FieldA", "Value1" } });
            _mockStorage = new Mock<IAsyncStorage>();
            _mockProgress = new Mock<IProgress<CommandProgress>>();
        }

        [Test]
        public async Task ExecuteAsync_SuccessfulLoad_AddsScheme()
        {
            // Arrange
            var command = new LoadDataSchemeCommand(_testScheme, overwriteExisting: true, storage: _mockStorage.Object);

            // Act
            var result = await command.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsSuccess, result.Message);
            Assert.That(Schema.DoesSchemeExist(_schemeName), Is.True);
            Schema.GetScheme(_schemeName).TryAssert(out var loadedScheme);
            Assert.That(loadedScheme, Is.EqualTo(_testScheme));
        }

        [Test]
        public async Task ExecuteAsync_InvalidSchemeName_Fails()
        {
            // Arrange
            var badScheme = new DataScheme("");
            var command = new LoadDataSchemeCommand(badScheme, overwriteExisting: true, storage: _mockStorage.Object);

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
            Schema.LoadDataScheme(_testScheme, true);
            var duplicateScheme = new DataScheme(_schemeName);
            var command = new LoadDataSchemeCommand(duplicateScheme, overwriteExisting: false, storage: _mockStorage.Object);

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
            var command = new LoadDataSchemeCommand(_testScheme, overwriteExisting: true, storage: _mockStorage.Object);
            await command.ExecuteAsync(CancellationToken.None);
            Assert.That(Schema.DoesSchemeExist(_schemeName), Is.True);

            // Act
            var undoResult = await command.UndoAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(undoResult.IsSuccess, undoResult.Message);
            Assert.That(Schema.DoesSchemeExist(_schemeName), Is.False);
        }

        [Test]
        public async Task UndoAsync_WithoutExecution_Fails()
        {
            // Arrange
            var command = new LoadDataSchemeCommand(_testScheme, overwriteExisting: true, storage: _mockStorage.Object);

            // Act
            var undoResult = await command.UndoAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(undoResult.IsFailure);
            Assert.That(undoResult.Message, Does.Contain("Command 'Load data scheme 'TestScheme'' cannot be undone"));
        }
    }
} 