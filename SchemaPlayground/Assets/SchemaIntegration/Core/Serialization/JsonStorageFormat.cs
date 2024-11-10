using System.IO;
using Newtonsoft.Json;

namespace Schema.Core.Serialization
{
    public class JsonStorageFormat : IStorageFormat<DataScheme>
    {
        private JsonSerializerSettings settings;
        public string Extension => "json";

        public JsonStorageFormat()
        {
            settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            };
        }
        public DataScheme Load(string filePath)
        {
            string jsonData = File.ReadAllText(filePath);
            
            // TODO: Handle a non-schema formatted file, converting into schema format
            DataScheme schema = JsonConvert.DeserializeObject<DataScheme>(jsonData, settings);
            return schema;
        }

        public void Save(string filePath, DataScheme data)
        {
            string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented, settings);
            File.WriteAllText(filePath, jsonData);
        }
    }
}