using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            get => GetData(key);
            set => SetData(key, value);
        }

        // Get and set data for an attribute

        public object GetData(string attributeName)
        {
            return entryData.TryGetValue(attributeName, out var value) ? value : null;
        }

        public string GetDataAsString(string attributeName)
        {
            return entryData.TryGetValue(attributeName, out var value) ? value.ToString() : null;
        }

        public bool TryGetDataAsString(string entryKey, out string data)
        {
            if (entryData.TryGetValue(entryKey, out var rawData))
            {
                // what if the data is null?
                // since casting to an explicit type, maybe better to have more conversion safety explicitly
                if (rawData is null)
                {
                    data = null;
                    return false;
                }
                
                data = rawData.ToString();
                return true;
            }

            data = null;
            return false;
        }
        
        public void SetData(string attributeName, object value)
        {
            Logger.LogVerbose($"");
            entryData[attributeName] = value;
        }

        public void MigrateData(string prevAttributeName, string newAttributeName)
        {
            SetData(newAttributeName, GetData(prevAttributeName));
            entryData.Remove(prevAttributeName);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Entry=[");
            foreach (var kvp in entryData)
            {
                sb.Append("(");
                sb.Append(kvp.Key).Append(": ");
                if (kvp.Value != null)
                {
                    sb.Append(kvp.Value);
                }
                else
                {
                    sb.Append("(not set)");
                }
                sb.Append("),");
            }

            sb.Append("]");
            return sb.ToString();
        }
    }
}