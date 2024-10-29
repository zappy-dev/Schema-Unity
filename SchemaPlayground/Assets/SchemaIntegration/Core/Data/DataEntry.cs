using System;
using System.Collections.Generic;

namespace Schema.Core
{
    [Serializable]
    public class DataEntry
    {
        public Dictionary<string, object> EntryData { get; set; }

        public DataEntry()
        {
            EntryData = new Dictionary<string, object>();
        }

        // Get and set data for an attribute
        public void SetData(string attributeName, object value)
        {
            EntryData[attributeName] = value;
        }

        public object GetData(string attributeName)
        {
            return EntryData.ContainsKey(attributeName) ? EntryData[attributeName] : null;
        }
    }
}