using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    public class ScriptableObjectStorageFormat : IStorageFormat<DataScheme>
    {
        public string Extension => ".asset";
        public string DisplayName => "Scriptable Object";
        public bool IsImportSupported => true;
        public bool IsExportSupported => false;
    
        public SchemaResult<DataScheme> DeserializeFromFile(SchemaContext context, string filePath)
        {
            throw new System.NotImplementedException();
        }

        public SchemaResult<DataScheme> Deserialize(SchemaContext context, string content)
        {
            throw new System.NotImplementedException();
        }

        public SchemaResult SerializeToFile(SchemaContext context, string filePath, DataScheme data)
        {
            throw new System.NotImplementedException();
        }

        public SchemaResult<string> Serialize(DataScheme data)
        {
            throw new System.NotImplementedException();
        }
    }
}