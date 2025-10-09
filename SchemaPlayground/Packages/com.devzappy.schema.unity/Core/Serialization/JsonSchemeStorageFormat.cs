// using System.IO;

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Schema.Core.Data;
using Schema.Core.IO;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Serialization
{
    public class JsonSchemeStorageFormat : ISchemeStorageFormat
    {
        private JsonSerializerSettings settings;
        public string Extension => "json";
        public string DisplayName => "JSON";
        public bool IsImportSupported => false;
        public bool IsExportSupported => true;

        private readonly IFileSystem fileSystem;

        public JsonSchemeStorageFormat(IFileSystem fileSystem)
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
        
        public SchemaResult<DataScheme> DeserializeFromFile(SchemaContext context, string filePath)
        {
            var readRes = fileSystem.ReadAllText(context, filePath);
            
            if (!readRes.Try(out string jsonData))
            {
                return readRes.CastError<DataScheme>();
            }

            return Deserialize(context, jsonData);
        }

        public SchemaResult<DataScheme> Deserialize(SchemaContext context, string content)
        {
            // TODO: Handle a non-scheme formatted file, converting into scheme format
            try
            {
                var data = JsonConvert.DeserializeObject<DataScheme>(content, settings);
                return SchemaResult<DataScheme>.Pass(data, "Parsed json data", this);
            }
            catch (Exception ex)
            {
                return SchemaResult<DataScheme>.Fail($"Error parsing JSON: {ex.Message}", this);
            }
        }

        public SchemaResult SerializeToFile(SchemaContext context, string filePath, DataScheme data)
        {
            if (!Serialize(context, data).Try(out var jsonData))
            {
                return Fail(context, "Failed to deserialize JSON");
            }
            
            fileSystem.WriteAllText(context, filePath, jsonData);
            return Pass($"Wrote {data} to file {filePath}", context);
        }

        public SchemaResult<string> Serialize(SchemaContext context, DataScheme data)
        {
            var jsonContent = JsonConvert.SerializeObject(data, Formatting.Indented, settings);
            return SchemaResult<string>.Pass(jsonContent, successMessage: "Serialized json data", this);
        }
    }
}