using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    [System.Serializable]
    public class EntryWrapper
    {
        protected readonly DataEntry DataEntry;
        public DataEntry _ => DataEntry;
        
        protected readonly DataScheme DataScheme;

        public EntryWrapper(DataScheme dataScheme, DataEntry entry)
        {
            DataScheme = dataScheme;
            DataEntry = entry;
        }

        public override string ToString()
        {
            return DataEntry.ToString();
        }

        public override bool Equals(object obj)
        {
            return DataEntry.Equals(obj);
        }

        public override int GetHashCode()
        {
            return DataEntry.GetHashCode();
        }
    }
}