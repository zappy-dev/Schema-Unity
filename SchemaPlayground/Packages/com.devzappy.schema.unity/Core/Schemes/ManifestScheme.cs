using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    public partial class ManifestScheme
    {
        public enum PublishTarget
        {
            RESOURCES,
            S3_BUCKET,
            SCRIPTABLE_OBJECT,
            
            DEFAULT = RESOURCES,
        }
        
        private ManifestEntry _selfEntry;
        public SchemaResult<ManifestEntry> GetSelfEntry(SchemaContext context)
        {
            if (_selfEntry != null)
            {
                return SchemaResult<ManifestEntry>.Pass(_selfEntry);
            }

            if (!DataScheme.GetEntry(e =>
                        Manifest.MANIFEST_SCHEME_NAME.Equals(e.GetDataAsString(nameof(ManifestEntry.SchemeName))), context)
                    .Try(out var selfEntry, out var selfError))
            {
                return selfError.CastError<ManifestEntry>();
            }

            _selfEntry = EntryFactory(_, selfEntry);
            return SchemaResult<ManifestEntry>.Pass(_selfEntry);
        }

        public bool IsDirty => DataScheme.IsDirty;

        public void SetDirty(SchemaContext context, bool isDirty)
        {
            DataScheme.SetDirty(context, isDirty);
        }

        public string SchemeName => DataScheme.SchemeName;

        public IEnumerable<string> GetAllSchemeNames()
        {
            return DataScheme.GetValuesForAttribute(nameof(ManifestEntry.SchemeName))
                    .Select(a => a?.ToString())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                ;
        }

        public SchemaResult<ManifestEntry> GetEntryForSchemeName(SchemaContext context, string schemeName)
        {
            if (!DataScheme
                    .GetEntry(e => string.Equals(schemeName, e.GetDataAsString(nameof(ManifestEntry.SchemeName))), context)
                    .Try(out var matchEntry, out var matchError))
            {
                return matchError.CastError<ManifestEntry>();
            }
            
            return SchemaResult<ManifestEntry>.Pass(EntryFactory(_, matchEntry));
        }

        public SchemaResult<ManifestEntry> AddManifestEntry(SchemaContext context, string schemeName, 
            PublishTarget publishTarget = PublishTarget.DEFAULT, 
            string importFilePath = null)
        {
            var newSchemeManifestEntry = ManifestDataEntryFactory.Build(context, this, schemeName, publishTarget, importFilePath);
            var res = DataScheme.AddEntry(context, newSchemeManifestEntry._, runDataValidation: false);
            
            return SchemaResult<ManifestEntry>.CheckIf(res.Passed, newSchemeManifestEntry, res.Message, res.Message);
        }

        public void DeleteEntry(SchemaContext context, ManifestEntry manifestEntry)
        {
            DataScheme.DeleteEntry(context, manifestEntry._);
        }
    }
}