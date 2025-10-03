namespace Schema.Core.Serialization
{
    public interface IStorageFormat<T>
    {
        /// File extension for the format
        string Extension { get; }
        string DisplayName { get; }

        bool IsImportSupported { get; }
        bool IsExportSupported { get; }
        SchemaResult<T> DeserializeFromFile(SchemaContext context, string filePath);
        SchemaResult<T> Deserialize(SchemaContext context, string content);
        SchemaResult SerializeToFile(SchemaContext context, string filePath, T data);
        SchemaResult<string> Serialize(T data);
    }
}