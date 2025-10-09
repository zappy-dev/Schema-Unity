using System.Collections.Generic;
using Schema.Core.Data;
using Schema.Core.Logging;
using Schema.Core.Serialization;

namespace Schema.Core.IO
{
    public class Storage
    {
        private string Context => nameof(Storage);

        public ISchemeStorageFormat JsonSchemeStorageFormat;
        public ISchemeStorageFormat CsvSchemeStorageFormat;
        public ISchemeStorageFormat CSharpSchemeStorageFormat;
        public ISchemeStorageFormat ScriptableObjectSchemeStorageFormat;
        
        public ISchemeStorageFormat DefaultSchemeStorageFormat => JsonSchemeStorageFormat;
        public ISchemeStorageFormat DefaultSchemaPublishFormat => JsonSchemeStorageFormat;

        public ISchemeStorageFormat DefaultManifestStorageFormat => JsonSchemeStorageFormat;

        public IEnumerable<ISchemeStorageFormat> AllFormats;

        public IFileSystem FileSystem { get; private set; }

        public Storage(IFileSystem fileSystem)
        {
            Logger.LogDbgVerbose($"Initializing {fileSystem.GetType().Name}", Context);
            
            FileSystem = fileSystem;
            JsonSchemeStorageFormat = new JsonSchemeStorageFormat(fileSystem);
            CsvSchemeStorageFormat = new CsvSchemeStorageFormat(fileSystem);
            CSharpSchemeStorageFormat = new CSharpSchemeStorageFormat(fileSystem);
            ScriptableObjectSchemeStorageFormat = new ScriptableObjectSchemeStorageFormat();
            
            AllFormats = new[]
            {
                JsonSchemeStorageFormat,
                CsvSchemeStorageFormat,
                CSharpSchemeStorageFormat,
                ScriptableObjectSchemeStorageFormat
            };
        }
    }
}