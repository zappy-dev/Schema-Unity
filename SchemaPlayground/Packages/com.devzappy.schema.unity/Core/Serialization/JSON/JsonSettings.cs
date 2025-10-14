using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Schema.Core.Serialization
{
    internal static class JsonSettings
    {
        private static JsonSerializerSettings settings;

        public static JsonSerializerSettings Settings => settings;

        static JsonSettings()
        {
            settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new DefaultNamingStrategy()
                },
                Converters = new List<JsonConverter>
                {
                    new DataTypeJsonConverter(),
                    new DataEntryJsonConverter(),
                    new PluginDataTypeJsonConverter(),
                },
                Formatting = Formatting.Indented,
            };
        }

        public static void AddConverters(params JsonConverter[] converters)
        {
            foreach (var converter in converters)
            {
                settings.Converters.Add(converter);
            }
        }
    }
}