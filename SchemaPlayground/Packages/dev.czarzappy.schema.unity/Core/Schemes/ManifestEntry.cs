using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    public partial class ManifestEntry : EntryWrapper
    {
        public ManifestEntry(DataScheme dataScheme, DataEntry dataEntry) : base(dataScheme, dataEntry)
        {
        }

        public string SchemeName
        {
            get => _dataEntry.GetDataAsString(nameof(SchemeName));
            set => _dataScheme.SetDataOnEntry(_dataEntry, nameof(SchemeName), value);
            // _dataEntry.SetData(nameof(FilePath), value);
        }

        public string FilePath
        {
            get => _dataEntry.GetDataAsString(nameof(FilePath));
            set => _dataScheme.SetDataOnEntry(_dataEntry, nameof(FilePath), value);
            // _dataEntry.SetData(nameof(FilePath), value);
        }
    }
}