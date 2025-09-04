using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    /// <summary>
    /// JSON converter for DataEntry objects that handles serialization and deserialization
    /// while preserving the original data types of values.
    /// </summary>
    public class DataEntryConverter : JsonConverter
    {
        /// <summary>
        /// Serializes a DataEntry to JSON by writing each key-value pair directly.
        /// </summary>
        /// <param name="writer">The JSON writer to use.</param>
        /// <param name="value">The DataEntry object to serialize.</param>
        /// <param name="serializer">The JSON serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is DataEntry dataEntry))
            {
                throw new JsonSerializationException("Expected DataEntry object value");
            }
            
            // simplify serializing data entries as just key-value pairs
            writer.WriteStartObject();
            foreach (var kvp in dataEntry)
            {
                writer.WritePropertyName(kvp.Key);
                serializer.Serialize(writer, kvp.Value);
            }
            
            writer.WriteEndObject();
        }
        
        /// <summary>
        /// Deserializes JSON to a DataEntry, preserving the original data types of values.
        /// </summary>
        /// <param name="reader">The JSON reader to use.</param>
        /// <param name="objectType">The target object type.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <param name="serializer">The JSON serializer.</param>
        /// <returns>A new DataEntry with the deserialized data.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            
            // handle legacy serialization format
            var entryData = jo["EntryData"];

            var entries = new Dictionary<string, object>();
            var kvpParent = entryData ?? jo;
            foreach (var token in kvpParent)
            {
                var property = token as JProperty;
                var attributeName = property.Name;
                
                // Preserve the original data type by converting JToken to its native .NET type
                // This allows DataType::ConvertData methods to work with the correct types
                var data = ConvertJTokenToNativeType(property.Value);
                entries.Add(attributeName, data);
            }

            return new DataEntry(entries);
        }

        /// <summary>
        /// Converts a JToken to its native .NET type, preserving the original data type.
        /// </summary>
        /// <param name="token">The JToken to convert.</param>
        /// <returns>The native .NET object representing the token's value.</returns>
        private static object ConvertJTokenToNativeType(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                    // Handle both int and long appropriately
                    var longValue = token.Value<long>();
                    if (longValue >= int.MinValue && longValue <= int.MaxValue)
                        return (int)longValue;
                    return longValue;
                case JTokenType.Float:
                    return token.Value<double>();
                case JTokenType.String:
                    return token.Value<string>();
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.Date:
                    return token.Value<DateTime>();
                case JTokenType.Null:
                    return null;
                case JTokenType.Array:
                    return token.ToObject<object[]>();
                case JTokenType.Object:
                    // For complex objects, return as JObject to preserve structure
                    return token;
                default:
                    // Fallback to string for unknown types
                    return token.ToString();
            }
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override bool CanConvert(Type objectType)
        {
            return typeof(DataEntry).IsAssignableFrom(objectType);
        }
    }
}