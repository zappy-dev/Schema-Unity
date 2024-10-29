using System.IO;
using Newtonsoft.Json;

namespace Schema.Core.Serialization
{
    public class JsonStorageFormat<T> : IStorageFormat<T> where T : new()
    {
        public string Extension => "json";
        public T Load(string filePath)
        {
            string jsonData = File.ReadAllText(filePath);
            T obj = JsonConvert.DeserializeObject<T>(jsonData);
            return obj;
        }

        public void Save(string filePath, T data)
        {
            string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, jsonData);
        }
    }
}