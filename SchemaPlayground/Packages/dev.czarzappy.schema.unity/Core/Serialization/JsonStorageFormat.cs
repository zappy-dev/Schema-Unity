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
                    new DataTypeConverter(),
                    new DataEntryConverter(),
                },
                Formatting = Formatting.Indented,
            };
        }
        
        public SchemaResult<T> DeserializeFromFile(string filePath)
        {
            string jsonData = fileSystem.ReadAllText(filePath);

            return Deserialize(jsonData);
        }

        public SchemaResult<T> Deserialize(string content)
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

        public SchemaResult SerializeToFile(string filePath, T data)
        {
            if (!Serialize(data).Try(out var jsonData))
            {
                return Fail("Failed to deserialize JSON", this);
            }
            
            // Extract the directory path from the file path
            var directoryPath = Path.GetDirectoryName(filePath);

            // Check if the directory exists, and if not, create it
            if (!fileSystem.DirectoryExists(directoryPath))
            {
                fileSystem.CreateDirectory(directoryPath);
            }
            
            fileSystem.WriteAllText(filePath, jsonData);
            return Pass($"Wrote {data} to file {filePath}", this);
        }

        public SchemaResult<string> Serialize(T data)
        {
            var jsonContent = JsonConvert.SerializeObject(data, Formatting.Indented, settings);
            return SchemaResult<string>.Pass(jsonContent, successMessage: "Serialized json data", this);
        }
    }
}