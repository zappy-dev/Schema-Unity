using System.Collections.Generic;
using Schema.Core.Data;
using Schema.Core.Logging;
using Schema.Core.Serialization;

namespace Schema.Core.IO
{
    public class Storage
    {
        private string Context => nameof(Storage);

        public IStorageFormat<DataScheme> JSONStorageFormat;
        public IStorageFormat<DataScheme> CSVStorageFormat;
        public IStorageFormat<DataScheme> CSharpStorageFormat;
        public IStorageFormat<DataScheme> ScriptableObjectStorageFormat;
        
        public IStorageFormat<DataScheme> DefaultSchemaStorageFormat => JSONStorageFormat;
        public IStorageFormat<DataScheme> DefaultSchemaPublishFormat => JSONStorageFormat;

        public IStorageFormat<DataScheme> DefaultManifestStorageFormat => JSONStorageFormat;

        public IEnumerable<IStorageFormat<DataScheme>> AllFormats;

        public IFileSystem FileSystem { get; private set; }

        public Storage(IFileSystem fileSystem)
        {
            Logger.LogDbgVerbose($"Initializing {fileSystem.GetType().Name}", Context);
            
            FileSystem = fileSystem;
            JSONStorageFormat = new JsonStorageFormat<DataScheme>(fileSystem);
            CSVStorageFormat = new CSVStorageFormat(fileSystem);
            CSharpStorageFormat = new CSharpStorageFormat(fileSystem);
            ScriptableObjectStorageFormat = new ScriptableObjectStorageFormat();
            
            AllFormats = new[]
            {
                JSONStorageFormat,
                CSVStorageFormat,
                CSharpStorageFormat,
                ScriptableObjectStorageFormat
            };
        }
    }
}