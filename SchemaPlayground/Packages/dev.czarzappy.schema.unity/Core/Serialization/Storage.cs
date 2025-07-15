using System.Collections.Generic;
using Schema.Core.Data;
using Schema.Core.IO;

namespace Schema.Core.Serialization
{
    public static class Storage
    {
        private static IFileSystem defaultFileSystem = new LocalFileSystem();

        public static IStorageFormat<DataScheme> JSONStorageFormat;
        public static IStorageFormat<DataScheme> CSVStorageFormat;
        public static IStorageFormat<DataScheme> CSharpCodeExportFormat;
        
        public static IStorageFormat<DataScheme> DefaultSchemaStorageFormat => JSONStorageFormat;

        public static IStorageFormat<DataScheme> DefaultManifestStorageFormat => JSONStorageFormat;

        public static IEnumerable<IStorageFormat<DataScheme>> AllFormats = new[]
        {
            JSONStorageFormat,
            CSVStorageFormat,
            CSharpCodeExportFormat,
        };

        public static IFileSystem FileSystem { get; private set; }

        public static void SetFileSystem(IFileSystem fileSystem)
        {
            FileSystem = fileSystem;
            JSONStorageFormat = new JsonStorageFormat<DataScheme>(fileSystem);
            CSVStorageFormat = new CSVStorageFormat(fileSystem);
            CSharpCodeExportFormat = new CSharpCodeExportFormat(fileSystem);
        }

        static Storage()
        {
            SetFileSystem(defaultFileSystem);
        }
    }
}