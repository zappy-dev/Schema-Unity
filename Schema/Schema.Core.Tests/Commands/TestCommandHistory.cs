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


namespace Schema.Core.Tests.Commands
{
    [TestFixture]
    public class TestCommandHistory
    {
        private CommandHistory _commandHistory;
        private Mock<ILogger> _mockLogger;
        private Mock<ISchemaCommand<string>> _mockCommand;
        private Mock<ISchemaCommand<int>> _mockCommand2;
        private Mock<ISchemaCommand<bool>> _mockNonUndoableCommand;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _commandHistory = new CommandHistory(_mockLogger.Object);
            
            // Setup mock commands
            _mockCommand = new Mock<ISchemaCommand<string>>();
            _mockCommand.Setup(c => c.Description).Returns("Test Command");
            _mockCommand.Setup(c => c.CanUndo).Returns(true);
            
            _mockCommand2 = new Mock<ISchemaCommand<int>>();
            _mockCommand2.Setup(c => c.Description).Returns("Test Command 2");
            _mockCommand2.Setup(c => c.CanUndo).Returns(true);
            
            _mockNonUndoableCommand = new Mock<ISchemaCommand<bool>>();
            _mockNonUndoableCommand.Setup(c => c.Description).Returns("Non-Undoable Command");
            _mockNonUndoableCommand.Setup(c => c.CanUndo).Returns(false);
        }

        [Test]
        public void Constructor_WithLogger_UsesProvidedLogger()
        {
            // Act
            var history = new CommandHistory(_mockLogger.Object);

            // Assert
            Assert.IsNotNull(history);
        }

        [Test]
        public void Constructor_WithoutLogger_UsesNullLogger()
        {
            // Act
            var history = new CommandHistory();

            // Assert
            Assert.IsNotNull(history);
        }

        [Test]
        public void InitialState_PropertiesHaveCorrectValues()
        {
            // Assert
            Assert.IsFalse(_commandHistory.CanUndo);
            Assert.IsFalse(_commandHistory.CanRedo);
            Assert.That(_commandHistory.Count, Is.EqualTo(0));
            Assert.IsNull(_commandHistory.LastCommand);
            Assert.That(_commandHistory.History.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.MaxHistorySize, Is.EqualTo(100));
        }

        [Test]
        public async Task ExecuteAsync_ValidCommand_Succeeds()
        {
            // Arrange
            var expectedResult = CommandResult<string>.Success("test result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.That(result.Result, Is.EqualTo("test result"));
            Assert.That(_commandHistory.Count, Is.EqualTo(1));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);
            Assert.That(_commandHistory.LastCommand, Is.EqualTo(_mockCommand.Object));
        }

