using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Data
{
    [Serializable]
    public class DataEntry : IEnumerable<KeyValuePair<string, object>>
    {
        [JsonProperty("EntryData")] 
        private Dictionary<string, object> entryData { get; set; } = new Dictionary<string, object>();

        public DataEntry()
        {
        }
        
        public DataEntry(Dictionary<string, object> entryData)
        {
            this.entryData = entryData;
        }

        // Get and set data for an attribute
        public object GetData(string attributeName)
        {
            return !entryData.TryGetValue(attributeName, out var value) ? null : value;
        }
        
        public bool HasData(AttributeDefinition attribute)
        {
            if (!entryData.TryGetValue(attribute.AttributeName, out var data))
            {
                return false;
            }

            if (attribute.DataType is ReferenceDataType referenceDataType)
            {
                return data != null || referenceDataType.SupportsEmptyReferences;
            }

            return data != null;
        }

        public SchemaResult<object> GetData(AttributeDefinition attribute)
        {
            if (!entryData.TryGetValue(attribute.AttributeName, out var value))
                return SchemaResult<object>.Fail($"No data found for {attribute}", this);

            return SchemaResult<object>.Pass(value, successMessage: $"Found value for {attribute}", this);

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
        
        public SchemaResult SetData(string attributeName, object value)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                return Fail($"The attribute name is empty.");
            }
            
            entryData[attributeName] = value;
            return Pass($"Setting '{attributeName}' to '{value}'");
        }

        public void MigrateData(string prevAttributeName, string newAttributeName)
        {
            SetData(newAttributeName, GetData(prevAttributeName));
            entryData.Remove(prevAttributeName);
        }

        public override string ToString()
        {
            return Print();
        }

        public string Print(int numAttributesToPrint = -1)
        {
            var sb = new StringBuilder();
            sb.Append("DataEntry=[");
            int entryIndex = 0;
            int numEntriesToPrint = numAttributesToPrint == -1 ? entryData.Count : numAttributesToPrint;
            foreach (var kvp in entryData.Take(numEntriesToPrint))
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
                sb.Append(")");
                
                if (entryIndex != numEntriesToPrint - 1)
                {
                    sb.Append(',');
                }
                entryIndex++;
            }

            sb.Append("]");
            return sb.ToString();
        }

        public void Add(string attributeName, object attributeValue)
        {
            if (SetData(attributeName, attributeValue).Failed)
            {
                throw new ArgumentException($"Failed to set '{attributeName}' to '{attributeValue}'");
            }
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return entryData.GetEnumerator();
        }

        #region Equality Members

        protected bool Equals(DataEntry other)
        {
            return entryData.SequenceEqual(other.entryData);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DataEntry)obj);
        }

        public override int GetHashCode()
        {
            return (entryData != null ? entryData.GetHashCode() : 0);
        }

        public static bool operator ==(DataEntry left, DataEntry right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DataEntry left, DataEntry right)
        {
            return !Equals(left, right);
        }

        #endregion
    }
}