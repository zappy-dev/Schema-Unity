using System;
using Schema.Core.Data;

namespace Schema.Core.Schemes
{
    [System.Serializable]
    public class EntryWrapper : IEquatable<EntryWrapper>
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
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((EntryWrapper)obj);
        }

        public override int GetHashCode()
        {
            return (DataEntry != null ? DataEntry.GetHashCode() : 0);
        }

        public bool Equals(EntryWrapper other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(DataEntry, other.DataEntry);
        }
    }
}