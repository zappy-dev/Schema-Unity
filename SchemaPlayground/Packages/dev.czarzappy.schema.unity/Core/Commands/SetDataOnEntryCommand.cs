using System;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Command that sets a value on a DataEntry within a DataScheme. Supports undo/redo by restoring the previous value.
    /// </summary>
    public sealed class SetDataOnEntryCommand : SchemaCommandBase<SchemaResult>
    {
        private readonly DataScheme _scheme;
        private readonly DataEntry _entry;
        private readonly string _attributeName;
        private readonly object _newValue;
        private readonly bool _allowIdentifierUpdate;

        // Captured for undo
        private object _oldValue;
        private bool _hasCapturedOldValue;

        public SetDataOnEntryCommand(
            DataScheme scheme,
            DataEntry entry,
            string attributeName,
            object newValue,
            bool allowIdentifierUpdate = false)
        {
            _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _attributeName = attributeName ?? throw new ArgumentNullException(nameof(attributeName));
            _newValue = newValue; // can be null
            _allowIdentifierUpdate = allowIdentifierUpdate;
        }

        public override string Description =>
            $"SetDataOnEntry '{_scheme.SchemeName}.{_attributeName}'";

        protected override Task<CommandResult<SchemaResult>> ExecuteInternalAsync(CancellationToken cancellationToken)
        {
            // First execution: capture previous value for undo
            if (!_hasCapturedOldValue)
            {
                var oldRes = _entry.GetData(_attributeName);
                _oldValue = oldRes;
                _hasCapturedOldValue = true;
            }

            var result = _scheme.SetDataOnEntry(_entry, _attributeName, _newValue, _allowIdentifierUpdate);

            var cmdResult = result.Passed
                ? CommandResult<SchemaResult>.Success(result)
                : CommandResult<SchemaResult>.Failure(result.Message);

            return Task.FromResult(cmdResult);
        }

        protected override Task<CommandResult> UndoInternalAsync(CancellationToken cancellationToken)
        {
            var result = _scheme.SetDataOnEntry(_entry, _attributeName, _oldValue, allowIdentifierUpdate: true);
            return Task.FromResult(result.Passed ? CommandResult.Success("Undo successful") : CommandResult.Failure(result.Message));
        }
    }
} 