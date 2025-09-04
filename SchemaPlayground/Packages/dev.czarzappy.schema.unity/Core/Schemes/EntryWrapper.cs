using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    public class EntryWrapper
    {
        private protected DataEntry _dataEntry;
        public DataEntry _ => _dataEntry;
        
        private protected DataScheme _dataScheme;

        public EntryWrapper(DataScheme dataScheme, DataEntry entry)
        {
            _dataScheme = dataScheme;
            _dataEntry = entry;
        }
    }
}