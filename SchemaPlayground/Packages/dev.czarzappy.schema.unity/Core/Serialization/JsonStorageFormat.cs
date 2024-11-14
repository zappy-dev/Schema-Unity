// using System.IO;

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Schema.Core.IO;

namespace Schema.Core.Serialization
{
    public class JsonStorageFormat<T> : IStorageFormat<T>
    {
        private JsonSerializerSettings settings;
        public string Extension => "json";

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
        
        public bool TryDeserializeFromFile(string filePath, out T scheme)
        {
            string jsonData = fileSystem.ReadAllText(filePath);

            return TryDeserialize(jsonData, out scheme);
        }

        public bool TryDeserialize(string content, out T data)
        {
            // TODO: Handle a non-scheme formatted file, converting into scheme format
            try
            {
                data = JsonConvert.DeserializeObject<T>(content, settings);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error parsing JSON: {ex.Message}");
                data = default;
                return false;
            }
        }

        public void SerializeToFile(string filePath, T data)
        {
            string jsonData = Serialize(data);
            // Extract the directory path from the file path
            var directoryPath = Path.GetDirectoryName(filePath);

            // Check if the directory exists, and if not, create it
            if (!fileSystem.DirectoryExists(directoryPath))
            {
                fileSystem.CreateDirectory(directoryPath);
            }
            
            fileSystem.WriteAllText(filePath, jsonData);
        }

        public string Serialize(T data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented, settings);
        }
    }
}