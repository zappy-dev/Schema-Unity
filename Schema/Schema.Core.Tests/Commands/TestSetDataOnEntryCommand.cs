using Schema.Core.Commands;
using Schema.Core.Data;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests.Commands
{
    [TestFixture]
    public class TestSetDataOnEntryCommand
    {
        private DataScheme _scheme;
        private DataEntry _entry;
        private const string AttributeName = "Name";

        [SetUp]
        public void Setup()
        { 
            Schema.Reset();
            _scheme = new DataScheme("People");
            _scheme.AddAttribute(AttributeName, DataType.Text, "")
                   .AssertPassed();
            _entry = _scheme.CreateNewEmptyEntry(); // create an empty entry
            _scheme.SetDataOnEntry(_entry, AttributeName, "Alice").AssertPassed();
            // Load scheme into global context so identifier checks work as expected
            Schema.LoadDataScheme(_scheme, overwriteExisting: true);
        }

        [Test]
        public async Task ExecuteAsync_UpdatesEntryValue()
        {
            // Arrange
            var cmd = new SetDataOnEntryCommand(_scheme, _entry, AttributeName, "Bob");

            // Act
            var result = await cmd.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(result.IsSuccess, result.Message);
            Assert.That(_entry.GetDataAsString(AttributeName), Is.EqualTo("Bob"));
        }

        [Test]
        public async Task UndoAsync_RevertsValue()
        {
            var cmd = new SetDataOnEntryCommand(_scheme, _entry, AttributeName, "Charlie");
            await cmd.ExecuteAsync();
            Assert.That(_entry.GetDataAsString(AttributeName), Is.EqualTo("Charlie"));

            var undo = await cmd.UndoAsync();
            Assert.IsTrue(undo.IsSuccess, undo.Message);
            Assert.That(_entry.GetDataAsString(AttributeName), Is.EqualTo("Alice"));
        }

        [Test]
        public async Task ExecuteAsync_InvalidAttribute_Fails()
        {
            var badCmd = new SetDataOnEntryCommand(_scheme, _entry, "BadAttr", "Foo");
            var result = await badCmd.ExecuteAsync();
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public async Task CommandHistory_ExecuteUndoRedo_WorkCorrectly()
        {
            var history = new CommandHistory();
            var cmd = new SetDataOnEntryCommand(_scheme, _entry, AttributeName, "Eve");

            // Execute
            var exec = await history.ExecuteAsync(cmd);
            Assert.IsTrue(exec.IsSuccess);
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history.CanUndo, Is.True);
            Assert.That(_entry.GetDataAsString(AttributeName), Is.EqualTo("Eve"));

            // Undo
            var undo = await history.UndoAsync();
            Assert.IsTrue(undo.IsSuccess);
            Assert.That(_entry.GetDataAsString(AttributeName), Is.EqualTo("Alice"));
            Assert.That(history.CanRedo, Is.True);

            // Redo
            var redo = await history.RedoAsync();
            Assert.IsTrue(redo.IsSuccess);
            Assert.That(_entry.GetDataAsString(AttributeName), Is.EqualTo("Eve"));
        }

        [Test]
        public async Task ExecuteAsync_Cancellation_ReturnsCancelled()
        {
            var cts = new CancellationTokenSource();
            await cts.CancelAsync(); // immediately cancel
            var cmd = new SetDataOnEntryCommand(_scheme, _entry, AttributeName, "Zoe");

            var result = await cmd.ExecuteAsync(cts.Token);
            Assert.IsTrue(result.IsCancelled);
            // Value should remain unchanged
            Assert.That(_entry.GetDataAsString(AttributeName), Is.EqualTo("Alice"));
        }

        [Test]
        public async Task UndoAsync_WithoutExecute_FailsGracefully()
        {
            var cmd = new SetDataOnEntryCommand(_scheme, _entry, AttributeName, "Delta");
            var undo = await cmd.UndoAsync();
            Assert.IsTrue(undo.IsFailure);
        }
    }
} 