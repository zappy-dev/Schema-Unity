using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    public partial class ManifestScheme
    {
        private ManifestEntry _selfEntry;
        public ManifestEntry SelfEntry
        {
            get
            {
                if (_selfEntry != null)
                {
                    return _selfEntry;
                }

                if (!_dataScheme.TryGetEntry(e =>
                            Manifest.MANIFEST_SCHEME_NAME.Equals(e.GetDataAsString(nameof(ManifestEntry.SchemeName))),
                        out var selfEntry))
                {
                    return null;
                }

                _selfEntry = new ManifestEntry(_, selfEntry);
                return _selfEntry;
            }
        }

        public bool IsDirty
        {
            set => _dataScheme.IsDirty = value;
            get => _dataScheme.IsDirty;
        }

        public string SchemeName => _dataScheme.SchemeName;

        public IEnumerable<string> GetAllSchemeNames()
        {
            return _dataScheme.GetValuesForAttribute(nameof(ManifestEntry.SchemeName))
                    .Select(a => a?.ToString())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                ;
        }

        public bool TryGetEntryForSchemeName(string schemeName, out ManifestEntry manifestEntry)
        {
            bool success = _dataScheme.TryGetEntry(e => string.Equals(schemeName, e.GetDataAsString(nameof(ManifestEntry.SchemeName))),
                out var matchEntry);
            manifestEntry = new ManifestEntry(_, matchEntry);
            return success;
        }

        public SchemaResult<ManifestEntry> AddManifestEntry(string schemeName, string importFilePath = null)
        {
            var newSchemeManifestEntry = ManifestDataEntryFactory.Build(this, schemeName, importFilePath);
            var res = _dataScheme.AddEntry(newSchemeManifestEntry._, runDataValidation: false);
            
            return SchemaResult<ManifestEntry>.CheckIf(res.Passed, newSchemeManifestEntry, res.Message, res.Message);
        }

        public void DeleteEntry(ManifestEntry manifestEntry)
        {
            _dataScheme.DeleteEntry(manifestEntry._);
        }
    }
}