using System.Collections.Generic;
using System.Linq;
using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    public abstract class SchemeWrapper<TEntry> where TEntry : EntryWrapper
    {
        private protected DataScheme _dataScheme;
        public DataScheme _ => _dataScheme;

        public SchemeWrapper(DataScheme dataScheme)
        {
            _dataScheme = dataScheme;
        }
        
        public int EntryCount => _dataScheme.EntryCount;

        protected abstract TEntry EntryFactory(DataScheme dataScheme, DataEntry dataEntry);

        public TEntry GetEntry(int randomIdx)
        {
            return EntryFactory(_dataScheme, _dataScheme.GetEntry(randomIdx));
        }

        public IEnumerable<TEntry> GetEntries()
        {
            return _dataScheme.GetEntries().Select(e => EntryFactory(_, e));
        }
    }
}