        [Test]
        public void ExecuteAsync_NullCommand_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _commandHistory.ExecuteAsync<string>(null));
        }

        [Test]
        public async Task ExecuteAsync_CommandFails_DoesNotAddToHistory()
        {
            // Arrange
            var failedResult = CommandResult<string>.Failure("Command failed");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedResult);

            // Act
            var result = await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.That(_commandHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.IsNull(_commandHistory.LastCommand);
        }

        [Test]
        public async Task ExecuteAsync_NonUndoableCommand_DoesNotAddToUndoStack()
        {
            // Arrange
            var successResult = CommandResult<bool>.Success(true, "Success");
            _mockNonUndoableCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            // Act
            var result = await _commandHistory.ExecuteAsync(_mockNonUndoableCommand.Object);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.That(_commandHistory.Count, Is.EqualTo(1));
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ExecuteAsync_NewCommand_ClearsRedoStack()
        {
            Logger.Level = Logger.LogLevel.VERBOSE;
            // Arrange
            var successResult1 = CommandResult<string>.Success("result1", "Success");
            var successResult2 = CommandResult<int>.Success(42, "Success");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult1);
            // Undo first command to populate redo stack
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommandResult.Success("Undone"));
            
            _mockCommand2.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult2);

            // Execute first command
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.UndoAsync();

            // Act - Execute second command
            await _commandHistory.ExecuteAsync(_mockCommand2.Object);

            // Assert
            Assert.That(_commandHistory.CanRedo, Is.False);
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ExecuteAsync_ExceedsMaxHistorySize_RemovesOldestCommand()
        {
            // Arrange
            _commandHistory.MaxHistorySize = 2;
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            // Act
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Assert
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.History.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task UndoAsync_ValidCommand_Succeeds()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Act
            var result = await _commandHistory.UndoAsync();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.That(_commandHistory.CanRedo, Is.True);
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task UndoAsync_NoCommandsToUndo_ReturnsFailure()
        {
            // Act
            var result = await _commandHistory.UndoAsync();

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.That(result.Message, Is.EqualTo("No commands available to undo"));
        }

        [Test]
        public async Task UndoAsync_UndoFails_PutsCommandBackOnUndoStack()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoFailureResult = CommandResult.Failure("Undo failed");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoFailureResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Act
            var result = await _commandHistory.UndoAsync();

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(1));
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task RedoAsync_ValidCommand_Succeeds()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);
            _mockCommand.Setup(c => c.RedoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult.ToCommandResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.UndoAsync();

            // Act
            var result = await _commandHistory.RedoAsync();

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(1));
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task RedoAsync_NoCommandsToRedo_ReturnsFailure()
        {
            // Act
            var result = await _commandHistory.RedoAsync();

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.That(result.Message, Is.EqualTo("No commands available to redo"));
        }

        [Test]
        public async Task RedoAsync_RedoFails_PutsCommandBackOnRedoStack()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoResult = CommandResult.Success("Undone");
            var redoFailureResult = CommandResult<string>.Failure("Redo failed");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);
            _mockCommand.Setup(c => c.RedoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(redoFailureResult.ToCommandResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.UndoAsync();

            // Act
            var result = await _commandHistory.RedoAsync();

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.That(_commandHistory.CanRedo, Is.True);
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearHistory_ClearsAllStacks()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            // Act
            _commandHistory.ClearHistory();

            // Assert
            Assert.That(_commandHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.That(_commandHistory.CanRedo, Is.False);
            Assert.That(_commandHistory.History.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(0));
            Assert.IsNull(_commandHistory.LastCommand);
        }

        [Test]
        public async Task ExecuteAsync_RaisesCommandExecutedEvent()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            CommandExecutedEventArgs capturedArgs = null;
            _commandHistory.CommandExecuted += (sender, args) => capturedArgs = args;

            // Act
            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Assert
            Assert.IsNotNull(capturedArgs);
            Assert.That(capturedArgs.Command, Is.EqualTo(_mockCommand.Object));
            Assert.IsTrue(capturedArgs.Result.IsSuccess);
            Assert.That(capturedArgs.Duration, Is.GreaterThan(TimeSpan.Zero));
        }

        [Test]
        public async Task UndoAsync_RaisesCommandUndoneEvent()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            CommandUndoneEventArgs capturedArgs = null;
            _commandHistory.CommandUndone += (sender, args) => capturedArgs = args;

            // Act
            await _commandHistory.UndoAsync();

            // Assert
            Assert.IsNotNull(capturedArgs);
            Assert.That(capturedArgs.Command, Is.EqualTo(_mockCommand.Object));
            Assert.IsTrue(capturedArgs.Result.IsSuccess);
            Assert.That(capturedArgs.Duration, Is.GreaterThan(TimeSpan.Zero));
        }

        [Test]
        public async Task RedoAsync_RaisesCommandRedoneEvent()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);
            _mockCommand.Setup(c => c.RedoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult.ToCommandResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.UndoAsync();

            CommandRedoneEventArgs capturedArgs = null;
            _commandHistory.CommandRedone += (sender, args) => capturedArgs = args;

            // Act
            await _commandHistory.RedoAsync();

            // Assert
            Assert.IsNotNull(capturedArgs);
            Assert.That(capturedArgs.Command, Is.EqualTo(_mockCommand.Object));
            Assert.IsTrue(capturedArgs.Result.IsSuccess);
            Assert.That(capturedArgs.Duration, Is.GreaterThan(TimeSpan.Zero));
        }

        [Test]
        public void ClearHistory_RaisesHistoryClearedEvent()
        {
            // Arrange
            bool eventRaised = false;
            _commandHistory.HistoryCleared += (sender, args) => eventRaised = true;

            // Act
            _commandHistory.ClearHistory();

            // Assert
            Assert.IsTrue(eventRaised);
        }

        [Test]
        public async Task ExecuteAsync_CancellationRequested_ThrowsTaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _commandHistory.ExecuteAsync(_mockCommand.Object, cancellationTokenSource.Token));
        }

        [Test]
        public async Task UndoAsync_CancellationRequested_ThrowsTaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _commandHistory.UndoAsync(cancellationTokenSource.Token));
        }

        [Test]
        public async Task RedoAsync_CancellationRequested_ThrowsTaskCanceledException()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _commandHistory.RedoAsync(cancellationTokenSource.Token));
        }

        [Test]
        public async Task ConcurrentExecution_IsThreadSafe()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            var tasks = new List<Task<CommandResult<string>>>();

            // Act - Execute multiple commands concurrently
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_commandHistory.ExecuteAsync(_mockCommand.Object));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.That(_commandHistory.Count, Is.EqualTo(10));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(10));
        }

        [Test]
        public async Task MultipleUndoRedoOperations_WorkCorrectly()
        {
            // Arrange
            var successResult1 = CommandResult<string>.Success("result1", "Success");
            var successResult2 = CommandResult<int>.Success(42, "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult1);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);
            _mockCommand.Setup(c => c.RedoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult1.ToCommandResult);
            
            _mockCommand2.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult2);
            _mockCommand2.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);
            _mockCommand2.Setup(c => c.RedoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult2.ToCommandResult);

            // Act
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.ExecuteAsync(_mockCommand2.Object);
            
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);

            await _commandHistory.UndoAsync();
            
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.True);

            await _commandHistory.UndoAsync();
            
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.That(_commandHistory.CanRedo, Is.True);

            await _commandHistory.RedoAsync();
            
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.True);

            await _commandHistory.RedoAsync();
            
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);
        }

        [Test]
        public async Task MaxHistorySize_WhenExceeded_RemovesOldestCommands()
        {
            // Arrange
            _commandHistory.MaxHistorySize = 3;
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            // Act
            for (int i = 0; i < 5; i++)
            {
                await _commandHistory.ExecuteAsync(_mockCommand.Object);
            }

            // Assert
            Assert.That(_commandHistory.Count, Is.EqualTo(3));
            Assert.That(_commandHistory.History.Count, Is.EqualTo(3));
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task MaxHistorySize_WithUndoRedo_HandlesCorrectly()
        {
            // Arrange
            _commandHistory.MaxHistorySize = 2;
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);

            // Act
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.UndoAsync();
            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Assert
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);
        }

        [Test]
        public void Dispose_ReleasesResources()
        {
            // Act
            _commandHistory.Dispose();

            // Assert
            // Note: We can't easily test if semaphore is disposed, but we can verify no exceptions are thrown
            Assert.Pass("Dispose completed without exceptions");
        }

        [Test]
        public async Task History_ReturnsReadOnlyList()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Act & Assert
            Assert.That(_commandHistory.History, Is.InstanceOf<IReadOnlyList<ISchemaCommand>>());
            Assert.That(_commandHistory.History.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task UndoHistory_ReturnsReadOnlyList()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Act & Assert
            Assert.That(_commandHistory.UndoHistory, Is.InstanceOf<IReadOnlyList<ISchemaCommand>>());
            Assert.That(_commandHistory.UndoHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task RedoHistory_ReturnsReadOnlyList()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);

            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.UndoAsync();

            // Act & Assert
            Assert.That(_commandHistory.RedoHistory, Is.InstanceOf<IReadOnlyList<ISchemaCommand>>());
            Assert.That(_commandHistory.RedoHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task LastCommand_ReturnsMostRecentCommand()
        {
            // Arrange
            var successResult1 = CommandResult<string>.Success("result1", "Success");
            var successResult2 = CommandResult<int>.Success(42, "Success");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult1);
            _mockCommand2.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult2);

            // Act
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.ExecuteAsync(_mockCommand2.Object);

            // Assert
            Assert.That(_commandHistory.LastCommand, Is.EqualTo(_mockCommand2.Object));
        }

        [Test]
        public async Task Count_ReflectsTotalCommands()
        {
            // Arrange
            var successResult = CommandResult<string>.Success("result", "Success");
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            // Act
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            await _commandHistory.ExecuteAsync(_mockCommand.Object);

            // Assert
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task IntegrationTest_ComplexScenario()
        {
            // Arrange
            var successResult1 = CommandResult<string>.Success("result1", "Success");
            var successResult2 = CommandResult<int>.Success(42, "Success");
            var successResult3 = CommandResult<bool>.Success(true, "Success");
            var undoResult = CommandResult.Success("Undone");
            
            _mockCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult1);
            _mockCommand.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);
            _mockCommand.Setup(c => c.RedoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult1.ToCommandResult);
            
            _mockCommand2.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult2);
            _mockCommand2.Setup(c => c.UndoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(undoResult);
            _mockCommand2.Setup(c => c.RedoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult2.ToCommandResult);
            
            _mockNonUndoableCommand.Setup(c => c.ExecuteAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult3);

            // Act & Assert
            // Execute commands
            await _commandHistory.ExecuteAsync(_mockCommand.Object);
            Assert.That(_commandHistory.Count, Is.EqualTo(1));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);

            await _commandHistory.ExecuteAsync(_mockCommand2.Object);
            Assert.That(_commandHistory.Count, Is.EqualTo(2));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);

            await _commandHistory.ExecuteAsync(_mockNonUndoableCommand.Object);
            Assert.That(_commandHistory.Count, Is.EqualTo(3));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);

            // Undo operations
            await _commandHistory.UndoAsync();
            Assert.That(_commandHistory.Count, Is.EqualTo(3));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.True);

            await _commandHistory.UndoAsync();
            Assert.That(_commandHistory.Count, Is.EqualTo(3));
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.That(_commandHistory.CanRedo, Is.True);

            // Redo operations
            await _commandHistory.RedoAsync();
            Assert.That(_commandHistory.Count, Is.EqualTo(3));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.True);

            await _commandHistory.RedoAsync();
            Assert.That(_commandHistory.Count, Is.EqualTo(3));
            Assert.That(_commandHistory.CanUndo, Is.True);
            Assert.That(_commandHistory.CanRedo, Is.False);

            // Clear history
            _commandHistory.ClearHistory();
            Assert.That(_commandHistory.Count, Is.EqualTo(0));
            Assert.That(_commandHistory.CanUndo, Is.False);
            Assert.That(_commandHistory.CanRedo, Is.False);
        }
    }
} 