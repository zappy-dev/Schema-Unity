using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    public class PluginDataTypeJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(PluginDataType) == objectType;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is PluginDataType pluginDataType)) throw new InvalidOperationException("Can only write PluginDataType objects");
            
            writer.WriteStartObject();
            writer.WritePropertyName(JsonUtils.PROPERTY_NAME_TYPE);
            writer.WriteValue(pluginDataType.PluginTypeName);
            foreach (var kvp in pluginDataType.PluginData)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteValue(kvp.Value);
            }
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader);

            string typeName = jo[JsonUtils.PROPERTY_NAME_TYPE].Value<string>();
            var pluginData = new Dictionary<string, object>();

            var values = jo.GetEnumerator();
            while (values.MoveNext())
            {
                var kvp = values.Current;
                var key = kvp.Key;
                if (key == JsonUtils.PROPERTY_NAME_TYPE) continue; // already processed type key
                var value = kvp.Value;
                pluginData[key] = value;
            }

            return new PluginDataType(typeName, pluginData);
        }
    }
}