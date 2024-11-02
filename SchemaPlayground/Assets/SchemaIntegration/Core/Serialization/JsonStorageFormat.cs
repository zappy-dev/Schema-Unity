using System.IO;
using Newtonsoft.Json;

namespace Schema.Core.Serialization
{
    public class JsonStorageFormat : IStorageFormat<DataScheme>
    {
        public string Extension => "json";
        public DataScheme Load(string filePath)
        {
            string jsonData = File.ReadAllText(filePath);
            
            // TODO: Handle a non-schema formatted file, converting into schema format
            DataScheme schema = JsonConvert.DeserializeObject<DataScheme>(jsonData);
            return schema;
        }

        public void Save(string filePath, DataScheme data)
        {
            string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, jsonData);
        }
    }
}