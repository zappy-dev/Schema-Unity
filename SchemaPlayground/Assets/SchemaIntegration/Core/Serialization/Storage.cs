using System.Collections.Generic;

namespace Schema.Core.Serialization
{
    public static class Storage
    {
        public static IStorageFormat<DataScheme> JSONStorageFormat = new JsonStorageFormat();
        public static IStorageFormat<DataScheme> CSVStorageFormat = new CSVStorageFormat();

        public static IStorageFormat<DataScheme> DefaultSchemaStorageFormat = JSONStorageFormat;

        public static IStorageFormat<DataScheme> DefaultManifestStorageFormat = JSONStorageFormat;

        public static IEnumerable<IStorageFormat<DataScheme>> AllFormats = new[]
        {
            JSONStorageFormat,
            CSVStorageFormat,
        };
    }
}