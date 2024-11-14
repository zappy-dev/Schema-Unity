using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Schema.Core.Data
{
    [Serializable]
    public class DataEntry : IEnumerable<KeyValuePair<string, object>>
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

        // Get and set data for an attribute
        public object GetData(string attributeName)
        {
            return !entryData.TryGetValue(attributeName, out var value) ? null : value;
        }
        
        public object GetData(AttributeDefinition attribute, DataType castDataType = null)
        { 
            TryGetData(attribute, out object data, castDataType);
            return data;
        }
        
        public object GetDataOrDefault(AttributeDefinition attribute, DataType castDataType = null)
        {
            return TryGetData(attribute, out object data, castDataType) ? data : attribute.CloneDefaultValue();
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

        public bool TryGetData(AttributeDefinition attribute, 
            out object castValue, 
            DataType castDataType = null, 
            bool checkIfValid = false)
        {
            if (entryData.TryGetValue(attribute.AttributeName, out var value))
            {
                if (checkIfValid && !attribute.DataType.IsValid(value))
                {
                    castValue = null;
                    return false;
                }
                
                // check if there's any casts we need to do
                if (castDataType != null)
                {
                    return DataType.TryToConvertData(value, attribute.DataType, castDataType, out castValue);
                }

                castValue = value;
                return true;
            }

            if (castDataType != null)
            {
                castValue = castDataType.CloneDefaultValue();
                return true;
            }

            castValue = null;
            return false;
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
            Logger.LogVerbose($"Setting {attributeName} to {value}", this);
            entryData[attributeName] = value;
        }

        public void MigrateData(string prevAttributeName, string newAttributeName)
        {
            SetData(newAttributeName, GetData(prevAttributeName));
            entryData.Remove(prevAttributeName);
        }

        public override string ToString()
        {
            return Print(-1);
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
            SetData(attributeName, attributeValue);
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return entryData.GetEnumerator();
        }
    }
}