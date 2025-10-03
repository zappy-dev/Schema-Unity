// using System.IO;

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Schema.Core.IO;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Serialization
{
    public class JsonStorageFormat<T> : IStorageFormat<T>
    {
        private JsonSerializerSettings settings;
        public string Extension => "json";
        public string DisplayName => "JSON";
        public bool IsImportSupported => false;
        public bool IsExportSupported => true;

        private readonly IFileSystem fileSystem;

        public JsonStorageFormat(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
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
        
        public SchemaResult<T> DeserializeFromFile(SchemaContext context, string filePath)
        {
            var readRes = fileSystem.ReadAllText(context, filePath);
            
            if (!readRes.Try(out string jsonData))
            {
                return readRes.CastError<T>();
            }

            return Deserialize(context, jsonData);
        }

        public SchemaResult<T> Deserialize(SchemaContext context, string content)
        {
            // TODO: Handle a non-scheme formatted file, converting into scheme format
            try
            {
                var data = JsonConvert.DeserializeObject<T>(content, settings);
                return SchemaResult<T>.Pass(data, "Parsed json data", this);
            }
            catch (Exception ex)
            {
                return SchemaResult<T>.Fail($"Error parsing JSON: {ex.Message}", this);
            }
        }

        public SchemaResult SerializeToFile(SchemaContext context, string filePath, T data)
        {
            if (!Serialize(data).Try(out var jsonData))
            {
                return Fail(context, "Failed to deserialize JSON");
            }
            
            fileSystem.WriteAllText(context, filePath, jsonData);
            return Pass($"Wrote {data} to file {filePath}", context);
        }

        public SchemaResult<string> Serialize(T data)
        {
            var jsonContent = JsonConvert.SerializeObject(data, Formatting.Indented, settings);
            return SchemaResult<string>.Pass(jsonContent, successMessage: "Serialized json data", this);
        }
    }
}