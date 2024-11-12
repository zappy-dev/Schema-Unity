// using System.IO;

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Schema.Core.IO;

namespace Schema.Core.Serialization
{
    public class JsonStorageFormat : IStorageFormat<DataScheme>
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
                Formatting = Formatting.Indented,
            };
        }
        
        public bool TryDeserializeFromFile(string filePath, out DataScheme scheme)
        {
            string jsonData = fileSystem.ReadAllText(filePath);

            return TryDeserialize(jsonData, out scheme);
        }

        public bool TryDeserialize(string content, out DataScheme dataScheme)
        {
            // TODO: Handle a non-scheme formatted file, converting into scheme format
            try
            {
                dataScheme = JsonConvert.DeserializeObject<DataScheme>(content, settings);
                return true;
            }
            catch (JsonReaderException ex)
            {
                Logger.LogError($"Error parsing JSON: {ex.Message}");
                dataScheme = null;
                return false;
            }
        }

        public void SerializeToFile(string filePath, DataScheme data)
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

        public string Serialize(DataScheme data)
        {
            return JsonConvert.SerializeObject(data, Formatting.Indented, settings);
        }
    }
}