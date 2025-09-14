using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;
using Schema.Core.Schemes;
using static Schema.Core.Commands.CommandResult;

namespace Schema.Core.Commands
{
    public class DeleteEntryCommand : SchemaCommandBase
    {
        private readonly DataScheme _scheme;
        private readonly DataEntry _entry;
        
        // Captured for undo
        private int entryIdx;
        
        public DeleteEntryCommand(SchemaContext context, DataScheme scheme,
            DataEntry entry) : base(context)
        {
            _scheme = scheme;
            _entry = entry;
        }

        public override string Description => $"DeleteEntry {_scheme.SchemeName}, {_entry}";
        protected override Task<CommandResult> ExecuteInternalAsync(CancellationToken cancellationToken)
        {
            entryIdx = _scheme.GetEntryIndex(_entry);
            if (entryIdx == -1)
            {
                return Task.FromResult(Fail("Scheme does not contain entry"));
            }

            var deleteRes = _scheme.DeleteEntry(Context, _entry);
            if (deleteRes.Failed)
            {
                return Task.FromResult(Fail(deleteRes.Message));
            }

            if (_scheme.IsManifest)
            {
                var manifestEntry = new ManifestEntry(_scheme, _entry);
                var unloadRes = Schema.UnloadScheme(Context, manifestEntry.SchemeName);

                if (unloadRes.Failed)
                {
                    return Task.FromResult(Fail(unloadRes.Message));
                }
            }

            return Task.FromResult(Pass(Unit.Value, "Successfully deleted entry from scheme"));
        }

        protected override Task<CommandResult> UndoInternalAsync(CancellationToken cancellationToken)
        {
            var addEntryRes = _scheme.AddEntry(Context, _entry);

            if (addEntryRes.Failed)
            {
                return Task.FromResult(Fail(addEntryRes.Message));
            }

            if (_scheme.IsManifest)
            {
                var manifestEntry = new ManifestEntry(_scheme, _entry);

                var reloadSchemaRes = Schema.LoadSchemeFromManifestEntry(Context, manifestEntry);
                if (reloadSchemaRes.Failed)
                {
                    return Task.FromResult(Fail(reloadSchemaRes.Message));
                }
            }

            var moveRes = _scheme.MoveEntry(Context, _entry, entryIdx);
            
            var cmdResult = moveRes.Passed
                ? Pass(moveRes)
                : Fail(moveRes.Message);

            return Task.FromResult(cmdResult);
        }
    }
}