using System.IO;
using Schema.Core;
using UnityEngine;

namespace Schema.Unity.Serialization
{
    public class JsonStorageFormat : IStorageFormat
    {
        public string Extension => ".json";
        public T Load<T>(string filePath)
        {
            string jsonData = File.ReadAllText(filePath);
            return JsonUtility.FromJson<T>(jsonData);
        }

        public void Save<T>(string filePath, T data)
        {
            string jsonData = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, jsonData);
        }
    }
}