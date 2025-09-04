using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    public class ScriptableObjectStorageFormat : IStorageFormat<DataScheme>
    {
        public string Extension => ".asset";
        public string DisplayName => "Scriptable Object";
        public bool IsImportSupported => true;
        public bool IsExportSupported => false;
    
        public SchemaResult<DataScheme> DeserializeFromFile(string filePath)
        {
            throw new System.NotImplementedException();
        }

        public SchemaResult<DataScheme> Deserialize(string content)
        {
            throw new System.NotImplementedException();
        }

        public SchemaResult SerializeToFile(string filePath, DataScheme data)
        {
            throw new System.NotImplementedException();
        }

        public SchemaResult<string> Serialize(DataScheme data)
        {
            throw new System.NotImplementedException();
        }
    }
}