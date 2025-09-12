using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Schema.Core.Logging;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Data
{
    [Serializable]
    public class DataEntry : IEnumerable<KeyValuePair<string, object>>
    {
        public string Context => nameof(DataEntry);
        
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
        
        /// <summary>
        /// High-performance version that returns the value directly without SchemaResult.
        /// Returns null if the attribute is not found.
        /// </summary>
        /// <param name="attribute">The attribute to get data for.</param>
        /// <returns>The value if found, null otherwise.</returns>
        public object GetDataDirect(AttributeDefinition attribute)
        {
            return entryData.TryGetValue(attribute.AttributeName, out var value) ? value : null;
        }

        public SchemaResult<object> GetData(AttributeDefinition attribute)
        {
            if (!entryData.TryGetValue(attribute.AttributeName, out var value))
                return SchemaResult<object>.Fail("No data found for attribute", Context);

            return SchemaResult<object>.Pass(value, successMessage: "Found value for attribute", Context);

        }

        public string GetDataAsString(string attributeName)
        {
            // TODO: Should this default to the default value for the attribute?
            return entryData.TryGetValue(attributeName, out var value) ? value.ToString() : string.Empty;
        }

        public int GetDataAsInt(string attributeName)
        {
            if (!entryData.TryGetValue(attributeName, out var value))
                return 0;
                
            if (value is int intValue)
                return intValue;

            if (value is long longValue)
            {
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                    return (int)longValue;
                return 0;
            }
                
            if (int.TryParse(value.ToString(), out var parsedValue))
                return parsedValue;
                
            return 0;
        }

        public bool GetDataAsBool(string attributeName)
        {
            if (!entryData.TryGetValue(attributeName, out var value))
                return false;
                
            if (value is bool boolValue)
                return boolValue;
                
            if (bool.TryParse(value.ToString(), out var parsedValue))
                return parsedValue;
                
            return false;
        }

        public Guid GetDataAsGuid(string attributeName)
        {
            if (!entryData.TryGetValue(attributeName, out var value))
                return Guid.Empty;
                
            if (value is Guid guidValue)
                return guidValue;
                
            if (Guid.TryParse(value.ToString(), out var parsedValue))
                return parsedValue;
                
            return Guid.Empty;
        }

        public float GetDataAsFloat(string attributeName)
        {
            if (!entryData.TryGetValue(attributeName, out var value))
                return 0f;
                
            if (value is float floatValue)
                return floatValue;
                
            if (value is double doubleValue)
                return (float)doubleValue;
                
            if (value is int intValue)
                return intValue;
                
            if (float.TryParse(value.ToString(), out var parsedValue))
                return parsedValue;
                
            return 0f;
        }

        public TEnum GetDataAsEnum<TEnum>(string attributeName) where TEnum : struct, Enum
        {
            if (!entryData.TryGetValue(attributeName, out var value))
                return default(TEnum);
                
            if (value is TEnum enumValue)
                return enumValue;
                
            if (value is int intValue)
            {
                if (Enum.IsDefined(typeof(TEnum), intValue))
                    return (TEnum)(object)intValue;
            }
                
            if (Enum.TryParse<TEnum>(value.ToString(), out var parsedValue))
                return parsedValue;
                
            return default(TEnum);
        }

        public bool TryGetDataAsEnum<TEnum>(string attributeName, out TEnum result) where TEnum : struct, Enum
        {
            if (!entryData.TryGetValue(attributeName, out var value))
            {
                result = default(TEnum);
                return false;
            }
                
            if (value is TEnum enumValue)
            {
                result = enumValue;
                return true;
            }
                
            if (value is int intValue)
            {
                if (Enum.IsDefined(typeof(TEnum), intValue))
                {
                    result = (TEnum)(object)intValue;
                    return true;
                }
            }
                
            if (Enum.TryParse<TEnum>(value.ToString(), out var parsedValue))
            {
                result = parsedValue;
                return true;
            }
                
            result = default(TEnum);
            return false;
        }

        public bool TryGetDataAsInt(string attributeName, out int result)
        {
            if (!entryData.TryGetValue(attributeName, out var value))
            {
                result = 0;
                return false;
            }
                
            if (value is int intValue)
            {
                result = intValue;
                return true;
            }
                
            if (value is long longValue)
            {
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    result = (int)longValue;
                    return true;
                }
                result = 0;
                return false;
            }
                
            if (int.TryParse(value.ToString(), out var parsedValue))
            {
                result = parsedValue;
                return true;
            }
                
            result = 0;
            return false;
        }

        public bool TryGetDataAsFloat(string attributeName, out float result)
        {
            if (!entryData.TryGetValue(attributeName, out var value))
            {
                result = 0f;
                return false;
            }
                
            if (value is float floatValue)
            {
                result = floatValue;
                return true;
            }
                
            if (value is double doubleValue)
            {
                result = (float)doubleValue;
                return true;
            }
                
            if (value is int intValue)
            {
                result = intValue;
                return true;
            }
                
            if (float.TryParse(value.ToString(), out var parsedValue))
            {
                result = parsedValue;
                return true;
            }
                
            result = 0f;
            return false;
        }

        public bool TryGetDataAsBool(string attributeName, out bool result)
        {
            if (!entryData.TryGetValue(attributeName, out var value))
            {
                result = false;
                return false;
            }
                
            if (value is bool boolValue)
            {
                result = boolValue;
                return true;
            }
                
            if (bool.TryParse(value.ToString(), out var parsedValue))
            {
                result = parsedValue;
                return true;
            }
                
            result = false;
            return false;
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
        
        /// <summary>
        /// Sets the value for an attribute in this entry.
        /// <b>Warning:</b> Do not call this directly for identifier attributes. Use <see cref="DataScheme.SetDataOnEntry"/> instead to ensure data integrity.
        /// </summary>
        /// <param name="attributeName">The attribute name to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A SchemaResult indicating success or failure.</returns>
        // [Obsolete("Do not call SetData directly. Use DataScheme.SetDataOnEntry instead to ensure identifier safety.", false)]
        public SchemaResult SetData(string attributeName, object value)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                return Fail($"The attribute name is empty.");
            }
            
            // Logger.LogDbgVerbose($"{attributeName}=>{value}({value?.GetType().Name})", Context);
            entryData[attributeName] = value;
            return Pass("Setting attribute value");
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
            sb.Append($"DataEntry({RuntimeHelpers.GetHashCode(this)})=[");
            int entryIndex = 0;
            int numEntriesToPrint = numAttributesToPrint == -1 ? entryData.Count : numAttributesToPrint;
            foreach (var kvp in entryData.Take(numEntriesToPrint))
            {
                sb.Append("(");
                sb.Append(kvp.Key).Append(": ");
                if (kvp.Value != null)
                {
                    sb.Append(kvp.Value);
                    sb.Append(':');
                    sb.Append(kvp.Value.GetType().Name);
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