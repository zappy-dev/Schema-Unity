using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Schema.Core
{
    [Serializable]
    public class DataEntry
    {
        [JsonProperty("EntryData")]
        private Dictionary<string, object> entryData { get; set; }

        public DataEntry() : this(new Dictionary<string, object>())
        {
        }
        
        public DataEntry(Dictionary<string, object> entryData)
        {
            this.entryData = entryData;
        }

        public object this[string key]
        {
            get => entryData[key];
            set => entryData[key] = value;
        }

        // Get and set data for an attribute

        public object GetData(string attributeName)
        {
            return entryData.ContainsKey(attributeName) ? entryData[attributeName] : null;
        }

        public bool TryGetDataAsString(string entryKey, out string data)
        {
            if (entryData.TryGetValue(entryKey, out var rawData))
            {
                data = rawData.ToString();
                return true;
            }

            data = null;
            return false;
        }
        
        public void SetData(string attributeName, object value)
        {
            entryData[attributeName] = value;
        }

        public void MigrateData(string prevAttributeName, string newAttributeName)
        {
            SetData(newAttributeName, GetData(prevAttributeName));
            entryData.Remove(prevAttributeName);
        }
    }
}