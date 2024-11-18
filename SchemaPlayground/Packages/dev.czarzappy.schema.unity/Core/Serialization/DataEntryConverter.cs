using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    public class DataEntryConverter : JsonConverter
    {
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
        
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // throw new NotImplementedException();
            JObject jo = JObject.Load(reader);
            
            // handle legacy serialization format
            var entryData = jo["EntryData"];

            var entries = new Dictionary<string, object>();
            var kvpParent = entryData ?? jo;
            foreach (var token in kvpParent)
            {
                var property = token as JProperty;
                var attributeName = property.Name;
                
                // TODO: How to handle parsing entry data from JSON
                // Currently using a post-processor after loading a scheme into memory to validate and convert entry data if possible.
                // Cannot object casting, else this returns a JObject, which makes upstream parsing more complicated
                // Converting to string for now, since all DataType::ConvertData methods should be able to handle converting from string
                var data = property.Value.Value<string>(); 
                entries.Add(attributeName, data);
            }

            return new DataEntry(entries);
            // 
            // handle entryData 
            // return entryData.ToObject<DataEntry>(serializer);
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override bool CanConvert(Type objectType)
        {
            return typeof(DataEntry).IsAssignableFrom(objectType);
        }
    }
}