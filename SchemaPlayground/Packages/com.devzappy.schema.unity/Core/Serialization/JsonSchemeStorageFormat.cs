// using System.IO;

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Schema.Core.Data;
using Schema.Core.IO;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Serialization
{
    public class JsonSchemeStorageFormat : ISchemeStorageFormat
    {
        public string Extension => "json";
        public string DisplayName => "JSON";
        public bool IsImportSupported => false;
        public bool IsExportSupported => true;

        private readonly IFileSystem fileSystem;

        public JsonSchemeStorageFormat(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        public async Task<SchemaResult<DataScheme>> DeserializeFromFile(SchemaContext context, string filePath,
            CancellationToken cancellationToken = default)
        {
            var readRes = await fileSystem.ReadAllText(context, filePath, cancellationToken);
            
            if (!readRes.Try(out string jsonData))
            {
                return readRes.CastError<DataScheme>();
            }

            return Deserialize(context, jsonData);
        }

        public SchemaResult<DataScheme> Deserialize(SchemaContext context, string content)
        {
            // TODO: Handle a non-scheme formatted file, converting into scheme format
            try
            {
                var data = JsonConvert.DeserializeObject<DataScheme>(content, JsonSettings.Settings);
                return SchemaResult<DataScheme>.Pass(data, "Parsed json data", this);
            }
            catch (Exception ex)
            {
                return SchemaResult<DataScheme>.Fail($"Error parsing JSON: {ex.Message}", this);
            }
        }

        public async Task<SchemaResult> SerializeToFile(SchemaContext context, string filePath, DataScheme data,
            CancellationToken cancellationToken = default)
        {
            if (!Serialize(context, data).Try(out var jsonData))
            {
                return Fail(context, "Failed to deserialize JSON");
            }
            
            await fileSystem.WriteAllText(context, filePath, jsonData, cancellationToken);
            return Pass($"Wrote {data} to file {filePath}", context);
        }

        public SchemaResult<string> Serialize(SchemaContext context, DataScheme data)
        {
            var jsonContent = JsonConvert.SerializeObject(data, Formatting.Indented, JsonSettings.Settings);
            return SchemaResult<string>.Pass(jsonContent, successMessage: "Serialized json data", this);
        }
    }
}