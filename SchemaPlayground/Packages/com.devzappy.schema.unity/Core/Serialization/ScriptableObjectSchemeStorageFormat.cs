using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;

namespace Schema.Core.Serialization
{
    public class ScriptableObjectSchemeStorageFormat : ISchemeStorageFormat
    {
        public string Extension => ".asset";
        public string DisplayName => "Scriptable Object";
        public bool IsImportSupported => true;
        public bool IsExportSupported => false;
    
        public Task<SchemaResult<DataScheme>> DeserializeFromFile(SchemaContext context, string filePath,
            CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException($"{nameof(DataTypeJsonConverter)}.{nameof(DeserializeFromFile)}");
        }

        public SchemaResult<DataScheme> Deserialize(SchemaContext context, string content)
        {
            throw new System.NotImplementedException($"{nameof(DataTypeJsonConverter)}.{nameof(Deserialize)}");
        }

        public Task<SchemaResult> SerializeToFile(SchemaContext context, string filePath, DataScheme data,
            CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException($"{nameof(DataTypeJsonConverter)}.{nameof(SerializeToFile)}");
        }

        public SchemaResult<string> Serialize(SchemaContext context, DataScheme data)
        {
            throw new System.NotImplementedException($"{nameof(DataTypeJsonConverter)}.{nameof(Serialize)}");
        }
    }
}